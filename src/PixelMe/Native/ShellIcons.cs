using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PixelMe.Native;

public static class ShellIcons
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? ForPath(string path)
    {
        var extension = Directory.Exists(path) ? "<folder>" : Path.GetExtension(path);
        var key = extension is ".exe" or ".lnk" or ".ico" or "" ? path : extension;
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        ImageSource? image = null;
        var info = new NativeMethods.SHFILEINFO();
        var result = NativeMethods.SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>(), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);
        if (result != IntPtr.Zero && info.hIcon != IntPtr.Zero)
        {
            try
            {
                image = FromHandle(info.hIcon);
            }
            finally
            {
                NativeMethods.DestroyIcon(info.hIcon);
            }
        }

        Cache[key] = image;
        return image;
    }

    public static ImageSource? ForWindow(IntPtr hwnd)
    {
        NativeMethods.SendMessageTimeout(hwnd, NativeMethods.WM_GETICON, new IntPtr(1), IntPtr.Zero, 2, 80, out var icon);
        if (icon == IntPtr.Zero)
        {
            icon = NativeMethods.GetClassLongPtr(hwnd, -14);
        }

        return icon == IntPtr.Zero ? null : FromHandle(icon);
    }

    private static ImageSource? FromHandle(IntPtr icon)
    {
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
