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
                2,
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
                    2,
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
                    2,
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
                2,
                copyColor,
                _config.Ending.CopyrightFontWeight
            );
        }

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
}
