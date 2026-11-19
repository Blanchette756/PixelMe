using System.Management;
using PixelMe.Infrastructure;

namespace PixelMe.Modules.Controls;

public sealed class Brightness
{
    private readonly object gate = new();
    private int? pending;
    private bool writing;

    public static async Task<int?> ReadAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\wmi"), new SelectQuery("WmiMonitorBrightness"));
                foreach (ManagementObject item in searcher.Get())
                {
                    using (item)
                    {
                        return (int?)Convert.ToInt32(item["CurrentBrightness"]);
                    }
                }
            }
            catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            {
            }

            return null;
        });
    }

    public void Write(int value)
    {
        lock (gate)
        {
            pending = Math.Clamp(value, 0, 100);
            if (writing)
            {
                return;
            }

            writing = true;
        }

        Task.Run(Drain);
    }

    private void Drain()
    {
        while (true)
        {
            int value;
            lock (gate)
            {
                if (pending is null)
                {
                    writing = false;
                    return;
                }

                value = pending.Value;
                pending = null;
            }

            try
            {
                using var searcher = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\wmi"), new SelectQuery("WmiMonitorBrightnessMethods"));
                foreach (ManagementObject item in searcher.Get())
                {
                    using (item)
                    {
                        item.InvokeMethod("WmiSetBrightness", [1u, (byte)value]);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Changing brightness failed", ex);
            }
        }
    }
}
