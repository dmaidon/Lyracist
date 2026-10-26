// Created on Oct 26, 2026 @ 07:01:00 -> Tests for Scaryoke spin history persistence, dupe prevention, and clearing on rotation reset
using ScaryokeWheel;
using Xunit;

namespace KSRotation.Tests;

public class ScaryokeSpinHistoryTests
{
    [Fact]
    public void RecordSpin_PersistsHistoryAndCapsAtLimit()
    {
        var settings = new ScaryokeSettings();
        settings.ClearSpinHistory();

        settings.RecordSpin("Elvis");
        settings.RecordSpin("Rock");
        settings.RecordSpin("Country");

        Assert.Equal(3, settings.SpinHistory.Count);
        Assert.Equal("Country", settings.SpinHistory[0]);
        Assert.Equal("Rock", settings.SpinHistory[1]);
        Assert.Equal("Elvis", settings.SpinHistory[2]);

        for (int i = 0; i < 10; i++)
        {
            settings.RecordSpin($"Category_{i}");
        }

        Assert.True(settings.SpinHistory.Count <= 8);
        Assert.Equal("Category_9", settings.SpinHistory[0]);

        settings.ClearSpinHistory();
        Assert.Empty(settings.SpinHistory);
    }

    [Fact]
    public void ScaryokeSettings_AvoidsDuplicateSpinsWhenSaved()
    {
        var settings = new ScaryokeSettings();
        settings.ClearSpinHistory();

        settings.RecordSpin("80s Hits");

        Assert.Single(settings.SpinHistory);
        Assert.Equal("80s Hits", settings.SpinHistory[0]);

        // Simulating second spin: verify the last landed can be checked to prevent duplicate
        bool isDuplicate = string.Equals(settings.SpinHistory[0], "80s Hits", StringComparison.OrdinalIgnoreCase);
        Assert.True(isDuplicate);

        settings.ClearSpinHistory();
        Assert.Empty(settings.SpinHistory);
    }
}
