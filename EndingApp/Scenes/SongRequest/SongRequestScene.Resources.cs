using System.Text.Json;

namespace EndingApp.Scenes.SongRequest;

internal sealed partial class SongRequestScene
{
    private void LoadLocalSongs()
    {
        try
        {
            string tempPath = Path.Combine(AppContext.BaseDirectory, "temp");
            string manifestPath = Path.Combine(tempPath, "manifest.json");

            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    var manifest = JsonSerializer.Deserialize(
                        json,
                        SongContext.Default.SongManifest
                    );

                    if (manifest != null)
                    {
                        string requestedPath = Path.Combine(tempPath, "requested");
                        string recurrentPath = Path.Combine(tempPath, "recurrent");

                        lock (_listLock)
                        {
                            // Load Requested
                            foreach (var s in manifest.Requested)
                            {
                                string fullPath = Path.Combine(requestedPath, s.Filename);
                                if (
                                    File.Exists(fullPath)
                                    && !_requestedSongs.Any(x => x.Path == fullPath)
                                )
                                {
                                    _requestedSongs.Add(new PlaylistItem(fullPath, s));
                                }
                            }

                            // Load Recurrent
                            foreach (var s in manifest.Recurrent)
                            {
                                string fullPath = Path.Combine(recurrentPath, s.Filename);
                                if (
                                    File.Exists(fullPath)
                                    && !_recurrentSongs.Any(x => x.Path == fullPath)
                                )
                                {
                                    _recurrentSongs.Add(new PlaylistItem(fullPath, s));
                                }
                            }
                        }
                        Logger.Info(
                            $"SongRequestScene: Loaded from manifest - {_requestedSongs.Count} requested, {_recurrentSongs.Count} recurrent."
                        );
                        return; // Successfully loaded from manifest
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"SongRequestScene: Failed to read manifest - {ex.Message}");
                }
            }

            // Fallback to scanning files if manifest fails or doesn't exist
            Logger.Warn(
                "SongRequestScene: Manifest not found or invalid, falling back to file scan."
            );
            string reqDir = Path.Combine(tempPath, "requested");
            string recDir = Path.Combine(tempPath, "recurrent");

            if (Directory.Exists(reqDir))
            {
                var files = Directory.GetFiles(reqDir, "*.*");
                lock (_listLock)
                {
                    foreach (var file in files)
                    {
                        if (!_requestedSongs.Any(x => x.Path == file))
                        {
                            var s = new Song(
                                Path.GetFileName(file),
                                "",
                                "requested",
                                "",
                                "",
                                Path.GetFileNameWithoutExtension(file),
                                "",
                                "Unknown",
                                0,
                                0,
                                null
                            );
                            _requestedSongs.Add(new PlaylistItem(file, s));
                        }
                    }
                }
            }

            if (Directory.Exists(recDir))
            {
                var files = Directory.GetFiles(recDir, "*.*");
                lock (_listLock)
                {
                    foreach (var file in files)
                    {
                        if (!_recurrentSongs.Any(x => x.Path == file))
                        {
                            var s = new Song(
                                Path.GetFileName(file),
                                "",
                                "recurrent",
                                "",
                                "",
                                Path.GetFileNameWithoutExtension(file),
                                "",
                                "System",
                                0,
                                null,
                                null
                            );
                            _recurrentSongs.Add(new PlaylistItem(file, s));
                        }
                    }
                }
            }

            Logger.Info(
                $"SongRequestScene: Loaded {_requestedSongs.Count} requested and {_recurrentSongs.Count} recurrent songs from disk (fallback)."
            );
        }
        catch (Exception ex)
        {
            Logger.Error($"SongRequestScene: Failed to load local songs - {ex.Message}");
        }
    }

    private static void SaveManifest(List<Song> requested, List<Song> recurrent)
    {
        try
        {
            string tempPath = Path.Combine(AppContext.BaseDirectory, "temp");
            string manifestPath = Path.Combine(tempPath, "manifest.json");
            Directory.CreateDirectory(tempPath);

            var manifest = new SongManifest(requested, recurrent);
            string json = JsonSerializer.Serialize(manifest, SongContext.Default.SongManifest);
            File.WriteAllText(manifestPath, json);
        }
        catch (Exception ex)
        {
            Logger.Error($"SongRequestScene: Failed to save manifest - {ex.Message}");
        }
    }
}
