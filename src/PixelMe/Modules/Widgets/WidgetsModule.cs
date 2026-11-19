using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Localization;
using PixelMe.Settings;
using Forms = System.Windows.Forms;

namespace PixelMe.Modules.Widgets;

public sealed class TodoItem
{
    public string Text { get; set; } = string.Empty;

    public bool Done { get; set; }
}

public sealed class TodoData
{
    public List<TodoItem> Items { get; set; } = [];
}

public sealed class WidgetsModule(ModuleServices services) : IslandModule
{
    private static readonly string[] PhotoExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"];

    private readonly DispatcherTimer ticker = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch stopwatch = new();
    private readonly Random random = new();
    private TodoData todos = new();
    private WeatherReport? weather;
    private string weatherCity = string.Empty;
    private bool fetchingWeather;
    private int tickCount;
    private StackPanel? cards;
    private TextBlock? clockTime;
    private TextBlock? clockDate;
    private StackPanel? worldList;
    private TextBlock? weatherGlyph;
    private TextBlock? weatherTemp;
    private TextBlock? weatherPlace;
    private TextBlock? batteryText;
    private TextBlock? batteryState;
    private FrameworkElement? batteryCard;
    private TextBlock? stopwatchText;
    private Button? stopwatchToggle;
    private TextBlock? cpuText;
    private TextBlock? ramText;
    private TextBlock? gpuText;
    private StackPanel? todoList;
    private Border? photo;
    private TextBlock? photoHint;
    private List<string> photoFiles = [];
    private string photoFolder = string.Empty;

    public override string Id => "widgets";

    public override string DisplayName => Strings.Widgets;

    public override string Glyph => "";

    public override int Priority => 70;

    public override Task InitializeAsync(CancellationToken cancellationToken)
    {
        todos = JsonFile.Load<TodoData>("todo");
        ticker.Tick += (_, _) => Tick();
        services.Settings.Changed += OnSettingsChanged;
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        services.Settings.Changed -= OnSettingsChanged;
        ticker.Stop();
    }

    protected override FrameworkElement BuildExpanded()
    {
        cards = new StackPanel { Orientation = Orientation.Horizontal };
        var scroll = Ui.Scroll(cards, horizontal: true);
        scroll.IsVisibleChanged += (_, _) =>
        {
            if (scroll.IsVisible)
            {
                tickCount = 0;
                Tick();
                ticker.Start();
            }
            else
            {
                ticker.Stop();
            }
        };
        Render();
        return scroll;
    }

    protected override void Render()
    {
        if (cards is null)
        {
            return;
        }

        cards.Children.Clear();
        cards.Children.Add(Card(170, BuildClock()));
        cards.Children.Add(Card(190, BuildWorld()));
        cards.Children.Add(Card(160, BuildWeather()));
        batteryCard = Card(140, BuildBattery());
        cards.Children.Add(batteryCard);
        cards.Children.Add(Card(170, BuildStopwatch()));
        cards.Children.Add(Card(170, BuildStats()));
        cards.Children.Add(Card(230, BuildTodo()));
        cards.Children.Add(Card(200, BuildPhoto(), 0));
        Tick();
    }

    private static Border Card(double width, UIElement content, double padding = 14)
    {
        var card = Ui.Card(content, padding);
        card.Width = width;
        card.Margin = new Thickness(0, 0, 10, 0);
        card.ClipToBounds = true;
        return card;
    }

    private static TextBlock Caption(string text) => Ui.Text(text.ToUpper(CultureInfo.CurrentCulture), 10.5, FontWeights.Bold, "Island.Secondary");

    private UIElement BuildClock()
    {
        clockTime = Ui.Text(string.Empty, 34, FontWeights.Light);
        clockDate = Ui.Text(string.Empty, 12.5, null, "Island.Secondary");
        clockDate.TextWrapping = TextWrapping.Wrap;
        return Ui.Column(4, Caption(Strings.Clock), clockTime, clockDate);
    }

    private UIElement BuildWorld()
    {
        worldList = new StackPanel();
        return Ui.Column(8, Caption(Strings.WorldClock), worldList);
    }

    private UIElement BuildWeather()
    {
        weatherGlyph = Ui.Glyph("", 26);
        weatherTemp = Ui.Text("—", 28, FontWeights.Light);
        weatherPlace = Ui.Text(string.Empty, 12, null, "Island.Secondary");
        weatherPlace.TextWrapping = TextWrapping.Wrap;
        return Ui.Column(6, Caption(Strings.WeatherTitle), Ui.Row(10, weatherGlyph, weatherTemp), weatherPlace);
    }

    private UIElement BuildBattery()
    {
        batteryText = Ui.Text("—", 30, FontWeights.Light);
        batteryState = Ui.Text(string.Empty, 12, null, "Island.Secondary");
        return Ui.Column(6, Caption(Strings.Battery), batteryText, batteryState);
    }

    private UIElement BuildStopwatch()
    {
        stopwatchText = Ui.Text("0:00.0", 28, FontWeights.Light);
        stopwatchText.SetResourceReference(TextBlock.FontFamilyProperty, "Island.MonoFont");
        stopwatchToggle = Ui.Pill(Strings.Start, ToggleStopwatch);
        var reset = Ui.Pill(Strings.Reset, () =>
        {
            stopwatch.Reset();
            UpdateStopwatch();
        });
        return Ui.Column(8, Caption(Strings.Stopwatch), stopwatchText, Ui.Row(6, stopwatchToggle, reset));
    }

    private UIElement BuildStats()
    {
        cpuText = Ui.Text("CPU —", 13.5, FontWeights.SemiBold);
        gpuText = Ui.Text("GPU —", 13.5, FontWeights.SemiBold);
        ramText = Ui.Text("RAM —", 13.5, FontWeights.SemiBold);
        foreach (var text in new[] { cpuText, gpuText, ramText })
        {
            text.SetResourceReference(TextBlock.FontFamilyProperty, "Island.MonoFont");
        }

        return Ui.Column(8, Caption(Strings.PcStats), cpuText, gpuText, ramText);
    }

    private UIElement BuildTodo()
    {
        todoList = new StackPanel();
        var input = Ui.Input(Strings.AddTask, box =>
        {
            var text = box.Text.Trim();
            if (text.Length == 0)
            {
                return;
            }

            todos.Items.Add(new TodoItem { Text = text });
            box.Text = string.Empty;
            SaveTodos();
        });
        input.Height = 28;
        input.FontSize = 12;
        RenderTodos();
        var list = Ui.Scroll(todoList);
        list.Height = 110;
        return Ui.Column(6, Caption(Strings.Todo), input, list);
    }

    private UIElement BuildPhoto()
    {
        photo = new Border { CornerRadius = new CornerRadius(16) };
        photoHint = Ui.Text(Strings.PhotosHint, 12, null, "Island.Secondary");
        photoHint.TextWrapping = TextWrapping.Wrap;
        photoHint.Margin = new Thickness(14);
        photoHint.VerticalAlignment = VerticalAlignment.Center;
        var grid = new Grid();
        grid.Children.Add(photo);
        grid.Children.Add(photoHint);
        LoadPhotoFolder();
        ShowRandomPhoto();
        return grid;
    }

    private void RenderTodos()
    {
        if (todoList is null)
        {
            return;
        }

        todoList.Children.Clear();
        foreach (var item in todos.Items.ToList())
        {
            var check = Ui.Check(item.Text, item.Done, done =>
            {
                item.Done = done;
                SaveTodos();
            });
            check.FontSize = 12.5;
            check.Opacity = item.Done ? 0.5 : 1;
            var remove = Ui.IconButton("", Strings.Remove, () =>
            {
                todos.Items.Remove(item);
                SaveTodos();
            }, 22, 9);
            todoList.Children.Add(Ui.Columns((Ui.Star(), check), (Ui.Auto, remove)).With(new Thickness(0, 2, 0, 2)));
        }
    }

    private void SaveTodos()
    {
        JsonFile.Save("todo", todos);
        RenderTodos();
    }

    private void ToggleStopwatch()
    {
        if (stopwatch.IsRunning)
        {
            stopwatch.Stop();
        }
        else
        {
            stopwatch.Start();
        }

        UpdateStopwatch();
    }

    private void UpdateStopwatch()
    {
        if (stopwatchText is null || stopwatchToggle is null)
        {
            return;
        }

        var elapsed = stopwatch.Elapsed;
        stopwatchText.Text = elapsed.TotalHours >= 1 ? elapsed.ToString(@"h\:mm\:ss") : $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 100}";
        stopwatchToggle.Content = stopwatch.IsRunning ? Strings.Pause : Strings.Start;
        ticker.Interval = stopwatch.IsRunning ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(1);
    }

    private void Tick()
    {
        var now = DateTime.Now;
        var fullTick = ticker.Interval >= TimeSpan.FromSeconds(1) || tickCount % 10 == 0;
        tickCount++;
        UpdateStopwatch();
        if (!fullTick)
        {
            return;
        }

        if (clockTime is not null && clockDate is not null)
        {
            clockTime.Text = now.ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern, CultureInfo.CurrentCulture);
            clockDate.Text = now.ToString("dddd, d MMMM", Strings.Culture);
        }

        UpdateWorld();
        UpdateStats();
        UpdateBattery();
        UpdateWeather();
        if (tickCount % 300 == 1)
        {
            ShowRandomPhoto();
        }
    }

    private void UpdateWorld()
    {
        if (worldList is null)
        {
            return;
        }

        worldList.Children.Clear();
        foreach (var id in services.Settings.Current.WorldClocks)
        {
            string city;
            string time;
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(id);
                city = id.Contains('/') ? id[(id.LastIndexOf('/') + 1)..].Replace('_', ' ') : zone.StandardName;
                time = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, zone).ToString("HH:mm", CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                city = id;
                time = "?";
            }

            var name = Ui.Text(city, 13);
            var value = Ui.Text(time, 13, FontWeights.SemiBold);
            value.SetResourceReference(TextBlock.FontFamilyProperty, "Island.MonoFont");
            worldList.Children.Add(Ui.Columns((Ui.Star(), name), (Ui.Auto, value)).With(new Thickness(0, 0, 0, 6)));
        }
    }

    private void UpdateStats()
    {
        if (cpuText is null || ramText is null || gpuText is null)
        {
            return;
        }

        var stats = services.Stats.Sample();
        cpuText.Text = $"CPU {stats.Cpu,4:0}%";
        gpuText.Text = stats.Gpu is { } gpu ? $"GPU {gpu,4:0}%" : "GPU    —";
        ramText.Text = $"RAM {stats.Ram,4:0}%";
        ramText.ToolTip = $"{stats.UsedGb:0.0} / {stats.TotalGb:0.0} GB";
    }

    private void UpdateBattery()
    {
        if (batteryText is null || batteryState is null || batteryCard is null)
        {
            return;
        }

        var status = Forms.SystemInformation.PowerStatus;
        var hasBattery = !status.BatteryChargeStatus.HasFlag(Forms.BatteryChargeStatus.NoSystemBattery)
            && status.BatteryChargeStatus != Forms.BatteryChargeStatus.Unknown;
        batteryCard.Visibility = hasBattery ? Visibility.Visible : Visibility.Collapsed;
        batteryText.Text = $"{status.BatteryLifePercent * 100:0}%";
        batteryState.Text = status.PowerLineStatus == Forms.PowerLineStatus.Online ? Strings.Charging : Strings.OnBattery;
    }

    private async void UpdateWeather()
    {
        if (weatherTemp is null || weatherPlace is null || weatherGlyph is null)
        {
            return;
        }

        var city = services.Settings.Current.WeatherCity;
        if (string.IsNullOrWhiteSpace(city))
        {
            weatherTemp.Text = "—";
            weatherPlace.Text = Strings.WeatherHint;
            return;
        }

        var stale = weather is null || weatherCity != city || DateTimeOffset.Now - weather.FetchedAt > TimeSpan.FromMinutes(30);
        if (stale && !fetchingWeather)
        {
            fetchingWeather = true;
            weatherCity = city;
            weather = await Weather.FetchAsync(city) ?? weather;
            fetchingWeather = false;
        }

        if (weather is null)
        {
            weatherPlace.Text = Strings.WeatherFailed;
            return;
        }

        weatherGlyph.Text = Weather.GlyphFor(weather.Code);
        weatherTemp.Text = $"{weather.Temperature:0}°";
        weatherPlace.Text = weather.Place;
    }

    private void LoadPhotoFolder()
    {
        photoFolder = services.Settings.Current.PhotosFolder;
        photoFiles = [];
        if (string.IsNullOrWhiteSpace(photoFolder) || !Directory.Exists(photoFolder))
        {
            return;
        }

        try
        {
            photoFiles = Directory.EnumerateFiles(photoFolder)
                .Where(f => PhotoExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Take(1000)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Warn("Reading the photos folder failed", ex);
        }
    }

    private void ShowRandomPhoto()
    {
        if (photo is null || photoHint is null)
        {
            return;
        }

        if (photoFiles.Count == 0)
        {
            photo.Background = null;
            photoHint.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 400;
            image.UriSource = new Uri(photoFiles[random.Next(photoFiles.Count)]);
            image.EndInit();
            image.Freeze();
            photo.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            photoHint.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Log.Warn("Showing a photo failed", ex);
        }
    }

    private void OnSettingsChanged(AppSettings previous, AppSettings current)
    {
        if (previous.PhotosFolder != current.PhotosFolder)
        {
            LoadPhotoFolder();
            ShowRandomPhoto();
        }
    }
}
