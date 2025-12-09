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
                Logger.Info("ResourceCache: Loaded texture {0} (id={1})", path, t.Id);
                return (t, 1);
            },
            (k, v) =>
            {
                v.refCount++;
                Logger.Debug(
                    "ResourceCache: Increment texture refcount {0} => {1}",
                    path,
                    v.refCount
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
            Logger.Debug("ResourceCache: ReleaseTexture refcount {0} => {1}", path, val.refCount);
            if (val.refCount <= 0)
            {
                try
                {
                    if (val.texture.Id != 0)
                        Raylib.UnloadTexture(val.texture);
                    Logger.Info(
                        "ResourceCache: Unloaded texture {0} (id={1})",
                        path,
                        val.texture.Id
                    );
                }
                catch (Exception ex)
                {
                    Logger.Warn(
                        "ResourceCache: Error unloading texture {0}: {1}",
                        path,
                        ex.Message
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
                Logger.Info("ResourceCache: Loaded music {0}", path);
                return (m, 1);
            },
            (k, v) =>
            {
                v.refCount++;
                Logger.Debug(
                    "ResourceCache: Increment music refcount {0} => {1}",
                    path,
                    v.refCount
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
            Logger.Debug("ResourceCache: ReleaseMusic refcount {0} => {1}", path, val.refCount);
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
                        Logger.Warn("ResourceCache: StopMusicStream error: {0}", e.Message);
                    }
                    try
                    {
                        Raylib.UnloadMusicStream(val.music);
                    }
                    catch (Exception e)
                    {
                        Logger.Warn("ResourceCache: UnloadMusicStream error: {0}", e.Message);
                    }
                    Logger.Info("ResourceCache: Unloaded music {0}", path);
                }
                catch (Exception ex)
                {
                    Logger.Warn("ResourceCache: Error unloading music {0}: {1}", path, ex.Message);
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
        Logger.Info("ResourceCache: textures = {0}, musics = {1}", s_textures.Count, _musics.Count);
        foreach (var kvp in s_textures)
        {
            Logger.Info(
                "ResourceCache: texture {0}, refcount = {1}, id = {2}",
                kvp.Key,
                kvp.Value.refCount,
                kvp.Value.texture.Id
            );
        }
        foreach (var kvp in _musics)
        {
            Logger.Info("ResourceCache: music {0}, refcount = {1}", kvp.Key, kvp.Value.refCount);
        }
    }
}
