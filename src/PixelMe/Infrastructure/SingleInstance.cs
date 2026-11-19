using PixelMe.Native;

namespace PixelMe.Infrastructure;

public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\PixelMe.SingleInstance";
    private const string SignalName = @"Local\PixelMe.Activate";

    private readonly Mutex mutex;
    private readonly EventWaitHandle signal;
    private RegisteredWaitHandle? registration;

    public SingleInstance()
    {
        mutex = new Mutex(true, MutexName, out var createdNew);
        IsPrimary = createdNew;
        signal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName);
    }

    public bool IsPrimary { get; }

    public void NotifyPrimary()
    {
        try
        {
            NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
            signal.Set();
        }
        catch (Exception ex)
        {
            Log.Warn("Could not reach the running instance", ex);
        }
    }

    public void ListenForActivation(Action onActivate)
    {
        registration = ThreadPool.RegisterWaitForSingleObject(
            signal,
            (_, _) => onActivate(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        registration?.Unregister(null);
        signal.Dispose();
        if (IsPrimary)
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        mutex.Dispose();
    }
}
