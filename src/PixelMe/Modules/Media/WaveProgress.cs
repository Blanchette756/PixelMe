using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PixelMe.Modules.Media;

public sealed class WaveProgress : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(WaveProgress),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent),
        typeof(Brush),
        typeof(WaveProgress),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track),
        typeof(Brush),
        typeof(WaveProgress),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThumbProperty = DependencyProperty.Register(
        nameof(Thumb),
        typeof(Brush),
        typeof(WaveProgress),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsWavingProperty = DependencyProperty.Register(
        nameof(IsWaving),
        typeof(bool),
        typeof(WaveProgress),
        new FrameworkPropertyMetadata(false, (d, _) => ((WaveProgress)d).UpdateClock()));

    private const double Amplitude = 3.2;
    private const double Wavelength = 22;
    private const double Speed = 5.5;
    private const double FrameMilliseconds = 48;

    private readonly Stopwatch clock = new();
    private TimeSpan lastFrame;
    private double phase;
    private bool subscribed;

    public WaveProgress()
    {
        Cursor = Cursors.Hand;
        IsVisibleChanged += (_, _) => UpdateClock();
    }

    public event Action<double>? SeekRequested;

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Brush Accent
    {
        get => (Brush)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public Brush Track
    {
        get => (Brush)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public Brush Thumb
    {
        get => (Brush)GetValue(ThumbProperty);
        set => SetValue(ThumbProperty, value);
    }

    public bool IsWaving
    {
        get => (bool)GetValue(IsWavingProperty);
        set => SetValue(IsWavingProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

        var middle = height / 2;
        var x = Math.Clamp(Progress, 0, 1) * width;
        var amplitude = IsWaving ? Amplitude : 0;

        var trackPen = new Pen(Track, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (x + 8 < width - 2)
        {
            drawingContext.DrawLine(trackPen, new Point(x + 8, middle), new Point(width - 2, middle));
        }

        var waveEnd = x - 6;
        if (waveEnd > 2)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(2, Y(2)), false, false);
                for (var px = 4.0; px < waveEnd; px += 2)
                {
                    context.LineTo(new Point(px, Y(px)), true, true);
                }

                context.LineTo(new Point(waveEnd, Y(waveEnd)), true, true);
            }

            geometry.Freeze();
            var wavePen = new Pen(Accent, 3)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            drawingContext.DrawGeometry(null, wavePen, geometry);
        }

        drawingContext.DrawRoundedRectangle(Thumb, null, new Rect(x - 2, middle - 9, 4, 18), 2, 2);

        double Y(double px) => middle + (amplitude * Math.Sin((px / Wavelength * Math.PI * 2) - phase));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsEnabled || ActualWidth <= 0)
        {
            return;
        }

        SeekRequested?.Invoke(Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1));
        e.Handled = true;
    }

    private void UpdateClock()
    {
        var shouldRun = IsWaving && IsVisible;
        if (shouldRun && !subscribed)
        {
            subscribed = true;
            clock.Restart();
            lastFrame = TimeSpan.Zero;
            CompositionTarget.Rendering += OnRendering;
        }
        else if (!shouldRun && subscribed)
        {
            subscribed = false;
            clock.Stop();
            CompositionTarget.Rendering -= OnRendering;
        }

        InvalidateVisual();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = clock.Elapsed;
        var elapsed = now - lastFrame;
        if (elapsed.TotalMilliseconds < FrameMilliseconds)
        {
            return;
        }

        lastFrame = now;
        phase = (phase + (Math.Min(elapsed.TotalSeconds, 0.1) * Speed)) % (Math.PI * 2);
        InvalidateVisual();
    }
}
