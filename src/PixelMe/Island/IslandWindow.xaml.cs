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
    private IReadOnlyList<IIslandModule> modules = [];
    private IslandStyle style;
    private IntPtr handle;
    private bool expanded;
    private bool interactive;
    private bool suppressed;

    public IslandWindow(SettingsStore settings)
    {
        this.settings = settings;
        InitializeComponent();

        Width = IslandMetrics.WindowWidth;
        Height = IslandMetrics.WindowHeight;
        ExpandedHost.Width = IslandMetrics.Expanded.Width - (IslandMetrics.BorderThickness * 2);
        ExpandedHost.Height = IslandMetrics.Expanded.Height - (IslandMetrics.BorderThickness * 2);
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
            if (!IslandBorder.IsMouseOver)
            {
                SetExpanded(false);
            }
        };

        animator = new SpringAnimator(TargetValues(), ApplyFrame);
        RefreshText();
    }

    public event Action? SettingsRequested;

    public IntPtr Handle => handle;

    public void PrepareSurface(ThemePalette palette)
    {
        handle = new WindowInteropHelper(this).EnsureHandle();
        WindowStyler.MakeOverlay(handle);
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        ApplyPalette(palette);
        ApplyStyle(style, animate: false);
    }

    public void ApplyPalette(ThemePalette palette)
    {
        palette.ApplyTo(Application.Current.Resources);
        IslandBorder.Background = palette.Background;
        IslandBorder.BorderBrush = palette.Border;
    }

    public void ApplyStyle(IslandStyle next, bool animate)
    {
        style = next;
        IslandBorder.Margin = new Thickness(0, style == IslandStyle.Floating ? IslandMetrics.FloatingGap : 0, 0, 0);
        SizeCompactHost();
        Retarget(animate);
        Reposition();
    }

    public void Mount(IReadOnlyList<IIslandModule> ready)
    {
        var mounted = new List<IIslandModule>();
        TabStrip.Children.Clear();

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
        }

        modules = mounted;
        CompactHost.Content = modules.Select(m => TryBuildView(m, x => x.CompactView)).FirstOrDefault(v => v is not null);

        if (TabStrip.Children.Count > 0 && TabStrip.Children[0] is RadioButton first)
        {
            first.IsChecked = true;
        }

        RefreshText();
    }

    public void EnableInteraction()
    {
        if (interactive)
        {
            return;
        }

        interactive = true;
        IslandBorder.MouseEnter += (_, _) =>
        {
            collapseTimer.Stop();
            if (!expanded)
            {
                Restart(expandTimer, settings.Current.ExpandDelayMs);
            }
        };

        IslandBorder.MouseLeave += (_, _) =>
        {
            expandTimer.Stop();
            if (expanded)
            {
                Restart(collapseTimer, settings.Current.CollapseDelayMs);
            }
        };

        IslandBorder.PreviewMouseLeftButtonDown += (_, _) =>
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
        foreach (var tab in TabStrip.Children.OfType<RadioButton>())
        {
            if (tab.Tag is IIslandModule module)
            {
                tab.ToolTip = module.DisplayName;
            }
        }

        var single = modules.Count == 1;
        TabStrip.Visibility = single ? Visibility.Collapsed : Visibility.Visible;
        HeaderTitle.Visibility = single ? Visibility.Visible : Visibility.Collapsed;
        HeaderTitle.Text = single ? modules[0].DisplayName : string.Empty;
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
        Retarget(animate: true);
    }

    private void SizeCompactHost()
    {
        var compact = IslandMetrics.Compact(style);
        CompactHost.Width = compact.Width - (IslandMetrics.BorderThickness * 2);
        CompactHost.Height = compact.Height - (IslandMetrics.BorderThickness * 2);
    }

    private double[] TargetValues()
    {
        var shape = expanded ? IslandMetrics.Expanded : IslandMetrics.Compact(style);
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
        var width = Math.Max(values[0], 12);
        var height = Math.Max(values[1], 12);
        var radius = Math.Clamp(values[2], 0, Math.Min(width, height) / 2);
        var progress = Math.Clamp(values[3], 0, 1);
        var notch = style == IslandStyle.Notch;

        IslandBorder.Width = width;
        IslandBorder.Height = height;
        IslandBorder.CornerRadius = notch ? new CornerRadius(0, 0, radius, radius) : new CornerRadius(radius);

        var innerWidth = width - (IslandMetrics.BorderThickness * 2);
        var innerHeight = height - (IslandMetrics.BorderThickness * 2);
        Surface.Clip = BuildClip(innerWidth, innerHeight, Math.Max(0, radius - IslandMetrics.BorderThickness), notch);

        Canvas.SetLeft(CompactHost, (innerWidth - CompactHost.Width) / 2);
        Canvas.SetLeft(ExpandedHost, (innerWidth - ExpandedHost.Width) / 2);

        CompactHost.Opacity = Math.Clamp(1 - (progress * 2.5), 0, 1);
        ExpandedHost.Opacity = Math.Clamp((progress - 0.35) / 0.65, 0, 1);
        CompactHost.Visibility = CompactHost.Opacity > 0 ? Visibility.Visible : Visibility.Hidden;
        ExpandedHost.Visibility = ExpandedHost.Opacity > 0 ? Visibility.Visible : Visibility.Hidden;
        ExpandedHost.IsHitTestVisible = progress > 0.6;
    }

    private static Geometry BuildClip(double width, double height, double radius, bool notch)
    {
        if (!notch)
        {
            var rounded = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
            rounded.Freeze();
            return rounded;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var size = new Size(radius, radius);
            context.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
            context.LineTo(new Point(width, 0), isStroked: false, isSmoothJoin: false);
            context.LineTo(new Point(width, height - radius), isStroked: false, isSmoothJoin: false);
            context.ArcTo(new Point(width - radius, height), size, 0, false, SweepDirection.Clockwise, false, false);
            context.LineTo(new Point(radius, height), isStroked: false, isSmoothJoin: false);
            context.ArcTo(new Point(0, height - radius), size, 0, false, SweepDirection.Clockwise, false, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private void Reposition()
    {
        if (handle == IntPtr.Zero || !NativeMethods.TryGetPrimaryMonitorBounds(out var screen) || screen.Width <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var widthPx = (int)Math.Round(Width * dpi.DpiScaleX);
        var x = screen.Left + ((screen.Width - widthPx) / 2);
        NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HWND_TOPMOST,
            x,
            screen.Top,
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_DISPLAYCHANGE)
        {
            Dispatcher.InvokeAsync(Reposition, DispatcherPriority.Background);
        }

        return IntPtr.Zero;
    }
}
