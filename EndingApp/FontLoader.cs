using System.Numerics;
using Raylib_cs;

namespace EndingApp;

/// <summary>
/// Handles font loading with automatic fallback support for Unicode symbols.
/// Uses a primary font for ASCII characters and a symbol font for non-ASCII.
/// Only loads the exact codepoints needed from the credits for memory efficiency.
/// </summary>
internal sealed class FontLoader : IDisposable
{
    private static readonly string[] s_italicCandidates =
    [
        "-Italic",
        "Italic",
        "_Italic",
        "-italic",
    ];
    private static readonly string[] s_boldCandidates = ["-Bold", "Bold", "_Bold", "-bold"];
    private static readonly string[] s_boldItalicCandidates =
    [
        "-BoldItalic",
        "-BoldItalic",
        "BoldItalic",
        "-Bold-Italic",
        "Bold-Italic",
        "_BoldItalic",
    ];
    private Font _primaryRegular;
    private Font _primaryItalic;
    private Font _primaryBold;
    private Font _primaryBoldItalic;
    private Font _symbolRegular;
    private Font _symbolItalic;
    private Font _symbolBold;
    private Font _symbolBoldItalic;
    private readonly HashSet<int> _primaryGlyphs = [];
    private readonly HashSet<int> _symbolGlyphs = [];
    private readonly HashSet<uint> _loggedFontTextureIds = [];
    private bool _disposed;

    /// <summary>
    /// Gets the primary font (used for ASCII characters).
    /// </summary>
    public Font PrimaryFont => _primaryRegular;
    public Font PrimaryItalicFont => _primaryItalic;
    public Font PrimaryBoldFont => _primaryBold;
    public Font PrimaryBoldItalicFont => _primaryBoldItalic;

    /// <summary>
    /// Gets the symbol font (used for non-ASCII characters).
    /// </summary>
    public Font SymbolFont => _symbolRegular;

    /// <summary>
    /// Loads fonts with the specified codepoints for rendering.
    /// </summary>
    /// <param name="primaryFontPath">Path to primary font (ASCII characters).</param>
    /// <param name="symbolFontPath">Path to symbol font (symbols and Japanese characters).</param>
    /// <param name="fontSize">Font size to load.</param>
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
        // Include a small set of common extended Latin characters into the primary font
        // so that symbols like © (U+00A9) render in the primary font style when possible.
        var primaryCpSet = new HashSet<int>(asciiCodepoints);
        foreach (int cp in codepoints)
        {
            if (cp <= 255) // include Latin-1 range for primary font
                primaryCpSet.Add(cp);
        }
        int[] primaryCodepoints = primaryCpSet.OrderBy(x => x).ToArray();

        // Load primary font with ASCII only
        if (File.Exists(primaryFontPath))
        {
            _primaryRegular = FontCache.LoadFont(primaryFontPath, fontSize, primaryCodepoints);
            Raylib.SetTextureFilter(_primaryRegular.Texture, textureFilter);
            Console.WriteLine(
                $"FontLoader: Primary regular font loaded from {primaryFontPath} ({asciiCodepoints.Length} glyphs)"
            );
        }
        else
        {
            Console.WriteLine(
                $"FontLoader: Primary font not found at {primaryFontPath}, using default"
            );
            _primaryRegular = Raylib.GetFontDefault();
        }

        // Populate primary glyphs set
        _primaryGlyphs.Clear();
        foreach (int cp in asciiCodepoints)
            _primaryGlyphs.Add(cp);

        // Attempt to load primary font variants (Italic/Bold/BoldItalic) from common variant filenames
        string dir = Path.GetDirectoryName(primaryFontPath) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(primaryFontPath);
        string ext = Path.GetExtension(primaryFontPath);

        Font? TryLoadVariant(string[] candidates)
        {
            foreach (string cand in candidates)
            {
                string candidatePath = Path.Combine(dir, baseName + cand + ext);
                // Also try variants where baseName may include '-Regular' suffix (e.g. 'NotoSansJP-Regular')
                string baseWithoutRegular = baseName;
                if (baseWithoutRegular.EndsWith("-Regular", StringComparison.OrdinalIgnoreCase))
                    baseWithoutRegular = baseWithoutRegular.Substring(
                        0,
                        baseWithoutRegular.Length - "-Regular".Length
                    );
                else if (
                    baseWithoutRegular.EndsWith("_Regular", StringComparison.OrdinalIgnoreCase)
                )
                    baseWithoutRegular = baseWithoutRegular.Substring(
                        0,
                        baseWithoutRegular.Length - "_Regular".Length
                    );
                string candidatePath2 = Path.Combine(dir, baseWithoutRegular + cand + ext);
                if (File.Exists(candidatePath))
                {
                    try
                    {
                        var f = FontCache.LoadFont(candidatePath, fontSize, primaryCodepoints);
                        Raylib.SetTextureFilter(f.Texture, textureFilter);
                        Console.WriteLine(
                            $"FontLoader: Primary variant loaded from {candidatePath}"
                        );
                        return f;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"FontLoader: Failed to load font {candidatePath}: {ex.Message}"
                        );
                        // continue to try other candidates
                    }
                }

                if (
                    !string.Equals(
                        candidatePath2,
                        candidatePath,
                        StringComparison.OrdinalIgnoreCase
                    ) && File.Exists(candidatePath2)
                )
                {
                    try
                    {
                        var f = FontCache.LoadFont(candidatePath2, fontSize, primaryCodepoints);
                        Raylib.SetTextureFilter(f.Texture, textureFilter);
                        Console.WriteLine(
                            $"FontLoader: Primary variant loaded from {candidatePath2}"
                        );
                        return f;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"FontLoader: Failed to load font {candidatePath2}: {ex.Message}"
                        );
                    }
                }
            }
            return null;
        }

        // Load symbol font with all codepoints (symbols + Japanese)
        if (File.Exists(symbolFontPath))
        {
            _symbolRegular = FontCache.LoadFont(symbolFontPath, fontSize, codepoints);
            Raylib.SetTextureFilter(_symbolRegular.Texture, textureFilter);
            Console.WriteLine(
                $"FontLoader: Symbol regular font loaded from {symbolFontPath} ({_symbolRegular.GlyphCount} glyphs)"
            );
        }
        else
        {
            Console.WriteLine(
                $"FontLoader: Symbol font not found at {symbolFontPath}, using default"
            );
            _symbolRegular = Raylib.GetFontDefault();
        }

        // Populate symbol glyphs set
        _symbolGlyphs.Clear();
        if (_symbolRegular.GlyphCount > 0)
        {
            for (int i = 0; i < _symbolRegular.GlyphCount; i++)
            {
                _symbolGlyphs.Add(_symbolRegular.Glyphs[i].Value);
            }
        }

        // Attempts to load available variants for the primary font family
        if (_primaryRegular.Texture.Id != 0)
        {
            var italicVariant = TryLoadVariant(s_italicCandidates);
            _primaryItalic = italicVariant ?? _primaryRegular;
            var boldVariant = TryLoadVariant(s_boldCandidates);
            _primaryBold = boldVariant ?? _primaryRegular;
            var boldItalicVariant = TryLoadVariant(s_boldItalicCandidates);
            if (boldItalicVariant.HasValue)
                _primaryBoldItalic = boldItalicVariant.Value;
            else if (_primaryBold.Texture.Id != 0)
                _primaryBoldItalic = _primaryBold;
            else if (_primaryItalic.Texture.Id != 0)
                _primaryBoldItalic = _primaryItalic;
            else
                _primaryBoldItalic = _primaryRegular;
        }

        // Attempt to load symbol font variants from the symbol font path
        string sdir = Path.GetDirectoryName(symbolFontPath) ?? string.Empty;
        string sbaseName = Path.GetFileNameWithoutExtension(symbolFontPath);
        string sext = Path.GetExtension(symbolFontPath);
        Font? TryLoadSymbolVariant(string[] candidates)
        {
            foreach (string cand in candidates)
            {
                string candidatePath = Path.Combine(sdir, sbaseName + cand + sext);
                string sBaseWithoutRegular = sbaseName;
                if (sBaseWithoutRegular.EndsWith("-Regular", StringComparison.OrdinalIgnoreCase))
                    sBaseWithoutRegular = sBaseWithoutRegular.Substring(
                        0,
                        sBaseWithoutRegular.Length - "-Regular".Length
                    );
                else if (
                    sBaseWithoutRegular.EndsWith("_Regular", StringComparison.OrdinalIgnoreCase)
                )
                    sBaseWithoutRegular = sBaseWithoutRegular.Substring(
                        0,
                        sBaseWithoutRegular.Length - "_Regular".Length
                    );
                string candidatePath2 = Path.Combine(sdir, sBaseWithoutRegular + cand + sext);
                if (File.Exists(candidatePath))
                {
                    try
                    {
                        var f = FontCache.LoadFont(candidatePath, fontSize, codepoints);
                        Raylib.SetTextureFilter(f.Texture, textureFilter);
                        Console.WriteLine(
                            $"FontLoader: Symbol variant loaded from {candidatePath}"
                        );
                        return f;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"FontLoader: Failed to load symbol font {candidatePath}: {ex.Message}"
                        );
                    }
                }
                if (
                    !string.Equals(
                        candidatePath2,
                        candidatePath,
                        StringComparison.OrdinalIgnoreCase
                    ) && File.Exists(candidatePath2)
                )
                {
                    try
                    {
                        var f = FontCache.LoadFont(candidatePath2, fontSize, codepoints);
                        Raylib.SetTextureFilter(f.Texture, textureFilter);
                        Console.WriteLine(
                            $"FontLoader: Symbol variant loaded from {candidatePath2}"
                        );
                        return f;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"FontLoader: Failed to load symbol font {candidatePath2}: {ex.Message}"
                        );
                    }
                }
            }
            return null;
        }

        if (_symbolRegular.Texture.Id != 0)
        {
            var italicVariant = TryLoadSymbolVariant(s_italicCandidates);
            _symbolItalic = italicVariant ?? _symbolRegular;
            var boldVariant = TryLoadSymbolVariant(s_boldCandidates);
            _symbolBold = boldVariant ?? _symbolRegular;
            var boldItalicVariant = TryLoadSymbolVariant(s_boldItalicCandidates);
            if (boldItalicVariant.HasValue)
                _symbolBoldItalic = boldItalicVariant.Value;
            else if (_symbolBold.Texture.Id != 0)
                _symbolBoldItalic = _symbolBold;
            else if (_symbolItalic.Texture.Id != 0)
                _symbolBoldItalic = _symbolItalic;
            else
                _symbolBoldItalic = _symbolRegular;
        }
    }

    /// <summary>
    /// Extracts all unique codepoints from the given credits entries.
    /// Includes ASCII baseline (32-126) automatically.
    /// Only extracts the exact characters needed - does NOT load full CJK ranges.
    /// </summary>
    /// <param name="credits">List of credit entries to extract codepoints from.</param>
    /// <returns>Array of unique codepoints.</returns>
    public static int[] ExtractCodepoints(List<CreditEntry> credits)
    {
        var codepointSet = new HashSet<int>();

        foreach (var entry in credits)
        {
            if (entry.IsSeparator && entry.Separator != null)
            {
                foreach (var rune in entry.Separator.EnumerateRunes())
                {
                    codepointSet.Add(rune.Value);
                }
            }
            else
            {
                if (entry.Section != null)
                {
                    foreach (var rune in entry.Section.EnumerateRunes())
                    {
                        codepointSet.Add(rune.Value);
                    }
                }

                foreach (string value in entry.Values)
                {
                    foreach (var rune in value.EnumerateRunes())
                    {
                        codepointSet.Add(rune.Value);
                    }
                }
            }
        }

        // Add ASCII baseline (32-126)
        for (int i = 32; i <= 126; i++)
        {
            codepointSet.Add(i);
        }

        var nonAscii = codepointSet.Where(cp => cp > 126).OrderBy(cp => cp).ToList();
        if (nonAscii.Count > 0)
        {
            string chars = string.Join("", nonAscii.Select(cp => char.ConvertFromUtf32(cp)));
            Console.WriteLine(
                $"FontLoader: Extracted {nonAscii.Count} non-ASCII codepoints: {chars}"
            );
        }

        return [.. codepointSet];
    }

    /// <summary>
    /// Extracts unique codepoints from a single string. Useful for singling out glyphs not present in credits: start text, end text, etc.
    /// </summary>
    /// <param name="s">The string to extract runes from.</param>
    /// <returns>Array of unique codepoints.</returns>
    public static int[] ExtractCodepointsFromString(string? s)
    {
        var cpSet = new HashSet<int>();
        if (string.IsNullOrEmpty(s))
            return [.. cpSet];
        foreach (var rune in s.EnumerateRunes())
            cpSet.Add(rune.Value);

        // Add ASCII baseline (32-126) to ensure ASCII glyphs are available in primary
        for (int i = 32; i <= 126; i++)
            cpSet.Add(i);

        return [.. cpSet];
    }

    /// <summary>
    /// Gets the appropriate font for a given codepoint.
    /// </summary>
    /// <param name="codepoint">The Unicode codepoint.</param>
    /// <returns>The font that should be used for this codepoint.</returns>
    public Font GetFontForCodepoint(int codepoint, FontWeight weight)
    {
        if (!_primaryGlyphs.Contains(codepoint))
        {
            switch (weight)
            {
                case FontWeight.Bold:
                    if (_symbolBold.Texture.Id != 0)
                        return _symbolBold;
                    return _symbolRegular;
                case FontWeight.Italic:
                    if (_symbolItalic.Texture.Id != 0)
                        return _symbolItalic;
                    return _symbolRegular;
                case FontWeight.BoldItalic:
                    if (_symbolBoldItalic.Texture.Id != 0)
                        return _symbolBoldItalic;
                    if (_symbolBold.Texture.Id != 0)
                        return _symbolBold;
                    if (_symbolItalic.Texture.Id != 0)
                        return _symbolItalic;
                    return _symbolRegular;
                default:
                    return _symbolRegular;
            }
        }

        switch (weight)
        {
            case FontWeight.Regular:
                if (_primaryRegular.Texture.Id != 0)
                    return _primaryRegular;
                if (_primaryBold.Texture.Id != 0)
                    return _primaryBold;
                if (_primaryItalic.Texture.Id != 0)
                    return _primaryItalic;
                if (_primaryBoldItalic.Texture.Id != 0)
                    return _primaryBoldItalic;
                return _primaryRegular;
            case FontWeight.Italic:
                if (_primaryItalic.Texture.Id != 0)
                    return _primaryItalic;
                return _primaryRegular;
            case FontWeight.Bold:
                if (_primaryBold.Texture.Id != 0)
                    return _primaryBold;
                return _primaryRegular;
            case FontWeight.BoldItalic:
                if (_primaryBoldItalic.Texture.Id != 0)
                    return _primaryBoldItalic;
                if (_primaryBold.Texture.Id != 0)
                    return _primaryBold;
                if (_primaryItalic.Texture.Id != 0)
                    return _primaryItalic;
                return _primaryRegular;
            default:
                return _primaryRegular;
        }
    }

    /// <summary>
    /// Measures the width of text using font fallback.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="fontSize">Font size.</param>
    /// <param name="spacing">Character spacing.</param>
    /// <returns>The size of the text.</returns>
    public Vector2 MeasureText(
        string text,
        float fontSize,
        float spacing,
        FontWeight weight = FontWeight.Regular
    )
    {
        float width = 0;
        float height = fontSize;

        foreach (var rune in text.EnumerateRunes())
        {
            var fontToUse = GetFontForCodepoint(rune.Value, weight);
            if (!_loggedFontTextureIds.Contains(fontToUse.Texture.Id))
            {
                Console.WriteLine(
                    $"FontLoader: Using font texture {fontToUse.Texture.Id} for codepoint {rune.Value} (weight {weight})"
                );
                _loggedFontTextureIds.Add(fontToUse.Texture.Id);
            }
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
        Color color,
        FontWeight weight = FontWeight.Regular
    )
    {
        float xOffset = 0;
        // NOTE: primary/font variants can have different BaseSize; compute per glyph

        foreach (var rune in text.EnumerateRunes())
        {
            int codepoint = rune.Value;
            Font fontToUse;
            float scale;

            fontToUse = GetFontForCodepoint(codepoint, weight);
            scale = fontToUse.BaseSize > 0 ? fontSize / fontToUse.BaseSize : 1f;

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
        Color color,
        FontWeight weight = FontWeight.Regular
    )
    {
        var size = MeasureText(text, fontSize, spacing, weight);
        DrawText(text, new Vector2(centerX - size.X / 2, y), fontSize, spacing, color, weight);
    }

    /// <summary>
    /// Draws text right-aligned at the given position.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="rightX">The X coordinate for the right edge of the text.</param>
    /// <param name="y">The Y coordinate.</param>
    /// <param name="fontSize">Font size.</param>
    /// <param name="spacing">Character spacing.</param>
    /// <param name="color">Text color.</param>
    public void DrawTextRightAligned(
        string text,
        float rightX,
        float y,
        float fontSize,
        float spacing,
        Color color,
        FontWeight weight = FontWeight.Regular
    )
    {
        var size = MeasureText(text, fontSize, spacing, weight);
        DrawText(text, new Vector2(rightX - size.X, y), fontSize, spacing, color, weight);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        static void UnloadIfCustom(Font f, string name, string? pathKey = null)
        {
            if (f.Texture.Id != 0 && f.Texture.Id != Raylib.GetFontDefault().Texture.Id)
            {
                Console.WriteLine($"FontLoader: unloading font {name} (texture id {f.Texture.Id})");
                // Prefer releasing via font cache by instance
                try
                {
                    FontCache.ReleaseFont(f);
                }
                catch
                {
                    try
                    {
                        Raylib.UnloadFont(f);
                    }
                    catch { }
                }
            }
        }

        UnloadIfCustom(_primaryRegular, "primaryRegular");
        if (_primaryBold.Texture.Id != 0 && _primaryBold.Texture.Id != _primaryRegular.Texture.Id)
            UnloadIfCustom(_primaryBold, "primaryBold");
        if (
            _primaryItalic.Texture.Id != 0
            && _primaryItalic.Texture.Id != _primaryRegular.Texture.Id
        )
            UnloadIfCustom(_primaryItalic, "primaryItalic");
        if (
            _primaryBoldItalic.Texture.Id != 0
            && _primaryBoldItalic.Texture.Id != _primaryRegular.Texture.Id
        )
            UnloadIfCustom(_primaryBoldItalic, "primaryBoldItalic");
        if (
            _symbolRegular.Texture.Id != 0
            && _symbolRegular.Texture.Id != Raylib.GetFontDefault().Texture.Id
        )
            UnloadIfCustom(_symbolRegular, "symbolRegular");
        if (_symbolBold.Texture.Id != 0 && _symbolBold.Texture.Id != _symbolRegular.Texture.Id)
            UnloadIfCustom(_symbolBold, "symbolBold");
        if (_symbolItalic.Texture.Id != 0 && _symbolItalic.Texture.Id != _symbolRegular.Texture.Id)
            UnloadIfCustom(_symbolItalic, "symbolItalic");
        if (
            _symbolBoldItalic.Texture.Id != 0
            && _symbolBoldItalic.Texture.Id != _symbolRegular.Texture.Id
        )
            UnloadIfCustom(_symbolBoldItalic, "symbolBoldItalic");

        // Clear references and logging sets so no managed objects keep memory alive
        _loggedFontTextureIds.Clear();
        _primaryGlyphs.Clear();
        _symbolGlyphs.Clear();
        _primaryRegular = default;
        _primaryBold = default;
        _primaryItalic = default;
        _primaryBoldItalic = default;
        _symbolRegular = default;
        _symbolBold = default;
        _symbolItalic = default;
        _symbolBoldItalic = default;

        _disposed = true;
        // Null out fields to allow GC to reclaim references
        _primaryRegular = default;
        _primaryBold = default;
        _primaryItalic = default;
        _primaryBoldItalic = default;
        _symbolRegular = default;
        _symbolBold = default;
        _symbolItalic = default;
        _symbolBoldItalic = default;
        // Force GC to allow memory to be reclaimed by the runtime sooner
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
