using System.Windows.Interop;

namespace PixelMe.Native;

public sealed class MessageWindow : IDisposable
{
    private readonly HwndSource source;

    public MessageWindow()
    {
        var parameters = new HwndSourceParameters("PixelMe Messages")
        {
            ParentWindow = new IntPtr(-3),
            WindowStyle = 0,
            Width = 0,
            Height = 0,
        };
        source = new HwndSource(parameters);
        source.AddHook(Hook);
    }

    public event Action<int, IntPtr, IntPtr>? MessageReceived;

    public IntPtr Handle => source.Handle;

    public void Dispose()
    {
        source.RemoveHook(Hook);
        source.Dispose();
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        MessageReceived?.Invoke(msg, wParam, lParam);
        return IntPtr.Zero;
    }
}
