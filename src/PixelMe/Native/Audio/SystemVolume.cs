using System.Runtime.InteropServices;
using System.Windows.Threading;
using PixelMe.Infrastructure;

namespace PixelMe.Native.Audio;

public sealed class SystemVolume(int flow = SystemVolume.RenderFlow, int role = SystemVolume.ConsoleRole) : IDisposable
{
    public const int RenderFlow = 0;
    public const int CaptureFlow = 1;
    public const int ConsoleRole = 0;
    public const int CommunicationsRole = 2;
    private const int AllContexts = 23;

    private static readonly Guid OwnContext = Guid.NewGuid();

    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private IMMDeviceEnumerator? enumerator;
    private IAudioEndpointVolume? endpoint;
    private Callback? callback;
    private bool reportedFailure;

    public event Action<float, bool>? Changed;

    public void Refresh()
    {
        Release();
        try
        {
            enumerator ??= (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(flow, role, out var device));
            try
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, AllContexts, IntPtr.Zero, out var instance));
                endpoint = (IAudioEndpointVolume)instance;
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }

            callback = new Callback(this);
            endpoint.RegisterControlChangeNotify(callback);
            reportedFailure = false;
            Publish();
        }
        catch (Exception ex)
        {
            if (!reportedFailure)
            {
                Log.Warn("System volume is unavailable", ex);
                reportedFailure = true;
            }

            Release();
        }
    }

    public void SetLevel(float level)
    {
        if (endpoint is null)
        {
            return;
        }

        var context = OwnContext;
        endpoint.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref context);
        if (level > 0 && endpoint.GetMute(out var muted) == 0 && muted)
        {
            endpoint.SetMute(false, ref context);
            Publish();
        }
    }

    public bool IsAvailable => endpoint is not null;

    public bool? IsMuted => endpoint is not null && endpoint.GetMute(out var muted) == 0 ? muted : null;

    public void SetMuted(bool muted)
    {
        if (endpoint is null)
        {
            return;
        }

        var context = OwnContext;
        endpoint.SetMute(muted, ref context);
        Publish();
    }

    public void ToggleMute()
    {
        if (endpoint is null || endpoint.GetMute(out var muted) != 0)
        {
            return;
        }

        var context = OwnContext;
        endpoint.SetMute(!muted, ref context);
        Publish();
    }

    public void Dispose()
    {
        Release();
        if (enumerator is not null)
        {
            Marshal.ReleaseComObject(enumerator);
            enumerator = null;
        }
    }

    private void Publish()
    {
        if (endpoint is not null
            && endpoint.GetMasterVolumeLevelScalar(out var level) == 0
            && endpoint.GetMute(out var muted) == 0)
        {
            Changed?.Invoke(level, muted);
        }
    }

    private void Release()
    {
        if (endpoint is not null)
        {
            if (callback is not null)
            {
                endpoint.UnregisterControlChangeNotify(callback);
            }

            Marshal.ReleaseComObject(endpoint);
        }

        endpoint = null;
        callback = null;
    }

    private void OnNotify(IntPtr data)
    {
        if (data == IntPtr.Zero)
        {
            return;
        }

        var context = Marshal.PtrToStructure<Guid>(data);
        if (context == OwnContext)
        {
            return;
        }

        var muted = Marshal.ReadInt32(data, 16) != 0;
        var level = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(data, 20));
        dispatcher.InvokeAsync(() => Changed?.Invoke(level, muted));
    }

    private sealed class Callback(SystemVolume owner) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(IntPtr notifyData)
        {
            owner.OnNotify(notifyData);
            return 0;
        }
    }
}
