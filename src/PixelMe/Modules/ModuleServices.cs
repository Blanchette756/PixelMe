using PixelMe.Native;
using PixelMe.Settings;
using PixelMe.Shell;

namespace PixelMe.Modules;

public sealed class ModuleServices : IDisposable
{
    public ModuleServices(SettingsStore settings)
    {
        Settings = settings;
        Messages = new MessageWindow();
        Hotkeys = new HotkeyManager(Messages);
        Stats = new SystemStats();
        Web = new WebDrawer();
    }

    public SettingsStore Settings { get; }

    public MessageWindow Messages { get; }

    public HotkeyManager Hotkeys { get; }

    public SystemStats Stats { get; }

    public WebDrawer Web { get; }

    public void Dispose()
    {
        Web.Dispose();
        Hotkeys.Dispose();
        Stats.Dispose();
        Messages.Dispose();
    }
}
