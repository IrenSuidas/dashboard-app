using FFMediaToolkit.Decoding;
using FFMediaToolkit.Graphics;
using Raylib_cs;

namespace EndingApp.Utils;

public unsafe class GifPlayer : IDisposable
{
    public Texture2D Texture { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool IsLoaded => _file != null;

    private MediaFile? _file;
    private double _frameDelay; // Seconds per frame
    private double _timer;
    private bool _isLooping;
    private byte[] _currentFrameData = [];

    // Buffer for texture update if needed

    public void Load(string filePath)
    {
        Dispose();

        if (!File.Exists(filePath))
        {
            Logger.Error($"GifPlayer: File not found: {filePath}");
            return;
        }

        try
        {
            // Ensure FFmpeg is initialized (using the logic from VideoPlayer)
            VideoPlayer.InitializeFFmpeg();

            var options = new MediaOptions
            {
                StreamsToLoad = MediaMode.Video,
                // Request RGBA for transparency support
                VideoPixelFormat = ImagePixelFormat.Rgba32,
            };

            _file = MediaFile.Open(filePath, options);

            var info = _file.Video.Info;
            Width = info.FrameSize.Width;
            Height = info.FrameSize.Height;

            // Calculate delay from FPS
            // info.AvgFrameRate might be 0 or variable for GIFs, but usually it works.
            double fps = info.AvgFrameRate;
            if (fps <= 0)
                fps = 10; // Default fallback
            _frameDelay = 1.0 / fps;

            _timer = 0;

            // Load first frame
            if (_file.Video.TryGetNextFrame(out var frame))
            {
                _currentFrameData = frame.Data.ToArray();

                fixed (byte* pData = _currentFrameData)
                {
                    var image = new Image
                    {
                        Data = pData,
                        Width = Width,
                        Height = Height,
                        Mipmaps = 1,
                        Format = PixelFormat.UncompressedR8G8B8A8,
                    };

                    Texture = Raylib.LoadTextureFromImage(image);
                }
            }
            else
            {
                Logger.Warn($"GifPlayer: No frames found in GIF: {filePath}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"GifPlayer: Failed to load GIF: {filePath}. Error: {ex.Message}");
            Dispose();
        }
    }

    public void Update()
    {
        if (_file == null)
            return;

        _timer += Raylib.GetFrameTime();
        if (_timer >= _frameDelay)
        {
            _timer -= _frameDelay; // Keep remainder for smooth timing

            // Try to get next frame
            if (_file.Video.TryGetNextFrame(out var frame))
            {
                // We reuse the buffer if possible or allocate new if size changed (unlikely for GIF)
                if (_currentFrameData.Length != frame.Data.Length)
                    _currentFrameData = new byte[frame.Data.Length];

                frame.Data.CopyTo(_currentFrameData);

                fixed (byte* pData = _currentFrameData)
                {
                    Raylib.UpdateTexture(Texture, pData);
                }
            }
            else
            {
                // End of stream
                if (_isLooping)
                {
                    // Seek to beginning
                    // Note: Seek might be slow depending on format, but for small GIF it should be ok-ish
                    // However, FFMediaToolkit's seek implementation for some formats might be tricky.
                    // For GIF, standard seek should work.
                    try
                    {
                        // GetFrame seeks to timestamp.
                        // But we want to reset the stream reader.
                        // FFMediaToolkit doesn't expose a raw "Seek to frame 0" easily on the stream reader without GetFrame.
                        // calling GetFrame(TimeSpan.Zero) will seek and return the frame.
                        using var firstFrame = _file.Video.GetFrame(TimeSpan.Zero);
                        if (_currentFrameData.Length != firstFrame.Data.Length)
                            _currentFrameData = new byte[firstFrame.Data.Length];

                        firstFrame.Data.CopyTo(_currentFrameData);

                        fixed (byte* pData = _currentFrameData)
                        {
                            Raylib.UpdateTexture(Texture, pData);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"GifPlayer: Loop seek failed: {ex.Message}");
                    }
                }
            }
        }
    }

    public void SetLooping(bool loop)
    {
        _isLooping = loop;
    }

    public void Dispose()
    {
        if (Texture.Id != 0)
        {
            Raylib.UnloadTexture(Texture);
            Texture = default;
        }

        _file?.Dispose();
        _file = null;
        _currentFrameData = [];

        GC.SuppressFinalize(this);
    }
}
