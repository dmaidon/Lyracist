// Edited on Aug 27, 2026 @ 07:07:00 -> Add comprehensive tests for mixed-case, prefix apostrophe, Mc, and acronym proper-casing
using Lyracist.Core.Helpers;
using Lyracist.Shared;

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
    [InlineData("DENNIS MAIDON", "Dennis Maidon")]
    [InlineData("  DENNIS   MAIDON  ", "  Dennis   Maidon  ")]
    [InlineData("DeaR", "DeaR")]
    [InlineData("deaR", "DeaR")]
    [InlineData("dear", "Dear")]
    [InlineData("DEAR", "Dear")]
    [InlineData("O'Neal", "O'Neal")]
    [InlineData("O'neal", "O'Neal")]
    [InlineData("o'neal", "O'Neal")]
    [InlineData("O'NEAL", "O'Neal")]
    [InlineData("D'Angelo", "D'Angelo")]
    [InlineData("d'angelo", "D'Angelo")]
    [InlineData("D'ANGELO", "D'Angelo")]
    [InlineData("L'Amour", "L'Amour")]
    [InlineData("l'amour", "L'Amour")]
    [InlineData("McDonald", "McDonald")]
    [InlineData("mcdonald", "McDonald")]
    [InlineData("MCDONALD", "McDonald")]
    [InlineData("McCartney", "McCartney")]
    [InlineData("mccartney", "McCartney")]
    [InlineData("Mary-Ann Smith", "Mary-Ann Smith")]
    [InlineData("mary-ann smith", "Mary-Ann Smith")]
    [InlineData("MARY-ANN SMITH", "Mary-Ann Smith")]
    [InlineData("Smith-O'Neal", "Smith-O'Neal")]
    [InlineData("smith-o'neal", "Smith-O'Neal")]
    [InlineData("Don't Stop Believin'", "Don't Stop Believin'")]
    [InlineData("don't stop believin'", "Don't Stop Believin'")]
    [InlineData("DON'T STOP BELIEVIN'", "Don't Stop Believin'")]
    [InlineData("DJ Khaled", "DJ Khaled")]
    [InlineData("dj khaled", "DJ Khaled")]
    [InlineData("MC Hammer", "MC Hammer")]
    [InlineData("mc hammer", "MC Hammer")]
    [InlineData("Henry VIII", "Henry VIII")]
    [InlineData("henry viii", "Henry VIII")]
    [InlineData("John Doe Jr.", "John Doe Jr.")]
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
        Assert.Equal("None", NameFormatting.ProperCase("None"));
    }
}

