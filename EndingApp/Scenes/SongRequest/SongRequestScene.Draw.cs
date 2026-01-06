using System.Numerics;
using EndingApp.Services;
using Raylib_cs;

namespace EndingApp.Scenes.SongRequest;

internal sealed partial class SongRequestScene
{
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
        var mousePos = Raylib.GetMousePosition();

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

            // Truncate title to 65 characters for display in lists only
            string displayTitle = item.Title;
            if (displayTitle.Length > 65)
            {
                displayTitle = string.Concat(displayTitle.AsSpan(0, 65), "...");
            }

            // Calculate available width for title
            // Width - padding - duration width - extra padding between title and duration
            float durationWidth = _fontLoader?.MeasureText(durationStr, 16, 1).X ?? 60;
            float availableTitleWidth = bounds.Width - 20 - durationWidth - 30; // 20px left pad, 30px gap+right pad

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
        var statusColor = _statusMessage.StartsWith("Error") ? _colorAccent : _colorTextMain;
        _fontLoader.DrawText(_statusMessage, new Vector2(40, 85), 18, 1, statusColor);

        // Volume Slider (Keeping existing logic)
        /* Removed as it's moving to bottom right
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
        */

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
            float textLeftX = 40;
            string nowPlayingText = _currentSongItem.Title;
            string requesterText = $"Requested by: {_currentSongItem.Requester}";

            // Title (Left) - Made smaller (28 -> 24)
            _fontLoader.DrawText(
                nowPlayingText,
                new Vector2(textLeftX, bottomY + 25),
                24,
                1,
                _colorBrand,
                FontWeight.Bold
            );
            // Requester (Left)
            _fontLoader.DrawText(
                requesterText,
                new Vector2(textLeftX, bottomY + 55),
                20,
                1,
                _colorTextSub
            );

            // Controls (Right)
            float btnSize = 32;
            float spacing = 20;
            float rightMargin = 40;
            float controlsY = bottomY + 25; // Moved up slightly to make room for volume

            // Calculate X positions right-aligned
            // Order: [Prev] [Play] [Next] -> Right edge
            float nextX = _config.Ending.Width - rightMargin - btnSize;
            float playX = nextX - spacing - btnSize;
            float prevX = playX - spacing - btnSize;

            // Align Prev and Next visually with the volume bar width
            // Volume bar starts at prevX and ends at nextX + btnSize
            // So prevX is fine, nextX is fine. The buttons are 32x32.
            // Volume bar width = (nextX + 32) - prevX.
            // This means the volume bar exactly spans the outer edges of the Prev and Next buttons.
            // That sounds correctly aligned.

            // Prev
            _fontLoader.DrawText(
                "<<",
                new Vector2(prevX, controlsY),
                30,
                1,
                _colorAccent,
                FontWeight.Bold
            );
            // Play/Pause
            string playText = _isPaused ? ">" : "||";
            _fontLoader.DrawText(
                playText,
                new Vector2(playX + 8, controlsY), // +8 to center the symbol roughly in the btn area
                30,
                1,
                _colorAccent,
                FontWeight.Bold
            );
            // Next
            _fontLoader.DrawText(
                ">>",
                new Vector2(nextX, controlsY),
                30,
                1,
                _colorAccent,
                FontWeight.Bold
            );

            // Volume (Underneath Controls)
            float volWidth = (nextX + btnSize) - prevX; // Span the width of the controls
            float volHeight = 8;
            float volX = prevX;
            float volY = controlsY + btnSize + 15;

            // Volume Percentage (Left of bar)
            string volText = $"{(int)(_volume * 100)}%";
            _fontLoader.DrawTextRightAligned(
                volText,
                volX - 8,
                volY - 4, // Vertically center with bar (approx)
                16,
                1,
                _colorTextSub
            );

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

            // Volume Percentage Text (Right of volume bar, small)
            /*
            _fontLoader.DrawText(
                $"{(int)(_volume * 100)}%",
                new Vector2(volX + volWidth + 8, volY - 4),
                14,
                1,
                _colorTextSub
            );
            */

            // Progress Bar (Bottom)
            float barHeight = 8;
            float barY = bottomY + 100;
            float barMargin = 40;
            float barWidth = _config.Ending.Width - (barMargin * 2);

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

            // Timers (Underneath Seek Bar)
            string curTimeStr = TimeSpan.FromSeconds(currentTime).ToString("mm\\:ss");
            string totTimeStr = TimeSpan.FromSeconds(totalTime).ToString("mm\\:ss");
            float timerY = barY + 15;

            _fontLoader.DrawText(curTimeStr, new Vector2(barMargin, timerY), 16, 1, _colorTextSub);
            _fontLoader.DrawTextRightAligned(
                totTimeStr,
                _config.Ending.Width - barMargin,
                timerY,
                16,
                1,
                _colorTextSub
            );
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
    }
}
