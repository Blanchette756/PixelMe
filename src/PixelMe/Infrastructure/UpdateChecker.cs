using System.Net.Http;
using System.Text.Json;

namespace PixelMe.Infrastructure;

public sealed record ReleaseInfo(string Version, string Url);

public static class UpdateChecker
{
    private const string LatestRelease = "https://api.github.com/repos/Blanchette756/PixelMe/releases/latest";

    public static async Task<ReleaseInfo?> FindNewerAsync()
    {
        try
        {
            using var document = JsonDocument.Parse(await Http.Client.GetStringAsync(LatestRelease));
            var tag = document.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? string.Empty;
            var url = document.RootElement.GetProperty("html_url").GetString() ?? AppInfo.RepositoryUrl;
            if (Version.TryParse(tag, out var latest) && Version.TryParse(AppInfo.Version, out var current) && latest > current)
            {
                return new ReleaseInfo(tag, url);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Log.Info($"Update check skipped: {ex.Message}");
        }

        return null;
    }
}
