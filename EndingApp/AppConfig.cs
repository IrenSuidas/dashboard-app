using Raylib_cs;

namespace EndingApp;

internal sealed class AppConfig
{
    public EndingConfig Ending { get; set; } = new();

    public static AppConfig Load(string configPath = "config.yaml")
    {
        var config = new AppConfig();
        var data = SimpleYamlReader.Read(configPath);

        // Load ending configuration
        config.Ending.BackgroundImage = data.GetString("ending.background", "assets/images/bg.png");
        config.Ending.Music = data.GetString("ending.music", "assets/music/remix.mp3");
        config.Ending.Width = data.GetInt("ending.width", 1280);
        config.Ending.Height = data.GetInt("ending.height", 720);
        config.Ending.BlackBarHeight = data.GetInt("ending.blackBarHeight", 100);
        config.Ending.FontSize = data.GetInt("ending.fontSize", 32);
        config.Ending.SectionFontSize = data.GetInt("ending.sectionFontSize", 40);
        config.Ending.CreditsPositionPercentage = data.GetInt(
            "ending.creditsPositionPercentage",
            60
        );
        config.Ending.StartDelay = data.GetFloat("ending.endingStartDelay", 2.0f);
        config.Ending.StartText = data.GetString("ending.endingStartText", "Stream Ending");
        config.Ending.StartTextHideTime = data.GetFloat("ending.endingStartTextHideTime", 3.0f);

        // Load color configuration
        var (R, G, B, A) = data.GetColorHex("ending.sectionColorHex", (255, 220, 150, 255));
        config.Ending.SectionColor = new Color(R, G, B, A);

        var valuesColor = data.GetColorHex("ending.valuesColorHex", (255, 255, 255, 255));
        config.Ending.ValuesColor = new Color(
            valuesColor.R,
            valuesColor.G,
            valuesColor.B,
            valuesColor.A
        );

        var startTextColor = data.GetColorHex(
            "ending.endingStartTextColorHex",
            (255, 255, 255, 255)
        );
        config.Ending.StartTextColor = new Color(
            startTextColor.R,
            startTextColor.G,
            startTextColor.B,
            startTextColor.A
        );

        return config;
    }
}

internal sealed class EndingConfig
{
    public string BackgroundImage { get; set; } = "assets/images/bg.png";
    public string Music { get; set; } = "assets/music/remix.mp3";
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public int BlackBarHeight { get; set; } = 100;
    public int FontSize { get; set; } = 32;
    public int SectionFontSize { get; set; } = 40;
    public int CreditsPositionPercentage { get; set; } = 60;
    public float StartDelay { get; set; } = 2.0f;
    public string StartText { get; set; } = "Stream Ending";
    public float StartTextHideTime { get; set; } = 3.0f;
    public Color SectionColor { get; set; } = new Color(255, 220, 150, 255);
    public Color ValuesColor { get; set; } = new Color(255, 255, 255, 255);
    public Color StartTextColor { get; set; } = new Color(255, 255, 255, 255);
}
