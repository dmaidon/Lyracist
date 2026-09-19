// Edited on Sep 19, 2026 @ 18:09:00 -> Add unit test for SessionHandoffPayload serialization round-trip
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

    [Fact]
    public void SessionHandoffPayload_RoundTripsAllProperties()
    {
        var singer1 = new SingerEntry
        {
            Name = "Alice",
            Song = "Hey Jude",
            Artist = "The Beatles",
            IsCurrent = true,
            IsRotationStart = true,
            Song1Completed = true,
            QueuedSongs = [new QueuedSong("Yesterday", "The Beatles")]
        };
        var singer2 = new SingerEntry
        {
            Name = "Bob",
            Song = "Hotel California",
            Artist = "Eagles",
            IsNext = true,
            LinkedSingerId = singer1.Id
        };
        var history = new List<SongPerformance>
        {
            new()
            {
                SingerId = singer1.Id,
                SingerName = "Alice",
                SongTitle = "Hey Jude",
                ArtistName = "The Beatles",
                Round = 1,
                Timestamp = new DateTime(2026, 9, 19, 20, 0, 0)
            }
        };
        var requests = new List<PatronRequest>
        {
            new()
            {
                Name = "Charlie",
                Song = "Wonderwall",
                Artist = "Oasis"
            }
        };

        var original = new SessionHandoffPayload
        {
            Version = 1,
            ExportedAt = new DateTime(2026, 9, 19, 21, 30, 0),
            SourceDevice = "Laptop-DJ",
            VenueName = "The Rusty Anchor",
            DjName = "DJ Mike",
            DjPin = "4321",
            IsLastRound = true,
            EnableSessionSchedule = true,
            SessionStartTime = "9:00 PM",
            SessionStopTime = "1:00 AM",
            FloatCurrentSingerToTop = true,
            ShowEstimatedWaitTime = true,
            DefaultSongLengthMinutes = 5.0,
            Singers = [singer1, singer2],
            PerformanceHistory = history,
            IncomingRequests = requests
        };

        string json = System.Text.Json.JsonSerializer.Serialize(original, AppJsonContext.Default.SessionHandoffPayload);
        Assert.NotNull(json);

        var restored = System.Text.Json.JsonSerializer.Deserialize<SessionHandoffPayload>(json, AppJsonContext.Default.SessionHandoffPayload);
        Assert.NotNull(restored);

        Assert.Equal(original.VenueName, restored.VenueName);
        Assert.Equal(original.DjName, restored.DjName);
        Assert.Equal(original.DjPin, restored.DjPin);
        Assert.True(restored.IsLastRound);
        Assert.Equal(2, restored.Singers.Count);

        var restoredSinger1 = restored.Singers[0];
        Assert.Equal(singer1.Id, restoredSinger1.Id);
        Assert.Equal("Alice", restoredSinger1.Name);
        Assert.True(restoredSinger1.IsCurrent);
        Assert.True(restoredSinger1.Song1Completed);
        Assert.Single(restoredSinger1.QueuedSongs);

        var restoredSinger2 = restored.Singers[1];
        Assert.Equal(singer2.Id, restoredSinger2.Id);
        Assert.Equal("Bob", restoredSinger2.Name);
        Assert.True(restoredSinger2.IsNext);
        Assert.Equal(singer1.Id, restoredSinger2.LinkedSingerId);

        var restoredHistory = Assert.Single(restored.PerformanceHistory);
        Assert.Equal(singer1.Id, restoredHistory.SingerId);
        Assert.Equal("Hey Jude", restoredHistory.SongTitle);

        var restoredReq = Assert.Single(restored.IncomingRequests);
        Assert.Equal("Charlie", restoredReq.Name);
        Assert.Equal("Wonderwall", restoredReq.Song);
    }
}
