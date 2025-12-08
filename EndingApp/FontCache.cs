using System.Collections.Concurrent;
using Raylib_cs;

namespace EndingApp;

internal static class FontCache
{
    // Internal Key structure is not used; using composite string keys instead

    private static readonly ConcurrentDictionary<string, (Font font, int refCount)> s_fonts = new();

    private static string MakeKey(string path, int size, int[] codepoints)
    {
        // Create fingerprint of codepoints; for brevity, compute a stable hash
        unchecked
        {
            int h = 17;
            foreach (int cp in codepoints)
            {
                h = h * 31 + cp;
            }
            return $"{path.ToLowerInvariant()}::{size}::{h}";
        }
    }

    public static Font LoadFont(string path, int size, int[] codepoints)
    {
        if (string.IsNullOrEmpty(path))
            return default;
        string key = MakeKey(path, size, codepoints);
        var (font, refCount) = s_fonts.AddOrUpdate(
            key,
            k =>
            {
                var f = Raylib.LoadFontEx(path, size, codepoints, codepoints.Length);
                Console.WriteLine($"FontCache: Loaded font {path} size={size} key={k}");
                return (f, 1);
            },
            (k, v) =>
            {
                v.refCount++;
                Console.WriteLine(
                    $"FontCache: Increment font refcount {path} size={size} => {v.refCount}"
                );
                return (v.font, v.refCount);
            }
        );
        return font;
    }

    public static void ReleaseFont(string path, int size, int[] codepoints)
    {
        if (string.IsNullOrEmpty(path))
            return;
        string key = MakeKey(path, size, codepoints);
        if (s_fonts.TryGetValue(key, out var val))
        {
            val.refCount--;
            Console.WriteLine(
                $"FontCache: ReleaseFont refcount {path} size={size} => {val.refCount}"
            );
            if (val.refCount <= 0)
            {
                try
                {
                    Raylib.UnloadFont(val.font);
                    Console.WriteLine($"FontCache: Unloaded font {path} size={size}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FontCache: Error unloading font {path}: {ex.Message}");
                }
                s_fonts.TryRemove(key, out _);
            }
            else
            {
                s_fonts[key] = (val.font, val.refCount);
            }
        }
    }

    public static void ReleaseFont(Font f)
    {
        if (f.Texture.Id == 0)
            return;
        string? matchKey = null;
        foreach (var kvp in s_fonts)
        {
            if (kvp.Value.font.Texture.Id == f.Texture.Id)
            {
                matchKey = kvp.Key;
                break;
            }
        }
        if (matchKey != null && s_fonts.TryGetValue(matchKey, out var val))
        {
            val.refCount--;
            Console.WriteLine(
                $"FontCache: ReleaseFont by instance refcount {matchKey} => {val.refCount}"
            );
            if (val.refCount <= 0)
            {
                try
                {
                    Raylib.UnloadFont(val.font);
                    Console.WriteLine($"FontCache: Unloaded font key {matchKey}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"FontCache: Error unloading font key {matchKey}: {ex.Message}"
                    );
                }
                s_fonts.TryRemove(matchKey, out _);
            }
            else
            {
                s_fonts[matchKey] = (val.font, val.refCount);
            }
        }
    }

    public static void DumpState()
    {
        Console.WriteLine($"FontCache: fonts = {s_fonts.Count}");
        foreach (var kvp in s_fonts)
        {
            Console.WriteLine($"FontCache: font {kvp.Key}, refcount = {kvp.Value.refCount}");
        }
    }
}
