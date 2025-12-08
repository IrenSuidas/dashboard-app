using System.Numerics;
using System.Text.RegularExpressions;
using Raylib_cs;

namespace EndingApp;

internal sealed partial class EndingScene(AppConfig config) : IDisposable
{
    private AppConfig _config = config;
    private Texture2D _backgroundTexture;
    private Music _music;
    private FontLoader? _fontLoader;
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

    private List<CreditEntry> _credits = [];
    private float _creditsScrollY;
    private float _creditsScrollSpeed; // dynamically calculated
    private float _songDuration;
    private float _musicPlayElapsed; // Manual timer for music playback
    private float _creditsHeight;

    // State for intro sequence
    private float _elapsedTime;
    private bool _musicStarted;
    private bool _musicStopped;
    private bool _creditsStarted;
    private bool _showStartText;
    private float _startTextAlpha;
    private bool _startTextFading;
    private float _startTextFadeElapsed;

    // Fade-in state for start text
    private bool _startTextFadeIn;
    private float _startTextFadeInElapsed;

    // End text state
    private bool _endTextStarted;
    private bool _showEndText;
    private float _endTextAlpha;
    private bool _endTextFading;
    private float _endTextFadeElapsed;
    private bool _endTextFadeIn;
    private float _endTextFadeInElapsed;
    private float _endTextShowElapsed;
    private Texture2D _emoteTexture;
    private bool _emoteLoaded;
    private bool _endBackgroundActive; // When true, draw plain background color instead of image
    private bool _endBackgroundFading;
    private float _endBackgroundFadeElapsed;
    private float _endBackgroundAlpha;

    // Copyright text state
    private bool _copyrightStarted;
    private bool _copyrightFadingIn;
    private float _copyrightFadeElapsed;
    private float _copyrightAlpha;

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

        // Load background image from config (use resource cache)
        _backgroundTexture = ResourceCache.LoadTexture(_config.Ending.BackgroundImage);

        // Load credits
        _credits = CreditsReader.Read("credits.yaml");

        // Extract codepoints and load fonts using FontLoader
        // Include characters from credits plus StartText, EndText and Copyright text
        var codepointSet = new HashSet<int>(FontLoader.ExtractCodepoints(_credits));
        foreach (var rune in (_config.Ending.StartText ?? string.Empty).EnumerateRunes())
            codepointSet.Add(rune.Value);
        foreach (var rune in (_config.Ending.EndText ?? string.Empty).EnumerateRunes())
            codepointSet.Add(rune.Value);
        foreach (var rune in (_config.Ending.CopyrightText ?? string.Empty).EnumerateRunes())
            codepointSet.Add(rune.Value);
        int[] codepoints = [.. codepointSet];
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
        _endTextAlpha = 0f;
        _endTextFading = false;
        _endTextFadeElapsed = 0f;
        _endTextFadeIn = false;
        _endTextFadeInElapsed = 0f;
        _endBackgroundActive = false;
        _endBackgroundFading = false;
        _endBackgroundFadeElapsed = 0f;
        _endBackgroundAlpha = 0f;
        _showStartText = false;
        _startTextAlpha = 0f;
        _startTextFading = false;
        _startTextFadeElapsed = 0f;
        _startTextFadeIn = false;
        _startTextFadeInElapsed = 0f;

        IsActive = true;

        // Load emote if provided
        _emoteLoaded = false;
        if (
            !string.IsNullOrEmpty(_config.Ending.EmotePath) && File.Exists(_config.Ending.EmotePath)
        )
        {
            _emoteTexture = ResourceCache.LoadTexture(_config.Ending.EmotePath);
            _emoteLoaded = true;
        }
        // Reset cleanup flag (we have re-initialized a new scene)
        _cleanedUp = false;
        // Reset copyright state
        _copyrightStarted = false;
        _copyrightFadingIn = false;
        _copyrightFadeElapsed = 0f;
        _copyrightAlpha = 0f;
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
            // Return to main menu safely: do not unload resources here. Instead set IsActive false
            // and allow Program.Main to call Cleanup() in a predictable location.
            Console.WriteLine(
                "INFO: EndingScene: ctrl+space pressed - returning to main menu (deferred cleanup)"
            );
            // Set flags to stop updates and schedule cleanup
            IsActive = false;
            _creditsStarted = false;
            // Prevent any lingering start text or end text from briefly displaying while main menu resets
            _showStartText = false;
            _startTextAlpha = 0f;
            _showEndText = false;
            _endTextAlpha = 0f;
            return;
        }

        float dt = Raylib.GetFrameTime();
        _elapsedTime += dt;

        float startDelay = _config.Ending.StartDelay;
        float startTextHideTime = _config.Ending.StartTextHideTime;

        // Start music and credits after delay (only if music hasn't been started or stopped already)
        if (!_musicStarted && !_musicStopped && _elapsedTime >= startDelay)
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

        // Check if credits have finished scrolling off-screen to start end-text sequence
        if (
            _creditsStarted
            && !_endTextStarted
            && _creditsHeight > 0f
            && _creditsScrollY <= -_creditsHeight
        )
        {
            // Start end text sequence
            _endTextStarted = true;
            _showEndText = true;
            _endTextFadeIn = true;
            _endTextFadeInElapsed = 0f;
            _endTextAlpha = 0f;
            _endTextShowElapsed = 0f;
            // Always set plain background when the end-text starts in case we missed the 1s pre-trigger
            if (!_endBackgroundActive)
            {
                _endBackgroundActive = true;
            }
        }
        // Activate plain end background 1s before the end text fades in so we can switch to a solid background
        if (!_endBackgroundActive && _creditsStarted && _creditsHeight > 0f)
        {
            // If the credits will reach the finish line in about 1 second of scrolling time
            float pxBeforeEnd = _creditsScrollSpeed * 1f; // pixels traveled in 1 second
            if (_creditsScrollY <= -_creditsHeight + pxBeforeEnd)
            {
                _endBackgroundActive = true;
                _endBackgroundFading = true;
                _endBackgroundFadeElapsed = 0f;
            }
        }

        // Update end background fade progress if active
        if (_endBackgroundFading)
        {
            _endBackgroundFadeElapsed += dt;
            float duration = Math.Max(0.001f, _config.Ending.EndBackgroundFadeDuration);
            _endBackgroundAlpha = Math.Clamp(_endBackgroundFadeElapsed / duration, 0f, 1f);
            if (_endBackgroundAlpha >= 1f)
            {
                _endBackgroundAlpha = 1f;
                _endBackgroundFading = false;
            }
        }

        // Update music stream if started and not stopped
        if (_musicStarted && !_musicStopped && Raylib.IsAudioDeviceReady())
        {
            Raylib.UpdateMusicStream(_music);

            _musicPlayElapsed += dt;
            float played = Raylib.GetMusicTimePlayed(_music);

            const float tolerance = 0.08f; // small tolerance in seconds
            bool endedByApi = played > 0 && played + tolerance >= _songDuration;
            bool endedByManual = _musicPlayElapsed + tolerance >= _songDuration;
            if (endedByApi || endedByManual)
            {
                // Stop playback explicitly and mark stopped to avoid further updates.
                try
                {
                    Raylib.StopMusicStream(_music);
                }
                catch { }
                _musicStopped = true;
                _musicStarted = false;
                Console.WriteLine(
                    "INFO: MUSIC: Stopped playback after {0:F2}s (duration {1:F2}s, api={2:F2}s)",
                    _musicPlayElapsed,
                    _songDuration,
                    played
                );
            }
        }

        // Update end text fading/visibility timers
        if (_showEndText)
        {
            if (_endTextFadeIn)
            {
                _endTextFadeInElapsed += dt;
                float fadeInDuration = Math.Max(0.001f, _config.Ending.EndTextFadeInDuration);
                _endTextAlpha = Math.Clamp(_endTextFadeInElapsed / fadeInDuration, 0f, 1f);
                if (_endTextAlpha >= 1f)
                {
                    _endTextAlpha = 1f;
                    _endTextFadeIn = false;
                }
            }
            else if (!_endTextFading)
            {
                _endTextShowElapsed += dt;
                if (_endTextShowElapsed >= _config.Ending.EndTextHideTime)
                {
                    _endTextFading = true;
                    _endTextFadeElapsed = 0f;
                }
            }

            if (_endTextFading)
            {
                _endTextFadeElapsed += dt;
                float fadeDuration = Math.Max(0.001f, _config.Ending.EndTextFadeOutDuration);
                _endTextAlpha = 1f - (_endTextFadeElapsed / fadeDuration);
                if (_endTextAlpha <= 0f)
                {
                    _showEndText = false;
                    _endTextFading = false;
                    _endTextAlpha = 0f;
                    // Start copyright fade-in if configured and not started already
                    if (!string.IsNullOrEmpty(_config.Ending.CopyrightText) && !_copyrightStarted)
                    {
                        _copyrightStarted = true;
                        _copyrightFadingIn = true;
                        _copyrightFadeElapsed = 0f;
                        _copyrightAlpha = 0f;
                    }
                }
            }
        }

        // Update copyright fade-in if active
        if (_copyrightFadingIn)
        {
            _copyrightFadeElapsed += dt;
            float dur = Math.Max(0.001f, _config.Ending.CopyrightFadeInDuration);
            _copyrightAlpha = Math.Clamp(_copyrightFadeElapsed / dur, 0f, 1f);
            if (_copyrightAlpha >= 1f)
            {
                _copyrightAlpha = 1f;
                _copyrightFadingIn = false;
            }
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
        // Draw overlay background rectangle (fades in via _endBackgroundAlpha)
        if (_endBackgroundAlpha > 0f)
        {
            var bg = _config.Ending.EndBackgroundColor;
            byte a = (byte)Math.Clamp(bg.A * _endBackgroundAlpha, 0f, 255f);
            // Set alpha on the local copy then draw
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
            textColor.A = (byte)(textColor.A * Math.Clamp(_startTextAlpha, 0f, 1f));
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
            endTextColor.A = (byte)(endTextColor.A * Math.Clamp(_endTextAlpha, 0f, 1f));

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
                byte emoteAlpha = (byte)(Math.Clamp(_endTextAlpha, 0f, 1f) * 255f);
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
                    sectionFontSize,
                    charSpacing,
                    _config.Ending.SectionColor,
                    _config.Ending.SectionFontWeight
                );
                currentY += sectionFontSize + sectionSpacing;
            }
            else if (entry.Section != null)
            {
                // Draw section header left-aligned
                _fontLoader.DrawText(
                    entry.Section,
                    new Vector2(creditsLeftX, currentY),
                    sectionFontSize,
                    charSpacing,
                    _config.Ending.SectionColor,
                    _config.Ending.SectionFontWeight
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
                            _config.Ending.ValuesColor,
                            _config.Ending.ValueFontWeight
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
                                _config.Ending.ValuesColor,
                                _config.Ending.ValueFontWeight
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
                            _config.Ending.ValuesColor,
                            _config.Ending.ValueFontWeight
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
        if (_cleanedUp)
            return;

        // Log memory use to help track potential leaks
        long memBefore = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64;
        Console.WriteLine($"INFO: Cleanup: memory before cleanup {memBefore / 1024 / 1024} MB");

        if (IsActive)
        {
            // Unload only if the texture handles are valid
            try
            {
                if (_backgroundTexture.Id != 0)
                {
                    // Release the cached texture via path
                    ResourceCache.ReleaseTexture(_config.Ending.BackgroundImage);
                    _backgroundTexture = default;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"WARN: Cleanup: failed to unload background texture: {ex.Message}"
                );
            }

            _fontLoader?.Dispose();
            _fontLoader = null;

            if (_emoteLoaded && _emoteTexture.Id != 0)
            {
                try
                {
                    ResourceCache.ReleaseTexture(_config.Ending.EmotePath);
                    _emoteTexture = default;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"WARN: Cleanup: failed to unload emote texture: {ex.Message}"
                    );
                }
                _emoteLoaded = false;
            }

            try
            {
                // Ensure any playing music is stopped before release
                try
                {
                    if (Raylib.IsAudioDeviceReady())
                        Raylib.StopMusicStream(_music);
                }
                catch { }
                ResourceCache.ReleaseMusic(_config.Ending.Music);
                _music = default;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARN: Cleanup: failed to unload music stream: {ex.Message}");
            }

            // Do NOT close audio device here: leaving it open prevents races while UI returns to main menu
            IsActive = false;
        }
        else
        {
            // If not active, attempt to safely unload any remaining allocated textures and fonts
            try
            {
                if (_backgroundTexture.Id != 0)
                {
                    ResourceCache.ReleaseTexture(_config.Ending.BackgroundImage);
                    _backgroundTexture = default;
                }
            }
            catch { }
        }

        _cleanedUp = true;
        // Clear credits to allow memory to be freed.
        _credits.Clear();

        // Force a GC collection and wait to help ensure managed resources are reclaimed.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long memAfter = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64;
        Console.WriteLine(
            $"INFO: Cleanup: memory after cleanup and GC {memAfter / 1024 / 1024} MB (delta {((memAfter - memBefore) / 1024 / 1024)} MB)"
        );
    }

    [GeneratedRegex("(?:,|\\r?\\n|\\\\n)")]
    private static partial Regex MyRegex();
}
