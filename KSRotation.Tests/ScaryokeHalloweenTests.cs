// Edited on Oct 29, 2026 @ 04:05:30 -> Add Labor Day and Veterans Day Auto date tests
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

public class HolidayThemeTests
{
    [Theory]
    [InlineData("🦇", "1f987")]
    [InlineData("🕷️", "1f577")]
    [InlineData("🇺🇸", "1f1fa-1f1f8")]
    public void EmojiFileKey_MatchesTwemojiNaming(string glyph, string expected) =>
        Assert.Equal(expected, KSRotation.Models.HolidayThemes.EmojiFileKey(glyph));

    [Fact]
    public void EveryThemeGlyph_HasABundledImage()
    {
        var dir = AppContext.BaseDirectory;
        string? emojiDir = null;
        while (dir != null && emojiDir == null)
        {
            var candidate = Path.Combine(dir, "KSRotation", "Resources", "Emoji");
            if (Directory.Exists(candidate)) emojiDir = candidate;
            dir = Path.GetDirectoryName(dir);
        }
        Assert.NotNull(emojiDir);

        var missing = KSRotation.Models.HolidayThemes.All
            .SelectMany(t => t.Crossers.Concat(t.Ambient))
            .Distinct()
            .Where(g => !File.Exists(Path.Combine(emojiDir!, KSRotation.Models.HolidayThemes.EmojiFileKey(g) + ".png")))
            .ToList();
        Assert.Empty(missing);

        // Every shipped image must actually decode in WPF
        var undecodable = Directory.GetFiles(emojiDir!, "*.png")
            .Where(f =>
            {
                try
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.UriSource = new System.Uri(f);
                    bmp.EndInit();
                    return bmp.PixelWidth <= 0;
                }
                catch { return true; }
            })
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(undecodable);
    }

    [Theory]
    [InlineData(2026, 10, 28, "Halloween")]
    [InlineData(2026, 11, 26, "Thanksgiving")]   // 4th Thursday of Nov 2026
    [InlineData(2026, 12, 20, "Christmas")]
    [InlineData(2026, 12, 31, "NewYears")]
    [InlineData(2027, 1, 1, "NewYears")]
    [InlineData(2027, 2, 14, "Valentines")]
    [InlineData(2027, 3, 17, "StPatricks")]
    [InlineData(2026, 11, 1, "Birthday")]
    [InlineData(2027, 3, 28, "Easter")]           // Easter Sunday 2027
    [InlineData(2027, 3, 29, "Easter")]           // Easter Monday 2027
    [InlineData(2027, 5, 9, "Mothers")]           // 2nd Sunday of May 2027
    [InlineData(2027, 6, 20, "Fathers")]          // 3rd Sunday of June 2027
    [InlineData(2027, 7, 4, "Independence")]
    [InlineData(2027, 5, 31, "Memorial")]         // last Monday of May 2027
    [InlineData(2026, 9, 7, "Labor")]             // 1st Monday of Sept 2026
    [InlineData(2026, 11, 11, "Veterans")]        // Veterans Day
    public void ForDate_PicksTheHolidayInSeason(int y, int m, int d, string expected)
    {
        Assert.Equal(expected, KSRotation.Models.HolidayThemes.ForDate(new System.DateTime(y, m, d))?.Key);
    }

    [Fact]
    public void ForDate_ReturnsNullOutOfSeason()
    {
        Assert.Null(KSRotation.Models.HolidayThemes.ForDate(new System.DateTime(2026, 9, 15)));
    }

    [Fact]
    public void Resolve_HandlesOffAutoAndExplicitChoices()
    {
        var today = new System.DateTime(2026, 10, 30);
        Assert.Null(KSRotation.Models.HolidayThemes.Resolve("None", today));
        Assert.Equal("Halloween", KSRotation.Models.HolidayThemes.Resolve("Auto", today)?.Key);
        Assert.Equal("Christmas", KSRotation.Models.HolidayThemes.Resolve("Christmas", today)?.Key);
        Assert.Null(KSRotation.Models.HolidayThemes.Resolve("bogus", today));
    }

    [Fact]
    public void Resolve_Auto_KeepsEveningShowThemePastMidnight()
    {
        // A Halloween show at 1:30 AM on Nov 1 still counts as the Oct 31 show night
        var halloweenAfterMidnight = new System.DateTime(2026, 11, 1, 1, 30, 0);
        Assert.Equal(new System.DateTime(2026, 10, 31), KSRotation.Models.HolidayThemes.ShowDate(halloweenAfterMidnight));
        Assert.Equal("Halloween", KSRotation.Models.HolidayThemes.Resolve("Auto", halloweenAfterMidnight)?.Key);

        // By 6:00 AM on Nov 1 (or any daytime/evening hour on Nov 1), it resolves to Brenda's Birthday
        var birthdayDaytime = new System.DateTime(2026, 11, 1, 12, 0, 0);
        Assert.Equal("Birthday", KSRotation.Models.HolidayThemes.Resolve("Auto", birthdayDaytime)?.Key);

        // And Brenda's Birthday party running at 1:15 AM on Nov 2 stays on Birthday until 6:00 AM
        var birthdayAfterMidnight = new System.DateTime(2026, 11, 2, 1, 15, 0);
        Assert.Equal("Birthday", KSRotation.Models.HolidayThemes.Resolve("Auto", birthdayAfterMidnight)?.Key);
    }

    [Fact]
    public void EveryTheme_HasValidColorsAndGlyphs()
    {
        foreach (var t in KSRotation.Models.HolidayThemes.All)
        {
            Assert.Matches("^#[0-9A-Fa-f]{8}$", t.VignetteColor);
            Assert.Matches("^[0-9A-Fa-f]{6}$", t.FogColor);
            Assert.NotEmpty(t.Crossers);
            Assert.NotEmpty(t.Palette);
            Assert.All(t.Palette, c => Assert.Matches("^#[0-9A-Fa-f]{6}$", c));
            Assert.True(t.CrossSeconds > 0 && t.AmbientSeconds > 0);
        }
        Assert.True(KSRotation.Models.HolidayThemes.Find("Halloween")!.Crossers.Length >= 10);
    }
}
