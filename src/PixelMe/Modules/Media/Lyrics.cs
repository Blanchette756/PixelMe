using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using PixelMe.Infrastructure;

namespace PixelMe.Modules.Media;

public readonly record struct LyricLine(TimeSpan Time, string Text);

public static partial class Lyrics
{
    private static readonly Dictionary<string, IReadOnlyList<LyricLine>?> Cache = [];

    public static async Task<IReadOnlyList<LyricLine>?> FindAsync(string artist, string title, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
        {
            return null;
        }

        var key = $"{artist}\n{title}".ToLowerInvariant();
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        IReadOnlyList<LyricLine>? result = null;
        try
        {
            var url = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
            using var response = await Http.Client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var best = document.RootElement.EnumerateArray()
                    .Where(e => e.TryGetProperty("syncedLyrics", out var s) && s.ValueKind == JsonValueKind.String)
                    .OrderBy(e => duration > TimeSpan.Zero && e.TryGetProperty("duration", out var d) && d.TryGetDouble(out var seconds)
                        ? Math.Abs(seconds - duration.TotalSeconds)
                        : 0)
                    .Select(e => e.GetProperty("syncedLyrics").GetString())
                    .FirstOrDefault();
                result = best is null ? null : Parse(best);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Log.Warn("Lyrics could not be fetched", ex);
            return null;
        }

        Cache[key] = result;
        return result;
    }

    public static string? LineAt(IReadOnlyList<LyricLine>? lines, TimeSpan position)
    {
        if (lines is null || lines.Count == 0)
        {
            return null;
        }

        var low = 0;
        var high = lines.Count - 1;
        var found = -1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (lines[mid].Time <= position)
            {
                found = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return found < 0 ? null : lines[found].Text;
    }

    private static List<LyricLine> Parse(string lrc)
    {
        var lines = new List<LyricLine>();
        foreach (var raw in lrc.Split('\n'))
        {
            var matches = Tag().Matches(raw);
            if (matches.Count == 0)
            {
                continue;
            }

            var text = Tag().Replace(raw, string.Empty).Trim();
            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var seconds = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                lines.Add(new LyricLine(TimeSpan.FromSeconds((minutes * 60) + seconds), text));
            }
        }

        lines.Sort((a, b) => a.Time.CompareTo(b.Time));
        return lines;
    }

    [GeneratedRegex(@"\[(\d+):(\d+(?:\.\d+)?)\]")]
    private static partial Regex Tag();
}
