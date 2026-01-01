using System.Net.Http.Headers;
using System.Text.Json;

namespace EndingApp.Scenes.ClipScene;

internal sealed partial class ClipScene
{
    private async Task FetchAndDownloadClips()
    {
        if (_isFetching || string.IsNullOrEmpty(_config.Api.EndpointUrl))
            return;

        _isFetching = true;

        try
        {
            _statusMessage = "Fetching clips...";
            string url = $"{_config.Api.EndpointUrl}/clips";

            // Configure Authorization header if API key is present
            if (!string.IsNullOrEmpty(_config.Api.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    _config.Api.ApiKey
                );
            }

            // Use a short timeout for the API fetch
            using var fetchCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            fetchCts.CancelAfter(TimeSpan.FromSeconds(10));

            string json = await _httpClient.GetStringAsync(url, fetchCts.Token);

            var clips = JsonSerializer.Deserialize(json, ClipContext.Default.ListClip);
            if (clips == null)
            {
                _statusMessage = "Failed to parse clips.";
                return;
            }

            if (clips.Count == 0)
            {
                _statusMessage = "No clips found.";
                Logger.Info("ClipScene: No clips found on server.");
                return;
            }

            _statusMessage = $"Found {clips.Count} clips. Downloading...";

            string tempPath = Path.Combine(AppContext.BaseDirectory, "temp");
            string clipsPath = Path.Combine(tempPath, "clips");

            Directory.CreateDirectory(clipsPath);

            foreach (var clip in clips)
            {
                string downloadUrl = $"{_config.Api.EndpointUrl}{clip.Path}";
                string filePath = Path.Combine(clipsPath, clip.Filename);

                if (!File.Exists(filePath))
                {
                    _statusMessage = $"Downloading {clip.Title}...";
                    Logger.Info($"ClipScene: Start downloading {clip.Title} from {downloadUrl}");

                    try
                    {
                        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(
                            _cts.Token
                        );
                        connectCts.CancelAfter(TimeSpan.FromSeconds(10));

                        using var response = await _httpClient.GetAsync(
                            downloadUrl,
                            HttpCompletionOption.ResponseHeadersRead,
                            connectCts.Token
                        );
                        response.EnsureSuccessStatusCode();

                        using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
                        using var fileStream = new FileStream(
                            filePath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None
                        );
                        await stream.CopyToAsync(fileStream, _cts.Token);
                        Logger.Info($"ClipScene: Finished downloading {clip.Title}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"ClipScene: Failed to download {clip.Title} - {ex.Message}");
                        _statusMessage = $"Failed download: {clip.Title}";
                        continue;
                    }
                }
                else
                {
                    Logger.Info($"ClipScene: File exists for {clip.Title}, skipping download.");
                }

                lock (_listLock)
                {
                    if (!_clips.Any(c => c.Id == clip.Id))
                    {
                        _clips.Add(clip);
                    }
                }
            }
            _statusMessage = "Clips updated.";
        }
        catch (OperationCanceledException)
        {
            Logger.Info("ClipScene: Fetch cancelled.");
            _statusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            Logger.Error($"ClipScene: Error - {ex.Message}");
            _statusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            _isFetching = false;
        }
    }
}
