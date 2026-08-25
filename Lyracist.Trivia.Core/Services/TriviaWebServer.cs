// Edited on Aug 20, 2026 @ 06:23:00 -> Added tiered scoring properties and potential points to /api/trivia/state payload
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

    // Connections are now kept alive across many requests (see HandleClientAsync) instead of one
    // request per TCP connection, so this now bounds concurrently *connected* phones rather than
    // concurrently *in-flight* requests - a much bigger number for the same crowd size. Sized for
    // a busy venue (100+ phones each holding one open connection) with headroom.
    private const int MaxConcurrentConnections = 200;
    private static readonly TimeSpan RequestReadTimeout = TimeSpan.FromSeconds(15);

    private readonly TriviaGameEngine _engine;
    private readonly int _port;
    private readonly SemaphoreSlim _connectionLimiter = new(MaxConcurrentConnections, MaxConcurrentConnections);

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private string? _cachedHtml;
    private bool _disposed;

    public bool IsRunning { get; private set; }
    public int Port => _port;

    public TriviaWebServer(TriviaGameEngine engine, int port = 8085)
    {
        _engine = engine;
        _port = port;
    }

    public void Start()
    {
        if (IsRunning) return;

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            IsRunning = true;

            Task.Run(() => AcceptConnectionsAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TriviaWebServer Start failed on port {_port}: {ex.Message}");
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

                if (!_connectionLimiter.Wait(0))
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
                System.Diagnostics.Debug.WriteLine($"TriviaWebServer AcceptConnectionsAsync error: {ex.Message}");
                try { await Task.Delay(250, token); } catch { break; }
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken serverToken)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var readStream = new BufferedStream(stream, 4096))
        {
            // Phones poll /api/trivia/state roughly once a second while connected. Serving each
            // poll on its own TCP connection (the old behavior - every response sent
            // "Connection: close") meant a fresh handshake per poll, which adds up fast with a
            // venue full of phones. Reuse this connection for as many requests as the client
            // keeps sending (HTTP/1.1's default), closing only when the client asks to
            // (Connection: close), the read times out (RequestReadTimeout, same budget used for
            // both the first request and every request after - a connected phone polling every
            // second is comfortably inside that window), or something goes wrong.
            while (!serverToken.IsCancellationRequested)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(serverToken);
                timeoutCts.CancelAfter(RequestReadTimeout);
                bool keepAlive = false;

                try
                {
                    string headers = await ReadHeadersAsync(readStream, timeoutCts.Token);
                    if (string.IsNullOrWhiteSpace(headers)) return; // client closed the connection

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

                    keepAlive = !RequestWantsClose(headerLines);

                    if (method == "OPTIONS")
                    {
                        await SendCorsPreflightResponseAsync(stream, keepAlive);
                        if (!keepAlive) return;
                        continue;
                    }

                    int contentLength = ParseContentLength(headerLines);
                    if (contentLength > MaxRequestBodyBytes)
                    {
                        await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Request body too large.\"}"), keepAlive: false);
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
                            if (read == 0)
                            {
                                // Connection closed mid-body - too short to safely resume reading
                                // a next request from this stream even if the client asked to
                                // keep it alive.
                                return;
                            }
                            readTotal += read;
                        }
                        body = Encoding.UTF8.GetString(bodyBuffer, 0, readTotal);
                    }

                    await RouteRequestAsync(stream, method, path, queryString, body, keepAlive);
                    if (!keepAlive) return;
                }
                catch (Exception ex) when (ex is OperationCanceledException || ex is IOException || ex is SocketException)
                {
                    // Client disconnected or timed out
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"TriviaWebServer HandleClientAsync error: {ex.Message}");
                    try
                    {
                        await SendResponseAsync(stream, 500, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Internal Server Error\"}"), keepAlive: false);
                    }
                    catch { }
                    return;
                }
            }
        }
    }

    /// True if the request explicitly asked to close the connection after this response.
    /// HTTP/1.1 defaults to keep-alive otherwise (this server only ever speaks HTTP/1.1).
    private static bool RequestWantsClose(string[] headerLines)
    {
        foreach (var line in headerLines)
        {
            if (line.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase))
            {
                return line["Connection:".Length..].Trim().Equals("close", StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }

    private async Task RouteRequestAsync(NetworkStream stream, string method, string path, string queryString, string body, bool keepAlive)
    {
        // Every branch below has already fully consumed any request body (HandleClientAsync reads
        // exactly Content-Length bytes before calling this), so the stream is always correctly
        // positioned for a next request regardless of which response we send - keepAlive can be
        // honored on every path, including error responses.
        if (path == "/" || path.Equals("/trivia", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            byte[] htmlBytes = await GetHtmlBytesAsync();
            await SendResponseAsync(stream, 200, "text/html; charset=utf-8", htmlBytes, keepAlive);
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

                    if (existing != null && existing.IsConnected && !isSamePlayer)
                    {
                        await SendResponseAsync(stream, 409, "application/json; charset=utf-8",
                            Encoding.UTF8.GetBytes("{\"error\":\"That name is already in use this game. Please choose a different name.\"}"), keepAlive);
                        return;
                    }

                    var player = _engine.RegisterPlayer(trimmedName, joinReq.TeamName?.Trim() ?? "");
                    var resp = new { success = true, playerId = player.PlayerId, name = player.Name };
                    await SendResponseAsync(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resp)), keepAlive);
                }
                else
                {
                    await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Name is required.\"}"), keepAlive);
                }
            }
            catch
            {
                await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Invalid JSON format.\"}"), keepAlive);
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
                        await SendResponseAsync(stream, 409, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Player identity mismatch - please rejoin.\"}"), keepAlive);
                        return;
                    }

                    int optionCount = _engine.CurrentSession.CurrentQuestion?.Options.Count ?? 4;
                    if (subReq.SelectedOptionIndex < 0 || subReq.SelectedOptionIndex >= optionCount)
                    {
                        await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Invalid answer option.\"}"), keepAlive);
                        return;
                    }

                    bool accepted = _engine.SubmitAnswer(trimmedName, subReq.SelectedOptionIndex, subReq.ResponseTimeMs);
                    var resp = new { success = accepted };
                    await SendResponseAsync(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resp)), keepAlive);
                }
                else
                {
                    await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Invalid submission payload.\"}"), keepAlive);
                }
            }
            catch
            {
                await SendResponseAsync(stream, 400, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Invalid JSON format.\"}"), keepAlive);
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

            var player = !string.IsNullOrEmpty(playerName)
                ? _engine.GetPlayers().FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase))
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
                player.LastSeenAt = DateTime.Now;
            }

            var q = _engine.CurrentSession.CurrentQuestion;
            bool isCorrect = player != null && q != null && player.LastAnswerIndex == q.CorrectAnswerIndex;
            var gameResult = _engine.GetGameResult();

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
                leaderboard = _engine.GetPlayers().Take(10).Select((p, idx) => new
                {
                    rank = idx + 1,
                    name = p.Name,
                    team = p.TeamName,
                    score = p.TotalScore
                })
            };

            await SendResponseAsync(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(statePayload)), keepAlive);
            return;
        }

        // 404 Not Found
        await SendResponseAsync(stream, 404, "text/plain", Encoding.UTF8.GetBytes("404 Not Found"), keepAlive);
    }

    private async Task<byte[]> GetHtmlBytesAsync()
    {
        if (_cachedHtml == null)
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("Lyracist.Trivia.Core.Resources.trivia.html");
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

    private static async Task SendCorsPreflightResponseAsync(NetworkStream stream, bool keepAlive)
    {
        string response = "HTTP/1.1 204 No Content\r\n" +
                          "Access-Control-Allow-Origin: *\r\n" +
                          "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                          "Access-Control-Allow-Headers: Content-Type\r\n" +
                          "Access-Control-Max-Age: 86400\r\n" +
                          (keepAlive ? "Connection: keep-alive\r\n" : "Connection: close\r\n") +
                          "Content-Length: 0\r\n\r\n";
        byte[] bytes = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(bytes);
    }

    private static async Task SendResponseAsync(NetworkStream stream, int statusCode, string contentType, byte[] content, bool keepAlive)
    {
        string statusText = statusCode switch
        {
            200 => "OK",
            204 => "No Content",
            400 => "Bad Request",
            404 => "Not Found",
            500 => "Internal Server Error",
            _ => "OK"
        };

        string header = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                        $"Content-Type: {contentType}\r\n" +
                        $"Content-Length: {content.Length}\r\n" +
                        "Access-Control-Allow-Origin: *\r\n" +
                        "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                        "Access-Control-Allow-Headers: Content-Type\r\n" +
                        (keepAlive ? "Connection: keep-alive\r\n\r\n" : "Connection: close\r\n\r\n");

        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
        await stream.WriteAsync(headerBytes);
        if (content.Length > 0)
        {
            await stream.WriteAsync(content);
        }
    }

    private static async Task<string> ReadHeadersAsync(BufferedStream stream, CancellationToken token)
    {
        using var ms = new MemoryStream();
        byte[] buffer = new byte[1];
        int pattern = 0; // Tracks \r\n\r\n

        while (ms.Length < 8192)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, 1), token);
            if (read == 0) break;

            ms.WriteByte(buffer[0]);

            if (buffer[0] == '\r' && (pattern == 0 || pattern == 2)) pattern++;
            else if (buffer[0] == '\n' && (pattern == 1 || pattern == 3)) pattern++;
            else if (buffer[0] == '\r') pattern = 1;
            else pattern = 0;

            if (pattern == 4) break;
        }

        return Encoding.UTF8.GetString(ms.ToArray());
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
