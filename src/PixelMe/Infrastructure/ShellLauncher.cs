using System.Diagnostics;

namespace PixelMe.Infrastructure;

public static class ShellLauncher
{
    public static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error($"Opening '{target}' failed", ex);
        }
    }

    public static void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            Log.Error($"Creating '{folder}' failed", ex);
            return;
        }

        Open(folder);
    }
}
