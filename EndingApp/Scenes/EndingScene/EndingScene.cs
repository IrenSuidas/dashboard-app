using Raylib_cs;

namespace EndingApp;

internal sealed partial class EndingScene(AppConfig config) : IDisposable
{
    private AppConfig _config = config;

    // moved resource fields to EndingScene.Resources.cs
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

    // moved internal state fields to EndingScene.State.cs

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

        // Load resources (textures, fonts, music, emote)
        LoadResources();

        // Start credits at bottom of screen (outside view)
        _creditsScrollY = _config.Ending.Height;

        // Prepare music but do not play yet - init audio only if not already ready
        if (!Raylib.IsAudioDeviceReady())
        {
            Raylib.InitAudioDevice();
        }
        _music = ResourceCache.LoadMusic(_config.Ending.Music);
        // Reset playback position so the music always starts from the beginning when the scene starts.
        try
        {
            if (Raylib.IsAudioDeviceReady())
            {
                // Stop and seek to start explicitly, even if returned by cache
                try
                {
                    Raylib.StopMusicStream(_music);
                }
                catch { }
                try
                {
                    Raylib.SeekMusicStream(_music, 0f);
                }
                catch { }
                Console.WriteLine($"INFO: MUSIC: Reset playback for {_config.Ending.Music}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARN: MUSIC: failed to reset playback: {ex.Message}");
        }

        // Calculate dynamic credits scroll speed
        float songDuration = Raylib.GetMusicTimeLength(_music);
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

    // Update implementation moved to EndingScene.Update.cs

    // CalculateCreditsHeight implementation moved to EndingScene.Credits.cs

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

        // Close audio device if it is ready
        if (Raylib.IsAudioDeviceReady())
        {
            try
            {
                Raylib.CloseAudioDevice();
            }
            catch { }
        }
    }

    // Draw implementation moved to EndingScene.Draw.cs

    // DrawCredits implementation moved to EndingScene.Credits.cs

    // Cleanup implementation moved to EndingScene.Resources.cs

    // Regex helper moved to EndingScene.Regex.cs
}
