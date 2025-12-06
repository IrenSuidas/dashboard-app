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
        config.Ending.StartDelay = data.GetFloat("ending.endingStartDelay", 2.0f);
        config.Ending.StartText = data.GetString("ending.endingStartText", "Stream Ending");
        config.Ending.StartTextHideTime = data.GetFloat("ending.endingStartTextHideTime", 3.0f);

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
    public float StartDelay { get; set; } = 2.0f;
    public string StartText { get; set; } = "Stream Ending";
    public float StartTextHideTime { get; set; } = 3.0f;
}
