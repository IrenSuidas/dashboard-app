using System.Numerics;
using EndingApp.Services;
using FFMediaToolkit;
using FFMediaToolkit.Audio;
using FFMediaToolkit.Decoding;
using FFMediaToolkit.Graphics;
using Raylib_cs;

namespace EndingApp.Utils;

public enum VideoPlayerState
{
    Stopped,
    Playing,
    Paused,
    Ended,
}

public sealed class VideoPlayer : IDisposable
{
    // State
    public VideoPlayerState State { get; private set; } = VideoPlayerState.Stopped;

    // Properties
    public int Width { get; private set; }
    public int Height { get; private set; }
    public double FrameRate { get; private set; }
    public TimeSpan Duration { get; private set; }
    public TimeSpan CurrentTime => TimeSpan.FromSeconds(_masterClock);
    public Texture2D Texture { get; private set; }
    public bool IsLooping { get; set; }

    // Internal
    private string _filePath = "";
    private MediaFile? _videoFile;
    private MediaFile? _audioFile;
    private byte[]? _frameBuffer;
    private byte[]? _textureBuffer;
    private int _stride;

    private double _masterClock;
    private double _frameInterval;
    private double _nextFrameTime;
    private double _startTime;

    // Audio
    private Raylib_cs.AudioStream _audioStream;
    private bool _hasAudio;
    private bool _audioInitialized;
    private int _audioChannels;

    // Audio Ring Buffer
    private float[] _audioRingBuffer = [];
    private int _ringBufferWritePos;
    private int _ringBufferReadPos;
    private int _ringBufferCount;
    private const int RingBufferSize = 4096 * 16; // Enough for ~1.5s of audio
    private const int AudioChunkSize = 4096; // Size to push to Raylib

    public void Load(string filePath)
    {
        Dispose();
        _filePath = filePath;

        try
        {
            FFmpegLoader.FFmpegPath = GetFFmpegPath();

            // 1. Open Video
            var videoOptions = new MediaOptions
            {
                StreamsToLoad = MediaMode.Video,
                VideoPixelFormat = ImagePixelFormat.Rgba32,
            };
            _videoFile = MediaFile.Open(filePath, videoOptions);

            var info = _videoFile.Video.Info;
            Width = info.FrameSize.Width;
            Height = info.FrameSize.Height;
            FrameRate = info.AvgFrameRate;
            Duration = info.Duration;
            _frameInterval = 1.0 / FrameRate;

            // 2. Setup Buffers
            // Read first frame to get stride and size
            using var testFrame = _videoFile.Video.GetFrame(TimeSpan.Zero);
            _stride = testFrame.Stride;
            _frameBuffer = new byte[testFrame.Data.Length];
            _textureBuffer = new byte[Width * Height * 4];

            // 3. Setup Texture
            var image = Raylib.GenImageColor(Width, Height, Color.Black);
            Raylib.ImageFormat(ref image, PixelFormat.UncompressedR8G8B8A8);
            Texture = Raylib.LoadTextureFromImage(image);
            Raylib.UnloadImage(image);
            Raylib.SetTextureFilter(Texture, TextureFilter.Bilinear);

            // Load initial frame content
            testFrame.Data.CopyTo(_frameBuffer);
            UpdateTexture();

            // 4. Setup Audio
            SetupAudio();

            State = VideoPlayerState.Stopped;
        }
        catch (Exception ex)
        {
            Logger.Error($"VideoPlayer: Load failed: {ex.Message}");
            Dispose();
        }
    }

    private void SetupAudio()
    {
        try
        {
            // Close existing if any
            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }

            var audioOptions = new MediaOptions { StreamsToLoad = MediaMode.Audio };
            _audioFile = MediaFile.Open(_filePath, audioOptions);

            if (_audioFile.HasAudio)
            {
                var audioInfo = _audioFile.Audio.Info;

                // Only initialize Raylib audio stream once
                if (!_audioInitialized)
                {
                    if (AudioService.Register())
                    {
                        Raylib.SetAudioStreamBufferSizeDefault(4096);
                        _audioStream = Raylib.LoadAudioStream(
                            (uint)audioInfo.SampleRate,
                            32, // 32-bit float
                            (uint)audioInfo.NumChannels
                        );
                        _audioChannels = audioInfo.NumChannels;
                        _hasAudio = true;
                        _audioInitialized = true;

                        // Initialize ring buffer
                        _audioRingBuffer = new float[RingBufferSize * _audioChannels];
                    }
                }
            }
            else
            {
                _audioFile.Dispose();
                _audioFile = null;
                _hasAudio = false;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"VideoPlayer: Audio setup failed: {ex.Message}");
            _hasAudio = false;
        }
    }

    public void Play()
    {
        if (State == VideoPlayerState.Playing)
            return;

        if (State == VideoPlayerState.Stopped || State == VideoPlayerState.Ended)
        {
            _masterClock = 0;
            _nextFrameTime = 0;

            // Reset Video
            if (_videoFile != null && _frameBuffer != null)
            {
                using var frame = _videoFile.Video.GetFrame(TimeSpan.Zero);
                frame.Data.CopyTo(_frameBuffer);
                UpdateTexture();
            }

            // Reset Audio
            if (_hasAudio)
            {
                SetupAudio();
                // Clear ring buffer
                _ringBufferReadPos = 0;
                _ringBufferWritePos = 0;
                _ringBufferCount = 0;
            }

            _startTime = Raylib.GetTime();
        }
        else if (State == VideoPlayerState.Paused)
        {
            _startTime = Raylib.GetTime() - _masterClock;
        }

        if (_hasAudio)
            Raylib.PlayAudioStream(_audioStream);
        State = VideoPlayerState.Playing;
    }

    public void Pause()
    {
        if (State != VideoPlayerState.Playing)
            return;

        if (_hasAudio)
            Raylib.PauseAudioStream(_audioStream);
        State = VideoPlayerState.Paused;
    }

    public void Stop()
    {
        if (_hasAudio)
            Raylib.StopAudioStream(_audioStream);
        State = VideoPlayerState.Stopped;
        _masterClock = 0;
    }

    public void Update()
    {
        if (State != VideoPlayerState.Playing)
            return;

        // Update clock
        _masterClock = Raylib.GetTime() - _startTime;

        // 1. Handle Audio
        if (_hasAudio && _audioInitialized && _audioFile != null)
        {
            // Fill Ring Buffer from File
            // We want to keep the ring buffer reasonably full, but not overflow
            while (_ringBufferCount < _audioRingBuffer.Length - 8192) // Leave some space
            {
                if (_audioFile.Audio.TryGetNextFrame(out var audioData))
                {
                    using (audioData)
                    {
                        WriteToRingBuffer(audioData);
                    }
                }
                else
                {
                    break; // End of audio stream
                }
            }

            // Feed Raylib from Ring Buffer
            if (Raylib.IsAudioStreamProcessed(_audioStream))
            {
                // Push a chunk if we have enough data
                // Raylib buffer is 4096 frames. We push 4096 frames (samples * channels)
                int framesToPush = 4096;
                int samplesToPush = framesToPush * _audioChannels;

                if (_ringBufferCount >= samplesToPush)
                {
                    ReadFromRingBufferAndPush(framesToPush);
                }
            }
        }

        // 2. Handle Video
        if (_masterClock >= _nextFrameTime)
        {
            if (_videoFile != null && _frameBuffer != null)
            {
                if (_videoFile.Video.TryGetNextFrame(_frameBuffer.AsSpan()))
                {
                    UpdateTexture();
                    _nextFrameTime += _frameInterval;
                }
                else
                {
                    HandlePlaybackComplete();
                }
            }
        }
    }

    private void WriteToRingBuffer(AudioData audioData)
    {
        float[][] sampleData = audioData.GetSampleData();
        int channels = sampleData.Length;
        if (channels == 0)
            return;
        int samplesPerChannel = sampleData[0].Length;

        // Interleave and write
        for (int i = 0; i < samplesPerChannel; i++)
        {
            for (int c = 0; c < channels; c++)
            {
                _audioRingBuffer[_ringBufferWritePos] = sampleData[c][i];
                _ringBufferWritePos = (_ringBufferWritePos + 1) % _audioRingBuffer.Length;
                _ringBufferCount++;
            }
        }
    }

    private unsafe void ReadFromRingBufferAndPush(int frames)
    {
        int samples = frames * _audioChannels;
        // We need a contiguous buffer for Raylib.UpdateAudioStream
        // Since ring buffer might wrap, we copy to a temp buffer.
        // Allocating every frame is bad, but let's optimize later if needed.
        // Or use a pre-allocated buffer.

        // Use stackalloc for small buffers? 4096 * 2 * 4 bytes = 32KB. A bit large for stackalloc maybe?
        // Let's use a pooled array or just new for now (GC will handle it, it's gen0).
        float[] tempBuffer = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            tempBuffer[i] = _audioRingBuffer[_ringBufferReadPos];
            _ringBufferReadPos = (_ringBufferReadPos + 1) % _audioRingBuffer.Length;
        }
        _ringBufferCount -= samples;

        fixed (float* ptr = tempBuffer)
        {
            Raylib.UpdateAudioStream(_audioStream, ptr, frames);
        }
    }

    private void HandlePlaybackComplete()
    {
        if (IsLooping)
        {
            Stop();
            Play();
        }
        else
        {
            State = VideoPlayerState.Ended;
            Stop();
        }
    }

    private unsafe void UpdateTexture()
    {
        if (_frameBuffer == null || _textureBuffer == null)
            return;

        int rowBytes = Width * 4;

        if (_stride == rowBytes)
        {
            Buffer.BlockCopy(_frameBuffer, 0, _textureBuffer, 0, _textureBuffer.Length);
        }
        else
        {
            for (int y = 0; y < Height; y++)
            {
                int srcOffset = y * _stride;
                int dstOffset = y * rowBytes;
                Buffer.BlockCopy(_frameBuffer, srcOffset, _textureBuffer, dstOffset, rowBytes);
            }
        }

        fixed (byte* ptr = _textureBuffer)
        {
            Raylib.UpdateTexture(Texture, ptr);
        }
    }

    public void Draw(Rectangle bounds, Color tint)
    {
        if (!Raylib.IsTextureValid(Texture))
            return;

        float videoAspect = (float)Width / Height;
        float boundsAspect = bounds.Width / bounds.Height;

        float destWidth,
            destHeight;
        if (videoAspect > boundsAspect)
        {
            destWidth = bounds.Width;
            destHeight = bounds.Width / videoAspect;
        }
        else
        {
            destHeight = bounds.Height;
            destWidth = bounds.Height * videoAspect;
        }

        float x = bounds.X + (bounds.Width - destWidth) / 2f;
        float y = bounds.Y + (bounds.Height - destHeight) / 2f;

        var source = new Rectangle(0, 0, Width, Height);
        var dest = new Rectangle(x, y, destWidth, destHeight);

        Raylib.DrawTexturePro(Texture, source, dest, Vector2.Zero, 0f, tint);
    }

    private static string GetFFmpegPath()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(AppContext.BaseDirectory, "ffmpeg");
        if (OperatingSystem.IsLinux())
            return "/usr/lib/x86_64-linux-gnu";
        if (OperatingSystem.IsMacOS())
            return "/opt/homebrew/lib";
        return "";
    }

    public void Dispose()
    {
        Stop();

        _videoFile?.Dispose();
        _videoFile = null;

        _audioFile?.Dispose();
        _audioFile = null;

        if (_audioInitialized)
        {
            Raylib.UnloadAudioStream(_audioStream);
            AudioService.Unregister();
            _audioInitialized = false;
        }

        if (Raylib.IsTextureValid(Texture))
            Raylib.UnloadTexture(Texture);
    }
}
