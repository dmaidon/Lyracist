// Created on Aug 29, 2026 @ 10:43:00 -> WheelService and BannerService unit tests
using System.Collections.Generic;
using System.Linq;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;
using Xunit;

namespace KnockoutTrivia.Tests;

public class WheelAndBannerServiceTests
{
    [Fact]
    public void WheelService_BuildWheelSegments_IncludesOnlyTokenHoldersExcludingSuperStreakPlayer()
    {
        var wheelService = new WheelService();

        var superStreakPlayer = new KnockoutPlayer { Name = "SuperStar", Tokens = 2 };
        var opponent1 = new KnockoutPlayer { Name = "Opponent1", Tokens = 1 };
        var opponent2 = new KnockoutPlayer { Name = "Opponent2", Tokens = 3 };
        var zeroTokenOpponent = new KnockoutPlayer { Name = "ZeroTokens", Tokens = 0 };
        var eliminatedOpponent = new KnockoutPlayer { Name = "Eliminated", Tokens = 1, StrikeCount = 3 };

        var allPlayers = new List<KnockoutPlayer>
        {
            superStreakPlayer,
            opponent1,
            opponent2,
            zeroTokenOpponent,
            eliminatedOpponent
        };

        var segments = wheelService.BuildWheelSegments(allPlayers, superStreakPlayer);

        Assert.Equal(2, segments.Count);
        Assert.Contains(segments, s => s.PlayerName == "Opponent1");
        Assert.Contains(segments, s => s.PlayerName == "Opponent2");
        Assert.DoesNotContain(segments, s => s.PlayerName == "SuperStar");
        Assert.DoesNotContain(segments, s => s.PlayerName == "ZeroTokens");
        Assert.DoesNotContain(segments, s => s.PlayerName == "Eliminated");
    }

    [Fact]
    public void WheelService_BuildWheelSegments_FallsBackToBonusWhenNoTokenHolders()
    {
        var wheelService = new WheelService();
        var superStreakPlayer = new KnockoutPlayer { Name = "SoloHero", Tokens = 3 };
        var opponent = new KnockoutPlayer { Name = "BrokeOpponent", Tokens = 0 };

        var segments = wheelService.BuildWheelSegments(new[] { superStreakPlayer, opponent }, superStreakPlayer);

        Assert.Single(segments);
        Assert.Contains("BONUS REWARD", segments[0].PlayerName);
    }

    [Fact]
    public void BannerService_CreatesFormattedBanners()
    {
        var bannerService = new BannerService();

        var intro = bannerService.CreateIntroBanner();
        Assert.Equal(BannerType.Intro, intro.Type);
        Assert.Equal("KNOCKOUT TRIVIA", intro.Title);

        var winner = bannerService.CreateWinnerBanner(new KnockoutPlayer { Name = "Champion", Score = 150 });
        Assert.Equal(BannerType.Winner, winner.Type);
        Assert.Equal("Champion", winner.WinnerName);
        Assert.Equal(150, winner.FinalScore);

        var super = bannerService.CreateSuperStreakBanner(new KnockoutPlayer { Name = "Streaker" });
        Assert.Equal(BannerType.SuperStreak, super.Type);
    }
}
