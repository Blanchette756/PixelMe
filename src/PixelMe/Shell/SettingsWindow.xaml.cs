using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using PixelMe.Infrastructure;
using PixelMe.Localization;
using PixelMe.Settings;

namespace PixelMe.Shell;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore store;
    private bool loading;

    public SettingsWindow(SettingsStore store)
    {
        this.store = store;
        InitializeComponent();
        LoadLogo();
        ApplyText();

        ThemeBox.SelectionChanged += (_, _) => Commit(s => s.Theme = Selected(ThemeBox, s.Theme));
        StyleBox.SelectionChanged += (_, _) => Commit(s => s.Style = Selected(StyleBox, s.Style));
        LanguageBox.SelectionChanged += (_, _) => Commit(s => s.Language = Selected(LanguageBox, s.Language));
        StartupBox.Click += (_, _) => Commit(s => s.StartWithWindows = StartupBox.IsChecked == true);
        FullscreenBox.Click += (_, _) => Commit(s => s.HideInFullscreen = FullscreenBox.IsChecked == true);
        GitHubButton.Click += (_, _) => ShellLauncher.Open(AppInfo.RepositoryUrl);
        LogButton.Click += (_, _) => ShellLauncher.OpenFolder(AppPaths.LogFolder);

        store.Changed += OnStoreChanged;
        Closed += (_, _) => store.Changed -= OnStoreChanged;
    }

    public void ApplyText()
    {
        loading = true;
        var current = store.Current;

        Title = $"PixelMe - {Strings.Settings}";
        VersionText.Text = $"{Strings.Version} {AppInfo.Version}";
        AppearanceHeader.Text = Strings.Appearance;
        BehaviorHeader.Text = Strings.Behavior;
        AboutHeader.Text = Strings.About;
        ThemeLabel.Text = Strings.Theme;
        StyleLabel.Text = Strings.Style;
        LanguageLabel.Text = Strings.Language;
        StartupBox.Content = Strings.StartWithWindows;
        FullscreenBox.Content = Strings.HideInFullscreen;
        LicenseText.Text = Strings.License;
        GitHubButton.Content = Strings.ViewOnGitHub;
        LogButton.Content = Strings.OpenLogFolder;

        Fill(ThemeBox, current.Theme,
            (IslandThemeKind.Glass, Strings.ThemeGlass),
            (IslandThemeKind.Black, Strings.ThemeBlack),
            (IslandThemeKind.Graphite, Strings.ThemeGraphite),
            (IslandThemeKind.Light, Strings.ThemeLight));

        Fill(StyleBox, current.Style,
            (IslandStyle.Floating, Strings.StyleFloating),
            (IslandStyle.Notch, Strings.StyleNotch));

        Fill(LanguageBox, current.Language,
            (AppLanguage.Auto, Strings.LanguageAuto),
            (AppLanguage.English, "English"),
            (AppLanguage.Indonesian, "Bahasa Indonesia"));

        StartupBox.IsChecked = current.StartWithWindows;
        FullscreenBox.IsChecked = current.HideInFullscreen;
        loading = false;
    }

    private static void Fill<T>(ComboBox box, T selected, params (T Value, string Label)[] items)
        where T : struct, Enum
    {
        box.Items.Clear();
        foreach (var (value, label) in items)
        {
            var item = new ComboBoxItem { Content = label, Tag = value };
            box.Items.Add(item);
            if (EqualityComparer<T>.Default.Equals(value, selected))
            {
                box.SelectedItem = item;
            }
        }
    }

    private static T Selected<T>(ComboBox box, T fallback)
        where T : struct, Enum
    {
        return box.SelectedItem is ComboBoxItem { Tag: T value } ? value : fallback;
    }

    private void Commit(Action<AppSettings> change)
    {
        if (!loading)
        {
            store.Update(change);
        }
    }

    private void OnStoreChanged(AppSettings previous, AppSettings current)
    {
        loading = true;
        StartupBox.IsChecked = current.StartWithWindows;
        FullscreenBox.IsChecked = current.HideInFullscreen;
        loading = false;
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
