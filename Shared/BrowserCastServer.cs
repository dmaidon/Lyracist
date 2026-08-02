// Created on Aug 1, 2026 @ 12:08:00 -> Add BrowserCastServer HTTP listener server
// Rewritten on Aug 1, 2026 -> Replace HttpListener with a raw TcpListener. HttpListener binds
// through the HTTP.sys kernel driver, which requires either Administrator or a one-time `netsh
// http add urlacl` reservation for any prefix other than localhost - neither of which this app
// has, so a wildcard bind silently fell back to localhost-only and became unreachable from any
// device on the network (a cast target's image requests never even connected, showing a black
// screen). TcpListener has no such restriction and needs nothing beyond the normal firewall rule.
// Rewritten again on Aug 1, 2026 -> Serve an MJPEG multipart stream instead of one-shot images.
// The rotation frame changes continuously (marquee bulb chase, crawl scroll, singer changes), but
// a single static-image response can only be refreshed by having the caller issue a brand new Cast
// LOAD command - and every LOAD is a full media transition on the receiver, which is what showed up
// as visible flicker/blinking on the TV, while also being far too infrequent to show fast animations
// like the marquee chase at all. A `multipart/x-mixed-replace` stream keeps one connection open and
// pushes fresh frames down it continuously - no reload transition per frame, and pushed often enough
// to actually show motion. This is the standard technique for casting a live/IP-camera-style image
// feed to a Chromecast's Default Media Receiver (LOAD contentType "multipart/x-mixed-replace",
// streamType "LIVE", no repeat LOAD needed).
// Moved to Shared on Aug 1, 2026 -> byte-for-byte duplicated between Lyracist and KSRotation.
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
    private const string MjpegBoundary = "lyracistframe";
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
                using var stream = client.GetStream();

                // Minimal HTTP/1.1 request read: enough to know the request is complete before
                // we respond. We don't care about the path or headers - any request gets the
                // live rotation stream, matching the single-endpoint contract callers rely on.
                if (!await ReadRequestHeadersAsync(stream, serverToken))
                {
                    return;
                }

                Globals.LogInfo("Shared", $"BrowserCastServer stream started for {remote}");

                var header =
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
