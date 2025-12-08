using System.Collections.Concurrent;
using Raylib_cs;

namespace EndingApp;

internal static class ResourceCache
{
    private static readonly ConcurrentDictionary<
        string,
        (Texture2D texture, int refCount)
    > s_textures = new();
    private static readonly ConcurrentDictionary<string, (Music music, int refCount)> _musics =
        new();

    public static Texture2D LoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path))
            return default;
        string key = path.ToLowerInvariant();
        var (texture, refCount) = s_textures.AddOrUpdate(
            key,
            k =>
            {
                var t = Raylib.LoadTexture(path);
                Console.WriteLine($"ResourceCache: Loaded texture {path} (id={t.Id})");
                return (t, 1);
            },
            (k, v) =>
            {
                v.refCount++;
                Console.WriteLine(
                    $"ResourceCache: Increment texture refcount {path} => {v.refCount}"
                );
                return (v.texture, v.refCount);
            }
        );
        return texture;
    }

    public static void ReleaseTexture(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        string key = path.ToLowerInvariant();
        if (s_textures.TryGetValue(key, out var val))
        {
            val.refCount--;
            Console.WriteLine($"ResourceCache: ReleaseTexture refcount {path} => {val.refCount}");
            if (val.refCount <= 0)
            {
                try
                {
                    if (val.texture.Id != 0)
                        Raylib.UnloadTexture(val.texture);
                    Console.WriteLine(
                        $"ResourceCache: Unloaded texture {path} (id={val.texture.Id})"
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"ResourceCache: Error unloading texture {path}: {ex.Message}"
                    );
                }
                s_textures.TryRemove(key, out _);
            }
            else
            {
                s_textures[key] = (val.texture, val.refCount);
            }
        }
    }

    public static Music LoadMusic(string path)
    {
        if (string.IsNullOrEmpty(path))
            return default;
        string key = path.ToLowerInvariant();
        var (music, refCount) = _musics.AddOrUpdate(
            key,
            k =>
            {
                var m = Raylib.LoadMusicStream(path);
                Console.WriteLine($"ResourceCache: Loaded music {path}");
                return (m, 1);
            },
            (k, v) =>
            {
                v.refCount++;
                Console.WriteLine(
                    $"ResourceCache: Increment music refcount {path} => {v.refCount}"
                );
                return (v.music, v.refCount);
            }
        );
        return music;
    }

    public static void ReleaseMusic(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        string key = path.ToLowerInvariant();
        if (_musics.TryGetValue(key, out var val))
        {
            val.refCount--;
            Console.WriteLine($"ResourceCache: ReleaseMusic refcount {path} => {val.refCount}");
            if (val.refCount <= 0)
            {
                try
                {
                    // Stop the music if still playing then unload
                    try
                    {
                        Raylib.StopMusicStream(val.music);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"ResourceCache: StopMusicStream error: {e.Message}");
                    }
                    try
                    {
                        Raylib.UnloadMusicStream(val.music);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"ResourceCache: UnloadMusicStream error: {e.Message}");
                    }
                    Console.WriteLine($"ResourceCache: Unloaded music {path}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ResourceCache: Error unloading music {path}: {ex.Message}");
                }
                _musics.TryRemove(key, out _);
            }
            else
            {
                _musics[key] = (val.music, val.refCount);
            }
        }
    }

    public static void DumpState()
    {
        Console.WriteLine(
            $"ResourceCache: textures = {s_textures.Count}, musics = {_musics.Count}"
        );
        foreach (var kvp in s_textures)
        {
            Console.WriteLine(
                $"ResourceCache: texture {kvp.Key}, refcount = {kvp.Value.refCount}, id = {kvp.Value.texture.Id}"
            );
        }
        foreach (var kvp in _musics)
        {
            Console.WriteLine($"ResourceCache: music {kvp.Key}, refcount = {kvp.Value.refCount}");
        }
    }
}
