using System.Net.Http;

namespace PixelMe.Infrastructure;

public static class Http
{
    public static HttpClient Client { get; } = Create();

    private static HttpClient Create()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"PixelMe/{AppInfo.Version} (+{AppInfo.RepositoryUrl})");
        return client;
    }
}
