// Edited on Aug 6, 2026 @ 07:01:27 -> Fix test to use the corrected XP-progress threshold formula (25*(l-1)*l, not the buggy 50*(l-1)*l)
using Lyracist.Core.Helpers;

namespace Lyracist.Tests;

public class SingerXpHelperTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 100)]
    [InlineData(3, 50, 350)]
    public void CalculateXP_MatchesFormula(int totalSongsSung, int score, int expected)
    {
        Assert.Equal(expected, SingerXpHelper.CalculateXP(totalSongsSung, score));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    [InlineData(1, 1)]
    public void CalculateLevel_NonPositiveOrLowXp_ReturnsLevelOne(int xp, int expectedLevel)
    {
        Assert.Equal(expectedLevel, SingerXpHelper.CalculateLevel(xp));
    }

    [Fact]
    public void CalculateLevel_IncreasesMonotonicallyWithXp()
    {
        int previousLevel = SingerXpHelper.CalculateLevel(0);
        for (int xp = 0; xp <= 5000; xp += 50)
        {
            int level = SingerXpHelper.CalculateLevel(xp);
            Assert.True(level >= previousLevel, $"Level dropped from {previousLevel} to {level} at xp={xp}");
            previousLevel = level;
        }
    }

    [Fact]
    public void CalculateXPProgress_AtLevelFloor_IsZero()
    {
        int level = 3;
        int xpForCurrent = 25 * (level - 1) * level;

        // Sanity-check that this xp value is actually where CalculateLevel starts returning `level`.
        Assert.Equal(level, SingerXpHelper.CalculateLevel(xpForCurrent));

        double progress = SingerXpHelper.CalculateXPProgress(xpForCurrent, level);

        Assert.Equal(0, progress, precision: 3);
    }

    [Fact]
    public void CalculateXPProgress_ClampsWithinZeroToHundred()
    {
        // Way below the level's floor and way above its ceiling should both clamp, not go negative/over 100.
        Assert.Equal(0, SingerXpHelper.CalculateXPProgress(0, 5), precision: 3);
        Assert.Equal(100, SingerXpHelper.CalculateXPProgress(1_000_000, 1), precision: 3);
    }

    [Theory]
    [InlineData(1, "Shower Singer")]
    [InlineData(9, "Karaoke Champion")]
    [InlineData(10, "Karaoke Legend 👑")]
    [InlineData(999, "Karaoke Legend 👑")]
    public void GetLevelName_ReturnsExpectedTitle(int level, string expected)
    {
        Assert.Equal(expected, SingerXpHelper.GetLevelName(level));
    }

    [Fact]
    public void GetBadges_FreshSinger_HasNoBadges()
    {
        var badges = SingerXpHelper.GetBadges(totalSongsSung: 0, score: 0, averageRating: 0, ratingCount: 0);

        Assert.Empty(badges);
    }

    [Fact]
    public void GetBadges_MeetsAllThresholds_IncludesEveryBadge()
    {
        var badges = SingerXpHelper.GetBadges(totalSongsSung: 10, score: 200, averageRating: 4.5, ratingCount: 3);

        Assert.Contains("🎤 Debut", badges);
        Assert.Contains("🔥 Rising Star", badges);
        Assert.Contains("👑 Legend", badges);
        Assert.Contains("⭐ Crowd Pleaser", badges);
        Assert.Contains("🎯 High Scorer", badges);
    }

    [Fact]
    public void GetBadges_HighRatingButTooFewRatings_OmitsCrowdPleaser()
    {
        var badges = SingerXpHelper.GetBadges(totalSongsSung: 1, score: 0, averageRating: 5.0, ratingCount: 2);

        Assert.DoesNotContain("⭐ Crowd Pleaser", badges);
    }
}

public class NameFormattingTests
{
    [Theory]
    [InlineData("dennis maidon", "Dennis Maidon")]
    [InlineData("  DENNIS   MAIDON  ", "  Dennis   Maidon  ")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void ProperCase_FormatsAsExpected(string? input, string expected)
    {
        Assert.Equal(expected, NameFormatting.ProperCase(input));
    }

    [Fact]
    public void ProperCase_PreservesNoneRegardlessOfCasing()
    {
        Assert.Equal("None", NameFormatting.ProperCase("NONE"));
        Assert.Equal("None", NameFormatting.ProperCase("none"));
    }
}
