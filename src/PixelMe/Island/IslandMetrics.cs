using PixelMe.Settings;

namespace PixelMe.Island;

public readonly record struct IslandShape(double Width, double Height, double Radius);

public static class IslandMetrics
{
    public const double FloatingGap = 8;
    public const double BorderThickness = 1;
    public const double WindowWidth = 560;
    public const double WindowHeight = 260;

    public static readonly IslandShape CompactFloating = new(200, 36, 18);
    public static readonly IslandShape CompactNotch = new(200, 32, 14);
    public static readonly IslandShape Expanded = new(440, 196, 30);

    public static IslandShape Compact(IslandStyle style) => style == IslandStyle.Notch ? CompactNotch : CompactFloating;
}
