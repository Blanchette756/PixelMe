using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace PixelMe.Modules.Agents;

public sealed record AgentSignal(string Agent, string State, string Message, string Project);

public static class AgentCommand
{
    public static string PipeName => $"PixelMe.Agents.{Environment.UserName}";

    public static bool TryRun(string[] args)
    {
        var signal = Parse(args);
        if (signal is null)
        {
            return false;
        }

        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            pipe.Connect(1500);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(signal));
            pipe.Write(bytes);
        }
        catch
        {
        }

        return true;
    }

    private static AgentSignal? Parse(string[] args)
    {
        var hook = Value(args, "--agent-hook");
        if (hook is not null)
        {
            return hook.ToLowerInvariant() switch
            {
                "claude" => FromClaude(Console.IsInputRedirected ? Console.In.ReadToEnd() : string.Empty),
                "codex" => FromCodex(args.LastOrDefault() ?? string.Empty),
                _ => null,
            };
        }

        var agent = Value(args, "--agent");
        return agent is null
            ? null
            : new AgentSignal(agent, Value(args, "--state") ?? "waiting", Value(args, "--message") ?? string.Empty, Value(args, "--project") ?? string.Empty);
    }

    private static string? Value(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static AgentSignal? FromClaude(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var hookEvent = Text(root, "hook_event_name");
            var project = Path.GetFileName(Text(root, "cwd").TrimEnd('\\', '/'));
            return hookEvent switch
            {
                "Notification" => new AgentSignal("Claude Code", "waiting", Text(root, "message"), project),
                "Stop" => new AgentSignal("Claude Code", "done", string.Empty, project),
                "UserPromptSubmit" => new AgentSignal("Claude Code", "working", string.Empty, project),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AgentSignal? FromCodex(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return Text(root, "type") == "agent-turn-complete"
                ? new AgentSignal("Codex", "done", Text(root, "last-assistant-message"), Path.GetFileName(Text(root, "cwd").TrimEnd('\\', '/')))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Text(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }
}
