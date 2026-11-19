using PixelMe.Settings;

namespace PixelMe.Island;

public readonly record struct IslandShape(double Width, double Height, double Radius);

public static class IslandMetrics
{
    public const double FloatingGap = 8;
    public const double ShoulderRadius = 12;
    public const double WindowWidth = 720;
    public const double WindowHeight = 290;
    public const double DefaultCompactWidth = 190;

    public static readonly IslandShape Expanded = new(600, 224, 34);

    public static IslandShape Compact(IslandStyle style, double width) => style == IslandStyle.Notch
        ? new IslandShape(width, 32, 12)
        : new IslandShape(width, 36, 18);
}
