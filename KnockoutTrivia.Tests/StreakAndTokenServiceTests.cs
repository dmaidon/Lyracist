// Created on Aug 29, 2026 @ 10:41:30 -> StreakService, TokenService, and KnockoutPlayer tests
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;
using Xunit;

namespace KnockoutTrivia.Tests;

public class StreakAndTokenServiceTests
{
    [Fact]
    public void TokenService_AwardToken_CapsAtMaxTokens()
    {
        var tokenService = new TokenService();
        var player = new KnockoutPlayer { Tokens = 2 };

        tokenService.AwardToken(player, maxTokens: 3);
        Assert.Equal(3, player.Tokens);

        // Trying to award past maxTokens (3) remains capped at 3
        tokenService.AwardToken(player, maxTokens: 3);
        Assert.Equal(3, player.Tokens);
    }

    [Fact]
    public void TokenService_DeductToken_FloorsAtZero()
    {
        var tokenService = new TokenService();
        var player = new KnockoutPlayer { Tokens = 1 };

        tokenService.DeductToken(player);
        Assert.Equal(0, player.Tokens);

        tokenService.DeductToken(player);
        Assert.Equal(0, player.Tokens);
    }

    [Fact]
    public void StreakService_AdvancesMultiplesOfFiveWithoutResettingTotalStreak()
    {
        var tokenService = new TokenService();
        var streakService = new StreakService(tokenService);
        var player = new KnockoutPlayer { Name = "Runner", Tokens = 0, StreakCount = 0 };

        int tokenRewards = 0;
        bool superStreakFired = false;

        streakService.TokenRewardEarned += (s, p) => tokenRewards++;
        streakService.SuperStreakReached += (s, p) => superStreakFired = true;

        // Answer 19 questions correctly
        for (int i = 1; i <= 19; i++)
        {
            streakService.RecordAnswer(player, true, streakRequirement: 5, superStreakThreshold: 20, maxTokens: 3);
            Assert.Equal(i, player.StreakCount);
        }

        Assert.Equal(19, player.StreakCount);
        Assert.Equal(3, tokenRewards); // Earned at 5, 10, 15
        Assert.False(superStreakFired);

        // 20th correct answer triggers Super Streak milestone
        streakService.RecordAnswer(player, true, streakRequirement: 5, superStreakThreshold: 20, maxTokens: 3);

        Assert.Equal(20, player.StreakCount);
        Assert.True(superStreakFired);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(5, 5)]
    [InlineData(6, 1)]
    [InlineData(9, 4)]
    [InlineData(10, 5)]
    [InlineData(15, 5)]
    [InlineData(20, 5)]
    public void KnockoutPlayer_StreakMeterProgress_CyclesCorrectly(int streakCount, int expectedMeter)
    {
        var player = new KnockoutPlayer { StreakCount = streakCount };
        Assert.Equal(expectedMeter, player.StreakMeterProgress);
    }

    [Fact]
    public void KnockoutPlayer_StatusText_And_StrikeColorHex_ReflectStrikes()
    {
        var player = new KnockoutPlayer { StrikeCount = 0 };
        Assert.Equal("ACTIVE", player.StatusText);
        Assert.Equal("#10B981", player.StrikeColorHex);

        player.StrikeCount = 1;
        Assert.Equal("1 STRIKE", player.StatusText);
        Assert.Equal("#EAB308", player.StrikeColorHex);

        player.StrikeCount = 2;
        Assert.Equal("DANGER", player.StatusText);
        Assert.Equal("#F97316", player.StrikeColorHex);

        player.StrikeCount = 3;
        Assert.Equal("KNOCKED OUT", player.StatusText);
        Assert.Equal("#EF4444", player.StrikeColorHex);
        Assert.True(player.IsEliminated);
    }
}
