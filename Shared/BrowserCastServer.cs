// Edited on Aug 25, 2026 @ 06:40:00 -> Fix RCS1261 await using stream and RCS1118 const header
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Lyracist.Shared;

public class BrowserCastServer
{
    public const string MjpegBoundary = "lyracistframe";
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(400);

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public async Task<bool> StartServerAsync(IRotationRenderer renderer)
    {
        if (_listener != null)
        {
            await StopServerAsync();
        }

        _cts = new CancellationTokenSource();

        try
        {
            _listener = new TcpListener(IPAddress.Any, 8080);
            _listener.Start();
            Globals.LogInfo("Shared", "BrowserCastServer listening on port 8080 (any interface).");
        }
        catch (Exception ex)
        {
            Globals.LogError("Shared", "BrowserCastServer.StartServerAsync", ex);
            _listener = null;
            return false;
        }

        var cts = _cts;
        var listener = _listener;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(cts.Token);
                    _ = Task.Run(() => HandleClientAsync(client, renderer, cts.Token), cts.Token);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is ObjectDisposedException || ex is SocketException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                Globals.LogError("Shared", "BrowserCastServer.AcceptLoop", ex);
            }
        }, cts.Token);

        return true;
    }

    private async Task HandleClientAsync(TcpClient client, IRotationRenderer renderer, CancellationToken serverToken)
    {
        var remote = client.Client.RemoteEndPoint;
        using (client)
        {
            try
            {
                client.NoDelay = true;
                await using var stream = client.GetStream();

                // Minimal HTTP/1.1 request read: enough to know the request is complete before
                // we respond. We don't care about the path or headers - any request gets the
                // live rotation stream, matching the single-endpoint contract callers rely on.
                if (!await ReadRequestHeadersAsync(stream, serverToken))
                {
                    return;
                }

                Globals.LogInfo("Shared", $"BrowserCastServer stream started for {remote}");

                const string header =
                    "HTTP/1.1 200 OK\r\n" +
                    $"Content-Type: multipart/x-mixed-replace; boundary={MjpegBoundary}\r\n" +
                    "Cache-Control: no-store\r\n" +
                    "Access-Control-Allow-Origin: *\r\n" +
                    "Connection: close\r\n" +
                    "\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(header), serverToken);

                // Stream frames until the client disconnects (detected via a failed write) or the
                // server is shutting down.
                while (!serverToken.IsCancellationRequested)
                {
                    var frameStart = DateTime.UtcNow;

                    var bmp = await renderer.RenderRotationAsync();
                    if (bmp != null)
                    {
                        var png = BitmapToPng(bmp);
                        var partHeader =
                            $"--{MjpegBoundary}\r\n" +
                            "Content-Type: image/png\r\n" +
                            $"Content-Length: {png.Length}\r\n" +
                            "\r\n";

                        await stream.WriteAsync(Encoding.ASCII.GetBytes(partHeader), serverToken);
                        await stream.WriteAsync(png, serverToken);
                        await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"), serverToken);
                        await stream.FlushAsync(serverToken);
                    }

                    var elapsed = DateTime.UtcNow - frameStart;
                    var wait = FrameInterval - elapsed;
                    if (wait > TimeSpan.Zero)
                    {
                        await Task.Delay(wait, serverToken);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is SocketException || ex is OperationCanceledException)
            {
                // Client disconnected or server shutting down - not worth logging every time.
            }
            catch (Exception ex)
            {
                Globals.LogError("Shared", "BrowserCastServer.HandleClient", ex);
            }
            finally
            {
                Globals.LogInfo("Shared", $"BrowserCastServer stream ended for {remote}");
            }
        }
    }

    /// <summary>Reads until the blank line that ends an HTTP request's headers, or gives up past a size/time limit.</summary>
    private static async Task<bool> ReadRequestHeadersAsync(NetworkStream stream, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        var buffer = new byte[8192];
        int total = 0;

        try
        {
            while (total < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), timeoutCts.Token);
                if (read <= 0) return false;
                total += read;

                var text = Encoding.ASCII.GetString(buffer, 0, total);
                if (text.Contains("\r\n\r\n"))
                {
                    return true;
                }
            }
            return true; // header section larger than our buffer; proceed anyway rather than hang
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public async Task StopServerAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        if (_listener != null)
        {
            try
            {
                _listener.Stop();
            }
            catch { }
            _listener = null;
        }

        await Task.Delay(1);
    }

    private static byte[] BitmapToPng(BitmapSource bmp)
    {
        using var ms = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        encoder.Save(ms);
        return ms.ToArray();
    }
}
