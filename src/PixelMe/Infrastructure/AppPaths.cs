namespace PixelMe.Infrastructure;

public static class AppPaths
{
    public static string RoamingFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.Name);

    public static string LocalFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Name);

    public static string SettingsFile => Path.Combine(RoamingFolder, "settings.json");

    public static string LogFolder => Path.Combine(LocalFolder, "logs");
}
