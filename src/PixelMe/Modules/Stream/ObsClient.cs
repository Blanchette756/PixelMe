using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PixelMe.Modules.Stream;

public sealed class ObsClient
{
    public event Action<bool>? ConnectionChanged;

    public event Action<bool, TimeSpan>? RecordingChanged;

    public async Task RunAsync(string host, int port, string password, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                connectTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                await socket.ConnectAsync(new Uri($"ws://{host}:{port}"), connectTimeout.Token);
                await SessionAsync(socket, password, cancellation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
            }

            ConnectionChanged?.Invoke(false);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cancellation);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static string Hash(string value) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task SendAsync(ClientWebSocket socket, JsonObject message, CancellationToken cancellation)
    {
        var bytes = Encoding.UTF8.GetBytes(message.ToJsonString());
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellation);
    }

    private static async Task<JsonDocument?> ReceiveAsync(ClientWebSocket socket, CancellationToken cancellation)
    {
        var buffer = new byte[16384];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellation);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return JsonDocument.Parse(message.ToArray());
            }
        }
    }

    private async Task SessionAsync(ClientWebSocket socket, string password, CancellationToken cancellation)
    {
        using (var hello = await ReceiveAsync(socket, cancellation))
        {
            if (hello is null)
            {
                return;
            }

            var identify = new JsonObject { ["rpcVersion"] = 1, ["eventSubscriptions"] = 64 };
            var data = hello.RootElement.GetProperty("d");
            if (data.TryGetProperty("authentication", out var auth))
            {
                var secret = Hash(password + auth.GetProperty("salt").GetString());
                identify["authentication"] = Hash(secret + auth.GetProperty("challenge").GetString());
            }

            await SendAsync(socket, new JsonObject { ["op"] = 1, ["d"] = identify }, cancellation);
        }

        while (socket.State == WebSocketState.Open)
        {
            using var message = await ReceiveAsync(socket, cancellation);
            if (message is null)
            {
                return;
            }

            var op = message.RootElement.GetProperty("op").GetInt32();
            var d = message.RootElement.GetProperty("d");
            switch (op)
            {
                case 2:
                    ConnectionChanged?.Invoke(true);
                    await SendAsync(
                        socket,
                        new JsonObject { ["op"] = 6, ["d"] = new JsonObject { ["requestType"] = "GetRecordStatus", ["requestId"] = "record" } },
                        cancellation);
                    break;
                case 5 when d.GetProperty("eventType").GetString() == "RecordStateChanged":
                    var active = d.GetProperty("eventData").GetProperty("outputActive").GetBoolean();
                    RecordingChanged?.Invoke(active, TimeSpan.Zero);
                    break;
                case 7 when d.GetProperty("requestType").GetString() == "GetRecordStatus"
                    && d.TryGetProperty("responseData", out var response):
                    var recording = response.GetProperty("outputActive").GetBoolean();
                    var elapsed = response.TryGetProperty("outputDuration", out var duration) ? TimeSpan.FromMilliseconds(duration.GetDouble()) : TimeSpan.Zero;
                    RecordingChanged?.Invoke(recording, elapsed);
                    break;
            }
        }
    }
}
