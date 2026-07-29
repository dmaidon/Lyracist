// Edited on Jul 28, 2026 @ 18:39:00 -> Add support for requestType parameter in REST web API requests
// Last Edit: Jul 16, 2026 11:00 - REST backend web API server
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KSRotation.Models;

namespace KSRotation.Services
{
    public class PatronRequestServer(
        int port, 
        Action<string, List<RequestedSong>, string> onRequestReceived, 
        Func<string> onGetRotationJson,
        Func<string, bool> onVerifyPin,
        Func<string> onGetRequestsJson,
        Func<string, string, string, string, string, string, string> onHandleDjAction)
    {
        private const int MaxRequestBodyBytes = 8_192;
        private const int MaxConcurrentConnections = 64;
        private const int MaxPinAttemptsBeforeLockout = 5;
        private static readonly TimeSpan PinLockoutDuration = TimeSpan.FromMinutes(2);

        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly int _port = port;
        private readonly Action<string, List<RequestedSong>, string> _onRequestReceived = onRequestReceived;
        private readonly Func<string> _onGetRotationJson = onGetRotationJson;
        private readonly Func<string, bool> _onVerifyPin = onVerifyPin;
        private readonly Func<string> _onGetRequestsJson = onGetRequestsJson;
        private readonly Func<string, string, string, string, string, string, string> _onHandleDjAction = onHandleDjAction;
        private readonly SemaphoreSlim _connectionLimiter = new(MaxConcurrentConnections, MaxConcurrentConnections);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, PinAttemptState> _pinAttemptsByIp = new();

        private sealed class PinAttemptState
        {
            public int FailedCount;
            public DateTime LockedUntilUtc;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            Task.Run(() => AcceptConnectionsAsync(_cts.Token));
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
            }
            catch (Exception ex)
            {
                LoggerService.LogError("PatronRequestServer.Stop", ex);
            }
        }

        private async Task AcceptConnectionsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener!.AcceptTcpClientAsync(token);

                    // Cap concurrent in-flight connections so a burst of requests can't exhaust threads/sockets.
                    if (!_connectionLimiter.Wait(0))
                    {
                        client.Dispose();
                        continue;
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await HandleClientAsync(client);
                        }
                        finally
                        {
                            _connectionLimiter.Release();
                        }
                    }, token);
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested) break;
                    LoggerService.LogError("PatronRequestServer.AcceptConnectionsAsync", ex);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            string clientIp = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "unknown";

            using (client)
            using (NetworkStream stream = client.GetStream())
            // Reads go through a small buffer so header/body parsing isn't one syscall per byte;
            // responses are still written directly to `stream`, unbuffered.
            using (BufferedStream readStream = new(stream, 4096))
            {
                try
                {
                    string headers = await ReadHeadersAsync(readStream);
                    if (string.IsNullOrWhiteSpace(headers))
                    {
                        return;
                    }

                    string[] headerLines = headers.Split("\r\n", StringSplitOptions.None);
                    string requestLine = headerLines[0];
                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2)
                    {
                        return;
                    }

                    string method = parts[0];
                    string rawPath = parts[1];
                    string path = rawPath;
                    string queryPin = "";
                    int qIdx = rawPath.IndexOf('?');
                    if (qIdx >= 0)
                    {
                        path = rawPath[..qIdx];
                        string query = rawPath[(qIdx + 1)..];
                        foreach (string param in query.Split('&'))
                        {
                            string[] kv = param.Split('=');
                            if (kv.Length == 2 && kv[0].Equals("pin", StringComparison.OrdinalIgnoreCase))
                            {
                                queryPin = WebUtility.UrlDecode(kv[1]);
                            }
                        }
                    }

                    string pin = ParseHeader(headerLines, "X-DJ-PIN");
                    if (string.IsNullOrEmpty(pin))
                    {
                        pin = queryPin;
                    }

                    if (method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                    {
                        await SendCorsPreflightResponseAsync(stream);
                        return;
                    }

                    int contentLength = ParseContentLength(headerLines);
                    if (contentLength < 0 || contentLength > MaxRequestBodyBytes)
                    {
                        await SendBadRequestAsync(stream, "{\"error\":\"Request body too large.\"}");
                        return;
                    }

                    if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        if (path == "/" || path == "/index.html")
                        {
                            await SendHtmlResponseAsync(stream, GetHtmlContent());
                        }
                        else if (path == "/dj" || path == "/dj.html")
                        {
                            await SendHtmlResponseAsync(stream, GetDjHtmlContent());
                        }
                        else if (path.StartsWith("/api/rotation", StringComparison.OrdinalIgnoreCase))
                        {
                            string json = _onGetRotationJson();
                            await SendJsonResponseAsync(stream, json);
                        }
                        else if (path.StartsWith("/api/requests", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!TryAuthorizeDj(clientIp, pin, out bool lockedOut))
                            {
                                if (lockedOut)
                                {
                                    await SendTooManyRequestsAsync(stream);
                                }
                                else
                                {
                                    await SendUnauthorizedAsync(stream);
                                }
                                return;
                            }
                            string json = _onGetRequestsJson();
                            await SendJsonResponseAsync(stream, json);
                        }
                        else
                        {
                            await SendNotFoundAsync(stream);
                        }
                    }
                    else if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.StartsWith("/api/request", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] bodyBytes = await ReadBodyBytesAsync(readStream, contentLength);
                        string body = Encoding.UTF8.GetString(bodyBytes);

                        using JsonDocument doc = JsonDocument.Parse(body);
                        JsonElement root = doc.RootElement;
                        string name = root.TryGetProperty("name", out var nProp) ? (nProp.GetString() ?? "") : "";
                        string song = root.TryGetProperty("song", out var sProp) ? (sProp.GetString() ?? "") : "";
                        string artist = root.TryGetProperty("artist", out var aProp) ? (aProp.GetString() ?? "") : "";
                        string requestType = root.TryGetProperty("requestType", out var rtProp) ? (rtProp.GetString() ?? "Karaoke") : "Karaoke";

                        var songs = new List<RequestedSong>();
                        if (root.TryGetProperty("songs", out var songsProp) && songsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var songEl in songsProp.EnumerateArray())
                            {
                                string sTitle = songEl.TryGetProperty("song", out var stProp) ? (stProp.GetString() ?? "") : "";
                                string sArtist = songEl.TryGetProperty("artist", out var saProp) ? (saProp.GetString() ?? "") : "";
                                if (!string.IsNullOrWhiteSpace(sTitle))
                                {
                                    songs.Add(new RequestedSong { Song = sTitle.Trim(), Artist = sArtist.Trim() });
                                }
                            }
                        }

                        if (songs.Count == 0 && !string.IsNullOrWhiteSpace(song))
                        {
                            songs.Add(new RequestedSong { Song = song.Trim(), Artist = artist.Trim() });
                        }

                        if (!string.IsNullOrWhiteSpace(name) && songs.Count > 0)
                        {
                            _onRequestReceived(name.Trim(), songs, requestType);
                            await SendJsonResponseAsync(stream, "{\"success\":true}");
                        }
                        else
                        {
                            await SendBadRequestAsync(stream, "{\"error\":\"Name and at least one Song title are required.\"}");
                        }
                    }
                    else if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.StartsWith("/api/dj/action", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryAuthorizeDj(clientIp, pin, out bool lockedOut))
                        {
                            if (lockedOut)
                            {
                                await SendTooManyRequestsAsync(stream);
                            }
                            else
                            {
                                await SendUnauthorizedAsync(stream);
                            }
                            return;
                        }

                        byte[] bodyBytes = await ReadBodyBytesAsync(readStream, contentLength);
                        string body = Encoding.UTF8.GetString(bodyBytes);

                        using JsonDocument doc = JsonDocument.Parse(body);
                        JsonElement root = doc.RootElement;
                        string action = root.TryGetProperty("action", out var actProp) ? (actProp.GetString() ?? "") : "";
                        string targetId = root.TryGetProperty("targetId", out var tgtProp) ? (tgtProp.GetString() ?? "") : "";
                        string extraData = root.TryGetProperty("extraData", out var extProp) ? (extProp.GetString() ?? "") : "";
                        string name = root.TryGetProperty("name", out var nameProp) ? (nameProp.GetString() ?? "") : "";
                        string song = root.TryGetProperty("song", out var songProp) ? (songProp.GetString() ?? "") : "";
                        string artist = root.TryGetProperty("artist", out var artProp) ? (artProp.GetString() ?? "") : "";

                        string error = _onHandleDjAction(action, targetId, extraData, name, song, artist);
                        if (string.IsNullOrEmpty(error))
                        {
                            await SendJsonResponseAsync(stream, "{\"success\":true}");
                        }
                        else
                        {
                            await SendBadRequestAsync(stream, $"{{\"error\":\"{JsonEncodedText.Encode(error)}\"}}");
                        }
                    }
                    else
                    {
                        await SendNotFoundAsync(stream);
                    }
                }
                catch (Exception ex)
                {
                    // Log the real exception server-side, but never echo its message back to the client —
                    // internal exception text (paths, stack details, etc.) is not for a patron's browser.
                    LoggerService.LogError("PatronRequestServer.HandleClientAsync", ex);
                    try
                    {
                        await SendBadRequestAsync(stream, "{\"error\":\"Invalid request.\"}");
                    }
                    catch (Exception innerEx)
                    {
                        LoggerService.LogError("PatronRequestServer.HandleClientAsync.SendError", innerEx);
                    }
                }
            }
        }

        private static int ParseContentLength(string[] headerLines)
        {
            foreach (string line in headerLines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }

                int separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string headerName = line[..separatorIndex].Trim();
                if (!headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = line[(separatorIndex + 1)..].Trim();
                if (int.TryParse(value, out int contentLength))
                {
                    return contentLength;
                }
            }

            return 0;
        }

        private static async Task<string> ReadHeadersAsync(Stream stream)
        {
            const int maxHeaderBytes = 16_384;
            using MemoryStream headerBuffer = new();
            byte[] singleByte = new byte[1];

            while (headerBuffer.Length < maxHeaderBytes)
            {
                int read = await stream.ReadAsync(singleByte.AsMemory(0, 1));
                if (read == 0)
                {
                    break;
                }

                headerBuffer.WriteByte(singleByte[0]);
                if (EndsWithHeaderTerminator(headerBuffer))
                {
                    break;
                }
            }

            if (headerBuffer.Length >= maxHeaderBytes)
            {
                throw new InvalidOperationException("HTTP headers exceeded the maximum allowed size.");
            }

            return Encoding.ASCII.GetString(headerBuffer.ToArray());
        }

        private static bool EndsWithHeaderTerminator(MemoryStream stream)
        {
            if (stream.Length < 4)
            {
                return false;
            }

            byte[] bytes = stream.GetBuffer();
            int i = (int)stream.Length - 4;
            return bytes[i] == '\r'
                && bytes[i + 1] == '\n'
                && bytes[i + 2] == '\r'
                && bytes[i + 3] == '\n';
        }

        private static async Task SendHtmlResponseAsync(NetworkStream stream, string html)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: text/html; charset=utf-8\r\n" +
                $"Content-Length: {Encoding.UTF8.GetByteCount(html)}\r\n" +
                "Connection: close\r\n\r\n" +
                html);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private static async Task SendJsonResponseAsync(NetworkStream stream, string json)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n" +
                "Connection: close\r\n\r\n" +
                json);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private static async Task SendCorsPreflightResponseAsync(NetworkStream stream)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(
                "HTTP/1.1 204 No Content\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type, X-DJ-PIN\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private static async Task SendNotFoundAsync(NetworkStream stream)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private static async Task SendUnauthorizedAsync(NetworkStream stream)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(
                "HTTP/1.1 401 Unauthorized\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                "Content-Length: 25\r\n" +
                "Connection: close\r\n\r\n" +
                "{\"error\":\"Unauthorized\"}");
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private static async Task SendTooManyRequestsAsync(NetworkStream stream)
        {
            const string json = "{\"error\":\"Too many failed PIN attempts. Try again later.\"}";
            byte[] responseBytes = Encoding.UTF8.GetBytes(
                "HTTP/1.1 429 Too Many Requests\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n" +
                "Connection: close\r\n\r\n" +
                json);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        /// <summary>
        /// Verifies the DJ PIN for <paramref name="clientIp"/>, tracking failed attempts per IP and
        /// locking that IP out for <see cref="PinLockoutDuration"/> after <see cref="MaxPinAttemptsBeforeLockout"/>
        /// consecutive failures. Guards against brute-forcing a short numeric PIN over the LAN.
        /// </summary>
        private bool TryAuthorizeDj(string clientIp, string pin, out bool lockedOut)
        {
            PinAttemptState state = _pinAttemptsByIp.GetOrAdd(clientIp, _ => new PinAttemptState());

            lock (state)
            {
                if (DateTime.UtcNow < state.LockedUntilUtc)
                {
                    lockedOut = true;
                    return false;
                }

                if (_onVerifyPin(pin))
                {
                    state.FailedCount = 0;
                    state.LockedUntilUtc = DateTime.MinValue;
                    lockedOut = false;
                    return true;
                }

                state.FailedCount++;
                if (state.FailedCount >= MaxPinAttemptsBeforeLockout)
                {
                    state.LockedUntilUtc = DateTime.UtcNow.Add(PinLockoutDuration);
                    state.FailedCount = 0;
                }

                lockedOut = false;
                return false;
            }
        }

        private static async Task SendBadRequestAsync(NetworkStream stream, string jsonError)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(
                "HTTP/1.1 400 Bad Request\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {Encoding.UTF8.GetByteCount(jsonError)}\r\n" +
                "Connection: close\r\n\r\n" +
                jsonError);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private static string ParseHeader(string[] headerLines, string headerName)
        {
            foreach (string line in headerLines)
            {
                if (line.StartsWith(headerName + ":", StringComparison.OrdinalIgnoreCase))
                {
                    return line[(headerName.Length + 1)..].Trim();
                }
            }
            return string.Empty;
        }

        private static async Task<byte[]> ReadBodyBytesAsync(Stream stream, int contentLength)
        {
            byte[] buffer = new byte[contentLength];
            int totalRead = 0;

            while (totalRead < contentLength)
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, contentLength - totalRead));
                if (bytesRead == 0)
                {
                    throw new InvalidOperationException("Request body ended before all bytes were received.");
                }

                totalRead += bytesRead;
            }

            return buffer;
        }

        // The portal markup lives in Resources/PatronPortal.html and Resources/dj.html (embedded resources); loaded once on first request.
        private static readonly Lazy<string> CachedHtml = new(LoadHtmlContent);
        private static readonly Lazy<string> CachedDjHtml = new(LoadDjHtmlContent);
 
        private static string GetHtmlContent() => CachedHtml.Value;
        private static string GetDjHtmlContent() => CachedDjHtml.Value;
 
        private static string LoadHtmlContent()
        {
            Assembly assembly = typeof(PatronRequestServer).Assembly;
            string? resourceName = Array.Find(
                assembly.GetManifestResourceNames(),
                n => n.EndsWith("PatronPortal.html", StringComparison.OrdinalIgnoreCase));
 
            if (resourceName != null)
            {
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using StreamReader reader = new(stream, Encoding.UTF8);
                    return reader.ReadToEnd();
                }
            }
 
            LoggerService.LogError(
                "PatronRequestServer.LoadHtmlContent",
                new InvalidOperationException("Embedded resource 'PatronPortal.html' was not found."));
            return "<!DOCTYPE html><html><body><h1>KSRotation</h1><p>Portal resource missing.</p></body></html>";
        }

        private static string LoadDjHtmlContent()
        {
            Assembly assembly = typeof(PatronRequestServer).Assembly;
            string? resourceName = Array.Find(
                assembly.GetManifestResourceNames(),
                n => n.EndsWith("dj.html", StringComparison.OrdinalIgnoreCase));
 
            if (resourceName != null)
            {
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using StreamReader reader = new(stream, Encoding.UTF8);
                    return reader.ReadToEnd();
                }
            }
 
            LoggerService.LogError(
                "PatronRequestServer.LoadDjHtmlContent",
                new InvalidOperationException("Embedded resource 'dj.html' was not found."));
            return "<!DOCTYPE html><html><body><h1>KSRotation</h1><p>DJ portal resource missing.</p></body></html>";
        }
    }
}
