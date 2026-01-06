using System.Numerics;
using Raylib_cs;

namespace EndingApp.Scenes.ClipScene;

internal sealed partial class ClipScene
{
    public void Draw()
    {
        if (_isVideoLoaded && _videoPlayer != null && _currentClip != null)
        {
            Raylib.ClearBackground(Color.Black);

            // Draw Video
            var bounds = new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            _videoPlayer.Draw(bounds, Color.White);

            // Draw Compact Overlay Controls at bottom
            int overlayHeight = 60;
            int overlayY = Raylib.GetScreenHeight() - overlayHeight;

            // Semi-transparent background for controls
            Raylib.DrawRectangle(
                0,
                overlayY,
                Raylib.GetScreenWidth(),
                overlayHeight,
                new Color(0, 0, 0, 200)
            );

            if (_fontLoader != null)
            {
                // Compact title format: "Title - User"
                // Title in bright color, user in darker shade
                float leftMargin = 20;
                float topMargin = overlayY + 12;

                // Draw title
                _fontLoader.DrawText(
                    _currentClip.Title,
                    new Vector2(leftMargin, topMargin),
                    20,
                    1,
                    new Color(255, 255, 255, 255),
                    FontWeight.Bold
                );

                // Measure title width to position the separator and user
                float titleWidth = _fontLoader.MeasureText(_currentClip.Title, 20, 1).X;

                // Measure separator width
                float separatorWidth = _fontLoader.MeasureText("            ", 20, 1).X;

                // Draw user in darker shade
                _fontLoader.DrawText(
                    _currentClip.CreatorName,
                    new Vector2(leftMargin + titleWidth + separatorWidth, topMargin),
                    20,
                    1,
                    new Color(160, 160, 160, 255)
                );

                // Time display (right side)
                string timeText =
                    $"{_videoPlayer.CurrentTime:mm\\:ss} / {_videoPlayer.Duration:mm\\:ss}";
                _fontLoader.DrawTextRightAligned(
                    timeText,
                    Raylib.GetScreenWidth() - 20,
                    topMargin,
                    18,
                    1,
                    new Color(200, 200, 200, 255)
                );

                // Compact instructions on second line
                string instructions = "Space: Pause | Arrows: Seek | Ctrl+Space: Back";
                _fontLoader.DrawText(
                    instructions,
                    new Vector2(leftMargin, topMargin + 26),
                    14,
                    1,
                    new Color(140, 140, 140, 255)
                );
            }

            // Progress Bar at the very bottom
            float progress = 0;
            if (_videoPlayer.Duration.TotalSeconds > 0)
            {
                progress = (float)(
                    _videoPlayer.CurrentTime.TotalSeconds / _videoPlayer.Duration.TotalSeconds
                );
            }

            int barHeight = 4;
            int barY = Raylib.GetScreenHeight() - barHeight;

            // Background bar (full width)
            Raylib.DrawRectangle(
                0,
                barY,
                Raylib.GetScreenWidth(),
                barHeight,
                new Color(80, 80, 80, 255)
            );

            // Progress bar
            if (progress > 0)
            {
                Raylib.DrawRectangle(
                    0,
                    barY,
                    (int)(Raylib.GetScreenWidth() * progress),
                    barHeight,
                    _colorBrand
                );
            }
        }
        else
        {
            // List View (matching SongRequest scene style)
            Raylib.ClearBackground(_colorBackground);

            if (_fontLoader == null)
                return;

            // Header Background
            Raylib.DrawRectangle(0, 0, _config.Ending.Width, 80, _colorPrimary);

            // Header Text
            _fontLoader.DrawText(
                "Clips",
                new Vector2(40, 25),
                36,
                1,
                _colorTextMain,
                FontWeight.Bold
            );

            // Status Text
            var statusColor = _statusMessage.StartsWith("Error") ? _colorAccent : _colorTextMain;
            _fontLoader.DrawText(_statusMessage, new Vector2(40, 85), 18, 1, statusColor);

            // Clips List (leave space for instructions at bottom)
            float listTopY = 120;
            float listBottomY = _config.Ending.Height - 50; // More space for instructions
            float listHeight = listBottomY - listTopY;
            float margin = 40;
            float listWidth = _config.Ending.Width - (margin * 2);

            lock (_listLock)
            {
                DrawClipsList(new Rectangle(margin, listTopY, listWidth, listHeight));
            }
        }
    }

    private void DrawClipsList(Rectangle bounds)
    {
        // Draw Header Background
        Raylib.DrawRectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, 40, _colorSecondary);

        // Draw Header Text
        _fontLoader?.DrawText(
            $"Available Clips ({_clips.Count})",
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
        float itemHeight = 60;
        float contentHeight = _clips.Count * itemHeight;

        // Handle Scrolling
        HandleScrolling(listArea, ref _scrollOffset, (int)contentHeight);

        // Scissor Mode for Clipping
        Raylib.BeginScissorMode(
            (int)listArea.X,
            (int)listArea.Y,
            (int)listArea.Width,
            (int)listArea.Height
        );

        float startY = listArea.Y - _scrollOffset;
        var mousePos = Raylib.GetMousePosition();

        for (int i = 0; i < _clips.Count; i++)
        {
            var clip = _clips[i];
            float yPos = startY + (i * itemHeight);

            // Skip if out of view
            if (yPos + itemHeight < listArea.Y || yPos > listArea.Y + listArea.Height)
                continue;

            Rectangle itemRect = new(bounds.X, yPos, bounds.Width, itemHeight);

            // Interaction logic (Hover/Click)
            bool isHovered =
                Raylib.CheckCollisionPointRec(mousePos, itemRect)
                && Raylib.CheckCollisionPointRec(mousePos, listArea);
            bool isSelected = i == _selectedIndex;

            // Background
            if (isSelected)
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
                _selectedIndex = i;
                PlayClip(clip);
            }

            // Check if file exists
            string tempPath = Path.Combine(AppContext.BaseDirectory, "temp", "clips");
            string filePath = Path.Combine(tempPath, clip.Filename);
            bool isReady = File.Exists(filePath);

            // Truncate title to 65 characters for display
            string displayTitle = clip.Title;
            if (displayTitle.Length > 65)
            {
                displayTitle = string.Concat(displayTitle.AsSpan(0, 65), "...");
            }

            // Title
            float textY = yPos + 10;
            if (isSelected)
            {
                _fontLoader?.DrawText(
                    displayTitle,
                    new Vector2(bounds.X + 10, textY),
                    18,
                    1,
                    _colorBrand,
                    FontWeight.Bold
                );
            }
            else
            {
                _fontLoader?.DrawText(
                    displayTitle,
                    new Vector2(bounds.X + 10, textY),
                    18,
                    1,
                    _colorTextMain
                );
            }

            // Creator info
            string creatorText = $"By: {clip.CreatorName} | Posted by: {clip.PostedBy}";
            _fontLoader?.DrawText(
                creatorText,
                new Vector2(bounds.X + 10, textY + 25),
                16,
                1,
                _colorTextSub
            );

            // Status indicator
            if (!isReady)
            {
                _fontLoader?.DrawTextRightAligned(
                    "Downloading...",
                    bounds.X + bounds.Width - 10,
                    textY + 12,
                    16,
                    1,
                    _colorAccent
                );
            }

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

        // Scrollbar visual
        if (contentHeight > listArea.Height)
        {
            float scrollRatio = _scrollOffset / (contentHeight - listArea.Height);
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

        // Instructions at bottom if no clips
        if (_clips.Count == 0)
        {
            string waitingText =
                _statusMessage == "Ready" ? "Waiting for clips..." : _statusMessage;
            _fontLoader?.DrawTextCentered(
                waitingText,
                bounds.X + bounds.Width / 2,
                bounds.Y + bounds.Height / 2,
                24,
                1,
                _colorTextSub
            );
        }
        else
        {
            // Instructions (draw below the list, within window bounds)
            string instructions =
                "Click or Enter: Play | Up/Down: Navigate | Ctrl+Space: Back to Menu";
            _fontLoader?.DrawText(
                instructions,
                new Vector2(bounds.X, _config.Ending.Height - 30),
                16,
                1,
                _colorTextSub
            );
        }
    }

    private static void HandleScrolling(Rectangle rect, ref float scrollOffset, int contentHeight)
    {
        var mousePos = Raylib.GetMousePosition();
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
}
