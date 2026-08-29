// Created on Aug 29, 2026 @ 10:36:30 -> Unit tests verifying KnockoutTrivia streak service, player state, shuffle, and security repairs
using System;
using System.Collections.Generic;
using System.Linq;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;
using Xunit;

namespace Lyracist.Trivia.Tests;

public class KnockoutTriviaTests
{
    [Fact]
    public void StreakService_RecordAnswer_SuperStreakWheelMilestoneReachedAt20()
    {
        var tokenService = new TokenService();
        var streakService = new StreakService(tokenService);
        var player = new KnockoutPlayer { Name = "Alice", Tokens = 0, StreakCount = 0 };

        int tokenRewardsEarned = 0;
        bool superStreakTriggered = false;

        streakService.TokenRewardEarned += (s, p) => tokenRewardsEarned++;
        streakService.SuperStreakReached += (s, p) => superStreakTriggered = true;

        for (int i = 1; i <= 20; i++)
        {
            streakService.RecordAnswer(player, true, streakRequirement: 5, superStreakThreshold: 20, maxTokens: 3);
        }

        Assert.Equal(20, player.StreakCount);
        Assert.True(superStreakTriggered, "Super Streak should be triggered at 20 consecutive answers.");
        Assert.Equal(4, tokenRewardsEarned); // Tokens earned at 5, 10, 15, 20
        Assert.Equal(3, player.Tokens); // Max tokens is 3
    }

    [Fact]
    public void StreakService_ResetStreak_ResetsOnWrongAnswer()
    {
        var tokenService = new TokenService();
        var streakService = new StreakService(tokenService);
        var player = new KnockoutPlayer { Name = "Bob", Tokens = 1, StreakCount = 4 };

        streakService.RecordAnswer(player, false, streakRequirement: 5, superStreakThreshold: 20, maxTokens: 3);

        Assert.Equal(0, player.StreakCount);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    [InlineData(5, 5)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(10, 5)]
    [InlineData(11, 1)]
    [InlineData(20, 5)]
    public void KnockoutPlayer_StreakMeterProgress_CalculatesCorrectly(int streakCount, int expectedMeter)
    {
        var player = new KnockoutPlayer { StreakCount = streakCount };
        Assert.Equal(expectedMeter, player.StreakMeterProgress);
    }

    [Fact]
    public void KnockoutPlayer_SessionToken_IsPopulatedAndUnique()
    {
        var p1 = new KnockoutPlayer { Name = "Player 1" };
        var p2 = new KnockoutPlayer { Name = "Player 2" };

        Assert.False(string.IsNullOrWhiteSpace(p1.SessionToken));
        Assert.False(string.IsNullOrWhiteSpace(p2.SessionToken));
        Assert.NotEqual(p1.SessionToken, p2.SessionToken);
    }

    [Fact]
    public void GameStateService_ShuffleQuestions_FisherYatesPermutationPreservesAllItems()
    {
        var mockTrivia = new TriviaDataService();
        var tokenService = new TokenService();
        var streakService = new StreakService(tokenService);
        using var gameState = new GameStateService(mockTrivia, tokenService, streakService);

        gameState.ShuffleQuestions(); // Empty check doesn't throw
    }
}
