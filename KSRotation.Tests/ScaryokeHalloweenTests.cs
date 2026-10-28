// Created on Oct 28, 2026 @ 10:00:00 -> Unit tests for Halloween curses, fortunes, preset, and curse clearing on finish
using System.Linq;
using ScaryokeWheel;
using Xunit;

namespace KSRotation.Tests;

public class ScaryokeHalloweenTests
{
    [Fact]
    public void RandomCurse_AlwaysReturnsAKnownCurse()
    {
        for (int i = 0; i < 200; i++)
        {
            Assert.Contains(ScaryokeFlavor.RandomCurse(), ScaryokeFlavor.Curses);
        }
    }

    [Theory]
    [InlineData("Monster voice", true)]
    [InlineData("No curse - you're spared!", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRealCurse_ExcludesSparedAndBlank(string? curse, bool expected)
    {
        Assert.Equal(expected, ScaryokeFlavor.IsRealCurse(curse));
    }

    [Theory]
    [InlineData("Thriller")]
    [InlineData("DJ's Choice")]
    [InlineData("Singer's Choice")]
    public void Fortune_MentionsSingerAndIsNeverBlank(string category)
    {
        string fortune = ScaryokeFlavor.Fortune(category, "Dave");
        Assert.False(string.IsNullOrWhiteSpace(fortune));
        Assert.Contains("Dave", fortune);
    }

    [Fact]
    public void Fortune_FallsBackWhenNoSingerName()
    {
        Assert.Contains("the singer", ScaryokeFlavor.Fortune("Thriller", null).Replace("The ", "the "), System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureHalloweenPreset_AddsOnce_AndRespectsCategoryLimit()
    {
        var settings = new ScaryokeSettings();
        Assert.True(ScaryokeFlavor.EnsureHalloweenPreset(settings));
        Assert.False(ScaryokeFlavor.EnsureHalloweenPreset(settings));

        var preset = Assert.Single(settings.Presets);
        Assert.Equal(ScaryokeFlavor.HalloweenPresetName, preset.Name);
        Assert.True(preset.Categories.Count <= ScaryokeSettings.MaxCategories);
        Assert.DoesNotContain(preset.Categories, c => ScaryokeSettings.ReservedNames.Contains(c));
        Assert.All(preset.Styles.Keys, k => Assert.Contains(k, preset.Categories));
    }

    [Fact]
    public void FinishingASinger_ClearsTheirCurse()
    {
        var finished = new KSRotation.Models.SingerEntry { Name = "A", ScaryokeCategory = "Thriller", ScaryokeColor = "#B71C1C", ScaryokeCurse = "Monster voice" };
        var other = new KSRotation.Models.SingerEntry { Name = "B" };
        var list = new System.Collections.Generic.List<KSRotation.Models.SingerEntry> { finished, other };

        Lyracist.Shared.RotationHelpers.AdvanceRotationAfterFinished(list, finished, floatCurrentToTop: false);

        Assert.Null(finished.ScaryokeCurse);
        Assert.Null(finished.ScaryokeCategory);
    }
}
