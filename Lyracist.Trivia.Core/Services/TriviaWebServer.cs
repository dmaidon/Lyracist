// Edited on Aug 17, 2026 @ 15:42:30 -> Added isPaused and pauseReason to web status payload
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
            catch (Exception ex) when (ex is OperationCanceledException || ex is ObjectDisposedException || ex is SocketException)
            {
                break; // Server stopping
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TriviaWebServer AcceptConnectionsAsync error: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken serverToken)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var readStream = new BufferedStream(stream, 4096))
        using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(serverToken))
        {
            timeoutCts.CancelAfter(RequestReadTimeout);

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

                await RouteRequestAsync(stream, method, path, queryString, body);
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is IOException || ex is SocketException)
            {
                // Client disconnected or timed out
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TriviaWebServer HandleClientAsync error: {ex.Message}");
                try
                {
                    await SendResponseAsync(stream, 500, "application/json; charset=utf-8", Encoding.UTF8.GetBytes("{\"error\":\"Internal Server Error\"}"));
                }
                catch { }
            }
        }
    }

    private async Task RouteRequestAsync(NetworkStream stream, string method, string path, string queryString, string body)
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
                    var player = _engine.RegisterPlayer(joinReq.Name.Trim(), joinReq.TeamName?.Trim() ?? "");
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
                    bool accepted = _engine.SubmitAnswer(subReq.PlayerName.Trim(), subReq.SelectedOptionIndex, subReq.ResponseTimeMs);
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
            if (!string.IsNullOrEmpty(queryString))
            {
                var qParams = HttpUtility.ParseQueryString(queryString);
                playerName = qParams["player"] ?? "";
            }

            var player = !string.IsNullOrEmpty(playerName)
                ? _engine.GetPlayers().FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                : null;

            var q = _engine.CurrentSession.CurrentQuestion;
            bool isCorrect = player != null && q != null && player.LastAnswerIndex == q.CorrectAnswerIndex;
            var gameResult = _engine.GetGameResult();

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

    private static async Task SendCorsPreflightResponseAsync(NetworkStream stream)
    {
        string response = "HTTP/1.1 204 No Content\r\n" +
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

    private record JoinRequest(string Name, string? TeamName);
    private record SubmitRequest(string PlayerName, int SelectedOptionIndex, double ResponseTimeMs);
}
