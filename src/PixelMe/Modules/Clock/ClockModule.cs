using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using PixelMe.Localization;

namespace PixelMe.Modules.Clock;

public sealed class ClockModule : IIslandModule
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer timer = new(DispatcherPriority.Background);
    private TextBlock? compactTime;
    private TextBlock? expandedTime;
    private TextBlock? expandedDate;
    private FrameworkElement? compactView;
    private FrameworkElement? expandedView;
    private bool started;

    public string Id => "clock";

    public string DisplayName => Strings.Clock;

    public string Glyph => "";

    public int Priority => 100;

    public FrameworkElement CompactView => compactView ??= BuildCompact();

    public FrameworkElement ExpandedView => expandedView ??= BuildExpanded();

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        timer.Tick += (_, _) => Refresh();
        SystemEvents.TimeChanged += OnSystemTimeChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        started = true;
        Refresh();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        timer.Stop();
        if (started)
        {
            SystemEvents.TimeChanged -= OnSystemTimeChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            started = false;
        }
    }

    private static TextBlock CreateText(double size, FontWeight weight, string brushKey)
    {
        var text = new TextBlock { FontSize = size, FontWeight = weight };
        text.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return text;
    }

    private FrameworkElement BuildCompact()
    {
        compactTime = CreateText(14, FontWeights.SemiBold, "Island.Foreground");
        compactTime.VerticalAlignment = VerticalAlignment.Center;

        var dot = new Ellipse { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
        dot.SetResourceReference(Shape.FillProperty, "Island.Accent");
        DockPanel.SetDock(dot, Dock.Right);

        var panel = new DockPanel { Margin = new Thickness(18, 0, 16, 0), LastChildFill = true };
        panel.Children.Add(dot);
        panel.Children.Add(compactTime);

        Refresh();
        return panel;
    }

    private FrameworkElement BuildExpanded()
    {
        expandedTime = CreateText(46, FontWeights.Light, "Island.Foreground");
        expandedDate = CreateText(14, FontWeights.Normal, "Island.Secondary");
        expandedDate.Margin = new Thickness(2, 0, 0, 0);

        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(expandedTime);
        panel.Children.Add(expandedDate);

        Refresh();
        return panel;
    }

    private void Refresh()
    {
        var now = DateTime.Now;
        var regional = CultureInfo.CurrentCulture;
        var time = now.ToString(regional.DateTimeFormat.ShortTimePattern, regional);

        if (compactTime is not null)
        {
            compactTime.Text = time;
        }

        if (expandedTime is not null)
        {
            expandedTime.Text = time;
        }

        if (expandedDate is not null)
        {
            expandedDate.Text = now.ToString("dddd, d MMMM yyyy", Strings.Culture);
        }

        if (!started)
        {
            return;
        }

        var untilNextMinute = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);
        timer.Stop();
        timer.Interval = untilNextMinute + TimeSpan.FromMilliseconds(50);
        timer.Start();
    }

    private void OnSystemTimeChanged(object? sender, EventArgs e)
    {
        dispatcher.InvokeAsync(Refresh);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            dispatcher.InvokeAsync(Refresh);
        }
    }
}
