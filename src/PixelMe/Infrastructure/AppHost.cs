using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using PixelMe.Native;
using PixelMe.Settings;

namespace PixelMe.Infrastructure;

public sealed class AppHost : IDisposable
{
    private static readonly TimeSpan ShellWaitLimit = TimeSpan.FromSeconds(45);

    private readonly Stopwatch startupClock = new();
    private SingleInstance? instance;

    public AppHost(SettingsStore settings) => Settings = settings;

    public event Action? ActivationRequested;

    public SettingsStore Settings { get; }

    public bool IsPrimary => instance?.IsPrimary == true;

    public bool Start(Application app, string[] args)
    {
        if (Modules.Agents.AgentCommand.TryRun(args))
        {
            return false;
        }

        if (args.Contains(Shell.Dock.TaskbarSwitch.RestoreArgument, StringComparer.OrdinalIgnoreCase))
        {
            Shell.Dock.TaskbarSwitch.Restore();
            return false;
        }

        startupClock.Start();
        instance = new SingleInstance();
        if (!instance.IsPrimary)
        {
            instance.NotifyPrimary();
            return false;
        }

        app.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var dispatcher = app.Dispatcher;
        instance.ListenForActivation(() => dispatcher.InvokeAsync(() => ActivationRequested?.Invoke()));

        Log.Info($"PixelMe {AppInfo.Version} starting on {Environment.OSVersion.VersionString}");
        return true;
    }

    public async Task WaitUntilShellReadyAsync()
    {
        while (NativeMethods.FindWindow("Shell_TrayWnd", null) == IntPtr.Zero && startupClock.Elapsed < ShellWaitLimit)
        {
            await Task.Delay(250);
        }
    }

    public void FinishStartup()
    {
        startupClock.Stop();
        Log.Info($"Startup finished in {startupClock.ElapsedMilliseconds} ms");
    }

    public void Dispose()
    {
        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        instance?.Dispose();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled UI exception", e.Exception);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error("Fatal exception", e.ExceptionObject as Exception);
        try
        {
            Shell.Dock.TaskbarSwitch.RestoreIfLeftHidden();
        }
        catch
        {
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error("Unobserved task exception", e.Exception);
        e.SetObserved();
    }
}
