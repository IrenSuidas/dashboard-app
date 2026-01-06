using Raylib_cs;

namespace EndingApp.Scenes.ClipScene;

internal sealed partial class ClipScene
{
    public void Update()
    {
        // Handle Ctrl+Space based on context
        if (
            (
                Raylib.IsKeyDown(KeyboardKey.LeftControl)
                || Raylib.IsKeyDown(KeyboardKey.RightControl)
            ) && Raylib.IsKeyPressed(KeyboardKey.Space)
        )
        {
            if (_isVideoLoaded && _videoPlayer != null)
            {
                // If video is playing, stop it and return to list
                Logger.Info("ClipScene: ctrl+space pressed - stopping video and returning to list");
                _videoPlayer.Dispose();
                _videoPlayer = null;
                _isVideoLoaded = false;
                _currentClip = null;
                return;
            }
            else
            {
                // If in list view, return to main menu
                Logger.Info("ClipScene: ctrl+space pressed - returning to main menu");
                IsActive = false;
                return;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.Dispose();
                _videoPlayer = null;
                _isVideoLoaded = false;
                _currentClip = null;
            }
            IsActive = false;
            return;
        }

        // Polling logic
        double currentTime = Raylib.GetTime();
        if (currentTime - _lastFetchTime >= 10.0 && !_isFetching)
        {
            _lastFetchTime = currentTime;
            _ = FetchAndDownloadClips();
        }

        // Navigation (if no video playing or even if playing to switch)
        if (Raylib.IsKeyPressed(KeyboardKey.Down))
        {
            lock (_listLock)
            {
                if (_clips.Count > 0)
                {
                    _selectedIndex = (_selectedIndex + 1) % _clips.Count;
                }
            }
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Up))
        {
            lock (_listLock)
            {
                if (_clips.Count > 0)
                {
                    _selectedIndex = (_selectedIndex - 1 + _clips.Count) % _clips.Count;
                }
            }
        }

        // Selection / Play
        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            Clip? selected = null;
            lock (_listLock)
            {
                if (_selectedIndex >= 0 && _selectedIndex < _clips.Count)
                {
                    selected = _clips[_selectedIndex];
                }
            }

            if (selected != null)
            {
                PlayClip(selected);
            }
        }

        // Video Controls
        if (_videoPlayer != null && _isVideoLoaded)
        {
            _videoPlayer.Update();

            // Play/Pause with Space (only if Ctrl is not pressed)
            if (
                Raylib.IsKeyPressed(KeyboardKey.Space)
                && !Raylib.IsKeyDown(KeyboardKey.LeftControl)
                && !Raylib.IsKeyDown(KeyboardKey.RightControl)
            )
            {
                if (_videoPlayer.State == VideoPlayerState.Playing)
                    _videoPlayer.Pause();
                else
                    _videoPlayer.Play();
            }

            // Seek
            if (Raylib.IsKeyPressed(KeyboardKey.Right))
            {
                var seekTime =
                    Raylib.IsKeyDown(KeyboardKey.LeftControl)
                    || Raylib.IsKeyDown(KeyboardKey.RightControl)
                        ? TimeSpan.FromSeconds(2)
                        : TimeSpan.FromSeconds(5);
                _videoPlayer.Seek(_videoPlayer.CurrentTime + seekTime);
            }
            else if (Raylib.IsKeyPressed(KeyboardKey.Left))
            {
                var seekTime =
                    Raylib.IsKeyDown(KeyboardKey.LeftControl)
                    || Raylib.IsKeyDown(KeyboardKey.RightControl)
                        ? TimeSpan.FromSeconds(2)
                        : TimeSpan.FromSeconds(5);
                _videoPlayer.Seek(_videoPlayer.CurrentTime - seekTime);
            }
        }
    }

    private void PlayClip(Clip clip)
    {
        string tempPath = Path.Combine(AppContext.BaseDirectory, "temp", "clips");
        string filePath = Path.Combine(tempPath, clip.Filename);

        if (!File.Exists(filePath))
        {
            _statusMessage = "File not downloaded yet.";
            return;
        }

        if (_videoPlayer != null)
        {
            _videoPlayer.Dispose();
        }

        try
        {
            _videoPlayer = new VideoPlayer();
            _videoPlayer.Load(filePath);
            _videoPlayer.Play();
            _videoPlayer.IsLooping = true;
            _currentClip = clip;
            _isVideoLoaded = true;
            _statusMessage = $"Playing: {clip.Title}";
        }
        catch (Exception ex)
        {
            Logger.Error($"ClipScene: Failed to play clip {clip.Title} - {ex.Message}");
            _statusMessage = "Failed to play clip.";
            _isVideoLoaded = false;
        }
    }
}
