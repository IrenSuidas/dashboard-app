using Raylib_cs;

namespace EndingApp;

internal sealed partial class EndingScene
{
    // Credits state
    private List<CreditEntry> _credits = [];
    private float _creditsScrollY;
    private float _creditsScrollSpeed; // dynamically calculated
    private float _songDuration;
    private float _musicPlayElapsed; // Manual timer for music playback
    private float _creditsHeight;

    // Music volume control
    private float _musicVolume = 1.0f;
    private float _targetMusicVolume = 1.0f;
    private const float MusicFadeSpeed = 0.5f; // Volume units per second

    // State for intro sequence
    private float _elapsedTime;
    private bool _musicStarted;
    private bool _musicStopped;
    private bool _creditsStarted;
    private bool _showStartText;
    private Fader _startTextFader;
    private bool _startTextPlayed;
    private bool _startTextHasFadedOut;

    // End text state
    private bool _endTextStarted;
    private bool _showEndText;
    private Fader _endTextFader;

    // flag to prevent re-triggering end text fade
    private bool _endTextHasFadedOut;
    private float _endTextShowElapsed;
    private bool _endBackgroundActive; // When true, draw plain background color instead of image
    private Fader _endBackgroundFader;
    private bool _endBackgroundPlayed;

    // Copyright text state
    private bool _copyrightStarted;
    private bool _copyrightFadingIn;
    private float _copyrightFadeElapsed;
    private float _copyrightAlpha;

    // Carousel state
    private List<CarouselItem> _carouselItems = [];
    private int _carouselIndex = -1;
    private CarouselState _carouselState = CarouselState.Hidden;
    private Fader _carouselFader;
    private float _carouselTimer;
    private VideoPlayer? _carouselVideoPlayer;
    private Texture2D _carouselImageTexture;
    private bool _carouselImageLoaded;
    private CarouselItemType _carouselCurrentItemType;
    private string _carouselCurrentFileName = "";

    private sealed class CarouselItem
    {
        public string Path { get; set; } = "";
        public CarouselItemType Type { get; set; }
        public TimeSpan? Duration { get; set; }
    }

    private enum CarouselState
    {
        Hidden,
        Loading,
        FadingIn,
        Playing,
        FadingOut,
        Finished,
    }

    private enum CarouselItemType
    {
        Image,
        Video,
    }
}
