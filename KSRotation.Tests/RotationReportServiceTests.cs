// Edited on Oct 22, 2026 @ 10:25:00 -> Update unit tests for MMMddyy date format in filenames
using KSRotation.Models;
using KSRotation.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace KSRotation.Tests;

public class BuildReportRowsTests
{
    private static SongPerformance Perf(Guid singerId, string name, int round, string song = "Song", string artist = "Artist", bool isMusic = false) =>
        new() { SingerId = singerId, SingerName = name, Round = round, SongTitle = song, ArtistName = artist, Timestamp = DateTime.Now, IsMusic = isMusic };

    [Fact]
    public void MatchesPerformancesToQueuedSingerById()
    {
        var singer = new SingerEntry { Name = "Alice" };
        var history = new List<SongPerformance>
        {
            Perf(singer.Id, "Alice", 1),
            Perf(singer.Id, "Alice", 2),
        };

        var rows = RotationReportGenerator.BuildReportRows([singer], history);

        var row = Assert.Single(rows);
        Assert.Equal("Alice", row.SingerName);
        Assert.Equal(2, row.Performances.Count);
        // Rounds are ordered ascending.
        Assert.Equal(1, row.Performances[0].Round);
        Assert.Equal(2, row.Performances[1].Round);
    }

    [Fact]
    public void MatchesLegacyPerformancesByNameWhenSingerIdEmpty()
    {
        var singer = new SingerEntry { Name = "Bob" };
        var history = new List<SongPerformance>
        {
            Perf(Guid.Empty, "bob", 1), // legacy entry, case-insensitive name match
        };

        var rows = RotationReportGenerator.BuildReportRows([singer], history);

        var row = Assert.Single(rows);
        Assert.Equal("Bob", row.SingerName);
        Assert.Single(row.Performances);
    }

    [Fact]
    public void AppendsSingersWithHistoryButNoLongerInQueue()
    {
        var queued = new SingerEntry { Name = "Alice" };
        var goneId = Guid.NewGuid();
        var history = new List<SongPerformance>
        {
            Perf(queued.Id, "Alice", 1),
            Perf(goneId, "Carol", 1),
        };

        var rows = RotationReportGenerator.BuildReportRows([queued], history);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Alice", rows[0].SingerName); // queue order first
        Assert.Equal("Carol", rows[1].SingerName); // extra appended after
        Assert.False(rows[1].IsInactive);          // extra singers are counted active
    }

    [Fact]
    public void PreservesQueueOrderAndInactiveFlag()
    {
        var a = new SingerEntry { Name = "Alice" };
        var b = new SingerEntry { Name = "Bob", IsInactive = true };

        var rows = RotationReportGenerator.BuildReportRows([a, b], []);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Alice", rows[0].SingerName);
        Assert.False(rows[0].IsInactive);
        Assert.Equal("Bob", rows[1].SingerName);
        Assert.True(rows[1].IsInactive);
        Assert.Empty(rows[0].Performances);
    }

    [Fact]
    public void SeparatesKaraokeAndMusicRowsCorrectly()
    {
        var singer = new SingerEntry { Name = "Alice", IsMusic = false };
        var musicReq = new SingerEntry { Name = "Bob", IsMusic = true };

        var history = new List<SongPerformance>
        {
            Perf(singer.Id, "Alice", 1, isMusic: false),
            Perf(musicReq.Id, "Bob", 1, isMusic: true),
        };

        var rows = RotationReportGenerator.BuildReportRows([singer, musicReq], history);

        Assert.Equal(2, rows.Count);
        Assert.False(rows[0].IsMusic);
        Assert.True(rows[1].IsMusic);

        Assert.Equal("Alice", rows[0].SingerName);
        Assert.Equal("Bob", rows[1].SingerName);
    }
}

public class EscapeCsvTests
{
    [Theory]
    [InlineData("=SUM(A1)")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("@cmd")]
    public void PrefixesFormulaTriggerCharacters(string dangerous)
    {
        string result = RotationReportGenerator.EscapeCsv(dangerous);
        Assert.StartsWith("'", result);
    }

    [Fact]
    public void LeavesOrdinaryTextUnchanged()
    {
        Assert.Equal("Bob Jones", RotationReportGenerator.EscapeCsv("Bob Jones"));
    }

    [Fact]
    public void DoublesEmbeddedQuotes()
    {
        Assert.Equal("a\"\"b", RotationReportGenerator.EscapeCsv("a\"b"));
    }

    [Fact]
    public void ReturnsEmptyForNullOrEmpty()
    {
        Assert.Equal(string.Empty, RotationReportGenerator.EscapeCsv(""));
        Assert.Equal(string.Empty, RotationReportGenerator.EscapeCsv(null!));
    }
}

public class GenerateReportTests
{
    [Fact]
    public void GetPerformanceDate_ReturnsEarliestDate_WhenHistoryExists()
    {
        var day1 = new DateTime(2026, 10, 20, 21, 30, 0);
        var day2 = new DateTime(2026, 10, 21, 1, 15, 0);
        List<SongPerformance> history =
        [
            new() { Timestamp = day2, SingerName = "Bob" },
            new() { Timestamp = day1, SingerName = "Alice" }
        ];

        DateTime result = RotationReportGenerator.GetPerformanceDate(history);
        Assert.Equal(new DateTime(2026, 10, 20), result);
    }

    [Fact]
    public void GetPerformanceDate_ReturnsToday_WhenHistoryIsEmpty()
    {
        DateTime result = RotationReportGenerator.GetPerformanceDate([]);
        Assert.Equal(DateTime.Today, result);
    }

    [Fact]
    public void SanitizeFileNamePart_RemovesIllegalCharacters()
    {
        string sanitized = RotationReportGenerator.SanitizeFileNamePart("Rusty/Anchor: Bar & Grill?");
        Assert.DoesNotContain("/", sanitized);
        Assert.DoesNotContain(":", sanitized);
        Assert.DoesNotContain("?", sanitized);
        Assert.Contains("Rusty", sanitized);
        Assert.Contains("Anchor", sanitized);
    }

    [Fact]
    public void BuildReportBaseFileName_FormatsVenueAndDateWithoutRotation()
    {
        var date = new DateTime(2026, 10, 21);
        string baseName = RotationReportGenerator.BuildReportBaseFileName("The Rusty Anchor", date);
        Assert.StartsWith("The Rusty Anchor - Oct2126", baseName);
        Assert.DoesNotContain("Rotation", baseName);
    }

    [Fact]
    public void BuildReportBaseFileName_UsesDateOnly_WhenVenueIsEmpty()
    {
        var date = new DateTime(2026, 10, 21);
        string baseName = RotationReportGenerator.BuildReportBaseFileName("", date);
        Assert.StartsWith("Oct2126", baseName);
        Assert.DoesNotContain("Rotation", baseName);
    }

    [Fact]
    public void GeneratePdfCreatesValidPdfFileWithVenueAndDateName()
    {
        var singer = new SingerEntry { Name = "Alice" };
        var perfDate = new DateTime(2026, 10, 21, 22, 0, 0);
        var perf = new SongPerformance { SingerId = singer.Id, SingerName = "Alice", SongTitle = "Song Title", ArtistName = "Artist Name", Round = 1, Timestamp = perfDate };

        string filePath = RotationReportGenerator.GeneratePdf([singer], [perf], "Test Venue");

        Assert.True(File.Exists(filePath));
        string fileName = Path.GetFileName(filePath);
        Assert.StartsWith("Test Venue - Oct2126", fileName);
        Assert.EndsWith(".pdf", fileName);
        Assert.DoesNotContain("Rotation", fileName);

        var fileInfo = new FileInfo(filePath);
        Assert.True(fileInfo.Length > 0);

        try { File.Delete(filePath); } catch { }
    }

    [Fact]
    public void GenerateCsvCreatesValidCsvFileWithVenueAndDateName()
    {
        var singer = new SingerEntry { Name = "Bob" };
        var perfDate = new DateTime(2026, 10, 21, 23, 0, 0);
        var perf = new SongPerformance { SingerId = singer.Id, SingerName = "Bob", SongTitle = "Test Song", ArtistName = "Test Artist", Round = 1, Timestamp = perfDate };

        string filePath = RotationReportGenerator.GenerateCsv([singer], [perf], "Test Venue");

        Assert.True(File.Exists(filePath));
        string fileName = Path.GetFileName(filePath);
        Assert.StartsWith("Test Venue - Oct2126", fileName);
        Assert.EndsWith(".csv", fileName);
        Assert.DoesNotContain("Rotation", fileName);

        string content = File.ReadAllText(filePath);
        Assert.Contains("Bob", content);
        Assert.Contains("Test Song", content);

        try { File.Delete(filePath); } catch { }
    }
}

