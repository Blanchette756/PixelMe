using System.Globalization;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Localization;

namespace PixelMe.Modules.Reminders;

public sealed class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Label { get; set; } = string.Empty;

    public DateTimeOffset Due { get; set; }

    public int Minutes { get; set; }

    public bool Repeat { get; set; }

    public bool Firing { get; set; }
}

public sealed class ReminderData
{
    public List<Reminder> Items { get; set; } = [];
}

public sealed class RemindersModule : IslandModule
{
    private static readonly int[] QuickMinutes = [1, 5, 10, 15, 30, 60];

    private readonly DispatcherTimer alarm = new(DispatcherPriority.Normal);
    private readonly DispatcherTimer ticker = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private ReminderData data = new();
    private StackPanel? list;
    private TextBox? label;
    private TextBox? minutes;
    private TextBlock? compactText;
    private TextBlock? compactGlyph;
    private FrameworkElement? compact;
    private FrameworkElement? panel;

    public override string Id => "reminders";

    public override string DisplayName => Strings.RemindersTitle;

    public override string Glyph => "";

    public override int Priority => 40;

    public override bool HasActivity => data.Items.Count > 0;

    public override int ActivityRank => data.Items.Any(r => r.Firing) ? 1 : 15;

    public override double CompactWidth => 250;

    public override Task InitializeAsync(CancellationToken cancellationToken)
    {
        data = JsonFile.Load<ReminderData>("reminders");
        alarm.Tick += (_, _) => CheckDue();
        ticker.Tick += (_, _) => Tick();
        CheckDue();
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        alarm.Stop();
        ticker.Stop();
    }

    protected override FrameworkElement BuildCompact()
    {
        compactGlyph = Ui.Glyph("", 14, "Island.Accent");
        compactText = Ui.Text(string.Empty, 13, FontWeights.SemiBold);
        compactText.Margin = new Thickness(8, 0, 0, 0);
        var row = Ui.Columns((Ui.Auto, compactGlyph), (Ui.Star(), compactText));
        row.Margin = new Thickness(16, 0, 16, 0);
        compact = row;
        compact.IsVisibleChanged += (_, _) => UpdateTicker();
        UpdateCompact();
        return compact;
    }

    protected override FrameworkElement BuildExpanded()
    {
        label = Ui.Input(Strings.ReminderLabel, _ => AddCustom());
        minutes = Ui.Input(Strings.Minutes, _ => AddCustom());
        minutes.Width = 70;
        minutes.Margin = new Thickness(8, 0, 8, 0);
        var add = Ui.Pill(Strings.Add, AddCustom, "");
        var inputRow = Ui.Columns((Ui.Star(), label), (Ui.Auto, minutes), (Ui.Auto, add));

        var quick = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
        foreach (var value in QuickMinutes)
        {
            quick.Children.Add(Ui.Pill(FormatMinutes(value), () => Add(value)).With(new Thickness(0, 0, 6, 0)));
        }

        list = new StackPanel();
        var root = new DockPanel();
        DockPanel.SetDock(inputRow, Dock.Top);
        DockPanel.SetDock(quick, Dock.Top);
        root.Children.Add(inputRow);
        root.Children.Add(quick);
        root.Children.Add(Ui.Scroll(list));
        panel = root;
        panel.IsVisibleChanged += (_, _) => UpdateTicker();
        Render();
        return root;
    }

    protected override void Render()
    {
        UpdateCompact();
        if (list is null)
        {
            return;
        }

        list.Children.Clear();
        if (data.Items.Count == 0)
        {
            list.Children.Add(Ui.Text(Strings.NoReminders, 12.5, null, "Island.Secondary").With(new Thickness(4, 8, 0, 0)));
            return;
        }

        foreach (var reminder in data.Items.OrderBy(r => r.Due))
        {
            list.Children.Add(BuildRow(reminder));
        }
    }

    private static string FormatMinutes(int value) => value >= 60 ? $"{value / 60}h" : $"{value}m";

    private static string Remaining(Reminder reminder)
    {
        if (reminder.Firing)
        {
            return Strings.Now;
        }

        var left = reminder.Due - DateTimeOffset.Now;
        if (left < TimeSpan.Zero)
        {
            left = TimeSpan.Zero;
        }

        return left.TotalHours >= 1 ? $"{(int)left.TotalHours}:{left.Minutes:00}:{left.Seconds:00}" : $"{left.Minutes}:{left.Seconds:00}";
    }

    private static string Title(Reminder reminder) => string.IsNullOrWhiteSpace(reminder.Label) ? Strings.Timer : reminder.Label;

    private UIElement BuildRow(Reminder reminder)
    {
        var glyph = Ui.Glyph(reminder.Firing ? "" : "", 15, reminder.Firing ? "Island.Accent" : "Island.Secondary");
        var name = Ui.Text(Title(reminder), 13.5, FontWeights.SemiBold).With(new Thickness(10, 0, 8, 0));
        var time = Ui.Text(Remaining(reminder), 13, FontWeights.SemiBold, reminder.Firing ? "Island.Accent" : "Island.Secondary");
        time.SetResourceReference(TextBlock.FontFamilyProperty, "Island.MonoFont");
        time.Margin = new Thickness(0, 0, 8, 0);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        if (reminder.Firing)
        {
            buttons.Children.Add(Ui.Pill(Strings.Snooze, () => Snooze(reminder)).With(new Thickness(0, 0, 6, 0)));
        }

        var repeat = Ui.IconButton("", Strings.RepeatToggle, () =>
        {
            reminder.Repeat = !reminder.Repeat;
            Save();
        });
        if (reminder.Repeat)
        {
            repeat.SetResourceReference(Control.ForegroundProperty, "Island.Accent");
        }

        buttons.Children.Add(repeat);
        buttons.Children.Add(Ui.IconButton("", Strings.Done, () => Complete(reminder)));
        buttons.Children.Add(Ui.IconButton("", Strings.Remove, () =>
        {
            data.Items.Remove(reminder);
            Save();
        }));

        var row = Ui.Columns((Ui.Auto, glyph), (Ui.Star(), name), (Ui.Auto, time), (Ui.Auto, buttons));
        row.Margin = new Thickness(6, 3, 0, 3);
        row.Tag = reminder;
        return row;
    }

    private void AddCustom()
    {
        var value = int.TryParse(minutes?.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsed) ? parsed : 5;
        Add(Math.Clamp(value, 1, 24 * 60));
        if (minutes is not null)
        {
            minutes.Text = string.Empty;
        }
    }

    private void Add(int value)
    {
        data.Items.Add(new Reminder
        {
            Label = label?.Text.Trim() ?? string.Empty,
            Minutes = value,
            Due = DateTimeOffset.Now.AddMinutes(value),
        });
        if (label is not null)
        {
            label.Text = string.Empty;
        }

        Save();
    }

    private void Snooze(Reminder reminder)
    {
        reminder.Firing = false;
        reminder.Due = DateTimeOffset.Now.AddMinutes(5);
        Save();
    }

    private void Complete(Reminder reminder)
    {
        if (reminder.Repeat)
        {
            reminder.Firing = false;
            reminder.Due = DateTimeOffset.Now.AddMinutes(Math.Max(1, reminder.Minutes));
        }
        else
        {
            data.Items.Remove(reminder);
        }

        Save();
    }

    private void CheckDue()
    {
        var now = DateTimeOffset.Now.AddMilliseconds(400);
        var fired = false;
        foreach (var reminder in data.Items.Where(r => !r.Firing && r.Due <= now))
        {
            reminder.Firing = true;
            fired = true;
        }

        if (fired)
        {
            SystemSounds.Exclamation.Play();
            Save();
            RequestAttention();
        }
        else
        {
            Schedule();
        }
    }

    private void Schedule()
    {
        alarm.Stop();
        var next = data.Items.Where(r => !r.Firing).Select(r => (DateTimeOffset?)r.Due).Min();
        if (next is null)
        {
            return;
        }

        var wait = next.Value - DateTimeOffset.Now;
        alarm.Interval = wait < TimeSpan.FromMilliseconds(50) ? TimeSpan.FromMilliseconds(50) : wait > TimeSpan.FromHours(12) ? TimeSpan.FromHours(12) : wait;
        alarm.Start();
    }

    private void Tick()
    {
        UpdateCompact();
        if (list is null)
        {
            return;
        }

        foreach (var row in list.Children.OfType<Grid>())
        {
            if (row.Tag is Reminder reminder && row.Children.Count > 2 && row.Children[2] is TextBlock time)
            {
                time.Text = Remaining(reminder);
            }
        }
    }

    private void UpdateTicker()
    {
        var needed = data.Items.Count > 0 && ((compact?.IsVisible ?? false) || (panel?.IsVisible ?? false));
        if (needed && !ticker.IsEnabled)
        {
            ticker.Start();
        }
        else if (!needed && ticker.IsEnabled)
        {
            ticker.Stop();
        }
    }

    private void UpdateCompact()
    {
        if (compactText is null || compactGlyph is null)
        {
            return;
        }

        var firing = data.Items.FirstOrDefault(r => r.Firing);
        var next = firing ?? data.Items.OrderBy(r => r.Due).FirstOrDefault();
        compactGlyph.Text = firing is null ? "" : "";
        compactText.Text = next is null ? string.Empty : $"{Remaining(next)}  {Title(next)}";
    }

    private void Save()
    {
        JsonFile.Save("reminders", data);
        Schedule();
        Render();
        UpdateTicker();
        RaiseActivityChanged();
    }
}
