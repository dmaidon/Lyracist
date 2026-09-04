// Edited on Sep 3, 2026 @ 08:22:00 -> Add /billboard, /api/info, and /api/qr endpoints with Audience Billboard support
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
using QRCoder;
#if !MAUI
using Microsoft.EntityFrameworkCore;
#endif
using System.Security.Cryptography;

namespace KSRotation.Services
{
    public class PatronRequestServer(
        int port,
        Action<string, List<RequestedSong>, string, string> onRequestReceived,
        Func<string> onGetRotationJson,
        Func<string, bool> onVerifyPin,
        Func<string> onGetRequestsJson,
        Func<string, string, string, string, string, string, string, Task<string>> onHandleDjAction,
        Func<string> onGetSpecialEventsJson,
        Func<string> onGetActiveSpecialEvent,
        Func<string>? onGetVenueInfoJson = null)
    {
        private const int MaxRequestBodyBytes = 4_194_304; // 4 MB
        private const int MaxAvatarImageBytes = 2_097_152; // 2 MB - a profile avatar has no business being larger
        private const int MaxConcurrentConnections = 64;
        private const int MaxPinAttemptsBeforeLockout = 5;
        // Caps how long a single connection may take to send its full headers+body. Without this, a client
        // that trickles bytes one at a time (or never sends the final CRLFCRLF) holds a connection slot
        // forever — with only MaxConcurrentConnections slots, that's enough to starve every real patron/DJ.
        private static readonly TimeSpan RequestReadTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan PinLockoutDuration = TimeSpan.FromMinutes(2);

        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly int _port = port;
        private readonly Action<string, List<RequestedSong>, string, string> _onRequestReceived = onRequestReceived;
        private readonly Func<string> _onGetRotationJson = onGetRotationJson;
        private readonly Func<string, bool> _onVerifyPin = onVerifyPin;
        private readonly Func<string> _onGetRequestsJson = onGetRequestsJson;
        private readonly Func<string, string, string, string, string, string, string, Task<string>> _onHandleDjAction = onHandleDjAction;
        private readonly Func<string> _onGetSpecialEventsJson = onGetSpecialEventsJson;
        private readonly Func<string> _onGetActiveSpecialEvent = onGetActiveSpecialEvent;
        private readonly Func<string>? _onGetVenueInfoJson = onGetVenueInfoJson;
        private readonly SemaphoreSlim _connectionLimiter = new(MaxConcurrentConnections, MaxConcurrentConnections);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, PinAttemptState> _pinAttemptsByIp = new();

        // Separate from _pinAttemptsByIp above: singer PINs (login/profile/avatar upload) get the
        // same brute-force protection as the DJ PIN, but tracked independently so a patron
        // mistyping their own singer PIN can't affect (or be affected by) DJ login attempts from
        // a different device sharing the lockout state.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, PinAttemptState> _singerPinAttemptsByIp = new();

        // Once distinct client IPs pile up (long event, many patron devices), sweep out entries that
        // haven't attempted in a while so the dictionary doesn't grow for the lifetime of the server.
        private const int PinAttemptSweepThreshold = 256;
        private static readonly TimeSpan PinAttemptStaleThreshold = TimeSpan.FromMinutes(30);

        private sealed class PinAttemptState
        {
            public int FailedCount;
            public DateTime LockedUntilUtc;
            public DateTime LastAttemptUtc;
        }

        // Caps how many brand-new singer profiles a single IP can auto-register (via
        // /api/singer/login with a name that doesn't exist yet - see TryAllowRegistration).
        // Without this, an unauthenticated script on the venue WiFi can insert an unbounded
        // number of rows into the DJ's live singer table in a few minutes.
        private const int MaxRegistrationsPerWindow = 5;
        private static readonly TimeSpan RegistrationWindow = TimeSpan.FromMinutes(10);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RegistrationRateState> _registrationsByIp = new();

        private sealed class RegistrationRateState
        {
            public int Count;
            public DateTime WindowStartUtc;
        }

        private bool TryAllowRegistration(string clientIp)
        {
            var state = _registrationsByIp.GetOrAdd(clientIp, _ => new RegistrationRateState { WindowStartUtc = DateTime.UtcNow });

            lock (state)
            {
                if (DateTime.UtcNow - state.WindowStartUtc > RegistrationWindow)
                {
                    state.WindowStartUtc = DateTime.UtcNow;
                    state.Count = 0;
                }

                if (state.Count >= MaxRegistrationsPerWindow)
                {
                    return false;
                }

                state.Count++;
                return true;
            }
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
            finally
            {
                _cts?.Dispose();
                _cts = null;
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
                    if (!_connectionLimiter.Wait(0, CancellationToken.None))
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
            await using (NetworkStream stream = client.GetStream())
            // Reads go through a small buffer so header/body parsing isn't one syscall per byte;
            // responses are still written directly to `stream`, unbuffered.
            await using (BufferedStream readStream = new(stream, 4096))
            using (CancellationTokenSource readTimeoutCts = new(RequestReadTimeout))
            {
                try
                {
                    string headers = await ReadHeadersAsync(readStream, readTimeoutCts.Token);
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
                        else if (path == "/kiosk" || path == "/kiosk.html")
                        {
                            await SendHtmlResponseAsync(stream, GetKioskHtmlContent());
                        }
                        else if (path == "/dj" || path == "/dj.html")
                        {
                            await SendHtmlResponseAsync(stream, GetDjHtmlContent());
                        }
                        else if (path == "/billboard" || path == "/billboard.html")
                        {
                            await SendHtmlResponseAsync(stream, GetBillboardHtmlContent());
                        }
                        else if (path.StartsWith("/api/info", StringComparison.OrdinalIgnoreCase))
                        {
                            string json = _onGetVenueInfoJson?.Invoke() ?? "{}";
                            await SendJsonResponseAsync(stream, json);
                        }
                        else if (path.StartsWith("/api/qr", StringComparison.OrdinalIgnoreCase))
                        {
                            string text = ParseQueryParam(path, "text");
                            if (!string.IsNullOrEmpty(text))
                            {
                                using QRCodeGenerator qrGenerator = new();
                                using QRCodeData qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
                                using PngByteQRCode pngQr = new(qrCodeData);
                                byte[] qrBytes = pngQr.GetGraphic(12);
                                await SendImageResponseAsync(stream, qrBytes, "image/png");
                            }
                            else
                            {
                                await SendBadRequestAsync(stream, "{\"error\":\"Missing 'text' query parameter.\"}");
                            }
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
                        else if (path.StartsWith("/api/special-events", StringComparison.OrdinalIgnoreCase))
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
                            string json = _onGetSpecialEventsJson();
                            await SendJsonResponseAsync(stream, json);
                        }
                        else if (path.StartsWith("/api/special-event/active", StringComparison.OrdinalIgnoreCase))
                        {
                            string activeEvent = _onGetActiveSpecialEvent();
                            string json = JsonSerializer.Serialize(new { activeSpecialEvent = activeEvent });
                            await SendJsonResponseAsync(stream, json);
                        }
                        else if (path.StartsWith("/api/singer/avatar", StringComparison.OrdinalIgnoreCase))
                        {
                            string queryName = "";
                            int nameIdx = rawPath.IndexOf("name=");
                            if (nameIdx >= 0)
                            {
                                queryName = rawPath[(nameIdx + 5)..];
                                int ampIdx = queryName.IndexOf('&');
                                if (ampIdx >= 0)
                                {
                                    queryName = queryName[..ampIdx];
                                }
                                queryName = WebUtility.UrlDecode(queryName).Trim();
                            }

                            if (string.IsNullOrEmpty(queryName))
                            {
                                await SendRedirectResponseAsync(stream, "https://www.gravatar.com/avatar/00000000000000000000000000000000?d=mp&s=150");
                                return;
                            }

#if !MAUI
                            await using var context = new Lyracist.Data.LyracistDbContext();
                            var dbSinger = await context.Singers.FirstOrDefaultAsync(s => s.Name == queryName);
                            if (dbSinger != null)
                            {
                                if (dbSinger.AvatarType == "Gravatar" && !string.IsNullOrEmpty(dbSinger.AvatarSource))
                                {
                                    await SendRedirectResponseAsync(stream, $"https://www.gravatar.com/avatar/{dbSinger.AvatarSource}?d=identicon&s=150");
                                    return;
                                }
                                else if (dbSinger.AvatarType == "Uploaded" && !string.IsNullOrEmpty(dbSinger.AvatarSource))
                                {
                                    string? fullPath = ResolveAvatarPath(Lyracist.Shared.Globals.AvatarsDir, dbSinger.AvatarSource);
                                    if (fullPath != null && File.Exists(fullPath))
                                    {
                                        try
                                        {
                                            byte[] fileBytes = await File.ReadAllBytesAsync(fullPath);
                                            await SendImageResponseAsync(stream, fileBytes);
                                            return;
                                        }
                                        catch (Exception ex)
                                        {
                                            LoggerService.LogError("ServeUploadedAvatar", ex);
                                        }
                                    }
                                }
                            }
#endif

                            await SendRedirectResponseAsync(stream, "https://www.gravatar.com/avatar/00000000000000000000000000000000?d=mp&s=150");
                        }
                        else
                        {
                            await SendNotFoundAsync(stream);
                        }
                    }
                    else if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.StartsWith("/api/request", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] bodyBytes = await ReadBodyBytesAsync(readStream, contentLength, readTimeoutCts.Token);
                        string body = Encoding.UTF8.GetString(bodyBytes);

                        using JsonDocument doc = JsonDocument.Parse(body);
                        JsonElement root = doc.RootElement;
                        string name = root.TryGetProperty("name", out var nProp) ? (nProp.GetString() ?? "") : "";
                        string duetPartner = root.TryGetProperty("duetPartner", out var dpProp) ? (dpProp.GetString() ?? "") : (root.TryGetProperty("duetPartnerName", out var dpnProp) ? (dpnProp.GetString() ?? "") : "");
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
                            _onRequestReceived(name.Trim(), songs, requestType, duetPartner.Trim());
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

                        byte[] bodyBytes = await ReadBodyBytesAsync(readStream, contentLength, readTimeoutCts.Token);
                        string body = Encoding.UTF8.GetString(bodyBytes);

                        using JsonDocument doc = JsonDocument.Parse(body);
                        JsonElement root = doc.RootElement;
                        string action = root.TryGetProperty("action", out var actProp) ? (actProp.GetString() ?? "") : "";
                        string targetId = root.TryGetProperty("targetId", out var tgtProp) ? (tgtProp.GetString() ?? "") : "";
                        string extraData = root.TryGetProperty("extraData", out var extProp) ? (extProp.GetString() ?? "") : "";
                        string name = root.TryGetProperty("name", out var nameProp) ? (nameProp.GetString() ?? "") : "";
                        string duetPartner = root.TryGetProperty("duetPartner", out var dpProp) ? (dpProp.GetString() ?? "") : (root.TryGetProperty("duetPartnerName", out var dpnProp) ? (dpnProp.GetString() ?? "") : "");
                        string song = root.TryGetProperty("song", out var songProp) ? (songProp.GetString() ?? "") : "";
                        string artist = root.TryGetProperty("artist", out var artProp) ? (artProp.GetString() ?? "") : "";

                        string error = await _onHandleDjAction(action, targetId, extraData, name, song, artist, duetPartner);
                        if (string.IsNullOrEmpty(error))
                        {
                            await SendJsonResponseAsync(stream, "{\"success\":true}");
                        }
                        else
                        {
                            await SendBadRequestAsync(stream, $"{{\"error\":\"{JsonEncodedText.Encode(error)}\"}}");
                        }
                    }
                    else if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.StartsWith("/api/singer/login", StringComparison.OrdinalIgnoreCase))
                    {
#if !MAUI
                        byte[] bodyBytes = await ReadBodyBytesAsync(readStream, contentLength, readTimeoutCts.Token);
                        string body = Encoding.UTF8.GetString(bodyBytes);

                        using JsonDocument doc = JsonDocument.Parse(body);
                        JsonElement root = doc.RootElement;
                        string name = root.TryGetProperty("name", out var nProp) ? (nProp.GetString() ?? "") : "";
                        string singerPin = root.TryGetProperty("pin", out var pProp) ? (pProp.GetString() ?? "") : "";

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            await SendBadRequestAsync(stream, "{\"error\":\"Singer name is required.\"}");
                            return;
                        }

                        await using var context = new Lyracist.Data.LyracistDbContext();
                        var dbSinger = await context.Singers.FirstOrDefaultAsync(s => s.Name == name);
                        if (dbSinger == null)
                        {
                            if (!TryAllowRegistration(clientIp))
                            {
                                await SendTooManyRequestsAsync(stream);
                                return;
                            }

                            dbSinger = new Lyracist.Data.Models.Singer
                            {
                                Name = name,
                                PinCode = singerPin,
                                AvatarType = "None",
                                AvatarSource = ""
                            };
                            context.Singers.Add(dbSinger);
                            await context.SaveChangesAsync();

                            await SendJsonResponseAsync(stream, JsonSerializer.Serialize(new
                            {
                                success = true,
                                registered = true,
                                singer = new { name = dbSinger.Name, avatarType = dbSinger.AvatarType, avatarSource = dbSinger.AvatarSource, email = dbSinger.Email, vocalRange = dbSinger.VocalRange, customTitle = dbSinger.CustomTitle }
                            }));
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(dbSinger.PinCode))
                            {
                                dbSinger.PinCode = singerPin;
                                await context.SaveChangesAsync();

                                await SendJsonResponseAsync(stream, JsonSerializer.Serialize(new
                                {
                                    success = true,
                                    claimed = true,
                                    singer = new { name = dbSinger.Name, avatarType = dbSinger.AvatarType, avatarSource = dbSinger.AvatarSource, email = dbSinger.Email, vocalRange = dbSinger.VocalRange, customTitle = dbSinger.CustomTitle }
                                }));
                            }
                            else
                            {
                                bool authorized = TryAuthorizeSinger(clientIp, () => dbSinger.PinCode == singerPin, out bool singerLockedOut);
                                if (singerLockedOut)
                                {
                                    await SendTooManyRequestsAsync(stream);
                                }
                                else if (authorized)
                                {
                                    await SendJsonResponseAsync(stream, JsonSerializer.Serialize(new
                                    {
                                        success = true,
                                        singer = new { name = dbSinger.Name, avatarType = dbSinger.AvatarType, avatarSource = dbSinger.AvatarSource, email = dbSinger.Email, vocalRange = dbSinger.VocalRange, customTitle = dbSinger.CustomTitle }
                                    }));
                                }
                                else
                                {
                                    await SendBadRequestAsync(stream, "{\"error\":\"Incorrect PIN code for this singer name.\"}");
                                }
                            }
                        }
#else
                        await SendBadRequestAsync(stream, "{\"error\":\"Performer profiles not supported on mobile rotation view controller.\"}");
#endif
                    }
                    else if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.StartsWith("/api/singer/profile", StringComparison.OrdinalIgnoreCase))
                    {
#if !MAUI
                        byte[] bodyBytes = await ReadBodyBytesAsync(readStream, contentLength, readTimeoutCts.Token);
                        string body = Encoding.UTF8.GetString(bodyBytes);

                        using JsonDocument doc = JsonDocument.Parse(body);
                        JsonElement root = doc.RootElement;
                        string name = root.TryGetProperty("name", out var nProp) ? (nProp.GetString() ?? "") : "";
                        string singerPin = root.TryGetProperty("pin", out var pProp) ? (pProp.GetString() ?? "") : "";
                        string email = root.TryGetProperty("email", out var eProp) ? (eProp.GetString() ?? "") : "";
                        string avatarType = root.TryGetProperty("avatarType", out var atProp) ? (atProp.GetString() ?? "None") : "None";
                        string vocalRange = root.TryGetProperty("vocalRange", out var vrProp) ? (vrProp.GetString() ?? "") : "";
                        string customTitle = root.TryGetProperty("customTitle", out var ctProp) ? (ctProp.GetString() ?? "") : "";

                        await using var context = new Lyracist.Data.LyracistDbContext();
                        var dbSinger = await context.Singers.FirstOrDefaultAsync(s => s.Name == name);
                        bool profileAuthorized = TryAuthorizeSinger(clientIp, () => dbSinger != null && dbSinger.PinCode == singerPin, out bool profileLockedOut);
                        if (profileLockedOut)
                        {
                            await SendTooManyRequestsAsync(stream);
                            return;
                        }
                        if (!profileAuthorized || dbSinger == null)
                        {
                            await SendUnauthorizedAsync(stream);
                            return;
                        }

                        dbSinger.Email = email;
                        dbSinger.AvatarType = avatarType;
                        dbSinger.VocalRange = vocalRange;
                        dbSinger.CustomTitle = customTitle;
                        if (avatarType == "Gravatar")
                        {
                            dbSinger.AvatarSource = MD5Hash(email);
                        }

                        await context.SaveChangesAsync();
                        await SendJsonResponseAsync(stream, "{\"success\":true}");
#else
                        await SendBadRequestAsync(stream, "{\"error\":\"Performer profiles not supported on mobile rotation view controller.\"}");
#endif
                    }
                    else if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.StartsWith("/api/singer/avatar/upload", StringComparison.OrdinalIgnoreCase))
                    {
#if !MAUI
                        byte[] bodyBytes = await ReadBodyBytesAsync(readStream, contentLength, readTimeoutCts.Token);
                        string body = Encoding.UTF8.GetString(bodyBytes);

                        using JsonDocument doc = JsonDocument.Parse(body);
                        JsonElement root = doc.RootElement;
                        string name = root.TryGetProperty("name", out var nProp) ? (nProp.GetString() ?? "") : "";
                        string singerPin = root.TryGetProperty("pin", out var pProp) ? (pProp.GetString() ?? "") : "";
                        string imageBase64 = root.TryGetProperty("image", out var imgProp) ? (imgProp.GetString() ?? "") : "";

                        await using var context = new Lyracist.Data.LyracistDbContext();
                        var dbSinger = await context.Singers.FirstOrDefaultAsync(s => s.Name == name);
                        bool avatarAuthorized = TryAuthorizeSinger(clientIp, () => dbSinger != null && dbSinger.PinCode == singerPin, out bool avatarLockedOut);
                        if (avatarLockedOut)
                        {
                            await SendTooManyRequestsAsync(stream);
                            return;
                        }
                        if (!avatarAuthorized || dbSinger == null)
                        {
                            await SendUnauthorizedAsync(stream);
                            return;
                        }

                        if (!string.IsNullOrEmpty(imageBase64))
                        {
                            if (imageBase64.Contains(','))
                            {
                                imageBase64 = imageBase64[(imageBase64.IndexOf(',') + 1)..];
                            }
                            byte[] imgBytes = Convert.FromBase64String(imageBase64);

                            // The request body cap (MaxRequestBodyBytes) allows up to 4 MB overall,
                            // but a single avatar image has no business being that large - and
                            // without checking the actual bytes, anything decoded from the "image"
                            // field would be written straight to disk under a .jpg extension
                            // regardless of what it actually contains.
                            if (imgBytes.Length > MaxAvatarImageBytes || !LooksLikeImage(imgBytes))
                            {
                                await SendBadRequestAsync(stream, "{\"error\":\"Invalid or oversized image.\"}");
                                return;
                            }

                            string avatarsDir = Lyracist.Shared.Globals.AvatarsDir;
                            Directory.CreateDirectory(avatarsDir);

                            // Filename is derived from a GUID, never from patron-controlled input,
                            // so it cannot contain path-traversal segments.
                            string filename = $"{Guid.NewGuid():N}.jpg";
                            string? fullPath = ResolveAvatarPath(avatarsDir, filename);
                            if (fullPath == null)
                            {
                                await SendBadRequestAsync(stream, "{\"error\":\"Invalid avatar file name.\"}");
                                return;
                            }

                            if (dbSinger.AvatarType == "Uploaded" && !string.IsNullOrEmpty(dbSinger.AvatarSource))
                            {
                                string? oldPath = ResolveAvatarPath(avatarsDir, dbSinger.AvatarSource);
                                if (oldPath != null && File.Exists(oldPath))
                                {
                                    try { File.Delete(oldPath); } catch {}
                                }
                            }

                            await File.WriteAllBytesAsync(fullPath, imgBytes);

                            dbSinger.AvatarType = "Uploaded";
                            dbSinger.AvatarSource = filename;
                            await context.SaveChangesAsync();

                            await SendJsonResponseAsync(stream, JsonSerializer.Serialize(new { success = true, avatarSource = filename }));
                        }
                        else
                        {
                            await SendBadRequestAsync(stream, "{\"error\":\"Image content is empty.\"}");
                        }
#else
                        await SendBadRequestAsync(stream, "{\"error\":\"Performer profiles not supported on mobile rotation view controller.\"}");
#endif
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

        private static async Task<string> ReadHeadersAsync(Stream stream, CancellationToken cancellationToken)
        {
            const int maxHeaderBytes = 16_384;
            await using MemoryStream headerBuffer = new();
            byte[] singleByte = new byte[1];

            while (headerBuffer.Length < maxHeaderBytes)
            {
                int read = await stream.ReadAsync(singleByte.AsMemory(0, 1), cancellationToken);
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
            await stream.WriteAsync(responseBytes);
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
            await stream.WriteAsync(responseBytes);
        }

        private static async Task SendImageResponseAsync(NetworkStream stream, byte[] imageBytes, string contentType = "image/png")
        {
            byte[] headerBytes = Encoding.UTF8.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                $"Content-Type: {contentType}\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                $"Content-Length: {imageBytes.Length}\r\n" +
                "Cache-Control: public, max-age=60\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(headerBytes);
            await stream.WriteAsync(imageBytes);
        }

        private static string ParseQueryParam(string url, string paramName)
        {
            int queryIdx = url.IndexOf('?');
            if (queryIdx < 0 || queryIdx == url.Length - 1) return string.Empty;
            string query = url[(queryIdx + 1)..];
            foreach (string pair in query.Split('&'))
            {
                string[] kv = pair.Split('=', 2);
                if (kv.Length == 2 && kv[0].Equals(paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1]);
                }
            }
            return string.Empty;
        }

        private static async Task SendCorsPreflightResponseAsync(NetworkStream stream)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(
                "HTTP/1.1 204 No Content\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type, X-DJ-PIN\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(responseBytes);
        }

        private static async Task SendNotFoundAsync(NetworkStream stream)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(responseBytes);
        }

        private static async Task SendUnauthorizedAsync(NetworkStream stream)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(
                "HTTP/1.1 401 Unauthorized\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                "Content-Length: 25\r\n" +
                "Connection: close\r\n\r\n" +
                "{\"error\":\"Unauthorized\"}");
            await stream.WriteAsync(responseBytes);
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
            await stream.WriteAsync(responseBytes);
        }

        private static async Task SendRedirectResponseAsync(NetworkStream stream, string redirectUrl)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(
                "HTTP/1.1 302 Found\r\n" +
                $"Location: {redirectUrl}\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Content-Length: 0\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(responseBytes);
        }

#if !MAUI
        private static async Task SendImageResponseAsync(NetworkStream stream, byte[] imageBytes)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: image/jpeg\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                $"Content-Length: {imageBytes.Length}\r\n" +
                "Connection: close\r\n\r\n");

            byte[] fullResponse = new byte[responseBytes.Length + imageBytes.Length];
            Buffer.BlockCopy(responseBytes, 0, fullResponse, 0, responseBytes.Length);
            Buffer.BlockCopy(imageBytes, 0, fullResponse, responseBytes.Length, imageBytes.Length);

            await stream.WriteAsync(fullResponse);
        }

        private static string MD5Hash(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input.Trim().ToLowerInvariant());
            byte[] hashBytes = System.Security.Cryptography.MD5.HashData(inputBytes);
            return Convert.ToHexStringLower(hashBytes);
        }

        /// <summary>
        /// Resolves an avatar file name to a full path guaranteed to stay inside <paramref name="avatarsDir"/>,
        /// rejecting any path-traversal or rooted-path attempt (e.g. "..\..\Windows\evil.jpg" or "C:\evil.jpg").
        /// Returns null if the resolved path would escape the avatars directory.
        /// </summary>
        private static string? ResolveAvatarPath(string avatarsDir, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.IsPathRooted(fileName))
            {
                return null;
            }

            string fullAvatarsDir = Path.GetFullPath(avatarsDir);
            string fullPath = Path.GetFullPath(Path.Combine(fullAvatarsDir, fileName));

            string jail = fullAvatarsDir.EndsWith(Path.DirectorySeparatorChar) ? fullAvatarsDir : fullAvatarsDir + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(jail, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }

        /// <summary>
        /// Cheap format sniff so an avatar upload can't write arbitrary patron-supplied bytes to
        /// disk under a .jpg extension - checks for the magic bytes of the image formats a phone
        /// camera or gallery picker would actually produce (JPEG, PNG, WEBP, GIF).
        /// </summary>
        private static bool LooksLikeImage(byte[] bytes)
        {
            if (bytes.Length < 12) return false;

            // JPEG: FF D8 FF
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return true;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;

            // GIF: "GIF87a" or "GIF89a"
            if (bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == '8'
                && (bytes[4] == '7' || bytes[4] == '9') && bytes[5] == 'a') return true;

            // WEBP: "RIFF"....."WEBP"
            if (bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
                && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P') return true;

            return false;
        }
#endif

        /// <summary>
        /// Verifies the DJ PIN for <paramref name="clientIp"/>, tracking failed attempts per IP and
        /// locking that IP out for <see cref="PinLockoutDuration"/> after <see cref="MaxPinAttemptsBeforeLockout"/>
        /// consecutive failures. Guards against brute-forcing a short numeric PIN over the LAN.
        /// </summary>
        private bool TryAuthorizeDj(string clientIp, string pin, out bool lockedOut) =>
            TryAuthorizePin(_pinAttemptsByIp, clientIp, () => _onVerifyPin(pin), out lockedOut);

        /// <summary>
        /// Verifies a singer's own PIN (login/profile/avatar upload) for <paramref name="clientIp"/>,
        /// with the same per-IP lockout policy as <see cref="TryAuthorizeDj"/> - a singer's PIN is
        /// exactly as brute-forceable over the LAN as the DJ's.
        /// </summary>
        private bool TryAuthorizeSinger(string clientIp, Func<bool> verify, out bool lockedOut) =>
            TryAuthorizePin(_singerPinAttemptsByIp, clientIp, verify, out lockedOut);

        private bool TryAuthorizePin(
            System.Collections.Concurrent.ConcurrentDictionary<string, PinAttemptState> attemptsByIp,
            string clientIp,
            Func<bool> verify,
            out bool lockedOut)
        {
            PinAttemptState state = attemptsByIp.GetOrAdd(clientIp, _ => new PinAttemptState());

            bool result;
            lock (state)
            {
                state.LastAttemptUtc = DateTime.UtcNow;

                if (DateTime.UtcNow < state.LockedUntilUtc)
                {
                    lockedOut = true;
                    return false;
                }

                if (verify())
                {
                    state.FailedCount = 0;
                    state.LockedUntilUtc = DateTime.MinValue;
                    lockedOut = false;
                    result = true;
                }
                else
                {
                    state.FailedCount++;
                    if (state.FailedCount >= MaxPinAttemptsBeforeLockout)
                    {
                        state.LockedUntilUtc = DateTime.UtcNow.Add(PinLockoutDuration);
                        state.FailedCount = 0;
                    }

                    lockedOut = false;
                    result = false;
                }
            }

            if (attemptsByIp.Count > PinAttemptSweepThreshold)
            {
                SweepStalePinAttempts(attemptsByIp);
            }

            return result;
        }

        private static void SweepStalePinAttempts(System.Collections.Concurrent.ConcurrentDictionary<string, PinAttemptState> attemptsByIp)
        {
            DateTime cutoff = DateTime.UtcNow - PinAttemptStaleThreshold;
            foreach (KeyValuePair<string, PinAttemptState> entry in attemptsByIp)
            {
                if (entry.Value.LastAttemptUtc < cutoff)
                {
                    attemptsByIp.TryRemove(entry.Key, out _);
                }
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
            await stream.WriteAsync(responseBytes);
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

        private static async Task<byte[]> ReadBodyBytesAsync(Stream stream, int contentLength, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[contentLength];
            int totalRead = 0;

            while (totalRead < contentLength)
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, contentLength - totalRead), cancellationToken);
                if (bytesRead == 0)
                {
                    throw new InvalidOperationException("Request body ended before all bytes were received.");
                }

                totalRead += bytesRead;
            }

            return buffer;
        }

        // The portal markup lives in Resources/PatronPortal.html, Resources/kiosk.html, Resources/dj.html, and Resources/billboard.html (embedded resources); loaded once on first request.
        private static readonly Lazy<string> CachedHtml = new(LoadHtmlContent);
        private static readonly Lazy<string> CachedDjHtml = new(LoadDjHtmlContent);
        private static readonly Lazy<string> CachedKioskHtml = new(LoadKioskHtmlContent);
        private static readonly Lazy<string> CachedBillboardHtml = new(LoadBillboardHtmlContent);

        private static string GetHtmlContent() => CachedHtml.Value;
        private static string GetDjHtmlContent() => CachedDjHtml.Value;
        private static string GetKioskHtmlContent() => CachedKioskHtml.Value;
        private static string GetBillboardHtmlContent() => CachedBillboardHtml.Value;

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

        private static string LoadKioskHtmlContent()
        {
            Assembly assembly = typeof(PatronRequestServer).Assembly;
            string? resourceName = Array.Find(
                assembly.GetManifestResourceNames(),
                n => n.EndsWith("kiosk.html", StringComparison.OrdinalIgnoreCase));

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
                "PatronRequestServer.LoadKioskHtmlContent",
                new InvalidOperationException("Embedded resource 'kiosk.html' was not found."));
            return "<!DOCTYPE html><html><body><h1>KSRotation</h1><p>Kiosk portal resource missing.</p></body></html>";
        }

        private static string LoadBillboardHtmlContent()
        {
            Assembly assembly = typeof(PatronRequestServer).Assembly;
            string? resourceName = Array.Find(
                assembly.GetManifestResourceNames(),
                n => n.EndsWith("billboard.html", StringComparison.OrdinalIgnoreCase));

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
                "PatronRequestServer.LoadBillboardHtmlContent",
                new InvalidOperationException("Embedded resource 'billboard.html' was not found."));
            return "<!DOCTYPE html><html><body><h1>KSRotation</h1><p>Billboard portal resource missing.</p></body></html>";
        }
    }
}
