using System.Diagnostics;
using System.Text;
using System.Windows.Media;
using PixelMe.Infrastructure;
using PixelMe.Native;

namespace PixelMe.Shell.Dock;

public sealed class DockApp
{
    public required string Key { get; init; }

    public required string Name { get; set; }

    public string? Shortcut { get; init; }

    public ImageSource? Icon { get; set; }

    public List<IntPtr> Windows { get; } = [];

    public bool Pinned { get; init; }
}

public static class AppWindows
{
    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow",
    };

    private static readonly Dictionary<string, string?> ShortcutTargets = new(StringComparer.OrdinalIgnoreCase);

    private static string PinnedFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

    public static List<DockApp> Collect()
    {
        var apps = new List<DockApp>();
        var byKey = new Dictionary<string, DockApp>(StringComparer.OrdinalIgnoreCase);

        foreach (var shortcut in PinnedShortcuts())
        {
            var target = ResolveShortcut(shortcut);
            var key = string.IsNullOrEmpty(target) ? shortcut : target;
            if (byKey.ContainsKey(key))
            {
                continue;
            }

            var app = new DockApp
            {
                Key = key,
                Name = Path.GetFileNameWithoutExtension(shortcut),
                Shortcut = shortcut,
                Icon = !string.IsNullOrEmpty(target) && File.Exists(target) ? ShellIcons.ForPath(target) : ShellIcons.ForPath(shortcut),
                Pinned = true,
            };
            byKey[key] = app;
            apps.Add(app);
        }

        foreach (var window in Visible())
        {
            var exe = ProcessPath(window);
            var title = Title(window);
            var key = exe is null || exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase) ? "window:" + title : exe;
            if (!byKey.TryGetValue(key, out var app))
            {
                app = new DockApp
                {
                    Key = key,
                    Name = title,
                    Icon = ShellIcons.ForWindow(window) ?? (exe is null ? null : ShellIcons.ForPath(exe)),
                };
                byKey[key] = app;
                apps.Add(app);
            }

            app.Windows.Add(window);
        }

        return apps;
    }

    public static void Activate(DockApp app)
    {
        if (app.Windows.Count == 0)
        {
            if (app.Shortcut is not null)
            {
                ShellLauncher.Open(app.Shortcut);
            }

            return;
        }

        var foreground = NativeMethods.GetForegroundWindow();
        var index = app.Windows.IndexOf(foreground);
        if (index >= 0 && app.Windows.Count == 1 && !NativeMethods.IsIconic(foreground))
        {
            NativeMethods.ShowWindow(foreground, NativeMethods.SW_MINIMIZE);
            return;
        }

        var target = app.Windows[(index + 1) % app.Windows.Count];
        if (NativeMethods.IsIconic(target))
        {
            NativeMethods.ShowWindow(target, NativeMethods.SW_RESTORE);
        }

        NativeMethods.SetForegroundWindow(target);
    }

    public static void Close(DockApp app)
    {
        foreach (var window in app.Windows)
        {
            NativeMethods.PostMessage(window, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static IEnumerable<string> PinnedShortcuts()
    {
        try
        {
            return Directory.Exists(PinnedFolder) ? Directory.GetFiles(PinnedFolder, "*.lnk").OrderBy(f => File.GetCreationTimeUtc(f)) : [];
        }
        catch (Exception ex)
        {
            Log.Warn("Reading pinned taskbar apps failed", ex);
            return [];
        }
    }

    private static string? ResolveShortcut(string shortcut)
    {
        if (ShortcutTargets.TryGetValue(shortcut, out var cached))
        {
            return cached;
        }

        string? target = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is not null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                target = (string)shell.CreateShortcut(shortcut).TargetPath;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Resolving a pinned shortcut failed", ex);
        }

        ShortcutTargets[shortcut] = string.IsNullOrWhiteSpace(target) ? null : target;
        return ShortcutTargets[shortcut];
    }

    private static List<IntPtr> Visible()
    {
        var own = (uint)Environment.ProcessId;
        var list = new List<IntPtr>();
        NativeMethods.EnumWindows(
            (hwnd, _) =>
            {
                if (IsAppWindow(hwnd, own))
                {
                    list.Add(hwnd);
                }

                return true;
            },
            IntPtr.Zero);
        return list;
    }

    private static bool IsAppWindow(IntPtr hwnd, uint own)
    {
        if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.GetWindowTextLength(hwnd) == 0)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == own)
        {
            return false;
        }

        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        var owned = NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER) != IntPtr.Zero;
        if ((style & NativeMethods.WS_EX_TOOLWINDOW) != 0 || (owned && (style & NativeMethods.WS_EX_APPWINDOW) == 0))
        {
            return false;
        }

        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return false;
        }

        return !IgnoredClasses.Contains(NativeMethods.ReadClassName(hwnd));
    }

    private static string Title(IntPtr hwnd)
    {
        var buffer = new StringBuilder(NativeMethods.GetWindowTextLength(hwnd) + 1);
        NativeMethods.GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string? ProcessPath(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        var process = NativeMethods.OpenProcess(0x1000, false, pid);
        if (process == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var size = (uint)buffer.Capacity;
            return NativeMethods.QueryFullProcessImageName(process, 0, buffer, ref size) ? buffer.ToString() : null;
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }
}
