namespace PixelMe.Infrastructure;

public static class AppInfo
{
    public const string Name = "PixelMe";
    public const string RepositoryUrl = "https://github.com/Blanchette756/PixelMe";

    public static string Version { get; } = typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
