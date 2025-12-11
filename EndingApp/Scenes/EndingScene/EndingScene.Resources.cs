using EndingApp.Services;
using Raylib_cs;

namespace EndingApp;

internal sealed partial class EndingScene
{
    private Texture2D _backgroundTexture;
    private Music _music;
    private FontLoader? _fontLoader;
    private Texture2D _emoteTexture;
    private bool _emoteLoaded;

    private void LoadResources()
    {
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

        // Prepare music but do not play yet - music managed by caller
        _music = ResourceCache.LoadMusic(_config.Ending.Music);
        // Reset playback position so the music always starts from the beginning when the scene starts.
        try
        {
            if (AudioService.IsAudioDeviceReady)
            {
                AudioService.ResetPlayback(_music);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"MUSIC: failed to reset playback: {ex.Message}");
        }

        // Load emote if provided
        _emoteLoaded = false;
        if (
            !string.IsNullOrEmpty(_config.Ending.EmotePath) && File.Exists(_config.Ending.EmotePath)
        )
        {
            _emoteTexture = ResourceCache.LoadTexture(_config.Ending.EmotePath);
            _emoteLoaded = true;
        }
    }

    public void Cleanup()
    {
        if (_cleanedUp)
            return;

        // Log memory use to help track potential leaks
        long memBefore = Diagnostics.GetPrivateMemoryMB();
        Diagnostics.LogMemory("Cleanup: memory before cleanup", memBefore);

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
                Logger.Warn($"Cleanup: failed to unload background texture: {ex.Message}");
            }

            _fontLoader?.Dispose();
            _fontLoader = null;

            if (_emoteLoaded && _emoteTexture.Id != 0)
            {
                try
                {
                    ResourceCache.ReleaseTexture(_config.Ending.EmotePath);
                    _emoteTexture = default;
                    _emoteLoaded = false;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Cleanup: failed to unload emote texture: {ex.Message}");
                }
            }

            // Carousel cleanup
            try
            {
                _carouselVideoPlayer?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warn($"Cleanup: failed to dispose video player: {ex.Message}");
            }
            _carouselVideoPlayer = null;

            if (_carouselImageLoaded)
            {
                Raylib.UnloadTexture(_carouselImageTexture);
                _carouselImageLoaded = false;
            }
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
        long memAfter = Diagnostics.GetPrivateMemoryMB();
        Diagnostics.LogMemoryDelta("Cleanup: memory after cleanup and GC", memBefore, memAfter);
    }
}
