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
    Buffering,
    Loading,
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
    private int _frameDataSize; // Size of one frame in bytes
    private byte[]? _textureBuffer; // Used only if stride mismatch
    private float[]? _audioPushBuffer; // Reused buffer for pushing audio
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
    private const int RingBufferSize = 4096 * 16; // Increased to ~1.4s
    private const int AudioChunkSize = 4096; // Size to push to Raylib
    private readonly object _audioLock = new();

    // Video Frame Buffer
    private const int VideoBufferSize = 3; // Reduced to 3 for memory optimization
    private readonly Queue<byte[]> _videoFrameQueue = new();
    private readonly Stack<byte[]> _videoFramePool = new();

    private bool _justLoaded;
    private static bool s_ffmpegInitialized;
    private Task? _bufferingTask;
    private CancellationTokenSource? _bufferingCts;
    private Task? _decodingTask;
    private CancellationTokenSource? _decodingCts;
    private volatile bool _isVideoFinished;

    private Task<LoadResult?>? _loadingTask;

    public static void InitializeFFmpeg()
    {
        if (!s_ffmpegInitialized)
        {
            FFmpegLoader.FFmpegPath = GetFFmpegPath();
            s_ffmpegInitialized = true;
        }
    }

    private sealed class LoadResult
    {
        public MediaFile VideoFile = null!;
        public MediaFile? AudioFile;
        public int Width;
        public int Height;
        public double FrameRate;
        public TimeSpan Duration;
        public int Stride;
        public byte[] InitialFrame = null!;
        public bool HasAudio;
        public int AudioChannels;
        public int SampleRate;
    }

    public void Load(string filePath)
    {
        LoadAsync(filePath);
        if (_loadingTask != null)
        {
            _loadingTask.Wait();
            if (_loadingTask.Status == TaskStatus.RanToCompletion && _loadingTask.Result != null)
            {
                FinishLoad(_loadingTask.Result);
            }
            else
            {
                // Handle error or cancellation
                State = VideoPlayerState.Stopped;
            }
            _loadingTask = null;
        }
    }

    public void LoadAsync(string filePath)
    {
        Dispose();
        _filePath = filePath;
        State = VideoPlayerState.Loading;

        _loadingTask = Task.Run(() => PerformBackgroundLoad(filePath));
    }

    private static LoadResult? PerformBackgroundLoad(string filePath)
    {
        try
        {
            InitializeFFmpeg();

            var result = new LoadResult();

            // 1. Open Video
            var videoOptions = new MediaOptions
            {
                StreamsToLoad = MediaMode.Video,
                VideoPixelFormat = ImagePixelFormat.Rgb24,
            };
            result.VideoFile = MediaFile.Open(filePath, videoOptions);

            var info = result.VideoFile.Video.Info;
            result.Width = info.FrameSize.Width;
            result.Height = info.FrameSize.Height;
            result.FrameRate = info.AvgFrameRate;
            result.Duration = info.Duration;

            // 2. Setup Buffers
            // Read first frame to get stride and size
            using var testFrame = result.VideoFile.Video.GetFrame(TimeSpan.Zero);
            result.Stride = testFrame.Stride;
            result.InitialFrame = new byte[testFrame.Data.Length];

            // Load initial frame content
            testFrame.Data.CopyTo(result.InitialFrame);

            // 3. Setup Audio (Background part)
            try
            {
                var audioOptions = new MediaOptions { StreamsToLoad = MediaMode.Audio };
                result.AudioFile = MediaFile.Open(filePath, audioOptions);

                if (result.AudioFile.HasAudio)
                {
                    var audioInfo = result.AudioFile.Audio.Info;
                    result.HasAudio = true;
                    result.AudioChannels = audioInfo.NumChannels;
                    result.SampleRate = audioInfo.SampleRate;
                }
                else
                {
                    result.AudioFile.Dispose();
                    result.AudioFile = null;
                    result.HasAudio = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"VideoPlayer: Audio setup failed: {ex.Message}");
                result.HasAudio = false;
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.Error($"VideoPlayer: Load failed: {ex.Message}");
            return null;
        }
    }

    private void FinishLoad(LoadResult result)
    {
        _videoFile = result.VideoFile;
        _audioFile = result.AudioFile;
        Width = result.Width;
        Height = result.Height;
        FrameRate = result.FrameRate;
        Duration = result.Duration;
        _frameInterval = 1.0 / FrameRate;
        _stride = result.Stride;
        _frameDataSize = result.InitialFrame.Length;

        // Only allocate texture buffer if stride mismatch requires repacking
        if (_stride != Width * 3)
        {
            _textureBuffer = new byte[Width * Height * 3];
        }

        _hasAudio = result.HasAudio;
        _audioChannels = result.AudioChannels;

        // Clear pools
        _videoFrameQueue.Clear();
        _videoFramePool.Clear();

        // Setup Texture (Main Thread)
        var image = Raylib.GenImageColor(Width, Height, Color.Black);
        Raylib.ImageFormat(ref image, PixelFormat.UncompressedR8G8B8);
        Texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        Raylib.SetTextureFilter(Texture, TextureFilter.Bilinear);

        UpdateTexture(result.InitialFrame);

        // Setup Audio Stream (Main Thread)
        if (_hasAudio)
        {
            if (!_audioInitialized)
            {
                if (AudioService.Register())
                {
                    Raylib.SetAudioStreamBufferSizeDefault(4096);
                    _audioStream = Raylib.LoadAudioStream(
                        (uint)result.SampleRate,
                        32, // 32-bit float
                        (uint)result.AudioChannels
                    );
                    _audioInitialized = true;
                    _audioRingBuffer = new float[RingBufferSize * _audioChannels];
                    _audioPushBuffer = new float[AudioChunkSize * _audioChannels]; // Pre-allocate push buffer
                }
            }
        }

        State = VideoPlayerState.Stopped;
        _justLoaded = true;
    }

    public void SetVolume(float volume)
    {
        if (_hasAudio && _audioInitialized)
        {
            Raylib.SetAudioStreamVolume(_audioStream, volume);
        }
    }

    public void Seek(TimeSpan time)
    {
        if (State == VideoPlayerState.Loading)
            return;

        bool wasPlaying = State == VideoPlayerState.Playing;

        // Stop decoding
        _decodingCts?.Cancel();
        if (_decodingTask != null)
        {
            try
            {
                _decodingTask.Wait();
            }
            catch (AggregateException) { }
            _decodingTask = null;
        }

        // Clamp time
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;
        if (time > Duration)
            time = Duration;

        // Reset clocks
        _masterClock = time.TotalSeconds;
        _startTime = Raylib.GetTime() - _masterClock;
        _nextFrameTime = _masterClock;
        _isVideoFinished = false;

        // Clear Buffers
        lock (_videoFrameQueue)
        {
            _videoFrameQueue.Clear();
            _videoFramePool.Clear();
        }

        lock (_audioLock)
        {
            _ringBufferReadPos = 0;
            _ringBufferWritePos = 0;
            _ringBufferCount = 0;
        }

        // Perform Seek
        try
        {
            if (_videoFile != null)
            {
                using var frame = _videoFile.Video.GetFrame(time);
                byte[] data = new byte[frame.Data.Length];
                frame.Data.CopyTo(data);
                UpdateTexture(data);
            }

            if (_hasAudio && _audioFile != null)
            {
                // Try to seek audio
                try
                {
                    using var audioFrame = _audioFile.Audio.GetFrame(time);
                }
                catch (Exception ex)
                {
                    Logger.Warn(
                        $"VideoPlayer: Audio seek failed (might not be supported): {ex.Message}"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"VideoPlayer: Seek failed: {ex.Message}");
        }

        if (wasPlaying)
        {
            StartDecodingWorker();
        }
    }

    public void Play()
    {
        if (State == VideoPlayerState.Playing || State == VideoPlayerState.Buffering)
            return;

        if (State == VideoPlayerState.Stopped || State == VideoPlayerState.Ended)
        {
            _masterClock = 0;
            _nextFrameTime = 0;
            State = VideoPlayerState.Buffering;

            _bufferingCts?.Cancel();
            _bufferingCts = new CancellationTokenSource();
            var token = _bufferingCts.Token;

            _bufferingTask = Task.Run(() => BufferInitialData(token), token);
        }
        else if (State == VideoPlayerState.Paused)
        {
            _startTime = Raylib.GetTime() - _masterClock;
            if (_hasAudio)
                Raylib.ResumeAudioStream(_audioStream);
            State = VideoPlayerState.Playing;
            StartDecodingWorker();
        }
    }

    private void StartDecodingWorker()
    {
        _decodingCts?.Cancel();
        _decodingCts = new CancellationTokenSource();
        var token = _decodingCts.Token;
        _decodingTask = Task.Run(() => DecodingLoop(token), token);
    }

    private void DecodingLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                bool didWork = false;

                // 1. Decode Video
                if (_videoFile != null && !_isVideoFinished)
                {
                    int queueCount;
                    lock (_videoFrameQueue)
                    {
                        queueCount = _videoFrameQueue.Count;
                    }

                    if (queueCount < VideoBufferSize)
                    {
                        // Get buffer from pool or create new
                        byte[] buffer;
                        lock (_videoFramePool)
                        {
                            if (_videoFramePool.Count > 0)
                                buffer = _videoFramePool.Pop();
                            else
                                buffer = new byte[_frameDataSize];
                        }

                        if (_videoFile.Video.TryGetNextFrame(buffer.AsSpan()))
                        {
                            lock (_videoFrameQueue)
                            {
                                _videoFrameQueue.Enqueue(buffer);
                            }
                            didWork = true;
                        }
                        else
                        {
                            // End of stream, push back unused buffer
                            lock (_videoFramePool)
                            {
                                _videoFramePool.Push(buffer);
                            }

                            _isVideoFinished = true;
                        }
                    }
                }

                // 2. Decode Audio
                if (_hasAudio && _audioFile != null)
                {
                    // Fill Ring Buffer
                    bool shouldRead = false;
                    lock (_audioLock)
                    {
                        if (_ringBufferCount < _audioRingBuffer.Length - 8192)
                        {
                            shouldRead = true;
                        }
                    }

                    if (shouldRead)
                    {
                        if (_audioFile.Audio.TryGetNextFrame(out var audioData))
                        {
                            using (audioData)
                            {
                                WriteToRingBuffer(audioData);
                            }
                            didWork = true;
                        }
                    }
                }

                if (!didWork)
                {
                    Thread.Sleep(1);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"VideoPlayer: Decoding loop failed: {ex.Message}");
        }
    }

    private void BufferInitialData(CancellationToken token)
    {
        try
        {
            _isVideoFinished = false;

            if (!_justLoaded)
            {
                // Reset Video
                if (_videoFile != null)
                {
                    // Clear queue and pool
                    lock (_videoFrameQueue)
                    {
                        _videoFrameQueue.Clear();
                        _videoFramePool.Clear();
                    }

                    // Pre-decode frames
                    // First frame immediately
                    if (token.IsCancellationRequested)
                        return;

                    // If we just loaded (and haven't played yet), the first frame is already loaded into the texture.
                    // But if we stopped and are restarting, or looping, we need to seek.

                    // Fallback to GetFrame(0) to reset position
                    using (var frame = _videoFile.Video.GetFrame(TimeSpan.Zero)) { }

                    // Decode subsequent frames into buffer
                    for (int i = 0; i < VideoBufferSize; i++)
                    {
                        if (token.IsCancellationRequested)
                            return;
                        if (_videoFile.Video.TryGetNextFrame(out var nextFrame))
                        {
                            using (nextFrame)
                            {
                                byte[] buffer = new byte[_frameDataSize];
                                nextFrame.Data.CopyTo(buffer);
                                lock (_videoFrameQueue)
                                {
                                    _videoFrameQueue.Enqueue(buffer);
                                }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                // Reset Audio
                if (_hasAudio && _audioFile != null)
                {
                    // Reset audio file position to the beginning
                    try
                    {
                        using var audioFrame = _audioFile.Audio.GetFrame(TimeSpan.Zero);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"VideoPlayer: Audio reset failed: {ex.Message}");
                    }

                    // Clear the ring buffer
                    lock (_audioLock)
                    {
                        _ringBufferReadPos = 0;
                        _ringBufferWritePos = 0;
                        _ringBufferCount = 0;
                    }
                }
            }
            else
            {
                // Just loaded, ensure ring buffer is clear
                lock (_audioLock)
                {
                    _ringBufferReadPos = 0;
                    _ringBufferWritePos = 0;
                    _ringBufferCount = 0;
                }

                // Also ensure video buffer is filled if we just loaded
                if (_videoFile != null)
                {
                    lock (_videoFrameQueue)
                    {
                        if (_videoFrameQueue.Count == 0)
                        {
                            // Pre-decode frames
                            // First frame is already on texture from Load(), so start from next?
                            // Actually Load() reads frame at Zero.
                            // So we should read subsequent frames.
                            // But we need to be careful about file position.
                            // Let's just fill the buffer.

                            // Note: Load() calls GetFrame(Zero). The file position is now after that frame?
                            // FFMediaToolkit GetFrame(TimeSpan) seeks. TryGetNextFrame continues.
                            // So if Load used GetFrame(Zero), we are at the start.
                            // We should probably seek to start to be safe or just continue if we know where we are.
                            // Let's assume we need to fill from start (skipping the one already loaded if possible, or just overwriting).
                            // Actually, if we just loaded, the texture shows frame 0.
                            // We want the queue to have frame 1, 2, 3...
                            // But GetFrame(Zero) might not advance the internal "next frame" pointer in the way TryGetNextFrame expects if they are mixed.
                            // Let's just fill the buffer normally.

                            // Decode frames into buffer
                            for (int i = 0; i < VideoBufferSize; i++)
                            {
                                if (token.IsCancellationRequested)
                                    return;
                                if (_videoFile.Video.TryGetNextFrame(out var nextFrame))
                                {
                                    using (nextFrame)
                                    {
                                        byte[] buffer = new byte[_frameDataSize];
                                        nextFrame.Data.CopyTo(buffer);
                                        _videoFrameQueue.Enqueue(buffer);
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // Pre-buffer audio to avoid initial stutter
            if (_hasAudio && _audioInitialized && _audioFile != null)
            {
                // Fill buffer up to 50% before starting
                while (true)
                {
                    if (token.IsCancellationRequested)
                        return;

                    bool isFull;
                    lock (_audioLock)
                    {
                        isFull = _ringBufferCount >= _audioRingBuffer.Length * 1 / 2;
                    }

                    if (isFull)
                        break;

                    if (_audioFile.Audio.TryGetNextFrame(out var audioData))
                    {
                        using (audioData)
                        {
                            WriteToRingBuffer(audioData);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"VideoPlayer: Buffering failed: {ex.Message}");
        }
    }

    public void Pause()
    {
        if (State != VideoPlayerState.Playing)
            return;

        _decodingCts?.Cancel();
        if (_decodingTask != null)
        {
            try
            {
                _decodingTask.Wait();
            }
            catch (AggregateException) { }
            _decodingTask = null;
        }

        if (_hasAudio)
            Raylib.PauseAudioStream(_audioStream);
        State = VideoPlayerState.Paused;
    }

    public void Stop()
    {
        _decodingCts?.Cancel();
        if (_decodingTask != null)
        {
            try
            {
                _decodingTask.Wait();
            }
            catch (AggregateException) { }
            _decodingTask = null;
        }

        if (_hasAudio)
            Raylib.StopAudioStream(_audioStream);
        State = VideoPlayerState.Stopped;
        _masterClock = 0;
    }

    public void Update()
    {
        if (State == VideoPlayerState.Loading)
        {
            if (_loadingTask != null && _loadingTask.IsCompleted)
            {
                if (
                    _loadingTask.Status == TaskStatus.RanToCompletion
                    && _loadingTask.Result != null
                )
                {
                    FinishLoad(_loadingTask.Result);
                }
                else
                {
                    Logger.Error("VideoPlayer: Async load failed or cancelled.");
                    State = VideoPlayerState.Stopped;
                }
                _loadingTask = null;
            }
            return;
        }

        if (State == VideoPlayerState.Buffering)
        {
            if (_bufferingTask != null && _bufferingTask.IsCompleted)
            {
                // Finalize startup on main thread
                _bufferingTask = null;
                _bufferingCts?.Dispose();
                _bufferingCts = null;

                // Push initial audio to Raylib immediately to prevent startup gap
                if (_hasAudio && _audioInitialized)
                {
                    int framesToPush = AudioChunkSize;
                    int samplesToPush = framesToPush * _audioChannels;

                    // Push first chunk
                    bool hasEnough;
                    lock (_audioLock)
                    {
                        hasEnough = _ringBufferCount >= samplesToPush;
                    }
                    if (hasEnough)
                    {
                        ReadFromRingBufferAndPush(framesToPush);
                    }

                    Raylib.PlayAudioStream(_audioStream);
                }

                _justLoaded = false;
                _startTime = Raylib.GetTime();
                State = VideoPlayerState.Playing;
                StartDecodingWorker();
            }
            return;
        }

        if (State != VideoPlayerState.Playing)
            return;

        // Update clock
        _masterClock = Raylib.GetTime() - _startTime;

        // 1. Handle Audio
        if (_hasAudio && _audioInitialized)
        {
            // Feed Raylib from Ring Buffer
            if (Raylib.IsAudioStreamProcessed(_audioStream))
            {
                // Push a chunk if we have enough data
                // Raylib buffer is 4096 frames. We push 4096 frames (samples * channels)
                int framesToPush = AudioChunkSize;
                int samplesToPush = framesToPush * _audioChannels;

                bool hasEnough;
                lock (_audioLock)
                {
                    hasEnough = _ringBufferCount >= samplesToPush;
                }

                if (hasEnough)
                {
                    ReadFromRingBufferAndPush(framesToPush);
                }
            }
        }

        // 2. Handle Video
        if (_masterClock >= _nextFrameTime)
        {
            if (_videoFile != null)
            {
                // Try to get from queue first
                byte[]? buffer = null;
                lock (_videoFrameQueue)
                {
                    if (_videoFrameQueue.Count > 0)
                    {
                        buffer = _videoFrameQueue.Dequeue();
                    }
                }

                if (buffer != null)
                {
                    UpdateTexture(buffer);
                    _nextFrameTime += _frameInterval;

                    // Return buffer to pool
                    lock (_videoFramePool)
                    {
                        _videoFramePool.Push(buffer);
                    }
                }
                else
                {
                    // Queue empty
                    if (_isVideoFinished)
                    {
                        HandlePlaybackComplete();
                    }
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

        lock (_audioLock)
        {
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
    }

    private unsafe void ReadFromRingBufferAndPush(int frames)
    {
        int samples = frames * _audioChannels;

        // Use pre-allocated buffer if available and large enough
        if (_audioPushBuffer == null || _audioPushBuffer.Length < samples)
        {
            _audioPushBuffer = new float[samples];
        }

        lock (_audioLock)
        {
            for (int i = 0; i < samples; i++)
            {
                _audioPushBuffer[i] = _audioRingBuffer[_ringBufferReadPos];
                _ringBufferReadPos = (_ringBufferReadPos + 1) % _audioRingBuffer.Length;
            }
            _ringBufferCount -= samples;
        }

        fixed (float* ptr = _audioPushBuffer)
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
            Stop();
            State = VideoPlayerState.Ended;
        }
    }

    private unsafe void UpdateTexture(byte[] data)
    {
        int rowBytes = Width * 3;

        if (_stride == rowBytes)
        {
            // Fast path: direct copy
            fixed (byte* ptr = data)
            {
                Raylib.UpdateTexture(Texture, ptr);
            }
        }
        else
        {
            // Slow path: repack
            if (_textureBuffer == null)
                return;

            for (int y = 0; y < Height; y++)
            {
                int srcOffset = y * _stride;
                int dstOffset = y * rowBytes;
                Buffer.BlockCopy(data, srcOffset, _textureBuffer, dstOffset, rowBytes);
            }

            fixed (byte* ptr = _textureBuffer)
            {
                Raylib.UpdateTexture(Texture, ptr);
            }
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

        // Handle pending load task
        if (_loadingTask != null)
        {
            if (_loadingTask.IsCompleted)
            {
                if (
                    _loadingTask.Status == TaskStatus.RanToCompletion
                    && _loadingTask.Result != null
                )
                {
                    _loadingTask.Result.VideoFile?.Dispose();
                    _loadingTask.Result.AudioFile?.Dispose();
                }
            }
            else
            {
                // Task is still running. Attach continuation to dispose result when done.
                _loadingTask.ContinueWith(t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion && t.Result != null)
                    {
                        t.Result.VideoFile?.Dispose();
                        t.Result.AudioFile?.Dispose();
                    }
                });
            }
            _loadingTask = null;
        }

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

        // Do not unload default texture (ID 1)
        if (Texture.Id != 1 && Raylib.IsTextureValid(Texture))
            Raylib.UnloadTexture(Texture);

        // Clear buffers to help GC
        _textureBuffer = null;
        _audioRingBuffer = [];
        _audioPushBuffer = null;
        _videoFrameQueue.Clear();
        _videoFramePool.Clear();

        // Force GC collection to reclaim large buffers immediately
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
