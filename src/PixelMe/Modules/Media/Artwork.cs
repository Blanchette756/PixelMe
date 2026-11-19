using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Storage.Streams;

namespace PixelMe.Modules.Media;

public sealed record Artwork(ImageSource Image, Color Accent)
{
    private const int DecodeSize = 240;
    private const int SampleSize = 24;

    public static async Task<Artwork?> LoadAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        var buffer = new MemoryStream();
        using (var stream = await reference.OpenReadAsync())
        using (var source = stream.AsStreamForRead())
        {
            await source.CopyToAsync(buffer);
        }

        if (buffer.Length == 0)
        {
            return null;
        }

        return await Task.Run(() => Decode(buffer));
    }

    private static Artwork Decode(MemoryStream buffer)
    {
        buffer.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = DecodeSize;
        image.StreamSource = buffer;
        image.EndInit();
        image.Freeze();
        return new Artwork(image, ExtractAccent(image));
    }

    private static Color ExtractAccent(BitmapSource image)
    {
        if (image.PixelWidth == 0 || image.PixelHeight == 0)
        {
            return MediaState.DefaultAccent;
        }

        var scaled = new TransformedBitmap(
            image,
            new ScaleTransform((double)SampleSize / image.PixelWidth, (double)SampleSize / image.PixelHeight));
        var converted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var pixels = new byte[width * height * 4];
        converted.CopyPixels(pixels, width * 4, 0);

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
            return Color.FromRgb(0xE5, 0xE7, 0xEB);
        }

        var red = reds[best] / weights[best];
        var green = greens[best] / weights[best];
        var blue = blues[best] / weights[best];
        ToHsv(red, green, blue, out var h, out var s, out var v);
        return FromHsv(h, Math.Clamp(s, 0.35, 0.75), Math.Max(v, 0.85));
    }

    private static void ToHsv(double r, double g, double b, out double hue, out double saturation, out double value)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        value = max;
        saturation = max <= 0 ? 0 : delta / max;

        if (delta <= 0)
        {
            hue = 0;
        }
        else if (max == r)
        {
            hue = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            hue = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            hue = 60 * (((r - g) / delta) + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }
    }

    private static Color FromHsv(double hue, double saturation, double value)
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

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
