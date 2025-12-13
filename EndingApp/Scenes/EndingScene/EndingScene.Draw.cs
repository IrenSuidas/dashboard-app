using System.Numerics;
using Raylib_cs;

namespace EndingApp;

internal sealed partial class EndingScene
{
    public void Draw()
    {
        if (!IsActive || _fontLoader == null)
            return;

        // Draw a fullscreen opaque black rectangle first to block transparency
        Raylib.DrawRectangle(0, 0, _config.Ending.Width, _config.Ending.Height, Color.Black);

        // Draw background image first (we overlay a solid rectangle later to fade to plain color)
        if (_backgroundTexture.Id != 0)
        {
            Raylib.DrawTexturePro(
                _backgroundTexture,
                new Rectangle(0, 0, _backgroundTexture.Width, _backgroundTexture.Height),
                new Rectangle(0, 0, _config.Ending.Width, _config.Ending.Height),
                new Vector2(0, 0),
                0f,
                Color.White
            );
        }
        else
        {
            Raylib.DrawRectangle(0, 0, _config.Ending.Width, _config.Ending.Height, Color.Black);
        }
        // Draw overlay background rectangle (fades in via _endBackgroundFader.Alpha)
        if (_endBackgroundFader.Alpha > 0f)
        {
            var bg = _config.Ending.EndBackgroundColor;
            byte a = (byte)Math.Clamp(bg.A * _endBackgroundFader.Alpha, 0f, 255f);
            bg.A = a;
            Raylib.DrawRectangle(0, 0, _config.Ending.Width, _config.Ending.Height, bg);
        }

        // Draw scrolling credits only after credits started
        if (_creditsStarted)
        {
            DrawCredits();
        }

        // Draw Carousel
        if (_carouselState != CarouselState.Hidden)
        {
            DrawCarousel();
        }

        // Draw start text in the middle if needed
        if (_showStartText && !string.IsNullOrEmpty(_config.Ending.StartText))
        {
            string startText = _config.Ending.StartText;
            int fontSize = _config.Ending.StartTextFontSize;
            int centerX = _config.Ending.Width / 2;
            int centerY = _config.Ending.Height / 2;
            var textColor = _config.Ending.StartTextColor;
            textColor.A = (byte)(textColor.A * Math.Clamp(_startTextFader.Alpha, 0f, 1f));
            _fontLoader?.DrawTextCentered(
                startText,
                centerX,
                centerY - fontSize / 2,
                fontSize,
                _config.Ending.StartTextSpacing,
                textColor,
                _config.Ending.StartTextFontWeight
            );
        }

        // Draw ending endText after credits have finished and it's active
        if (_showEndText && !string.IsNullOrEmpty(_config.Ending.EndText) && _fontLoader != null)
        {
            string endText = _config.Ending.EndText;
            int fontSize = _config.Ending.EndTextFontSize;
            int centerX = _config.Ending.Width / 2;
            int centerY = _config.Ending.Height / 2;
            var endTextColor = _config.Ending.EndTextColor;
            endTextColor.A = (byte)(endTextColor.A * Math.Clamp(_endTextFader.Alpha, 0f, 1f));

            // Split into lines on comma or literal \n or actual newline
            var lines = MyRegex()
                .Split(endText)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            float lineSpacing = 6f;

            // Compute total height for the text block
            float totalHeight = 0f;
            List<float> lineHeights = new List<float>();
            foreach (string? line in lines)
            {
                var size = _fontLoader.MeasureText(
                    line,
                    fontSize,
                    _config.Ending.EndTextSpacing,
                    _config.Ending.EndTextFontWeight
                );
                lineHeights.Add(size.Y);
                totalHeight += size.Y;
            }
            if (lines.Count > 1)
                totalHeight += (lines.Count - 1) * lineSpacing;

            // If emote exists, include it with a spacing in the combined height so the block is centered
            float emoteSpacing = 8f;
            float emoteHeight = 0f;
            if (_emoteLoaded && _emoteTexture.Height != 0)
            {
                emoteHeight = _emoteTexture.Height; // we draw at scale 1f
            }

            float combinedHeight =
                totalHeight + (emoteHeight > 0 ? (emoteSpacing + emoteHeight) : 0f);
            float curY = centerY - (combinedHeight / 2f);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                _fontLoader?.DrawTextCentered(
                    line,
                    centerX,
                    curY,
                    fontSize,
                    _config.Ending.EndTextSpacing,
                    endTextColor,
                    _config.Ending.EndTextFontWeight
                );
                curY += lineHeights[i] + lineSpacing;
            }

            // Draw emote below the end text with same alpha as the end text
            if (_emoteLoaded && _emoteTexture.Width != 0)
            {
                int emoteWidth = _emoteTexture.Width;
                int emoteHeightPx = _emoteTexture.Height;
                float emoteX = centerX - emoteWidth / 2f;
                float emoteY = curY + emoteSpacing;
                byte emoteAlpha = (byte)(Math.Clamp(_endTextFader.Alpha, 0f, 1f) * 255f);
                var emoteColor = new Color(255, 255, 255, (int)emoteAlpha);
                Raylib.DrawTextureEx(
                    _emoteTexture,
                    new Vector2(emoteX, emoteY),
                    0f,
                    1f,
                    emoteColor
                );
            }
        }

        // Draw copyright text centered after end text fades out
        if (_copyrightStarted && !string.IsNullOrEmpty(_config.Ending.CopyrightText))
        {
            int cFontSize = _config.Ending.CopyrightFontSize;
            int centerX = _config.Ending.Width / 2;
            int centerY = _config.Ending.Height / 2;
            var copyColor = _config.Ending.CopyrightColor;
            copyColor.A = (byte)(copyColor.A * Math.Clamp(_copyrightAlpha, 0f, 1f));
            _fontLoader?.DrawTextCentered(
                _config.Ending.CopyrightText,
                centerX,
                centerY - cFontSize / 2,
                cFontSize,
                _config.Ending.CopyrightSpacing,
                copyColor,
                _config.Ending.CopyrightFontWeight
            );
        }

        // Draw overlay
        DrawOverlay();

        // Draw cinematic black bars on top and bottom
        if (_config.Ending.BlackBarHeight > 0)
        {
            Raylib.DrawRectangle(
                0,
                0,
                _config.Ending.Width,
                _config.Ending.BlackBarHeight,
                Color.Black
            );

            Raylib.DrawRectangle(
                0,
                _config.Ending.Height - _config.Ending.BlackBarHeight,
                _config.Ending.Width,
                _config.Ending.BlackBarHeight,
                Color.Black
            );
        }
    }

    private void DrawOverlay()
    {
        if (_overlayPlayer != null && Raylib.IsTextureValid(_overlayPlayer.Texture))
        {
            var tex = _overlayPlayer.Texture;
            float windowAspect = (float)_config.Ending.Width / _config.Ending.Height;
            float videoAspect = (float)tex.Width / tex.Height;

            Rectangle sourceRect;
            if (windowAspect > videoAspect)
            {
                // Window is wider, crop top/bottom
                float newHeight = tex.Width / windowAspect;
                sourceRect = new Rectangle(0, (tex.Height - newHeight) / 2, tex.Width, newHeight);
            }
            else
            {
                // Video is wider, crop left/right
                float newWidth = tex.Height * windowAspect;
                sourceRect = new Rectangle((tex.Width - newWidth) / 2, 0, newWidth, tex.Height);
            }

            // Apply zoom
            if (_config.Ending.OverlayZoom != 100)
            {
                float zoomFactor = 100f / Math.Max(1, _config.Ending.OverlayZoom);
                float zoomedWidth = sourceRect.Width * zoomFactor;
                float zoomedHeight = sourceRect.Height * zoomFactor;

                sourceRect = new Rectangle(
                    sourceRect.X + (sourceRect.Width - zoomedWidth) / 2,
                    sourceRect.Y + (sourceRect.Height - zoomedHeight) / 2,
                    zoomedWidth,
                    zoomedHeight
                );
            }

            Rectangle destRect = new Rectangle(0, 0, _config.Ending.Width, _config.Ending.Height);

            // #ffecbc
            Color tint = new Color(0xff, 0xec, 0xbc, (int)(255 * _config.Ending.OverlayOpacity));

            Raylib.DrawTexturePro(tex, sourceRect, destRect, Vector2.Zero, 0f, tint);
        }
    }

    private void DrawCarousel()
    {
        float alpha = _carouselFader.Alpha;
        if (alpha <= 0f)
            return;

        int centerX = (int)(
            _config.Ending.Width * (_config.Ending.CarouselPositionPercentage / 100f)
        );
        int centerY = _config.Ending.Height / 2;
        var tint = Color.White;
        tint.A = (byte)(255 * alpha);

        Texture2D textureToDraw = default;
        int texWidth = 0;
        int texHeight = 0;

        if (_carouselCurrentItemType == CarouselItemType.Video && _carouselVideoPlayer != null)
        {
            textureToDraw = _carouselVideoPlayer.Texture;
            texWidth = _carouselVideoPlayer.Width;
            texHeight = _carouselVideoPlayer.Height;
        }
        else if (_carouselCurrentItemType == CarouselItemType.Image && _carouselImageLoaded)
        {
            textureToDraw = _carouselImageTexture;
            texWidth = textureToDraw.Width;
            texHeight = textureToDraw.Height;
        }

        if (texWidth > 0 && texHeight > 0)
        {
            // Calculate size based on percentage of window height (16:9 aspect ratio)
            int drawHeight = (int)(
                _config.Ending.Height * (_config.Ending.CarouselSizePercentage / 100f)
            );
            int drawWidth = (int)(drawHeight * (16f / 9f));

            Rectangle destRect = new Rectangle(
                centerX - drawWidth / 2,
                centerY - drawHeight / 2,
                drawWidth,
                drawHeight
            );

            // Draw texture
            if (_carouselCurrentItemType == CarouselItemType.Video && _carouselVideoPlayer != null)
            {
                _carouselVideoPlayer.Draw(destRect, tint);
            }
            else
            {
                Raylib.DrawTexturePro(
                    textureToDraw,
                    new Rectangle(0, 0, texWidth, texHeight),
                    destRect,
                    Vector2.Zero,
                    0f,
                    tint
                );
            }

            // Draw filename
            if (!string.IsNullOrEmpty(_carouselCurrentFileName) && _fontLoader != null)
            {
                int fontSize = _config.Ending.CarouselMediaTitleFontSize;
                var textColor = _config.Ending.CarouselMediaTitleColor;
                textColor.A = tint.A;

                var textSize = _fontLoader.MeasureText(
                    _carouselCurrentFileName,
                    fontSize,
                    _config.Ending.CarouselMediaTitleSpacing,
                    _config.Ending.CarouselMediaTitleFontWeight
                );
                // Position top-left outside the media
                int textX = (int)destRect.X;
                int textY = (int)(destRect.Y - textSize.Y - 10);

                _fontLoader.DrawText(
                    _carouselCurrentFileName,
                    new Vector2(textX, textY),
                    fontSize,
                    _config.Ending.CarouselMediaTitleSpacing,
                    textColor,
                    _config.Ending.CarouselMediaTitleFontWeight
                );
            }
        }
    }
}
