using System.Numerics;
using Raylib_cs;

namespace EndingApp;

/// <summary>
/// Handles font loading with automatic fallback support for Unicode symbols.
/// Uses a primary font for ASCII characters and a symbol font for non-ASCII.
/// </summary>
internal sealed class FontLoader : IDisposable
{
    private Font _primaryFont;
    private Font _symbolFont;
    private readonly HashSet<int> _primaryGlyphs = [];
    private readonly HashSet<int> _symbolGlyphs = [];
    private bool _disposed;

    /// <summary>
    /// Gets the primary font (used for ASCII characters).
    /// </summary>
    public Font PrimaryFont => _primaryFont;

    /// <summary>
    /// Gets the symbol font (used for non-ASCII characters).
    /// </summary>
    public Font SymbolFont => _symbolFont;

    /// <summary>
    /// Loads fonts with the specified codepoints for rendering.
    /// <param name="codepoints">All codepoints that need to be supported.</param>
    /// <param name="textureFilter">Texture filter for font rendering quality.</param>
    public unsafe void Load(
        string primaryFontPath,
        string symbolFontPath,
        int fontSize,
        int[] codepoints,
        TextureFilter textureFilter = TextureFilter.Trilinear
    )
    {
        // ASCII codepoints for primary font (32-126)
        int[] asciiCodepoints = [.. Enumerable.Range(32, 95)];

        // Load primary font with ASCII only
        if (File.Exists(primaryFontPath))
        {
            _primaryFont = Raylib.LoadFontEx(
                primaryFontPath,
                fontSize,
                asciiCodepoints,
                asciiCodepoints.Length
            );
            Raylib.SetTextureFilter(_primaryFont.Texture, textureFilter);
            Console.WriteLine(
                $"FontLoader: Primary font loaded from {primaryFontPath} ({asciiCodepoints.Length} glyphs)"
            );
        }
        else
        {
            Console.WriteLine(
                $"FontLoader: Primary font not found at {primaryFontPath}, using default"
            );
            _primaryFont = Raylib.GetFontDefault();
        }

        // Populate primary glyphs set
        _primaryGlyphs.Clear();
        foreach (int cp in asciiCodepoints)
            _primaryGlyphs.Add(cp);

        // Load symbol font with all codepoints
        if (File.Exists(symbolFontPath))
        {
            _symbolFont = Raylib.LoadFontEx(
                symbolFontPath,
                fontSize,
                codepoints,
                codepoints.Length
            );
            Raylib.SetTextureFilter(_symbolFont.Texture, textureFilter);
            Console.WriteLine(
                $"FontLoader: Symbol font loaded from {symbolFontPath} ({codepoints.Length} glyphs)"
            );
        }
        else
        {
            Console.WriteLine(
                $"FontLoader: Symbol font not found at {symbolFontPath}, using default"
            );
            _symbolFont = Raylib.GetFontDefault();
        }

        // Populate symbol glyphs set
        _symbolGlyphs.Clear();
        if (_symbolFont.GlyphCount > 0)
        {
            for (int i = 0; i < _symbolFont.GlyphCount; i++)
            {
                _symbolGlyphs.Add(_symbolFont.Glyphs[i].Value);
            }
        }
    }

    /// <summary>
    /// Extracts all unique codepoints from the given credits entries.
    /// Includes ASCII baseline (32-126) automatically.
    /// </summary>
    /// <param name="credits">List of credit entries to extract codepoints from.</param>
    /// <returns>Array of unique codepoints.</returns>
    public static int[] ExtractCodepoints(List<CreditEntry> credits)
    {
        var codepointSet = new HashSet<int>();
        // For debug: collect all chars for logging
        var allChars = new List<char>();

        foreach (var entry in credits)
        {
            if (entry.IsSeparator && entry.Separator != null)
            {
                foreach (var rune in entry.Separator.EnumerateRunes())
                {
                    codepointSet.Add(rune.Value);
                    allChars.Add((char)rune.Value);
                }
            }
            else
            {
                if (entry.Section != null)
                {
                    foreach (var rune in entry.Section.EnumerateRunes())
                    {
                        codepointSet.Add(rune.Value);
                        allChars.Add((char)rune.Value);
                    }
                }

                foreach (string value in entry.Values)
                {
                    foreach (var rune in value.EnumerateRunes())
                    {
                        codepointSet.Add(rune.Value);
                        allChars.Add((char)rune.Value);
                    }
                }
            }
        }

        // Add ASCII baseline (32-126)
        for (int i = 32; i <= 126; i++)
        {
            codepointSet.Add(i);
            allChars.Add((char)i);
        }

        // Always add hiragana (U+3040–U+309F)
        for (int i = 0x3040; i <= 0x309F; i++)
            codepointSet.Add(i);
        // Always add katakana (U+30A0–U+30FF)
        for (int i = 0x30A0; i <= 0x30FF; i++)
            codepointSet.Add(i);
        // Always add common kanji (U+4E00–U+9FFF)
        for (int i = 0x4E00; i <= 0x9FFF; i++)
            codepointSet.Add(i);

        // Log all extracted chars for debug

        return [.. codepointSet];
    }

    /// <summary>
    /// Gets the appropriate font for a given codepoint.
    /// </summary>
    /// <param name="codepoint">The Unicode codepoint.</param>
    /// <returns>The font that should be used for this codepoint.</returns>
    public Font GetFontForCodepoint(int codepoint)
    {
        return _primaryGlyphs.Contains(codepoint) ? _primaryFont : _symbolFont;
    }

    /// <summary>
    /// Measures the width of text using font fallback.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="fontSize">Font size.</param>
    /// <param name="spacing">Character spacing.</param>
    /// <returns>The size of the text.</returns>
    public Vector2 MeasureText(string text, float fontSize, float spacing)
    {
        float width = 0;
        float height = fontSize;

        foreach (var rune in text.EnumerateRunes())
        {
            var fontToUse = GetFontForCodepoint(rune.Value);
            var charSize = Raylib.MeasureTextEx(fontToUse, rune.ToString(), fontSize, spacing);
            width += charSize.X;
        }

        return new Vector2(width, height);
    }

    /// <summary>
    /// Draws text with automatic font fallback for non-ASCII characters.
    /// Uses DrawTextCodepoint for proper Unicode handling.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="position">Position to draw at.</param>
    /// <param name="fontSize">Font size.</param>
    /// <param name="spacing">Character spacing.</param>
    /// <param name="color">Text color.</param>
    public unsafe void DrawText(
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

            if (_primaryGlyphs.Contains(codepoint))
            {
                fontToUse = _primaryFont;
                scale = primaryScale;
            }
            else
            {
                fontToUse = _symbolFont;
                scale = symbolScale;
            }

            // Use DrawTextCodepoint for proper Unicode handling
            Vector2 charPos = new(position.X + xOffset, position.Y);
            int glyphIndex = Raylib.GetGlyphIndex(fontToUse, codepoint);
            if (glyphIndex < 0 || glyphIndex >= fontToUse.GlyphCount)
            {
                continue;
            }
            Raylib.DrawTextCodepoint(fontToUse, codepoint, charPos, fontSize, color);

            // Calculate advance width
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

    /// <summary>
    /// Draws text centered horizontally at the given Y position.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="centerX">The X coordinate to center on.</param>
    /// <param name="y">The Y coordinate.</param>
    /// <param name="fontSize">Font size.</param>
    /// <param name="spacing">Character spacing.</param>
    /// <param name="color">Text color.</param>
    public void DrawTextCentered(
        string text,
        float centerX,
        float y,
        float fontSize,
        float spacing,
        Color color
    )
    {
        var size = MeasureText(text, fontSize, spacing);
        DrawText(text, new Vector2(centerX - size.X / 2, y), fontSize, spacing, color);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (
            _primaryFont.Texture.Id != 0
            && _primaryFont.Texture.Id != Raylib.GetFontDefault().Texture.Id
        )
            Raylib.UnloadFont(_primaryFont);
        if (
            _symbolFont.Texture.Id != 0
            && _symbolFont.Texture.Id != Raylib.GetFontDefault().Texture.Id
        )
            Raylib.UnloadFont(_symbolFont);

        _disposed = true;
    }
}
