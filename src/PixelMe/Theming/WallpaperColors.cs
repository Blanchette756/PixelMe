using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelMe.Infrastructure;
using PixelMe.Native;

namespace PixelMe.Theming;

public readonly record struct WallpaperTone(Color Average, Color Vivid);

public static class WallpaperColors
{
    private static WallpaperTone? cached;

    public static void Invalidate() => cached = null;

    public static WallpaperTone Current() => cached ??= Read();

    public static Color Vivid(BitmapSource image, Color fallback)
    {
        var pixels = Sample(image, out var count);
        if (count == 0)
        {
            return fallback;
        }

        var weights = new double[12];
        var reds = new double[12];
        var greens = new double[12];
        var blues = new double[12];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] < 128)
            {
                continue;
            }

            var b = pixels[i] / 255.0;
            var g = pixels[i + 1] / 255.0;
            var r = pixels[i + 2] / 255.0;
            ToHsv(r, g, b, out var hue, out var saturation, out var value);
            if (value < 0.2 || saturation < 0.25)
            {
                continue;
            }

            var weight = saturation * saturation * value;
            var bucket = (int)(hue / 30) % 12;
            weights[bucket] += weight;
            reds[bucket] += r * weight;
            greens[bucket] += g * weight;
            blues[bucket] += b * weight;
        }

        var best = Array.IndexOf(weights, weights.Max());
        if (weights[best] <= 0)
        {
            return fallback;
        }

        ToHsv(reds[best] / weights[best], greens[best] / weights[best], blues[best] / weights[best], out var h, out var s, out var v);
        return FromHsv(h, Math.Clamp(s, 0.35, 0.75), Math.Max(v, 0.85));
    }

    public static Color Average(BitmapSource image)
    {
        var pixels = Sample(image, out var count);
        if (count == 0)
        {
            return Color.FromRgb(32, 33, 38);
        }

        double r = 0, g = 0, b = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            b += pixels[i];
            g += pixels[i + 1];
            r += pixels[i + 2];
        }

        return Color.FromRgb((byte)(r / count), (byte)(g / count), (byte)(b / count));
    }

    public static Color Darken(Color color, double amount) => Color.FromRgb(
        (byte)(color.R * (1 - amount)),
        (byte)(color.G * (1 - amount)),
        (byte)(color.B * (1 - amount)));

    public static void ToHsv(double r, double g, double b, out double hue, out double saturation, out double value)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        value = max;
        saturation = max <= 0 ? 0 : delta / max;
        hue = delta <= 0 ? 0
            : max == r ? 60 * (((g - b) / delta) % 6)
            : max == g ? 60 * (((b - r) / delta) + 2)
            : 60 * (((r - g) / delta) + 4);
        if (hue < 0)
        {
            hue += 360;
        }
    }

    public static Color FromHsv(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs(((hue / 60) % 2) - 1));
        var m = value - c;
        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    private static byte[] Sample(BitmapSource image, out int count)
    {
        count = 0;
        if (image.PixelWidth == 0 || image.PixelHeight == 0)
        {
            return [];
        }

        var scaled = new TransformedBitmap(image, new ScaleTransform(24.0 / image.PixelWidth, 24.0 / image.PixelHeight));
        var converted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        count = converted.PixelWidth * converted.PixelHeight;
        return pixels;
    }

    private static WallpaperTone Read()
    {
        var glass = SystemParameters.WindowGlassColor;
        var fallback = new WallpaperTone(Darken(glass, 0.5), FromHsvOf(glass));
        try
        {
            var path = NativeMethods.WallpaperPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return fallback;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 96;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return new WallpaperTone(Average(image), Vivid(image, fallback.Vivid));
        }
        catch (Exception ex)
        {
            Log.Warn("Reading the wallpaper colours failed", ex);
            return fallback;
        }
    }

    private static Color FromHsvOf(Color color)
    {
        ToHsv(color.R / 255.0, color.G / 255.0, color.B / 255.0, out var h, out var s, out var v);
        return FromHsv(h, Math.Clamp(s, 0.35, 0.75), Math.Max(v, 0.85));
    }
}
