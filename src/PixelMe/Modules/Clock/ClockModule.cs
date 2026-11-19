using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using PixelMe.Localization;

namespace PixelMe.Modules.Clock;

public sealed class ClockModule : IslandModule
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer timer = new(DispatcherPriority.Background);
    private TextBlock? time;
    private bool started;

    public override string Id => "clock";

    public override string DisplayName => Strings.Clock;

    public override string Glyph => "";

    public override int Priority => 1000;

    public override bool ShowsTab => false;

    public override bool HasActivity => true;

    public override int ActivityRank => 1000;

    public override double CompactWidth => 170;

    public override Task InitializeAsync(CancellationToken cancellationToken)
    {
        timer.Tick += (_, _) => Refresh();
        SystemEvents.TimeChanged += OnSystemTimeChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        started = true;
        Refresh();
        return Task.CompletedTask;
    }

    public override void RefreshText() => Refresh();

    public override void Dispose()
    {
        timer.Stop();
        if (started)
        {
            SystemEvents.TimeChanged -= OnSystemTimeChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            started = false;
        }
    }

    protected override FrameworkElement BuildCompact()
    {
        time = Ui.Text(string.Empty, 13, FontWeights.SemiBold);
        time.HorizontalAlignment = HorizontalAlignment.Left;

        var dot = new Ellipse { Width = 6, Height = 6, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        dot.SetResourceReference(Shape.FillProperty, "Island.Accent");

        var panel = new Grid { Margin = new Thickness(18, 0, 16, 0) };
        panel.Children.Add(time);
        panel.Children.Add(dot);

        Refresh();
        return panel;
    }

    protected override FrameworkElement BuildExpanded() => new Grid();

    private void Refresh()
    {
        var now = DateTime.Now;
        if (time is not null)
        {
            time.Text = now.ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern, CultureInfo.CurrentCulture);
        }

        if (!started)
        {
            return;
        }

        timer.Stop();
        timer.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond) + TimeSpan.FromMilliseconds(50);
        timer.Start();
    }

    private void OnSystemTimeChanged(object? sender, EventArgs e) => dispatcher.InvokeAsync(Refresh);

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            dispatcher.InvokeAsync(Refresh);
        }
    }
}
