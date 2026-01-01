using Raylib_cs;

namespace EndingApp.Scenes.ClipScene;

internal sealed partial class ClipScene(AppConfig config) : IDisposable
{
    private readonly AppConfig _config = config;
    private bool _disposed;

    public void Start()
    {
        // Resize window to match EndingScene dimensions (reusing config for now)
        Raylib.SetWindowSize(_config.Ending.Width, _config.Ending.Height);

        // Center window on screen
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition(
            (monitorWidth - _config.Ending.Width) / 2,
            (monitorHeight - _config.Ending.Height) / 2
        );

        IsActive = true;
        Logger.Info("ClipScene: Started");

        _fontLoader = new FontLoader();
        string fontPath = Path.Combine(
            AppContext.BaseDirectory,
            "assets",
            "fonts",
            "NotoSansJP-Regular.ttf"
        );
        var codepoints = new List<int>();
        // Basic Latin
        for (int i = 32; i <= 95; i++)
            codepoints.Add(i);
        // Symbols
        codepoints.Add(0x2026); // Ellipsis
        codepoints.Add(0x00A9); // Copyright

        _fontLoader.Load(fontPath, fontPath, 64, codepoints.ToArray());

        _lastFetchTime = Raylib.GetTime();
        _ = FetchAndDownloadClips();
    }

    public void Cleanup()
    {
        _cts.Cancel();

        _fontLoader?.Dispose();
        _fontLoader = null;

        // Restore window size for main menu
        Raylib.SetWindowSize(400, 200);

        // Center window on screen
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition((monitorWidth - 400) / 2, (monitorHeight - 200) / 2);

        Logger.Info("ClipScene: Cleanup complete");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
            _httpClient.Dispose();
            _videoPlayer?.Dispose();
            _fontLoader?.Dispose();
        }
    }
}
