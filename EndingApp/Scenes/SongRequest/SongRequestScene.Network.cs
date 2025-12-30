using System.Net.Http.Headers;
using System.Text.Json;
using EndingApp.Services;
using Raylib_cs;

namespace EndingApp.Scenes.SongRequest;

internal sealed partial class SongRequestScene
{
    private async Task FetchAndDownloadSongs()
    {
        if (_isFetching || string.IsNullOrEmpty(_config.Api.EndpointUrl))
            return;

        _isFetching = true;

        try
        {
            _statusMessage = "Fetching songs...";
            string url = $"{_config.Api.EndpointUrl}/songs";

            // Use a short timeout for the API fetch
            using var fetchCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            fetchCts.CancelAfter(TimeSpan.FromSeconds(5));

            string json = await _httpClient.GetStringAsync(url, fetchCts.Token);

            var songs = JsonSerializer.Deserialize(json, SongContext.Default.ListSong);
            if (songs == null)
            {
                _statusMessage = "Failed to parse songs.";
                return;
            }

            var requested = songs
                .Where(s => s.Type == "requested")
                .OrderBy(s => s.QueuePosition)
                .ToList();
            var recurrent = songs.Where(s => s.Type == "recurrent").ToList();

            if (requested.Count == 0 && recurrent.Count == 0)
            {
                _statusMessage = "No songs found.";
                Logger.Info("SongRequestScene: No songs found on server.");
                return;
            }

            _statusMessage =
                $"Found {requested.Count} requested, {recurrent.Count} recurrent. Downloading...";

            string tempPath = Path.Combine(AppContext.BaseDirectory, "temp");
            string requestedPath = Path.Combine(tempPath, "requested");
            string recurrentPath = Path.Combine(tempPath, "recurrent");

            Directory.CreateDirectory(requestedPath);
            Directory.CreateDirectory(recurrentPath);

            // Helper to download
            async Task<PlaylistItem?> DownloadAndCreateItem(Song song, string folder)
            {
                string downloadUrl = $"{_config.Api.EndpointUrl}{song.Path}";
                string filePath = Path.Combine(folder, song.Filename);

                try
                {
                    if (!File.Exists(filePath))
                    {
                        _statusMessage = $"Downloading {song.Title}...";
                        Logger.Info(
                            $"SongRequestScene: Start downloading {song.Title} from {downloadUrl}"
                        );

                        // Use a short timeout for connecting
                        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(
                            _cts.Token
                        );
                        connectCts.CancelAfter(TimeSpan.FromSeconds(5));

                        using var response = await _httpClient.GetAsync(
                            downloadUrl,
                            HttpCompletionOption.ResponseHeadersRead,
                            connectCts.Token
                        );
                        response.EnsureSuccessStatusCode();

                        // Use the main cancellation token for the actual download (allows it to take longer)
                        using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
                        using var fileStream = new FileStream(
                            filePath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None
                        );
                        await stream.CopyToAsync(fileStream, _cts.Token);

                        Logger.Info($"SongRequestScene: Finished downloading {song.Title}");
                    }
                    else
                    {
                        Logger.Info(
                            $"SongRequestScene: File exists for {song.Title}, skipping download."
                        );
                    }

                    // Get Duration if not provided by API
                    // If song.Duration is null or 0, we can try to probe it using Raylib (if loaded) or just leave it
                    // Since Raylib.LoadMusicStream requires audio device and main thread context for safe loading,
                    // we might skip accurate duration probing here to avoid threading issues or just rely on API.
                    // However, we can try a quick probe if we are on main thread? No, this is async task.
                    // Best rely on API. If API is missing it, we might update it when playing.

                    return new PlaylistItem(filePath, song);
                }
                catch (Exception ex)
                {
                    Logger.Error(
                        $"SongRequestScene: Failed to download/process {song.Title} - {ex.Message}"
                    );
                    _statusMessage = $"Failed to download {song.Title}";
                    return null;
                }
            }

            // 1. Update/Add requested songs
            var requestedFilenames = requested.Select(s => s.Filename).ToHashSet();

            // First, update existing items with metadata if we loaded them locally without it
            lock (_listLock)
            {
                // Remove songs that shouldn't be here (deleted from server)
                _requestedSongs.RemoveAll(item =>
                    !requestedFilenames.Contains(Path.GetFileName(item.Path))
                );

                // Update metadata for existing items
                for (int i = 0; i < _requestedSongs.Count; i++)
                {
                    var item = _requestedSongs[i];
                    var filename = Path.GetFileName(item.Path);
                    var songData = requested.FirstOrDefault(s => s.Filename == filename);

                    if (
                        songData != null
                        && (item.Title == "Unknown" || item.Requester == "Unknown")
                    )
                    {
                        // Create new item with correct metadata
                        _requestedSongs[i] = new PlaylistItem(item.Path, songData);
                    }
                }
            }

            // 2. Download and add new songs incrementally
            foreach (var song in requested)
            {
                string filePath = Path.Combine(requestedPath, song.Filename);

                // Check if we need to process this song
                bool shouldProcess = false;
                lock (_listLock)
                {
                    // If it's not in queue, not played, and not current
                    if (
                        !_requestedSongs.Any(x => x.Path == filePath)
                        && !_playedRequestedPaths.Contains(filePath)
                        && _currentSongItem?.Path != filePath
                    )
                    {
                        shouldProcess = true;
                    }
                }

                if (shouldProcess)
                {
                    var newItem = await DownloadAndCreateItem(song, requestedPath);
                    if (newItem != null)
                    {
                        // Ensure critical fields are never null
                        if (newItem.Title == null)
                            newItem = newItem with
                            {
                                Data = newItem.Data with { Title = "Unknown Title" },
                            };
                        if (newItem.Requester == null)
                            newItem = newItem with
                            {
                                Data = newItem.Data with { AddedBy = "Unknown" },
                            };

                        lock (_listLock)
                        {
                            // Double check before adding
                            if (
                                !_requestedSongs.Any(x => x.Path == newItem.Path)
                                && !_playedRequestedPaths.Contains(newItem.Path)
                                && _currentSongItem?.Path != newItem.Path
                            )
                            {
                                _requestedSongs.Add(newItem);
                            }
                        }
                    }
                }
            }

            // Re-sort requested after updates/adds
            lock (_listLock)
            {
                _requestedSongs.Sort(
                    (a, b) => (a.QueuePosition ?? 0).CompareTo(b.QueuePosition ?? 0)
                );
            }

            // 3. Process Recurrent Songs (Bulk)
            // We want to keep what we have playing, but update the list
            var newRecurrentList = new List<PlaylistItem>();
            foreach (var song in recurrent)
            {
                // Determine if we already have it locally to avoid redownload if file exists
                string filePath = Path.Combine(recurrentPath, song.Filename);
                if (File.Exists(filePath))
                {
                    newRecurrentList.Add(new PlaylistItem(filePath, song));
                }
                else
                {
                    var item = await DownloadAndCreateItem(song, recurrentPath);
                    if (item != null)
                    {
                        if (item.Title == null)
                            item = item with { Data = item.Data with { Title = "Unknown Title" } };
                        if (item.Requester == null)
                            item = item with { Data = item.Data with { AddedBy = "System" } };
                        newRecurrentList.Add(item);
                    }
                }
            }

            lock (_listLock)
            {
                // Smart update of recurrent list to try and preserve state if possible
                // But for simplicity, just replacing is usually okay as long as we handle current song correctly
                // The main update loop handles playing via index, so if the list changes drastically, index might be wrong
                // But usually recurrent playlist is static-ish.
                _recurrentSongs.Clear();
                _recurrentSongs.AddRange(newRecurrentList);
            }

            // Save manifest for next launch
            SaveManifest(requested, recurrent);

            _statusMessage = "Songs updated.";
        }
        catch (OperationCanceledException)
        {
            Logger.Info("SongRequestScene: Fetch cancelled.");
            _statusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            Logger.Error($"SongRequestScene: Error - {ex.Message}");
            _statusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            _isFetching = false;
        }
    }
}
