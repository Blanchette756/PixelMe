using System.Windows;
using PixelMe.Infrastructure;
using PixelMe.Localization;
using PixelMe.Settings;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace PixelMe.Shell;

public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Drawing.Icon image;
    private Forms.ToolStripMenuItem? autostartItem;

    public TrayIcon()
    {
        image = LoadImage();
        icon = new Forms.NotifyIcon { Icon = image, Text = AppInfo.Name };
        icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                SettingsRequested?.Invoke();
            }
        };
    }

    public event Action? SettingsRequested;

    public event Action<bool>? AutostartToggled;

    public event Action? ExitRequested;

    public void Show(AppSettings settings)
    {
        Rebuild(settings);
        icon.Visible = true;
    }

    public void Rebuild(AppSettings settings)
    {
        var previous = icon.ContextMenuStrip;
        var menu = new Forms.ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };

        var open = new Forms.ToolStripMenuItem(Strings.Settings);
        open.Font = new Drawing.Font(open.Font, Drawing.FontStyle.Bold);
        open.Click += (_, _) => SettingsRequested?.Invoke();
        menu.Items.Add(open);

        var autostart = new Forms.ToolStripMenuItem(Strings.StartWithWindows) { Checked = settings.StartWithWindows };
        autostart.Click += (_, _) => AutostartToggled?.Invoke(!autostart.Checked);
        menu.Items.Add(autostart);
        autostartItem = autostart;

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Strings.OpenLogFolder, null, (_, _) => ShellLauncher.OpenFolder(AppPaths.LogFolder));
        menu.Items.Add(Strings.ViewOnGitHub, null, (_, _) => ShellLauncher.Open(AppInfo.RepositoryUrl));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Strings.Exit, null, (_, _) => ExitRequested?.Invoke());

        icon.ContextMenuStrip = menu;
        previous?.Dispose();
    }

    public void SetAutostart(bool enabled)
    {
        if (autostartItem is not null)
        {
            autostartItem.Checked = enabled;
        }
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.ContextMenuStrip?.Dispose();
        icon.Dispose();
        image.Dispose();
    }

    private static Drawing.Icon LoadImage()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/PixelMe.ico"));
            if (resource is not null)
            {
                using var stream = resource.Stream;
                return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Tray icon image could not be loaded", ex);
        }

        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }
}
