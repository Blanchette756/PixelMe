using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Localization;
using PixelMe.Settings;

namespace PixelMe.Modules.Stream;

public sealed class StreamModule(ModuleServices services) : IslandModule
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer ticker = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private CancellationTokenSource? obsRun;
    private bool connected;
    private DateTimeOffset? recordingSince;
    private TextBlock? compactText;
    private TextBlock? obsStatus;
    private StackPanel? chatButtons;

    public override string Id => "stream";

    public override string DisplayName => Strings.StreamTitle;

    public override string Glyph => "";

    public override int Priority => 110;

    public override bool HasActivity => recordingSince is not null;

    public override int ActivityRank => 4;

    public override double CompactWidth => 200;

    public override Task InitializeAsync(CancellationToken cancellationToken)
    {
        ticker.Tick += (_, _) => UpdateTexts();
        services.Settings.Changed += OnSettingsChanged;
        RestartObs();
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        services.Settings.Changed -= OnSettingsChanged;
        obsRun?.Cancel();
        ticker.Stop();
    }

    protected override FrameworkElement BuildCompact()
    {
        var dot = new Ellipse { Width = 9, Height = 9, Fill = Brushes.Red, VerticalAlignment = VerticalAlignment.Center };
        compactText = Ui.Text(string.Empty, 13, FontWeights.Bold);
        compactText.SetResourceReference(TextBlock.FontFamilyProperty, "Island.MonoFont");
        compactText.Margin = new Thickness(8, 0, 0, 0);
        var row = Ui.Columns((Ui.Auto, dot), (Ui.Star(), compactText));
        row.Margin = new Thickness(18, 0, 16, 0);
        UpdateTexts();
        return row;
    }

    protected override FrameworkElement BuildExpanded()
    {
        obsStatus = Ui.Text(string.Empty, 14, FontWeights.SemiBold);
        chatButtons = new StackPanel { Orientation = Orientation.Horizontal };
        var obsCard = Ui.Card(Ui.Column(6, Ui.Text("OBS", 11, FontWeights.Bold, "Island.Secondary"), obsStatus), 14);
        var chatCard = Ui.Card(Ui.Column(8, Ui.Text(Strings.LiveChat.ToUpperInvariant(), 11, FontWeights.Bold, "Island.Secondary"), chatButtons), 14);
        chatCard.Margin = new Thickness(0, 10, 0, 0);
        Render();
        return Ui.Column(0, obsCard, chatCard);
    }

    protected override void Render()
    {
        UpdateTexts();
        if (chatButtons is null)
        {
            return;
        }

        chatButtons.Children.Clear();
        var settings = services.Settings.Current;
        if (!string.IsNullOrWhiteSpace(settings.TwitchChannel))
        {
            var channel = settings.TwitchChannel.Trim().TrimStart('@');
            chatButtons.Children.Add(Ui.Pill("Twitch", () => services.Web.Open($"https://www.twitch.tv/popout/{Uri.EscapeDataString(channel)}/chat?darkpopout", $"Twitch · {channel}"), "").With(new Thickness(0, 0, 8, 0)));
        }

        var video = YouTubeId(settings.YouTubeLive);
        if (video is not null)
        {
            chatButtons.Children.Add(Ui.Pill("YouTube", () => services.Web.Open($"https://www.youtube.com/live_chat?is_popout=1&v={video}", "YouTube chat"), ""));
        }

        if (chatButtons.Children.Count == 0)
        {
            chatButtons.Children.Add(Ui.Text(Strings.ChatHint, 12.5, null, "Island.Secondary"));
        }
    }

    private static string? YouTubeId(string input)
    {
        var text = input.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            if (query["v"] is { Length: > 0 } fromQuery)
            {
                return fromQuery;
            }

            var last = uri.Segments.LastOrDefault()?.Trim('/');
            return string.IsNullOrEmpty(last) ? null : last;
        }

        return Uri.EscapeDataString(text);
    }

    private void RestartObs()
    {
        obsRun?.Cancel();
        obsRun = null;
        SetRecording(false, TimeSpan.Zero);
        connected = false;
        var settings = services.Settings.Current;
        if (!settings.ObsEnabled)
        {
            UpdateTexts();
            return;
        }

        var client = new ObsClient();
        client.ConnectionChanged += value => dispatcher.InvokeAsync(() =>
        {
            connected = value;
            if (!value)
            {
                SetRecording(false, TimeSpan.Zero);
            }

            UpdateTexts();
        });
        client.RecordingChanged += (active, elapsed) => dispatcher.InvokeAsync(() => SetRecording(active, elapsed));
        obsRun = new CancellationTokenSource();
        var password = Secrets.Unprotect(settings.ObsPasswordProtected);
        _ = client.RunAsync(settings.ObsHost, settings.ObsPort, password, obsRun.Token);
        UpdateTexts();
    }

    private void SetRecording(bool active, TimeSpan elapsed)
    {
        var was = recordingSince is not null;
        recordingSince = active ? DateTimeOffset.Now - elapsed : null;
        if (active)
        {
            ticker.Start();
        }
        else
        {
            ticker.Stop();
        }

        UpdateTexts();
        if (was != active)
        {
            RaiseActivityChanged();
        }
    }

    private void UpdateTexts()
    {
        var elapsed = recordingSince is { } since ? DateTimeOffset.Now - since : TimeSpan.Zero;
        var clock = elapsed.ToString(@"h\:mm\:ss");
        if (compactText is not null)
        {
            compactText.Text = $"REC {clock}";
        }

        if (obsStatus is not null)
        {
            obsStatus.Text = !services.Settings.Current.ObsEnabled
                ? Strings.ObsOff
                : recordingSince is not null ? $"{Strings.Recording} · {clock}" : connected ? Strings.ObsConnected : Strings.ObsConnecting;
        }
    }

    private void OnSettingsChanged(AppSettings previous, AppSettings current)
    {
        if (previous.ObsEnabled != current.ObsEnabled
            || previous.ObsHost != current.ObsHost
            || previous.ObsPort != current.ObsPort
            || previous.ObsPasswordProtected != current.ObsPasswordProtected)
        {
            RestartObs();
        }

        if (previous.TwitchChannel != current.TwitchChannel || previous.YouTubeLive != current.YouTubeLive)
        {
            Render();
        }
    }
}
