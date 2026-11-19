using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
    private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(4);
    private const double SmallMarginX = 60;
    private const double SmallMarginY = 14;
    private double[]? lastFrame;

    private readonly SettingsStore settings;
    private readonly DispatcherTimer expandTimer;
    private readonly DispatcherTimer collapseTimer;
    private readonly DispatcherTimer bounceTimer = new(DispatcherPriority.Input);
    private readonly DispatcherTimer flashTimer = new(DispatcherPriority.Background);
    private IIslandModule? flashModule;
    private bool hovering;
    private bool bouncing;
    private readonly SpringAnimator animator;
    private readonly Dictionary<IIslandModule, RadioButton> tabs = [];
    private IReadOnlyList<IIslandModule> modules = [];
    private IIslandModule? compactModule;
    private GlassBackdrop? backdrop;
    private FrameLayout layout;
    private IslandStyle style;
    private IslandPresence presence = IslandPresence.Normal;
    private double compactWidth = IslandMetrics.DefaultCompactWidth;
    private IntPtr handle;
    private int windowX;
    private int windowY;
    private bool expanded;
    private bool interactive;
    private bool keyboard;

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
        ThemeButton.Content = "";
        ThemeButton.Click += (_, _) => settings.Update(s => s.Theme = (IslandThemeKind)(((int)s.Theme + 1) % Enum.GetValues<IslandThemeKind>().Length));

        bounceTimer.Tick += (_, _) =>
        {
            bounceTimer.Stop();
            bouncing = false;
            if (!expanded)
            {
                Retarget(animate: true);
            }
        };

        flashTimer.Tick += (_, _) =>
        {
            flashTimer.Stop();
            flashModule = null;
            RefreshCompact(animate: true);
        };

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
            if (!IslandRoot.IsMouseOver && !(keyboard && IsActive))
            {
                SetExpanded(false);
            }
        };

        animator = new SpringAnimator(TargetValues(), ApplyFrame);
        animator.Settled += ShrinkToFit;
        IsVisibleChanged += (_, _) => backdrop?.SetVisible(IsVisible);
        Deactivated += (_, _) =>
        {
            if (keyboard)
            {
                DisableKeyboard();
                if (!IslandRoot.IsMouseOver)
                {
                    Restart(collapseTimer, settings.Current.CollapseDelayMs);
                }
            }
        };
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
            module.AttentionRequested -= OnModuleAttention;
        }

        TabStrip.Children.Clear();
        tabs.Clear();
        var mounted = new List<IIslandModule>();

        foreach (var module in ready.OrderBy(m => m.Priority))
        {
            if (module.ShowsTab && TryBuildView(module, m => m.ExpandedView) is null)
            {
                continue;
            }

            mounted.Add(module);
            module.ActivityChanged += OnModuleActivityChanged;
            module.AttentionRequested += OnModuleAttention;
            if (!module.ShowsTab)
            {
                continue;
            }

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
        }

        modules = mounted;
        RefreshCompact(animate: false);
        SelectTab(tabs.Keys.FirstOrDefault());
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
            if (expanded || presence != IslandPresence.Normal)
            {
                return;
            }

            hovering = true;
            Retarget(animate: true);
            if (settings.Current.OpenOnHover)
            {
                Restart(expandTimer, settings.Current.ExpandDelayMs);
            }
        };

        IslandRoot.MouseLeave += (_, _) =>
        {
            expandTimer.Stop();
            if (hovering)
            {
                hovering = false;
                if (!expanded)
                {
                    Retarget(animate: true);
                }
            }

            if (expanded && !(keyboard && IsActive))
            {
                Restart(collapseTimer, settings.Current.CollapseDelayMs + 250);
            }
        };

        IslandRoot.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (presence != IslandPresence.Normal)
            {
                return;
            }

            if (!expanded)
            {
                expandTimer.Stop();
                SetExpanded(true);
                e.Handled = true;
                return;
            }

            if (e.OriginalSource is DependencyObject source && WantsKeyboard(source))
            {
                EnableKeyboard();
            }
        };

        IslandRoot.AllowDrop = true;
        IslandRoot.DragEnter += OnDragOver;
        IslandRoot.DragOver += OnDragOver;
        IslandRoot.DragLeave += (_, _) => Restart(collapseTimer, 900);
        IslandRoot.Drop += (_, e) =>
        {
            var target = FindDropTarget(e.Data);
            if (target is not null)
            {
                try
                {
                    target.Accept(e.Data);
                }
                catch (Exception ex)
                {
                    Log.Error("Dropping files on the island failed", ex);
                }
            }

            e.Handled = true;
        };
    }

    public void RefreshText()
    {
        SettingsButton.ToolTip = Strings.Settings;
        ThemeButton.ToolTip = Strings.ChangeTheme;
        foreach (var module in modules)
        {
            if (tabs.TryGetValue(module, out var tab))
            {
                tab.ToolTip = module.DisplayName;
            }

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

    public void SetPresence(IslandPresence next)
    {
        if (presence == next)
        {
            return;
        }

        presence = next;
        expandTimer.Stop();
        collapseTimer.Stop();
        if (presence != IslandPresence.Normal)
        {
            expanded = false;
            DisableKeyboard();
        }

        if (presence == IslandPresence.Hidden)
        {
            Hide();
            return;
        }

        if (!IsVisible)
        {
            Show();
            WindowStyler.BringToTop(handle);
            Reposition();
        }

        Retarget(animate: true);
    }

    public void SetStripText(string text)
    {
        StripText.Text = text;
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.InvokeAsync(Reposition, DispatcherPriority.Background);
    }

    protected override void OnClosed(EventArgs e)
    {
        bounceTimer.Stop();
        flashTimer.Stop();
        foreach (var module in modules)
        {
            module.ActivityChanged -= OnModuleActivityChanged;
            module.AttentionRequested -= OnModuleAttention;
        }

        base.OnClosed(e);
        backdrop?.Dispose();
        backdrop = null;
    }

    private static void Restart(DispatcherTimer timer, double delayMs)
    {
        timer.Stop();
        timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, delayMs));
        timer.Start();
    }

    private static bool WantsKeyboard(DependencyObject source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current))
        {
            if (current is TextBoxBase or PasswordBox)
            {
                return true;
            }

            if (current is IslandWindow)
            {
                return false;
            }
        }

        return false;
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

    private void EnableKeyboard()
    {
        if (keyboard)
        {
            return;
        }

        keyboard = true;
        WindowStyler.SetActivatable(handle, true);
        Activate();
        NativeMethods.SetForegroundWindow(handle);
    }

    private void DisableKeyboard()
    {
        if (!keyboard)
        {
            return;
        }

        keyboard = false;
        Keyboard.ClearFocus();
        WindowStyler.SetActivatable(handle, false);
    }

    private IFileDropTarget? FindDropTarget(IDataObject data)
    {
        foreach (var target in modules.OfType<IFileDropTarget>())
        {
            try
            {
                if (target.CanAccept(data))
                {
                    return target;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Checking a drop target failed", ex);
            }
        }

        return null;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var target = presence == IslandPresence.Normal ? FindDropTarget(e.Data) : null;
        e.Effects = target is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
        if (target is IIslandModule module)
        {
            collapseTimer.Stop();
            SelectTab(module);
            SetExpanded(true);
        }
    }

    private void OnModuleActivityChanged(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() => RefreshCompact(animate: true));
    }

    private void OnModuleAttention(object? sender, EventArgs e)
    {
        if (sender is not IIslandModule module)
        {
            return;
        }

        Dispatcher.InvokeAsync(() =>
        {
            if (presence != IslandPresence.Normal)
            {
                return;
            }

            SelectTab(module);
            if (expanded)
            {
                return;
            }

            flashModule = module;
            Restart(flashTimer, FlashDuration.TotalMilliseconds);
            RefreshCompact(animate: true);
            bouncing = true;
            Retarget(animate: true);
            Restart(bounceTimer, 180);
        });
    }

    private void RefreshCompact(bool animate)
    {
        var active = (flashModule is not null && modules.Contains(flashModule) ? flashModule : null)
            ?? modules.Where(SafeHasActivity).OrderBy(m => m.ActivityRank).FirstOrDefault()
            ?? modules.FirstOrDefault(m => !m.ShowsTab)
            ?? modules.FirstOrDefault();

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
        if (expanded == value || (value && presence != IslandPresence.Normal))
        {
            return;
        }

        expanded = value;
        hovering = false;
        if (expanded && compactModule is not null && compactModule.ShowsTab && (SafeHasActivity(compactModule) || compactModule == flashModule))
        {
            SelectTab(compactModule);
        }

        if (!expanded)
        {
            DisableKeyboard();
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
        var shape = presence == IslandPresence.Strip
            ? IslandMetrics.Strip(style)
            : expanded ? IslandMetrics.Expanded : IslandMetrics.Compact(style, compactWidth);

        if (!expanded && presence == IslandPresence.Normal && (bouncing || hovering))
        {
            var growth = bouncing ? IslandMetrics.BounceGrowth : IslandMetrics.HoverGrowth;
            shape = shape with { Width = shape.Width + growth, Height = shape.Height + (growth / 4) };
        }

        return [shape.Width, shape.Height, shape.Radius, expanded ? 1 : 0];
    }

    private void Retarget(bool animate)
    {
        var target = TargetValues();
        if (expanded)
        {
            SetExtent(IslandMetrics.WindowWidth, IslandMetrics.WindowHeight);
        }
        else
        {
            var shoulder = style == IslandStyle.Notch ? IslandMetrics.ShoulderRadius * 2 : 0;
            var top = style == IslandStyle.Notch ? 0 : IslandMetrics.FloatingGap;
            var width = Math.Max(target[0], layout.Width) + shoulder;
            var height = Math.Max(target[1], layout.Height) + top;
            SetExtent(Math.Max(width + SmallMarginX, Width), Math.Max(height + SmallMarginY, Height));
        }

        if (animate)
        {
            animator.AnimateTo(target);
        }
        else
        {
            animator.JumpTo(target);
        }
    }

    private void ShrinkToFit()
    {
        if (expanded || handle == IntPtr.Zero)
        {
            return;
        }

        var shoulder = style == IslandStyle.Notch ? IslandMetrics.ShoulderRadius * 2 : 0;
        var top = style == IslandStyle.Notch ? 0 : IslandMetrics.FloatingGap;
        SetExtent(layout.Width + shoulder + SmallMarginX, layout.Height + top + SmallMarginY);
    }

    private void SetExtent(double width, double height)
    {
        width = Math.Min(IslandMetrics.WindowWidth, Math.Ceiling(width));
        height = Math.Min(IslandMetrics.WindowHeight, Math.Ceiling(height));
        if (Math.Abs(width - Width) < 0.5 && Math.Abs(height - Height) < 0.5)
        {
            return;
        }

        Width = width;
        Height = height;
        Reposition();
        if (lastFrame is not null)
        {
            ApplyFrame(lastFrame);
        }
    }

    private void ApplyFrame(double[] values)
    {
        lastFrame = values;
        var width = Math.Max(values[0], 24);
        var height = Math.Max(values[1], 12);
        var radius = Math.Clamp(values[2], 0, Math.Min(width, height) / 2);
        var progress = Math.Clamp(values[3], 0, 1);
        var notch = style == IslandStyle.Notch;
        var shoulder = notch ? IslandMetrics.ShoulderRadius : 0;
        var top = notch ? 0 : IslandMetrics.FloatingGap;
        var left = (Width - (width + (shoulder * 2))) / 2;
        var strip = presence == IslandPresence.Strip;

        layout = new FrameLayout(left, top, width, height, radius, shoulder, notch);

        Canvas.SetLeft(IslandRoot, left);
        Canvas.SetTop(IslandRoot, top);
        Outline.Data = IslandGeometry.Outline(width, height, radius, shoulder, notch, 0);

        Canvas.SetLeft(Surface, shoulder);
        Surface.Width = width;
        Surface.Height = height;
        Surface.Clip = IslandGeometry.Outline(width, height, radius, 0, notch, 1);

        Canvas.SetLeft(CompactHost, (width - CompactHost.Width) / 2);
        Canvas.SetTop(CompactHost, expanded ? 0 : Math.Max(0, (height - CompactHost.Height) / 2));
        Canvas.SetLeft(ExpandedHost, (width - ExpandedHost.Width) / 2);
        Canvas.SetLeft(StripText, (width - IslandMetrics.StripTextWidth) / 2);
        Canvas.SetTop(StripText, Math.Max(0, (height - 16) / 2));

        CompactHost.Opacity = strip ? 0 : Math.Clamp(1 - (progress * 2.5), 0, 1);
        ExpandedHost.Opacity = Math.Clamp((progress - 0.35) / 0.65, 0, 1);
        CompactHost.Visibility = CompactHost.Opacity > 0 ? Visibility.Visible : Visibility.Hidden;
        ExpandedHost.Visibility = ExpandedHost.Opacity > 0 ? Visibility.Visible : Visibility.Hidden;
        ExpandedHost.IsHitTestVisible = progress > 0.6;
        StripText.Visibility = strip && height < 30 ? Visibility.Visible : Visibility.Hidden;

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
