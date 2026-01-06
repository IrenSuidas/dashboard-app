using Raylib_cs;

namespace EndingApp.Scenes.ClipScene;

internal sealed partial class ClipScene
{
    private readonly HttpClient _httpClient = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _isFetching;
    private double _lastFetchTime;
    private readonly List<Clip> _clips = [];
    private readonly object _listLock = new();
    private string _statusMessage = "Ready";
    private FontLoader? _fontLoader;

    // Video Player State
    private VideoPlayer? _videoPlayer;
    private int _selectedIndex;
    private Clip? _currentClip;
    private bool _isVideoLoaded;

    // UI State
    private float _scrollOffset;

    // UI Colors (matching SongRequest scene)
    private readonly Color _colorPrimary = new(156, 211, 227, 255); // #9cd3e3
    private readonly Color _colorSecondary = new(249, 180, 192, 255); // #f9b4c0
    private readonly Color _colorAccent = new(216, 63, 81, 255); // #d83f51
    private readonly Color _colorBrand = new(38, 110, 255, 255); // #266eff
    private readonly Color _colorBackground = new(255, 247, 244, 255); // #fff7f4
    private readonly Color _colorTextMain = new(30, 30, 30, 255);
    private readonly Color _colorTextSub = new(100, 100, 100, 255);
    private readonly Color _colorHighlight = new(230, 240, 255, 255); // Light blue for highlighting

    public bool IsActive { get; private set; }
}
