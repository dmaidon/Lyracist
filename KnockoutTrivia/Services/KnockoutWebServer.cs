// Edited on Oct 1, 2026 @ 07:48:00 -> Reduce body cap to 8KB, optimize rate limiter (60/5m with pruning), cache HTML bytes, static JsonOptions, and add QuestionId validation
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using KnockoutTrivia.Models;
using Lyracist.Shared;

namespace KnockoutTrivia.Services;

public class KnockoutWebServer : IKnockoutWebServer
{
    private const int MaxRequestBodyBytes = 8_192; // 8 KB
    private const int MaxConcurrentConnections = 64;
    private static readonly TimeSpan RequestReadTimeout = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Caps how many brand-new players a single IP can register (via /api/knockout/join with a
    // name that doesn't match an existing connected/reconnectable player - see RouteRequestAsync).
    // Raised to 60 per 5 minutes to accommodate large groups sharing venue Wi-Fi NAT IPs
    // while still preventing malicious unbounded bot floods.
    private const int MaxRegistrationsPerWindow = 60;
    private static readonly TimeSpan RegistrationWindow = TimeSpan.FromMinutes(5);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RegistrationRateState> _registrationsByIp = new();
    private DateTime _lastPruneUtc = DateTime.UtcNow;

    private sealed class RegistrationRateState
    {
        public int Count;
        public DateTime WindowStartUtc;
    }

    private void PruneExpiredRegistrations(DateTime now)
    {
        if (now - _lastPruneUtc < TimeSpan.FromMinutes(2)) return;
        _lastPruneUtc = now;

        foreach (var kvp in _registrationsByIp)
        {
            if (now - kvp.Value.WindowStartUtc > RegistrationWindow * 2)
            {
                _registrationsByIp.TryRemove(kvp.Key, out _);
            }
        }
    }

    private bool TryAllowRegistration(string clientIp)
    {
        var now = DateTime.UtcNow;
        PruneExpiredRegistrations(now);

        var state = _registrationsByIp.GetOrAdd(clientIp, _ => new RegistrationRateState { WindowStartUtc = now });

        lock (state)
        {
            if (now - state.WindowStartUtc > RegistrationWindow)
            {
                state.WindowStartUtc = now;
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

    private readonly IGameStateService _gameStateService;
    private readonly SemaphoreSlim _connectionLimiter = new(MaxConcurrentConnections, MaxConcurrentConnections);

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private byte[]? _cachedHtmlBytes;
    private bool _disposed;

    public bool IsRunning { get; private set; }
    public int Port { get; private set; } = 8088;

    public string ConnectUrl
    {
        get
        {
            string ip = LocalNetworkHelper.GetLocalIPv4();
            return $"http://{ip}:{Port}";
        }
    }

    public KnockoutWebServer(IGameStateService gameStateService, int defaultPort = 8088)
    {
        _gameStateService = gameStateService;
        Port = defaultPort;
    }

    public bool Start(int? port = null)
    {
        if (IsRunning)
        {
            if (port.HasValue && port.Value != Port)
            {
                Stop();
            }
            else
            {
                return true;
            }
        }

        if (port.HasValue && port.Value > 0)
        {
            Port = port.Value;
        }

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            IsRunning = true;

            Task.Run(() => AcceptConnectionsAsync(_cts.Token));
            return true;
        }
        catch (Exception ex)
        {
            Globals.LogError("KnockoutTrivia", $"KnockoutWebServer.Start (port {Port})", ex);
            IsRunning = false;
            return false;
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;

        try
        {
            _cts?.Cancel();
            _listener?.Stop();
        }
        catch
        {
            // Ignore stop errors
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsRunning = false;
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(token);

                if (!_connectionLimiter.Wait(0, CancellationToken.None))
                {
                    client.Dispose();
                    continue;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleClientAsync(client, token);
                    }
                    finally
                    {
                        _connectionLimiter.Release();
                    }
                }, token);
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Globals.LogError("KnockoutTrivia", "KnockoutWebServer.AcceptConnectionsAsync", ex);
                try { await Task.Delay(250, token); } catch { break; }
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken serverToken)
    {
        string clientIp = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "unknown";

        using (client)
        await using (var stream = client.GetStream())
        using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(serverToken))
        {
            timeoutCts.CancelAfter(RequestReadTimeout);
            var readStream = new ChunkedReader(stream);

            try
            {
                string headers = await ReadHeadersAsync(readStream, timeoutCts.Token);
                if (string.IsNullOrWhiteSpace(headers)) return;

                var headerLines = headers.Split("\r\n", StringSplitOptions.None);
                string requestLine = headerLines[0];
                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;

                string method = parts[0].ToUpperInvariant();
                string rawPath = parts[1];
                string path = rawPath;
                string queryString = "";

                int qIdx = rawPath.IndexOf('?');
                if (qIdx >= 0)
                {
                    path = rawPath[..qIdx];
                    queryString = rawPath[(qIdx + 1)..];
                }

                if (method == "OPTIONS")
                {
                    await SendCorsPreflightResponseAsync(stream);
                    return;
                }

                int contentLength = ParseContentLength(headerLines);
                if (contentLength > MaxRequestBodyBytes)
                {
                    await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Request body too large.\"}"));
                    return;
                }

                string body = "";
                if (contentLength > 0)
                {
                    byte[] bodyBuffer = new byte[contentLength];
                    int readTotal = 0;
                    while (readTotal < contentLength)
                    {
                        int read = await readStream.ReadAsync(bodyBuffer.AsMemory(readTotal, contentLength - readTotal), timeoutCts.Token);
                        if (read == 0) break;
                        readTotal += read;
                    }
                    body = Encoding.UTF8.GetString(bodyBuffer, 0, readTotal);
                }

                await RouteRequestAsync(stream, method, path, queryString, body, clientIp);
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is IOException || ex is SocketException)
            {
                // Client disconnected or timed out
            }
            catch (Exception ex)
            {
                Globals.LogError("KnockoutTrivia", "KnockoutWebServer.HandleClientAsync", ex);
                try
                {
                    await SendResponseAsync(stream, 500, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Internal Server Error\"}"));
                }
                catch { }
            }
        }
    }

    private async Task RouteRequestAsync(NetworkStream stream, string method, string path, string queryString, string body, string clientIp)
    {
        if (path == "/" || path.Equals("/knockout", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            byte[] htmlBytes = await GetHtmlBytesAsync();
            await SendResponseAsync(stream, 200, "text/html; charset=utf-8", htmlBytes);
            return;
        }

        if (path.Equals("/api/knockout/join", StringComparison.OrdinalIgnoreCase) && method == "POST")
        {
            try
            {
                var joinReq = JsonSerializer.Deserialize<JoinRequest>(body, JsonOptions);
                if (joinReq != null && !string.IsNullOrWhiteSpace(joinReq.Name))
                {
                    // Only rate-limit genuinely new registrations - a returning player (known
                    // playerId, or a disconnected player rejoining by name) is never blocked,
                    // mirroring exactly the lookup RegisterOrGetPlayer itself uses below.
                    string trimmedName = joinReq.Name.Trim();
                    var snapshot = _gameStateService.GetPlayersSnapshot();
                    bool isReconnect = (!string.IsNullOrEmpty(joinReq.PlayerId) && snapshot.Any(p => string.Equals(p.Id, joinReq.PlayerId, StringComparison.OrdinalIgnoreCase)))
                        || snapshot.Any(p => string.Equals(p.Name, trimmedName, StringComparison.OrdinalIgnoreCase) && !p.IsConnected);

                    if (!isReconnect && !TryAllowRegistration(clientIp))
                    {
                        await SendResponseAsync(stream, 429, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Too many join attempts. Please try again later.\"}"));
                        return;
                    }

                    var player = _gameStateService.RegisterOrGetPlayer(joinReq.Name, joinReq.PlayerId);
                    var resp = new
                    {
                        success = true,
                        playerId = player.Id,
                        sessionToken = player.SessionToken,
                        name = player.Name,
                        tokens = player.Tokens,
                        strikes = player.StrikeCount,
                        score = player.Score
                    };
                    await SendResponseAsync(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resp, JsonOptions)));
                }
                else
                {
                    await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Name is required.\"}"));
                }
            }
            catch
            {
                await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Invalid JSON format.\"}"));
            }
            return;
        }

        if (path.Equals("/api/knockout/submit", StringComparison.OrdinalIgnoreCase) && method == "POST")
        {
            try
            {
                var subReq = JsonSerializer.Deserialize<SubmitRequest>(body, JsonOptions);
                if (subReq != null && !string.IsNullOrWhiteSpace(subReq.PlayerId))
                {
                    var currentQ = _gameStateService.CurrentQuestion;
                    if (!string.IsNullOrEmpty(subReq.QuestionId) && currentQ != null &&
                        !string.Equals(currentQ.Id, subReq.QuestionId, StringComparison.OrdinalIgnoreCase))
                    {
                        await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Question has ended.\",\"staleQuestion\":true,\"success\":false}"));
                        return;
                    }

                    bool accepted = _gameStateService.SubmitPlayerAnswer(subReq.PlayerId, subReq.SelectedOptionIndex, (int)subReq.ResponseTimeMs, subReq.SessionToken);
                    var resp = new { success = accepted };
                    await SendResponseAsync(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resp, JsonOptions)));
                }
                else
                {
                    await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Invalid submission payload.\"}"));
                }
            }
            catch
            {
                await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Invalid JSON format.\"}"));
            }
            return;
        }

        if (path.Equals("/api/knockout/state", StringComparison.OrdinalIgnoreCase) && method == "GET")
        {
            string playerId = "";
            string sessionToken = "";
            if (!string.IsNullOrEmpty(queryString))
            {
                var qParams = HttpUtility.ParseQueryString(queryString);
                playerId = qParams["playerId"] ?? "";
                sessionToken = qParams["sessionToken"] ?? "";
            }

            var allPlayers = _gameStateService.GetPlayersSnapshot();
            var player = !string.IsNullOrEmpty(playerId)
                ? allPlayers.FirstOrDefault(p => string.Equals(p.Id, playerId, StringComparison.OrdinalIgnoreCase))
                : null;

            if (player != null)
            {
                // Verify session token if provided
                if (string.IsNullOrEmpty(sessionToken) || string.Equals(player.SessionToken, sessionToken, StringComparison.Ordinal))
                {
                    player.IsConnected = true;
                    player.LastSeenAt = DateTime.Now;
                }
                else
                {
                    player = null;
                }
            }

            var q = _gameStateService.CurrentQuestion;
            bool isCorrect = player != null && q != null && player.HasAnsweredCurrentQuestion && player.LastAnswerIndex == q.CorrectAnswerIndex;

            var statePayload = new
            {
                phase = _gameStateService.Phase.ToString(),
                isGameActive = _gameStateService.IsGameActive,
                isAnswerRevealed = _gameStateService.IsAnswerRevealed,
                secondsRemaining = _gameStateService.SecondsRemaining,
                totalSeconds = _gameStateService.TotalCountdownSeconds,
                questionIndex = _gameStateService.CurrentQuestionIndex,
                questionNumber = _gameStateService.CurrentQuestionIndex + 1,
                totalQuestions = _gameStateService.TotalQuestions,
                venueName = _gameStateService.Settings.VenueName,
                hostName = _gameStateService.Settings.HostName,
                question = q != null ? new
                {
                    id = q.Id,
                    category = q.Category,
                    prompt = q.Prompt,
                    options = q.Options,
                    correctAnswerIndex = _gameStateService.IsAnswerRevealed ? q.CorrectAnswerIndex : -1,
                    explanation = _gameStateService.IsAnswerRevealed ? q.Explanation : ""
                } : null,
                player = player != null ? new
                {
                    id = player.Id,
                    name = player.Name,
                    score = player.Score,
                    strikeCount = player.StrikeCount,
                    tokens = player.Tokens,
                    streakCount = player.StreakCount,
                    isEliminated = player.IsEliminated,
                    statusText = player.StatusText,
                    hasAnswered = player.HasAnsweredCurrentQuestion,
                    selectedOptionIndex = player.LastAnswerIndex,
                    lastPointsEarned = player.LastPointsEarned,
                    wasShieldProtected = player.WasShieldProtected,
                    isCorrect = isCorrect
                } : null,
                leaderboard = allPlayers
                    .OrderByDescending(p => !p.IsEliminated)
                    .ThenByDescending(p => p.Score)
                    .Take(15)
                    .Select((p, idx) => new
                    {
                        rank = idx + 1,
                        name = p.Name,
                        score = p.Score,
                        strikes = p.StrikeCount,
                        tokens = p.Tokens,
                        streak = p.StreakCount,
                        isEliminated = p.IsEliminated,
                        hasAnswered = p.HasAnsweredCurrentQuestion
                    })
            };

            await SendResponseAsync(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(statePayload, JsonOptions)));
            return;
        }

        // 404 Not Found
        await SendResponseAsync(stream, 404, "text/plain", Encoding.UTF8.GetBytes("404 Not Found"));
    }

    private async Task<byte[]> GetHtmlBytesAsync()
    {
        if (_cachedHtmlBytes == null)
        {
            string html;
            var asm = Assembly.GetExecutingAssembly();
            await using var stream = asm.GetManifestResourceStream("KnockoutTrivia.Resources.knockout.html");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                html = await reader.ReadToEndAsync();
            }
            else
            {
                string diskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "knockout.html");
                if (File.Exists(diskPath))
                {
                    html = await File.ReadAllTextAsync(diskPath);
                }
                else
                {
                    html = "<html><body><h1>Knockout Trivia</h1><p>knockout.html resource not found.</p></body></html>";
                }
            }
            _cachedHtmlBytes = Encoding.UTF8.GetBytes(html);
        }
        return _cachedHtmlBytes;
    }

    private static async Task SendCorsPreflightResponseAsync(NetworkStream stream)
    {
        const string response = "HTTP/1.1 204 No Content\r\n" +
                                "Access-Control-Allow-Origin: *\r\n" +
                                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                                "Access-Control-Allow-Headers: Content-Type\r\n" +
                                "Access-Control-Max-Age: 86400\r\n" +
                                "Connection: close\r\n" +
                                "Content-Length: 0\r\n\r\n";
        byte[] bytes = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(bytes);
    }

    private static async Task SendResponseAsync(NetworkStream stream, int statusCode, string contentType, byte[] content)
    {
        string statusText = statusCode switch
        {
            200 => "OK",
            204 => "No Content",
            400 => "Bad Request",
            404 => "Not Found",
            429 => "Too Many Requests",
            500 => "Internal Server Error",
            _ => "OK"
        };

        string header = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                        $"Content-Type: {contentType}\r\n" +
                        $"Content-Length: {content.Length}\r\n" +
                        "Access-Control-Allow-Origin: *\r\n" +
                        "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                        "Access-Control-Allow-Headers: Content-Type\r\n" +
                        "Connection: close\r\n\r\n";

        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
        await stream.WriteAsync(headerBytes);
        if (content.Length > 0)
        {
            await stream.WriteAsync(content);
        }
    }

    private static async Task<string> ReadHeadersAsync(ChunkedReader stream, CancellationToken token)
    {
        await using var ms = new MemoryStream();
        int pattern = 0; // Tracks \r\n\r\n

        while (ms.Length < 8192)
        {
            int b = await stream.ReadByteAsync(token);
            if (b < 0) break;

            ms.WriteByte((byte)b);

            if (b == '\r' && (pattern == 0 || pattern == 2)) pattern++;
            else if (b == '\n' && (pattern == 1 || pattern == 3)) pattern++;
            else if (b == '\r') pattern = 1;
            else pattern = 0;

            if (pattern == 4) break;
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private sealed class ChunkedReader(Stream inner)
    {
        private readonly byte[] _buffer = new byte[4096];
        private int _pos;
        private int _len;

        public async Task<int> ReadByteAsync(CancellationToken token)
        {
            if (_pos >= _len)
            {
                _len = await inner.ReadAsync(_buffer.AsMemory(0, _buffer.Length), token);
                _pos = 0;
                if (_len == 0) return -1;
            }
            return _buffer[_pos++];
        }

        public async Task<int> ReadAsync(Memory<byte> destination, CancellationToken token)
        {
            if (_pos < _len)
            {
                int available = _len - _pos;
                int toCopy = Math.Min(available, destination.Length);
                _buffer.AsSpan(_pos, toCopy).CopyTo(destination.Span);
                _pos += toCopy;
                return toCopy;
            }
            return await inner.ReadAsync(destination, token);
        }
    }

    private static int ParseContentLength(string[] headerLines)
    {
        foreach (var line in headerLines)
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                string val = line["Content-Length:".Length..].Trim();
                if (int.TryParse(val, out int len)) return len;
            }
        }
        return 0;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _cts?.Dispose();
            _connectionLimiter.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private record JoinRequest(string Name, string? PlayerId);
    private record SubmitRequest(string PlayerId, int SelectedOptionIndex, double ResponseTimeMs, string? SessionToken = null, string? QuestionId = null);
}
