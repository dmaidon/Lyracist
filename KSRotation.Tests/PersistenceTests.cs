// Edited on Aug 25, 2026 @ 06:39:00 -> Fix xUnit2033 return values of Assert.Single
using KSRotation.Models;
using KSRotation.Services;

namespace KSRotation.Tests;

// All three services persist into AppContext.BaseDirectory/Settings using fixed file names, so they share one
// (non-parallel) test class to avoid cross-test file contention.
[Collection("Persistence")]
public class PersistenceTests
{
    [Fact]
    public void StringListStore_RoundTripsItems()
    {
        string fileName = $"test_list_{Guid.NewGuid():N}.json";
        var items = new List<string> { "Charlie", "alice", "Bob" };

        StringListStore.Save(items, fileName);
        List<string> loaded = StringListStore.Load(fileName, "fallback");

        Assert.Equal(items, loaded);
    }

    [Fact]
    public void StringListStore_ReturnsDefaultWhenFileMissing()
    {
        string fileName = $"missing_{Guid.NewGuid():N}.json";

        List<string> loaded = StringListStore.Load(fileName, "fallback");

        Assert.Equal(["fallback"], loaded);
    }

    [Fact]
    public void SettingsService_RoundTripsValues()
    {
        var settings = new AppSettings
        {
            VenueName = "Test Venue",
            DjName = "DJ Test",
            Theme = "Dark",
            MarqueeSpeed = 123,
            EmailRecipient = "a@b.com",
            WatermarkOpacity = 0.5,
        };

        SettingsService.Save(settings);
        AppSettings loaded = SettingsService.Load();

        Assert.Equal("Test Venue", loaded.VenueName);
        Assert.Equal("DJ Test", loaded.DjName);
        Assert.Equal("Dark", loaded.Theme);
        Assert.Equal(123, loaded.MarqueeSpeed);
        Assert.Equal("a@b.com", loaded.EmailRecipient);
        Assert.Equal(0.5, loaded.WatermarkOpacity);
    }

    [Fact]
    public void NightDatabase_RoundTripsAndFlushes()
    {
        var singer = new SingerEntry { Name = "Alice", Song = "Hey Jude", Artist = "The Beatles" };
        var history = new List<SongPerformance>
        {
            new() { SingerId = singer.Id, SingerName = "Alice", Round = 1, SongTitle = "Hey Jude", ArtistName = "The Beatles", Timestamp = DateTime.Now }
        };

        NightDatabaseService.Save([singer], history);
        NightDbState loaded = NightDatabaseService.Load();

        var loadedSinger = Assert.Single(loaded.ActiveQueue);
        Assert.Equal("Alice", loadedSinger.Name);
        Assert.Equal(singer.Id, loadedSinger.Id); // stable Guid survives round-trip
        var loadedHistory = Assert.Single(loaded.PerformanceHistory);
        Assert.Equal(1, loadedHistory.Round);

        NightDatabaseService.Flush();
        NightDbState afterFlush = NightDatabaseService.Load();

        Assert.Empty(afterFlush.ActiveQueue);
        Assert.Empty(afterFlush.PerformanceHistory);
    }
}
