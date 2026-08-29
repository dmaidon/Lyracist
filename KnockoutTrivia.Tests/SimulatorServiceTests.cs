// Created on Aug 29, 2026 @ 10:48:30 -> SimulatorService unit tests for bot spawning, answers, and super streaks
using System.Linq;
using System.Threading.Tasks;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;
using Xunit;

namespace KnockoutTrivia.Tests;

public class SimulatorServiceTests
{
    [Fact]
    public void SimulatorService_SpawnBots_PopulatesBotPlayers()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);
        using var sim = new SimulatorService(game);

        sim.SpawnBots(8);

        Assert.Equal(8, sim.BotCount);
        Assert.Equal(8, game.Players.Count);
        Assert.All(game.Players, p => Assert.True(p.IsBot));
        Assert.All(game.Players, p => Assert.Equal(1, p.Tokens));
    }

    [Fact]
    public void SimulatorService_ClearBots_RemovesOnlyBots()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);
        using var sim = new SimulatorService(game);

        var realPlayer = game.RegisterOrGetPlayer("RealHuman");
        realPlayer.IsBot = false;

        sim.SpawnBots(5);
        Assert.Equal(6, game.Players.Count);

        sim.ClearBots();
        Assert.Equal(0, sim.BotCount);
        Assert.Single(game.Players);
        Assert.Equal("RealHuman", game.Players[0].Name);
    }

    [Fact]
    public void SimulatorService_TriggerSimulatedSuperStreak_ReachesTwentyConsecutive()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);
        using var sim = new SimulatorService(game);

        sim.SpawnBots(3);
        sim.TriggerSimulatedSuperStreak();

        var streaker = game.Players.FirstOrDefault(p => p.StreakCount == 20);
        Assert.NotNull(streaker);
    }

    [Fact]
    public void SimulatorService_ClearLogs_EmptiesActivityList()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);
        using var sim = new SimulatorService(game);

        sim.SpawnBots(2);
        Assert.NotEmpty(sim.ActivityLogs);

        sim.ClearLogs();
        Assert.Empty(sim.ActivityLogs);
    }
}
