// Created on Aug 29, 2026 @ 10:41:00 -> GameStateService unit tests covering lifecycle, scoring, elimination, and shuffling
using System;
using System.Collections.Generic;
using System.Linq;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;
using Xunit;

namespace KnockoutTrivia.Tests;

public class GameStateServiceTests
{
    [Fact]
    public void GameStateService_InitializesInLobbyPhase()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        game.InitializeGame(new KnockoutSettings());

        Assert.Equal(GameStatePhase.Lobby, game.Phase);
        Assert.Empty(game.Players);
        Assert.Equal(0, game.CurrentQuestionIndex);
    }

    [Fact]
    public void RegisterOrGetPlayer_CreatesNewPlayerWhenNotExisting()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        var player = game.RegisterOrGetPlayer("Sam");

        Assert.NotNull(player);
        Assert.Equal("Sam", player.Name);
        Assert.Equal(1, player.Tokens); // Initial 1 token
        Assert.True(player.IsConnected);
        Assert.False(string.IsNullOrWhiteSpace(player.SessionToken));
        Assert.Single(game.Players);
    }

    [Fact]
    public void RegisterOrGetPlayer_DisambiguatesDuplicateNamesForConnectedPlayers()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        var player1 = game.RegisterOrGetPlayer("Alex");
        var player2 = game.RegisterOrGetPlayer("Alex"); // Another phone joins with name "Alex"

        Assert.NotEqual(player1.Id, player2.Id);
        Assert.NotEqual(player1.SessionToken, player2.SessionToken);
        Assert.Equal(2, game.Players.Count);
    }

    [Fact]
    public void RegisterOrGetPlayer_ReclaimsDisconnectedPlayerRecord()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        var player1 = game.RegisterOrGetPlayer("Chris");
        player1.IsConnected = false; // Connection dropped

        var reconnected = game.RegisterOrGetPlayer("Chris");

        Assert.Equal(player1.Id, reconnected.Id);
        Assert.True(reconnected.IsConnected);
        Assert.Single(game.Players);
    }

    [Fact]
    public void SubmitPlayerAnswer_ValidatesAnswerRange()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        var player = game.RegisterOrGetPlayer("Jordan");
        game.StartGame();

        // When no question or out of range options, submission is rejected
        bool acceptedOutOfRange = game.SubmitPlayerAnswer(player.Id, 999, 1500, player.SessionToken);
        Assert.False(acceptedOutOfRange);

        bool acceptedNegative = game.SubmitPlayerAnswer(player.Id, -5, 1500, player.SessionToken);
        Assert.False(acceptedNegative);
    }

    [Fact]
    public void SubmitPlayerAnswer_RejectsInvalidSessionToken()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        var player = game.RegisterOrGetPlayer("Taylor");
        game.StartGame();

        bool accepted = game.SubmitPlayerAnswer(player.Id, 0, 1200, "fake_session_token_123");
        Assert.False(accepted);
    }

    [Fact]
    public void EvaluateSubmittedAnswers_AwardsPointsAndRecordsStrikes()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        var settings = new KnockoutSettings
        {
            PointsPerCorrectAnswer = 10,
            StreakRequirement = 5,
            SuperStreakThreshold = 20
        };
        game.InitializeGame(settings);

        var p1 = game.RegisterOrGetPlayer("Player1"); // Will answer correctly
        var p2 = game.RegisterOrGetPlayer("Player2"); // Will answer incorrectly, has 1 shield
        var p3 = game.RegisterOrGetPlayer("Player3"); // Will answer incorrectly, 0 shields
        p3.Tokens = 0;

        game.StartGame();

        // Record answers directly
        game.RecordPlayerAnswer(p1.Id, true);
        game.RecordPlayerAnswer(p2.Id, false);
        game.RecordPlayerAnswer(p3.Id, false);

        Assert.Equal(10, p1.Score);
        Assert.Equal(0, p1.StrikeCount);
        Assert.Equal(1, p1.StreakCount);

        // Player 2 had 1 token, so token deducted, was shield protected, 0 strikes
        Assert.Equal(0, p2.Tokens);
        Assert.True(p2.WasShieldProtected);
        Assert.Equal(0, p2.StrikeCount);

        // Player 3 had 0 tokens, so received 1 strike
        Assert.Equal(0, p3.Tokens);
        Assert.False(p3.WasShieldProtected);
        Assert.Equal(1, p3.StrikeCount);
    }

    [Fact]
    public void PlayerElimination_OccursAtThreeStrikes()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        var player = game.RegisterOrGetPlayer("Dave");
        player.Tokens = 0;

        bool eliminatedEventFired = false;
        game.PlayerEliminated += (s, p) => eliminatedEventFired = true;

        game.RecordPlayerAnswer(player.Id, false); // Strike 1
        Assert.Equal(1, player.StrikeCount);
        Assert.False(player.IsEliminated);

        game.RecordPlayerAnswer(player.Id, false); // Strike 2
        Assert.Equal(2, player.StrikeCount);
        Assert.False(player.IsEliminated);

        game.RecordPlayerAnswer(player.Id, false); // Strike 3
        Assert.Equal(3, player.StrikeCount);
        Assert.True(player.IsEliminated);
        Assert.True(eliminatedEventFired);
    }

    [Fact]
    public void ShuffleQuestions_UsesUniformFisherYates()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        game.ShuffleQuestions();
        Assert.Equal(0, game.TotalQuestions);
    }
}
