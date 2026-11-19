using Microsoft.Win32;

namespace PixelMe.Infrastructure;

public static class Autostart
{
    public const string LaunchArgument = "--autostart";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                {
                    Log.Warn("Autostart skipped because the executable path is unknown");
                    return;
                }

                key.SetValue(AppInfo.Name, $"\"{exe}\" {LaunchArgument}");
            }
            else if (key.GetValue(AppInfo.Name) is not null)
            {
                key.DeleteValue(AppInfo.Name, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Updating autostart failed", ex);
        }
    }
}
