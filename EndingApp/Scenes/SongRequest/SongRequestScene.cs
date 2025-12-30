using System.Net.Http.Headers;
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
    private readonly HttpClient _httpClient = new() { Timeout = Timeout.InfiniteTimeSpan };
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
    private int _currentRequestedIndex; // Index in _requestedSongs
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

    // Probing
    private double _lastProbeTime;
    private const double ProbeInterval = 0.2; // Probe 5 songs per second maximum

    // Volume
    private float _volume = 0.5f;
    private bool _isPaused;

    // Crossfading
    private bool _isCrossfading;
    private float _crossfadeTimer;
    private const float CrossfadeDuration = 2.0f;

    // UI State
    private float _scrollRequested;
    private float _scrollRecurrent;

    // Font
    private FontLoader? _fontLoader;

    // UI Colors
    private readonly Color _colorPrimary = new(156, 211, 227, 255); // #9cd3e3
    private readonly Color _colorSecondary = new(249, 180, 192, 255); // #f9b4c0
    private readonly Color _colorAccent = new(216, 63, 81, 255); // #d83f51
    private readonly Color _colorBrand = new(38, 110, 255, 255); // #266eff
    private readonly Color _colorBackground = new(255, 247, 244, 255); // #fff7f4
    private readonly Color _colorTextMain = new(30, 30, 30, 255);
    private readonly Color _colorTextSub = new(100, 100, 100, 255);
    private readonly Color _colorHighlight = new(230, 240, 255, 255); // Light blue for highlighting

    private sealed record PlaylistItem(string Path, Song Data)
    {
        public string Title => Data.Title;
        public string Requester => Data.AddedBy;
        public string Type => Data.Type;
        public int? QueuePosition => Data.QueuePosition;
        public int? Duration => Data.Duration;
    }

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

        // Initialize fonts
        _fontLoader = new FontLoader();
        string assetsPath = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");
        // Using NotoSansJP as primary font per user request
        string primaryPath = Path.Combine(assetsPath, "NotoSansJP-Regular.ttf");
        // Same for fallback since it handles both
        string symbolPath = Path.Combine(assetsPath, "NotoSansJP-Regular.ttf");

        // Basic ASCII + some common symbols
        var codepoints = Enumerable.Range(32, 95).ToList();
        // Add common Japanese/Symbol ranges if needed, for now just basic set + dynamic fallback if implemented
        // But FontLoader needs explicit codepoints for the Symbol font pre-loading usually
        // Let's add a reasonable set of common symbols just in case
        codepoints.Add(0x2026); // Ellipsis ...
        codepoints.Add(0x00A9); // Copyright

        _fontLoader.Load(
            primaryPath,
            symbolPath,
            64, // Load at high res for scaling down
            [.. codepoints]
        );

        // Configure Authorization header if API key is present
        if (!string.IsNullOrEmpty(_config.Api.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _config.Api.ApiKey
            );
        }

        IsActive = true;
        Logger.Info("SongRequestScene: Started");

        // Resume if we were playing
        if (_currentSongItem != null)
        {
            Logger.Info(
                $"SongRequestScene: Resuming {_currentSongItem.Title} at {_savedCurrentPosition}"
            );
            _currentMusic = AudioService.Load(_currentSongItem.Path);
            AudioService.Play(_currentMusic);
            AudioService.Seek(_currentMusic, _savedCurrentPosition);
            AudioService.SetVolume(_currentMusic, 1.0f);
            _isMusicLoaded = true;
            UpdateSongFile(_currentSongItem.Title);
        }
        else
        {
            // Try to load local songs immediately so we have something to play
            LoadLocalSongs();
        }

        // Start fetching songs
        _lastFetchTime = Raylib.GetTime();
        _ = FetchAndDownloadSongs();
    }

    private void LoadLocalSongs()
    {
        try
        {
            string tempPath = Path.Combine(AppContext.BaseDirectory, "temp");
            string manifestPath = Path.Combine(tempPath, "manifest.json");

            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    var manifest = JsonSerializer.Deserialize(
                        json,
                        SongContext.Default.SongManifest
                    );

                    if (manifest != null)
                    {
                        string requestedPath = Path.Combine(tempPath, "requested");
                        string recurrentPath = Path.Combine(tempPath, "recurrent");

                        lock (_listLock)
                        {
                            // Load Requested
                            foreach (var s in manifest.Requested)
                            {
                                string fullPath = Path.Combine(requestedPath, s.Filename);
                                if (
                                    File.Exists(fullPath)
                                    && !_requestedSongs.Any(x => x.Path == fullPath)
                                )
                                {
                                    _requestedSongs.Add(new PlaylistItem(fullPath, s));
                                }
                            }

                            // Load Recurrent
                            foreach (var s in manifest.Recurrent)
                            {
                                string fullPath = Path.Combine(recurrentPath, s.Filename);
                                if (
                                    File.Exists(fullPath)
                                    && !_recurrentSongs.Any(x => x.Path == fullPath)
                                )
                                {
                                    _recurrentSongs.Add(new PlaylistItem(fullPath, s));
                                }
                            }
                        }
                        Logger.Info(
                            $"SongRequestScene: Loaded from manifest - {_requestedSongs.Count} requested, {_recurrentSongs.Count} recurrent."
                        );
                        return; // Successfully loaded from manifest
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"SongRequestScene: Failed to read manifest - {ex.Message}");
                }
            }

            // Fallback to scanning files if manifest fails or doesn't exist
            Logger.Warn(
                "SongRequestScene: Manifest not found or invalid, falling back to file scan."
            );
            string reqDir = Path.Combine(tempPath, "requested");
            string recDir = Path.Combine(tempPath, "recurrent");

            if (Directory.Exists(reqDir))
            {
                var files = Directory.GetFiles(reqDir, "*.*");
                lock (_listLock)
                {
                    foreach (var file in files)
                    {
                        if (!_requestedSongs.Any(x => x.Path == file))
                        {
                            var s = new Song(
                                Path.GetFileName(file),
                                "",
                                "requested",
                                "",
                                "",
                                Path.GetFileNameWithoutExtension(file),
                                "",
                                "Unknown",
                                0,
                                0,
                                null
                            );
                            _requestedSongs.Add(new PlaylistItem(file, s));
                        }
                    }
                }
            }

            if (Directory.Exists(recDir))
            {
                var files = Directory.GetFiles(recDir, "*.*");
                lock (_listLock)
                {
                    foreach (var file in files)
                    {
                        if (!_recurrentSongs.Any(x => x.Path == file))
                        {
                            var s = new Song(
                                Path.GetFileName(file),
                                "",
                                "recurrent",
                                "",
                                "",
                                Path.GetFileNameWithoutExtension(file),
                                "",
                                "System",
                                0,
                                null,
                                null
                            );
                            _recurrentSongs.Add(new PlaylistItem(file, s));
                        }
                    }
                }
            }

            Logger.Info(
                $"SongRequestScene: Loaded {_requestedSongs.Count} requested and {_recurrentSongs.Count} recurrent songs from disk (fallback)."
            );
        }
        catch (Exception ex)
        {
            Logger.Error($"SongRequestScene: Failed to load local songs - {ex.Message}");
        }
    }

    private static void SaveManifest(List<Song> requested, List<Song> recurrent)
    {
        try
        {
            string tempPath = Path.Combine(AppContext.BaseDirectory, "temp");
            string manifestPath = Path.Combine(tempPath, "manifest.json");
            Directory.CreateDirectory(tempPath);

            var manifest = new SongManifest(requested, recurrent);
            string json = JsonSerializer.Serialize(manifest, SongContext.Default.SongManifest);
            File.WriteAllText(manifestPath, json);
        }
        catch (Exception ex)
        {
            Logger.Error($"SongRequestScene: Failed to save manifest - {ex.Message}");
        }
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

    private static void HandleScrolling(Rectangle rect, ref float scrollOffset, int contentHeight)
    {
        Vector2 mousePos = Raylib.GetMousePosition();
        if (Raylib.CheckCollisionPointRec(mousePos, rect))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                scrollOffset -= wheel * 30; // Scroll speed
            }
        }

        // Clamp scroll
        float maxScroll = Math.Max(0, contentHeight - rect.Height);
        scrollOffset = Math.Clamp(scrollOffset, 0, maxScroll);
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

        // Probing for missing durations
        if (Raylib.GetTime() - _lastProbeTime > ProbeInterval)
        {
            _lastProbeTime = Raylib.GetTime();
            ProbeNextMissingDuration();
        }

        // Audio Update
        if (_isMusicLoaded)
            AudioService.Update(_currentMusic);
        if (_isFadingMusicLoaded)
            AudioService.Update(_fadingMusic);

        // Crossfade Logic
        if (_isCrossfading)
        {
            _crossfadeTimer += Raylib.GetFrameTime();
            float alpha = Math.Clamp(_crossfadeTimer / CrossfadeDuration, 0f, 1f);

            // Fade in current, Fade out fading
            AudioService.SetVolume(_currentMusic, alpha * _volume);
            AudioService.SetVolume(_fadingMusic, (1.0f - alpha) * _volume);

            if (alpha >= 1.0f)
            {
                _isCrossfading = false;
                AudioService.Stop(_fadingMusic);
                AudioService.Unload(_fadingMusic);
                _isFadingMusicLoaded = false;
            }
        }

        // Playback Logic
        lock (_listLock)
        {
            // Check if we need to switch from Recurrent to Requested
            if (
                !_isPlayingRequested
                && _requestedSongs.Count > _currentRequestedIndex
                && !_isCrossfading
            )
            {
                // Interrupt Recurrent
                Logger.Info("SongRequestScene: Interrupting Recurrent for Requested");
                StartCrossfade(_requestedSongs[_currentRequestedIndex], true);
                return;
            }

            // Check if current song finished
            if (
                _isMusicLoaded
                && AudioService.GetTimePlayed(_currentMusic)
                    >= AudioService.GetTimeLength(_currentMusic) - 0.1f
            ) // Tolerance
            {
                if (_isPlayingRequested)
                {
                    // Requested finished
                    _currentRequestedIndex++;
                    if (_requestedSongs.Count > _currentRequestedIndex)
                    {
                        // Play next requested (Hard cut or quick fade? Hard cut for now)
                        PlayNext(_requestedSongs[_currentRequestedIndex], true);
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
                if (_requestedSongs.Count > _currentRequestedIndex)
                {
                    PlayNext(_requestedSongs[_currentRequestedIndex], true);
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
            HandleControls();
        }

        HandleVolume();
    }

    private void StartCrossfade(PlaylistItem nextItem, bool isRequested, float seekPos = 0)
    {
        if (_isMusicLoaded)
        {
            // Save recurrent position if we are interrupting it
            if (!_isPlayingRequested && isRequested)
            {
                _recurrentSeekPosition = AudioService.GetTimePlayed(_currentMusic);
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
        _currentMusic = AudioService.Load(nextItem.Path);
        AudioService.Play(_currentMusic);
        if (seekPos > 0)
            AudioService.Seek(_currentMusic, seekPos);
        AudioService.SetVolume(_currentMusic, 0f); // Start silent

        _isMusicLoaded = true;
        _isPlayingRequested = isRequested;
        if (isRequested)
            _playedRequestedPaths.Add(nextItem.Path);

        if (!_isFadingMusicLoaded) // If no previous music, just fade in? Or instant?
        {
            AudioService.SetVolume(_currentMusic, _volume);
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
            AudioService.Stop(_currentMusic);
            AudioService.Unload(_currentMusic);
        }

        _currentSongItem = nextItem;
        _currentMusic = AudioService.Load(nextItem.Path);
        AudioService.Play(_currentMusic);
        AudioService.SetVolume(_currentMusic, _volume);
        _isMusicLoaded = true;
        _isPlayingRequested = isRequested;
        _isPaused = false;
        if (isRequested)
            _playedRequestedPaths.Add(nextItem.Path);

        // Update duration if missing
        float len = AudioService.GetTimeLength(_currentMusic);
        if (len > 0 && (_currentSongItem.Duration == null || _currentSongItem.Duration == 0))
        {
            _currentSongItem = _currentSongItem with
            {
                Data = _currentSongItem.Data with { Duration = (int)len },
            };

            // Update in list too
            lock (_listLock)
            {
                if (isRequested)
                {
                    int idx = _requestedSongs.FindIndex(x => x.Path == nextItem.Path);
                    if (idx != -1)
                        _requestedSongs[idx] = _requestedSongs[idx] with
                        {
                            Data = _requestedSongs[idx].Data with { Duration = (int)len },
                        };
                }
                else
                {
                    int idx = _recurrentSongs.FindIndex(x => x.Path == nextItem.Path);
                    if (idx != -1)
                        _recurrentSongs[idx] = _recurrentSongs[idx] with
                        {
                            Data = _recurrentSongs[idx].Data with { Duration = (int)len },
                        };
                }
            }

            // Save to manifest
            List<Song> requested = _requestedSongs.Select(x => x.Data).ToList();
            List<Song> recurrent = _recurrentSongs.Select(x => x.Data).ToList();
            SaveManifest(requested, recurrent);
        }

        UpdateSongFile(nextItem.Title);
    }

    private void PlayPrevious()
    {
        if (!_isMusicLoaded)
            return;

        // If played more than 3 seconds, restart
        if (AudioService.GetTimePlayed(_currentMusic) > 3.0f)
        {
            AudioService.Seek(_currentMusic, 0f);
            return;
        }

        // Else go to history
        if (_history.Count > 0)
        {
            var prev = _history.Pop();
            // We don't push current to history when going back, effectively discarding it from history flow
            // But we need to stop current
            AudioService.Stop(_currentMusic);
            AudioService.Unload(_currentMusic);

            _currentSongItem = prev;
            _currentMusic = AudioService.Load(prev.Path);
            AudioService.Play(_currentMusic);
            AudioService.SetVolume(_currentMusic, _volume);
            _isMusicLoaded = true;
            _isPlayingRequested = prev.Type == "requested";
            _isPaused = false;

            // Sync indices
            lock (_listLock)
            {
                if (_isPlayingRequested)
                {
                    int index = _requestedSongs.FindIndex(x => x.Path == prev.Path);
                    if (index != -1)
                        _currentRequestedIndex = index;
                }
                else
                {
                    int index = _recurrentSongs.FindIndex(x => x.Path == prev.Path);
                    if (index != -1)
                        _currentRecurrentIndex = index;
                }
            }

            UpdateSongFile(prev.Title);
        }
    }

    private void HandleSeeking()
    {
        Vector2 mousePos = Raylib.GetMousePosition();
        float barX = 40;
        float barY = _config.Ending.Height - 30;
        float barWidth = _config.Ending.Width - 80;
        float barHeight = 10;

        // Expanded hitbox
        if (
            mousePos.X >= barX
            && mousePos.X <= barX + barWidth
            && mousePos.Y >= barY - 15
            && mousePos.Y <= barY + barHeight + 15
        )
        {
            float seekRatio = (mousePos.X - barX) / barWidth;
            seekRatio = Math.Clamp(seekRatio, 0f, 1f);
            float totalTime = AudioService.GetTimeLength(_currentMusic);
            AudioService.Seek(_currentMusic, totalTime * seekRatio);
        }
    }

    private void HandleVolume()
    {
        Vector2 mousePos = Raylib.GetMousePosition();
        float volWidth = 120;
        float volHeight = 12;
        float volX = _config.Ending.Width - volWidth - 40;
        float volY = 40;

        // Expanded hitbox
        if (
            mousePos.X >= volX - 10
            && mousePos.X <= volX + volWidth + 10
            && mousePos.Y >= volY - 10
            && mousePos.Y <= volY + volHeight + 10
        )
        {
            float volChange = 0;

            // Handle Mouse Wheel
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                volChange = wheel * 0.05f; // 5% change per step
            }

            // Handle Click
            if (Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                float volRatio = (mousePos.X - volX) / volWidth;
                _volume = Math.Clamp(volRatio, 0f, 1f);
                volChange = 0; // Click overrides wheel
            }

            if (volChange != 0)
            {
                _volume = Math.Clamp(_volume + volChange, 0f, 1f);
            }

            if (
                _isMusicLoaded
                && !_isCrossfading
                && (volChange != 0 || Raylib.IsMouseButtonDown(MouseButton.Left))
            )
            {
                AudioService.SetVolume(_currentMusic, _volume);
            }
        }
    }

    private void HandleControls()
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
            return;

        Vector2 mousePos = Raylib.GetMousePosition();

        float btnSize = 32;
        float spacing = 20;
        float centerX = _config.Ending.Width / 2.0f;
        float startY = _config.Ending.Height - 80;

        // Prev [<<]
        Rectangle prevRect = new(centerX - btnSize * 1.5f - spacing, startY, btnSize, btnSize);
        if (Raylib.CheckCollisionPointRec(mousePos, prevRect))
        {
            PlayPrevious();
            return;
        }

        // Play/Pause [> / ||]
        Rectangle playRect = new(centerX - btnSize / 2, startY, btnSize, btnSize);
        if (Raylib.CheckCollisionPointRec(mousePos, playRect))
        {
            _isPaused = !_isPaused;
            if (_isPaused)
                AudioService.Pause(_currentMusic);
            else
                AudioService.Resume(_currentMusic);
            return;
        }

        // Next [>>]
        Rectangle nextRect = new(centerX + btnSize * 0.5f + spacing, startY, btnSize, btnSize);
        if (Raylib.CheckCollisionPointRec(mousePos, nextRect))
        {
            // Trigger next logic manually
            lock (_listLock)
            {
                if (_requestedSongs.Count > _currentRequestedIndex + 1)
                {
                    _currentRequestedIndex++;
                    PlayNext(_requestedSongs[_currentRequestedIndex], true);
                }
                else if (_recurrentSongs.Count > 0)
                {
                    _currentRecurrentIndex = (_currentRecurrentIndex + 1) % _recurrentSongs.Count;
                    PlayNext(_recurrentSongs[_currentRecurrentIndex], false);
                }
            }
        }
    }

    private void ProbeNextMissingDuration()
    {
        // Don't probe while fading or loading music to avoid audio conflicts
        if (_isCrossfading || !_isMusicLoaded)
            return;

        PlaylistItem? target = null;
        bool isRequested = false;
        int index = -1;

        lock (_listLock)
        {
            // Check requested first
            for (int i = 0; i < _requestedSongs.Count; i++)
            {
                var item = _requestedSongs[i];
                if ((item.Duration == null || item.Duration == 0) && File.Exists(item.Path))
                {
                    // Don't probe currently playing song (it updates itself naturally)
                    if (_currentSongItem?.Path == item.Path)
                        continue;

                    target = item;
                    isRequested = true;
                    index = i;
                    break;
                }
            }

            // Then recurrent
            if (target == null)
            {
                for (int i = 0; i < _recurrentSongs.Count; i++)
                {
                    var item = _recurrentSongs[i];
                    if ((item.Duration == null || item.Duration == 0) && File.Exists(item.Path))
                    {
                        if (_currentSongItem?.Path == item.Path)
                            continue;

                        target = item;
                        isRequested = false;
                        index = i;
                        break;
                    }
                }
            }
        }

        if (target != null)
        {
            try
            {
                // Quickly load, get time, unload
                // Note: Raylib.LoadMusicStream must be on main thread, which we are in Update()
                Music temp = AudioService.Load(target.Path);
                float len = AudioService.GetTimeLength(temp);
                AudioService.Unload(temp);

                if (len > 0)
                {
                    lock (_listLock)
                    {
                        if (isRequested)
                        {
                            if (
                                index < _requestedSongs.Count
                                && _requestedSongs[index].Path == target.Path
                            )
                            {
                                _requestedSongs[index] = _requestedSongs[index] with
                                {
                                    Data = _requestedSongs[index].Data with { Duration = (int)len },
                                };
                            }
                        }
                        else
                        {
                            if (
                                index < _recurrentSongs.Count
                                && _recurrentSongs[index].Path == target.Path
                            )
                            {
                                _recurrentSongs[index] = _recurrentSongs[index] with
                                {
                                    Data = _recurrentSongs[index].Data with { Duration = (int)len },
                                };
                            }
                        }
                    }

                    // Save periodically? Or just let the next fetch/play save it.
                    // Let's save to manifest every successful probe to be safe but maybe batching is better?
                    // For now, save immediately is safest.
                    List<Song> requested = _requestedSongs.Select(x => x.Data).ToList();
                    List<Song> recurrent = _recurrentSongs.Select(x => x.Data).ToList();
                    SaveManifest(requested, recurrent);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    $"SongRequestScene: Failed to probe duration for {target.Title} - {ex.Message}"
                );
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

    private void DrawList(
        string title,
        List<PlaylistItem> items,
        Rectangle bounds,
        ref float scrollOffset,
        bool isRequestedList
    )
    {
        // Draw Header Background
        Raylib.DrawRectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, 40, _colorSecondary);

        // Draw Header Text
        _fontLoader?.DrawText(
            $"{title} ({items.Count})",
            new Vector2(bounds.X + 10, bounds.Y + 8),
            20,
            1,
            _colorTextMain,
            FontWeight.Bold
        );

        // List Area
        Rectangle listArea = new(bounds.X, bounds.Y + 40, bounds.Width, bounds.Height - 40);
        Raylib.DrawRectangleRec(listArea, Color.White);

        // Border
        Raylib.DrawRectangleLinesEx(bounds, 1, _colorTextSub);

        // Content
        float itemHeight = 40;
        float contentHeight = items.Count * itemHeight;

        // Handle Scrolling
        HandleScrolling(listArea, ref scrollOffset, (int)contentHeight);

        // Scissor Mode for Clipping
        Raylib.BeginScissorMode(
            (int)listArea.X,
            (int)listArea.Y,
            (int)listArea.Width,
            (int)listArea.Height
        );

        float startY = listArea.Y - scrollOffset;
        Vector2 mousePos = Raylib.GetMousePosition();

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            float yPos = startY + (i * itemHeight);

            // Skip if out of view
            if (yPos + itemHeight < listArea.Y || yPos > listArea.Y + listArea.Height)
                continue;

            Rectangle itemRect = new(bounds.X, yPos, bounds.Width, itemHeight);

            // Interaction logic (Hover/Click)
            bool isHovered =
                Raylib.CheckCollisionPointRec(mousePos, itemRect)
                && Raylib.CheckCollisionPointRec(mousePos, listArea);
            bool isPlaying = _currentSongItem?.Path == item.Path;

            // Background
            if (isPlaying)
            {
                Raylib.DrawRectangleRec(itemRect, _colorHighlight);
            }
            else if (isHovered)
            {
                Raylib.DrawRectangleRec(itemRect, Raylib.Fade(_colorHighlight, 0.5f));
            }

            // Click Handler
            if (isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                lock (_listLock)
                {
                    // If clicking a requested song, remove from queue and play immediately
                    // If recurrent, just play immediately
                    if (isRequestedList)
                    {
                        // Play immediately
                        PlayNext(item, true);
                        // Update index so we continue from here
                        int newIndex = _requestedSongs.IndexOf(item);
                        if (newIndex != -1)
                            _currentRequestedIndex = newIndex;
                    }
                    else
                    {
                        PlayNext(item, false);
                        // For recurrent, we need to update the index so it continues from here
                        int newIndex = _recurrentSongs.IndexOf(item);
                        if (newIndex != -1)
                            _currentRecurrentIndex = newIndex;
                    }
                }
            }

            // Text
            string durationStr =
                item.Duration.HasValue && item.Duration.Value > 0
                    ? TimeSpan.FromSeconds(item.Duration.Value).ToString("mm\\:ss")
                    : "--:--";

            // Calculate available width for title
            // Width - padding - duration width - extra padding between title and duration
            float durationWidth = _fontLoader?.MeasureText(durationStr, 16, 1).X ?? 60;
            float availableTitleWidth = bounds.Width - 20 - durationWidth - 30; // 20px left pad, 30px gap+right pad

            string displayTitle = item.Title;
            if (_fontLoader != null)
            {
                float titleWidth = _fontLoader.MeasureText(displayTitle, 18, 1).X;
                if (titleWidth > availableTitleWidth)
                {
                    string dots = "...";
                    float dotsWidth = _fontLoader.MeasureText(dots, 18, 1).X;
                    float targetWidth = availableTitleWidth - dotsWidth;

                    // Ensure we don't have negative target width
                    if (targetWidth > 0)
                    {
                        // Iteratively shorten
                        // Optimization: Start from estimated char count instead of full length
                        // Average char width approx 10px?
                        int estimatedLen = (int)(targetWidth / 10) + 5;
                        int len = Math.Min(displayTitle.Length, estimatedLen);

                        // Safety check if estimation was too small
                        if (len < displayTitle.Length)
                        {
                            // Scan forward if we guessed too low
                            while (
                                len < displayTitle.Length
                                && _fontLoader
                                    .MeasureText(displayTitle.Substring(0, len + 1), 18, 1)
                                    .X <= targetWidth
                            )
                            {
                                len++;
                            }
                        }

                        // Scan backward to fit
                        while (len > 0)
                        {
                            string sub = displayTitle.Substring(0, len);
                            if (_fontLoader.MeasureText(sub, 18, 1).X <= targetWidth)
                            {
                                displayTitle = sub + dots;
                                break;
                            }
                            len--;
                        }
                    }
                    else
                    {
                        // Very narrow space
                        displayTitle = dots;
                    }
                }
            }

            float textY = yPos + 10;
            _fontLoader?.DrawText(
                displayTitle,
                new Vector2(bounds.X + 10, textY),
                18,
                1,
                isPlaying ? _colorBrand : _colorTextMain
            );

            // Duration on the right
            _fontLoader?.DrawTextRightAligned(
                durationStr,
                bounds.X + bounds.Width - 10,
                textY,
                16,
                1,
                _colorTextSub
            );

            // Separator line
            Raylib.DrawLine(
                (int)bounds.X,
                (int)(yPos + itemHeight),
                (int)(bounds.X + bounds.Width),
                (int)(yPos + itemHeight),
                Raylib.Fade(_colorTextSub, 0.2f)
            );
        }

        Raylib.EndScissorMode();

        // Scrollbar visual (simple)
        if (contentHeight > listArea.Height)
        {
            float scrollRatio = scrollOffset / (contentHeight - listArea.Height);
            float barHeight = listArea.Height * (listArea.Height / contentHeight);
            float barY = listArea.Y + (scrollRatio * (listArea.Height - barHeight));

            Raylib.DrawRectangle(
                (int)(bounds.X + bounds.Width - 6),
                (int)barY,
                6,
                (int)barHeight,
                Raylib.Fade(_colorTextSub, 0.5f)
            );
        }
    }

    public void Draw()
    {
        Raylib.ClearBackground(_colorBackground);

        if (_fontLoader == null)
            return;

        // Header Background
        Raylib.DrawRectangle(0, 0, _config.Ending.Width, 80, _colorPrimary);
        // Header Text
        _fontLoader.DrawText(
            "Song Request",
            new Vector2(40, 25),
            36,
            1,
            _colorTextMain,
            FontWeight.Bold
        );

        // Status Text
        Color statusColor = _statusMessage.StartsWith("Error") ? _colorAccent : _colorTextMain;
        _fontLoader.DrawText(_statusMessage, new Vector2(40, 85), 18, 1, statusColor);

        // Volume Slider (Keeping existing logic)
        {
            float volWidth = 120;
            float volHeight = 12;
            float volX = _config.Ending.Width - volWidth - 40;
            float volY = 40;

            _fontLoader.DrawText("Vol:", new Vector2(volX - 45, volY - 5), 20, 1, _colorTextMain);
            Raylib.DrawRectangleRounded(
                new Rectangle(volX, volY, volWidth, volHeight),
                0.5f,
                10,
                Color.White
            );
            Raylib.DrawRectangleRounded(
                new Rectangle(volX, volY, volWidth * _volume, volHeight),
                0.5f,
                10,
                _colorAccent
            );
            _fontLoader.DrawText(
                $"{(int)(_volume * 100)}%",
                new Vector2(volX + volWidth + 10, volY - 5),
                16,
                1,
                _colorTextMain
            );
        }

        // Dual Lists Layout
        float listTopY = 120;
        float bottomAreaHeight = 160;
        float listBottomY = _config.Ending.Height - bottomAreaHeight - 20; // 20px padding above bottom area
        float listHeight = listBottomY - listTopY;

        float margin = 40;
        float availableWidth = _config.Ending.Width - (margin * 2);
        float listWidth = (availableWidth - 20) / 2; // 20px gap between lists

        lock (_listLock)
        {
            // Left List: Requested
            DrawList(
                "Requested",
                _requestedSongs,
                new Rectangle(margin, listTopY, listWidth, listHeight),
                ref _scrollRequested,
                true
            );

            // Right List: Recurrent
            DrawList(
                "Recurrent",
                _recurrentSongs,
                new Rectangle(margin + listWidth + 20, listTopY, listWidth, listHeight),
                ref _scrollRecurrent,
                false
            );
        }

        // Now Playing Section (Bottom)
        if (_isMusicLoaded && _currentSongItem != null)
        {
            float totalTime = AudioService.GetTimeLength(_currentMusic);
            float currentTime = AudioService.GetTimePlayed(_currentMusic);
            float bottomY = _config.Ending.Height - bottomAreaHeight;

            // Background for Now Playing
            Raylib.DrawRectangle(
                0,
                (int)bottomY,
                _config.Ending.Width,
                (int)bottomAreaHeight,
                Color.White
            );
            Raylib.DrawRectangleLinesEx(
                new Rectangle(0, bottomY, _config.Ending.Width, bottomAreaHeight),
                1,
                _colorSecondary
            );

            // Song Info
            float textCenterX = _config.Ending.Width / 2.0f;
            string nowPlayingText = _currentSongItem.Title;
            string requesterText = $"Requested by: {_currentSongItem.Requester}";

            // Title
            _fontLoader.DrawTextCentered(
                nowPlayingText,
                textCenterX,
                bottomY + 20,
                28,
                1,
                _colorBrand,
                FontWeight.Bold
            );
            // Requester
            _fontLoader.DrawTextCentered(
                requesterText,
                textCenterX,
                bottomY + 55,
                20,
                1,
                _colorTextSub
            );

            // Controls (Center)
            float btnSize = 32;
            float spacing = 20;
            float ctrlY = _config.Ending.Height - 80;

            // Prev
            _fontLoader.DrawText(
                "<<",
                new Vector2(textCenterX - btnSize * 1.5f - spacing, ctrlY),
                30,
                1,
                _colorAccent,
                FontWeight.Bold
            );
            // Play/Pause
            string playText = _isPaused ? ">" : "||";
            _fontLoader.DrawText(
                playText,
                new Vector2(textCenterX - btnSize / 3.0f, ctrlY),
                30,
                1,
                _colorAccent,
                FontWeight.Bold
            );
            // Next
            _fontLoader.DrawText(
                ">>",
                new Vector2(textCenterX + btnSize * 0.5f + spacing, ctrlY),
                30,
                1,
                _colorAccent,
                FontWeight.Bold
            );

            // Progress Bar
            float barHeight = 8;
            float barY = _config.Ending.Height - 30;
            float barMargin = 40;
            float barWidth = _config.Ending.Width - (barMargin * 2);

            string curTimeStr = TimeSpan.FromSeconds(currentTime).ToString("mm\\:ss");
            string totTimeStr = TimeSpan.FromSeconds(totalTime).ToString("mm\\:ss");

            _fontLoader.DrawText(
                curTimeStr,
                new Vector2(barMargin - 5, barY - 25),
                16,
                1,
                _colorTextSub
            );
            _fontLoader.DrawTextRightAligned(
                totTimeStr,
                _config.Ending.Width - barMargin + 5,
                barY - 25,
                16,
                1,
                _colorTextSub
            );

            Raylib.DrawRectangleRounded(
                new Rectangle(barMargin, barY, barWidth, barHeight),
                0.5f,
                10,
                _colorSecondary
            );
            if (totalTime > 0)
            {
                float progress = currentTime / totalTime;
                Raylib.DrawRectangleRounded(
                    new Rectangle(barMargin, barY, barWidth * progress, barHeight),
                    0.5f,
                    10,
                    _colorBrand
                );
            }
        }
        else
        {
            // Center the waiting message only if lists are empty, otherwise lists are visible
            // Actually, if list is empty, DrawList shows empty list.
            // We can show a big message if BOTH are empty?
            bool bothEmpty = false;
            lock (_listLock)
            {
                bothEmpty = _requestedSongs.Count == 0 && _recurrentSongs.Count == 0;
            }

            if (bothEmpty)
            {
                string waitingText =
                    _statusMessage == "Idle" ? "Waiting for songs..." : _statusMessage;
                if (_statusMessage.StartsWith("No songs found"))
                {
                    double nextFetch = _lastFetchTime + FetchInterval - Raylib.GetTime();
                    if (nextFetch > 0)
                        waitingText += $" ({nextFetch:0}s)";
                }

                _fontLoader.DrawTextCentered(
                    waitingText,
                    _config.Ending.Width / 2.0f,
                    _config.Ending.Height - 100,
                    24,
                    1,
                    _colorTextSub
                );
            }
        }

        // Return Hint
        _fontLoader.DrawTextRightAligned(
            "Ctrl+Space to return",
            _config.Ending.Width - 20,
            _config.Ending.Height - 25,
            16,
            1,
            _colorTextSub
        );
    }

    public void Cleanup()
    {
        _cts.Cancel();

        if (_isMusicLoaded)
        {
            _savedCurrentPosition = AudioService.GetTimePlayed(_currentMusic);
            AudioService.Stop(_currentMusic);
            AudioService.Unload(_currentMusic);
            _isMusicLoaded = false;
        }
        if (_isFadingMusicLoaded)
        {
            AudioService.Stop(_fadingMusic);
            AudioService.Unload(_fadingMusic);
            _isFadingMusicLoaded = false;
        }

        AudioService.Unregister();

        if (_fontLoader != null)
        {
            _fontLoader.Dispose();
            _fontLoader = null;
        }

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
            _fontLoader?.Dispose();
        }
    }
}
