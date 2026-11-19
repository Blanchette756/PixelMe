using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PixelMe.Infrastructure;
using PixelMe.Localization;
using PixelMe.Settings;

namespace PixelMe.Shell;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore store;
    private CheckBox? startupBox;

    public SettingsWindow(SettingsStore store)
    {
        this.store = store;
        InitializeComponent();
        LoadLogo();
        ApplyText();
        GitHubButton.Click += (_, _) => ShellLauncher.Open(AppInfo.RepositoryUrl);
        LogButton.Click += (_, _) => ShellLauncher.OpenFolder(AppPaths.LogFolder);
        store.Changed += OnStoreChanged;
        Closed += (_, _) => store.Changed -= OnStoreChanged;
    }

    public void ApplyText()
    {
        var current = store.Current;
        Title = $"PixelMe - {Strings.Settings}";
        VersionText.Text = $"{Strings.Version} {AppInfo.Version}";
        AppearanceHeader.Text = Strings.Appearance;
        BehaviorHeader.Text = Strings.Behavior;
        FeaturesHeader.Text = Strings.Features;
        StreamHeader.Text = Strings.StreamTitle;
        AboutHeader.Text = Strings.About;
        LicenseText.Text = Strings.License;
        GitHubButton.Content = Strings.ViewOnGitHub;
        LogButton.Content = Strings.OpenLogFolder;

        AppearancePanel.Children.Clear();
        AppearancePanel.Children.Add(Combo(Strings.Theme, current.Theme, (s, v) => s.Theme = v,
            (IslandThemeKind.Glass, Strings.ThemeGlass),
            (IslandThemeKind.Black, Strings.ThemeBlack),
            (IslandThemeKind.Graphite, Strings.ThemeGraphite),
            (IslandThemeKind.Light, Strings.ThemeLight)));
        AppearancePanel.Children.Add(Combo(Strings.Style, current.Style, (s, v) => s.Style = v,
            (IslandStyle.Notch, Strings.StyleNotch),
            (IslandStyle.Floating, Strings.StyleFloating)));

        BehaviorPanel.Children.Clear();
        startupBox = Check(Strings.StartWithWindows, current.StartWithWindows, (s, v) => s.StartWithWindows = v);
        BehaviorPanel.Children.Add(startupBox);
        BehaviorPanel.Children.Add(Check(Strings.OpenOnHoverSetting, current.OpenOnHover, (s, v) => s.OpenOnHover = v));
        BehaviorPanel.Children.Add(Check(Strings.MicHotkeySetting, current.MicHotkey, (s, v) => s.MicHotkey = v));
        BehaviorPanel.Children.Add(Combo(Strings.Fullscreen, current.Fullscreen, (s, v) => s.Fullscreen = v,
            (FullscreenBehavior.Strip, Strings.FullscreenStrip),
            (FullscreenBehavior.Hide, Strings.FullscreenHide),
            (FullscreenBehavior.Stay, Strings.FullscreenStay)));
        BehaviorPanel.Children.Add(Combo(Strings.Language, current.Language, (s, v) => s.Language = v,
            (AppLanguage.Auto, Strings.LanguageAuto),
            (AppLanguage.English, "English"),
            (AppLanguage.Indonesian, "Bahasa Indonesia")));

        FeaturesPanel.Children.Clear();
        FeaturesPanel.Children.Add(Check(Strings.LyricsSetting, current.Lyrics, (s, v) => s.Lyrics = v));
        FeaturesPanel.Children.Add(Field(Strings.WeatherCity, current.WeatherCity, (s, v) => s.WeatherCity = v.Trim()));
        FeaturesPanel.Children.Add(Field(Strings.WorldClocksSetting, string.Join(", ", current.WorldClocks), (s, v) =>
            s.WorldClocks = v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()));
        FeaturesPanel.Children.Add(FolderField(Strings.PhotosFolder, current.PhotosFolder));
        FeaturesPanel.Children.Add(Field(Strings.PinnedSitesSetting, string.Join(Environment.NewLine, current.PinnedSites), (s, v) =>
            s.PinnedSites = v.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(), multiline: true));

        StreamPanel.Children.Clear();
        StreamPanel.Children.Add(Field(Strings.TwitchChannel, current.TwitchChannel, (s, v) => s.TwitchChannel = v.Trim()));
        StreamPanel.Children.Add(Field(Strings.YouTubeLive, current.YouTubeLive, (s, v) => s.YouTubeLive = v.Trim()));
        StreamPanel.Children.Add(Check(Strings.ObsEnable, current.ObsEnabled, (s, v) => s.ObsEnabled = v));
        StreamPanel.Children.Add(Field(Strings.ObsHost, current.ObsHost, (s, v) => s.ObsHost = v.Trim()));
        StreamPanel.Children.Add(Field(Strings.ObsPort, current.ObsPort.ToString(CultureInfo.InvariantCulture), (s, v) =>
            s.ObsPort = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ? port : 4455));
        StreamPanel.Children.Add(PasswordField(Strings.ObsPassword));
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        Margin = new Thickness(0, 0, 0, 6),
        TextWrapping = TextWrapping.Wrap,
    };

    private FrameworkElement Combo<T>(string label, T selected, Action<AppSettings, T> apply, params (T Value, string Text)[] items)
        where T : struct, Enum
    {
        var box = new ComboBox { Width = 220 };
        foreach (var (value, text) in items)
        {
            var item = new ComboBoxItem { Content = text, Tag = value };
            box.Items.Add(item);
            if (EqualityComparer<T>.Default.Equals(value, selected))
            {
                box.SelectedItem = item;
            }
        }

        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ComboBoxItem { Tag: T value })
            {
                Dispatcher.InvokeAsync(() => store.Update(s => apply(s, value)));
            }
        };

        var caption = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 0) };
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(box, 1);
        grid.Children.Add(caption);
        grid.Children.Add(box);
        return grid;
    }

    private CheckBox Check(string label, bool value, Action<AppSettings, bool> apply)
    {
        var box = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 6, 0, 6) };
        box.Click += (_, _) => store.Update(s => apply(s, box.IsChecked == true));
        return box;
    }

    private FrameworkElement Field(string label, string value, Action<AppSettings, string> apply, bool multiline = false)
    {
        var box = new TextBox
        {
            Text = value,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = multiline ? 90 : 0,
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
        };
        box.LostFocus += (_, _) =>
        {
            if (box.Text != value)
            {
                value = box.Text;
                store.Update(s => apply(s, box.Text));
            }
        };
        return new StackPanel { Margin = new Thickness(0, 8, 0, 4), Children = { Label(label), box } };
    }

    private FrameworkElement FolderField(string label, string value)
    {
        var box = new TextBox { Text = value, IsReadOnly = true };
        var browse = new Button { Content = Strings.Browse, Margin = new Thickness(8, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            var dialog = new OpenFolderDialog { InitialDirectory = Directory.Exists(box.Text) ? box.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) };
            if (dialog.ShowDialog(this) == true)
            {
                box.Text = dialog.FolderName;
                store.Update(s => s.PhotosFolder = dialog.FolderName);
            }
        };
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(browse, 1);
        row.Children.Add(box);
        row.Children.Add(browse);
        return new StackPanel { Margin = new Thickness(0, 8, 0, 4), Children = { Label(label), row } };
    }

    private FrameworkElement PasswordField(string label)
    {
        var box = new PasswordBox { Password = Secrets.Unprotect(store.Current.ObsPasswordProtected) };
        box.LostFocus += (_, _) =>
        {
            var protectedValue = Secrets.Protect(box.Password);
            if (Secrets.Unprotect(store.Current.ObsPasswordProtected) != box.Password)
            {
                store.Update(s => s.ObsPasswordProtected = protectedValue);
            }
        };
        return new StackPanel { Margin = new Thickness(0, 8, 0, 4), Children = { Label(label), box } };
    }

    private void OnStoreChanged(AppSettings previous, AppSettings current)
    {
        if (startupBox is not null && startupBox.IsChecked != current.StartWithWindows)
        {
            startupBox.IsChecked = current.StartWithWindows;
        }
    }

    private void LoadLogo()
    {
        try
        {
            var decoder = BitmapDecoder.Create(
                new Uri("pack://application:,,,/Assets/PixelMe.ico"),
                BitmapCreateOptions.None,
                BitmapCacheOption.OnLoad);
            LogoImage.Source = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        }
        catch (Exception ex)
        {
            Log.Warn("Logo could not be loaded", ex);
        }
    }
}
