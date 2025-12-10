using Raylib_cs;

namespace EndingApp.Scenes.TestingScene;

internal sealed class TestingScene : IDisposable
{
    private bool _disposed;
    private VideoPlayer? _videoPlayer;
    private string[] _videoFiles = [];
    private int _currentVideoIndex;
    private bool _isLoading;

    public bool IsActive { get; private set; }

    public void Start()
    {
        // Resize window
        const int screenWidth = 1280;
        const int screenHeight = 720;
        Raylib.SetWindowSize(screenWidth, screenHeight);

        // Center window on screen
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition(
            (monitorWidth - screenWidth) / 2,
            (monitorHeight - screenHeight) / 2
        );

        // Load video files from carousel directory
        string carouselPath = Path.Combine(AppContext.BaseDirectory, "assets", "carousel");

        if (Directory.Exists(carouselPath))
        {
            // Get all video files (mp4, avi, mov, etc.)
            _videoFiles =
            [
                .. Directory
                    .GetFiles(carouselPath, "*.*")
                    .Where(file =>
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        return ext == ".mp4"
                            || ext == ".avi"
                            || ext == ".mov"
                            || ext == ".mkv"
                            || ext == ".webm";
                    }),
            ];

            Logger.Info(
                $"TestingScene: Found {_videoFiles.Length} video file(s) in carousel directory"
            );

            if (_videoFiles.Length > 0)
            {
                // Initialize video player
                _videoPlayer = new VideoPlayer { IsLooping = true };

                LoadVideo(0);
            }
            else
            {
                Logger.Warn("TestingScene: No video files found in carousel directory");
            }
        }
        else
        {
            Logger.Error($"TestingScene: Carousel directory not found at {carouselPath}");
        }

        IsActive = true;
    }

    private void LoadVideo(int index)
    {
        if (_videoFiles.Length == 0 || _videoPlayer == null)
            return;

        _isLoading = true;
        _currentVideoIndex = index;
        string videoPath = _videoFiles[_currentVideoIndex];

        Logger.Info($"TestingScene: Loading video: {Path.GetFileName(videoPath)}");

        try
        {
            _videoPlayer.Load(videoPath);
            _videoPlayer.Play();
            _isLoading = false;
            Logger.Info(
                $"TestingScene: Video loaded successfully: {_videoPlayer.Width}x{_videoPlayer.Height} @ {_videoPlayer.FrameRate:F2}fps"
            );
        }
        catch (Exception ex)
        {
            Logger.Error($"TestingScene: Failed to load video: {ex.Message}");
            _isLoading = false;
        }
    }

    public void Update()
    {
        if (!IsActive)
            return;

        // Handle input
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Stop();
            return;
        }

        // Navigate between videos
        if (_videoFiles.Length > 1)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Right))
            {
                int nextIndex = (_currentVideoIndex + 1) % _videoFiles.Length;
                LoadVideo(nextIndex);
            }
            else if (Raylib.IsKeyPressed(KeyboardKey.Left))
            {
                int prevIndex = (_currentVideoIndex - 1 + _videoFiles.Length) % _videoFiles.Length;
                LoadVideo(prevIndex);
            }
        }

        // Playback controls
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            if (_videoPlayer?.State == VideoPlayerState.Playing)
            {
                _videoPlayer.Pause();
                Logger.Info("TestingScene: Video paused");
            }
            else if (_videoPlayer?.State == VideoPlayerState.Paused)
            {
                _videoPlayer.Play();
                Logger.Info("TestingScene: Video resumed");
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.L))
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.IsLooping = !_videoPlayer.IsLooping;
                Logger.Info(
                    $"TestingScene: Looping {(_videoPlayer.IsLooping ? "enabled" : "disabled")}"
                );
            }
        }

        // Update video player
        _videoPlayer?.Update();
    }

    public void Draw()
    {
        if (!IsActive)
            return;

        Raylib.ClearBackground(Color.Black);

        if (_videoFiles.Length == 0)
        {
            // Show error message
            string errorMsg = "No video files found in assets/carousel directory";
            int textWidth = Raylib.MeasureText(errorMsg, 20);
            Raylib.DrawText(
                errorMsg,
                (Raylib.GetScreenWidth() - textWidth) / 2,
                Raylib.GetScreenHeight() / 2 - 10,
                20,
                Color.White
            );

            string helpMsg = "Press ESC to return to menu";
            int helpWidth = Raylib.MeasureText(helpMsg, 16);
            Raylib.DrawText(
                helpMsg,
                (Raylib.GetScreenWidth() - helpWidth) / 2,
                Raylib.GetScreenHeight() / 2 + 20,
                16,
                Color.Gray
            );
        }
        else if (_isLoading)
        {
            // Show loading message
            string loadingMsg = "Loading video...";
            int textWidth = Raylib.MeasureText(loadingMsg, 20);
            Raylib.DrawText(
                loadingMsg,
                (Raylib.GetScreenWidth() - textWidth) / 2,
                Raylib.GetScreenHeight() / 2,
                20,
                Color.White
            );
        }
        else if (_videoPlayer != null)
        {
            // Draw video
            var bounds = new Rectangle(
                0,
                0,
                Raylib.GetScreenWidth(),
                Raylib.GetScreenHeight() - 100
            );
            _videoPlayer.Draw(bounds, Color.White);

            // Draw UI overlay
            Raylib.DrawRectangle(
                0,
                Raylib.GetScreenHeight() - 100,
                Raylib.GetScreenWidth(),
                100,
                new Color(0, 0, 0, 200)
            );

            // Video info
            string videoName = Path.GetFileName(_videoFiles[_currentVideoIndex]);
            Raylib.DrawText(videoName, 20, Raylib.GetScreenHeight() - 85, 20, Color.White);

            string videoInfo =
                $"{_videoPlayer.Width}x{_videoPlayer.Height} @ {_videoPlayer.FrameRate:F2}fps | ";
            videoInfo += $"{_videoPlayer.CurrentTime:mm\\:ss} / {_videoPlayer.Duration:mm\\:ss}";
            Raylib.DrawText(videoInfo, 20, Raylib.GetScreenHeight() - 60, 16, Color.Gray);

            string status =
                $"State: {_videoPlayer.State} | Looping: {(_videoPlayer.IsLooping ? "ON" : "OFF")}";
            Raylib.DrawText(status, 20, Raylib.GetScreenHeight() - 40, 16, Color.Gray);

            // Controls help
            string controls = "";
            if (_videoFiles.Length > 1)
            {
                controls += "← → Navigate | ";
            }
            controls += "SPACE Pause/Resume | L Toggle Loop | ESC Exit";

            int controlsWidth = Raylib.MeasureText(controls, 14);
            Raylib.DrawText(
                controls,
                Raylib.GetScreenWidth() - controlsWidth - 20,
                Raylib.GetScreenHeight() - 25,
                14,
                Color.LightGray
            );

            // Video count indicator
            if (_videoFiles.Length > 1)
            {
                string countText = $"{_currentVideoIndex + 1}/{_videoFiles.Length}";
                int countWidth = Raylib.MeasureText(countText, 18);
                Raylib.DrawText(
                    countText,
                    Raylib.GetScreenWidth() - countWidth - 20,
                    Raylib.GetScreenHeight() - 85,
                    18,
                    Color.White
                );
            }
        }

        // FPS counter
        Raylib.DrawFPS(10, 10);
    }

    public void Stop()
    {
        if (!IsActive)
            return;

        _videoPlayer?.Stop();

        // Restore window state
        RestoreWindowState();

        IsActive = false;
    }

    public void Cleanup()
    {
        _videoPlayer?.Dispose();
        _videoPlayer = null;
    }

    private static void RestoreWindowState()
    {
        // Restore original window size
        Raylib.SetWindowSize(400, 200);

        // Center the small window
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition((monitorWidth - 400) / 2, (monitorHeight - 200) / 2);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Cleanup();
        _disposed = true;
    }
}
