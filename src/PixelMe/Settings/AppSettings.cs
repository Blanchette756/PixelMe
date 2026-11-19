namespace PixelMe.Settings;

public enum IslandThemeKind
{
    Glass,
    Black,
    Graphite,
    Light,
}

public enum IslandStyle
{
    Floating,
    Notch,
}

public enum AppLanguage
{
    Auto,
    English,
    Indonesian,
}

public enum FullscreenBehavior
{
    Strip,
    Hide,
    Stay,
}

public sealed class AppSettings
{
    public IslandThemeKind Theme { get; set; } = IslandThemeKind.Glass;

    public IslandStyle Style { get; set; } = IslandStyle.Notch;

    public AppLanguage Language { get; set; } = AppLanguage.Auto;

    public bool StartWithWindows { get; set; }

    public FullscreenBehavior Fullscreen { get; set; } = FullscreenBehavior.Strip;

    public int ExpandDelayMs { get; set; } = 120;

    public int CollapseDelayMs { get; set; } = 450;

    public bool MicHotkey { get; set; } = true;

    public bool Lyrics { get; set; }

    public bool Notifications { get; set; }

    public string WeatherCity { get; set; } = string.Empty;

    public List<string> WorldClocks { get; set; } = ["Europe/London", "America/New_York", "Asia/Tokyo"];

    public string PhotosFolder { get; set; } = string.Empty;

    public List<string> PinnedSites { get; set; } = ["https://www.youtube.com", "https://www.google.com", "https://web.whatsapp.com"];

    public string TwitchChannel { get; set; } = string.Empty;

    public string YouTubeLive { get; set; } = string.Empty;

    public bool ObsEnabled { get; set; }

    public string ObsHost { get; set; } = "127.0.0.1";

    public int ObsPort { get; set; } = 4455;

    public string ObsPasswordProtected { get; set; } = string.Empty;

    public void Normalize()
    {
        if (!Enum.IsDefined(Theme))
        {
            Theme = IslandThemeKind.Glass;
        }

        if (!Enum.IsDefined(Style))
        {
            Style = IslandStyle.Notch;
        }

        if (!Enum.IsDefined(Language))
        {
            Language = AppLanguage.Auto;
        }

        if (!Enum.IsDefined(Fullscreen))
        {
            Fullscreen = FullscreenBehavior.Strip;
        }

        ExpandDelayMs = Math.Clamp(ExpandDelayMs, 0, 2000);
        CollapseDelayMs = Math.Clamp(CollapseDelayMs, 0, 5000);
        ObsPort = ObsPort is > 0 and < 65536 ? ObsPort : 4455;
        WeatherCity ??= string.Empty;
        PhotosFolder ??= string.Empty;
        TwitchChannel ??= string.Empty;
        YouTubeLive ??= string.Empty;
        ObsHost = string.IsNullOrWhiteSpace(ObsHost) ? "127.0.0.1" : ObsHost.Trim();
        ObsPasswordProtected ??= string.Empty;
        WorldClocks = (WorldClocks ?? []).Where(z => !string.IsNullOrWhiteSpace(z)).Select(z => z.Trim()).Take(6).ToList();
        PinnedSites = (PinnedSites ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().Take(24).ToList();
    }
}
