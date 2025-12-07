using System.Numerics;
using Raylib_cs;

namespace EndingApp;

internal sealed class EndingScene(AppConfig config) : IDisposable
{
    private AppConfig _config = config;
    private Texture2D _backgroundTexture;
    private Music _music;
    private FontLoader? _fontLoader;
    private bool _disposed;

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

    private List<CreditEntry> _credits = [];
    private float _creditsScrollY;
    private float _creditsScrollSpeed; // dynamically calculated

    // State for intro sequence
    private float _elapsedTime;
    private bool _musicStarted;
    private bool _creditsStarted;
    private bool _showStartText;
    private float _startTextAlpha;
    private bool _startTextFading;
    private float _startTextFadeElapsed;

    // Fade-in state for start text
    private bool _startTextFadeIn;
    private float _startTextFadeInElapsed;

    public bool IsActive { get; private set; }

    public void Start()
    {
        // Reload config for hot-reloading support
        _config = AppConfig.Load();

        // Clear transparent window flag for ending scene
        // Raylib.ClearWindowState(ConfigFlags.TransparentWindow);

        // Resize window using config
        Raylib.SetWindowSize(_config.Ending.Width, _config.Ending.Height);

        // Center window on screen
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition(
            (monitorWidth - _config.Ending.Width) / 2,
            (monitorHeight - _config.Ending.Height) / 2
        );

        // Load background image from config
        _backgroundTexture = Raylib.LoadTexture(_config.Ending.BackgroundImage);

        // Load credits
        _credits = CreditsReader.Read("credits.yaml");

        // Extract codepoints and load fonts using FontLoader
        // Only the exact characters from credits.yaml are extracted - no full CJK ranges
        int[] codepoints = FontLoader.ExtractCodepoints(_credits);
        _fontLoader = new FontLoader();
        _fontLoader.Load(
            "assets/fonts/georgia.ttf",
            "assets/fonts/NotoSansJP-Regular.ttf", // NotoSansJP has symbols + Japanese
            64, // Load at higher resolution for quality
            codepoints,
            TextureFilter.Bilinear
        );

        // Start credits at bottom of screen (outside view)
        _creditsScrollY = _config.Ending.Height;

        // Prepare music but do not play yet
        Raylib.InitAudioDevice();
        _music = Raylib.LoadMusicStream(_config.Ending.Music);

        // Calculate dynamic credits scroll speed
        float songDuration = Raylib.GetMusicTimeLength(_music);
        float scrollTime = Math.Max(songDuration - 15f, 2f); // minimum 2 seconds to avoid div by zero
        float creditsHeight = CalculateCreditsHeight();
        float scrollDistance = _config.Ending.Height + creditsHeight;
        _creditsScrollSpeed = scrollDistance / scrollTime;

        // Reset intro state
        _elapsedTime = 0f;
        _musicStarted = false;
        _creditsStarted = false;
        _showStartText = false;
        _startTextAlpha = 0f;
        _startTextFading = false;
        _startTextFadeElapsed = 0f;
        _startTextFadeIn = false;
        _startTextFadeInElapsed = 0f;

        IsActive = true;
    }

    public void Update()
    {
        if (!IsActive)
            return;

        // Check for Ctrl+Space to exit ending scene
        if (
            (
                Raylib.IsKeyDown(KeyboardKey.LeftControl)
                || Raylib.IsKeyDown(KeyboardKey.RightControl)
            ) && Raylib.IsKeyPressed(KeyboardKey.Space)
        )
        {
            Stop();
            return;
        }

        float dt = Raylib.GetFrameTime();
        _elapsedTime += dt;

        float startDelay = _config.Ending.StartDelay;
        float startTextHideTime = _config.Ending.StartTextHideTime;

        // Start music and credits after delay
        if (!_musicStarted && _elapsedTime >= startDelay)
        {
            Raylib.PlayMusicStream(_music);
            _musicStarted = true;
            _showStartText = true;
            _creditsStarted = true;
            // Start fade-in
            _startTextFadeIn = true;
            _startTextFadeInElapsed = 0f;
            _startTextAlpha = 0f;
        }

        // Fade in start text
        if (_showStartText && _startTextFadeIn)
        {
            _startTextFadeInElapsed += dt;
            float fadeInDuration = 0.5f;
            _startTextAlpha = Math.Clamp(_startTextFadeInElapsed / fadeInDuration, 0f, 1f);
            if (_startTextAlpha >= 1f)
            {
                _startTextAlpha = 1f;
                _startTextFadeIn = false;
            }
        }

        // Fade out start text after hide time
        if (_showStartText && !_startTextFading && _elapsedTime >= startDelay + startTextHideTime)
        {
            _startTextFading = true;
            _startTextFadeElapsed = 0f;
        }
        if (_startTextFading)
        {
            _startTextFadeElapsed += dt;
            // Fade out over 0.5s
            float fadeDuration = 0.5f;
            _startTextAlpha = 1f - (_startTextFadeElapsed / fadeDuration);
            if (_startTextAlpha <= 0f)
            {
                _showStartText = false;
                _startTextFading = false;
                _startTextAlpha = 0f;
            }
        }

        // Only scroll credits after delay
        if (_creditsStarted)
        {
            _creditsScrollY -= _creditsScrollSpeed * dt;
        }

        // Update music stream if started
        if (_musicStarted)
        {
            Raylib.UpdateMusicStream(_music);
        }
    }

    // Helper to calculate total credits height for scrolling
    private float CalculateCreditsHeight()
    {
        if (_fontLoader == null || _credits.Count == 0)
            return 0f;

        int fontSize = _config.Ending.FontSize;
        int sectionFontSize = _config.Ending.SectionFontSize;
        const int valueLineSpacing = 4; // Must match DrawCredits
        const int sectionGap = 8;
        const int sectionSpacing = 40;

        float totalHeight = 0f;
        foreach (var entry in _credits)
        {
            if (entry.IsSeparator)
            {
                totalHeight += fontSize + sectionSpacing;
            }
            else if (entry.Section != null)
            {
                totalHeight += sectionFontSize + sectionGap;
                if (entry.TwoColumns && entry.Values.Count > 0)
                {
                    int total = entry.Values.Count;
                    int rows = (total + 1) / 2;
                    totalHeight += rows * (fontSize + valueLineSpacing);
                }
                else
                {
                    totalHeight += entry.Values.Count * (fontSize + valueLineSpacing);
                }
                totalHeight += sectionSpacing - valueLineSpacing;
            }
        }
        return totalHeight;
    }

    public void Stop()
    {
        if (!IsActive)
            return;

        // Cleanup resources
        Raylib.UnloadTexture(_backgroundTexture);
        _fontLoader?.Dispose();
        Raylib.UnloadMusicStream(_music);
        Raylib.CloseAudioDevice();

        // Restore transparent window flag for main menu
        Raylib.SetWindowState(ConfigFlags.TransparentWindow);

        // Restore original window size
        Raylib.SetWindowSize(400, 200);

        // Center the small window
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition((monitorWidth - 400) / 2, (monitorHeight - 200) / 2);

        IsActive = false;
    }

    public void Draw()
    {
        if (!IsActive || _fontLoader == null)
            return;

        // Draw a fullscreen opaque black rectangle first to block transparency
        Raylib.DrawRectangle(0, 0, _config.Ending.Width, _config.Ending.Height, Color.Black);

        // Draw background image (scaled to fit window using config dimensions)
        Raylib.DrawTexturePro(
            _backgroundTexture,
            new Rectangle(0, 0, _backgroundTexture.Width, _backgroundTexture.Height),
            new Rectangle(0, 0, _config.Ending.Width, _config.Ending.Height),
            new Vector2(0, 0),
            0f,
            Color.White
        );

        // Draw scrolling credits only after credits started
        if (_creditsStarted)
        {
            DrawCredits();
        }

        // Draw start text in the middle if needed
        if (_showStartText && !string.IsNullOrEmpty(_config.Ending.StartText))
        {
            string startText = _config.Ending.StartText;
            int fontSize = _config.Ending.FontSize;
            int centerX = _config.Ending.Width / 2;
            int centerY = _config.Ending.Height / 2;
            var textColor = _config.Ending.StartTextColor;
            textColor.A = (byte)(textColor.A * Math.Clamp(_startTextAlpha, 0f, 1f));
            _fontLoader?.DrawTextCentered(
                startText,
                centerX,
                centerY - fontSize / 2,
                fontSize,
                2,
                textColor
            );
        }

        // Draw cinematic black bars on top and bottom
        if (_config.Ending.BlackBarHeight > 0)
        {
            // Top black bar
            Raylib.DrawRectangle(
                0,
                0,
                _config.Ending.Width,
                _config.Ending.BlackBarHeight,
                Color.Black
            );

            // Bottom black bar
            Raylib.DrawRectangle(
                0,
                _config.Ending.Height - _config.Ending.BlackBarHeight,
                _config.Ending.Width,
                _config.Ending.BlackBarHeight,
                Color.Black
            );
        }
    }

    private void DrawCredits()
    {
        if (_fontLoader == null)
            return;

        int fontSize = _config.Ending.FontSize;
        int sectionFontSize = _config.Ending.SectionFontSize;
        const int valueLineSpacing = 4; // Minimal gap between value lines
        const int sectionGap = 8; // Small gap between section title and first value
        const int sectionSpacing = 40; // Larger gap between different sections
        const float charSpacing = 2;

        float currentY = _creditsScrollY;
        // Position credits block using creditsPositionPercentage from config
        int creditsLeftX = (int)(
            (_config.Ending.Width * _config.Ending.CreditsPositionPercentage) / 100.0
        );

        foreach (var entry in _credits)
        {
            if (entry.IsSeparator)
            {
                // Draw separator - treat as a section for uniform spacing
                _fontLoader.DrawText(
                    entry.Separator!,
                    new Vector2(creditsLeftX, currentY),
                    fontSize,
                    charSpacing,
                    _config.Ending.SectionColor
                );
                currentY += fontSize + sectionSpacing;
            }
            else if (entry.Section != null)
            {
                // Draw section header left-aligned
                _fontLoader.DrawText(
                    entry.Section,
                    new Vector2(creditsLeftX, currentY),
                    sectionFontSize,
                    charSpacing,
                    _config.Ending.SectionColor
                );
                currentY += sectionFontSize + sectionGap;

                // Draw values with minimal spacing
                if (entry.TwoColumns && entry.Values.Count > 0)
                {
                    // Two column layout: evenly distribute values into two columns
                    int total = entry.Values.Count;
                    int rows = (total + 1) / 2;
                    int col1Count = rows;
                    int col2Count = total - rows;
                    float colGap = 32; // Space between columns
                    float colWidth = 200; // Fixed column width (can be adjusted or measured)
                    for (int row = 0; row < rows; row++)
                    {
                        // Left column value
                        string left = entry.Values[row];
                        _fontLoader.DrawText(
                            left,
                            new Vector2(creditsLeftX, currentY),
                            fontSize,
                            charSpacing,
                            _config.Ending.ValuesColor
                        );

                        // Right column value (if exists)
                        int rightIdx = row + rows;
                        if (rightIdx < total)
                        {
                            string right = entry.Values[rightIdx];
                            _fontLoader.DrawText(
                                right,
                                new Vector2(creditsLeftX + colWidth + colGap, currentY),
                                fontSize,
                                charSpacing,
                                _config.Ending.ValuesColor
                            );
                        }

                        currentY += fontSize + valueLineSpacing;
                    }
                }
                else
                {
                    // Single column layout - left-aligned
                    foreach (string value in entry.Values)
                    {
                        _fontLoader.DrawText(
                            value,
                            new Vector2(creditsLeftX, currentY),
                            fontSize,
                            charSpacing,
                            _config.Ending.ValuesColor
                        );
                        currentY += fontSize + valueLineSpacing;
                    }
                }

                // Add larger gap before next section
                currentY += sectionSpacing - valueLineSpacing;
            }
        }
    }

    public void Cleanup()
    {
        if (IsActive)
        {
            Raylib.UnloadTexture(_backgroundTexture);
            _fontLoader?.Dispose();
            Raylib.UnloadMusicStream(_music);
            Raylib.CloseAudioDevice();
            IsActive = false;
        }
    }
}
