using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
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

    public Brush Surface { get; init; } = Brushes.Black;

    public bool UsesBackdrop { get; init; }

    public uint BackdropTint { get; init; }

    public static ThemePalette For(IslandThemeKind kind)
    {
        var transparency = TransparencyEnabled();
        var wallpaper = WallpaperColors.Current();
        var vivid = new SolidColorBrush(wallpaper.Vivid);
        vivid.Freeze();

        return kind switch
        {
            IslandThemeKind.Black => new ThemePalette
            {
                Background = Solid(0xFF0A0A0B),
                Border = Solid(0x00000000),
                Foreground = Solid(0xFFF2F2F2),
                Secondary = Solid(0xFF8E8E93),
                Subtle = Solid(0x2BFFFFFF),
                Accent = Solid(0xFF34D399),
                Surface = Solid(0xFF0A0A0B),
            },
            IslandThemeKind.Graphite => new ThemePalette
            {
                Background = transparency ? Vertical(0x14FFFFFF, 0x00FFFFFF) : Vertical(0xFA2E3036, 0xFA1C1D21),
                Border = Vertical(0x59FFFFFF, 0x0FFFFFFF),
                Foreground = Solid(0xFFF4F4F6),
                Secondary = Solid(0xB3F4F4F6),
                Subtle = Solid(0x26FFFFFF),
                Accent = vivid,
                Surface = Solid(0xFF2A2B30),
                UsesBackdrop = transparency,
                BackdropTint = 0xB8222327,
            },
            IslandThemeKind.Light => new ThemePalette
            {
                Background = transparency ? Vertical(0x59FFFFFF, 0x14FFFFFF) : Vertical(0xFAFCFCFD, 0xFAECEDF1),
                Border = Vertical(0x99FFFFFF, 0x26000000),
                Foreground = Solid(0xFF111216),
                Secondary = Solid(0x99111216),
                Subtle = Solid(0x14000000),
                Accent = Solid(0xFF059669),
                Surface = Solid(0xFFF2F3F6),
                UsesBackdrop = transparency,
                BackdropTint = 0xA6F2F3F5,
            },
            IslandThemeKind.Wallpaper => new ThemePalette
            {
                Background = transparency ? Vertical(0x1AFFFFFF, 0x00FFFFFF) : Solid(Pack(0xFF, WallpaperColors.Darken(wallpaper.Average, 0.55))),
                Border = Vertical(0x66FFFFFF, 0x14FFFFFF),
                Foreground = Solid(0xFFFFFFFF),
                Secondary = Solid(0xC0FFFFFF),
                Subtle = Solid(0x2EFFFFFF),
                Accent = vivid,
                Surface = Solid(Pack(0xFF, WallpaperColors.Darken(wallpaper.Average, 0.5))),
                UsesBackdrop = transparency,
                BackdropTint = Pack(0xB0, WallpaperColors.Darken(wallpaper.Average, 0.45)),
            },
            _ => new ThemePalette
            {
                Background = transparency ? Vertical(0x24FFFFFF, 0x05FFFFFF) : Vertical(0xF22C2E38, 0xEB0E0F14),
                Border = Vertical(0x99FFFFFF, 0x14FFFFFF),
                Foreground = Solid(0xFFFFFFFF),
                Secondary = Solid(0xCCFFFFFF),
                Subtle = Solid(0x2EFFFFFF),
                Accent = vivid,
                Surface = Solid(0xFF1C1D24),
                UsesBackdrop = transparency,
                BackdropTint = 0x66101016,
            },
        };
    }

    public void ApplyTo(ResourceDictionary resources)
    {
        resources["Island.Foreground"] = Foreground;
        resources["Island.Secondary"] = Secondary;
        resources["Island.Subtle"] = Subtle;
        resources["Island.Accent"] = Accent;
        resources["Island.Surface"] = Surface;
        resources["Island.Background"] = Background;
        resources["Island.Border"] = Border;
    }

    private static uint Pack(byte alpha, Color color) => ((uint)alpha << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    private static bool TransparencyEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("EnableTransparency") is not int value || value != 0;
        }
        catch
        {
            return true;
        }
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
