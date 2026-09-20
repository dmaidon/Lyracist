// Edited on Sep 20, 2026 @ 07:11:00 -> Fix Content-Length byte count in SendUnauthorizedAsync and add FlushAsync to all HTTP responses
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
        Func<string, string, string, string, string, string, string, bool, Task<string>> onHandleDjAction,
        Func<string> onGetSpecialEventsJson,
        Func<string> onGetActiveSpecialEvent,
        Func<string>? onGetVenueInfoJson = null,
        Func<string, string, bool>? onCheckDuplicateSong = null,
        Func<string?>? onCheckRequestAllowed = null,
        Action<string, double, double>? onVenueLocationSynced = null,
        Func<SessionHandoffPayload>? onExportSession = null,
        Func<SessionHandoffPayload, Task<string?>>? onImportSession = null,
        Func<DiscoveredPeer>? onGetProbeInfo = null,
        Action<string, int, string>? onSessionExportedToPeer = null)
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
        private readonly Func<string, string, string, string, string, string, string, bool, Task<string>> _onHandleDjAction = onHandleDjAction;
        private readonly Func<string> _onGetSpecialEventsJson = onGetSpecialEventsJson;
        private readonly Func<string> _onGetActiveSpecialEvent = onGetActiveSpecialEvent;
        private readonly Func<string>? _onGetVenueInfoJson = onGetVenueInfoJson;
        private readonly Func<string, string, bool>? _onCheckDuplicateSong = onCheckDuplicateSong;
        private readonly Func<string?>? _onCheckRequestAllowed = onCheckRequestAllowed;
        private readonly Action<string, double, double>? _onVenueLocationSynced = onVenueLocationSynced;
        private readonly Func<SessionHandoffPayload>? _onExportSession = onExportSession;
        private readonly Func<SessionHandoffPayload, Task<string?>>? _onImportSession = onImportSession;
        private readonly Func<DiscoveredPeer>? _onGetProbeInfo = onGetProbeInfo;
        private readonly Action<string, int, string>? _onSessionExportedToPeer = onSessionExportedToPeer;
        private readonly SemaphoreSlim _connectionLimiter = new(MaxConcurrentConnections, MaxConcurrentConnections);

        // Serializes the /api/singer/login check-then-write sequence (find-by-name, then insert or
        // claim-empty-PIN). Without this, two near-simultaneous logins for the same new name can both
        // see no existing row and both insert, creating duplicate Singer rows for one name - there is
        // no unique DB constraint on Singer.Name to catch this at the database level. Login is low
        // frequency (a patron joining, not a hot path), so serializing it process-wide costs nothing
        // noticeable.
        private readonly SemaphoreSlim _singerLoginLock = new(1, 1);
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
                    var queryParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    int qIdx = rawPath.IndexOf('?');
                    if (qIdx >= 0)
                    {
                        path = rawPath[..qIdx];
                        string query = rawPath[(qIdx + 1)..];
                        foreach (string param in query.Split('&'))
                        {
                            string[] kv = param.Split('=');
                            if (kv.Length == 2)
                            {
                                string decodedKey = WebUtility.UrlDecode(kv[0]);
                                string decodedVal = WebUtility.UrlDecode(kv[1]);
                                queryParams[decodedKey] = decodedVal;
                                if (decodedKey.Equals("pin", StringComparison.OrdinalIgnoreCase))
                                {
                                    queryPin = decodedVal;
                                }
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
                        else if (path == "/handoff" || path == "/handoff.html" || path == "/switch")
                        {
                            await SendHtmlResponseAsync(stream, GetHandoffHtmlContent());
                        }
                        else if (path.StartsWith("/api/session/handoff/ack", StringComparison.OrdinalIgnoreCase))
                        {
                            // Called by the peer only after it has successfully imported the payload
                            // from the /api/session/handoff response below - this is what actually
                            // signals a completed handoff, not the mere sending of the export. The
                            // pin is re-checked (not just trusted from the earlier request) so this
                            // endpoint can't be used to spoof a handoff notification on its own. The
                            // peer's address is taken from the live TCP connection (clientIp), not
                            // from a caller-supplied query value, so this can't be used to redirect
                            // the exporting device's UI to an arbitrary host.
                            if (!TryAuthorizeDj(clientIp, pin, out bool ackLockedOut))
                            {
                                if (ackLockedOut)
                                {
                                    await SendTooManyRequestsAsync(stream);
                                }
                                else
                                {
                                    await SendUnauthorizedAsync(stream);
                                }
                                return;
                            }

                            int ackPeerPort = queryParams.TryGetValue("hostPort", out var ahpStr) && int.TryParse(ahpStr, out int ahp) && ahp is > 0 and <= 65535
                                ? ahp
                                : 5000;
                            string ackPeerPin = queryParams.TryGetValue("djPin", out var adPin) ? adPin : string.Empty;
                            _onSessionExportedToPeer?.Invoke(clientIp, ackPeerPort, ackPeerPin);

                            await SendJsonResponseAsync(stream, "{\"ok\":true}");
                        }
                        else if (path.StartsWith("/api/session/handoff", StringComparison.OrdinalIgnoreCase))
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
                            var payload = _onExportSession?.Invoke();
                            string json = payload != null ? JsonSerializer.Serialize(payload, AppJsonContext.Default.SessionHandoffPayload) : "{}";
                            await SendJsonResponseAsync(stream, json);
                        }
                        else if (path.StartsWith("/api/device/probe", StringComparison.OrdinalIgnoreCase))
                        {
                            var probe = _onGetProbeInfo?.Invoke() ?? new DiscoveredPeer
                            {
                                AppName = "KSRotation",
                                DeviceName = Environment.MachineName
                            };
                            string json = JsonSerializer.Serialize(probe, AppJsonContext.Default.DiscoveredPeer);
                            await SendJsonResponseAsync(stream, json);
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
                        else if (path.StartsWith("/api/venue/location", StringComparison.OrdinalIgnoreCase))
                        {
                            var venues = Lyracist.Shared.VenueLocationStore.Load();
                            string currentVenue = "";
                            if (_onGetVenueInfoJson != null)
                            {
                                try
                                {
                                    var infoObj = JsonSerializer.Deserialize<VenueInfoResponseDto>(_onGetVenueInfoJson());
                                    currentVenue = infoObj?.venue ?? "";
                                }
                                catch { }
                            }
                            var matched = venues.FirstOrDefault(v => string.Equals(v.Name, currentVenue, StringComparison.OrdinalIgnoreCase));
                            string json = JsonSerializer.Serialize(matched ?? new Lyracist.Shared.VenueLocationItem { Name = currentVenue });
                            await SendJsonResponseAsync(stream, json);
                        }
                        else if (path.StartsWith("/api/singer/avatar", StringComparison.OrdinalIgnoreCase))
                        {
                            // ParseQueryParam matches the whole "name" key between '?'/'&' delimiters,
                            // unlike a raw IndexOf("name=") which used to match inside any other param
                            // whose value or key happened to contain that substring (e.g. "?nickname=Bob&name=Alice"
                            // would previously resolve to "Bob").
                            string queryName = ParseQueryParam(rawPath, "name").Trim();

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
                                            await SendImageResponseAsync(stream, fileBytes, GetContentTypeForAvatarExtension(fullPath));
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
                            if (_onCheckRequestAllowed != null)
                            {
                                string? blockedReason = _onCheckRequestAllowed();
                                if (!string.IsNullOrEmpty(blockedReason))
                                {
                                    await SendBadRequestAsync(stream, $"{{\"error\":\"{JsonEncodedText.Encode(blockedReason)}\"}}");
                                    return;
                                }
                            }

                            if (_onCheckDuplicateSong != null)
                            {
                                foreach (var reqSong in songs)
                                {
                                    if (_onCheckDuplicateSong(reqSong.Song, reqSong.Artist))
                                    {
                                        await SendBadRequestAsync(stream, $"{{\"error\":\"'{reqSong.Song}' has already been performed or queued in this session. Duplicate songs are blocked by the DJ.\"}}");
                                        return;
                                    }
                                }
                            }

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
                        bool isSpecial = root.TryGetProperty("isSpecial", out var specProp) && (specProp.ValueKind == JsonValueKind.True || (specProp.ValueKind == JsonValueKind.String && bool.TryParse(specProp.GetString(), out var spB) && spB));

                        string error = await _onHandleDjAction(action, targetId, extraData, name, song, artist, duetPartner, isSpecial);
                        if (string.IsNullOrEmpty(error))
                        {
                            await SendJsonResponseAsync(stream, "{\"success\":true}");
                        }
                        else
                        {
                            await SendBadRequestAsync(stream, $"{{\"error\":\"{JsonEncodedText.Encode(error)}\"}}");
                        }
                    }
                    else if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.StartsWith("/api/special-event/active", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] bodyBytes = await ReadBodyBytesAsync(readStream, contentLength, readTimeoutCts.Token);
                        string body = Encoding.UTF8.GetString(bodyBytes);
                        string targetEvent = "None";
                        string performer = "";
                        try
                        {
                            using var doc = JsonDocument.Parse(body);
                            if (doc.RootElement.TryGetProperty("activeSpecialEvent", out var evtProp))
                            {
                                targetEvent = evtProp.GetString() ?? "None";
                            }
                            else if (doc.RootElement.TryGetProperty("event", out var eProp))
                            {
                                targetEvent = eProp.GetString() ?? "None";
                            }
                            if (doc.RootElement.TryGetProperty("performer", out var pProp))
                            {
                                performer = pProp.GetString() ?? "";
                            }
                        }
                        catch
                        {
                            if (!string.IsNullOrWhiteSpace(body)) targetEvent = body.Trim();
                        }

                        string error = await _onHandleDjAction("set-special-event", targetEvent, performer, performer, "", "", "", false);
                        if (string.IsNullOrEmpty(error))
                        {
                            await SendJsonResponseAsync(stream, "{\"success\":true}");
                        }
                        else
                        {
                            await SendBadRequestAsync(stream, $"{{\"error\":\"{JsonEncodedText.Encode(error)}\"}}");
                        }
                    }
                    else if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.StartsWith("/api/venue/location", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] bodyBytes = await ReadBodyBytesAsync(readStream, contentLength, readTimeoutCts.Token);
                        string body = Encoding.UTF8.GetString(bodyBytes);
                        try
                        {
                            using JsonDocument doc = JsonDocument.Parse(body);
                            JsonElement root = doc.RootElement;
                            double? lat = root.TryGetProperty("latitude", out var latEl) && latEl.TryGetDouble(out double latVal) ? latVal : null;
                            double? lon = root.TryGetProperty("longitude", out var lonEl) && lonEl.TryGetDouble(out double lonVal) ? lonVal : null;
                            string? venueName = root.TryGetProperty("venueName", out var vEl) ? vEl.GetString() : null;
                            string? wifiSsid = root.TryGetProperty("wifiSsid", out var wEl) ? wEl.GetString() : null;

                            if (lat.HasValue && lon.HasValue)
                            {
                                Lyracist.Shared.WindowsLocationService.SetSyncedCoordinates(lat.Value, lon.Value);
                                if (!string.IsNullOrWhiteSpace(venueName))
                                {
                                    Lyracist.Shared.VenueLocationStore.UpsertVenue(venueName, lat.Value, lon.Value, wifiSsid);
                                    _onVenueLocationSynced?.Invoke(venueName, lat.Value, lon.Value);
                                }
                            }
                            await SendJsonResponseAsync(stream, "{\"success\":true}");
                        }
                        catch (Exception ex)
                        {
                            await SendBadRequestAsync(stream, $"{{\"error\":\"{JsonEncodedText.Encode(ex.Message)}\"}}");
                        }
                    }
                    else if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.StartsWith("/api/session/handoff", StringComparison.OrdinalIgnoreCase))
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

                        try
                        {
                            var payload = JsonSerializer.Deserialize<SessionHandoffPayload>(body, AppJsonContext.Default.SessionHandoffPayload);
                            if (payload == null)
                            {
                                await SendBadRequestAsync(stream, "{\"error\":\"Invalid session handoff payload.\"}");
                                return;
                            }

                            string? error = null;
                            if (_onImportSession != null)
                            {
                                error = await _onImportSession(payload);
                            }

                            if (string.IsNullOrEmpty(error))
                            {
                                await SendJsonResponseAsync(stream, "{\"success\":true}");
                            }
                            else
                            {
                                await SendBadRequestAsync(stream, $"{{\"error\":\"{JsonEncodedText.Encode(error)}\"}}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggerService.LogError("PatronRequestServer.ImportHandoff", ex);
                            await SendBadRequestAsync(stream, $"{{\"error\":\"{JsonEncodedText.Encode(ex.Message)}\"}}");
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

                        // The find-then-insert (or find-then-claim-empty-PIN) sequence below is a
                        // check-then-write race with no unique DB constraint on Singer.Name to catch
                        // it, so it must be serialized - see _singerLoginLock's own comment.
                        Lyracist.Data.Models.Singer? dbSinger;
                        bool wasRegistered = false;
                        bool wasClaimed = false;
                        bool rateLimited = false;
                        await _singerLoginLock.WaitAsync(readTimeoutCts.Token);
                        try
                        {
                            dbSinger = await context.Singers.FirstOrDefaultAsync(s => s.Name == name);
                            if (dbSinger == null)
                            {
                                if (!TryAllowRegistration(clientIp))
                                {
                                    // Just note it and fall through to the finally - by this point the
                                    // DB read has already completed and nothing further will write, so
                                    // there's nothing left for the lock to serialize. Awaiting the 429
                                    // send while still holding it would stall every other patron's login
                                    // behind however long this client takes to drain its socket.
                                    rateLimited = true;
                                }
                                else
                                {
                                    dbSinger = new Lyracist.Data.Models.Singer
                                    {
                                        Name = name,
                                        PinCode = singerPin,
                                        AvatarType = "None",
                                        AvatarSource = ""
                                    };
                                    context.Singers.Add(dbSinger);
                                    await context.SaveChangesAsync();
                                    wasRegistered = true;
                                }
                            }
                            else if (string.IsNullOrEmpty(dbSinger.PinCode))
                            {
                                dbSinger.PinCode = singerPin;
                                await context.SaveChangesAsync();
                                wasClaimed = true;
                            }
                        }
                        finally
                        {
                            _singerLoginLock.Release();
                        }

                        if (rateLimited)
                        {
                            await SendTooManyRequestsAsync(stream);
                            return;
                        }

                        // Non-null by construction: the only path that leaves dbSinger null returns
                        // early (the rateLimited check above) before reaching here.
                        var confirmedSinger = dbSinger!;

                        if (wasRegistered)
                        {
                            await SendJsonResponseAsync(stream, JsonSerializer.Serialize(new
                            {
                                success = true,
                                registered = true,
                                singer = new { name = confirmedSinger.Name, avatarType = confirmedSinger.AvatarType, avatarSource = confirmedSinger.AvatarSource, email = confirmedSinger.Email, vocalRange = confirmedSinger.VocalRange, customTitle = confirmedSinger.CustomTitle }
                            }));
                        }
                        else if (wasClaimed)
                        {
                            await SendJsonResponseAsync(stream, JsonSerializer.Serialize(new
                            {
                                success = true,
                                claimed = true,
                                singer = new { name = confirmedSinger.Name, avatarType = confirmedSinger.AvatarType, avatarSource = confirmedSinger.AvatarSource, email = confirmedSinger.Email, vocalRange = confirmedSinger.VocalRange, customTitle = confirmedSinger.CustomTitle }
                            }));
                        }
                        else
                        {
                            bool authorized = TryAuthorizeSinger(clientIp, () => confirmedSinger.PinCode == singerPin, out bool singerLockedOut);
                            if (singerLockedOut)
                            {
                                await SendTooManyRequestsAsync(stream);
                            }
                            else if (authorized)
                            {
                                await SendJsonResponseAsync(stream, JsonSerializer.Serialize(new
                                {
                                    success = true,
                                    singer = new { name = confirmedSinger.Name, avatarType = confirmedSinger.AvatarType, avatarSource = confirmedSinger.AvatarSource, email = confirmedSinger.Email, vocalRange = confirmedSinger.VocalRange, customTitle = confirmedSinger.CustomTitle }
                                }));
                            }
                            else
                            {
                                await SendBadRequestAsync(stream, "{\"error\":\"Incorrect PIN code for this singer name.\"}");
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
                            // field would be written straight to disk under a made-up extension
                            // regardless of what it actually contains.
                            if (imgBytes.Length > MaxAvatarImageBytes || !TryDetectImageFormat(imgBytes, out string detectedExtension, out _))
                            {
                                await SendBadRequestAsync(stream, "{\"error\":\"Invalid or oversized image.\"}");
                                return;
                            }

                            string avatarsDir = Lyracist.Shared.Globals.AvatarsDir;
                            Directory.CreateDirectory(avatarsDir);

                            // Filename is derived from a GUID, never from patron-controlled input,
                            // so it cannot contain path-traversal segments. Extension matches the
                            // format actually detected above (JPEG/PNG/GIF/WEBP), not assumed.
                            string filename = $"{Guid.NewGuid():N}{detectedExtension}";
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
            await stream.FlushAsync();
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
            await stream.FlushAsync();
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
            await stream.FlushAsync();
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
            await stream.FlushAsync();
        }

        private static async Task SendNotFoundAsync(NetworkStream stream)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(responseBytes);
            await stream.FlushAsync();
        }

        private static async Task SendUnauthorizedAsync(NetworkStream stream)
        {
            const string json = "{\"error\":\"Unauthorized\"}";
            byte[] responseBytes = Encoding.UTF8.GetBytes(
                "HTTP/1.1 401 Unauthorized\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n" +
                "Connection: close\r\n\r\n" +
                json);
            await stream.WriteAsync(responseBytes);
            await stream.FlushAsync();
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
            await stream.FlushAsync();
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
        /// disk under a fixed extension - checks for the magic bytes of the image formats a phone
        /// camera or gallery picker would actually produce (JPEG, PNG, WEBP, GIF), and reports back
        /// the extension/Content-Type that actually matches the bytes instead of assuming JPEG:
        /// storing a PNG/WEBP/GIF upload as ".jpg" and always serving "image/jpeg" (as this used to)
        /// made strict clients reject or mis-render anything that wasn't actually a JPEG.
        /// </summary>
        private static bool TryDetectImageFormat(byte[] bytes, out string extension, out string contentType)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                extension = ".jpg";
                contentType = "image/jpeg";
                return true;
            }

            if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                extension = ".png";
                contentType = "image/png";
                return true;
            }

            if (bytes.Length >= 6 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == '8'
                && (bytes[4] == '7' || bytes[4] == '9') && bytes[5] == 'a')
            {
                extension = ".gif";
                contentType = "image/gif";
                return true;
            }

            if (bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
                && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P')
            {
                extension = ".webp";
                contentType = "image/webp";
                return true;
            }

            extension = "";
            contentType = "";
            return false;
        }

        /// <summary>Maps a stored avatar file's extension back to a Content-Type for serving it, so a
        /// previously-uploaded PNG/WEBP/GIF (see <see cref="TryDetectImageFormat"/>) is served with its
        /// real Content-Type instead of a hardcoded one. Defaults to JPEG for legacy files written
        /// before this mapping existed, all of which really were JPEGs.</summary>
        private static string GetContentTypeForAvatarExtension(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };
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
            await stream.FlushAsync();
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

        // The portal markup lives in Resources/PatronPortal.html, Resources/kiosk.html, Resources/dj.html, Resources/billboard.html, and Resources/handoff.html (embedded resources); loaded once on first request.
        private static readonly Lazy<string> CachedHtml = new(LoadHtmlContent);
        private static readonly Lazy<string> CachedDjHtml = new(LoadDjHtmlContent);
        private static readonly Lazy<string> CachedKioskHtml = new(LoadKioskHtmlContent);
        private static readonly Lazy<string> CachedBillboardHtml = new(LoadBillboardHtmlContent);
        private static readonly Lazy<string> CachedHandoffHtml = new(LoadHandoffHtmlContent);

        private static string GetHtmlContent() => CachedHtml.Value;
        private static string GetDjHtmlContent() => CachedDjHtml.Value;
        private static string GetKioskHtmlContent() => CachedKioskHtml.Value;
        private static string GetHandoffHtmlContent() => CachedHandoffHtml.Value;
        private string GetBillboardHtmlContent()
        {
            string html = CachedBillboardHtml.Value;
            if (_onGetVenueInfoJson != null)
            {
                try
                {
                    string infoJson = _onGetVenueInfoJson();
                    if (!string.IsNullOrWhiteSpace(infoJson))
                    {
                        var info = JsonSerializer.Deserialize<VenueInfoResponseDto>(infoJson);
                        if (info != null)
                        {
                            if (!info.listDjAndVenue)
                            {
                                html = html.Replace("<div class=\"brand-titles\">", "<div class=\"brand-titles\" style=\"display: none;\">");
                            }
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(info.venue))
                                {
                                    html = html.Replace("<span class=\"venue-name\" id=\"billboard-venue-name\">Karaoke Night</span>",
                                        $"<span class=\"venue-name\" id=\"billboard-venue-name\">{WebUtility.HtmlEncode(info.venue)}</span>");
                                }
                                if (!string.IsNullOrWhiteSpace(info.dj))
                                {
                                    html = html.Replace("<span class=\"dj-badge\" id=\"billboard-dj-name\">Hosted by DJ</span>",
                                        $"<span class=\"dj-badge\" id=\"billboard-dj-name\">Hosted by {WebUtility.HtmlEncode(info.dj)}</span>");
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            return html;
        }

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

        private static string LoadHandoffHtmlContent()
        {
            Assembly assembly = typeof(PatronRequestServer).Assembly;
            string? resourceName = Array.Find(
                assembly.GetManifestResourceNames(),
                n => n.EndsWith("handoff.html", StringComparison.OrdinalIgnoreCase));

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
                "PatronRequestServer.LoadHandoffHtmlContent",
                new InvalidOperationException("Embedded resource 'handoff.html' was not found."));
            return "<!DOCTYPE html><html><body><h1>KSRotation</h1><p>Handoff portal resource missing.</p></body></html>";
        }
    }
}
