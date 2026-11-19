using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Localization;
using PixelMe.Modules;
using PixelMe.Native;
using PixelMe.Settings;
using PixelMe.Theming;

namespace PixelMe.Island;

public partial class IslandWindow : Window
{
    private readonly SettingsStore settings;
    private readonly DispatcherTimer expandTimer;
    private readonly DispatcherTimer collapseTimer;
    private readonly SpringAnimator animator;
    private readonly Dictionary<IIslandModule, RadioButton> tabs = [];
    private IReadOnlyList<IIslandModule> modules = [];
    private IIslandModule? compactModule;
    private GlassBackdrop? backdrop;
    private FrameLayout layout;
    private IslandStyle style;
    private double compactWidth = IslandMetrics.DefaultCompactWidth;
    private IntPtr handle;
    private int windowX;
    private int windowY;
    private bool expanded;
    private bool interactive;
    private bool suppressed;

    public IslandWindow(SettingsStore settings)
    {
        this.settings = settings;
        InitializeComponent();

        Width = IslandMetrics.WindowWidth;
        Height = IslandMetrics.WindowHeight;
        ExpandedHost.Width = IslandMetrics.Expanded.Width;
        ExpandedHost.Height = IslandMetrics.Expanded.Height;
        SettingsButton.Content = "";
        SettingsButton.Click += (_, _) => SettingsRequested?.Invoke();

        style = settings.Current.Style;
        SizeCompactHost();

        expandTimer = new DispatcherTimer(DispatcherPriority.Input);
        expandTimer.Tick += (_, _) =>
        {
            expandTimer.Stop();
            SetExpanded(true);
        };

        collapseTimer = new DispatcherTimer(DispatcherPriority.Input);
        collapseTimer.Tick += (_, _) =>
        {
            collapseTimer.Stop();
            if (!IslandRoot.IsMouseOver)
            {
                SetExpanded(false);
            }
        };

        animator = new SpringAnimator(TargetValues(), ApplyFrame);
        IsVisibleChanged += (_, _) => backdrop?.SetVisible(IsVisible);
    }

    public event Action? SettingsRequested;

    public IntPtr Handle => handle;

    public void PrepareSurface(ThemePalette palette)
    {
        try
        {
            backdrop = new GlassBackdrop();
        }
        catch (Exception ex)
        {
            Log.Warn("Glass backdrop is unavailable", ex);
            backdrop = null;
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
        ApplyStyle(style, animate: false);
    }

    public void ApplyPalette(ThemePalette palette)
    {
        palette.ApplyTo(Application.Current.Resources);
        Outline.Fill = palette.Background;
        Outline.Stroke = palette.Border;
        backdrop?.SetEnabled(palette.UsesBackdrop);
        SyncBackdrop();
    }

    public void ApplyStyle(IslandStyle next, bool animate)
    {
        style = next;
        SizeCompactHost();
        Retarget(animate);
        Reposition();
    }

    public void Mount(IReadOnlyList<IIslandModule> ready)
    {
        foreach (var module in modules)
        {
            module.ActivityChanged -= OnModuleActivityChanged;
        }

        TabStrip.Children.Clear();
        tabs.Clear();
        var mounted = new List<IIslandModule>();

        foreach (var module in ready.OrderBy(m => m.Priority))
        {
            if (TryBuildView(module, m => m.ExpandedView) is null)
            {
                continue;
            }

            mounted.Add(module);
            var tab = new RadioButton
            {
                Style = TryFindResource("Island.TabButton") as System.Windows.Style,
                Content = module.Glyph,
                GroupName = "IslandTabs",
                Tag = module,
            };
            tab.Checked += (_, _) => Select(module);
            TabStrip.Children.Add(tab);
            tabs[module] = tab;
            module.ActivityChanged += OnModuleActivityChanged;
        }

        modules = mounted;
        RefreshCompact(animate: false);
        SelectTab(compactModule ?? modules.FirstOrDefault());
        RefreshText();
    }

    public void EnableInteraction()
    {
        if (interactive)
        {
            return;
        }

        interactive = true;
        IslandRoot.MouseEnter += (_, _) =>
        {
            collapseTimer.Stop();
            if (!expanded)
            {
                Restart(expandTimer, settings.Current.ExpandDelayMs);
            }
        };

        IslandRoot.MouseLeave += (_, _) =>
        {
            expandTimer.Stop();
            if (expanded)
            {
                Restart(collapseTimer, settings.Current.CollapseDelayMs);
            }
        };

        IslandRoot.PreviewMouseLeftButtonDown += (_, _) =>
        {
            if (!expanded)
            {
                expandTimer.Stop();
                SetExpanded(true);
            }
        };
    }

    public void RefreshText()
    {
        SettingsButton.ToolTip = Strings.Settings;
        foreach (var (module, tab) in tabs)
        {
            tab.ToolTip = module.DisplayName;
            try
            {
                module.RefreshText();
            }
            catch (Exception ex)
            {
                Log.Warn($"Module '{module.Id}' failed to refresh its text", ex);
            }
        }
    }

    public void SetSuppressed(bool value)
    {
        if (suppressed == value)
        {
            return;
        }

        suppressed = value;
        if (suppressed)
        {
            expandTimer.Stop();
            collapseTimer.Stop();
            expanded = false;
            Retarget(animate: false);
            Hide();
        }
        else
        {
            Show();
            WindowStyler.BringToTop(handle);
            Reposition();
        }
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.InvokeAsync(Reposition, DispatcherPriority.Background);
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (var module in modules)
        {
            module.ActivityChanged -= OnModuleActivityChanged;
        }

        base.OnClosed(e);
        backdrop?.Dispose();
        backdrop = null;
    }

    private static void Restart(DispatcherTimer timer, int delayMs)
    {
        timer.Stop();
        timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, delayMs));
        timer.Start();
    }

    private static FrameworkElement? TryBuildView(IIslandModule module, Func<IIslandModule, FrameworkElement> pick)
    {
        try
        {
            return pick(module);
        }
        catch (Exception ex)
        {
            Log.Error($"Module '{module.Id}' failed to build its view", ex);
            return null;
        }
    }

    private static bool SafeHasActivity(IIslandModule module)
    {
        try
        {
            return module.HasActivity;
        }
        catch (Exception ex)
        {
            Log.Warn($"Module '{module.Id}' failed to report activity", ex);
            return false;
        }
    }

    private void OnModuleActivityChanged(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() => RefreshCompact(animate: true));
    }

    private void RefreshCompact(bool animate)
    {
        var active = modules.FirstOrDefault(SafeHasActivity) ?? modules.FirstOrDefault();
        if (!ReferenceEquals(active, compactModule))
        {
            compactModule = active;
            CompactHost.Content = active is null ? null : TryBuildView(active, m => m.CompactView);
        }

        compactWidth = active?.CompactWidth ?? IslandMetrics.DefaultCompactWidth;
        SizeCompactHost();
        if (!expanded)
        {
            Retarget(animate);
        }
    }

    private void SelectTab(IIslandModule? module)
    {
        if (module is not null && tabs.TryGetValue(module, out var tab))
        {
            tab.IsChecked = true;
        }
    }

    private void Select(IIslandModule module)
    {
        ExpandedContent.Content = TryBuildView(module, m => m.ExpandedView);
    }

    private void SetExpanded(bool value)
    {
        if (expanded == value || suppressed)
        {
            return;
        }

        expanded = value;
        if (expanded && compactModule is not null && SafeHasActivity(compactModule))
        {
            SelectTab(compactModule);
        }

        Retarget(animate: true);
    }

    private void SizeCompactHost()
    {
        var compact = IslandMetrics.Compact(style, compactWidth);
        CompactHost.Width = compact.Width;
        CompactHost.Height = compact.Height;
    }

    private double[] TargetValues()
    {
        var shape = expanded ? IslandMetrics.Expanded : IslandMetrics.Compact(style, compactWidth);
        return [shape.Width, shape.Height, shape.Radius, expanded ? 1 : 0];
    }

    private void Retarget(bool animate)
    {
        if (animate)
        {
            animator.AnimateTo(TargetValues());
        }
        else
        {
            animator.JumpTo(TargetValues());
        }
    }

    private void ApplyFrame(double[] values)
    {
        var width = Math.Max(values[0], 24);
        var height = Math.Max(values[1], 12);
        var radius = Math.Clamp(values[2], 0, Math.Min(width, height) / 2);
        var progress = Math.Clamp(values[3], 0, 1);
        var notch = style == IslandStyle.Notch;
        var shoulder = notch ? IslandMetrics.ShoulderRadius : 0;
        var top = notch ? 0 : IslandMetrics.FloatingGap;
        var left = (IslandMetrics.WindowWidth - (width + (shoulder * 2))) / 2;

        layout = new FrameLayout(left, top, width, height, radius, shoulder, notch);

        Canvas.SetLeft(IslandRoot, left);
        Canvas.SetTop(IslandRoot, top);
        Outline.Data = IslandGeometry.Outline(width, height, radius, shoulder, notch, 0);

        Canvas.SetLeft(Surface, shoulder);
        Surface.Width = width;
        Surface.Height = height;
        Surface.Clip = IslandGeometry.Outline(width, height, radius, 0, notch, 1);

        Canvas.SetLeft(CompactHost, (width - CompactHost.Width) / 2);
        Canvas.SetLeft(ExpandedHost, (width - ExpandedHost.Width) / 2);

        CompactHost.Opacity = Math.Clamp(1 - (progress * 2.5), 0, 1);
        ExpandedHost.Opacity = Math.Clamp((progress - 0.35) / 0.65, 0, 1);
        CompactHost.Visibility = CompactHost.Opacity > 0 ? Visibility.Visible : Visibility.Hidden;
        ExpandedHost.Visibility = ExpandedHost.Opacity > 0 ? Visibility.Visible : Visibility.Hidden;
        ExpandedHost.IsHitTestVisible = progress > 0.6;

        SyncBackdrop();
    }

    private void SyncBackdrop()
    {
        if (backdrop is null || !backdrop.IsEnabled || handle == IntPtr.Zero)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var total = layout.Width + (layout.Shoulder * 2);
        var shape = IslandGeometry.Outline(layout.Width, layout.Height, layout.Radius, layout.Shoulder, layout.Notch, 1);
        backdrop.Update(
            windowX + (int)Math.Round(layout.Left * dpi.DpiScaleX),
            windowY + (int)Math.Round(layout.Top * dpi.DpiScaleY),
            (int)Math.Ceiling(total * dpi.DpiScaleX),
            (int)Math.Ceiling(layout.Height * dpi.DpiScaleY),
            shape,
            dpi.DpiScaleX,
            dpi.DpiScaleY);
    }

    private void Reposition()
    {
        if (handle == IntPtr.Zero || !NativeMethods.TryGetPrimaryMonitorBounds(out var screen) || screen.Width <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var widthPx = (int)Math.Round(Width * dpi.DpiScaleX);
        windowX = screen.Left + ((screen.Width - widthPx) / 2);
        windowY = screen.Top;
        NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HWND_TOPMOST,
            windowX,
            windowY,
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        SyncBackdrop();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_DISPLAYCHANGE)
        {
            Dispatcher.InvokeAsync(Reposition, DispatcherPriority.Background);
        }

        return IntPtr.Zero;
    }

    private readonly record struct FrameLayout(
        double Left,
        double Top,
        double Width,
        double Height,
        double Radius,
        double Shoulder,
        bool Notch);
}
