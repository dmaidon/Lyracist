// Edited on Sep 17, 2026 @ 10:36:20 -> Add DashCast web receiver support for HTML billboard casting
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Lyracist.Shared
{
    public interface IChromecastSender
    {
        Task<bool> LaunchReceiverAsync(ChromecastDevice device);
        Task<bool> SendRotationUrlAsync(ChromecastDevice device, string rotationUrl);
        Task<bool> CastWebUrlAsync(ChromecastDevice device, string webUrl);
        Task StopCastingAsync(ChromecastDevice device);
    }

    public class ChromecastSender : IChromecastSender
    {
        private const int ChromecastPort = 8009;

        private readonly ConcurrentDictionary<string, CastSession> _sessions = new();

        public async Task<bool> LaunchReceiverAsync(ChromecastDevice device)
        {
            if (device?.Address == null) return false;

            var session = await GetOrCreateSessionAsync(device);
            if (session == null) return false;

            return await session.LaunchDefaultReceiverAsync();
        }

        public async Task<bool> SendRotationUrlAsync(ChromecastDevice device, string rotationUrl)
        {
            if (device?.Address == null) return false;
            if (!_sessions.TryGetValue(device.Address.ToString(), out var session) || !session.IsConnected)
            {
                return false;
            }

            return await session.LoadMediaAsync(rotationUrl);
        }

        public async Task<bool> CastWebUrlAsync(ChromecastDevice device, string webUrl)
        {
            if (device?.Address == null) return false;

            var session = await GetOrCreateSessionAsync(device);
            if (session == null) return false;

            var launched = await session.LaunchWebReceiverAsync();
            if (!launched) return false;

            return await session.LoadWebUrlAsync(webUrl);
        }

        public async Task StopCastingAsync(ChromecastDevice device)
        {
            if (device?.Address == null) return;

            if (_sessions.TryRemove(device.Address.ToString(), out var session))
            {
                await session.StopAndCloseAsync();
            }
        }

        private async Task<CastSession?> GetOrCreateSessionAsync(ChromecastDevice device)
        {
            var key = device.Address!.ToString();

            if (_sessions.TryGetValue(key, out var existing) && existing.IsConnected)
            {
                return existing;
            }

            var session = new CastSession(device.Address, ChromecastPort);
            var connected = await session.ConnectAsync();
            if (!connected)
            {
                session.Dispose();
                return null;
            }

            _sessions[key] = session;
            return session;
        }
    }

    /// <summary>
    /// Holds one persistent TLS connection to a Cast device and drives the CONNECT/heartbeat/
    /// LAUNCH/LOAD handshake over it. One instance per device.
    /// </summary>
    internal sealed partial class CastSession : IDisposable
    {
        private const string SenderId = "sender-0";
        private const string PlatformDestinationId = "receiver-0";
        private const string NsConnection = "urn:x-cast:com.google.cast.tp.connection";
        private const string NsHeartbeat = "urn:x-cast:com.google.cast.tp.heartbeat";
        private const string NsReceiver = "urn:x-cast:com.google.cast.receiver";
        private const string NsMedia = "urn:x-cast:com.google.cast.media";
        private const string DefaultMediaReceiverAppId = "CC1AD845";
        private const string DashCastAppId = "84912283";
        private const string NsDashCast = "urn:x-cast:com.madmod.dashcast";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

        private readonly System.Net.IPAddress _address;
        private readonly int _port;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();

        private TcpClient? _tcpClient;
        private SslStream? _sslStream;
        private CancellationTokenSource? _cts;
        private Task? _receiveLoopTask;
        private System.Threading.Timer? _heartbeatTimer;
        private int _requestId;
        private string? _transportId;
        private string? _sessionId;
        private string _expectedAppId = DefaultMediaReceiverAppId;

        public bool IsConnected => _sslStream != null && _tcpClient?.Connected == true;

        public CastSession(System.Net.IPAddress address, int port)
        {
            _address = address;
            _port = port;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_address, _port);

                // Cast devices present a self-signed certificate; there is no CA to validate
                // against on a LAN, and this is how every Cast sender (Google's included) handles it.
                _sslStream = new SslStream(_tcpClient.GetStream(), false, (_, _, _, _) => true);
                await _sslStream.AuthenticateAsClientAsync(_address.ToString());

                _cts = new CancellationTokenSource();
                _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));

                await SendAsync(SenderId, PlatformDestinationId, NsConnection, "{\"type\":\"CONNECT\"}");
                StartHeartbeat();

                return true;
            }
            catch (Exception ex)
            {
                Globals.LogError("Shared", $"ChromecastSender.ConnectAsync({_address})", ex);
                Dispose();
                return false;
            }
        }

        public async Task<bool> LaunchDefaultReceiverAsync()
        {
            if (!IsConnected) return false;

            _expectedAppId = DefaultMediaReceiverAppId;
            var requestId = NextRequestId();
            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[requestId] = tcs;

            try
            {
                var launchMsg = $"{{\"type\":\"LAUNCH\",\"appId\":\"{DefaultMediaReceiverAppId}\",\"requestId\":{requestId}}}";
                await SendAsync(SenderId, PlatformDestinationId, NsReceiver, launchMsg);

                using var timeoutCts = new CancellationTokenSource(RequestTimeout);
                await using (timeoutCts.Token.Register(() => tcs.TrySetCanceled()))
                {
                    var status = await tcs.Task;
                    var ok = ApplyReceiverStatus(status);
                    Globals.LogInfo("Shared", $"ChromecastSender.LaunchDefaultReceiverAsync: status applied, ok={ok}, transportId={_transportId ?? "(none)"}");
                    return ok;
                }
            }
            catch (Exception ex)
            {
                Globals.LogError("Shared", "ChromecastSender.LaunchDefaultReceiverAsync", ex);
                return false;
            }
            finally
            {
                _pendingRequests.TryRemove(requestId, out _);
            }
        }

        public async Task<bool> LaunchWebReceiverAsync()
        {
            if (!IsConnected) return false;

            _expectedAppId = DashCastAppId;
            var requestId = NextRequestId();
            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[requestId] = tcs;

            try
            {
                var launchMsg = $"{{\"type\":\"LAUNCH\",\"appId\":\"{DashCastAppId}\",\"requestId\":{requestId}}}";
                await SendAsync(SenderId, PlatformDestinationId, NsReceiver, launchMsg);

                using var timeoutCts = new CancellationTokenSource(RequestTimeout);
                await using (timeoutCts.Token.Register(() => tcs.TrySetCanceled()))
                {
                    var status = await tcs.Task;
                    var ok = ApplyReceiverStatus(status);
                    Globals.LogInfo("Shared", $"ChromecastSender.LaunchWebReceiverAsync: status applied, ok={ok}, transportId={_transportId ?? "(none)"}");
                    return ok;
                }
            }
            catch (Exception ex)
            {
                Globals.LogError("Shared", "ChromecastSender.LaunchWebReceiverAsync", ex);
                return false;
            }
            finally
            {
                _pendingRequests.TryRemove(requestId, out _);
            }
        }

        public async Task<bool> LoadWebUrlAsync(string webUrl)
        {
            if (!IsConnected || string.IsNullOrEmpty(_transportId))
            {
                Globals.LogInfo("Shared", $"ChromecastSender.LoadWebUrlAsync: not connected or no transportId (connected={IsConnected}, transportId={_transportId ?? "(none)"})");
                return false;
            }

            var payload = $"{{\"url\":\"{JsonEscape(webUrl)}\",\"force\":true,\"reload\":false}}";
            await SendAsync(SenderId, _transportId!, NsDashCast, payload);
            return true;
        }

        public async Task<bool> LoadMediaAsync(string rotationUrl)
        {
            if (!IsConnected || string.IsNullOrEmpty(_transportId))
            {
                Globals.LogInfo("Shared", $"ChromecastSender.LoadMediaAsync: not connected or no transportId (connected={IsConnected}, transportId={_transportId ?? "(none)"})");
                return false;
            }

            // rotationUrl points at a multipart/x-mixed-replace stream (see BrowserCastServer) that
            // pushes fresh frames continuously over one persistent connection - the receiver keeps
            // rendering new frames as they arrive, so a single LOAD is enough. Re-issuing LOAD
            // periodically (the previous approach, needed because a plain static image never
            // updates on its own) caused a full media transition on the TV every refresh, which
            // showed up as visible flicker, and was also far too infrequent to convey fast
            // animations like the marquee bulb chase.
            return await SendLoadAsync(rotationUrl);
        }

        public async Task StopAndCloseAsync()
        {
            try
            {
                if (IsConnected && !string.IsNullOrEmpty(_sessionId))
                {
                    var requestId = NextRequestId();
                    var stopMsg = $"{{\"type\":\"STOP\",\"requestId\":{requestId},\"sessionId\":\"{_sessionId}\"}}";
                    await SendAsync(SenderId, PlatformDestinationId, NsReceiver, stopMsg);
                    await Task.Delay(200);
                }
            }
            catch
            {
                // ignore - we're tearing the connection down regardless
            }
            finally
            {
                Dispose();
            }
        }

        private bool ApplyReceiverStatus(JsonElement responseRoot)
        {
            if (!responseRoot.TryGetProperty("status", out var statusObj))
            {
                return false;
            }

            if (statusObj.TryGetProperty("sessionId", out var sid) && sid.ValueKind == JsonValueKind.String)
            {
                _sessionId = sid.GetString();
            }

            if (statusObj.TryGetProperty("applications", out var apps) && apps.ValueKind == JsonValueKind.Array)
            {
                foreach (var app in apps.EnumerateArray())
                {
                    if (app.TryGetProperty("appId", out var appIdProp) &&
                        string.Equals(appIdProp.GetString(), _expectedAppId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (app.TryGetProperty("transportId", out var tid))
                        {
                            _transportId = tid.GetString();
                        }
                        if (app.TryGetProperty("sessionId", out var asid))
                        {
                            _sessionId = asid.GetString();
                        }
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(_transportId))
            {
                return false;
            }

            // Open a virtual connection into the receiver app's own message bus - required
            // before it will accept namespaced messages like MEDIA/LOAD.
            _ = SendAsync(SenderId, _transportId!, NsConnection, "{\"type\":\"CONNECT\"}");
            return true;
        }

        private async Task<bool> SendLoadAsync(string rotationUrl)
        {
            if (string.IsNullOrEmpty(_transportId)) return false;

            var requestId = NextRequestId();

            // contentType matches the multipart/x-mixed-replace stream BrowserCastServer serves -
            // the receiver keeps rendering frames as they arrive over this one connection, so no
            // cache-busting or repeat LOAD is needed the way a static image would require.
            var loadMsg =
                "{\"type\":\"LOAD\",\"requestId\":" + requestId +
                ",\"sessionId\":\"" + _sessionId + "\"" +
                ",\"autoplay\":true" +
                ",\"media\":{\"contentId\":\"" + JsonEscape(rotationUrl) + "\"" +
                ",\"contentType\":\"multipart/x-mixed-replace\",\"streamType\":\"LIVE\"}}";

            await SendAsync(SenderId, _transportId!, NsMedia, loadMsg);
            return true;
        }

        private static string JsonEscape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private void StartHeartbeat()
        {
            _heartbeatTimer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    await SendAsync(SenderId, PlatformDestinationId, NsHeartbeat, "{\"type\":\"PING\"}");
                }
                catch
                {
                    // connection likely dead; the receive loop will observe it and tear down
                }
            }, null, HeartbeatInterval, HeartbeatInterval);
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _sslStream != null)
                {
                    var payload = await CastMessageCodec.ReadFrameAsync(_sslStream, ct);
                    if (payload == null) break;

                    if (!CastMessageCodec.TryDecode(payload, out _, out _, out var ns, out var payloadUtf8))
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(payloadUtf8)) continue;

                    if (ns == NsHeartbeat)
                    {
                        using var doc = JsonDocument.Parse(payloadUtf8);
                        if (doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == "PING")
                        {
                            await SendAsync(SenderId, PlatformDestinationId, NsHeartbeat, "{\"type\":\"PONG\"}");
                        }
                    }
                    else if (ns == NsReceiver)
                    {
                        using var doc = JsonDocument.Parse(payloadUtf8);
                        var root = doc.RootElement.Clone();
                        if (root.TryGetProperty("requestId", out var ridProp) && ridProp.TryGetInt32(out var rid))
                        {
                            if (_pendingRequests.TryRemove(rid, out var tcs))
                            {
                                tcs.TrySetResult(root);
                            }
                        }
                    }
                }
            }
            catch
            {
                // normal on disconnect/cancellation
            }
        }

        private async Task SendAsync(string source, string destination, string ns, string jsonPayload)
        {
            if (_sslStream == null) return;

            var frame = CastMessageCodec.Encode(source, destination, ns, jsonPayload);

            await _writeLock.WaitAsync();
            try
            {
                await CastMessageCodec.WriteFrameAsync(_sslStream, frame);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private int NextRequestId() => Interlocked.Increment(ref _requestId);

        public void Dispose()
        {
            try { _heartbeatTimer?.Dispose(); } catch { }
            try { _cts?.Cancel(); } catch { }
            try { _sslStream?.Dispose(); } catch { }
            try { _tcpClient?.Dispose(); } catch { }
            _cts?.Dispose();

            foreach (var kvp in _pendingRequests)
            {
                kvp.Value.TrySetCanceled();
            }
            _pendingRequests.Clear();
        }
    }

    /// <summary>
    /// Minimal hand-written codec for the CASTV2 wire format: a 4-byte big-endian length prefix
    /// followed by a protobuf-encoded CastMessage (extensions.api.cast_channel.CastMessage) with
    /// only the fields we use: protocol_version(1), source_id(2), destination_id(3), namespace(4),
    /// payload_type(5), payload_utf8(6). No external protobuf dependency needed for this subset.
    /// </summary>
    internal static class CastMessageCodec
    {
        public static byte[] Encode(string sourceId, string destinationId, string ns, string payloadUtf8)
        {
            using var body = new MemoryStream();
            WriteVarintField(body, 1, 0); // protocol_version = CASTV2_1_0
            WriteStringField(body, 2, sourceId);
            WriteStringField(body, 3, destinationId);
            WriteStringField(body, 4, ns);
            WriteVarintField(body, 5, 0); // payload_type = STRING
            WriteStringField(body, 6, payloadUtf8);

            var payload = body.ToArray();
            var frame = new byte[4 + payload.Length];
            frame[0] = (byte)((payload.Length >> 24) & 0xFF);
            frame[1] = (byte)((payload.Length >> 16) & 0xFF);
            frame[2] = (byte)((payload.Length >> 8) & 0xFF);
            frame[3] = (byte)(payload.Length & 0xFF);
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
            return frame;
        }

        public static async Task WriteFrameAsync(Stream stream, byte[] frameWithHeader)
        {
            await stream.WriteAsync(frameWithHeader);
            await stream.FlushAsync();
        }

        public static async Task<byte[]?> ReadFrameAsync(Stream stream, CancellationToken ct)
        {
            var lenBuf = await ReadExactAsync(stream, 4, ct);
            if (lenBuf == null) return null;

            int len = (lenBuf[0] << 24) | (lenBuf[1] << 16) | (lenBuf[2] << 8) | lenBuf[3];
            if (len <= 0 || len > 10 * 1024 * 1024) return null;

            return await ReadExactAsync(stream, len, ct);
        }

        public static bool TryDecode(byte[] data, out string sourceId, out string destinationId, out string ns, out string payloadUtf8)
        {
            sourceId = string.Empty;
            destinationId = string.Empty;
            ns = string.Empty;
            payloadUtf8 = string.Empty;

            try
            {
                int pos = 0;
                while (pos < data.Length)
                {
                    var tag = ReadVarint(data, ref pos);
                    int fieldNumber = (int)(tag >> 3);
                    int wireType = (int)(tag & 0x7);

                    if (wireType == 0)
                    {
                        ReadVarint(data, ref pos); // varint field value, unused (protocol_version/payload_type)
                    }
                    else if (wireType == 2)
                    {
                        var len = (int)ReadVarint(data, ref pos);
                        if (len < 0 || pos + len > data.Length) return false;

                        switch (fieldNumber)
                        {
                            case 2: sourceId = Encoding.UTF8.GetString(data, pos, len); break;
                            case 3: destinationId = Encoding.UTF8.GetString(data, pos, len); break;
                            case 4: ns = Encoding.UTF8.GetString(data, pos, len); break;
                            case 6: payloadUtf8 = Encoding.UTF8.GetString(data, pos, len); break;
                        }
                        pos += len;
                    }
                    else
                    {
                        return false; // unsupported wire type; not produced by CastMessage
                    }
                }

                return !string.IsNullOrEmpty(ns);
            }
            catch
            {
                return false;
            }
        }

        private static async Task<byte[]?> ReadExactAsync(Stream stream, int count, CancellationToken ct)
        {
            var buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
                if (read <= 0) return null;
                offset += read;
            }
            return buffer;
        }

        private static void WriteVarintField(Stream s, int fieldNumber, ulong value)
        {
            WriteVarint(s, (ulong)((fieldNumber << 3) | 0));
            WriteVarint(s, value);
        }

        private static void WriteStringField(Stream s, int fieldNumber, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteVarint(s, (ulong)((fieldNumber << 3) | 2));
            WriteVarint(s, (ulong)bytes.Length);
            s.Write(bytes, 0, bytes.Length);
        }

        private static void WriteVarint(Stream s, ulong value)
        {
            while (value >= 0x80)
            {
                s.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            s.WriteByte((byte)value);
        }

        private static ulong ReadVarint(byte[] data, ref int pos)
        {
            ulong result = 0;
            int shift = 0;
            while (true)
            {
                byte b = data[pos++];
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            return result;
        }
    }
}
