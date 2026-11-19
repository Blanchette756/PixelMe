using System.Windows;
using PixelMe.Infrastructure;
using PixelMe.Island;
using PixelMe.Localization;
using PixelMe.Modules;
using PixelMe.Settings;
using PixelMe.Shell;
using PixelMe.Theming;

namespace PixelMe;

public partial class App : Application
{
    private AppHost? host;
    private IslandWindow? island;
    private SystemIntegration? integration;
    private IReadOnlyList<IIslandModule> modules = [];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = SettingsStore.Load();
        host = new AppHost(settings);
        if (!host.Start(this))
        {
            Shutdown();
            return;
        }

        try
        {
            await host.WaitUntilShellReadyAsync();

            var initialization = ModuleLoader.InitializeAsync(ModuleCatalog.CreateAll());

            Strings.Use(settings.Current.Language);
            var palette = ThemePalette.For(settings.Current.Theme);

            island = new IslandWindow(settings);
            island.PrepareSurface(palette);

            var results = await initialization;
            host.FinishStartup();
            modules = ModuleLoader.KeepHealthy(results);

            island.Mount(modules);
            island.EnableInteraction();

            integration = new SystemIntegration(host, island);

            island.Show();
            integration.Start();
        }
        catch (Exception ex)
        {
            Log.Error("Startup failed", ex);
            MessageBox.Show(Strings.StartupFailed, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        integration?.Dispose();
        island?.Close();
        foreach (var module in modules)
        {
            ModuleLoader.SafeDispose(module);
        }
        host?.Dispose();
        Log.Info("PixelMe stopped");
        base.OnExit(e);
    }
}
