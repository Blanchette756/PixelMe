using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PixelMe.Localization;

namespace PixelMe.Modules.Media;

public partial class MediaPanel : UserControl
{
    private readonly DispatcherTimer ticker = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
    private MediaState current = MediaState.Empty;
    private bool updatingVolume;

    public MediaPanel()
    {
        InitializeComponent();
        ticker.Tick += (_, _) => UpdateTimeline();

        PreviousButton.Click += (_, _) => PreviousRequested?.Invoke();
        PlayPauseButton.Click += (_, _) => PlayPauseRequested?.Invoke();
        NextButton.Click += (_, _) => NextRequested?.Invoke();
        MuteButton.Click += (_, _) => MuteToggleRequested?.Invoke();
        Progress.SeekRequested += fraction => SeekRequested?.Invoke(fraction);
        VolumeSlider.ValueChanged += (_, e) =>
        {
            if (!updatingVolume)
            {
                VolumeRequested?.Invoke((float)e.NewValue);
            }
        };

        IsVisibleChanged += (_, _) =>
        {
            UpdateTicker();
            if (IsVisible)
            {
                UpdateTimeline();
                Shown?.Invoke();
            }
        };
    }

    public event Action? PreviousRequested;

    public event Action? PlayPauseRequested;

    public event Action? NextRequested;

    public event Action<double>? SeekRequested;

    public event Action<float>? VolumeRequested;

    public event Action? MuteToggleRequested;

    public event Action? Shown;

    public void Render(MediaState state)
    {
        current = state;

        EmptyTitle.Text = Strings.NothingPlaying;
        EmptyHint.Text = Strings.NothingPlayingHint;
        PreviousButton.ToolTip = Strings.Previous;
        PlayPauseButton.ToolTip = Strings.PlayPause;
        NextButton.ToolTip = Strings.Next;
        MuteButton.ToolTip = Strings.Mute;

        EmptyState.Visibility = state.HasSession ? Visibility.Collapsed : Visibility.Visible;
        PlayerState.Visibility = state.HasSession ? Visibility.Visible : Visibility.Collapsed;

        TitleText.Text = string.IsNullOrWhiteSpace(state.Title) ? Strings.UnknownTitle : state.Title;
        ArtistText.Text = state.Artist;

        var accent = new SolidColorBrush(state.Accent);
        accent.Freeze();
        ArtistText.Foreground = accent;
        Progress.Accent = accent;
        Badge.Background = accent;

        if (state.Art is null)
        {
            ArtImage.Background = null;
            ArtPlaceholder.Visibility = Visibility.Visible;
        }
        else
        {
            var brush = new ImageBrush(state.Art) { Stretch = Stretch.UniformToFill };
            brush.Freeze();
            ArtImage.Background = brush;
            ArtPlaceholder.Visibility = Visibility.Collapsed;
        }

        PlayPauseButton.Content = state.IsPlaying ? "" : "";
        PlayPauseButton.IsEnabled = state.CanPlayPause;
        PreviousButton.IsEnabled = state.CanPrevious;
        NextButton.IsEnabled = state.CanNext;

        var hasTimeline = state.Duration > TimeSpan.Zero;
        var timelineVisibility = hasTimeline ? Visibility.Visible : Visibility.Hidden;
        Progress.Visibility = timelineVisibility;
        ElapsedText.Visibility = timelineVisibility;
        RemainingText.Visibility = timelineVisibility;
        Progress.IsEnabled = state.CanSeek;
        Progress.IsWaving = state.IsPlaying;

        UpdateTimeline();
        UpdateTicker();
    }

    public void ShowVolume(float level, bool muted)
    {
        updatingVolume = true;
        VolumeSlider.Value = level;
        updatingVolume = false;
        MuteButton.Content = muted || level <= 0.001f ? "" : "";
        VolumeSlider.Opacity = muted ? 0.4 : 1;
    }

    private static string Format(TimeSpan time)
    {
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }

    private void UpdateTicker()
    {
        var shouldTick = IsVisible && current.IsPlaying && current.Duration > TimeSpan.Zero;
        if (shouldTick && !ticker.IsEnabled)
        {
            ticker.Start();
        }
        else if (!shouldTick && ticker.IsEnabled)
        {
            ticker.Stop();
        }
    }

    private void UpdateTimeline()
    {
        if (current.Duration <= TimeSpan.Zero)
        {
            LyricText.Visibility = Visibility.Collapsed;
            return;
        }

        var position = current.CurrentPosition(DateTimeOffset.Now);
        var line = Lyrics.LineAt(current.Lyrics, position);
        LyricText.Text = line ?? string.Empty;
        LyricText.Visibility = string.IsNullOrWhiteSpace(line) ? Visibility.Collapsed : Visibility.Visible;
        Progress.Progress = position.TotalMilliseconds / current.Duration.TotalMilliseconds;
        ElapsedText.Text = Format(position);
        RemainingText.Text = "-" + Format(current.Duration - position);
    }
}
