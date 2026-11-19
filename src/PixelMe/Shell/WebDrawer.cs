using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using PixelMe.Infrastructure;
using PixelMe.Island;
using PixelMe.Localization;
using PixelMe.Native;

namespace PixelMe.Shell;

public sealed class WebDrawer : IDisposable
{
    private WebDrawerWindow? window;

    public void Open(string url, string title)
    {
        if (window is null)
        {
            window = new WebDrawerWindow();
            window.Closed += (_, _) => window = null;
            window.Show();
        }

        window.Activate();
        window.Navigate(url, title);
    }

    public void Dispose()
    {
        window?.Close();
    }
}

internal sealed class WebDrawerWindow : Window
{
    private static readonly string DataFolder = Path.Combine(AppPaths.LocalFolder, "WebView2");
    private readonly WebView2 view = new() { DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 18, 19, 24) };
    private readonly TextBlock title;
    private readonly TextBlock failure;
    private Task? ready;

    public WebDrawerWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        ShowInTaskbar = false;
        Topmost = true;
        Width = 480;
        Height = 640;
        MinWidth = 320;
        MinHeight = 320;
        Background = new SolidColorBrush(Color.FromRgb(18, 19, 24));
        Title = "PixelMe Web";
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = IslandMetrics.Expanded.Height + 18;
        FontFamily = (FontFamily)Application.Current.FindResource("Island.Font");

        title = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(14, 0, 8, 0),
        };

        failure = new TextBlock
        {
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24),
            Visibility = Visibility.Collapsed,
            Text = Strings.WebViewMissing,
        };

        var header = new DockPanel { Height = 40, Background = new SolidColorBrush(Color.FromRgb(28, 29, 36)) };
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 6, 0) };
        buttons.Children.Add(HeaderButton("", Strings.Back, () => { if (view.CoreWebView2?.CanGoBack == true) view.CoreWebView2.GoBack(); }));
        buttons.Children.Add(HeaderButton("", Strings.Reload, () => view.CoreWebView2?.Reload()));
        buttons.Children.Add(HeaderButton("", Strings.OpenInBrowser, () => { if (view.Source is not null) ShellLauncher.Open(view.Source.ToString()); }));
        buttons.Children.Add(HeaderButton("", Strings.Close, Close));
        DockPanel.SetDock(buttons, System.Windows.Controls.Dock.Right);
        header.Children.Add(buttons);
        header.Children.Add(title);

        var body = new Grid();
        body.Children.Add(view);
        body.Children.Add(failure);

        var root = new DockPanel();
        DockPanel.SetDock(header, System.Windows.Controls.Dock.Top);
        root.Children.Add(header);
        root.Children.Add(body);
        Content = root;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    public async void Navigate(string url, string caption)
    {
        title.Text = caption;
        try
        {
            ready ??= InitializeAsync();
            await ready;
            view.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            Log.Error("The web drawer could not start", ex);
            view.Visibility = Visibility.Collapsed;
            failure.Visibility = Visibility.Visible;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var round = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        view.Dispose();
    }

    private static Button HeaderButton(string glyph, string tooltip, Action click)
    {
        var button = new Button { Content = glyph, ToolTip = tooltip, Width = 32, Height = 32, Foreground = Brushes.White };
        button.SetResourceReference(StyleProperty, "Island.IconButton");
        button.Foreground = Brushes.White;
        button.Click += (_, _) => click();
        return button;
    }

    private async Task InitializeAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(null, DataFolder);
        await view.EnsureCoreWebView2Async(environment);
        view.CoreWebView2.DocumentTitleChanged += (_, _) => title.Text = view.CoreWebView2.DocumentTitle;
        view.CoreWebView2.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            view.CoreWebView2.Navigate(args.Uri);
        };
    }
}
