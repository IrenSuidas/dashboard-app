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

    public bool IsActive { get; private set; }
}
