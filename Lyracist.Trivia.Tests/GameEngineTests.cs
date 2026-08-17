// Edited on Aug 17, 2026 @ 15:43:30 -> Added PauseGame and ResumeGame unit tests
using System;
using System.Collections.Generic;
using Lyracist.Trivia.Core.Models;
using Lyracist.Trivia.Core.Services;
using Xunit;

namespace Lyracist.Trivia.Tests;

public class GameEngineTests
{
    private static TriviaRound CreateSampleRound()
    {
        return new TriviaRound
        {
            RoundNumber = 1,
            Title = "Pop Music",
            Questions =
            [
                new TriviaQuestion
                {
                    Id = "Q1",
                    Prompt = "Who sang 'Thriller'?",
                    Options = ["Prince", "Michael Jackson", "Madonna", "Stevie Wonder"],
                    CorrectAnswerIndex = 1,
                    TimeLimitSeconds = 15
                },
                new TriviaQuestion
                {
                    Id = "Q2",
                    Prompt = "What year did Woodstock take place?",
                    Options = ["1967", "1968", "1969", "1970"],
                    CorrectAnswerIndex = 2,
                    TimeLimitSeconds = 15
                }
            ]
        };
    }

    [Fact]
    public void RegisterPlayer_AddsPlayerWithUniqueName()
    {
        using var engine = new TriviaGameEngine();
        var player = engine.RegisterPlayer("Alice", "Team Rocket");

        Assert.Equal("Alice", player.Name);
        Assert.Equal("Team Rocket", player.TeamName);
        Assert.Single(engine.GetPlayers());
    }

    [Fact]
    public void StartGame_ResetsPlayerScoresAndSetsLobbyState()
    {
        using var engine = new TriviaGameEngine();
        var p = engine.RegisterPlayer("Bob");
        p.TotalScore = 500;

        engine.StartGame([CreateSampleRound()], "Test Game");

        Assert.Equal(TriviaGameState.Lobby, engine.State);
        Assert.Equal(0, p.TotalScore);
        Assert.NotNull(engine.CurrentSession.CurrentRound);
    }

    [Fact]
    public void StartCurrentQuestion_TransitionsToQuestionActiveAndSetsCountdown()
    {
        using var engine = new TriviaGameEngine();
        engine.StartGame([CreateSampleRound()]);
        engine.StartCurrentQuestion();

        Assert.Equal(TriviaGameState.QuestionActive, engine.State);
        Assert.Equal(15, engine.RemainingSeconds);
        Assert.Equal(15, engine.TotalCountdownSeconds);
    }

    [Fact]
    public void SubmitAnswer_CorrectAnswerAwardsPointsAndIncrementsStreak()
    {
        var settings = new TriviaSettings
        {
            BasePointsPerQuestion = 1000,
            SpeedBonusEnabled = false,
            StreakBonusMultiplier = 0.1
        };

        using var engine = new TriviaGameEngine(settings);
        var alice = engine.RegisterPlayer("Alice");
        var bob = engine.RegisterPlayer("Bob"); // Add Bob so auto-lock doesn't trigger immediately

        engine.StartGame([CreateSampleRound()]);
        engine.StartCurrentQuestion();

        // Alice answers correctly (Option index 1 = Michael Jackson)
        bool accepted = engine.SubmitAnswer("Alice", 1, 2500);

        Assert.True(accepted);
        Assert.True(alice.HasAnsweredCurrentQuestion);

        // Lock & Reveal
        engine.LockAndRevealAnswer();

        Assert.True(engine.State == TriviaGameState.EliminatingAnswers || engine.State == TriviaGameState.RevealAnswer);
        Assert.Equal(1100, alice.TotalScore); // 1000 base * 1.1 (streak 1)
        Assert.Equal(1, alice.CurrentStreak);
        Assert.Equal(1, alice.TotalCorrect);
    }

    [Fact]
    public void SubmitAnswer_IncorrectAnswerResetsStreakAndAwardsZero()
    {
        using var engine = new TriviaGameEngine();
        var bob = engine.RegisterPlayer("Bob");
        var alice = engine.RegisterPlayer("Alice");

        engine.StartGame([CreateSampleRound()]);
        bob.CurrentStreak = 3;
        bob.TotalScore = 2500;

        engine.StartCurrentQuestion();

        // Bob picks wrong option (0 instead of 1)
        engine.SubmitAnswer("Bob", 0, 1000);
        engine.LockAndRevealAnswer();

        Assert.Equal(2500, bob.TotalScore); // No points added
        Assert.Equal(0, bob.CurrentStreak); // Streak reset
    }

    [Fact]
    public void ProgressiveAnswerElimination_FadesWrongOptionsFirst()
    {
        using var engine = new TriviaGameEngine();
        engine.StartGame([CreateSampleRound()]);
        engine.StartCurrentQuestion();

        // Correct answer index is 1 (Michael Jackson). Options: 0 (Prince), 1 (MJ), 2 (Madonna), 3 (Stevie)
        engine.StartAnswerElimination();

        Assert.Equal(TriviaGameState.EliminatingAnswers, engine.State);
        Assert.Single(engine.EliminatedAnswerIndices);
        Assert.NotEqual(1, engine.EliminatedAnswerIndices[0]); // Correct answer is NOT eliminated
    }

    [Fact]
    public void AllPlayersAnswered_AutomaticallyLocksAndBeginsElimination()
    {
        using var engine = new TriviaGameEngine();
        var alice = engine.RegisterPlayer("Alice");
        engine.StartGame([CreateSampleRound()]);
        engine.StartCurrentQuestion();

        // Single active player answers
        engine.SubmitAnswer("Alice", 1, 1500);

        // State should automatically enter EliminatingAnswers
        Assert.Equal(TriviaGameState.EliminatingAnswers, engine.State);
        Assert.NotEmpty(engine.EliminatedAnswerIndices);
    }

    [Fact]
    public void AdvanceToNextQuestion_StepsThroughRoundsAndCompletes()
    {
        using var engine = new TriviaGameEngine();
        engine.StartGame([CreateSampleRound()]);

        engine.StartCurrentQuestion();
        Assert.Equal("Q1", engine.CurrentSession.CurrentQuestion?.Id);

        // Advance to Q2
        bool hasNext = engine.AdvanceToNextQuestion();
        Assert.True(hasNext);
        Assert.Equal("Q2", engine.CurrentSession.CurrentQuestion?.Id);

        // Advance past last question -> Game Complete
        bool more = engine.AdvanceToNextQuestion();
        Assert.False(more);
        Assert.Equal(TriviaGameState.GameComplete, engine.State);
    }

    [Fact]
    public void GetGameResult_DeterminesIndividualWinner_WhenNoTeams()
    {
        using var engine = new TriviaGameEngine();
        var alice = engine.RegisterPlayer("Alice");
        var bob = engine.RegisterPlayer("Bob");

        alice.TotalScore = 3200;
        bob.TotalScore = 2100;

        var result = engine.GetGameResult();

        Assert.False(result.HasTeamWinner);
        Assert.Equal("Alice", result.WinningName);
        Assert.Equal(3200, result.WinningScore);
        Assert.Contains("Alice", result.WinnerTitle);
        Assert.Contains("Alice", result.WinningTeamMembers);
    }

    [Fact]
    public void GetGameResult_DeterminesTeamWinnerAndListsMembers_WhenTeamsPresent()
    {
        using var engine = new TriviaGameEngine();
        var p1 = engine.RegisterPlayer("Alice", "Quizzards");
        var p2 = engine.RegisterPlayer("Bob", "Quizzards");
        var p3 = engine.RegisterPlayer("Charlie", "Rocket Science");

        p1.TotalScore = 2000;
        p2.TotalScore = 1500; // Quizzards total = 3500
        p3.TotalScore = 3000; // Rocket Science total = 3000

        var result = engine.GetGameResult();

        Assert.True(result.HasTeamWinner);
        Assert.Equal("Quizzards", result.WinningName);
        Assert.Equal(3500, result.WinningScore);
        Assert.Equal(2, result.WinningTeamMembers.Count);
        Assert.Contains("Alice", result.WinningTeamMembers);
        Assert.Contains("Bob", result.WinningTeamMembers);
        Assert.Contains("Quizzards", result.WinnerTitle);
        Assert.Contains("Alice, Bob", result.WinningTeamMembersRoster);
    }

    [Fact]
    public void AdvanceToNextQuestion_FiresGameCompletedEvent()
    {
        using var engine = new TriviaGameEngine();
        var alice = engine.RegisterPlayer("Alice", "Singers");
        alice.TotalScore = 1500;

        engine.StartGame([CreateSampleRound()]);
        engine.StartCurrentQuestion();
        engine.AdvanceToNextQuestion(); // Moves to Q2

        TriviaGameResult? completedResult = null;
        engine.GameCompleted += (s, r) => completedResult = r;

        engine.AdvanceToNextQuestion(); // Reaches end of game

        Assert.NotNull(completedResult);
        Assert.True(completedResult.HasTeamWinner);
        Assert.Equal("Singers", completedResult.WinningName);
    }

    [Fact]
    public void GameComplete_InitializesIntermissionCountdown_WhenAutoStartEnabled()
    {
        var settings = new TriviaSettings
        {
            AutoStartNextGameEnabled = true,
            NextGameDelayMinutes = 2
        };
        using var engine = new TriviaGameEngine(settings);
        engine.RegisterPlayer("Alice");
        engine.StartGame([CreateSampleRound()]);
        engine.StartCurrentQuestion();
        engine.AdvanceToNextQuestion(); // to Q2
        engine.AdvanceToNextQuestion(); // completes game

        Assert.Equal(TriviaGameState.GameComplete, engine.State);
        Assert.True(engine.IsInIntermission);
        Assert.Equal(120, engine.IntermissionSecondsRemaining);
    }

    [Fact]
    public void SkipIntermission_ResetsIntermissionAndFiresCompletionEvent()
    {
        var settings = new TriviaSettings
        {
            AutoStartNextGameEnabled = true,
            NextGameDelayMinutes = 5
        };
        using var engine = new TriviaGameEngine(settings);
        engine.RegisterPlayer("Alice");
        engine.StartGame([CreateSampleRound()]);
        engine.StartCurrentQuestion();
        engine.AdvanceToNextQuestion();
        engine.AdvanceToNextQuestion();

        Assert.True(engine.IsInIntermission);

        bool completedFired = false;
        engine.IntermissionCompleted += (s, e) => completedFired = true;

        engine.SkipIntermission();

        Assert.False(engine.IsInIntermission);
        Assert.Equal(0, engine.IntermissionSecondsRemaining);
        Assert.True(completedFired);
    }

    [Fact]
    public void TriviaSettings_SupportsWifiAndPreGameCountdownDefaults()
    {
        var settings = new TriviaSettings
        {
            WifiSsid = "VenueGuestWifi",
            WifiPassword = "TriviaPassword123",
            PreGameCountdownMinutes = 5,
            AutoStartAfterCountdown = true
        };

        Assert.Equal("VenueGuestWifi", settings.WifiSsid);
        Assert.Equal("TriviaPassword123", settings.WifiPassword);
        Assert.Equal(5, settings.PreGameCountdownMinutes);
        Assert.True(settings.AutoStartAfterCountdown);
    }

    [Fact]
    public void PauseGame_FreezesStateAndBlocksSubmissions()
    {
        using var engine = new TriviaGameEngine();
        engine.RegisterPlayer("Alice");
        engine.StartGame([CreateSampleRound()]);
        engine.StartCurrentQuestion();

        Assert.Equal(TriviaGameState.QuestionActive, engine.State);
        Assert.False(engine.IsPaused);

        bool pausedFired = false;
        string? capturedReason = null;
        engine.GamePaused += (s, reason) =>
        {
            pausedFired = true;
            capturedReason = reason;
        };

        engine.PauseGame("DJ Banner Active");

        Assert.True(engine.IsPaused);
        Assert.Equal("DJ Banner Active", engine.PauseReason);
        Assert.True(pausedFired);
        Assert.Equal("DJ Banner Active", capturedReason);

        // Submissions must be blocked while paused
        bool submitted = engine.SubmitAnswer("Alice", 1);
        Assert.False(submitted);

        bool resumedFired = false;
        engine.GameResumed += (s, e) => resumedFired = true;

        engine.ResumeGame();

        Assert.False(engine.IsPaused);
        Assert.Null(engine.PauseReason);
        Assert.True(resumedFired);

        // Submissions can proceed after resume
        bool submittedAfterResume = engine.SubmitAnswer("Alice", 1);
        Assert.True(submittedAfterResume);
    }
}
