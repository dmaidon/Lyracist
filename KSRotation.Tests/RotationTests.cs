// Edited on Jul 17, 2026 @ 09:00:00 -> Enable testing in Debug
// Last Edit: Jun 29, 2026 13:26 - Initial test suite: SingerEntry round helpers, RotationHelpers, ThemeService.
using KSRotation.Models;
using KSRotation.Services;
using Lyracist.Shared;
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
            RotationHelpers.MarkNextSinger<SingerEntry>(null!, new SingerEntry()));
    }

    [Fact]
    public void UpdateNextSingerHighlight_ThrowsOnNullSingers()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RotationHelpers.UpdateNextSingerHighlight<SingerEntry>(null!));
    }

    [Fact]
    public void SetCurrentSinger_PromotesEntryAndMarksPreviousCurrentAsNext()
    {
        var singers = new ObservableCollection<SingerEntry>
        {
            new() { Name = "Alice", IsCurrent = true },
            Active("Bob"),
            Active("Carol")
        };

        // Promoting Carol (not Bob, who'd be the plain sequential-next after Alice) is the case
        // that distinguishes "previous current resumes as next" from "sequential next singer".
        RotationHelpers.SetCurrentSinger(singers, singers[2]);

        Assert.True(singers[2].IsCurrent);
        Assert.False(singers[0].IsCurrent);
        Assert.True(singers[0].IsNext);
        Assert.False(singers[1].IsNext);
    }

    [Fact]
    public void SetCurrentSinger_NoPreviousCurrent_FallsBackToSequentialNext()
    {
        var singers = new ObservableCollection<SingerEntry>
        {
            Active("Alice"),
            Active("Bob"),
            Active("Carol")
        };

        RotationHelpers.SetCurrentSinger(singers, singers[1]);

        Assert.True(singers[1].IsCurrent);
        Assert.True(singers[2].IsNext);
        Assert.False(singers[0].IsNext);
    }

    [Fact]
    public void SetCurrentSinger_PreviousCurrentInactive_FallsBackToSequentialNext()
    {
        var singers = new ObservableCollection<SingerEntry>
        {
            new() { Name = "Alice", IsCurrent = true, IsInactive = true },
            Active("Bob"),
            Active("Carol")
        };

        RotationHelpers.SetCurrentSinger(singers, singers[1]);

        Assert.True(singers[1].IsCurrent);
        Assert.False(singers[0].IsNext);
        Assert.True(singers[2].IsNext);
    }

    [Fact]
    public void SetCurrentSinger_ReactivatesInactiveEntry()
    {
        var singers = new ObservableCollection<SingerEntry>
        {
            new() { Name = "Alice", IsCurrent = true },
            new() { Name = "Bob", IsInactive = true }
        };

        RotationHelpers.SetCurrentSinger(singers, singers[1]);

        Assert.True(singers[1].IsCurrent);
        Assert.False(singers[1].IsInactive);
    }

    [Fact]
    public void SetCurrentSinger_ThrowsOnNullArguments()
    {
        var singers = new ObservableCollection<SingerEntry> { Active("Alice") };

        Assert.Throws<ArgumentNullException>(() =>
            RotationHelpers.SetCurrentSinger(null!, singers[0]));
        Assert.Throws<ArgumentNullException>(() =>
            RotationHelpers.SetCurrentSinger(singers, null!));
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

public class MainViewModelTests
{
    [Fact]
    public void TryAddPerformer_SingerExists_QueuesSongAndDoesNotDuplicate()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel
        {
            IsTestMode = true
        };
        vm.Singers.Clear();

        // Act
        bool addedFirst = vm.TryAddPerformer("Dennis Maidon", "Song A", "Artist A");
        bool addedSecond = vm.TryAddPerformer("Dennis Maidon", "Song B", "Artist B");

        // Assert
        Assert.True(addedFirst);
        Assert.True(addedSecond);
        Assert.Single(vm.Singers);
        
        var singer = vm.Singers[0];
        Assert.Equal("Dennis Maidon", singer.Name);
        Assert.Equal("Song A", singer.Song);
        Assert.Equal("Artist A", singer.Artist);
        
        Assert.Single(singer.QueuedSongs);
        Assert.Equal("Song B", singer.QueuedSongs[0].Song);
        Assert.Equal("Artist B", singer.QueuedSongs[0].Artist);
    }

    [Fact]
    public void TryAddPerformer_SingerExistsWithSpacingAnomalies_QueuesSongAndDoesNotDuplicate()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel
        {
            IsTestMode = true
        };
        vm.Singers.Clear();

        // Act - Dennis Maidon with a non-breaking space (0xA0), tab, leading/trailing space, and double inner spaces
        bool addedFirst = vm.TryAddPerformer("  Dennis\u00A0Maidon  ", "Song A", "Artist A");
        bool addedSecond = vm.TryAddPerformer("dennis \t  maidon", "Song B", "Artist B");

        // Assert
        Assert.True(addedFirst);
        Assert.True(addedSecond);
        Assert.Single(vm.Singers);

        var singer = vm.Singers[0];
        Assert.Equal("Dennis Maidon", singer.Name); // ProperCased
        Assert.Equal("Song A", singer.Song);
        
        Assert.Single(singer.QueuedSongs);
        Assert.Equal("Song B", singer.QueuedSongs[0].Song);
    }

    [Fact]
    public void SingerNameChanged_ToExistingSingerName_MergesSongsAndRemovesDuplicate()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel
        {
            IsTestMode = true
        };
        vm.Singers.Clear();

        // Add an existing singer with a song
        vm.TryAddPerformer("Dennis Maidon", "Song A", "Artist A");
        
        // Add a new blank row, then set song/artist
        var duplicateRow = new SingerEntry { Name = "New Singer" };
        vm.Singers.Add(duplicateRow);
        duplicateRow.Song = "Song B";
        duplicateRow.Artist = "Artist B";

        // Act - Simulate the user editing the name from "New Singer" to "Dennis Maidon"
        duplicateRow.Name = "Dennis Maidon";

        // Assert
        Assert.Single(vm.Singers); // The duplicate row should be removed/merged!
        
        var singer = vm.Singers[0];
        Assert.Equal("Dennis Maidon", singer.Name);
        Assert.Equal("Song A", singer.Song);
        
        Assert.Single(singer.QueuedSongs);
        Assert.Equal("Song B", singer.QueuedSongs[0].Song);
        Assert.Equal("Artist B", singer.QueuedSongs[0].Artist);
    }

    [Fact]
    public void AcceptRequest_MultipleSongs_QueuesAllSongsInOrder()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel
        {
            IsTestMode = true
        };
        vm.Singers.Clear();

        var request = new PatronRequest
        {
            Name = "Alice",
            Songs =
            [
                new RequestedSong("Song 1", "Artist 1"),
                new RequestedSong("Song 2", "Artist 2"),
                new RequestedSong("Song 3", "Artist 3")
            ]
        };

        // Act
        vm.AcceptRequest(request);

        // Assert
        Assert.Single(vm.Singers);
        var singer = vm.Singers[0];
        Assert.Equal("Alice", singer.Name);
        Assert.Equal("Song 1", singer.Song);
        Assert.Equal("Artist 1", singer.Artist);

        Assert.Equal(2, singer.QueuedSongs.Count);
        Assert.Equal("Song 2", singer.QueuedSongs[0].Song);
        Assert.Equal("Artist 2", singer.QueuedSongs[0].Artist);
        Assert.Equal("Song 3", singer.QueuedSongs[1].Song);
        Assert.Equal("Artist 3", singer.QueuedSongs[1].Artist);
    }

    [Fact]
    public void SongCleared_AutoPostsNextSongFromQueue()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel
        {
            IsTestMode = true
        };
        vm.Singers.Clear();

        var entry = new SingerEntry
        {
            Name = "Bob",
            Song = "Current Song",
            Artist = "Current Artist",
            QueuedSongs =
            [
                new QueuedSong("Next Song", "Next Artist"),
                new QueuedSong("Third Song", "Third Artist")
            ]
        };
        vm.Singers.Add(entry);

        // Act - clear the current song
        entry.Song = string.Empty;

        // Assert - should automatically promote the next song from queue
        Assert.Equal("Next Song", entry.Song);
        Assert.Equal("Next Artist", entry.Artist);
        Assert.Single(entry.QueuedSongs);
        Assert.Equal("Third Song", entry.QueuedSongs[0].Song);
    }

    [Fact]
    public void AcceptRequest_DuplicateSongs_IgnoresDuplicates()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel
        {
            IsTestMode = true
        };
        vm.Singers.Clear();

        var entry = new SingerEntry
        {
            Name = "Alice",
            Song = "Song 1",
            Artist = "Artist 1",
            QueuedSongs =
            [
                new QueuedSong("Song 2", "Artist 2")
            ]
        };
        vm.Singers.Add(entry);

        var request = new PatronRequest
        {
            Name = "Alice",
            Songs =
            [
                new RequestedSong("Song 1", "Artist 1"), // Duplicate of active song
                new RequestedSong("Song 2", "Artist 2"), // Duplicate of queued song
                new RequestedSong("Song 3", "Artist 3"), // Unique song
                new RequestedSong("Song 3", "Artist 3")  // Duplicate of another song in the request
            ]
        };

        // Act
        vm.AcceptRequest(request);

        // Assert - should only add Song 3 once, ignoring other duplicates
        Assert.Single(vm.Singers);
        var singer = vm.Singers[0];
        Assert.Equal("Alice", singer.Name);
        Assert.Equal("Song 1", singer.Song);

        Assert.Equal(2, singer.QueuedSongs.Count);
        Assert.Equal("Song 2", singer.QueuedSongs[0].Song);
        Assert.Equal("Song 3", singer.QueuedSongs[1].Song);
    }

    // ExecuteDjActionOnUi is private (invoked normally via HandleDjAction, which requires a live
    // System.Windows.Application.Current for its Dispatcher marshaling and so isn't testable
    // headlessly). It has no such dependency itself, so call it directly via reflection.
    private static string InvokeDjAction(KSRotation.ViewModels.MainViewModel vm, string action, string targetId)
    {
        var method = typeof(KSRotation.ViewModels.MainViewModel).GetMethod(
            "ExecuteDjActionOnUi",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (string)method.Invoke(vm, [action, targetId, "", "", "", ""])!;
    }

    [Fact]
    public void DjDeleteAction_DeletingCurrentSinger_PromotesFlaggedNextSinger()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = true };
        vm.Singers.Clear();

        var current = new SingerEntry { Name = "Alice", IsCurrent = true };
        var next = new SingerEntry { Name = "Bob", IsNext = true };
        var after = new SingerEntry { Name = "Carol" };
        vm.Singers.Add(current);
        vm.Singers.Add(next);
        vm.Singers.Add(after);

        // Act - delete the current singer via the DJ web console's delete action
        string result = InvokeDjAction(vm, "delete", current.Id.ToString());

        // Assert
        Assert.Equal("", result);
        Assert.Equal(2, vm.Singers.Count);
        Assert.True(next.IsCurrent);
        Assert.False(next.IsNext);
        Assert.True(after.IsNext);
    }

    [Fact]
    public void DjDeleteAction_DeletingCurrentSingerWithNoNextFlag_FallsBackToIndexOrder()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = true };
        vm.Singers.Clear();

        var current = new SingerEntry { Name = "Alice", IsCurrent = true };
        var after = new SingerEntry { Name = "Bob" };
        var last = new SingerEntry { Name = "Carol" };
        vm.Singers.Add(current);
        vm.Singers.Add(after);
        vm.Singers.Add(last);

        // Act - no one is flagged IsNext, so deletion should fall back to standard rotation order
        string result = InvokeDjAction(vm, "delete", current.Id.ToString());

        // Assert
        Assert.Equal("", result);
        Assert.Equal(2, vm.Singers.Count);
        Assert.True(after.IsCurrent);
        Assert.True(last.IsNext);
    }

    [Fact]
    public void DjToggleInactiveAction_PausingCurrentSingerWithNoNextFlag_FallsBackToIndexOrder()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = true };
        vm.Singers.Clear();

        var current = new SingerEntry { Name = "Alice", IsCurrent = true };
        var after = new SingerEntry { Name = "Bob" };
        var last = new SingerEntry { Name = "Carol" };
        vm.Singers.Add(current);
        vm.Singers.Add(after);
        vm.Singers.Add(last);

        // Act - pause the current singer (no one flagged IsNext) via the DJ web console
        string result = InvokeDjAction(vm, "toggle-inactive", current.Id.ToString());

        // Assert
        Assert.Equal("", result);
        Assert.True(current.IsInactive);
        Assert.False(current.IsCurrent);
        Assert.True(after.IsCurrent);
        Assert.True(last.IsNext);
    }

    [Fact]
    public void ToggleSingerInactiveCommand_PausingCurrentSinger_PromotesFlaggedNextSinger()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = true };
        vm.Singers.Clear();

        var current = new SingerEntry { Name = "Alice", IsCurrent = true };
        var next = new SingerEntry { Name = "Bob", IsNext = true };
        var after = new SingerEntry { Name = "Carol" };
        vm.Singers.Add(current);
        vm.Singers.Add(next);
        vm.Singers.Add(after);

        // Act - pause the current singer via the WPF grid's pause button
        vm.ToggleSingerInactiveCommand.Execute(current);

        // Assert
        Assert.True(current.IsInactive);
        Assert.False(current.IsCurrent);
        Assert.True(next.IsCurrent);
        Assert.False(next.IsNext);
        Assert.True(after.IsNext);
    }
}

