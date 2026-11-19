using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace PixelMe.Modules.Media;

public sealed class MediaCompact : Grid
{
    private static readonly double[] Durations = [0.42, 0.56, 0.36, 0.49];

    private readonly Border art;
    private readonly ScaleTransform[] scales;
    private readonly SolidColorBrush barBrush = new(MediaState.DefaultAccent);
    private readonly Storyboard pulse = new();
    private bool playing;
    private bool animating;

    public MediaCompact()
    {
        Margin = new Thickness(6, 0, 14, 0);

        art = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        art.SetResourceReference(Border.BackgroundProperty, "Island.Subtle");
        Children.Add(art);

        var bars = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Height = 14,
        };

        scales = new ScaleTransform[Durations.Length];
        for (var i = 0; i < Durations.Length; i++)
        {
            var scale = new ScaleTransform(1, 0.35);
            var bar = new Rectangle
            {
                Width = 3,
                Height = 14,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = barBrush,
                Margin = new Thickness(i == 0 ? 0 : 2.5, 0, 0, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = scale,
            };
            bars.Children.Add(bar);
            scales[i] = scale;

            var animation = new DoubleAnimation(0.3, 1, TimeSpan.FromSeconds(Durations[i]))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Storyboard.SetTarget(animation, bar);
            Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.ScaleY"));
            pulse.Children.Add(animation);
        }

        Timeline.SetDesiredFrameRate(pulse, 20);
        Children.Add(bars);
        IsVisibleChanged += (_, _) => UpdatePulse();
    }

    public void Render(MediaState state)
    {
        if (state.Art is null)
        {
            art.SetResourceReference(Border.BackgroundProperty, "Island.Subtle");
        }
        else
        {
            var brush = new ImageBrush(state.Art) { Stretch = Stretch.UniformToFill };
            brush.Freeze();
            art.Background = brush;
        }

        barBrush.Color = state.Accent;
        playing = state.IsPlaying;
        UpdatePulse();
    }

    private void UpdatePulse()
    {
        var shouldAnimate = playing && IsVisible;
        if (shouldAnimate == animating)
        {
            return;
        }

        animating = shouldAnimate;
        if (animating)
        {
            pulse.Begin(this, true);
            return;
        }

        pulse.Stop(this);
        foreach (var scale in scales)
        {
            scale.ScaleY = 0.35;
        }
    }
}
