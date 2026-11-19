using PixelMe.Modules.Clock;
using PixelMe.Modules.Media;

namespace PixelMe.Modules;

public static class ModuleCatalog
{
    public static IReadOnlyList<IIslandModule> CreateAll() =>
    [
        new MediaModule(),
        new ClockModule(),
    ];
}
