using System.Windows;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Island;
using PixelMe.Localization;
using PixelMe.Native;
using PixelMe.Settings;
using PixelMe.Theming;

namespace PixelMe.Shell;

public sealed class SystemIntegration : IDisposable
{
    private readonly AppHost host;
    private readonly IslandWindow island;
    private readonly TrayIcon tray;
    private FullscreenWatcher? fullscreen;
    private SettingsWindow? settingsWindow;
    private bool fullscreenActive;

    public SystemIntegration(AppHost host, IslandWindow island)
    {
        this.host = host;
        this.island = island;
        tray = new TrayIcon();
        tray.SettingsRequested += OpenSettings;
        tray.AutostartToggled += enabled => host.Settings.Update(s => s.StartWithWindows = enabled);
        tray.ExitRequested += () => Application.Current.Dispatcher.InvokeAsync(Application.Current.Shutdown);
        island.SettingsRequested += OpenSettings;
        host.ActivationRequested += OpenSettings;
        host.Settings.Changed += OnSettingsChanged;
    }

    public void Start()
    {
        var current = host.Settings.Current;
        Autostart.Apply(current.StartWithWindows);
        tray.Show(current);

        try
        {
            fullscreen = new FullscreenWatcher(island.Handle);
            fullscreen.Changed += active =>
            {
                fullscreenActive = active;
                UpdateIslandVisibility();
            };
            fullscreen.Start();
        }
        catch (Exception ex)
        {
            Log.Error("Fullscreen detection could not start", ex);
        }
    }

    public void Dispose()
    {
        host.Settings.Changed -= OnSettingsChanged;
        host.ActivationRequested -= OpenSettings;
        fullscreen?.Dispose();
        settingsWindow?.Close();
        tray.Dispose();
    }

    private void UpdateIslandVisibility()
    {
        island.SetSuppressed(fullscreenActive && host.Settings.Current.HideInFullscreen);
    }

    private void OnSettingsChanged(AppSettings previous, AppSettings current)
    {
        if (previous.Theme != current.Theme)
        {
            island.ApplyPalette(ThemePalette.For(current.Theme));
        }

        if (previous.Style != current.Style)
        {
            island.ApplyStyle(current.Style, animate: true);
        }

        if (previous.StartWithWindows != current.StartWithWindows)
        {
            Autostart.Apply(current.StartWithWindows);
            tray.SetAutostart(current.StartWithWindows);
        }

        if (previous.HideInFullscreen != current.HideInFullscreen)
        {
            UpdateIslandVisibility();
        }

        if (previous.Language != current.Language)
        {
            Application.Current.Dispatcher.InvokeAsync(
                () =>
                {
                    Strings.Use(host.Settings.Current.Language);
                    tray.Rebuild(host.Settings.Current);
                    island.RefreshText();
                    settingsWindow?.ApplyText();
                },
                DispatcherPriority.Background);
        }
    }

    private void OpenSettings()
    {
        if (settingsWindow is not null)
        {
            if (settingsWindow.WindowState == WindowState.Minimized)
            {
                settingsWindow.WindowState = WindowState.Normal;
            }

            settingsWindow.Activate();
            return;
        }

        settingsWindow = new SettingsWindow(host.Settings);
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
        settingsWindow.Activate();
    }
}
