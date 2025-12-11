using Raylib_cs;

namespace EndingApp;

public enum FontWeight
{
    Regular,
    Italic,
    Bold,
    BoldItalic,
}

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
        config.Ending.CarouselPositionPercentage = data.GetInt(
            "ending.carouselPositionPercentage",
            50
        );
        config.Ending.CarouselSizePercentage = data.GetInt("ending.carouselSizePercentage", 20);
        config.Ending.CarouselMediaTitleFontSize = data.GetInt(
            "ending.carouselMediaTitleFontSize",
            24
        );
        config.Ending.CarouselMediaTitleFontWeight = EndingConfig.ParseFontWeight(
            data.GetString("ending.carouselMediaTitleFontWeight", "bold")
        );
        var carouselTitleColor = data.GetColorHex(
            "ending.carouselMediaTitleColorHex",
            (255, 255, 255, 255)
        );
        config.Ending.CarouselMediaTitleColor = new Color(
            carouselTitleColor.R,
            carouselTitleColor.G,
            carouselTitleColor.B,
            carouselTitleColor.A
        );

        config.Ending.StartDelay = data.GetFloat("ending.endingStartDelay", 2.0f);
        config.Ending.StartText = data.GetString("ending.endingStartText", "Stream Ending");
        config.Ending.StartTextHideTime = data.GetFloat("ending.endingStartTextHideTime", 3.0f);
        config.Ending.StartTextFontSize = data.GetInt("ending.endingStartTextFontSize", 32);
        config.Ending.StartTextFontWeight = EndingConfig.ParseFontWeight(
            data.GetString("ending.endingStartTextFontWeight", "regular")
        );
        config.Ending.ValueFontWeight = EndingConfig.ParseFontWeight(
            data.GetString("ending.fontWeight", "regular")
        );
        config.Ending.SectionFontWeight = EndingConfig.ParseFontWeight(
            data.GetString("ending.sectionFontWeight", "regular")
        );

        // Spacing configuration
        config.Ending.SectionSpacing = data.GetFloat("ending.sectionSpacing", 2.0f);
        config.Ending.ValueSpacing = data.GetFloat("ending.valueSpacing", 2.0f);
        config.Ending.StartTextSpacing = data.GetFloat("ending.startTextSpacing", 2.0f);
        config.Ending.EndTextSpacing = data.GetFloat("ending.endTextSpacing", 2.0f);
        config.Ending.CopyrightSpacing = data.GetFloat("ending.copyrightSpacing", 2.0f);
        config.Ending.CarouselMediaTitleSpacing = data.GetFloat(
            "ending.carouselMediaTitleSpacing",
            2.0f
        );

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

        // End text properties
        config.Ending.EndText = data.GetString("ending.endingEndText", config.Ending.EndText);
        config.Ending.EndTextHideTime = data.GetFloat(
            "ending.endingEndTextHideTime",
            config.Ending.EndTextHideTime
        );
        config.Ending.EndTextFontSize = data.GetInt(
            "ending.endingEndTextFontSize",
            config.Ending.EndTextFontSize
        );
        config.Ending.EndTextFontWeight = EndingConfig.ParseFontWeight(
            data.GetString("ending.endingEndTextFontWeight", "regular")
        );
        var endTextColor = data.GetColorHex("ending.endingEndTextColorHex", (255, 255, 255, 255));
        config.Ending.EndTextColor = new Color(
            endTextColor.R,
            endTextColor.G,
            endTextColor.B,
            endTextColor.A
        );
        // End text background color (plain background to show behind end text/emote)
        var endBgColor = data.GetColorHex("ending.endingEndBackgroundColorHex", (34, 34, 34, 255));
        config.Ending.EndBackgroundColor = new Color(
            endBgColor.R,
            endBgColor.G,
            endBgColor.B,
            endBgColor.A
        );
        config.Ending.EndBackgroundFadeDuration = data.GetFloat(
            "ending.endingEndBackgroundFadeDuration",
            0.5f
        );
        // EndText fade durations (default 1.5s each — was 0.5s; user requested +1 second)
        config.Ending.EndTextFadeInDuration = data.GetFloat(
            "ending.endingEndTextFadeInDuration",
            1.5f
        );
        config.Ending.EndTextFadeOutDuration = data.GetFloat(
            "ending.endingEndTextFadeOutDuration",
            1.5f
        );
        // Emote path
        config.Ending.EmotePath = data.GetString("ending.endingEmote", "");
        // Copyright text will be shown after the end text fades out
        config.Ending.CopyrightText = data.GetString("ending.copyrightText", "");
        config.Ending.CopyrightFontSize = data.GetInt("ending.copyrightFontSize", 18);
        config.Ending.CopyrightFontWeight = EndingConfig.ParseFontWeight(
            data.GetString("ending.copyrightFontWeight", "regular")
        );
        var copyrightColor = data.GetColorHex("ending.copyrightColorHex", (200, 200, 200, 255));
        config.Ending.CopyrightColor = new Color(
            copyrightColor.R,
            copyrightColor.G,
            copyrightColor.B,
            copyrightColor.A
        );
        config.Ending.CopyrightFadeInDuration = data.GetFloat(
            "ending.copyrightFadeInDuration",
            1.0f
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
    public int CarouselPositionPercentage { get; set; } = 50;
    public int CarouselSizePercentage { get; set; } = 20;
    public int CarouselMediaTitleFontSize { get; set; } = 24;
    public FontWeight CarouselMediaTitleFontWeight { get; set; } = FontWeight.Bold;
    public Color CarouselMediaTitleColor { get; set; } = new Color(255, 255, 255, 255);
    public float StartDelay { get; set; } = 2.0f;
    public string StartText { get; set; } = "Stream Ending";
    public float StartTextHideTime { get; set; } = 3.0f;
    public int StartTextFontSize { get; set; } = 32;
    public FontWeight ValueFontWeight { get; set; }
    public FontWeight SectionFontWeight { get; set; }
    public FontWeight StartTextFontWeight { get; set; }

    // Spacing configuration
    public float SectionSpacing { get; set; } = 2.0f;
    public float ValueSpacing { get; set; } = 2.0f;
    public float StartTextSpacing { get; set; } = 2.0f;
    public float EndTextSpacing { get; set; } = 2.0f;
    public float CopyrightSpacing { get; set; } = 2.0f;
    public float CarouselMediaTitleSpacing { get; set; } = 2.0f;

    // New end text configuration
    public string EndText { get; set; } = "Thanks for watching, see you next time!";
    public float EndTextHideTime { get; set; } = 5.0f;
    public int EndTextFontSize { get; set; } = 32;
    public FontWeight EndTextFontWeight { get; set; } = FontWeight.Regular;

    public static FontWeight ParseFontWeight(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return FontWeight.Regular;

        s = s.Trim().ToLowerInvariant();
        return s switch
        {
            "regular" => FontWeight.Regular,
            "italic" => FontWeight.Italic,
            "bold" => FontWeight.Bold,
            "bold-italic" => FontWeight.BoldItalic,
            "bolditalic" => FontWeight.BoldItalic,
            "bold_italic" => FontWeight.BoldItalic,
            _ => FontWeight.Regular,
        };
    }

    public Color SectionColor { get; set; } = new Color(255, 220, 150, 255);
    public Color ValuesColor { get; set; } = new Color(255, 255, 255, 255);
    public Color StartTextColor { get; set; } = new Color(255, 255, 255, 255);
    public Color EndTextColor { get; set; } = new Color(255, 255, 255, 255);
    public Color EndBackgroundColor { get; set; } = new Color(34, 34, 34, 255);
    public float EndBackgroundFadeDuration { get; set; } = 0.5f;
    public float EndTextFadeInDuration { get; set; } = 1.5f;
    public float EndTextFadeOutDuration { get; set; } = 1.5f;
    public string EmotePath { get; set; } = "";
    public string CopyrightText { get; set; } = "";
    public int CopyrightFontSize { get; set; } = 18;
    public FontWeight CopyrightFontWeight { get; set; } = FontWeight.Regular;
    public Color CopyrightColor { get; set; } = new Color(200, 200, 200, 255);
    public float CopyrightFadeInDuration { get; set; } = 1.0f;
}
