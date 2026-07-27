// Last Edit: Jun 30, 2026 08:40 - Tests for the shared report-row grouping and CSV formula-injection hardening.
using KSRotation.Models;
using KSRotation.Services;

namespace KSRotation.Tests;

public class BuildReportRowsTests
{
    private static SongPerformance Perf(Guid singerId, string name, int round, string song = "Song", string artist = "Artist") =>
        new() { SingerId = singerId, SingerName = name, Round = round, SongTitle = song, ArtistName = artist, Timestamp = DateTime.Now };

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

        Assert.Single(rows);
        Assert.Equal("Alice", rows[0].SingerName);
        Assert.Equal(2, rows[0].Performances.Count);
        // Rounds are ordered ascending.
        Assert.Equal(1, rows[0].Performances[0].Round);
        Assert.Equal(2, rows[0].Performances[1].Round);
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

        Assert.Single(rows);
        Assert.Equal("Bob", rows[0].SingerName);
        Assert.Single(rows[0].Performances);
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
