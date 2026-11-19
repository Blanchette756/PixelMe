using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PixelMe.Infrastructure;
using PixelMe.Localization;

namespace PixelMe.Modules.Agents;

public sealed class AgentsModule : IslandModule
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly CancellationTokenSource stop = new();
    private readonly Dictionary<string, (AgentSignal Signal, DateTimeOffset At)> agents = [];
    private StackPanel? list;
    private TextBlock? compactText;
    private TextBlock? copied;

    public override string Id => "agents";

    public override string DisplayName => Strings.AiAgents;

    public override string Glyph => "";

    public override int Priority => 90;

    public override bool HasActivity => agents.Values.Any(a => a.Signal.State == "waiting");

    public override int ActivityRank => 2;

    public override double CompactWidth => 320;

    public override Task InitializeAsync(CancellationToken cancellationToken)
    {
        _ = ListenAsync(stop.Token);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        stop.Cancel();
    }

    protected override FrameworkElement BuildCompact()
    {
        compactText = Ui.Text(string.Empty, 13, FontWeights.SemiBold);
        compactText.Margin = new Thickness(8, 0, 0, 0);
        var row = Ui.Columns((Ui.Auto, Ui.Glyph("", 14, "Island.Accent")), (Ui.Star(), compactText));
        row.Margin = new Thickness(16, 0, 16, 0);
        Render();
        return row;
    }

    protected override FrameworkElement BuildExpanded()
    {
        copied = Ui.Text(string.Empty, 12, null, "Island.Accent");
        var actions = Ui.Row(
            6,
            Ui.Pill(Strings.CopyClaudeHook, () => Copy(ClaudeSnippet()), ""),
            Ui.Pill(Strings.CopyCodexHook, () => Copy(CodexSnippet()), ""),
            Ui.Pill(Strings.Clear, () =>
            {
                agents.Clear();
                Render();
                RaiseActivityChanged();
            }, ""),
            copied);
        list = new StackPanel();
        var root = new DockPanel();
        DockPanel.SetDock(actions, Dock.Top);
        actions.Margin = new Thickness(0, 0, 0, 8);
        root.Children.Add(actions);
        root.Children.Add(Ui.Scroll(list));
        Render();
        return root;
    }

    protected override void Render()
    {
        var waiting = agents.Values.Where(a => a.Signal.State == "waiting").OrderByDescending(a => a.At).FirstOrDefault();
        if (compactText is not null && waiting.Signal is not null)
        {
            compactText.Text = $"{waiting.Signal.Agent} {Strings.NeedsInput}";
        }

        if (list is null)
        {
            return;
        }

        list.Children.Clear();
        if (agents.Count == 0)
        {
            var hint = Ui.Text(Strings.AgentsHint, 12.5, null, "Island.Secondary");
            hint.TextWrapping = TextWrapping.Wrap;
            list.Children.Add(hint.With(new Thickness(4, 4, 4, 0)));
            return;
        }

        foreach (var (signal, at) in agents.Values.OrderByDescending(a => a.At))
        {
            var (label, color) = signal.State switch
            {
                "waiting" => (Strings.AgentWaiting, Color.FromRgb(0xF5, 0x9E, 0x0B)),
                "working" => (Strings.AgentWorking, Color.FromRgb(0x60, 0xA5, 0xFA)),
                _ => (Strings.AgentDone, Color.FromRgb(0x34, 0xD3, 0x99)),
            };
            var chip = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 2, 8, 2),
                Background = new SolidColorBrush(Color.FromArgb(0x33, color.R, color.G, color.B)),
                Child = new TextBlock { Text = label, FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(color) },
            };
            var name = Ui.Text(string.IsNullOrEmpty(signal.Project) ? signal.Agent : $"{signal.Agent} · {signal.Project}", 13.5, FontWeights.SemiBold);
            name.Margin = new Thickness(10, 0, 8, 0);
            var time = Ui.Text(at.ToString("HH:mm"), 11.5, null, "Island.Secondary");
            var top = Ui.Columns((Ui.Auto, chip), (Ui.Star(), name), (Ui.Auto, time));
            var message = Ui.Text(signal.Message, 12.5, null, "Island.Secondary");
            message.Visibility = string.IsNullOrWhiteSpace(signal.Message) ? Visibility.Collapsed : Visibility.Visible;
            list.Children.Add(Ui.Card(Ui.Column(4, top, message), 10).With(new Thickness(0, 0, 0, 6)));
        }
    }

    private static string ExePath => Environment.ProcessPath ?? "PixelMe.exe";

    private static string ClaudeSnippet()
    {
        var command = JsonSerializer.Serialize($"\"{ExePath}\" --agent-hook claude");
        var entry = $"[{{ \"hooks\": [{{ \"type\": \"command\", \"command\": {command} }}] }}]";
        return $"\"hooks\": {{\n  \"Notification\": {entry},\n  \"Stop\": {entry},\n  \"UserPromptSubmit\": {entry}\n}}";
    }

    private static string CodexSnippet()
    {
        return $"notify = [{JsonSerializer.Serialize(ExePath)}, \"--agent-hook\", \"codex\"]";
    }

    private void Copy(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            if (copied is not null)
            {
                copied.Text = Strings.Copied;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Copying the hook snippet failed", ex);
        }
    }

    private async Task ListenAsync(CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    AgentCommand.PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellation);
                using var reader = new StreamReader(server);
                var json = await reader.ReadToEndAsync(cancellation);
                var signal = JsonSerializer.Deserialize<AgentSignal>(json);
                if (signal is not null)
                {
                    await dispatcher.InvokeAsync(() => Receive(signal));
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Warn("Agent message could not be read", ex);
                await Task.Delay(500, CancellationToken.None);
            }
        }
    }

    private void Receive(AgentSignal signal)
    {
        var key = $"{signal.Agent}|{signal.Project}";
        agents[key] = (signal, DateTimeOffset.Now);
        Render();
        RaiseActivityChanged();
        if (signal.State is "waiting" or "done")
        {
            RequestAttention();
        }
    }
}
