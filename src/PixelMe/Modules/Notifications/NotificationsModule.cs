using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Localization;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace PixelMe.Modules.Notifications;

public sealed class NoticeItem
{
    public uint Id { get; set; }

    public string App { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }
}

public sealed class NoticeData
{
    public List<NoticeItem> Items { get; set; } = [];
}

public sealed class NotificationsModule(ModuleServices services) : IslandModule
{
    private const int MaxItems = 50;

    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer poll = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer peekEnd = new() { Interval = TimeSpan.FromSeconds(6) };
    private readonly HashSet<uint> seen = [];
    private UserNotificationListener? listener;
    private NoticeData data = new();
    private NoticeItem? peeking;
    private string status = string.Empty;
    private bool polling;
    private StackPanel? list;
    private ContentControl? header;
    private TextBlock? compactApp;
    private TextBlock? compactTitle;

    public override string Id => "notifications";

    public override string DisplayName => Strings.NotificationsTitle;

    public override string Glyph => "";

    public override int Priority => 80;

    public override bool HasActivity => peeking is not null;

    public override int ActivityRank => 5;

    public override double CompactWidth => 340;

    public override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        data = JsonFile.Load<NoticeData>("notifications");
        poll.Tick += async (_, _) => await PollAsync();
        peekEnd.Tick += (_, _) =>
        {
            peekEnd.Stop();
            peeking = null;
            RaiseActivityChanged();
        };

        try
        {
            listener = UserNotificationListener.Current;
            if (services.Settings.Current.Notifications && listener.GetAccessStatus() == UserNotificationListenerAccessStatus.Allowed)
            {
                await StartAsync(markExistingSeen: true);
            }
        }
        catch (Exception ex)
        {
            listener = null;
            status = Strings.NotificationsUnsupported;
            Log.Warn("Reading Windows notifications is not supported here", ex);
        }
    }

    public override void Dispose()
    {
        poll.Stop();
        peekEnd.Stop();
    }

    protected override FrameworkElement BuildCompact()
    {
        compactApp = Ui.Text(string.Empty, 12, FontWeights.Bold, "Island.Accent");
        compactTitle = Ui.Text(string.Empty, 13, FontWeights.SemiBold);
        compactTitle.Margin = new Thickness(10, 0, 0, 0);
        var row = Ui.Columns((Ui.Auto, Ui.Glyph("", 13, "Island.Accent").With(new Thickness(0, 0, 8, 0))), (Ui.Auto, compactApp), (Ui.Star(), compactTitle));
        row.Margin = new Thickness(16, 0, 16, 0);
        UpdateCompact();
        return row;
    }

    protected override FrameworkElement BuildExpanded()
    {
        header = new ContentControl { Margin = new Thickness(0, 0, 0, 8), Focusable = false };
        list = new StackPanel();
        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(Ui.Scroll(list));
        Render();
        return root;
    }

    protected override void Render()
    {
        UpdateCompact();
        if (list is null || header is null)
        {
            return;
        }

        if (!polling)
        {
            header.Content = listener is null
                ? Ui.Text(status, 12.5, null, "Island.Secondary")
                : Ui.Row(10, Ui.Text(Strings.NotificationsOff, 12.5, null, "Island.Secondary"), Ui.Pill(Strings.Enable, () => _ = EnableAsync(), ""));
        }
        else
        {
            var clear = Ui.Pill(Strings.Clear, () =>
            {
                data.Items.Clear();
                Save();
            }, "");
            clear.HorizontalAlignment = HorizontalAlignment.Right;
            header.Content = clear;
        }

        list.Children.Clear();
        if (data.Items.Count == 0)
        {
            list.Children.Add(Ui.Text(Strings.NoNotifications, 12.5, null, "Island.Secondary").With(new Thickness(4, 6, 0, 0)));
            return;
        }

        foreach (var item in data.Items.OrderByDescending(i => i.At))
        {
            var app = Ui.Text(item.App, 11.5, FontWeights.Bold, "Island.Accent");
            var time = Ui.Text(item.At.ToLocalTime().ToString("HH:mm"), 11.5, null, "Island.Secondary");
            var top = Ui.Columns((Ui.Star(), app), (Ui.Auto, time));
            var title = Ui.Text(item.Title, 13.5, FontWeights.SemiBold);
            var body = Ui.Text(item.Body, 12.5, null, "Island.Secondary");
            list.Children.Add(Ui.Card(Ui.Column(2, top, title, body), 10).With(new Thickness(0, 0, 0, 6)));
        }
    }

    private async Task EnableAsync()
    {
        if (listener is null)
        {
            return;
        }

        try
        {
            var access = await listener.RequestAccessAsync();
            if (access != UserNotificationListenerAccessStatus.Allowed)
            {
                status = Strings.NotificationsDenied;
                Render();
                return;
            }

            services.Settings.Update(s => s.Notifications = true);
            await StartAsync(markExistingSeen: true);
        }
        catch (Exception ex)
        {
            Log.Warn("Enabling notifications failed", ex);
            listener = null;
            status = Strings.NotificationsUnsupported;
            Render();
        }
    }

    private async Task StartAsync(bool markExistingSeen)
    {
        if (listener is null)
        {
            return;
        }

        if (markExistingSeen)
        {
            foreach (var existing in await listener.GetNotificationsAsync(NotificationKinds.Toast))
            {
                seen.Add(existing.Id);
            }
        }

        polling = true;
        try
        {
            listener.NotificationChanged += (_, _) => dispatcher.InvokeAsync(async () => await PollAsync());
        }
        catch (Exception)
        {
            poll.Start();
        }

        Render();
    }

    private async Task PollAsync()
    {
        if (listener is null)
        {
            return;
        }

        IReadOnlyList<UserNotification> current;
        try
        {
            current = await listener.GetNotificationsAsync(NotificationKinds.Toast);
        }
        catch (Exception ex)
        {
            Log.Warn("Reading notifications failed", ex);
            return;
        }

        NoticeItem? newest = null;
        foreach (var notification in current)
        {
            if (!seen.Add(notification.Id))
            {
                continue;
            }

            var item = Read(notification);
            if (item is null)
            {
                continue;
            }

            data.Items.Add(item);
            newest = item;
        }

        if (newest is null)
        {
            return;
        }

        while (data.Items.Count > MaxItems)
        {
            data.Items.Remove(data.Items.OrderBy(i => i.At).First());
        }

        Save();
        peeking = newest;
        peekEnd.Stop();
        peekEnd.Start();
        UpdateCompact();
        RaiseActivityChanged();
    }

    private static NoticeItem? Read(UserNotification notification)
    {
        try
        {
            var binding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
            var texts = binding?.GetTextElements().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? [];
            return new NoticeItem
            {
                Id = notification.Id,
                App = notification.AppInfo?.DisplayInfo?.DisplayName ?? "Windows",
                Title = texts.FirstOrDefault() ?? string.Empty,
                Body = string.Join(" ", texts.Skip(1)),
                At = notification.CreationTime,
            };
        }
        catch (Exception ex)
        {
            Log.Warn("A notification could not be read", ex);
            return null;
        }
    }

    private void UpdateCompact()
    {
        if (compactApp is null || compactTitle is null || peeking is null)
        {
            return;
        }

        compactApp.Text = peeking.App;
        compactTitle.Text = string.IsNullOrWhiteSpace(peeking.Title) ? peeking.Body : peeking.Title;
    }

    private void Save()
    {
        JsonFile.Save("notifications", data);
        Render();
    }
}
