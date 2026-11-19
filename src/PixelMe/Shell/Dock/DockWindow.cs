using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Localization;
using PixelMe.Modules;
using PixelMe.Native;
using PixelMe.Theming;

namespace PixelMe.Shell.Dock;

public sealed class DockWindow : Window
{
    private const double BarHeight = 62;
    private const byte WinKey = 0x5B;

    private readonly Border pill;
    private readonly StackPanel apps = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock time;
    private readonly TextBlock date;
    private readonly DispatcherTimer clock = new(DispatcherPriority.Background);
    private readonly DispatcherTimer refresh = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly NativeMethods.WinEventProc showProc;
    private GlassBackdrop? backdrop;
    private IntPtr handle;
    private IntPtr showHook;
    private uint callbackMessage;
    private uint shellMessage;
    private uint taskbarCreated;

    public DockWindow()
    {
        Title = "PixelMe Dock";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Width = 800;
        Height = BarHeight;
        UseLayoutRounding = true;
        SetResourceReference(FontFamilyProperty, "Island.Font");
        SetResourceReference(TextElement.ForegroundProperty, "Island.Foreground");
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);

        time = Ui.Text(string.Empty, 12.5, FontWeights.SemiBold);
        date = Ui.Text(string.Empty, 11, null, "Island.Secondary");
        time.HorizontalAlignment = HorizontalAlignment.Right;
        date.HorizontalAlignment = HorizontalAlignment.Right;

        var start = DockButton(StartContent(), Strings.StartMenu, () => NativeMethods.PressKeys(WinKey));
        var tray = DockButton(Ui.Row(8, Ui.Glyph("", 14), Ui.Glyph("", 14), Ui.Glyph("", 14)), Strings.QuickSettingsTip, () => NativeMethods.PressKeys(WinKey, 0x41));
        tray.Padding = new Thickness(12, 0, 12, 0);
        var clockButton = DockButton(Ui.Column(0, time, date), Strings.NotificationCenterTip, () => NativeMethods.PressKeys(WinKey, 0x4E));
        clockButton.Padding = new Thickness(10, 0, 10, 0);
        var desktop = DockButton(new Border { Width = 3, Height = 22, CornerRadius = new CornerRadius(1.5), Background = Brushes.Gray, Opacity = 0.5 }, Strings.ShowDesktop, () => NativeMethods.PressKeys(WinKey, 0x44));
        desktop.Width = 18;

        pill = new Border
        {
            CornerRadius = new CornerRadius(20),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 4, 6, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = Ui.Row(4, start, Divider(), apps, Divider(), tray, clockButton, desktop),
        };
        pill.SetResourceReference(Border.BackgroundProperty, "Island.Background");
        pill.SetResourceReference(Border.BorderBrushProperty, "Island.Border");
        pill.SizeChanged += (_, _) => SyncBackdrop();
        Content = new Grid { Children = { pill } };

        clock.Tick += (_, _) => UpdateClock();
        refresh.Tick += (_, _) =>
        {
            refresh.Stop();
            RefreshApps();
        };
        showProc = OnTaskbarShown;
        IsVisibleChanged += (_, _) => backdrop?.SetVisible(IsVisible);
    }

    public void Open(ThemePalette palette)
    {
        try
        {
            backdrop = new GlassBackdrop();
        }
        catch (Exception ex)
        {
            Log.Warn("Dock backdrop is unavailable", ex);
        }

        var helper = new WindowInteropHelper(this);
        if (backdrop is not null)
        {
            helper.Owner = backdrop.Handle;
        }

        handle = helper.EnsureHandle();
        WindowStyler.MakeOverlay(handle);
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        ApplyPalette(palette);

        callbackMessage = NativeMethods.RegisterWindowMessage("PixelMe.DockNotify");
        shellMessage = NativeMethods.RegisterWindowMessage("SHELLHOOK");
        taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");
        NativeMethods.RegisterShellHookWindow(handle);

        RegisterAppBar();
        TaskbarSwitch.Hide();
        PositionAppBar();
        HookTaskbar();
        RefreshApps();
        UpdateClock();
        Show();
    }

    public void ApplyPalette(ThemePalette palette)
    {
        backdrop?.SetEnabled(palette.UsesBackdrop, palette.BackdropTint);
        SyncBackdrop();
    }

    public void SetFullscreen(bool active)
    {
        if (active && IsVisible)
        {
            Hide();
        }
        else if (!active && !IsVisible && handle != IntPtr.Zero)
        {
            Show();
            WindowStyler.BringToTop(handle);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        clock.Stop();
        refresh.Stop();
        if (showHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(showHook);
        }

        if (handle != IntPtr.Zero)
        {
            NativeMethods.DeregisterShellHookWindow(handle);
            var data = AppBarData();
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref data);
        }

        base.OnClosed(e);
        backdrop?.Dispose();
    }

    private static Border Divider()
    {
        var line = new Border { Width = 1.5, Height = 24, CornerRadius = new CornerRadius(1), Margin = new Thickness(4, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        line.SetResourceReference(Border.BackgroundProperty, "Island.Subtle");
        return line;
    }

    private static UIElement StartContent()
    {
        try
        {
            var decoder = BitmapDecoder.Create(new Uri("pack://application:,,,/Assets/PixelMe.ico"), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return new Image { Source = decoder.Frames.OrderByDescending(f => f.PixelWidth).First(), Width = 26, Height = 26 };
        }
        catch (Exception)
        {
            return Ui.Glyph("", 18);
        }
    }

    private static Button DockButton(UIElement content, string tooltip, Action click)
    {
        var button = new Button { Content = content, ToolTip = tooltip, Height = 46, MinWidth = 44, Padding = new Thickness(0) };
        button.SetResourceReference(StyleProperty, "Island.RowButton");
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.Click += (_, _) => click();
        return button;
    }

    private Button AppButton(DockApp app, bool active)
    {
        var image = new Image { Source = app.Icon, Width = 26, Height = 26, Margin = new Thickness(0, 0, 0, 4) };
        var indicator = new Border
        {
            Height = 3,
            Width = active ? 14 : 5,
            CornerRadius = new CornerRadius(1.5),
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = app.Windows.Count > 0 ? Visibility.Visible : Visibility.Hidden,
            Margin = new Thickness(0, 0, 0, 2),
        };
        indicator.SetResourceReference(Border.BackgroundProperty, active ? "Island.Accent" : "Island.Secondary");
        var grid = new Grid { Width = 40, Height = 44, Background = Brushes.Transparent };
        grid.Children.Add(image);
        grid.Children.Add(indicator);

        var button = DockButton(grid, app.Name, () =>
        {
            AppWindows.Activate(app);
            refresh.Stop();
            refresh.Start();
        });

        var menu = new ContextMenu();
        if (app.Shortcut is not null)
        {
            var open = new MenuItem { Header = Strings.NewWindow };
            open.Click += (_, _) => ShellLauncher.Open(app.Shortcut);
            menu.Items.Add(open);
        }

        if (app.Windows.Count > 0)
        {
            var close = new MenuItem { Header = Strings.CloseWindow };
            close.Click += (_, _) => AppWindows.Close(app);
            menu.Items.Add(close);
        }

        button.ContextMenu = menu.Items.Count > 0 ? menu : null;
        return button;
    }

    private void RefreshApps()
    {
        List<DockApp> list;
        try
        {
            list = AppWindows.Collect();
        }
        catch (Exception ex)
        {
            Log.Warn("Listing open apps failed", ex);
            return;
        }

        var foreground = NativeMethods.GetForegroundWindow();
        apps.Children.Clear();
        foreach (var app in list)
        {
            apps.Children.Add(AppButton(app, app.Windows.Contains(foreground)));
        }
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        time.Text = now.ToString(System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern);
        date.Text = now.ToString(System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern);
        clock.Stop();
        clock.Interval = TimeSpan.FromSeconds(60 - now.Second) + TimeSpan.FromMilliseconds(50);
        clock.Start();
    }

    private NativeMethods.APPBARDATA AppBarData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
        hWnd = handle,
        uCallbackMessage = callbackMessage,
    };

    private void RegisterAppBar()
    {
        var data = AppBarData();
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref data);
        PositionAppBar();
    }

    private void PositionAppBar()
    {
        if (handle == IntPtr.Zero || !NativeMethods.TryGetPrimaryMonitorBounds(out var screen))
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var height = (int)Math.Round(BarHeight * dpi.DpiScaleY);
        var data = AppBarData();
        data.uEdge = NativeMethods.ABE_BOTTOM;
        data.rc = new NativeMethods.RECT { Left = screen.Left, Right = screen.Right, Top = screen.Bottom - height, Bottom = screen.Bottom };
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_QUERYPOS, ref data);
        data.rc.Top = data.rc.Bottom - height;
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETPOS, ref data);
        NativeMethods.SetWindowPos(handle, NativeMethods.HWND_TOPMOST, data.rc.Left, data.rc.Top, data.rc.Width, data.rc.Height, NativeMethods.SWP_NOACTIVATE);
        Width = data.rc.Width / dpi.DpiScaleX;
        Height = BarHeight;
        SyncBackdrop();
    }

    private void HookTaskbar()
    {
        if (showHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(showHook);
            showHook = IntPtr.Zero;
        }

        var tray = TaskbarSwitch.PrimaryTray;
        if (tray == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.GetWindowThreadProcessId(tray, out var explorer);
        showHook = NativeMethods.SetWinEventHook(NativeMethods.EVENT_OBJECT_SHOW, NativeMethods.EVENT_OBJECT_SHOW, IntPtr.Zero, showProc, explorer, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    private void OnTaskbarShown(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject == 0 && idChild == 0)
        {
            TaskbarSwitch.HideAgain(hwnd);
        }
    }

    private void SyncBackdrop()
    {
        if (backdrop is null || !backdrop.IsEnabled || handle == IntPtr.Zero || pill.ActualWidth <= 0 || !NativeMethods.GetWindowRect(handle, out var window))
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var origin = pill.TranslatePoint(new Point(0, 0), this);
        var shape = new RectangleGeometry(new Rect(1, 1, pill.ActualWidth - 2, pill.ActualHeight - 2), 19, 19);
        backdrop.Update(
            window.Left + (int)Math.Round(origin.X * dpi.DpiScaleX),
            window.Top + (int)Math.Round(origin.Y * dpi.DpiScaleY),
            (int)Math.Ceiling(pill.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(pill.ActualHeight * dpi.DpiScaleY),
            shape,
            dpi.DpiScaleX,
            dpi.DpiScaleY);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var message = (uint)msg;
        if (message == callbackMessage && wParam.ToInt64() == NativeMethods.ABN_POSCHANGED)
        {
            PositionAppBar();
        }
        else if (message == shellMessage)
        {
            refresh.Stop();
            refresh.Start();
        }
        else if (message == taskbarCreated)
        {
            RegisterAppBar();
            TaskbarSwitch.Hide();
            PositionAppBar();
            HookTaskbar();
        }
        else if (msg == NativeMethods.WM_DISPLAYCHANGE)
        {
            Dispatcher.InvokeAsync(PositionAppBar, DispatcherPriority.Background);
        }

        return IntPtr.Zero;
    }
}
