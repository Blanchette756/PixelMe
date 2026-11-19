using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PixelMe.Localization;
using PixelMe.Settings;

namespace PixelMe.Modules.Web;

public sealed class WebModule(ModuleServices services) : IslandModule
{
    private UniformGrid? grid;
    private TextBox? address;

    public override string Id => "web";

    public override string DisplayName => Strings.WebDrawer;

    public override string Glyph => "";

    public override int Priority => 100;

    public override Task InitializeAsync(CancellationToken cancellationToken)
    {
        services.Settings.Changed += OnSettingsChanged;
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        services.Settings.Changed -= OnSettingsChanged;
    }

    public static string? Normalize(string input)
    {
        var text = input.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var absolute) && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.ToString();
        }

        if (!text.Contains(' ') && text.Contains('.') && Uri.TryCreate("https://" + text, UriKind.Absolute, out var guessed))
        {
            return guessed.ToString();
        }

        return "https://www.google.com/search?q=" + Uri.EscapeDataString(text);
    }

    protected override FrameworkElement BuildExpanded()
    {
        address = Ui.Input(Strings.SearchOrAddress, box => Open(box.Text));
        var go = Ui.Pill(Strings.Open, () => Open(address.Text), "").With(new Thickness(8, 0, 0, 0));
        var pin = Ui.Pill(Strings.Pin, PinAddress, "").With(new Thickness(6, 0, 0, 0));
        var top = Ui.Columns((Ui.Star(), address), (Ui.Auto, go), (Ui.Auto, pin));
        top.Margin = new Thickness(0, 0, 0, 10);

        grid = new UniformGrid { Columns = 4 };
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(Ui.Scroll(grid));
        Render();
        return root;
    }

    protected override void Render()
    {
        if (grid is null)
        {
            return;
        }

        grid.Children.Clear();
        foreach (var site in services.Settings.Current.PinnedSites)
        {
            var host = Uri.TryCreate(site, UriKind.Absolute, out var uri) ? uri.Host.Replace("www.", string.Empty) : site;
            var letter = Ui.Text(host.Length > 0 ? host[..1].ToUpperInvariant() : "?", 16, FontWeights.Bold);
            var avatar = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(10), Child = letter };
            avatar.SetResourceReference(Border.BackgroundProperty, "Island.Subtle");
            letter.HorizontalAlignment = HorizontalAlignment.Center;
            var name = Ui.Text(host, 12.5, FontWeights.SemiBold).With(new Thickness(8, 0, 0, 0));
            var content = Ui.Columns((Ui.Auto, avatar), (Ui.Star(), name));
            var tile = Ui.Tile(content, () => services.Web.Open(site, host));
            tile.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            tile.Margin = new Thickness(0, 0, 8, 8);
            tile.Padding = new Thickness(8);
            tile.ToolTip = site;
            tile.ContextMenu = new ContextMenu();
            var remove = new MenuItem { Header = Strings.Unpin };
            remove.Click += (_, _) => services.Settings.Update(s => s.PinnedSites.Remove(site));
            tile.ContextMenu.Items.Add(remove);
            grid.Children.Add(tile);
        }
    }

    private void Open(string text)
    {
        var url = Normalize(text);
        if (url is not null)
        {
            services.Web.Open(url, text.Trim());
        }
    }

    private void PinAddress()
    {
        if (address is null)
        {
            return;
        }

        var url = Normalize(address.Text);
        if (url is null || url.StartsWith("https://www.google.com/search", StringComparison.Ordinal))
        {
            return;
        }

        services.Settings.Update(s =>
        {
            if (!s.PinnedSites.Contains(url))
            {
                s.PinnedSites.Add(url);
            }
        });
        address.Text = string.Empty;
    }

    private void OnSettingsChanged(AppSettings previous, AppSettings current)
    {
        if (!previous.PinnedSites.SequenceEqual(current.PinnedSites))
        {
            Render();
        }
    }
}
