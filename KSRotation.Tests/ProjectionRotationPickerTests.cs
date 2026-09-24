// Created on Sep 24, 2026 @ 12:30:00 -> Unit tests for the shared ProjectionRotationPicker
using Lyracist.Shared;

namespace KSRotation.Tests;

public class ProjectionRotationPickerTests
{
    private static List<ProjectionRotationEntry> Views(params string[] names) =>
        [.. names.Select(n => new ProjectionRotationEntry { ViewName = n, IsEnabled = true })];

    [Fact]
    public void Pick_NothingEnabled_ReturnsNull()
    {
        Assert.Null(ProjectionRotationPicker.Pick([], "Jukebox", hasSingers: true));
    }

    [Fact]
    public void Pick_NeverRepeatsCurrentViewWhenThereIsAChoice()
    {
        var enabled = Views("Jukebox", "Disco Ball");
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal("Disco Ball", ProjectionRotationPicker.Pick(enabled, "Jukebox", hasSingers: true)?.ViewName);
        }
    }

    [Fact]
    public void Pick_SingleView_ReturnsItEvenIfCurrent()
    {
        Assert.Equal("Jukebox", ProjectionRotationPicker.Pick(Views("Jukebox"), "Jukebox", hasSingers: true)?.ViewName);
    }

    [Fact]
    public void Pick_EmptyQueue_SkipsViewsThatNeedSingers()
    {
        var enabled = Views("Star Wars Crawl", "Jukebox");
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal("Jukebox", ProjectionRotationPicker.Pick(enabled, null, hasSingers: false)?.ViewName);
        }
    }

    [Fact]
    public void Pick_EmptyQueue_OnlyCrawlEnabled_ReturnsNull()
    {
        Assert.Null(ProjectionRotationPicker.Pick(Views("Star Wars Crawl"), null, hasSingers: false));
    }

    [Fact]
    public void Pick_WithSingers_CrawlIsEligible()
    {
        var enabled = Views("Star Wars Crawl", "Jukebox");
        Assert.Equal("Star Wars Crawl", ProjectionRotationPicker.Pick(enabled, "Jukebox", hasSingers: true)?.ViewName);
    }

    [Fact]
    public void NeedsSingers_OnlyTheCrawl()
    {
        Assert.True(ProjectionRotationPicker.NeedsSingers("Star Wars Crawl"));
        Assert.False(ProjectionRotationPicker.NeedsSingers("Casino Slot Reels"));
        Assert.False(ProjectionRotationPicker.NeedsSingers(null));
    }
}
