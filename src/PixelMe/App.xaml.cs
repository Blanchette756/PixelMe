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
    private ModuleServices? services;
    private IslandWindow? island;
    private SystemIntegration? integration;
    private IReadOnlyList<IIslandModule> modules = [];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = SettingsStore.Load();
        host = new AppHost(settings);
        if (!host.Start(this, e.Args))
        {
            Shutdown();
            return;
        }

        try
        {
            await host.WaitUntilShellReadyAsync();

            services = new ModuleServices(settings);
            var initialization = ModuleLoader.InitializeAsync(ModuleCatalog.CreateAll(services));

            Strings.Use(settings.Current.Language);
            var palette = ThemePalette.For(settings.Current.Theme);

            island = new IslandWindow(settings);
            island.PrepareSurface(palette);

            var results = await initialization;
            host.FinishStartup();
            modules = ModuleLoader.KeepHealthy(results);

            island.Mount(modules);
            island.EnableInteraction();

            integration = new SystemIntegration(host, island, services);

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

        services?.Dispose();
        var primary = host?.IsPrimary == true;
        host?.Dispose();
        if (primary)
        {
            Log.Info("PixelMe stopped");
        }

        base.OnExit(e);
    }
}
