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

public sealed class AppSettings
{
    public IslandThemeKind Theme { get; set; } = IslandThemeKind.Glass;

    public IslandStyle Style { get; set; } = IslandStyle.Notch;

    public AppLanguage Language { get; set; } = AppLanguage.Auto;

    public bool StartWithWindows { get; set; }

    public bool HideInFullscreen { get; set; } = true;

    public int ExpandDelayMs { get; set; } = 120;

    public int CollapseDelayMs { get; set; } = 450;

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

        ExpandDelayMs = Math.Clamp(ExpandDelayMs, 0, 2000);
        CollapseDelayMs = Math.Clamp(CollapseDelayMs, 0, 5000);
    }
}
