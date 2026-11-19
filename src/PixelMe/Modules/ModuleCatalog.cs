using PixelMe.Modules.Agents;
using PixelMe.Modules.Call;
using PixelMe.Modules.Clipboard;
using PixelMe.Modules.Clock;
using PixelMe.Modules.Controls;
using PixelMe.Modules.Media;
using PixelMe.Modules.Notifications;
using PixelMe.Modules.Reminders;
using PixelMe.Modules.Shelf;
using PixelMe.Modules.Stream;
using PixelMe.Modules.Web;
using PixelMe.Modules.Widgets;

namespace PixelMe.Modules;

public static class ModuleCatalog
{
    public static IReadOnlyList<IIslandModule> CreateAll(ModuleServices services) =>
    [
        new MediaModule(services),
        new ClipboardModule(services),
        new ShelfModule(),
        new RemindersModule(),
        new CallModule(services),
        new ControlsModule(),
        new WidgetsModule(services),
        new NotificationsModule(services),
        new AgentsModule(),
        new WebModule(services),
        new StreamModule(services),
        new ClockModule(),
    ];
}
