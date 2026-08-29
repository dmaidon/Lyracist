// Created on Aug 29, 2026 @ 10:42:00 -> KnockoutWebServer integration tests covering endpoints, session tokens, and HTML delivery
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;
using Xunit;

namespace KnockoutTrivia.Tests;

public class KnockoutWebServerTests
{
    private static int _nextPort = 9020;
    private static readonly object _portLock = new();

    private static int GetNextPort()
    {
        lock (_portLock)
        {
            return _nextPort++;
        }
    }

    [Fact]
    public async Task WebServer_ServesHtmlAndCompanionEndpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);
        game.InitializeGame(new KnockoutSettings());

        int port = GetNextPort();
        using var server = new KnockoutWebServer(game, port);
        server.Start();

        Assert.True(server.IsRunning);
        Assert.Equal(port, server.Port);

        using var client = new HttpClient();
        string baseUrl = $"http://127.0.0.1:{port}";

        // 1. GET / (Mobile Web Companion HTML)
        var htmlResp = await client.GetAsync($"{baseUrl}/", ct);
        Assert.True(htmlResp.IsSuccessStatusCode);
        string html = await htmlResp.Content.ReadAsStringAsync(ct);
        Assert.Contains("Knockout Trivia", html);

        // 2. POST /api/knockout/join
        var joinPayload = JsonSerializer.Serialize(new { name = "WebServerTestPlayer" });
        var joinResp = await client.PostAsync($"{baseUrl}/api/knockout/join", new StringContent(joinPayload, Encoding.UTF8, "application/json"), ct);
        Assert.True(joinResp.IsSuccessStatusCode);
        string joinJson = await joinResp.Content.ReadAsStringAsync(ct);
        using var joinDoc = JsonDocument.Parse(joinJson);
        string playerId = joinDoc.RootElement.GetProperty("playerId").GetString()!;
        string sessionToken = joinDoc.RootElement.GetProperty("sessionToken").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(playerId));
        Assert.False(string.IsNullOrWhiteSpace(sessionToken));

        // 3. GET /api/knockout/state with valid playerId and sessionToken
        var stateResp = await client.GetAsync($"{baseUrl}/api/knockout/state?playerId={playerId}&sessionToken={sessionToken}", ct);
        Assert.True(stateResp.IsSuccessStatusCode);
        string stateJson = await stateResp.Content.ReadAsStringAsync(ct);
        using var stateDoc = JsonDocument.Parse(stateJson);
        Assert.Equal("Lobby", stateDoc.RootElement.GetProperty("phase").GetString());
        var playerObj = stateDoc.RootElement.GetProperty("player");
        Assert.Equal("WebServerTestPlayer", playerObj.GetProperty("name").GetString());

        // 4. POST /api/knockout/submit
        var submitPayload = JsonSerializer.Serialize(new
        {
            playerId,
            sessionToken,
            selectedOptionIndex = 0,
            responseTimeMs = 1200
        });
        var submitResp = await client.PostAsync($"{baseUrl}/api/knockout/submit", new StringContent(submitPayload, Encoding.UTF8, "application/json"), ct);
        Assert.True(submitResp.IsSuccessStatusCode);

        server.Stop();
        Assert.False(server.IsRunning);
    }
}
