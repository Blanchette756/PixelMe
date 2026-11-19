using System.Windows;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Island;
using PixelMe.Localization;
using PixelMe.Modules;
using PixelMe.Native;
using PixelMe.Settings;
using PixelMe.Theming;

namespace PixelMe.Shell;

public sealed class SystemIntegration : IDisposable
{
    private readonly AppHost host;
    private readonly IslandWindow island;
    private readonly ModuleServices services;
    private readonly TrayIcon tray;
    private readonly DispatcherTimer stripTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private FullscreenWatcher? fullscreen;
    private SettingsWindow? settingsWindow;
    private bool fullscreenActive;

    public SystemIntegration(AppHost host, IslandWindow island, ModuleServices services)
    {
        this.host = host;
        this.island = island;
        this.services = services;
        tray = new TrayIcon();
        tray.SettingsRequested += OpenSettings;
        tray.AutostartToggled += enabled => host.Settings.Update(s => s.StartWithWindows = enabled);
        tray.ExitRequested += () => Application.Current.Dispatcher.InvokeAsync(Application.Current.Shutdown);
        island.SettingsRequested += OpenSettings;
        host.ActivationRequested += OpenSettings;
        host.Settings.Changed += OnSettingsChanged;
        stripTimer.Tick += (_, _) => UpdateStrip();
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
                UpdatePresence();
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
        stripTimer.Stop();
        host.Settings.Changed -= OnSettingsChanged;
        host.ActivationRequested -= OpenSettings;
        fullscreen?.Dispose();
        settingsWindow?.Close();
        tray.Dispose();
    }

    private void UpdatePresence()
    {
        var mode = host.Settings.Current.Fullscreen;
        var presence = !fullscreenActive || mode == FullscreenBehavior.Stay
            ? IslandPresence.Normal
            : mode == FullscreenBehavior.Hide ? IslandPresence.Hidden : IslandPresence.Strip;

        if (presence == IslandPresence.Strip)
        {
            UpdateStrip();
            stripTimer.Start();
        }
        else
        {
            stripTimer.Stop();
        }

        island.SetPresence(presence);
    }

    private void UpdateStrip()
    {
        var stats = services.Stats.Sample();
        var gpu = stats.Gpu is { } value ? $"GPU {value,3:0}%   " : string.Empty;
        island.SetStripText($"CPU {stats.Cpu,3:0}%   {gpu}RAM {stats.Ram,3:0}%   {DateTime.Now:HH:mm}");
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

        if (previous.Fullscreen != current.Fullscreen)
        {
            UpdatePresence();
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
