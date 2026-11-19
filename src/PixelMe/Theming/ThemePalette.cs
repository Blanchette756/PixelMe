using System.Windows;
using System.Windows.Media;
using PixelMe.Settings;

namespace PixelMe.Theming;

public sealed class ThemePalette
{
    public required Brush Background { get; init; }

    public required Brush Border { get; init; }

    public required Brush Foreground { get; init; }

    public required Brush Secondary { get; init; }

    public required Brush Subtle { get; init; }

    public required Brush Accent { get; init; }

    public static ThemePalette For(IslandThemeKind kind) => kind switch
    {
        IslandThemeKind.Black => new ThemePalette
        {
            Background = Solid(0xFF000000),
            Border = Solid(0x1FFFFFFF),
            Foreground = Solid(0xFFFFFFFF),
            Secondary = Solid(0x99FFFFFF),
            Subtle = Solid(0x1AFFFFFF),
            Accent = Solid(0xFF34D399),
        },
        IslandThemeKind.Graphite => new ThemePalette
        {
            Background = Vertical(0xF72E3036, 0xF71C1D21),
            Border = Vertical(0x33FFFFFF, 0x14FFFFFF),
            Foreground = Solid(0xFFF4F4F6),
            Secondary = Solid(0x99F4F4F6),
            Subtle = Solid(0x1FFFFFFF),
            Accent = Solid(0xFF60A5FA),
        },
        IslandThemeKind.Light => new ThemePalette
        {
            Background = Vertical(0xF7FCFCFD, 0xF7ECEDF1),
            Border = Vertical(0x14000000, 0x29000000),
            Foreground = Solid(0xFF111216),
            Secondary = Solid(0x99111216),
            Subtle = Solid(0x14000000),
            Accent = Solid(0xFF059669),
        },
        _ => new ThemePalette
        {
            Background = Vertical(0xF22C2E38, 0xEB0E0F14),
            Border = Vertical(0x66FFFFFF, 0x14FFFFFF),
            Foreground = Solid(0xFFFFFFFF),
            Secondary = Solid(0xB3FFFFFF),
            Subtle = Solid(0x1FFFFFFF),
            Accent = Solid(0xFF34D399),
        },
    };

    public void ApplyTo(ResourceDictionary resources)
    {
        resources["Island.Foreground"] = Foreground;
        resources["Island.Secondary"] = Secondary;
        resources["Island.Subtle"] = Subtle;
        resources["Island.Accent"] = Accent;
    }

    private static SolidColorBrush Solid(uint argb)
    {
        var brush = new SolidColorBrush(ToColor(argb));
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush Vertical(uint top, uint bottom)
    {
        var brush = new LinearGradientBrush(ToColor(top), ToColor(bottom), new Point(0, 0), new Point(0, 1));
        brush.Freeze();
        return brush;
    }

    private static Color ToColor(uint argb) => Color.FromArgb(
        (byte)(argb >> 24),
        (byte)(argb >> 16),
        (byte)(argb >> 8),
        (byte)argb);
}
