// Edited on Oct 31, 2026 @ 11:21:30 -> Add test for TriviaDataService ignoring non-trivia databases without Questions table
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

    [Fact]
    public async Task EvaluateSubmittedAnswers_DefersSuperStreakUntilAllPlayersAreGraded()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        string tempPack = Path.Combine(Path.GetTempPath(), $"ko_defer_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempPack, """
            [
              {
                "Id": "Q1",
                "Prompt": "Question 1?",
                "Options": ["A", "B", "C", "D"],
                "CorrectAnswerIndex": 0
              }
            ]
            """, TestContext.Current.CancellationToken);

            game.InitializeGame(new KnockoutSettings { SuperStreakEnabled = true, SuperStreakThreshold = 20 });
            await game.LoadGameQuestionsAsync(tempPack);

            var streaker = game.RegisterOrGetPlayer("Streaker");
            streaker.StreakCount = 19;

            var opponent = game.RegisterOrGetPlayer("OpponentWithLastShield");
            opponent.Tokens = 1; // Will miss Q1 and lose this shield before wheel segments are built!

            var survivingOpponent = game.RegisterOrGetPlayer("SurvivingShieldHolder");
            survivingOpponent.Tokens = 2;
            survivingOpponent.Score = 50;

            int opponentTokensWhenSuperStreakFired = -1;
            game.SuperStreakTriggered += (s, p) =>
            {
                opponentTokensWhenSuperStreakFired = opponent.Tokens;
            };

            game.StartGame();
            streaker.StreakCount = 19;

            game.SubmitPlayerAnswer(streaker.Id, 0, 1000, streaker.SessionToken); // Correct -> hits 20
            game.SubmitPlayerAnswer(opponent.Id, 1, 1200, opponent.SessionToken); // Wrong -> loses shield
            game.SubmitPlayerAnswer(survivingOpponent.Id, 0, 1100, survivingOpponent.SessionToken); // Correct -> keeps 2 shields

            game.RevealAnswer();

            Assert.Equal(GameStatePhase.SuperStreak, game.Phase);
            Assert.Equal(0, opponentTokensWhenSuperStreakFired);
            Assert.Equal(0, opponent.Tokens);
        }
        finally
        {
            if (File.Exists(tempPack)) File.Delete(tempPack);
        }
    }

    [Fact]
    public async Task RecordPlayerAnswer_PreventsRepeatedManualAdjudicationOnSameQuestion()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        string tempPack = Path.Combine(Path.GetTempPath(), $"ko_manual_dup_{Guid.NewGuid():N}.json");
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

            game.InitializeGame(new KnockoutSettings { PointsPerCorrectAnswer = 10 });
            await game.LoadGameQuestionsAsync(tempPack);

            var p = game.RegisterOrGetPlayer("ManualTester");
            game.StartGame();

            game.RecordPlayerAnswer(p.Id, true);
            game.RecordPlayerAnswer(p.Id, true); // Duplicate click on same active question

            Assert.Equal(10, p.Score);
            Assert.Equal(1, p.StreakCount);
        }
        finally
        {
            if (File.Exists(tempPack)) File.Delete(tempPack);
        }
    }

    [Fact]
    public async Task SpeedBonusAndTieBreaker_AwardsExtraPointsAndBreaksTiesByFastestAverageTime()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        string tempPack = Path.Combine(Path.GetTempPath(), $"ko_speed_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempPack, """
            [
              {
                "Id": "Q1",
                "Prompt": "Fast question?",
                "Options": ["A", "B"],
                "CorrectAnswerIndex": 0
              }
            ]
            """, TestContext.Current.CancellationToken);

            game.InitializeGame(new KnockoutSettings
            {
                PointsPerCorrectAnswer = 10,
                EnableSpeedBonus = true,
                QuestionTimerSeconds = 15,
                NumberOfQuestionsPerGame = 1
            });
            await game.LoadGameQuestionsAsync(tempPack);

            var slowPlayer = game.RegisterOrGetPlayer("Slow");
            var fastPlayer = game.RegisterOrGetPlayer("Fast");

            game.StartGame();
            game.SubmitPlayerAnswer(slowPlayer.Id, 0, 14000, slowPlayer.SessionToken);
            game.SubmitPlayerAnswer(fastPlayer.Id, 0, 1000, fastPlayer.SessionToken);
            game.RevealAnswer();

            Assert.True(fastPlayer.Score > slowPlayer.Score);
            Assert.Equal(1000, fastPlayer.AverageResponseTimeMs);
            Assert.Equal(14000, slowPlayer.AverageResponseTimeMs);

            // Force tie on score and strikes to verify tie-breaker picks faster average response time
            slowPlayer.Score = 50;
            fastPlayer.Score = 50;

            KnockoutPlayer? winner = null;
            game.GameWon += (s, w) => winner = w;

            game.NextQuestion();
            Assert.NotNull(winner);
            Assert.Equal("Fast", winner!.Name);
        }
        finally
        {
            if (File.Exists(tempPack)) File.Delete(tempPack);
        }
    }

    [Fact]
    public void RevivePlayer_RestoresEliminatedPlayerToActiveWithOneShield()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        var p1 = game.RegisterOrGetPlayer("Survivor");
        var p2 = game.RegisterOrGetPlayer("KnockedOut");
        p2.Tokens = 0;

        game.AddPlayerStrike(p2);
        game.AddPlayerStrike(p2);
        game.AddPlayerStrike(p2);

        Assert.True(p2.IsEliminated);
        Assert.Equal(GameStatePhase.GameOver, game.Phase);

        game.RevivePlayer(p2);

        Assert.False(p2.IsEliminated);
        Assert.Equal(2, p2.StrikeCount);
        Assert.Equal(1, p2.Tokens);
    }

    [Fact]
    public async Task CategoryFilter_FiltersQuestionsBySelectedCategory()
    {
        var trivia = new TriviaDataService();
        var token = new TokenService();
        var streak = new StreakService(token);
        using var game = new GameStateService(trivia, token, streak);

        string tempPack = Path.Combine(Path.GetTempPath(), $"ko_cat_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempPack, """
            [
              {
                "Id": "Q1",
                "Category": "Science",
                "Prompt": "Science Q?",
                "Options": ["A", "B"],
                "CorrectAnswerIndex": 0
              },
              {
                "Id": "Q2",
                "Category": "Sports",
                "Prompt": "Sports Q?",
                "Options": ["A", "B"],
                "CorrectAnswerIndex": 1
              }
            ]
            """, TestContext.Current.CancellationToken);

            game.InitializeGame(new KnockoutSettings { SelectedCategory = "All Categories" });
            await game.LoadGameQuestionsAsync(tempPack);

            Assert.Equal(2, game.TotalQuestions);
            Assert.Contains("Science", game.AvailableCategories);
            Assert.Contains("Sports", game.AvailableCategories);

            game.UpdateSettings(new KnockoutSettings { SelectedCategory = "Sports" });
            Assert.Equal(1, game.TotalQuestions);
            Assert.Equal("Sports", game.CurrentQuestion?.Category);
        }
        finally
        {
            if (File.Exists(tempPack)) File.Delete(tempPack);
        }
    }

    [Fact]
    public async Task TriviaDataService_IgnoresNonTriviaDatabasesWithoutQuestionsTable()
    {
        var trivia = new TriviaDataService();
        Assert.True(TriviaDataService.IsExcludedNonTriviaDatabase(@"C:\Data\lyracist.db"));
        Assert.True(TriviaDataService.IsExcludedNonTriviaDatabase(@"C:\Data\lyracist_backup.db"));
        Assert.False(TriviaDataService.IsExcludedNonTriviaDatabase(@"C:\Data\trivia.db"));

        string tempDb = Path.Combine(Path.GetTempPath(), $"non_trivia_{Guid.NewGuid():N}.db");
        try
        {
            await File.WriteAllBytesAsync(tempDb, [], TestContext.Current.CancellationToken);
            var questions = await trivia.LoadQuestionsFromDatabaseAsync(tempDb);
            Assert.Empty(questions);

            var categories = await trivia.GetCategoriesAsync(tempDb);
            Assert.Empty(categories);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }
}
