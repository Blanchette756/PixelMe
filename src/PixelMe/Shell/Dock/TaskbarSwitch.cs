using System.Globalization;
using System.Runtime.InteropServices;
using PixelMe.Infrastructure;
using PixelMe.Native;

namespace PixelMe.Shell.Dock;

public static class TaskbarSwitch
{
    public const string RestoreArgument = "--restore-taskbar";

    private static string MarkerPath => Path.Combine(AppPaths.LocalFolder, "taskbar-state.txt");

    public static IntPtr PrimaryTray => NativeMethods.FindWindow("Shell_TrayWnd", null);

    public static void Hide()
    {
        var tray = PrimaryTray;
        if (tray == IntPtr.Zero)
        {
            return;
        }

        try
        {
            if (!File.Exists(MarkerPath))
            {
                Directory.CreateDirectory(AppPaths.LocalFolder);
                File.WriteAllText(MarkerPath, GetState(tray).ToString(CultureInfo.InvariantCulture));
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Remembering the taskbar state failed", ex);
        }

        SetState(tray, NativeMethods.ABS_AUTOHIDE);
        foreach (var window in Trays())
        {
            NativeMethods.ShowWindow(window, NativeMethods.SW_HIDE);
        }
    }

    public static void HideAgain(IntPtr hwnd)
    {
        if (Trays().Contains(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
        }
    }

    public static void Restore()
    {
        var tray = PrimaryTray;
        var previous = NativeMethods.ABS_ALWAYSONTOP;
        try
        {
            if (File.Exists(MarkerPath) && int.TryParse(File.ReadAllText(MarkerPath), NumberStyles.Integer, CultureInfo.InvariantCulture, out var saved))
            {
                previous = saved;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Reading the saved taskbar state failed", ex);
        }

        if (tray != IntPtr.Zero)
        {
            SetState(tray, previous);
            foreach (var window in Trays())
            {
                NativeMethods.ShowWindow(window, NativeMethods.SW_SHOWNA);
            }
        }

        try
        {
            File.Delete(MarkerPath);
        }
        catch (Exception ex)
        {
            Log.Warn("Clearing the taskbar marker failed", ex);
        }
    }

    public static void RestoreIfLeftHidden()
    {
        if (File.Exists(MarkerPath))
        {
            Restore();
        }
    }

    private static List<IntPtr> Trays()
    {
        var list = new List<IntPtr>();
        var primary = PrimaryTray;
        if (primary != IntPtr.Zero)
        {
            list.Add(primary);
        }

        var next = IntPtr.Zero;
        while ((next = NativeMethods.FindWindowEx(IntPtr.Zero, next, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
        {
            list.Add(next);
        }

        return list;
    }

    private static int GetState(IntPtr tray)
    {
        var data = new NativeMethods.APPBARDATA { cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(), hWnd = tray };
        return (int)NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref data).ToUInt32();
    }

    private static void SetState(IntPtr tray, int state)
    {
        var data = new NativeMethods.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
            hWnd = tray,
            lParam = new IntPtr(state),
        };
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETSTATE, ref data);
    }
}
