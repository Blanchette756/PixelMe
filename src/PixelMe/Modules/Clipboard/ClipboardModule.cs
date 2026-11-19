using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Localization;
using PixelMe.Native;
using WpfClipboard = System.Windows.Clipboard;

namespace PixelMe.Modules.Clipboard;

public enum ClipKind
{
    Text,
    Link,
    Image,
    Files,
}

public sealed class ClipItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ClipKind Kind { get; set; }

    public string Text { get; set; } = string.Empty;

    public string ImageFile { get; set; } = string.Empty;

    public List<string> Files { get; set; } = [];

    public bool Pinned { get; set; }

    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class ClipboardData
{
    public List<ClipItem> Items { get; set; } = [];
}

public sealed class ClipboardModule(ModuleServices services) : IslandModule
{
    private const int MaxRecent = 60;
    private const int MaxTextLength = 20000;
    private static readonly string ImageFolder = Path.Combine(AppPaths.LocalFolder, "clipboard");

    private readonly DispatcherTimer debounce = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };
    private ClipboardData data = new();
    private ClipKind? filter;
    private DateTime ignoreUntil;
    private StackPanel? list;
    private StackPanel? chips;
    private bool listening;

    public override string Id => "clipboard";

    public override string DisplayName => Strings.ClipboardTitle;

    public override string Glyph => "";

    public override int Priority => 20;

    public override Task InitializeAsync(CancellationToken cancellationToken)
    {
        data = JsonFile.Load<ClipboardData>("clipboard");
        data.Items.RemoveAll(i => i.Kind == ClipKind.Image && !File.Exists(i.ImageFile));
        debounce.Tick += (_, _) =>
        {
            debounce.Stop();
            _ = CaptureAsync();
        };
        services.Messages.MessageReceived += OnMessage;
        listening = NativeMethods.AddClipboardFormatListener(services.Messages.Handle);
        if (!listening)
        {
            Log.Warn("Clipboard history could not start listening");
        }

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        services.Messages.MessageReceived -= OnMessage;
        if (listening)
        {
            NativeMethods.RemoveClipboardFormatListener(services.Messages.Handle);
        }

        debounce.Stop();
    }

    protected override FrameworkElement BuildExpanded()
    {
        chips = new StackPanel { Orientation = Orientation.Horizontal };
        list = new StackPanel();
        var clear = Ui.Pill(Strings.ClearUnpinned, ClearUnpinned, "");
        DockPanel.SetDock(clear, Dock.Right);
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(clear);
        header.Children.Add(chips);

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(Ui.Scroll(list));
        Render();
        return root;
    }

    protected override void Render()
    {
        if (list is null || chips is null)
        {
            return;
        }

        chips.Children.Clear();
        AddChip(Strings.All, null);
        AddChip(Strings.TextKind, ClipKind.Text);
        AddChip(Strings.Links, ClipKind.Link);
        AddChip(Strings.Images, ClipKind.Image);
        AddChip(Strings.FilesKind, ClipKind.Files);

        list.Children.Clear();
        var visible = data.Items
            .Where(i => filter is null || i.Kind == filter)
            .OrderByDescending(i => i.Pinned)
            .ThenByDescending(i => i.CapturedAt)
            .ToList();

        if (visible.Count == 0)
        {
            list.Children.Add(Ui.Empty("", Strings.ClipboardEmpty, Strings.ClipboardEmptyHint).With(new Thickness(0, 24, 0, 0)));
            return;
        }

        foreach (var item in visible)
        {
            list.Children.Add(BuildRow(item));
        }
    }

    private static string Preview(ClipItem item) => item.Kind switch
    {
        ClipKind.Files => item.Files.Count == 1 ? Path.GetFileName(item.Files[0]) : $"{Path.GetFileName(item.Files[0])} +{item.Files.Count - 1}",
        ClipKind.Image => Strings.Image,
        _ => item.Text.ReplaceLineEndings(" ").Trim(),
    };

    private static UIElement Thumbnail(ClipItem item)
    {
        if (item.Kind == ClipKind.Image && File.Exists(item.ImageFile))
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 96;
                image.UriSource = new Uri(item.ImageFile);
                image.EndInit();
                image.Freeze();
                return new Border
                {
                    Width = 44,
                    Height = 32,
                    CornerRadius = new CornerRadius(6),
                    Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill },
                };
            }
            catch (Exception ex)
            {
                Log.Warn("Clipboard image preview failed", ex);
            }
        }

        var glyph = item.Kind switch
        {
            ClipKind.Link => "",
            ClipKind.Files => "",
            ClipKind.Image => "",
            _ => "",
        };
        var icon = Ui.Glyph(glyph, 15, "Island.Secondary");
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        return new Border { Width = 44, Height = 32, Child = icon };
    }

    private void AddChip(string text, ClipKind? kind)
    {
        chips!.Children.Add(Ui.Chip(text, filter == kind, () =>
        {
            filter = kind;
            Render();
        }));
    }

    private UIElement BuildRow(ClipItem item)
    {
        var text = Ui.Text(Preview(item), 13);
        text.Margin = new Thickness(10, 0, 8, 0);
        var pin = Ui.IconButton(item.Pinned ? "" : "", item.Pinned ? Strings.Unpin : Strings.Pin, () =>
        {
            item.Pinned = !item.Pinned;
            Save();
        });
        if (item.Pinned)
        {
            pin.SetResourceReference(Control.ForegroundProperty, "Island.Accent");
        }

        var remove = Ui.IconButton("", Strings.Remove, () => Remove(item));
        var row = Ui.Columns((Ui.Auto, Thumbnail(item)), (Ui.Star(), text), (Ui.Auto, pin), (Ui.Auto, remove));

        var button = new Button { Content = row, ToolTip = Strings.ClickToCopy };
        button.SetResourceReference(FrameworkElement.StyleProperty, "Island.RowButton");
        button.Click += (_, _) => CopyBack(item);
        return button;
    }

    private void OnMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            debounce.Stop();
            debounce.Start();
        }
    }

    private async Task CaptureAsync()
    {
        if (DateTime.UtcNow < ignoreUntil)
        {
            return;
        }

        IDataObject? source = null;
        for (var attempt = 0; attempt < 4 && source is null; attempt++)
        {
            try
            {
                source = WpfClipboard.GetDataObject();
            }
            catch (Exception ex) when (ex is COMException or ExternalException)
            {
                await Task.Delay(60);
            }
        }

        if (source is null || IsPrivate(source))
        {
            return;
        }

        try
        {
            var item = await ReadAsync(source);
            if (item is not null)
            {
                Add(item);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Reading the clipboard failed", ex);
        }
    }

    private static bool IsPrivate(IDataObject source)
    {
        if (source.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing") || source.GetDataPresent("Clipboard Viewer Ignore"))
        {
            return true;
        }

        return source.GetDataPresent("CanIncludeInClipboardHistory")
            && source.GetData("CanIncludeInClipboardHistory") is MemoryStream stream
            && stream.Length >= 4
            && BitConverter.ToInt32(stream.ToArray(), 0) == 0;
    }

    private static async Task<ClipItem?> ReadAsync(IDataObject source)
    {
        if (source.GetDataPresent(DataFormats.FileDrop) && source.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            return new ClipItem { Kind = ClipKind.Files, Files = files.ToList() };
        }

        if (source.GetDataPresent(DataFormats.Bitmap) && WpfClipboard.GetImage() is { } bitmap)
        {
            var frozen = new WriteableBitmap(bitmap);
            frozen.Freeze();
            var file = await Task.Run(() => SaveImage(frozen));
            return new ClipItem { Kind = ClipKind.Image, ImageFile = file };
        }

        if (source.GetDataPresent(DataFormats.UnicodeText) && source.GetData(DataFormats.UnicodeText) is string text && !string.IsNullOrWhiteSpace(text))
        {
            text = text.Length > MaxTextLength ? text[..MaxTextLength] : text;
            var trimmed = text.Trim();
            var isLink = !trimmed.Contains(' ')
                && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            return new ClipItem { Kind = isLink ? ClipKind.Link : ClipKind.Text, Text = text };
        }

        return null;
    }

    private static string SaveImage(BitmapSource bitmap)
    {
        Directory.CreateDirectory(ImageFolder);
        BitmapSource output = bitmap;
        var largest = Math.Max(bitmap.PixelWidth, bitmap.PixelHeight);
        if (largest > 1600)
        {
            var scale = 1600.0 / largest;
            output = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
        }

        var path = Path.Combine(ImageFolder, $"{Guid.NewGuid():N}.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(output));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    private void Add(ClipItem item)
    {
        var duplicate = data.Items.FirstOrDefault(existing => existing.Kind == item.Kind && item.Kind switch
        {
            ClipKind.Files => existing.Files.SequenceEqual(item.Files, StringComparer.OrdinalIgnoreCase),
            ClipKind.Image => false,
            _ => existing.Text == item.Text,
        });

        if (duplicate is not null)
        {
            duplicate.CapturedAt = DateTimeOffset.Now;
        }
        else
        {
            data.Items.Add(item);
        }

        var overflow = data.Items.Where(i => !i.Pinned).OrderByDescending(i => i.CapturedAt).Skip(MaxRecent).ToList();
        foreach (var old in overflow)
        {
            Delete(old);
        }

        Save();
    }

    private void Remove(ClipItem item)
    {
        Delete(item);
        Save();
    }

    private void ClearUnpinned()
    {
        foreach (var item in data.Items.Where(i => !i.Pinned).ToList())
        {
            Delete(item);
        }

        Save();
    }

    private void Delete(ClipItem item)
    {
        data.Items.Remove(item);
        if (item.Kind == ClipKind.Image)
        {
            try
            {
                File.Delete(item.ImageFile);
            }
            catch (Exception ex)
            {
                Log.Warn("Removing a clipboard image failed", ex);
            }
        }
    }

    private void CopyBack(ClipItem item)
    {
        ignoreUntil = DateTime.UtcNow.AddMilliseconds(800);
        try
        {
            switch (item.Kind)
            {
                case ClipKind.Files:
                    var files = new StringCollection();
                    files.AddRange(item.Files.Where(File.Exists).Concat(item.Files.Where(Directory.Exists)).Distinct().ToArray());
                    WpfClipboard.SetFileDropList(files);
                    break;
                case ClipKind.Image:
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(item.ImageFile);
                    image.EndInit();
                    WpfClipboard.SetImage(image);
                    break;
                default:
                    WpfClipboard.SetText(item.Text);
                    break;
            }

            item.CapturedAt = DateTimeOffset.Now;
            Save();
        }
        catch (Exception ex)
        {
            Log.Warn("Copying a clipboard item back failed", ex);
        }
    }

    private void Save()
    {
        JsonFile.Save("clipboard", data);
        Render();
    }
}
