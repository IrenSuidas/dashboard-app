using EndingApp.Services;
using Raylib_cs;

namespace EndingApp;

internal sealed partial class EndingScene(AppConfig config) : IDisposable
{
    private AppConfig _config = config;
    private bool _disposed;
    private bool _cleanedUp;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _fontLoader?.Dispose();
            }
            _disposed = true;
        }
    }

    ~EndingScene()
    {
        Dispose(false);
    }

    public bool IsActive { get; private set; }

    public void Start()
    {
        // Reload config for hot-reloading support
        _config = AppConfig.Load();

        // Resize window using config
        Raylib.SetWindowSize(_config.Ending.Width, _config.Ending.Height);

        // Center window on screen
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition(
            (monitorWidth - _config.Ending.Width) / 2,
            (monitorHeight - _config.Ending.Height) / 2
        );

        // Ensure audio device is initialized before loading music/resources that may require audio
        AudioService.Register();
        // Load resources (textures, fonts, music, emote)
        LoadResources();

        // Start credits at bottom of screen (outside view)
        _creditsScrollY = _config.Ending.Height;

        // Prepare music but do not play yet - ensure audio device is initialized
        AudioService.Register();
        _music = ResourceCache.LoadMusic(_config.Ending.Music);
        // Reset playback position so the music always starts from the beginning when the scene starts.
        try
        {
            if (AudioService.IsAudioDeviceReady)
            {
                AudioService.ResetPlayback(_music);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"MUSIC: failed to reset playback: {ex.Message}");
        }

        // Calculate dynamic credits scroll speed
        float songDuration = AudioService.GetTimeLength(_music);
        _songDuration = songDuration;
        _musicStopped = false;
        float scrollTime = Math.Max(songDuration - 15f, 2f); // minimum 2 seconds to avoid div by zero
        float creditsHeight = CalculateCreditsHeight();
        _creditsHeight = creditsHeight;
        float scrollDistance = _config.Ending.Height + creditsHeight;
        _creditsScrollSpeed = scrollDistance / scrollTime;

        // Reset intro state
        _elapsedTime = 0f;
        _musicStarted = false;
        _musicStopped = false;
        _musicPlayElapsed = 0f;
        _creditsStarted = false;
        _endTextStarted = false;
        _showEndText = false;
        _endTextFader.Reset();
        _endTextHasFadedOut = false;
        _endBackgroundActive = false;
        _endBackgroundFader.Reset();
        _endBackgroundPlayed = false;
        _showStartText = false;
        _startTextFader.Reset();
        _startTextPlayed = false;
        _startTextHasFadedOut = false;

        IsActive = true;

        // Emote loaded during LoadResources()
        // Reset cleanup flag (we have re-initialized a new scene)
        _cleanedUp = false;
        // Reset copyright state
        _copyrightStarted = false;
        _copyrightFadingIn = false;
        _copyrightFadeElapsed = 0f;
        _copyrightAlpha = 0f;
    }

    public void Stop()
    {
        // Ensure resources are cleaned and the window is restored when stopping normally.
        if (!_cleanedUp)
        {
            Cleanup();
        }

        RestoreWindowAndAudioState();
        IsActive = false;
    }

    /// <summary>
    /// Restore the main window settings and shut down audio safely.
    /// Should be called when returning to the main menu.
    /// </summary>
    public static void RestoreWindowAndAudioState()
    {
        // Restore transparent window flag for main menu
        Raylib.SetWindowState(ConfigFlags.TransparentWindow);

        // Restore original window size
        Raylib.SetWindowSize(400, 200);

        // Center the small window
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition((monitorWidth - 400) / 2, (monitorHeight - 200) / 2);

        // Close audio device if possible (decrement refcount and close if nobody else needs it)
        AudioService.Unregister();
    }
}
