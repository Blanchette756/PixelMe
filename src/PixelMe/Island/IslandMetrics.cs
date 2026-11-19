using PixelMe.Settings;

namespace PixelMe.Island;

public readonly record struct IslandShape(double Width, double Height, double Radius);

public enum IslandPresence
{
    Normal,
    Strip,
    Hidden,
}

public static class IslandMetrics
{
    public const double FloatingGap = 8;
    public const double ShoulderRadius = 14;
    public const double WindowWidth = 720;
    public const double WindowHeight = 290;
    public const double DefaultCompactWidth = 170;
    public const double StripTextWidth = 360;
    public const double HoverGrowth = 12;
    public const double BounceGrowth = 30;

    public static readonly IslandShape Expanded = new(580, 214, 32);

    public static IslandShape Compact(IslandStyle style, double width) => style == IslandStyle.Notch
        ? new IslandShape(width, 32, 12)
        : new IslandShape(width, 34, 17);

    public static IslandShape Strip(IslandStyle style) => style == IslandStyle.Notch
        ? new IslandShape(380, 22, 8)
        : new IslandShape(380, 24, 12);
}
