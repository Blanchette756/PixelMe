using PixelMe.Modules.Clock;

namespace PixelMe.Modules;

public static class ModuleCatalog
{
    public static IReadOnlyList<IIslandModule> CreateAll() =>
    [
        new ClockModule(),
    ];
}
