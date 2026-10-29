// Edited on Oct 29, 2026 @ 05:40:00 -> Add tests for shared HolidayThemes and DisplayPreferences.HolidayTheme in Lyracist
using KSRotation.Models;
using Lyracist.Core.Helpers;
using Lyracist.Services.Display;
using Xunit;

namespace Lyracist.Tests;

public class ScaryokeHistoryTests
{
    [Fact]
    public void HolidayThemes_SharedIntoLyracist_ResolvesAndPersistsInDisplayPreferences()
    {
        var prefs = new DisplayPreferences();
        Assert.Equal(HolidayThemes.None, prefs.HolidayTheme);

        prefs.HolidayTheme = "Halloween";
        var resolved = HolidayThemes.Resolve(prefs.HolidayTheme, new DateTime(2026, 6, 15));
        Assert.NotNull(resolved);
        Assert.Equal("Halloween", resolved.Key);

        var laborDay = HolidayThemes.ForDate(new DateTime(2026, 9, 7));
        Assert.NotNull(laborDay);
        Assert.Equal("Labor", laborDay.Key);

        var veteransDay = HolidayThemes.ForDate(new DateTime(2026, 11, 11));
        Assert.NotNull(veteransDay);
        Assert.Equal("Veterans", veteransDay.Key);
    }

    [Fact]
    public void AppSettings_RecordScaryokeSpin_PersistsAndCapsAtEleven()
    {
        AppSettings.ClearScaryokeSpinHistory();

        AppSettings.RecordScaryokeSpin("Country");
        AppSettings.RecordScaryokeSpin("Rock");

        var history = AppSettings.ScaryokeSpinHistory;
        Assert.Equal(2, history.Count);
        Assert.Equal("Rock", history[0]);
        Assert.Equal("Country", history[1]);

        for (int i = 0; i < 15; i++)
        {
            AppSettings.RecordScaryokeSpin($"Cat_{i}");
        }

        history = AppSettings.ScaryokeSpinHistory;
        Assert.True(history.Count <= 11);
        Assert.Equal("Cat_14", history[0]);

        AppSettings.ClearScaryokeSpinHistory();
        Assert.Empty(AppSettings.ScaryokeSpinHistory);
    }

    [Fact]
    public void AppSettings_ScaryokeToggles_CanBeSetAndPersisted()
    {
        AppSettings.ScaryokeShowSingersChoice = true;
        Assert.True(AppSettings.ScaryokeShowSingersChoice);

        AppSettings.ScaryokeShowSingersChoice = false;
        Assert.False(AppSettings.ScaryokeShowSingersChoice);

        AppSettings.ScaryokeShowDjsChoice = false;
        Assert.False(AppSettings.ScaryokeShowDjsChoice);

        AppSettings.ScaryokeAvoidRepeats = false;
        Assert.False(AppSettings.ScaryokeAvoidRepeats);

        // Reset back to defaults
        AppSettings.ScaryokeShowSingersChoice = true;
        AppSettings.ScaryokeShowDjsChoice = true;
        AppSettings.ScaryokeAvoidRepeats = true;
    }

    [Fact]
    public void AppSettings_ScaryokeOverlayMonitor_CanBeSetAndPersisted()
    {
        AppSettings.ScaryokeOverlayMonitor = @"\\.\DISPLAY2";
        Assert.Equal(@"\\.\DISPLAY2", AppSettings.ScaryokeOverlayMonitor);

        AppSettings.ScaryokeOverlayMonitor = "LYRICS";
        Assert.Equal("LYRICS", AppSettings.ScaryokeOverlayMonitor);

        AppSettings.ScaryokeOverlayMonitor = string.Empty;
        Assert.Equal(string.Empty, AppSettings.ScaryokeOverlayMonitor);
    }

    [Fact]
    public void AppSettings_ScaryokeCategories_EnforcesMaxElevenCategories()
    {
        // Add categories up to 11
        for (int i = 0; i < 15; i++)
        {
            AppSettings.AddScaryokeCategory($"TestCat_{i}");
        }

        Assert.True(AppSettings.ScaryokeCategories.Count <= 11);
    }

    [Fact]
    public void ScaryokeViewModel_RemembersSpins_AndClearsOnRotationClear()
    {
        AppSettings.ClearScaryokeSpinHistory();

        var display = new Moq.Mock<Lyracist.Services.Display.IDisplayService>();
        var mediaEngine = new Moq.Mock<Lyracist.Core.Interfaces.IMediaEngine>();
        var rotation = new Lyracist.ViewModels.RotationViewModel(display.Object, mediaEngine.Object);

        // Preload history in settings as if from previous run
        AppSettings.RecordScaryokeSpin("Country");

        var scaryoke = new Lyracist.ViewModels.ScaryokeViewModel(display.Object, rotation);
        scaryoke.AvoidRepeats = true;

        // Duplicate check should immediately kick in based on loaded history
        Assert.True(scaryoke.IsRecentRepeat("Country"));
        Assert.True(scaryoke.IsRecentRepeat("COUNTRY"));
        Assert.False(scaryoke.IsRecentRepeat("Rock"));

        // Simulate new singer spinning "Rock"
        scaryoke.ApplyResult("Rock");
        Assert.True(scaryoke.IsRecentRepeat("Rock"));
        Assert.Equal("Rock", AppSettings.ScaryokeSpinHistory[0]);

        // When rotation is cleared, Scaryoke history must be cleared
        rotation.ClearRotationCommand.Execute(null);

        Assert.Empty(AppSettings.ScaryokeSpinHistory);
        Assert.False(scaryoke.IsRecentRepeat("Rock"));
        Assert.False(scaryoke.IsRecentRepeat("Country"));
    }
}
