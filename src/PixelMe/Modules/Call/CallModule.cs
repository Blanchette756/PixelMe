using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PixelMe.Localization;
using PixelMe.Native;
using PixelMe.Native.Audio;
using PixelMe.Settings;

namespace PixelMe.Modules.Call;

public sealed class CallModule(ModuleServices services) : IslandModule
{
    private const uint KeyM = 0x4D;

    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly SystemVolume console = new(SystemVolume.CaptureFlow, SystemVolume.ConsoleRole);
    private readonly SystemVolume communications = new(SystemVolume.CaptureFlow, SystemVolume.CommunicationsRole);
    private readonly MicUsageWatcher usage = new();
    private readonly DispatcherTimer ticker = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private DateTimeOffset? callStarted;
    private bool muted;
    private int hotkey;
    private TextBlock? compactGlyph;
    private TextBlock? compactText;
    private Button? bigButton;
    private TextBlock? status;
    private TextBlock? timer;
    private TextBlock? hint;

    public override string Id => "call";

    public override string DisplayName => Strings.Microphone;

    public override string Glyph => "";

    public override int Priority => 100;

    public override bool HasActivity => callStarted is not null;

    public override int ActivityRank => 3;

    public override double CompactWidth => 230;

    public override Task InitializeAsync(CancellationToken cancellationToken)
    {
        console.Changed += (_, isMuted) => SetMuted(isMuted);
        console.Refresh();
        communications.Refresh();
        muted = console.IsMuted ?? false;

        usage.Changed += inUse => dispatcher.InvokeAsync(() => OnUsage(inUse));
        usage.Start();
        if (usage.InUse)
        {
            callStarted = DateTimeOffset.Now;
        }

        ticker.Tick += (_, _) => UpdateTexts();
        services.Settings.Changed += OnSettingsChanged;
        ApplyHotkey(services.Settings.Current.MicHotkey);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        services.Settings.Changed -= OnSettingsChanged;
        services.Hotkeys.Unregister(hotkey);
        ticker.Stop();
        usage.Dispose();
        console.Dispose();
        communications.Dispose();
    }

    protected override FrameworkElement BuildCompact()
    {
        compactGlyph = Ui.Glyph("", 14);
        compactText = Ui.Text(string.Empty, 13, FontWeights.SemiBold);
        compactText.Margin = new Thickness(8, 0, 0, 0);
        var row = Ui.Columns((Ui.Auto, compactGlyph), (Ui.Star(), compactText));
        row.Margin = new Thickness(16, 0, 16, 0);
        row.IsVisibleChanged += (_, _) => UpdateTicker();
        UpdateTexts();
        return row;
    }

    protected override FrameworkElement BuildExpanded()
    {
        bigButton = new Button { Width = 84, Height = 84, FontSize = 30, ToolTip = Strings.ToggleMic };
        bigButton.SetResourceReference(FrameworkElement.StyleProperty, "Island.Tile");
        bigButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        bigButton.SetResourceReference(Control.FontFamilyProperty, "Island.IconFont");
        bigButton.Click += (_, _) => Toggle();

        status = Ui.Text(string.Empty, 20, FontWeights.Bold);
        timer = Ui.Text(string.Empty, 13.5, FontWeights.SemiBold, "Island.Secondary");
        hint = Ui.Text(string.Empty, 12.5, null, "Island.Secondary");
        hint.TextWrapping = TextWrapping.Wrap;

        var text = Ui.Column(6, status, timer, hint);
        text.Margin = new Thickness(22, 0, 0, 0);
        text.VerticalAlignment = VerticalAlignment.Center;
        var root = Ui.Columns((Ui.Auto, bigButton), (Ui.Star(), text));
        root.VerticalAlignment = VerticalAlignment.Center;
        root.Margin = new Thickness(8, 0, 8, 0);
        root.IsVisibleChanged += (_, _) =>
        {
            UpdateTicker();
            if (root.IsVisible)
            {
                console.Refresh();
            }
        };
        Render();
        return root;
    }

    protected override void Render() => UpdateTexts();

    private void OnSettingsChanged(AppSettings previous, AppSettings current)
    {
        if (previous.MicHotkey != current.MicHotkey)
        {
            ApplyHotkey(current.MicHotkey);
        }
    }

    private void ApplyHotkey(bool enabled)
    {
        services.Hotkeys.Unregister(hotkey);
        hotkey = enabled ? services.Hotkeys.Register(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, KeyM, Toggle) : 0;
        UpdateTexts();
    }

    private void Toggle()
    {
        console.Refresh();
        communications.Refresh();
        var next = !(console.IsMuted ?? muted);
        console.SetMuted(next);
        communications.SetMuted(next);
        SetMuted(next);
        RequestAttention();
    }

    private void SetMuted(bool value)
    {
        muted = value;
        UpdateTexts();
    }

    private void OnUsage(bool inUse)
    {
        callStarted = inUse ? callStarted ?? DateTimeOffset.Now : null;
        UpdateTicker();
        UpdateTexts();
        RaiseActivityChanged();
    }

    private void UpdateTicker()
    {
        var needed = callStarted is not null && ((compactText?.IsVisible ?? false) || (timer?.IsVisible ?? false));
        if (needed && !ticker.IsEnabled)
        {
            ticker.Start();
        }
        else if (!needed && ticker.IsEnabled)
        {
            ticker.Stop();
        }
    }

    private void UpdateTexts()
    {
        var glyph = muted ? "" : "";
        var elapsed = callStarted is { } start ? DateTimeOffset.Now - start : TimeSpan.Zero;
        var clock = elapsed.TotalHours >= 1 ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"m\:ss");
        var brush = muted ? Brushes.IndianRed : (Brush)Application.Current.Resources["Island.Accent"];

        if (compactGlyph is not null && compactText is not null)
        {
            compactGlyph.Text = glyph;
            compactGlyph.Foreground = brush;
            var label = muted ? Strings.MicMuted : Strings.MicLive;
            compactText.Text = callStarted is null ? label : $"{clock}  {(muted ? Strings.Muted : Strings.Live)}";
        }

        if (bigButton is not null && status is not null && timer is not null && hint is not null)
        {
            bigButton.Content = glyph;
            bigButton.Foreground = muted ? Brushes.White : Brushes.Black;
            bigButton.Background = muted ? Brushes.IndianRed : brush;
            status.Text = muted ? Strings.MicMuted : Strings.MicLive;
            timer.Text = callStarted is null ? Strings.NoCall : string.Format(Strings.InCallFor, clock);
            hint.Text = !services.Settings.Current.MicHotkey
                ? Strings.HotkeyOff
                : hotkey == 0 ? Strings.HotkeyTaken : Strings.HotkeyHint;
        }
    }
}
