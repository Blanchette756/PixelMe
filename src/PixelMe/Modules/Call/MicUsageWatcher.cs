using Microsoft.Win32;
using PixelMe.Infrastructure;
using PixelMe.Native;

namespace PixelMe.Modules.Call;

public sealed class MicUsageWatcher : IDisposable
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    private readonly ManualResetEvent stop = new(false);
    private Thread? thread;

    public event Action<bool>? Changed;

    public bool InUse { get; private set; }

    public void Start()
    {
        InUse = Scan();
        thread = new Thread(Run) { IsBackground = true, Name = "PixelMe microphone watcher" };
        thread.Start();
    }

    public void Dispose()
    {
        stop.Set();
        thread?.Join(1000);
        stop.Dispose();
    }

    private static bool Scan()
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(KeyPath);
            return root is not null && AnyInUse(root);
        }
        catch (Exception ex)
        {
            Log.Warn("Reading microphone usage failed", ex);
            return false;
        }
    }

    private static bool AnyInUse(RegistryKey parent)
    {
        foreach (var name in parent.GetSubKeyNames())
        {
            using var child = parent.OpenSubKey(name);
            if (child is null)
            {
                continue;
            }

            if (name == "NonPackaged")
            {
                if (AnyInUse(child))
                {
                    return true;
                }

                continue;
            }

            if (child.GetValue("LastUsedTimeStop") is long stopped && stopped == 0
                && child.GetValue("LastUsedTimeStart") is long started && started > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void Run()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key is null)
            {
                return;
            }

            using var changed = new AutoResetEvent(false);
            while (true)
            {
                var result = NativeMethods.RegNotifyChangeKeyValue(
                    key.Handle.DangerousGetHandle(),
                    true,
                    NativeMethods.REG_NOTIFY_CHANGE_LAST_SET | NativeMethods.REG_NOTIFY_CHANGE_NAME,
                    changed.SafeWaitHandle.DangerousGetHandle(),
                    true);
                if (result != 0 || WaitHandle.WaitAny([changed, stop]) == 1)
                {
                    return;
                }

                var now = Scan();
                if (now != InUse)
                {
                    InUse = now;
                    Changed?.Invoke(now);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Microphone watcher stopped", ex);
        }
    }
}
