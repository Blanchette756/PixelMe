using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Localization;
using PixelMe.Native;
using PixelMe.Native.Audio;
using Windows.Devices.Radios;

namespace PixelMe.Modules.Controls;

public sealed class ControlsModule : IslandModule
{
    private static readonly (Guid Id, Func<string> Label)[] PowerModes =
    [
        (new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a"), () => Strings.PowerEfficiency),
        (Guid.Empty, () => Strings.PowerBalanced),
        (new Guid("ded574b5-45a0-4f42-8737-46345c09c238"), () => Strings.PowerPerformance),
    ];

    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly SystemVolume volume = new();
    private readonly Brightness brightness = new();
    private Radio? wifi;
    private Radio? bluetooth;
    private Slider? volumeSlider;
    private Slider? brightnessSlider;
    private TextBlock? volumeValue;
    private TextBlock? brightnessValue;
    private FrameworkElement? brightnessRow;
    private Button? wifiTile;
    private Button? bluetoothTile;
    private StackPanel? powerRow;
    private bool updating;

    public override string Id => "controls";

    public override string DisplayName => Strings.QuickControls;

    public override string Glyph => "";

    public override int Priority => 60;

    public override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        volume.Changed += (level, muted) => ShowVolume(level, muted);
        try
        {
            if (await Radio.RequestAccessAsync() == RadioAccessStatus.Allowed)
            {
                var radios = await Radio.GetRadiosAsync();
                wifi = radios.FirstOrDefault(r => r.Kind == RadioKind.WiFi);
                bluetooth = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
                foreach (var radio in new[] { wifi, bluetooth })
                {
                    if (radio is not null)
                    {
                        radio.StateChanged += (_, _) => dispatcher.InvokeAsync(UpdateRadios);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Wi-Fi and Bluetooth controls are unavailable", ex);
        }
    }

    public override void Dispose()
    {
        volume.Dispose();
    }

    protected override FrameworkElement BuildExpanded()
    {
        volumeSlider = SliderFor(v => volume.SetLevel((float)v));
        volumeValue = ValueText();
        brightnessSlider = SliderFor(v => brightness.Write((int)Math.Round(v * 100)));
        brightnessValue = ValueText();
        var volumeRow = SliderRow("", Strings.Volume, volumeSlider, volumeValue);
        brightnessRow = SliderRow("", Strings.Brightness, brightnessSlider, brightnessValue);
        brightnessRow.Visibility = Visibility.Collapsed;

        powerRow = new StackPanel { Orientation = Orientation.Horizontal };
        var power = Ui.Column(6, Ui.Text(Strings.PowerMode, 12, FontWeights.SemiBold, "Island.Secondary"), powerRow);
        var left = Ui.Column(14, volumeRow, brightnessRow, power);
        left.VerticalAlignment = VerticalAlignment.Center;

        wifiTile = TileFor("", Strings.WiFi, () => ToggleRadio(wifi));
        bluetoothTile = TileFor("", Strings.Bluetooth, () => ToggleRadio(bluetooth));
        var night = TileFor("", Strings.NightLight, () => ShellLauncher.Open("ms-settings:nightlight"), external: true);
        var focus = TileFor("", Strings.Focus, () => ShellLauncher.Open("ms-settings:quiethours"), external: true);

        var grid = new UniformGridLite(2, wifiTile, bluetoothTile, night, focus);
        var root = Ui.Columns((Ui.Star(), left), (Ui.Px(20), new Grid()), (Ui.Px(250), grid));
        root.IsVisibleChanged += async (_, _) =>
        {
            if (root.IsVisible)
            {
                await RefreshAsync();
            }
        };
        return root;
    }

    private static TextBlock ValueText()
    {
        var text = Ui.Text("—", 12.5, FontWeights.SemiBold, "Island.Secondary");
        text.Width = 38;
        text.TextAlignment = TextAlignment.Right;
        return text;
    }

    private static FrameworkElement SliderRow(string glyph, string tooltip, Slider slider, TextBlock value)
    {
        var icon = Ui.Glyph(glyph, 16);
        icon.ToolTip = tooltip;
        icon.Width = 26;
        slider.Margin = new Thickness(8, 0, 8, 0);
        return Ui.Columns((Ui.Auto, icon), (Ui.Star(), slider), (Ui.Auto, value));
    }

    private Slider SliderFor(Action<double> changed)
    {
        var slider = new Slider();
        slider.SetResourceReference(FrameworkElement.StyleProperty, "Island.Slider");
        slider.ValueChanged += (_, e) =>
        {
            if (!updating)
            {
                changed(e.NewValue);
                UpdateValueTexts();
            }
        };
        return slider;
    }

    private static Button TileFor(string glyph, string label, Action click, bool external = false)
    {
        var icon = Ui.Glyph(glyph, 17, "Island.Foreground");
        icon.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1) });
        var text = new TextBlock { Text = label, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        var content = Ui.Column(0, icon, text);
        if (external)
        {
            text.Text = label + "  ↗";
        }

        var tile = Ui.Tile(content, click);
        tile.Height = 64;
        tile.Margin = new Thickness(4);
        return tile;
    }

    private async Task RefreshAsync()
    {
        volume.Refresh();
        UpdateRadios();
        UpdatePower();
        var level = await Brightness.ReadAsync();
        if (brightnessRow is not null && brightnessSlider is not null)
        {
            brightnessRow.Visibility = level is null ? Visibility.Collapsed : Visibility.Visible;
            if (level is not null)
            {
                updating = true;
                brightnessSlider.Value = level.Value / 100.0;
                updating = false;
                UpdateValueTexts();
            }
        }
    }

    private void ShowVolume(float level, bool muted)
    {
        if (volumeSlider is null)
        {
            return;
        }

        updating = true;
        volumeSlider.Value = level;
        updating = false;
        volumeSlider.Opacity = muted ? 0.4 : 1;
        UpdateValueTexts();
    }

    private void UpdateValueTexts()
    {
        if (volumeValue is not null && volumeSlider is not null)
        {
            volumeValue.Text = $"{volumeSlider.Value * 100:0}%";
        }

        if (brightnessValue is not null && brightnessSlider is not null)
        {
            brightnessValue.Text = $"{brightnessSlider.Value * 100:0}%";
        }
    }

    private void UpdateRadios()
    {
        Apply(wifiTile, wifi);
        Apply(bluetoothTile, bluetooth);

        static void Apply(Button? tile, Radio? radio)
        {
            if (tile is null)
            {
                return;
            }

            tile.IsEnabled = radio is not null;
            Ui.SetActive(tile, radio?.State == RadioState.On);
        }
    }

    private async void ToggleRadio(Radio? radio)
    {
        if (radio is null)
        {
            return;
        }

        try
        {
            var result = await radio.SetStateAsync(radio.State == RadioState.On ? RadioState.Off : RadioState.On);
            if (result != RadioAccessStatus.Allowed)
            {
                Log.Warn($"Changing {radio.Kind} was refused: {result}");
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Changing a radio failed", ex);
        }

        UpdateRadios();
    }

    private void UpdatePower()
    {
        if (powerRow is null)
        {
            return;
        }

        powerRow.Children.Clear();
        if (NativeMethods.PowerGetEffectiveOverlayScheme(out var current) != 0)
        {
            powerRow.Children.Add(Ui.Text(Strings.PowerUnavailable, 12, null, "Island.Secondary"));
            return;
        }

        foreach (var (id, label) in PowerModes)
        {
            powerRow.Children.Add(Ui.Chip(label(), id == current, () =>
            {
                if (NativeMethods.PowerSetActiveOverlayScheme(id) != 0)
                {
                    Log.Warn("Changing the power mode failed");
                }

                UpdatePower();
            }));
        }
    }

    private sealed class UniformGridLite : System.Windows.Controls.Primitives.UniformGrid
    {
        public UniformGridLite(int columns, params UIElement[] children)
        {
            Columns = columns;
            foreach (var child in children)
            {
                Children.Add(child);
            }

            VerticalAlignment = VerticalAlignment.Center;
        }
    }
}
