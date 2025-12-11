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
    private Utils.Fader _startTextFader;
    private bool _startTextPlayed;
    private bool _startTextHasFadedOut;

    // End text state
    private bool _endTextStarted;
    private bool _showEndText;
    private Utils.Fader _endTextFader;

    // flag to prevent re-triggering end text fade
    private bool _endTextHasFadedOut;
    private float _endTextShowElapsed;
    private bool _endBackgroundActive; // When true, draw plain background color instead of image
    private Utils.Fader _endBackgroundFader;
    private bool _endBackgroundPlayed;

    // Copyright text state
    private bool _copyrightStarted;
    private bool _copyrightFadingIn;
    private float _copyrightFadeElapsed;
    private float _copyrightAlpha;

    // Carousel state
    private List<string> _carouselItems = [];
    private int _carouselIndex = -1;
    private CarouselState _carouselState = CarouselState.Hidden;
    private Utils.Fader _carouselFader;
    private float _carouselTimer;
    private Utils.VideoPlayer? _carouselVideoPlayer;
    private Texture2D _carouselImageTexture;
    private bool _carouselImageLoaded;
    private CarouselItemType _carouselCurrentItemType;
    private string _carouselCurrentFileName = "";

    private enum CarouselState
    {
        Hidden,
        FadingIn,
        Playing,
        FadingOut,
    }

    private enum CarouselItemType
    {
        Image,
        Video,
    }
}
