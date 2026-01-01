using System.Numerics;
using Raylib_cs;

namespace EndingApp.Scenes.ClipScene;

internal sealed partial class ClipScene
{
    public void Draw()
    {
        Raylib.ClearBackground(Color.Black);

        // If video is loaded, draw it
        if (_isVideoLoaded && _videoPlayer != null && _currentClip != null)
        {
            // Draw Video
            var bounds = new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            _videoPlayer.Draw(bounds, Color.White);

            // Draw Overlay Controls
            // Progress Bar
            float progress = 0;
            if (_videoPlayer.Duration.TotalSeconds > 0)
            {
                progress = (float)(
                    _videoPlayer.CurrentTime.TotalSeconds / _videoPlayer.Duration.TotalSeconds
                );
            }
            int barHeight = 10;
            Raylib.DrawRectangle(
                0,
                Raylib.GetScreenHeight() - barHeight,
                Raylib.GetScreenWidth(),
                barHeight,
                new Color(50, 50, 50, 200)
            );
            Raylib.DrawRectangle(
                0,
                Raylib.GetScreenHeight() - barHeight,
                (int)(Raylib.GetScreenWidth() * progress),
                barHeight,
                Color.Red
            );

            // Metadata Overlay
            string titleText = $"{_currentClip.Title}";
            _fontLoader?.DrawText(titleText, new Vector2(20, 20), 30, 2.0f, Color.White);
            string infoText =
                $"By: {_currentClip.CreatorName} | Posted: {_currentClip.PostedBy} | {_videoPlayer.CurrentTime:mm\\:ss} / {_videoPlayer.Duration:mm\\:ss}";
            _fontLoader?.DrawText(infoText, new Vector2(20, 60), 20, 2.0f, Color.LightGray);
        }
        else
        {
            // List View
            // Draw header
            _fontLoader?.DrawText("Clips Scene", new Vector2(20, 20), 30, 2.0f, Color.White);

            // Draw status
            _fontLoader?.DrawText(_statusMessage, new Vector2(20, 60), 20, 2.0f, Color.LightGray);

            // List clips
            lock (_listLock)
            {
                int startY = 100;
                int itemHeight = 30;
                int visibleCount = (Raylib.GetScreenHeight() - startY - 40) / itemHeight;
                int startIndex = Math.Max(0, _selectedIndex - visibleCount / 2);
                int endIndex = Math.Min(_clips.Count, startIndex + visibleCount);

                for (int i = startIndex; i < endIndex; i++)
                {
                    var clip = _clips[i];
                    int y = startY + (i - startIndex) * itemHeight;
                    Color color = (i == _selectedIndex) ? Color.Yellow : Color.White;
                    string prefix = (i == _selectedIndex) ? "> " : "  ";

                    // Check if file exists to mark as ready
                    string tempPath = Path.Combine(AppContext.BaseDirectory, "temp", "clips");
                    string filePath = Path.Combine(tempPath, clip.Filename);
                    bool isReady = File.Exists(filePath);
                    string statusSuffix = isReady ? "" : " (Downloading...)";

                    _fontLoader?.DrawText(
                        $"{prefix}{clip.Title} by {clip.CreatorName}{statusSuffix}",
                        new Vector2(20, y),
                        20,
                        2.0f,
                        color
                    );
                }
            }

            // Instructions
            _fontLoader?.DrawText(
                "Arrows: Nav | Enter: Play | ESC: Back",
                new Vector2(20, Raylib.GetScreenHeight() - 30),
                20,
                2.0f,
                Color.Gray
            );
        }

        if (_isVideoLoaded)
        {
            _fontLoader?.DrawText(
                "Space: Pause | Arrows: Seek | ESC: Stop",
                new Vector2(20, Raylib.GetScreenHeight() - 30 - 20), // Above progress bar
                20,
                2.0f,
                Color.White
            );
        }
    }
}
