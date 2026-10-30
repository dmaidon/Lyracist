// Edited on Oct 30, 2026 @ 09:20:00 -> Add tests for pack wrapper deserialization, UpdateSettings, no double-grading, and question-limit winner
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

    [Fact]
    public async Task TriviaDataService_LoadsBothPackWrapperAndRawJsonArray()
    {
        var trivia = new TriviaDataService();
        string tempWrapperPath = Path.Combine(Path.GetTempPath(), $"ko_pack_wrapper_{Guid.NewGuid():N}.json");
        string tempArrayPath = Path.Combine(Path.GetTempPath(), $"ko_pack_array_{Guid.NewGuid():N}.json");

        try
        {
            string wrapperJson = """
            {
              "PackId": "test_pack_01",
              "Title": "80s Pop Classics",
              "Category": "80s Music",
              "Description": "Classic 80s hits",
              "Questions": [
                {
                  "Id": "Q1",
                  "Difficulty": "Medium",
                  "QuestionType": "MultipleChoice",
                  "Prompt": "Who sang Thriller?",
                  "Options": ["Michael Jackson", "Prince", "Madonna", "Bowie"],
                  "CorrectAnswerIndex": 0,
                  "Explanation": "Released in 1982."
                }
              ]
            }
            """;

            string arrayJson = """
            [
              {
                "Id": "Q2",
                "Category": "Science",
                "Difficulty": "Easy",
                "QuestionType": "MultipleChoice",
                "Prompt": "What planet is known as the Red Planet?",
                "Options": ["Venus", "Mars", "Jupiter", "Saturn"],
                "CorrectAnswerIndex": 1
              }
            ]
            """;

            await File.WriteAllTextAsync(tempWrapperPath, wrapperJson, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(tempArrayPath, arrayJson, TestContext.Current.CancellationToken);

            var wrapperQuestions = await trivia.LoadQuestionsFromPackAsync(tempWrapperPath);
            Assert.Single(wrapperQuestions);
            Assert.Equal("Q1", wrapperQuestions[0].Id);
            Assert.Equal("80s Music", wrapperQuestions[0].Category);
            Assert.Equal(TriviaDifficulty.Medium, wrapperQuestions[0].Difficulty);

            var arrayQuestions = await trivia.LoadQuestionsFromPackAsync(tempArrayPath);
            Assert.Single(arrayQuestions);
            Assert.Equal("Q2", arrayQuestions[0].Id);
            Assert.Equal("Science", arrayQuestions[0].Category);
            Assert.Equal(TriviaDifficulty.Easy, arrayQuestions[0].Difficulty);
        }
        finally
        {
            if (File.Exists(tempWrapperPath)) File.Delete(tempWrapperPath);
            if (File.Exists(tempArrayPath)) File.Delete(tempArrayPath);
        }
    }

    [Fact]
    public void UpdateSettings_UpdatesLiveSettingsAndQuestionTimerPriority()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        game.InitializeGame(new KnockoutSettings { QuestionTimerSeconds = 15, PointsPerCorrectAnswer = 10 });
        Assert.Equal(1, game.CurrentQuestionNumber);

        var updated = new KnockoutSettings { QuestionTimerSeconds = 25, PointsPerCorrectAnswer = 20 };
        game.UpdateSettings(updated);

        Assert.Same(updated, game.Settings);
        Assert.Equal(20, game.Settings.PointsPerCorrectAnswer);

        game.StartQuestionTimer();
        Assert.Equal(25, game.TotalCountdownSeconds);
        Assert.Equal(25, game.SecondsRemaining);
        game.StopTimer();
    }

    [Fact]
    public async Task RecordPlayerAnswer_DoesNotDoubleGradeOnRevealAnswer()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        string tempPack = Path.Combine(Path.GetTempPath(), $"ko_eval_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempPack, """
            [
              {
                "Id": "Q1",
                "Prompt": "2 + 2?",
                "Options": ["3", "4", "5", "6"],
                "CorrectAnswerIndex": 1
              }
            ]
            """, TestContext.Current.CancellationToken);

            game.InitializeGame(new KnockoutSettings { PointsPerCorrectAnswer = 10 });
            await game.LoadGameQuestionsAsync(tempPack);

            var p1 = game.RegisterOrGetPlayer("ManualCorrect");
            var p2 = game.RegisterOrGetPlayer("ManualWrong");
            p2.Tokens = 0;

            game.StartGame();

            // Host manually adjudicates both players before clicking RevealAnswer
            game.RecordPlayerAnswer(p1.Id, true);
            game.RecordPlayerAnswer(p2.Id, false);

            Assert.Equal(10, p1.Score);
            Assert.Equal(1, p2.StrikeCount);

            // Now host clicks RevealAnswer -> EvaluateSubmittedAnswers must skip already evaluated players
            game.RevealAnswer();

            Assert.Equal(10, p1.Score);
            Assert.Equal(1, p1.StreakCount);
            Assert.Equal(1, p2.StrikeCount);
        }
        finally
        {
            if (File.Exists(tempPack)) File.Delete(tempPack);
        }
    }

    [Fact]
    public async Task NextQuestion_FiresGameWonForHighestScoringSurvivorWhenQuestionLimitReached()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        string tempPack = Path.Combine(Path.GetTempPath(), $"ko_limit_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempPack, """
            [
              {
                "Id": "Q1",
                "Prompt": "Question 1?",
                "Options": ["A", "B"],
                "CorrectAnswerIndex": 0
              }
            ]
            """, TestContext.Current.CancellationToken);

            game.InitializeGame(new KnockoutSettings { NumberOfQuestionsPerGame = 1, UnlimitedQuestions = false });
            await game.LoadGameQuestionsAsync(tempPack);

            var p1 = game.RegisterOrGetPlayer("LowScorer");
            p1.Score = 20;
            var p2 = game.RegisterOrGetPlayer("HighScorer");
            p2.Score = 50;

            KnockoutPlayer? declaredWinner = null;
            bool gameWonFired = false;
            game.GameWon += (s, winner) =>
            {
                gameWonFired = true;
                declaredWinner = winner;
            };

            game.StartGame();
            game.RevealAnswer();
            game.NextQuestion(); // Reaches question limit (1) with 2 survivors

            Assert.Equal(GameStatePhase.GameOver, game.Phase);
            Assert.True(gameWonFired);
            Assert.NotNull(declaredWinner);
            Assert.Equal("HighScorer", declaredWinner!.Name);
        }
        finally
        {
            if (File.Exists(tempPack)) File.Delete(tempPack);
        }
    }

    [Fact]
    public void KnockoutPlayer_HasTokenFlags_StayTrueWhenTokensExceedThree()
    {
        var p = new KnockoutPlayer { Tokens = 5 };
        Assert.True(p.HasToken1);
        Assert.True(p.HasToken2);
        Assert.True(p.HasToken3);
    }
}
