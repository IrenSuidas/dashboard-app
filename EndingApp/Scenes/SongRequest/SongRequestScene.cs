using System.Numerics;
using System.Text.Json;
using EndingApp.Services;
using Raylib_cs;

namespace EndingApp.Scenes.SongRequest;

internal sealed class SongRequestScene(AppConfig config) : IDisposable
{
    private bool _disposed;
    private readonly AppConfig _config = config;
    private string _statusMessage = "Idle";
    private readonly HttpClient _httpClient = new();
    private readonly CancellationTokenSource _cts = new();

    // Audio State
    private Music _currentMusic;
    private Music _fadingMusic;
    private bool _isMusicLoaded;
    private bool _isFadingMusicLoaded;

    // Playback State
    private bool _isPlayingRequested; // True if currently playing a requested song
    private float _recurrentSeekPosition; // Saved position for recurrent song
    private int _currentRecurrentIndex; // Index in _recurrentSongs
    private float _savedCurrentPosition; // Saved position when leaving scene

    // Data
    private readonly List<PlaylistItem> _requestedSongs = [];
    private readonly List<PlaylistItem> _recurrentSongs = [];
    private readonly HashSet<string> _playedRequestedPaths = [];
    private readonly Stack<PlaylistItem> _history = new();
    private PlaylistItem? _currentSongItem;
    private readonly object _listLock = new();

    // Polling
    private double _lastFetchTime;
    private bool _isFetching;
    private const double FetchInterval = 30.0;

    // Volume
    private float _volume = 0.5f;
    private bool _isPaused;

    // Crossfading
    private bool _isCrossfading;
    private float _crossfadeTimer;
    private const float CrossfadeDuration = 2.0f;

    private sealed record PlaylistItem(
        string Path,
        string Title,
        string Requester,
        string Type,
        int? QueuePosition
    );

    public bool IsActive { get; private set; }

    public void Start()
    {
        // Resize window to match EndingScene dimensions
        Raylib.SetWindowSize(_config.Ending.Width, _config.Ending.Height);

        // Center window on screen
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition(
            (monitorWidth - _config.Ending.Width) / 2,
            (monitorHeight - _config.Ending.Height) / 2
        );

        if (AudioService.Register())
        {
            Logger.Info("SongRequestScene: Audio initialized");
        }
        else
        {
            Logger.Warn("SongRequestScene: Failed to initialize audio");
        }

        IsActive = true;
        Logger.Info("SongRequestScene: Started");

        // Resume if we were playing
        if (_currentSongItem != null)
        {
            Logger.Info(
                $"SongRequestScene: Resuming {_currentSongItem.Title} at {_savedCurrentPosition}"
            );
            _currentMusic = Raylib.LoadMusicStream(_currentSongItem.Path);
            Raylib.PlayMusicStream(_currentMusic);
            Raylib.SeekMusicStream(_currentMusic, _savedCurrentPosition);
            Raylib.SetMusicVolume(_currentMusic, 1.0f);
            _isMusicLoaded = true;
            UpdateSongFile(_currentSongItem.Title);
        }

        // Start fetching songs
        _lastFetchTime = Raylib.GetTime();
        _ = FetchAndDownloadSongs();
    }

    private async Task FetchAndDownloadSongs()
    {
        if (_isFetching || string.IsNullOrEmpty(_config.Api.EndpointUrl))
            return;

        _isFetching = true;

        try
        {
            _statusMessage = "Fetching songs...";
            string url = $"{_config.Api.EndpointUrl}/songs";
            string json = await _httpClient.GetStringAsync(url, _cts.Token);

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

            _statusMessage =
                $"Found {requested.Count} requested, {recurrent.Count} recurrent. Downloading...";

            string tempPath = Path.Combine(AppContext.BaseDirectory, "temp");
            string requestedPath = Path.Combine(tempPath, "requested");
            string recurrentPath = Path.Combine(tempPath, "recurrent");

            Directory.CreateDirectory(requestedPath);
            Directory.CreateDirectory(recurrentPath);

            // Helper to download
            async Task<PlaylistItem> DownloadAndCreateItem(Song song, string folder)
            {
                string downloadUrl = $"{_config.Api.EndpointUrl}{song.Path}";
                string filePath = Path.Combine(folder, song.Filename);

                if (!File.Exists(filePath))
                {
                    _statusMessage = $"Downloading {song.Title}...";
                    Logger.Info($"SongRequestScene: Downloading {song.Title}");
                    byte[] data = await _httpClient.GetByteArrayAsync(downloadUrl, _cts.Token);
                    await File.WriteAllBytesAsync(filePath, data, _cts.Token);
                }
                return new PlaylistItem(
                    filePath,
                    song.Title,
                    song.AddedBy,
                    song.Type,
                    song.QueuePosition
                );
            }

            // 1. Remove songs that are no longer in the API response
            var requestedFilenames = requested.Select(s => s.Filename).ToHashSet();
            lock (_listLock)
            {
                _requestedSongs.RemoveAll(item =>
                    !requestedFilenames.Contains(Path.GetFileName(item.Path))
                );
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
                            // Keep sorted
                            _requestedSongs.Sort(
                                (a, b) => (a.QueuePosition ?? 0).CompareTo(b.QueuePosition ?? 0)
                            );
                        }
                    }
                }
            }

            // 3. Process Recurrent Songs (Bulk)
            var newRecurrentList = new List<PlaylistItem>();
            foreach (var song in recurrent)
            {
                newRecurrentList.Add(await DownloadAndCreateItem(song, recurrentPath));
            }

            lock (_listLock)
            {
                _recurrentSongs.Clear();
                _recurrentSongs.AddRange(newRecurrentList);
            }

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

    public void Update()
    {
        if (!IsActive)
            return;

        // Polling
        if (Raylib.GetTime() - _lastFetchTime > FetchInterval && !_isFetching)
        {
            _lastFetchTime = Raylib.GetTime();
            _ = FetchAndDownloadSongs();
        }

        // Exit check
        if (
            (
                Raylib.IsKeyDown(KeyboardKey.LeftControl)
                || Raylib.IsKeyDown(KeyboardKey.RightControl)
            ) && Raylib.IsKeyPressed(KeyboardKey.Space)
        )
        {
            Logger.Info("SongRequestScene: ctrl+space pressed - returning to main menu");
            IsActive = false;
            return;
        }

        // Audio Update
        if (_isMusicLoaded)
            Raylib.UpdateMusicStream(_currentMusic);
        if (_isFadingMusicLoaded)
            Raylib.UpdateMusicStream(_fadingMusic);

        // Crossfade Logic
        if (_isCrossfading)
        {
            _crossfadeTimer += Raylib.GetFrameTime();
            float alpha = Math.Clamp(_crossfadeTimer / CrossfadeDuration, 0f, 1f);

            // Fade in current, Fade out fading
            Raylib.SetMusicVolume(_currentMusic, alpha * _volume);
            Raylib.SetMusicVolume(_fadingMusic, (1.0f - alpha) * _volume);

            if (alpha >= 1.0f)
            {
                _isCrossfading = false;
                Raylib.StopMusicStream(_fadingMusic);
                Raylib.UnloadMusicStream(_fadingMusic);
                _isFadingMusicLoaded = false;
            }
        }

        // Playback Logic
        lock (_listLock)
        {
            // Check if we need to switch from Recurrent to Requested
            if (!_isPlayingRequested && _requestedSongs.Count > 0 && !_isCrossfading)
            {
                // Interrupt Recurrent
                Logger.Info("SongRequestScene: Interrupting Recurrent for Requested");
                StartCrossfade(_requestedSongs[0], true);
                _requestedSongs.RemoveAt(0); // Remove from queue
                return;
            }

            // Check if current song finished
            if (
                _isMusicLoaded
                && Raylib.GetMusicTimePlayed(_currentMusic)
                    >= Raylib.GetMusicTimeLength(_currentMusic) - 0.1f
            ) // Tolerance
            {
                if (_isPlayingRequested)
                {
                    // Requested finished
                    if (_requestedSongs.Count > 0)
                    {
                        // Play next requested (Hard cut or quick fade? Hard cut for now)
                        PlayNext(_requestedSongs[0], true);
                        _requestedSongs.RemoveAt(0);
                    }
                    else
                    {
                        // No more requested, resume recurrent
                        if (_recurrentSongs.Count > 0)
                        {
                            Logger.Info("SongRequestScene: Resuming Recurrent");
                            // Ensure index is valid
                            if (_currentRecurrentIndex >= _recurrentSongs.Count)
                                _currentRecurrentIndex = 0;

                            var nextRecurrent = _recurrentSongs[_currentRecurrentIndex];
                            StartCrossfade(nextRecurrent, false, _recurrentSeekPosition);
                        }
                    }
                }
                else
                {
                    // Recurrent finished
                    if (_recurrentSongs.Count > 0)
                    {
                        _currentRecurrentIndex =
                            (_currentRecurrentIndex + 1) % _recurrentSongs.Count;
                        PlayNext(_recurrentSongs[_currentRecurrentIndex], false);
                    }
                }
            }

            // Initial Start
            if (!_isMusicLoaded && !_isCrossfading)
            {
                if (_requestedSongs.Count > 0)
                {
                    PlayNext(_requestedSongs[0], true);
                    _requestedSongs.RemoveAt(0);
                }
                else if (_recurrentSongs.Count > 0)
                {
                    PlayNext(_recurrentSongs[_currentRecurrentIndex], false);
                }
            }
        }

        // Seek bar logic (only for current music)
        if (_isMusicLoaded && Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            HandleSeeking();
            HandleVolume();
            HandleControls();
        }
    }

    private void StartCrossfade(PlaylistItem nextItem, bool isRequested, float seekPos = 0)
    {
        if (_isMusicLoaded)
        {
            // Save recurrent position if we are interrupting it
            if (!_isPlayingRequested && isRequested)
            {
                _recurrentSeekPosition = Raylib.GetMusicTimePlayed(_currentMusic);
                // Find index of current item in recurrent list to be safe
                int idx = _recurrentSongs.FindIndex(x => x.Path == _currentSongItem?.Path);
                if (idx != -1)
                    _currentRecurrentIndex = idx;
            }

            _fadingMusic = _currentMusic;
            _isFadingMusicLoaded = true;
            _isCrossfading = true;
            _crossfadeTimer = 0f;
        }

        _currentSongItem = nextItem;
        _currentMusic = Raylib.LoadMusicStream(nextItem.Path);
        Raylib.PlayMusicStream(_currentMusic);
        if (seekPos > 0)
            Raylib.SeekMusicStream(_currentMusic, seekPos);
        Raylib.SetMusicVolume(_currentMusic, 0f); // Start silent

        _isMusicLoaded = true;
        _isPlayingRequested = isRequested;
        if (isRequested)
            _playedRequestedPaths.Add(nextItem.Path);

        if (!_isFadingMusicLoaded) // If no previous music, just fade in? Or instant?
        {
            Raylib.SetMusicVolume(_currentMusic, _volume);
            _isCrossfading = false;
        }

        UpdateSongFile(nextItem.Title);
    }

    private void PlayNext(PlaylistItem nextItem, bool isRequested)
    {
        if (_isMusicLoaded)
        {
            if (_currentSongItem != null)
                _history.Push(_currentSongItem);
            Raylib.StopMusicStream(_currentMusic);
            Raylib.UnloadMusicStream(_currentMusic);
        }

        _currentSongItem = nextItem;
        _currentMusic = Raylib.LoadMusicStream(nextItem.Path);
        Raylib.PlayMusicStream(_currentMusic);
        Raylib.SetMusicVolume(_currentMusic, _volume);
        _isMusicLoaded = true;
        _isPlayingRequested = isRequested;
        _isPaused = false;
        if (isRequested)
            _playedRequestedPaths.Add(nextItem.Path);

        UpdateSongFile(nextItem.Title);
    }

    private void PlayPrevious()
    {
        if (!_isMusicLoaded)
            return;

        // If played more than 3 seconds, restart
        if (Raylib.GetMusicTimePlayed(_currentMusic) > 3.0f)
        {
            Raylib.SeekMusicStream(_currentMusic, 0f);
            return;
        }

        // Else go to history
        if (_history.Count > 0)
        {
            var prev = _history.Pop();
            // We don't push current to history when going back, effectively discarding it from history flow
            // But we need to stop current
            Raylib.StopMusicStream(_currentMusic);
            Raylib.UnloadMusicStream(_currentMusic);

            _currentSongItem = prev;
            _currentMusic = Raylib.LoadMusicStream(prev.Path);
            Raylib.PlayMusicStream(_currentMusic);
            Raylib.SetMusicVolume(_currentMusic, _volume);
            _isMusicLoaded = true;
            _isPlayingRequested = prev.Type == "requested";
            _isPaused = false;
            UpdateSongFile(prev.Title);
        }
    }

    private void HandleSeeking()
    {
        Vector2 mousePos = Raylib.GetMousePosition();
        float barX = 50;
        float barY = _config.Ending.Height - 80;
        float barWidth = _config.Ending.Width - 100;
        float barHeight = 10;

        if (
            mousePos.X >= barX
            && mousePos.X <= barX + barWidth
            && mousePos.Y >= barY - 10
            && mousePos.Y <= barY + barHeight + 10
        )
        {
            float seekRatio = (mousePos.X - barX) / barWidth;
            seekRatio = Math.Clamp(seekRatio, 0f, 1f);
            float totalTime = Raylib.GetMusicTimeLength(_currentMusic);
            Raylib.SeekMusicStream(_currentMusic, totalTime * seekRatio);
        }
    }

    private void HandleVolume()
    {
        Vector2 mousePos = Raylib.GetMousePosition();
        float volX = _config.Ending.Width - 140;
        float volY = 20;
        float volWidth = 100;
        float volHeight = 10;

        if (
            mousePos.X >= volX - 10
            && mousePos.X <= volX + volWidth + 10
            && mousePos.Y >= volY - 10
            && mousePos.Y <= volY + volHeight + 10
        )
        {
            float volRatio = (mousePos.X - volX) / volWidth;
            _volume = Math.Clamp(volRatio, 0f, 1f);

            if (!_isCrossfading)
            {
                Raylib.SetMusicVolume(_currentMusic, _volume);
            }
        }
    }

    private void HandleControls()
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
            return;

        Vector2 mousePos = Raylib.GetMousePosition();
        float startX = _config.Ending.Width - 120;
        float startY = _config.Ending.Height - 65; // Under time bar (170 + 15 + 20 approx)
        float btnSize = 20;
        float spacing = 10;

        // Prev [<<]
        Rectangle prevRect = new(startX, startY, btnSize, btnSize);
        if (Raylib.CheckCollisionPointRec(mousePos, prevRect))
        {
            PlayPrevious();
            return;
        }

        // Play/Pause [> / ||]
        Rectangle playRect = new(startX + btnSize + spacing, startY, btnSize, btnSize);
        if (Raylib.CheckCollisionPointRec(mousePos, playRect))
        {
            _isPaused = !_isPaused;
            if (_isPaused)
                Raylib.PauseMusicStream(_currentMusic);
            else
                Raylib.ResumeMusicStream(_currentMusic);
            return;
        }

        // Next [>>]
        Rectangle nextRect = new(startX + (btnSize + spacing) * 2, startY, btnSize, btnSize);
        if (Raylib.CheckCollisionPointRec(mousePos, nextRect))
        {
            // Trigger next logic manually
            lock (_listLock)
            {
                if (_requestedSongs.Count > 0)
                {
                    PlayNext(_requestedSongs[0], true);
                    _requestedSongs.RemoveAt(0);
                }
                else if (_recurrentSongs.Count > 0)
                {
                    _currentRecurrentIndex = (_currentRecurrentIndex + 1) % _recurrentSongs.Count;
                    PlayNext(_recurrentSongs[_currentRecurrentIndex], false);
                }
            }
        }
    }

    private static void UpdateSongFile(string title)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "assets", "song.txt");
            // Ensure directory exists just in case
            string? dir = Path.GetDirectoryName(path);
            if (dir != null)
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, title);
        }
        catch (Exception ex)
        {
            Logger.Error($"SongRequestScene: Failed to update song.txt - {ex.Message}");
        }
    }

    public void Draw()
    {
        Raylib.ClearBackground(Color.Black);
        Raylib.DrawText("Song Request Scene", 20, 20, 40, Color.White);
        Raylib.DrawText($"Status: {_statusMessage}", 20, 70, 20, Color.Yellow);

        // Volume Slider
        float volX = _config.Ending.Width - 140;
        float volY = 20;
        float volWidth = 100;
        float volHeight = 10;
        Raylib.DrawText("Vol:", (int)volX - 40, (int)volY - 5, 20, Color.White);
        Raylib.DrawRectangle((int)volX, (int)volY, (int)volWidth, (int)volHeight, Color.DarkGray);
        Raylib.DrawRectangle(
            (int)volX,
            (int)volY,
            (int)(volWidth * _volume),
            (int)volHeight,
            Color.White
        );
        Raylib.DrawText(
            $"{(int)(_volume * 100)}%",
            (int)volX + (int)volWidth + 5,
            (int)volY - 5,
            10,
            Color.LightGray
        );

        lock (_listLock)
        {
            Raylib.DrawText($"Queue: {_requestedSongs.Count}", 20, 100, 20, Color.LightGray);
        }

        if (_isMusicLoaded && _currentSongItem != null)
        {
            float totalTime = Raylib.GetMusicTimeLength(_currentMusic);
            float currentTime = Raylib.GetMusicTimePlayed(_currentMusic);

            Raylib.DrawText(
                $"Now Playing ({(_isPlayingRequested ? "Requested" : "Recurrent")}):",
                50,
                _config.Ending.Height - 160,
                20,
                Color.SkyBlue
            );
            Raylib.DrawText(
                _currentSongItem.Title,
                50,
                _config.Ending.Height - 135,
                24,
                Color.White
            );
            Raylib.DrawText(
                $"Requested by: {_currentSongItem.Requester}",
                50,
                _config.Ending.Height - 110,
                20,
                Color.LightGray
            );

            // Seek bar
            float barX = 50;
            float barY = _config.Ending.Height - 80;
            float barWidth = _config.Ending.Width - 100;
            float barHeight = 10;

            Raylib.DrawRectangle(
                (int)barX,
                (int)barY,
                (int)barWidth,
                (int)barHeight,
                Color.DarkGray
            );
            if (totalTime > 0)
            {
                float progress = currentTime / totalTime;
                Raylib.DrawRectangle(
                    (int)barX,
                    (int)barY,
                    (int)(barWidth * progress),
                    (int)barHeight,
                    Color.Green
                );
            }

            string timeStr =
                $"{TimeSpan.FromSeconds(currentTime):mm\\:ss} / {TimeSpan.FromSeconds(totalTime):mm\\:ss}";
            Raylib.DrawText(timeStr, (int)barX, (int)barY + 15, 20, Color.White);

            // Controls
            float startX = _config.Ending.Width - 120;
            float startY = _config.Ending.Height - 65;
            float btnSize = 20;
            float spacing = 10;

            // Prev
            Raylib.DrawText("<<", (int)startX, (int)startY, 20, Color.White);

            // Play/Pause
            string playText = _isPaused ? ">" : "||";
            Raylib.DrawText(
                playText,
                (int)(startX + btnSize + spacing),
                (int)startY,
                20,
                Color.White
            );

            // Next
            Raylib.DrawText(
                ">>",
                (int)(startX + (btnSize + spacing) * 2),
                (int)startY,
                20,
                Color.White
            );
        }

        Raylib.DrawText(
            "Press Ctrl+Space to return",
            20,
            _config.Ending.Height - 30,
            20,
            Color.Gray
        );
    }

    public void Cleanup()
    {
        _cts.Cancel();

        if (_isMusicLoaded)
        {
            _savedCurrentPosition = Raylib.GetMusicTimePlayed(_currentMusic);
            Raylib.StopMusicStream(_currentMusic);
            Raylib.UnloadMusicStream(_currentMusic);
            _isMusicLoaded = false;
        }
        if (_isFadingMusicLoaded)
        {
            Raylib.StopMusicStream(_fadingMusic);
            Raylib.UnloadMusicStream(_fadingMusic);
            _isFadingMusicLoaded = false;
        }

        AudioService.Unregister();

        // Restore window size for main menu
        Raylib.SetWindowSize(400, 200);

        // Center window on screen
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition((monitorWidth - 400) / 2, (monitorHeight - 200) / 2);

        Logger.Info(
            "SongRequestScene: Cleanup complete. Config endpoint was: {0}",
            _config.Api.EndpointUrl
        );
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
            _httpClient.Dispose();
        }
    }
}
