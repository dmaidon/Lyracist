// Edited on Aug 25, 2026 @ 06:15:00 -> Fix RCS1085, CA2016, RCS1261 async disposals, RCS1146, and RCS1118 const in TriviaWebServer.cs
using System;
using System.Collections.Generic;
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
using Lyracist.Trivia.Core.Models;

namespace Lyracist.Trivia.Core.Services;

public class TriviaWebServer : IDisposable
{
    private const int MaxRequestBodyBytes = 2_097_152; // 2 MB
    private const int MaxConcurrentConnections = 64;
    private static readonly TimeSpan RequestReadTimeout = TimeSpan.FromSeconds(15);

    // Caps how many brand-new players a single IP can register (via /api/trivia/join with a name
    // TriviaGameEngine.RegisterPlayer has never seen - see RouteRequestAsync). Without this, an
    // unauthenticated script on the venue WiFi can insert an unbounded number of entries into the
    // live leaderboard in a few minutes. A legitimate phone joins once per session, so 5 per 10
    // minutes comfortably covers a handful of real players sharing an IP while still blocking a
    // scripted flood.
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

    private readonly TriviaGameEngine _engine;
    private readonly SemaphoreSlim _connectionLimiter = new(MaxConcurrentConnections, MaxConcurrentConnections);

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private string? _cachedHtml;
    private bool _disposed;

    public bool IsRunning { get; private set; }
    public int Port { get; }

    public TriviaWebServer(TriviaGameEngine engine, int port = 8085)
    {
        _engine = engine;
        Port = port;
    }

    public void Start()
    {
        if (IsRunning) return;

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            IsRunning = true;

            Task.Run(() => AcceptConnectionsAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            // No per-app logger available here (this library is shared across several host apps
            // with different names) - Trace.TraceError still isn't Debug-only, unlike Debug.WriteLine.
            System.Diagnostics.Trace.TraceError($"TriviaWebServer Start failed on port {Port}: {ex}");
            IsRunning = false;
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
                break; // Server stopping intentionally (Stop() was called)
            }
            catch (Exception ex)
            {
                // A single failed accept (e.g. transient SocketException from a flaky client)
                // must not take down the whole listener - log and keep accepting. A short
                // delay avoids a tight retry loop if the underlying socket is persistently faulted.
                System.Diagnostics.Trace.TraceError($"TriviaWebServer AcceptConnectionsAsync error: {ex}");
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
                System.Diagnostics.Trace.TraceError($"TriviaWebServer HandleClientAsync error: {ex}");
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
        if (path == "/" || path.Equals("/trivia", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            byte[] htmlBytes = await GetHtmlBytesAsync();
            await SendResponseAsync(stream, 200, "text/html; charset=utf-8", htmlBytes);
            return;
        }

        if (path.Equals("/api/trivia/join", StringComparison.OrdinalIgnoreCase) && method == "POST")
        {
            try
            {
                var joinReq = JsonSerializer.Deserialize<JoinRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (joinReq != null && !string.IsNullOrWhiteSpace(joinReq.Name))
                {
                    string trimmedName = TriviaGameEngine.NormalizePlayerName(joinReq.Name);

                    // If someone is already actively connected under this name, only let the join
                    // through if the request carries that same player's PlayerId (a legitimate
                    // reconnect - e.g. a page reload on the same device). Otherwise this is a
                    // second, different device picking the same display name, which used to
                    // silently merge into (and start acting as) the first device's player.
                    var existing = _engine.GetPlayers().FirstOrDefault(p => p.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
                    bool isSamePlayer = existing != null && !string.IsNullOrEmpty(joinReq.PlayerId) &&
                        string.Equals(existing.PlayerId, joinReq.PlayerId, StringComparison.OrdinalIgnoreCase);

                    if (existing?.IsConnected == true && !isSamePlayer)
                    {
                        await SendResponseAsync(stream, 409, "application/json; charset=utf-8",
                            Encoding.UTF8.GetBytes("{\"error\":\"That name is already in use this game. Please choose a different name.\"}"));
                        return;
                    }

                    // RegisterPlayer only creates a new entry when no player under this name has
                    // ever registered (existing == null) - anything else just re-marks an already-
                    // known player connected, so only the genuinely-new case needs rate limiting.
                    if (existing == null && !TryAllowRegistration(clientIp))
                    {
                        await SendResponseAsync(stream, 429, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Too many join attempts. Please try again later.\"}"));
                        return;
                    }

                    var player = _engine.RegisterPlayer(trimmedName, joinReq.TeamName?.Trim() ?? "");
                    var resp = new { success = true, playerId = player.PlayerId, name = player.Name };
                    await SendResponseAsync(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resp)));
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

        if (path.Equals("/api/trivia/submit", StringComparison.OrdinalIgnoreCase) && method == "POST")
        {
            try
            {
                var subReq = JsonSerializer.Deserialize<SubmitRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (subReq != null && !string.IsNullOrWhiteSpace(subReq.PlayerName))
                {
                    string trimmedName = TriviaGameEngine.NormalizePlayerName(subReq.PlayerName);

                    // If this name's PlayerId no longer matches who the caller thinks they are,
                    // another device has since taken over that name (or the caller's own session
                    // is stale) - don't let it submit an answer as somebody else.
                    var existing = _engine.GetPlayers().FirstOrDefault(p => p.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
                    if (existing != null && !string.IsNullOrEmpty(subReq.PlayerId) &&
                        !string.Equals(existing.PlayerId, subReq.PlayerId, StringComparison.OrdinalIgnoreCase))
                    {
                        await SendResponseAsync(stream, 409, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Player identity mismatch - please rejoin.\"}"));
                        return;
                    }

                    int optionCount = _engine.CurrentSession.CurrentQuestion?.Options.Count ?? 4;
                    if (subReq.SelectedOptionIndex < 0 || subReq.SelectedOptionIndex >= optionCount)
                    {
                        await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Invalid answer option.\"}"));
                        return;
                    }

                    bool accepted = _engine.SubmitAnswer(trimmedName, subReq.SelectedOptionIndex, subReq.ResponseTimeMs);
                    var resp = new { success = accepted };
                    await SendResponseAsync(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resp)));
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

        if (path.Equals("/api/trivia/state", StringComparison.OrdinalIgnoreCase) && method == "GET")
        {
            string playerName = "";
            string requestPlayerId = "";
            if (!string.IsNullOrEmpty(queryString))
            {
                var qParams = HttpUtility.ParseQueryString(queryString);
                playerName = TriviaGameEngine.NormalizePlayerName(qParams["player"]);
                requestPlayerId = qParams["playerId"] ?? "";
            }

            // Fetched once and reused below (identity lookup, GetGameResult, leaderboard) instead
            // of calling GetPlayers() up to three times per poll - this endpoint is hit roughly
            // once a second by every connected phone, and each call re-scans/re-sorts every player.
            var allPlayers = _engine.GetPlayers();

            var player = !string.IsNullOrEmpty(playerName)
                ? allPlayers.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                : null;

            // A playerId that doesn't match means another device has since taken over this name
            // (or this caller's own session is stale) - don't hand back that player's live score,
            // streak, or hasAnswered state to someone who isn't actually them.
            if (player != null && !string.IsNullOrEmpty(requestPlayerId) &&
                !string.Equals(player.PlayerId, requestPlayerId, StringComparison.OrdinalIgnoreCase))
            {
                player = null;
            }

            if (player != null)
            {
                player.IsConnected = true;
                player.LastSeenAt = DateTime.UtcNow;
            }

            var q = _engine.CurrentSession.CurrentQuestion;
            bool isCorrect = player != null && q != null && player.LastAnswerIndex == q.CorrectAnswerIndex;
            var gameResult = _engine.GetGameResult(allPlayers);

            int visibleCount = Math.Max(1, (q?.Options.Count ?? 4) - _engine.EliminatedAnswerIndices.Count);
            int currentPercent = _engine.GetTierPercent(visibleCount);
            int potentialPoints = (int)(_engine.Settings.BasePointsPerQuestion * (currentPercent / 100.0));

            var statePayload = new
            {
                state = _engine.State.ToString(),
                isPaused = _engine.IsPaused,
                pauseReason = _engine.PauseReason ?? "",
                remainingSeconds = _engine.RemainingSeconds,
                totalSeconds = _engine.TotalCountdownSeconds,
                warningSeconds = _engine.Settings.WarningCountdownSeconds,
                questionId = q?.Id ?? "",
                prompt = q?.Prompt ?? "",
                options = q?.Options ?? [],
                eliminatedIndices = _engine.EliminatedAnswerIndices,
                tieredScoringEnabled = _engine.Settings.TieredScoringEnabled,
                currentValuePercent = currentPercent,
                currentPotentialPoints = potentialPoints,
                basePoints = _engine.Settings.BasePointsPerQuestion,
                visibleOptionsCount = visibleCount,
                correctIndex = (_engine.State == TriviaGameState.RevealAnswer || _engine.State == TriviaGameState.RoundLeaderboard) ? q?.CorrectAnswerIndex : -1,
                explanation = (_engine.State == TriviaGameState.RevealAnswer) ? q?.Explanation : "",
                isCorrect = isCorrect,
                pointsEarned = player?.LastPointsEarned ?? 0,
                playerScore = player?.TotalScore ?? 0,
                playerStreak = player?.CurrentStreak ?? 0,
                hasAnswered = player?.HasAnsweredCurrentQuestion ?? false,
                hasTeamWinner = gameResult.HasTeamWinner,
                winnerTitle = gameResult.WinnerTitle,
                winningName = gameResult.WinningName,
                winningScore = gameResult.WinningScore,
                winningTeamMembers = gameResult.WinningTeamMembers,
                winningTeamMembersRoster = gameResult.WinningTeamMembersRoster,
                intermissionSecondsRemaining = _engine.IntermissionSecondsRemaining,
                isIntermissionActive = _engine.IsInIntermission,
                leaderboard = allPlayers.Take(10).Select((p, idx) => new
                {
                    rank = idx + 1,
                    name = p.Name,
                    team = p.TeamName,
                    score = p.TotalScore
                })
            };

            await SendResponseAsync(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(statePayload)));
            return;
        }

        // 404 Not Found
        await SendResponseAsync(stream, 404, "text/plain", Encoding.UTF8.GetBytes("404 Not Found"));
    }

    private async Task<byte[]> GetHtmlBytesAsync()
    {
        if (_cachedHtml == null)
        {
            var asm = Assembly.GetExecutingAssembly();
            await using var stream = asm.GetManifestResourceStream("Lyracist.Trivia.Core.Resources.trivia.html");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                _cachedHtml = await reader.ReadToEndAsync();
            }
            else
            {
                string diskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "trivia.html");
                if (File.Exists(diskPath))
                {
                    _cachedHtml = await File.ReadAllTextAsync(diskPath);
                }
                else
                {
                    _cachedHtml = "<html><body><h1>Lyracist Live Trivia</h1><p>trivia.html resource not found.</p></body></html>";
                }
            }
        }
        return Encoding.UTF8.GetBytes(_cachedHtml);
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
            409 => "Conflict",
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

    /// <summary>
    /// Minimal buffered reader over the raw NetworkStream, used only for the request-parsing side
    /// of a connection. ReadHeadersAsync used to pull one byte at a time straight off a
    /// BufferedStream, which meant an await/state-machine resume per header byte (~300-500 per
    /// request) even though the underlying socket read was already batched - this collapses that
    /// down to one resume per internal-buffer refill (every 4KB). ReadAsync drains any bytes left
    /// over from that buffer (e.g. the start of the body, if the client sent header+body in one
    /// packet) before falling through to the underlying stream, so body reads stay correct.
    /// </summary>
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

    private record JoinRequest(string Name, string? TeamName, string? PlayerId);
    private record SubmitRequest(string PlayerName, int SelectedOptionIndex, double ResponseTimeMs, string? PlayerId);
}
