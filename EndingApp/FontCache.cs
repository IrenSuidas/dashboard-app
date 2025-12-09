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
                Logger.Info("FontCache: Loaded font {0} size={1} key={2}", path, size, k);
                return (f, 1);
            },
            (k, v) =>
            {
                v.refCount++;
                Logger.Debug(
                    "FontCache: Increment font refcount {0} size={1} => {2}",
                    path,
                    size,
                    v.refCount
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
            Logger.Debug(
                "FontCache: ReleaseFont refcount {0} size={1} => {2}",
                path,
                size,
                val.refCount
            );
            if (val.refCount <= 0)
            {
                try
                {
                    Raylib.UnloadFont(val.font);
                    Logger.Info("FontCache: Unloaded font {0} size={1}", path, size);
                }
                catch (Exception ex)
                {
                    Logger.Warn("FontCache: Error unloading font {0}: {1}", path, ex.Message);
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
            Logger.Debug(
                "FontCache: ReleaseFont by instance refcount {0} => {1}",
                matchKey,
                val.refCount
            );
            if (val.refCount <= 0)
            {
                try
                {
                    Raylib.UnloadFont(val.font);
                    Logger.Info("FontCache: Unloaded font key {0}", matchKey);
                }
                catch (Exception ex)
                {
                    Logger.Warn(
                        "FontCache: Error unloading font key {0}: {1}",
                        matchKey,
                        ex.Message
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
        Logger.Info("FontCache: fonts = {0}", s_fonts.Count);
        foreach (var kvp in s_fonts)
        {
            Logger.Info("FontCache: font {0}, refcount = {1}", kvp.Key, kvp.Value.refCount);
        }
    }
}
