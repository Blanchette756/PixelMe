using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Localization;
using PixelMe.Native;
using QRCoder;

namespace PixelMe.Modules.Shelf;

public sealed class ShelfData
{
    public List<string> Files { get; set; } = [];
}

public sealed class ShelfModule : IslandModule, IFileDropTarget
{
    private static readonly Dictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly PhoneShare share = new();
    private readonly DispatcherTimer shareTimeout = new() { Interval = TimeSpan.FromMinutes(10) };
    private ShelfData data = new();
    private WrapPanel? tiles;
    private Grid? content;
    private Grid? qrOverlay;
    private TextBlock? countText;
    private Point dragStart;

    public override string Id => "shelf";

    public override string DisplayName => Strings.FilesShelf;

    public override string Glyph => "";

    public override int Priority => 30;

    public override Task InitializeAsync(CancellationToken cancellationToken)
    {
        data = JsonFile.Load<ShelfData>("shelf");
        data.Files = data.Files.Where(p => File.Exists(p) || Directory.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        shareTimeout.Tick += (_, _) => StopSharing();
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        shareTimeout.Stop();
        share.Dispose();
    }

    public bool CanAccept(IDataObject data) => data.GetDataPresent(DataFormats.FileDrop);

    public void Accept(IDataObject dropped)
    {
        if (dropped.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        foreach (var path in paths)
        {
            if (!data.Files.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                data.Files.Add(path);
            }
        }

        Save();
    }

    protected override FrameworkElement BuildExpanded()
    {
        countText = Ui.Text(string.Empty, 12.5, FontWeights.SemiBold, "Island.Secondary");
        var actions = Ui.Row(
            6,
            Ui.Pill(Strings.SendToPhone, StartSharing, ""),
            Ui.Pill(Strings.Clear, () =>
            {
                data.Files.Clear();
                Save();
            }, ""));
        DockPanel.SetDock(actions, Dock.Right);
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(actions);
        header.Children.Add(countText);

        tiles = new WrapPanel();
        content = new Grid();
        content.Children.Add(Ui.Scroll(tiles));

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(content);
        root.IsVisibleChanged += (_, _) =>
        {
            if (!root.IsVisible)
            {
                StopSharing();
            }
        };

        Render();
        return root;
    }

    protected override void Render()
    {
        if (tiles is null || countText is null)
        {
            return;
        }

        countText.Text = string.Format(Strings.FilesCount, data.Files.Count);
        tiles.Children.Clear();
        if (data.Files.Count == 0)
        {
            var empty = Ui.Empty("", Strings.ShelfEmpty, Strings.ShelfEmptyHint);
            empty.Width = 580;
            empty.Margin = new Thickness(0, 18, 0, 0);
            tiles.Children.Add(empty);
            return;
        }

        foreach (var path in data.Files)
        {
            tiles.Children.Add(BuildTile(path));
        }
    }

    private static ImageSource? IconFor(string path)
    {
        var extension = Directory.Exists(path) ? "<folder>" : Path.GetExtension(path);
        var key = extension is ".exe" or ".lnk" or ".ico" or "" ? path : extension;
        if (IconCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        ImageSource? image = null;
        var info = new NativeMethods.SHFILEINFO();
        var result = NativeMethods.SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>(), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);
        if (result != IntPtr.Zero && info.hIcon != IntPtr.Zero)
        {
            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                image = source;
            }
            finally
            {
                NativeMethods.DestroyIcon(info.hIcon);
            }
        }

        IconCache[key] = image;
        return image;
    }

    private UIElement BuildTile(string path)
    {
        var icon = new Image { Source = IconFor(path), Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Center };
        var name = Ui.Text(Path.GetFileName(path.TrimEnd('\\')), 11.5);
        name.TextWrapping = TextWrapping.Wrap;
        name.TextAlignment = TextAlignment.Center;
        name.MaxHeight = 30;
        name.Margin = new Thickness(0, 6, 0, 0);

        var remove = Ui.IconButton("", Strings.Remove, () =>
        {
            data.Files.Remove(path);
            Save();
        }, 20, 9);
        remove.HorizontalAlignment = HorizontalAlignment.Right;
        remove.VerticalAlignment = VerticalAlignment.Top;
        remove.Opacity = 0.6;

        var stack = Ui.Column(0, icon, name);
        stack.Margin = new Thickness(6, 10, 6, 6);
        var body = new Grid { Width = 92, Height = 92, Background = Brushes.Transparent, ToolTip = path, Cursor = Cursors.Hand };
        body.Children.Add(stack);
        body.Children.Add(remove);

        var card = Ui.Card(body, 0);
        card.Margin = new Thickness(0, 0, 8, 8);
        body.PreviewMouseLeftButtonDown += (_, e) =>
        {
            dragStart = e.GetPosition(null);
            if (e.ClickCount == 2)
            {
                ShellLauncher.Open(path);
            }
        };
        body.MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var delta = e.GetPosition(null) - dragStart;
            if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            {
                return;
            }

            try
            {
                DragDrop.DoDragDrop(body, new DataObject(DataFormats.FileDrop, new[] { path }), DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
            }
            catch (Exception ex)
            {
                Log.Warn("Dragging a file off the shelf failed", ex);
            }
        };
        return card;
    }

    private void StartSharing()
    {
        if (content is null)
        {
            return;
        }

        var existing = data.Files.Where(File.Exists).ToList();
        if (existing.Count == 0)
        {
            ShowOverlay(Ui.Text(Strings.ShareNoFiles, 13, null, "Island.Secondary"));
            return;
        }

        try
        {
            var url = share.Start(existing);
            var generator = new QRCodeGenerator();
            using var code = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(code).GetGraphic(6);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(png);
            image.EndInit();
            image.Freeze();

            var qr = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = Brushes.White,
                Padding = new Thickness(6),
                Child = new Image { Source = image, Width = 150, Height = 150 },
            };
            var text = Ui.Column(
                6,
                Ui.Text(Strings.ScanWithPhone, 15, FontWeights.SemiBold),
                new TextBlock { Text = Strings.ScanHint, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = (Brush)Application.Current.Resources["Island.Secondary"], MaxWidth = 300 },
                Ui.Text(url, 11, null, "Island.Secondary"),
                Ui.Pill(Strings.StopSharing, StopSharing, "").With(new Thickness(0, 6, 0, 0)));
            text.Margin = new Thickness(18, 0, 0, 0);
            text.VerticalAlignment = VerticalAlignment.Center;
            ((FrameworkElement)text.Children[^1]).HorizontalAlignment = HorizontalAlignment.Left;
            ShowOverlay(Ui.Row(0, qr, text));
            shareTimeout.Stop();
            shareTimeout.Start();
        }
        catch (Exception ex)
        {
            Log.Warn("Starting phone sharing failed", ex);
            ShowOverlay(Ui.Text(Strings.ShareFailed, 13, null, "Island.Secondary"));
        }
    }

    private void ShowOverlay(UIElement inner)
    {
        if (content is null)
        {
            return;
        }

        if (qrOverlay is not null)
        {
            content.Children.Remove(qrOverlay);
        }

        var close = Ui.IconButton("", Strings.Close, StopSharing);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.VerticalAlignment = VerticalAlignment.Top;
        content.Children[0].Visibility = Visibility.Hidden;
        qrOverlay = new Grid { Background = Brushes.Transparent };
        if (inner is FrameworkElement element)
        {
            element.HorizontalAlignment = HorizontalAlignment.Center;
            element.VerticalAlignment = VerticalAlignment.Center;
        }

        qrOverlay.Children.Add(inner);
        qrOverlay.Children.Add(close);
        content.Children.Add(qrOverlay);
    }

    private void StopSharing()
    {
        shareTimeout.Stop();
        share.Stop();
        if (qrOverlay is not null && content is not null)
        {
            content.Children.Remove(qrOverlay);
            content.Children[0].Visibility = Visibility.Visible;
            qrOverlay = null;
        }
    }

    private void Save()
    {
        JsonFile.Save("shelf", data);
        Render();
    }
}
