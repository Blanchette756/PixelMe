using PixelMe.Infrastructure;

namespace PixelMe.Modules;

public sealed record ModuleLoadResult(IIslandModule Module, Exception? Error);

public static class ModuleLoader
{
    private static readonly TimeSpan InitializeTimeout = TimeSpan.FromSeconds(10);

    public static async Task<IReadOnlyList<ModuleLoadResult>> InitializeAsync(IEnumerable<IIslandModule> modules)
    {
        var pending = modules.Select(InitializeOneAsync).ToArray();
        return await Task.WhenAll(pending);
    }

    public static IReadOnlyList<IIslandModule> KeepHealthy(IReadOnlyList<ModuleLoadResult> results)
    {
        var healthy = new List<IIslandModule>();
        foreach (var result in results)
        {
            if (result.Error is null)
            {
                healthy.Add(result.Module);
                continue;
            }

            Log.Error($"Module '{result.Module.Id}' failed to start and was disabled", result.Error);
            SafeDispose(result.Module);
        }

        return healthy;
    }

    public static void SafeDispose(IIslandModule module)
    {
        try
        {
            module.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warn($"Module '{module.Id}' failed to shut down cleanly", ex);
        }
    }

    private static async Task<ModuleLoadResult> InitializeOneAsync(IIslandModule module)
    {
        using var timeout = new CancellationTokenSource(InitializeTimeout);
        try
        {
            await module.InitializeAsync(timeout.Token).WaitAsync(timeout.Token);
            return new ModuleLoadResult(module, null);
        }
        catch (Exception ex)
        {
            return new ModuleLoadResult(module, ex);
        }
    }
}
