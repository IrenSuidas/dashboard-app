using System.Net.Http.Headers;
using EndingApp.Services;
using Raylib_cs;

namespace EndingApp.Scenes.SongRequest;

internal sealed partial class SongRequestScene(AppConfig config) : IDisposable
{
    private readonly AppConfig _config = config;
    private bool _disposed;

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
        Raylib.SetWindowSize(640, 220);

        // Center window on screen
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition((monitorWidth - 640) / 2, (monitorHeight - 220) / 2);

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
