using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using PixelMe.Infrastructure;

namespace PixelMe.Native;

internal sealed class FullscreenWatcher : IDisposable
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
    };

    private readonly IntPtr target;
    private readonly HwndSource source;
    private readonly uint callbackMessage;
    private readonly uint taskbarCreatedMessage;
    private readonly NativeMethods.WinEventProc foregroundProc;
    private readonly DispatcherTimer debounce;
    private IntPtr foregroundHook;
    private bool registered;
    private bool? lastState;

    public FullscreenWatcher(IntPtr windowHandle)
    {
        target = windowHandle;
        source = HwndSource.FromHwnd(windowHandle) ?? throw new InvalidOperationException("The island window has no HwndSource.");
        callbackMessage = NativeMethods.RegisterWindowMessage("PixelMe.AppBarNotify");
        taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
        foregroundProc = OnForegroundChanged;
        debounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
        debounce.Tick += (_, _) =>
        {
            debounce.Stop();
            Evaluate();
        };
    }

    public event Action<bool>? Changed;

    public void Start()
    {
        source.AddHook(WndProc);
        Register();
        foregroundHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            foregroundProc,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        if (foregroundHook == IntPtr.Zero)
        {
            Log.Warn("Foreground hook could not be installed");
        }

        Evaluate();
    }

    public void Dispose()
    {
        debounce.Stop();
        if (foregroundHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(foregroundHook);
            foregroundHook = IntPtr.Zero;
        }

        Unregister();
        source.RemoveHook(WndProc);
    }

    private void Register()
    {
        var data = CreateData();
        registered = NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref data) != UIntPtr.Zero;
        if (!registered)
        {
            Log.Warn("Fullscreen notifications from the shell are unavailable");
        }
    }

    private void Unregister()
    {
        if (!registered)
        {
            return;
        }

        var data = CreateData();
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref data);
        registered = false;
    }

    private NativeMethods.APPBARDATA CreateData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
        hWnd = target,
        uCallbackMessage = callbackMessage,
    };

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var message = (uint)msg;
        if (message == callbackMessage && wParam.ToInt64() == NativeMethods.ABN_FULLSCREENAPP)
        {
            Schedule();
        }
        else if (message == taskbarCreatedMessage)
        {
            registered = false;
            Register();
            Schedule();
        }

        return IntPtr.Zero;
    }

    private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        Schedule();
    }

    private void Schedule()
    {
        debounce.Stop();
        debounce.Start();
    }

    private void Evaluate()
    {
        var state = IsForegroundFullscreen();
        if (state == lastState)
        {
            return;
        }

        lastState = state;
        Changed?.Invoke(state);
    }

    private bool IsForegroundFullscreen()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero
            || foreground == target
            || foreground == NativeMethods.GetShellWindow()
            || foreground == NativeMethods.GetDesktopWindow())
        {
            return false;
        }

        if (ShellClasses.Contains(NativeMethods.ReadClassName(foreground)) || NativeMethods.IsZoomed(foreground))
        {
            return false;
        }

        var className = NativeMethods.ReadClassName(foreground);
        if (className.StartsWith("HwndWrapper[PixelMe", StringComparison.Ordinal))
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(foreground, out var owner);
        if (owner == (uint)Environment.ProcessId)
        {
            return false;
        }

        var monitor = NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor != NativeMethods.MonitorFromWindow(target, NativeMethods.MONITOR_DEFAULTTONEAREST))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(foreground, out var window) || !NativeMethods.TryGetMonitorBounds(monitor, out var screen))
        {
            return false;
        }

        return window.Left <= screen.Left
            && window.Top <= screen.Top
            && window.Right >= screen.Right
            && window.Bottom >= screen.Bottom;
    }
}
