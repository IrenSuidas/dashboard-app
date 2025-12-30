using Raylib_cs;

namespace EndingApp.Scenes.SongRequest;

internal sealed partial class SongRequestScene
{
    private string _statusMessage = "Idle";
    private readonly HttpClient _httpClient = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly CancellationTokenSource _cts = new();

    // Audio State
    private Music _currentMusic;
    private Music _fadingMusic;
    private bool _isMusicLoaded;
    private bool _isFadingMusicLoaded;

    // Playback State
    private bool _isPlayingRequested; // True if currently playing a requested song
    private float _recurrentSeekPosition; // Saved position for recurrent song
    private int _currentRecurrentIndex; // Index in _recurrentSongs
    private int _currentRequestedIndex; // Index in _requestedSongs
    private float _savedCurrentPosition; // Saved position when leaving scene

    // Data
    private readonly List<PlaylistItem> _requestedSongs = [];
    private readonly List<PlaylistItem> _recurrentSongs = [];
    private readonly HashSet<string> _playedRequestedPaths = [];
    private readonly Stack<PlaylistItem> _history = new();
    private PlaylistItem? _currentSongItem;
    private readonly object _listLock = new();

    // Polling
    private double _lastFetchTime;
    private bool _isFetching;
    private const double FetchInterval = 30.0;

    // Probing
    private double _lastProbeTime;
    private const double ProbeInterval = 0.2; // Probe 5 songs per second maximum

    // Volume
    private float _volume = 0.5f;
    private bool _isPaused;

    // Crossfading
    private bool _isCrossfading;
    private float _crossfadeTimer;
    private const float CrossfadeDuration = 2.0f;

    // UI State
    private float _scrollRequested;
    private float _scrollRecurrent;

    // Font
    private FontLoader? _fontLoader;

    // UI Colors
    private readonly Color _colorPrimary = new(156, 211, 227, 255); // #9cd3e3
    private readonly Color _colorSecondary = new(249, 180, 192, 255); // #f9b4c0
    private readonly Color _colorAccent = new(216, 63, 81, 255); // #d83f51
    private readonly Color _colorBrand = new(38, 110, 255, 255); // #266eff
    private readonly Color _colorBackground = new(255, 247, 244, 255); // #fff7f4
    private readonly Color _colorTextMain = new(30, 30, 30, 255);
    private readonly Color _colorTextSub = new(100, 100, 100, 255);
    private readonly Color _colorHighlight = new(230, 240, 255, 255); // Light blue for highlighting

    public bool IsActive { get; private set; }

    private sealed record PlaylistItem(string Path, Song Data)
    {
        public string Title => Data.Title;
        public string Requester => Data.AddedBy;
        public string Type => Data.Type;
        public int? QueuePosition => Data.QueuePosition;
        public int? Duration => Data.Duration;
    }
}
