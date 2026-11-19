using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using PixelMe.Infrastructure;

namespace PixelMe.Modules.Shelf;

public sealed class PhoneShare : IDisposable
{
    private const int MaxRequestBytes = 8192;

    private TcpListener? listener;
    private CancellationTokenSource? stop;
    private IReadOnlyList<string> files = [];
    private string token = string.Empty;

    public bool IsRunning => listener is not null;

    public string Start(IReadOnlyList<string> shared)
    {
        Stop();
        var address = FindLanAddress() ?? throw new InvalidOperationException("No local network connection was found.");
        files = shared;
        token = Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant();
        listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        stop = new CancellationTokenSource();
        _ = AcceptAsync(listener, stop.Token);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return $"http://{address}:{port}/{token}/";
    }

    public void Stop()
    {
        stop?.Cancel();
        stop?.Dispose();
        stop = null;
        listener?.Stop();
        listener = null;
    }

    public void Dispose() => Stop();

    private static IPAddress? FindLanAddress()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(n => (Adapter: n, Properties: n.GetIPProperties()))
            .Where(n => n.Properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
            .OrderByDescending(n => n.Adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .SelectMany(n => n.Properties.UnicastAddresses)
            .Select(u => u.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
    }

    private async Task AcceptAsync(TcpListener server, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await server.AcceptTcpClientAsync(cancellation);
            }
            catch
            {
                return;
            }

            _ = Task.Run(() => ServeAsync(client, cancellation), cancellation);
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken cancellation)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var path = await ReadPathAsync(stream, cancellation);
                var root = $"/{token}/";
                if (path == root)
                {
                    await WriteHtmlAsync(stream, cancellation);
                }
                else if (path is not null
                    && path.StartsWith(root + "f/", StringComparison.Ordinal)
                    && int.TryParse(path[(root.Length + 2)..], out var index)
                    && index >= 0 && index < files.Count
                    && File.Exists(files[index]))
                {
                    await WriteFileAsync(stream, files[index], cancellation);
                }
                else
                {
                    await WriteAsync(stream, "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", cancellation);
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Log.Warn("Serving a file to the phone failed", ex);
            }
        }
    }

    private static async Task<string?> ReadPathAsync(NetworkStream stream, CancellationToken cancellation)
    {
        var buffer = new byte[MaxRequestBytes];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellation);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (Encoding.ASCII.GetString(buffer, 0, total).Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                break;
            }
        }

        var firstLine = Encoding.ASCII.GetString(buffer, 0, total).Split("\r\n")[0].Split(' ');
        return firstLine.Length >= 2 && firstLine[0] == "GET" ? firstLine[1] : null;
    }

    private static Task WriteAsync(NetworkStream stream, string text, CancellationToken cancellation)
    {
        return stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellation).AsTask();
    }

    private async Task WriteHtmlAsync(NetworkStream stream, CancellationToken cancellation)
    {
        var rows = new StringBuilder();
        for (var i = 0; i < files.Count; i++)
        {
            var info = new FileInfo(files[i]);
            if (!info.Exists)
            {
                continue;
            }

            rows.Append($"<a href=\"f/{i}\"><b>{WebUtility.HtmlEncode(info.Name)}</b><span>{FormatSize(info.Length)}</span></a>");
        }

        var html = "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
            + "<title>PixelMe</title><style>body{margin:0;font-family:system-ui,sans-serif;background:#0d0e12;color:#fff;padding:24px}"
            + "h1{font-size:20px}a{display:flex;justify-content:space-between;gap:12px;padding:16px;margin:10px 0;border-radius:14px;"
            + "background:#1c1d24;color:#fff;text-decoration:none}span{opacity:.6;white-space:nowrap}</style></head><body>"
            + $"<h1>PixelMe</h1>{rows}</body></html>";
        var body = Encoding.UTF8.GetBytes(html);
        await WriteAsync(stream, $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n", cancellation);
        await stream.WriteAsync(body, cancellation);
    }

    private static async Task WriteFileAsync(NetworkStream stream, string path, CancellationToken cancellation)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, useAsync: true);
        var name = Uri.EscapeDataString(Path.GetFileName(path));
        await WriteAsync(
            stream,
            $"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: {file.Length}\r\n"
            + $"Content-Disposition: attachment; filename*=UTF-8''{name}\r\nConnection: close\r\n\r\n",
            cancellation);
        await file.CopyToAsync(stream, cancellation);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1073741824 => $"{bytes / 1073741824.0:0.0} GB",
        >= 1048576 => $"{bytes / 1048576.0:0.0} MB",
        >= 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B",
    };
}
