using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using PixelMe.Infrastructure;

namespace PixelMe.Native;

internal sealed class GlassBackdrop : IDisposable
{
    private readonly HwndSource source;
    private bool enabled;
    private bool visible;
    private bool shown;

    public GlassBackdrop()
    {
        var parameters = new HwndSourceParameters("PixelMe Backdrop")
        {
            WindowStyle = unchecked((int)NativeMethods.WS_POPUP),
            ExtendedWindowStyle = (int)(NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOPMOST),
            PositionX = 0,
            PositionY = 0,
            Width = 1,
            Height = 1,
        };

        source = new HwndSource(parameters) { RootVisual = new Grid { Background = Brushes.Transparent } };
        if (source.CompositionTarget is not null)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        var on = 1;
        var square = NativeMethods.DWMWCP_DONOTROUND;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_TRANSITIONS_FORCEDISABLED, ref on, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_EXCLUDED_FROM_PEEK, ref on, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref square, sizeof(int));

        if (!NativeMethods.EnableBlurBehind(Handle))
        {
            Log.Warn("Blur behind the island is not supported on this system");
        }
    }

    public IntPtr Handle => source.Handle;

    public bool IsEnabled => enabled;

    public void SetEnabled(bool value, uint tint = 0)
    {
        enabled = value;
        if (value && tint != 0 && !NativeMethods.EnableAcrylic(Handle, tint))
        {
            NativeMethods.EnableBlurBehind(Handle);
        }

        ApplyVisibility();
    }

    public void SetVisible(bool value)
    {
        visible = value;
        ApplyVisibility();
    }

    public void Update(int x, int y, int width, int height, Geometry shape, double scaleX, double scaleY)
    {
        NativeMethods.SetWindowPos(
            Handle,
            IntPtr.Zero,
            x,
            y,
            Math.Max(1, width),
            Math.Max(1, height),
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER);

        var region = RegionBuilder.FromGeometry(shape, scaleX, scaleY);
        if (region != IntPtr.Zero && NativeMethods.SetWindowRgn(Handle, region, true) == 0)
        {
            NativeMethods.DeleteObject(region);
        }
    }

    public void Dispose()
    {
        source.Dispose();
    }

    private void ApplyVisibility()
    {
        var show = enabled && visible;
        if (show == shown)
        {
            return;
        }

        shown = show;
        NativeMethods.ShowWindow(Handle, show ? NativeMethods.SW_SHOWNOACTIVATE : NativeMethods.SW_HIDE);
    }
}
