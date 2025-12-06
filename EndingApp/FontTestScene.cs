using System.Numerics;
using Raylib_cs;

namespace EndingApp;

/// <summary>
/// Test scene for font rendering with proper glyph detection and fallback.
/// Uses Approach 3 from FONT.md research: merged font atlas concept.
/// </summary>
internal sealed class FontTestScene
{
    private List<CreditEntry> _credits = [];
    private Font _primaryFont;
    private Font _symbolFont;
    private HashSet<int> _primaryGlyphs = [];
    private int[] _allCodepoints = [];

    public bool IsActive { get; private set; }

    public void Start()
    {
        // Clear transparent window flag
        Raylib.ClearWindowState(ConfigFlags.TransparentWindow);

        // Resize window
        Raylib.SetWindowSize(1280, 720);

        // Center window
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition((monitorWidth - 1280) / 2, (monitorHeight - 720) / 2);

        // Load credits to get all required codepoints
        _credits = CreditsReader.Read("credits.yaml");

        // Collect all unique codepoints from credits
        var codepointSet = new HashSet<int>();
        foreach (var entry in _credits)
        {
            if (entry.IsSeparator && entry.Separator != null)
            {
                foreach (var rune in entry.Separator.EnumerateRunes())
                    codepointSet.Add(rune.Value);
            }
            else
            {
                if (entry.Section != null)
                {
                    foreach (var rune in entry.Section.EnumerateRunes())
                        codepointSet.Add(rune.Value);
                }
                foreach (string value in entry.Values)
                {
                    foreach (var rune in value.EnumerateRunes())
                        codepointSet.Add(rune.Value);
                }
            }
        }

        // Add ASCII baseline
        for (int i = 32; i <= 126; i++)
            codepointSet.Add(i);

        _allCodepoints = [.. codepointSet];
        Console.WriteLine($"Total unique codepoints needed: {_allCodepoints.Length}");

        // Strategy: Load primary font with ONLY ASCII (what it definitely supports)
        // Then load symbol font with the full codepoint set
        // Use simple heuristic: ASCII goes to primary, everything else to symbol font

        // Load primary font (Georgia) with just ASCII
        string primaryFontPath = "assets/fonts/georgia.ttf";
        int[] asciiCodepoints = [.. Enumerable.Range(32, 95)]; // ASCII 32-126
        _primaryFont = Raylib.LoadFontEx(
            primaryFontPath,
            64,
            asciiCodepoints,
            asciiCodepoints.Length
        );
        Raylib.SetTextureFilter(_primaryFont.Texture, TextureFilter.Bilinear); // Sharp text rendering
        Console.WriteLine(
            $"Primary font (Georgia) loaded with {asciiCodepoints.Length} ASCII glyphs"
        );

        // Populate primary glyphs set with ASCII range
        _primaryGlyphs = [.. asciiCodepoints];

        // Find non-ASCII codepoints that need symbol font
        int[] nonAsciiCodepoints = [.. _allCodepoints.Where(cp => cp > 126)];
        Console.WriteLine($"Non-ASCII codepoints needed: {nonAsciiCodepoints.Length}");
        foreach (int cp in nonAsciiCodepoints)
        {
            string display = cp <= 0xFFFF ? ((char)cp).ToString() : char.ConvertFromUtf32(cp);
            Console.WriteLine($"  Symbol needed: U+{cp:X4} ({display})");
        }

        // Load symbol font (DejaVuSans) - has good coverage for box drawing and symbols
        string symbolFontPath = "assets/fonts/DejaVuSans.ttf";
        if (File.Exists(symbolFontPath))
        {
            _symbolFont = Raylib.LoadFontEx(
                symbolFontPath,
                64,
                _allCodepoints,
                _allCodepoints.Length
            );
            Raylib.SetTextureFilter(_symbolFont.Texture, TextureFilter.Bilinear); // Sharp text rendering
            Console.WriteLine(
                $"Symbol font (DejaVuSans) loaded with {_allCodepoints.Length} glyphs"
            );
        }
        else
        {
            Console.WriteLine($"Symbol font not found at: {symbolFontPath}");
            _symbolFont = Raylib.GetFontDefault();
        }

        IsActive = true;
    }

    /// <summary>
    /// Detects which glyphs are actually available in a font using the correct method.
    /// Key insight: GetGlyphInfo(font, codepoint).Value == codepoint means glyph exists.
    /// </summary>
    private static HashSet<int> DetectAvailableGlyphs(Font font, int[] codepoints)
    {
        var available = new HashSet<int>();
        foreach (int cp in codepoints)
        {
            var info = Raylib.GetGlyphInfo(font, cp);
            if (info.Value == cp)
            {
                available.Add(cp);
            }
        }
        return available;
    }

    /// <summary>
    /// Checks if a specific codepoint exists in the font.
    /// </summary>
    private static bool HasGlyph(Font font, int codepoint)
    {
        var info = Raylib.GetGlyphInfo(font, codepoint);
        return info.Value == codepoint;
    }

    public void Update()
    {
        if (!IsActive)
            return;

        // Check for Escape or Ctrl+Space to exit
        if (
            Raylib.IsKeyPressed(KeyboardKey.Escape)
            || (
                (
                    Raylib.IsKeyDown(KeyboardKey.LeftControl)
                    || Raylib.IsKeyDown(KeyboardKey.RightControl)
                ) && Raylib.IsKeyPressed(KeyboardKey.Space)
            )
        )
        {
            Stop();
        }
    }

    public void Stop()
    {
        if (!IsActive)
            return;

        Raylib.UnloadFont(_primaryFont);
        if (_symbolFont.Texture.Id != Raylib.GetFontDefault().Texture.Id)
        {
            Raylib.UnloadFont(_symbolFont);
        }

        // Restore window
        Raylib.SetWindowState(ConfigFlags.TransparentWindow);
        Raylib.SetWindowSize(400, 200);
        int monitorWidth = Raylib.GetMonitorWidth(Raylib.GetCurrentMonitor());
        int monitorHeight = Raylib.GetMonitorHeight(Raylib.GetCurrentMonitor());
        Raylib.SetWindowPosition((monitorWidth - 400) / 2, (monitorHeight - 200) / 2);

        IsActive = false;
    }

    public void Draw()
    {
        if (!IsActive)
            return;

        Raylib.DrawRectangle(0, 0, 1280, 720, Color.Black);

        // Draw title
        Raylib.DrawText("Font Test Scene - Press ESC to exit", 20, 20, 24, Color.Yellow);
        Raylib.DrawText(
            "Testing Approach 3: Per-character font selection with proper glyph detection",
            20,
            50,
            18,
            Color.Gray
        );

        // Draw credits statically
        float y = 100;
        const float fontSize = 32;
        const float spacing = 2;

        foreach (var entry in _credits)
        {
            if (entry.IsSeparator && entry.Separator != null)
            {
                DrawTextWithFallback(
                    entry.Separator,
                    new Vector2(50, y),
                    fontSize,
                    spacing,
                    Color.White
                );
                y += 50;
            }
            else if (entry.Section != null)
            {
                // Section header in gold
                DrawTextWithFallback(
                    entry.Section,
                    new Vector2(50, y),
                    fontSize + 8,
                    spacing,
                    new Color(255, 220, 150, 255)
                );
                y += 50;

                // Values
                foreach (string value in entry.Values)
                {
                    DrawTextWithFallback(value, new Vector2(80, y), fontSize, spacing, Color.White);
                    y += 40;
                }
                y += 20;
            }

            if (y > 680)
                break; // Stop if we run out of space
        }
    }

    /// <summary>
    /// Draws text using the primary font where glyphs exist, falling back to symbol font.
    /// Uses DrawTextCodepoint for proper Unicode handling (avoids Windows marshaling issues).
    /// </summary>
    private unsafe void DrawTextWithFallback(
        string text,
        Vector2 position,
        float fontSize,
        float spacing,
        Color color
    )
    {
        float xOffset = 0;
        float primaryScale = fontSize / _primaryFont.BaseSize;
        float symbolScale = fontSize / _symbolFont.BaseSize;

        foreach (var rune in text.EnumerateRunes())
        {
            int codepoint = rune.Value;
            Font fontToUse;
            float scale;

            // Simple heuristic: ASCII goes to primary (Georgia), everything else to symbol font (DejaVuSans)
            if (_primaryGlyphs.Contains(codepoint))
            {
                fontToUse = _primaryFont;
                scale = primaryScale;
            }
            else
            {
                // Non-ASCII - use symbol font
                fontToUse = _symbolFont;
                scale = symbolScale;
            }

            // Use DrawTextCodepoint instead of DrawTextEx for proper Unicode handling
            Vector2 charPos = new(position.X + xOffset, position.Y);
            Raylib.DrawTextCodepoint(fontToUse, codepoint, charPos, fontSize, color);

            // Calculate advance width
            int glyphIndex = Raylib.GetGlyphIndex(fontToUse, codepoint);
            float advance;
            if (fontToUse.Glyphs[glyphIndex].AdvanceX > 0)
            {
                advance = fontToUse.Glyphs[glyphIndex].AdvanceX * scale;
            }
            else
            {
                advance = fontToUse.Recs[glyphIndex].Width * scale;
            }
            xOffset += advance + spacing;
        }
    }

    public void Cleanup()
    {
        if (IsActive)
        {
            Raylib.UnloadFont(_primaryFont);
            if (_symbolFont.Texture.Id != Raylib.GetFontDefault().Texture.Id)
            {
                Raylib.UnloadFont(_symbolFont);
            }
            IsActive = false;
        }
    }
}
