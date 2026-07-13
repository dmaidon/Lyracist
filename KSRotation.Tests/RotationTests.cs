// Last Edit: Jun 29, 2026 13:26 - Initial test suite: SingerEntry round helpers, RotationHelpers, ThemeService.
using KSRotation.Models;
using KSRotation.Services;
using System.Collections.ObjectModel;

namespace KSRotation.Tests;

// ---------------------------------------------------------------------------
// SingerEntry tests
// ---------------------------------------------------------------------------

public class SingerEntryTests
{
    [Fact]
    public void NewSingerEntry_HasUniqueId()
    {
        var a = new SingerEntry();
        var b = new SingerEntry();

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void GetNextIncompleteRound_ReturnsOneForFreshSinger()
    {
        var entry = new SingerEntry();

        Assert.Equal(1, entry.GetNextIncompleteRound());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void GetNextIncompleteRound_AfterMarkingRound_ReturnsNextRound(int round)
    {
        var entry = new SingerEntry();
        for (int r = 1; r <= round; r++)
            entry.MarkRoundCompleted(r);

        int expected = round < 10 ? round + 1 : 0;
        Assert.Equal(expected, entry.GetNextIncompleteRound());
    }

    [Fact]
    public void GetNextIncompleteRound_AllComplete_ReturnsZero()
    {
        var entry = new SingerEntry();
        for (int r = 1; r <= 10; r++)
            entry.MarkRoundCompleted(r);

        Assert.Equal(0, entry.GetNextIncompleteRound());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(10)]
    public void GetHighestCompletedRound_AfterMarkingRound_ReturnsCorrectRound(int round)
    {
        var entry = new SingerEntry();
        entry.MarkRoundCompleted(round);

        Assert.Equal(round, entry.GetHighestCompletedRound());
    }

    [Fact]
    public void GetHighestCompletedRound_NoRoundsComplete_ReturnsZero()
    {
        var entry = new SingerEntry();

        Assert.Equal(0, entry.GetHighestCompletedRound());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void IsRoundCompleted_AfterMarkingRound_ReturnsTrue(int round)
    {
        var entry = new SingerEntry();
        entry.MarkRoundCompleted(round);

        Assert.True(entry.IsRoundCompleted(round));
    }

    [Fact]
    public void IsRoundCompleted_OutOfRangeRound_ReturnsFalse()
    {
        var entry = new SingerEntry();

        Assert.False(entry.IsRoundCompleted(0));
        Assert.False(entry.IsRoundCompleted(11));
    }
}

// ---------------------------------------------------------------------------
// RotationHelpers tests
// ---------------------------------------------------------------------------

public class RotationHelpersTests
{
    private static SingerEntry Active(string name) => new() { Name = name };
    private static SingerEntry Inactive(string name) => new() { Name = name, IsInactive = true };

    [Fact]
    public void MarkNextSinger_SetsIsNextOnFirstActiveSingerAfterCurrent()
    {
        var singers = new ObservableCollection<SingerEntry>
        {
            Active("Alice"),
            Active("Bob"),
            Active("Carol")
        };

        RotationHelpers.MarkNextSinger(singers, singers[0]);

        Assert.False(singers[0].IsNext);
        Assert.True(singers[1].IsNext);
        Assert.False(singers[2].IsNext);
    }

    [Fact]
    public void MarkNextSinger_SkipsInactiveSingers()
    {
        var singers = new ObservableCollection<SingerEntry>
        {
            Active("Alice"),
            Inactive("Bob"),
            Active("Carol")
        };

        RotationHelpers.MarkNextSinger(singers, singers[0]);

        Assert.False(singers[1].IsNext);
        Assert.True(singers[2].IsNext);
    }

    [Fact]
    public void MarkNextSinger_WrapsAroundToFirstActiveSinger()
    {
        var singers = new ObservableCollection<SingerEntry>
        {
            Active("Alice"),
            Active("Bob")
        };

        RotationHelpers.MarkNextSinger(singers, singers[1]);

        Assert.True(singers[0].IsNext);
        Assert.False(singers[1].IsNext);
    }

    [Fact]
    public void MarkNextSinger_AllOthersInactive_SetsNoIsNext()
    {
        var singers = new ObservableCollection<SingerEntry>
        {
            Active("Alice"),
            Inactive("Bob"),
            Inactive("Carol")
        };

        RotationHelpers.MarkNextSinger(singers, singers[0]);

        Assert.False(singers[1].IsNext);
        Assert.False(singers[2].IsNext);
    }

    [Fact]
    public void UpdateNextSingerHighlight_NoCurrent_ClearsAllIsNext()
    {
        var singers = new ObservableCollection<SingerEntry>
        {
            new() { Name = "Alice", IsNext = true },
            new() { Name = "Bob", IsNext = true }
        };

        RotationHelpers.UpdateNextSingerHighlight(singers);

        Assert.All(singers, s => Assert.False(s.IsNext));
    }

    [Fact]
    public void UpdateNextSingerHighlight_WithCurrent_MarksNextSinger()
    {
        var singers = new ObservableCollection<SingerEntry>
        {
            new() { Name = "Alice", IsCurrent = true },
            Active("Bob"),
            Active("Carol")
        };

        RotationHelpers.UpdateNextSingerHighlight(singers);

        Assert.True(singers[1].IsNext);
        Assert.False(singers[2].IsNext);
    }

    [Fact]
    public void MarkNextSinger_ThrowsOnNullSingers()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RotationHelpers.MarkNextSinger(null!, new SingerEntry()));
    }

    [Fact]
    public void UpdateNextSingerHighlight_ThrowsOnNullSingers()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RotationHelpers.UpdateNextSingerHighlight(null!));
    }
}

// ---------------------------------------------------------------------------
// ThemeService tests
// ---------------------------------------------------------------------------

public class ThemeServiceTests
{
    // IsSystemDarkMode is a pure registry read; smoke-test only that it returns
    // a bool without throwing (actual value is machine-dependent).
    [Fact]
    public void IsSystemDarkMode_DoesNotThrow()
    {
        bool result = ThemeService.IsSystemDarkMode();
        Assert.IsType<bool>(result);
    }
}
