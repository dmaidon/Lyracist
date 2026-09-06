// Edited on Sep 3, 2026 @ 23:57:00 -> Add unit tests for Last Round rotation behavior
using KSRotation.Models;
using KSRotation.Services;
using Lyracist.Shared;
using System.Collections.ObjectModel;

namespace KSRotation.Tests;

public class WifiPasswordStoreTests
{
    [Fact]
    public void SetAndGetPasswordForSsid_SavesAndRecallsCorrectly()
    {
        string ssid = "TestVenueWiFi_" + Guid.NewGuid().ToString("N")[..6];
        const string password = "SecretPassword123";

        WifiPasswordStore.SetPasswordForSsid(ssid, password);
        string recalled = WifiPasswordStore.GetPasswordForSsid(ssid);

        Assert.Equal(password, recalled);
    }

    [Fact]
    public void SetPasswordForSsid_CaseInsensitiveRecall()
    {
        string ssid = "MyTravelRouter_" + Guid.NewGuid().ToString("N")[..6];
        const string password = "RouterPassword999";

        WifiPasswordStore.SetPasswordForSsid(ssid.ToLowerInvariant(), password);
        string recalled = WifiPasswordStore.GetPasswordForSsid(ssid.ToUpperInvariant());

        Assert.Equal(password, recalled);
    }

    [Fact]
    public void UpdatePasswordForSsid_OverwritesExisting()
    {
        string ssid = "VenueWiFi_" + Guid.NewGuid().ToString("N")[..6];
        WifiPasswordStore.SetPasswordForSsid(ssid, "OldPass");
        WifiPasswordStore.SetPasswordForSsid(ssid, "NewPass");

        Assert.Equal("NewPass", WifiPasswordStore.GetPasswordForSsid(ssid));
    }
}

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

    [Fact]
    public void GetCurrentSinger_ReturnsActiveCurrentSinger()
    {
        var alice = new SingerEntry { Name = "Alice", IsCurrent = true };
        var bob = new SingerEntry { Name = "Bob" };
        var singers = new ObservableCollection<SingerEntry> { alice, bob };

        Assert.Same(alice, RotationHelpers.GetCurrentSinger(singers));
        Assert.True(RotationHelpers.HasActiveCurrentSinger(singers));
    }

    [Fact]
    public void GetCurrentSinger_ReturnsNullWhenCurrentIsInactiveOrPaused()
    {
        var alice = new SingerEntry { Name = "Alice", IsCurrent = true, IsPaused = true };
        var bob = new SingerEntry { Name = "Bob" };
        var singers = new ObservableCollection<SingerEntry> { alice, bob };

        Assert.Null(RotationHelpers.GetCurrentSinger(singers));
        Assert.False(RotationHelpers.HasActiveCurrentSinger(singers));
    }

    [Fact]
    public void GetActiveSingerCount_CountsOnlyActiveNonPausedSingers()
    {
        var singers = new ObservableCollection<SingerEntry>
        {
            new() { Name = "Alice" },
            new() { Name = "Bob", IsPaused = true },
            new() { Name = "Carol", IsInactive = true },
            new() { Name = "Dave" }
        };

        Assert.Equal(2, RotationHelpers.GetActiveSingerCount(singers));
    }

    [Fact]
    public void ClearHighlights_ClearsIsCurrentAndIsNextOnAllSingers()
    {
        var singers = new ObservableCollection<SingerEntry>
        {
            new() { Name = "Alice", IsCurrent = true },
            new() { Name = "Bob", IsNext = true }
        };

        RotationHelpers.ClearHighlights(singers);

        Assert.All(singers, s =>
        {
            Assert.False(s.IsCurrent);
            Assert.False(s.IsNext);
        });
    }

    [Fact]
    public void AdvanceRotationAfterFinished_SingleActiveSinger_ClearsCurrentAndReturnsNull()
    {
        var alice = new SingerEntry { Name = "Alice", IsCurrent = true };
        var singers = new ObservableCollection<SingerEntry> { alice };

        var result = RotationHelpers.AdvanceRotationAfterFinished(singers, alice);

        Assert.Null(result);
        Assert.False(alice.IsCurrent);
        Assert.False(alice.IsNext);
    }

    [Fact]
    public void RecalculateEstimatedWaits_UsesKnownDurationsAndFallsBackForUnknown_SingerEntryModel()
    {
        var alice = new SingerEntry { Name = "Alice", IsCurrent = true, EstimatedPerformanceSeconds = 200 };
        var bob = new SingerEntry { Name = "Bob", EstimatedPerformanceSeconds = 100 };
        var charlie = new SingerEntry { Name = "Charlie" }; // unknown duration -> falls back to 300s

        var singers = new ObservableCollection<SingerEntry> { alice, bob, charlie };

        RotationHelpers.RecalculateEstimatedWaits(singers);

        Assert.Equal(0, alice.EstimatedWaitMinutes);
        // Bob's wait = Alice's 200s ahead of him, rounded to whole minutes (3.33 -> 3).
        Assert.Equal(3, bob.EstimatedWaitMinutes);
        // Charlie's wait = Alice's 200s + Bob's 100s = 300s = 5 minutes.
        Assert.Equal(5, charlie.EstimatedWaitMinutes);
    }

    [Fact]
    public void RecalculateEstimatedWaits_NoCurrentSinger_ClearsAllWaits_SingerEntryModel()
    {
        var alice = new SingerEntry { Name = "Alice", EstimatedWaitMinutes = 4 };
        var bob = new SingerEntry { Name = "Bob", EstimatedWaitMinutes = 8 };
        var singers = new ObservableCollection<SingerEntry> { alice, bob };

        RotationHelpers.RecalculateEstimatedWaits(singers);

        Assert.Equal(0, alice.EstimatedWaitMinutes);
        Assert.Equal(0, bob.EstimatedWaitMinutes);
    }

    [Fact]
    public void RecalculateEstimatedWaits_SkipsPausedSinger_AndDoesNotCountTheirTime_SingerEntryModel()
    {
        var alice = new SingerEntry { Name = "Alice", IsCurrent = true, EstimatedPerformanceSeconds = 300 };
        var bob = new SingerEntry { Name = "Bob", IsPaused = true, EstimatedWaitMinutes = 7 }; // stale value from before being paused
        var charlie = new SingerEntry { Name = "Charlie", EstimatedPerformanceSeconds = 300 };

        var singers = new ObservableCollection<SingerEntry> { alice, bob, charlie };

        RotationHelpers.RecalculateEstimatedWaits(singers);

        Assert.Equal(0, bob.EstimatedWaitMinutes);
        // Only Alice's 300s counts ahead of Charlie - Bob is paused and skipped entirely.
        Assert.Equal(5, charlie.EstimatedWaitMinutes);
    }

    [Fact]
    public void RecalculateEstimatedWaits_UsesDjConfiguredDefaultForUnknownDuration_SingerEntryModel()
    {
        // The DJ-configurable "default song length" (Settings tab) overrides the constant
        // fallback used when a caller doesn't pass one - proven here with a value distinct from
        // both the old (300s) and new (285s) built-in constants.
        var alice = new SingerEntry { Name = "Alice", IsCurrent = true }; // unknown duration
        var bob = new SingerEntry { Name = "Bob" };

        var singers = new ObservableCollection<SingerEntry> { alice, bob };

        RotationHelpers.RecalculateEstimatedWaits(singers, defaultEstimatedPerformanceSeconds: 240.0); // 4 min

        Assert.Equal(0, alice.EstimatedWaitMinutes);
        Assert.Equal(4, bob.EstimatedWaitMinutes);
    }

    [Fact]
    public void RecalculateEstimatedWaits_DefaultConstant_Is285Seconds_SingerEntryModel()
    {
        // 4.75 minutes - closer to a typical song's actual runtime than the old flat 5 minutes.
        Assert.Equal(285.0, RotationHelpers.DefaultEstimatedPerformanceSeconds);
    }

    [Fact]
    public void RecalculateEstimatedWaits_LastRound_ExcludesSungPerformers_SingerEntryModel()
    {
        var alice = new SingerEntry { Name = "Alice", IsCurrent = true, EstimatedPerformanceSeconds = 300 };
        var bob = new SingerEntry { Name = "Bob", HasSungInLastRound = true, EstimatedPerformanceSeconds = 300 };
        var charlie = new SingerEntry { Name = "Charlie", EstimatedPerformanceSeconds = 300 };

        var singers = new ObservableCollection<SingerEntry> { alice, bob, charlie };

        RotationHelpers.RecalculateEstimatedWaits(singers, isLastRound: true);

        Assert.Equal(0, bob.EstimatedWaitMinutes);
        // Bob already sang this round and is skipped, so only Alice's 300s counts ahead of Charlie.
        Assert.Equal(5, charlie.EstimatedWaitMinutes);
    }

    [Fact]
    public void RecalculateEstimatedWaits_DisabledByDj_ClearsEveryoneToZero_SingerEntryModel()
    {
        // The DJ can turn wait-time badges off entirely; every display already hides the badge
        // when EstimatedWaitMinutes is 0, so disabling just needs to clear everyone to 0.
        var alice = new SingerEntry { Name = "Alice", IsCurrent = true, EstimatedPerformanceSeconds = 300 };
        var bob = new SingerEntry { Name = "Bob", EstimatedWaitMinutes = 7 }; // stale value from before being disabled
        var singers = new ObservableCollection<SingerEntry> { alice, bob };

        RotationHelpers.RecalculateEstimatedWaits(singers, enabled: false);

        Assert.Equal(0, alice.EstimatedWaitMinutes);
        Assert.Equal(0, bob.EstimatedWaitMinutes);
    }

    [Fact]
    public void LinkSingers_SetsMutualLinkAndSnapsThemAdjacent_SingerEntryModel()
    {
        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var carol = new SingerEntry { Name = "Carol" };
        var list = new ObservableCollection<SingerEntry> { alice, carol, bob };

        RotationHelpers.LinkSingers(list, alice, bob);

        Assert.Equal(bob.Id, alice.LinkedSingerId);
        Assert.Equal(alice.Id, bob.LinkedSingerId);
        Assert.True(alice.IsLinked);
        Assert.True(bob.IsLinked);
        Assert.Equal(1, list.IndexOf(bob));
    }

    [Fact]
    public void LinkSingers_ReLinkingBreaksAnyPriorLinkOnEitherSide_SingerEntryModel()
    {
        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var carol = new SingerEntry { Name = "Carol" };
        var list = new ObservableCollection<SingerEntry> { alice, bob, carol };

        RotationHelpers.LinkSingers(list, alice, bob);
        RotationHelpers.LinkSingers(list, alice, carol);

        Assert.False(bob.IsLinked);
        Assert.Equal(carol.Id, alice.LinkedSingerId);
        Assert.Equal(alice.Id, carol.LinkedSingerId);
    }

    [Fact]
    public void UnlinkSinger_ClearsBothSides_SingerEntryModel()
    {
        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var list = new ObservableCollection<SingerEntry> { alice, bob };

        RotationHelpers.LinkSingers(list, alice, bob);
        RotationHelpers.UnlinkSinger(list, alice);

        Assert.False(alice.IsLinked);
        Assert.False(bob.IsLinked);
    }

    [Fact]
    public void EnforceLinkedAdjacency_PullsPartnerForwardWhenSingerLandsBetweenThem_SingerEntryModel()
    {
        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var list = new ObservableCollection<SingerEntry> { alice, bob };
        RotationHelpers.LinkSingers(list, alice, bob);

        var charlie = new SingerEntry { Name = "Charlie" };
        list.Insert(1, charlie);
        Assert.Equal(["Alice", "Charlie", "Bob"], list.Select(s => s.Name));

        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(["Alice", "Bob", "Charlie"], list.Select(s => s.Name));
    }

    [Fact]
    public void EnforceLinkedAdjacency_NoOpWhenAlreadyAdjacent_SingerEntryModel()
    {
        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var carol = new SingerEntry { Name = "Carol" };
        var list = new ObservableCollection<SingerEntry> { alice, bob, carol };
        RotationHelpers.LinkSingers(list, alice, bob);

        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(["Alice", "Bob", "Carol"], list.Select(s => s.Name));
    }

    [Fact]
    public void EnforceLinkedAdjacency_StillEnforcedWhenPartnerIsPaused_SingerEntryModel()
    {
        // A paused linked singer keeps its place and is just skipped over - it does NOT exempt the
        // pair from staying adjacent.
        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob", IsPaused = true };
        var list = new ObservableCollection<SingerEntry> { alice, bob };
        RotationHelpers.LinkSingers(list, alice, bob);

        var charlie = new SingerEntry { Name = "Charlie" };
        list.Insert(1, charlie);

        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(["Alice", "Bob", "Charlie"], list.Select(s => s.Name));
    }

    [Fact]
    public void EnforceLinkedAdjacency_DoesNotPullAnInactivePartnerBack_SingerEntryModel()
    {
        // Marking one half of a pair inactive ("out for the night") is different from pausing -
        // the still-active partner should NOT be forced to relocate next to a retired singer.
        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var list = new ObservableCollection<SingerEntry> { alice, bob };
        RotationHelpers.LinkSingers(list, alice, bob);

        var charlie = new SingerEntry { Name = "Charlie" };
        list.Insert(1, charlie);
        bob.IsInactive = true;

        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(["Alice", "Charlie", "Bob"], list.Select(s => s.Name));
    }

    [Fact]
    public void EnforceLinkedAdjacency_DoesNotPullBackWhilePartnerIsCurrent_SingerEntryModel()
    {
        // A linked pair is *expected* to separate when one finishes and the other (promoted to
        // perform next) is now IsCurrent - that's them performing back-to-back, the whole point of
        // linking them. Dragging the finished singer back up would undo that.
        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob", IsCurrent = true };
        var carol = new SingerEntry { Name = "Carol" };
        var list = new ObservableCollection<SingerEntry> { bob, carol, alice }; // Alice floated to the bottom after finishing
        RotationHelpers.LinkSingers(list, alice, bob); // still linked, just not adjacent right now

        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(["Bob", "Carol", "Alice"], list.Select(s => s.Name));
    }

    [Fact]
    public void EnforceLinkedAdjacency_ReunitesPairOnceNeitherIsCurrentAnymore_SingerEntryModel()
    {
        // Once both halves of a pair have had their turn (neither is current anymore), the pair is
        // no longer exempt - Linked Singers stays linked all night, so they get pulled back together
        // for their next joint turn instead of needing to be manually re-linked.
        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var carol = new SingerEntry { Name = "Carol", IsCurrent = true };
        var list = new ObservableCollection<SingerEntry> { carol, bob, alice };
        RotationHelpers.LinkSingers(list, alice, bob);

        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(1, Math.Abs(list.IndexOf(alice) - list.IndexOf(bob)));
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
        var singer = Assert.Single(vm.Singers);

        Assert.Equal("Dennis Maidon", singer.Name);
        Assert.Equal("Song A", singer.Song);
        Assert.Equal("Artist A", singer.Artist);

        var queuedSong = Assert.Single(singer.QueuedSongs);
        Assert.Equal("Song B", queuedSong.Song);
        Assert.Equal("Artist B", queuedSong.Artist);
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
        var singer = Assert.Single(vm.Singers);

        Assert.Equal("Dennis Maidon", singer.Name); // ProperCased
        Assert.Equal("Song A", singer.Song);

        var queuedSong = Assert.Single(singer.QueuedSongs);
        Assert.Equal("Song B", queuedSong.Song);
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
        var singer = Assert.Single(vm.Singers); // The duplicate row should be removed/merged!

        Assert.Equal("Dennis Maidon", singer.Name);
        Assert.Equal("Song A", singer.Song);

        var queuedSong = Assert.Single(singer.QueuedSongs);
        Assert.Equal("Song B", queuedSong.Song);
        Assert.Equal("Artist B", queuedSong.Artist);
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
        var singer = Assert.Single(vm.Singers);
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
        var queued = Assert.Single(entry.QueuedSongs);
        Assert.Equal("Third Song", queued.Song);
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
        var singer = Assert.Single(vm.Singers);
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
        return (string)method.Invoke(vm, [action, targetId, "", "", "", "", ""])!;
    }

    private static string InvokeDjAction(KSRotation.ViewModels.MainViewModel vm, string action, string targetId, string extraData)
    {
        var method = typeof(KSRotation.ViewModels.MainViewModel).GetMethod(
            "ExecuteDjActionOnUi",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (string)method.Invoke(vm, [action, targetId, extraData, "", "", "", ""])!;
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
        Assert.Equal(3, vm.Singers.Count);
        Assert.True(current.IsInactive);
        Assert.Equal(current.Id, vm.Singers[2].Id); // Moved to end
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
        Assert.Equal(3, vm.Singers.Count);
        Assert.True(current.IsInactive);
        Assert.Equal(current.Id, vm.Singers[2].Id); // Moved to end
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
        Assert.True(current.IsPaused);
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

    [Fact]
    public void DjRestoreAction_RestoresInactiveSingerAndMovesToActiveEnd()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = true };
        vm.Singers.Clear();

        var active1 = new SingerEntry { Name = "Alice" };
        var active2 = new SingerEntry { Name = "Bob" };
        var inactive = new SingerEntry { Name = "Carol", IsInactive = true };
        vm.Singers.Add(active1);
        vm.Singers.Add(inactive);
        vm.Singers.Add(active2);

        // Act
        string result = InvokeDjAction(vm, "restore", inactive.Id.ToString());

        // Assert
        Assert.Equal("", result);
        Assert.False(inactive.IsInactive);
        // Carol is restored and moved to the end of the active section (index 2 since she is active now)
        Assert.Equal(inactive.Id, vm.Singers[2].Id);
    }

    [Fact]
    public void DjSetCurrentAction_FailsOnPausedOrInactiveSinger()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = true };
        vm.Singers.Clear();

        var paused = new SingerEntry { Name = "Alice", IsPaused = true };
        var inactive = new SingerEntry { Name = "Bob", IsInactive = true };
        vm.Singers.Add(paused);
        vm.Singers.Add(inactive);

        // Act
        string resultPaused = InvokeDjAction(vm, "set-current", paused.Id.ToString());
        string resultInactive = InvokeDjAction(vm, "set-current", inactive.Id.ToString());

        // Assert
        Assert.Equal("Singer is paused or inactive.", resultPaused);
        Assert.Equal("Singer is paused or inactive.", resultInactive);
    }

    [Fact]
    public void FinishSingerSong_SequentialCheckboxes_MarksNextIncompleteRound()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = false };
        vm.Singers.Clear();

        var singer1 = new SingerEntry { Name = "Alice" };
        var singer2 = new SingerEntry { Name = "Bob" };
        var singer3 = new SingerEntry { Name = "Charlie" };

        vm.Singers.Add(singer1);
        vm.Singers.Add(singer2);
        vm.Singers.Add(singer3);

        // Alice and Charlie sing. Bob missed a turn.
        singer1.MarkRoundCompleted(1);
        singer3.MarkRoundCompleted(1);

        // Alice sings again
        singer1.MarkRoundCompleted(2);

        // Now Bob sings his first song!
        vm.FinishSingerSongCommand.Execute(singer2);

        // Assert: Bob's actual 1st completed song checkbox (Song1Completed) is checked
        Assert.True(singer2.Song1Completed);
        Assert.False(singer2.Song2Completed);
    }

    [Fact]
    public void AutoAcceptRequests_WhenChecked_ApprovesPendingRequests()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = false };
        vm.Singers.Clear();
        vm.IncomingRequests.Clear();
        vm.AutoAcceptRequests = false;

        // Add some pending requests
        vm.IncomingRequests.Add(new PatronRequest { Name = "Alice", Song = "Song A", Artist = "Artist A", RequestType = "Karaoke" });
        vm.IncomingRequests.Add(new PatronRequest { Name = "Bob", Song = "Song B", Artist = "Artist B", RequestType = "Music" });

        Assert.Equal(2, vm.IncomingRequests.Count);
        Assert.Empty(vm.Singers);

        // Act
        vm.AutoAcceptRequests = true; // Trigger property changed callback

        // Assert
        Assert.Empty(vm.IncomingRequests); // Both requests are accepted
        Assert.Equal(2, vm.Singers.Count); // Both are added to Singers

        var alice = vm.Singers[0];
        Assert.Equal("Alice", alice.Name);
        Assert.Equal("Song A", alice.Song);
        Assert.False(alice.IsMusic);

        var bob = vm.Singers[1];
        Assert.Equal("Bob", bob.Name);
        Assert.Equal("Song B", bob.Song);
        Assert.True(bob.IsMusic);
    }

    [Fact]
    public void FinishSingerSong_LastSingerInRotation_RollsOverToTopSinger()
    {
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = false };
        vm.Singers.Clear();

        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var charlie = new SingerEntry { Name = "Charlie" };

        vm.Singers.Add(alice);
        vm.Singers.Add(bob);
        vm.Singers.Add(charlie);

        // Charlie (last singer) is current
        charlie.IsCurrent = true;
        RotationHelpers.UpdateNextSingerHighlight(vm.Singers);
        Assert.True(alice.IsNext);

        // Act - Finish Charlie's song
        vm.FinishSingerSongCommand.Execute(charlie);

        // Assert - Rotation rolled over to Alice (top singer) and Bob is marked next
        Assert.False(charlie.IsCurrent);
        Assert.True(alice.IsCurrent);
        Assert.False(alice.IsNext);
        Assert.True(bob.IsNext);
    }

    [Fact]
    public void FinishSingerSong_WithDuetPartner_SavesToHistoryAndClearsPartner()
    {
        // Arrange
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = true };
        vm.Singers.Clear();

        var singer = new SingerEntry
        {
            Name = "John",
            DuetPartnerName = "Jane",
            Song = "Endless Love",
            Artist = "Lionel & Diana",
            IsCurrent = true
        };
        vm.Singers.Add(singer);

        // Act - finish the song
        vm.FinishSingerSongCommand.Execute(singer);

        // Assert - duet partner should be cleared from active queue for next turn
        Assert.Equal(string.Empty, singer.DuetPartnerName);

        // Assert - history should capture John and Jane's duet performance
        var getHistoryMethod = typeof(KSRotation.ViewModels.MainViewModel).GetMethod(
            "GetPerformanceHistorySnapshot",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var history = (List<SongPerformance>)getHistoryMethod.Invoke(vm, null)!;

        var performance = Assert.Single(history);
        Assert.Equal("John", performance.SingerName);
        Assert.Equal("Jane", performance.DuetPartnerName);
        Assert.Equal("Endless Love", performance.SongTitle);
        Assert.True(performance.IsDuet);
    }

    [Fact]
    public void FinishSingerSong_WithFloatCurrentSingerToTop_DropsFinishedSingerToBottom()
    {
        // ViewModel-level coverage (not just the shared helper in isolation) for "finish the current
        // singer's song, and they should drop to the bottom of the rotation instead of staying put."
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = false };
        vm.Singers.Clear();
        vm.FloatCurrentSingerToTop = true;

        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var carol = new SingerEntry { Name = "Carol" };
        vm.Singers.Add(alice);
        vm.Singers.Add(bob);
        vm.Singers.Add(carol);

        RotationHelpers.SetCurrentSinger(vm.Singers, alice, floatCurrentToTop: true);
        Assert.Equal(alice, vm.Singers[0]);

        vm.FinishSingerSongCommand.Execute(alice);

        Assert.False(alice.IsCurrent);
        Assert.True(bob.IsCurrent);
        Assert.Equal(bob, vm.Singers[0]);
        Assert.Equal(alice, vm.Singers[^1]);
    }

    [Fact]
    public void FinishSingerSong_WithFloatCurrentSingerToTopAndLinkedPartner_StillDropsFinishedSingerToBottom()
    {
        // Regression test for a Linked Singers bug: FinishSingerSong used to call
        // EnforceLinkedAdjacency right after AdvanceRotationAfterFinished, which — when Alice
        // (current) and Bob (her linked partner, next in line) are adjacent — found the
        // just-floated-to-the-bottom Alice no longer adjacent to the newly-promoted-to-top Bob and
        // dragged Alice straight back up next to him, undoing the float-to-bottom entirely (Alice
        // ended up in 2nd place instead of last). Linked Singers must never override the normal
        // "finished singer moves to the bottom" behavior.
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = false };
        vm.Singers.Clear();
        vm.FloatCurrentSingerToTop = true;

        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var carol = new SingerEntry { Name = "Carol" };
        vm.Singers.Add(alice);
        vm.Singers.Add(bob);
        vm.Singers.Add(carol);

        RotationHelpers.LinkSingers(vm.Singers, alice, bob);
        RotationHelpers.SetCurrentSinger(vm.Singers, alice, floatCurrentToTop: true);
        Assert.Equal(alice, vm.Singers[0]);

        vm.FinishSingerSongCommand.Execute(alice);

        Assert.False(alice.IsCurrent);
        Assert.True(bob.IsCurrent);
        Assert.Equal(bob, vm.Singers[0]);
        // The regression: Alice must end up at the bottom of the list, not dragged back to sit
        // next to Bob (which would land her in 2nd place instead).
        Assert.Equal(alice, vm.Singers[^1]);
    }

    [Fact]
    public void FinishSingerSong_RemovesFlaggedMusicSingerWithNoQueuedSongs_ReassignsRotationStartFlag()
    {
        // A fully-played music request holding the 1st-singer flag gets removed from Singers entirely
        // inside FinishSingerSong's own _isFinishingSong-guarded block, which suppresses
        // OnSingersCollectionChanged's usual reentrant EnsureRotationStartFlag call. Nothing else in
        // FinishSingerSong reassigns the flag for this specific path, so it must do so explicitly once
        // the guarded block completes — otherwise nobody holds it after the removal.
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = false };
        vm.Singers.Clear();

        var alice = new SingerEntry { Name = "Alice", IsMusic = true, Song = "Some Song", IsRotationStart = true };
        var bob = new SingerEntry { Name = "Bob" };
        vm.Singers.Add(alice);
        vm.Singers.Add(bob);

        vm.FinishSingerSongCommand.Execute(alice);

        Assert.DoesNotContain(alice, vm.Singers);
        Assert.True(bob.IsRotationStart);
    }

    [Fact]
    public void FinishSingerSong_WhenSingerNotMarkedCurrent_StillAdvancesToNextSinger()
    {
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = false };
        vm.Singers.Clear();

        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var charlie = new SingerEntry { Name = "Charlie" };

        vm.Singers.Add(alice);
        vm.Singers.Add(bob);
        vm.Singers.Add(charlie);

        // Charlie is NOT marked current (IsCurrent was out of sync or on Alice)
        alice.IsCurrent = true;

        // Act - Click Done on Charlie
        vm.FinishSingerSongCommand.Execute(charlie);

        // Assert - Rotation advances relative to Charlie, wrapping around to Alice
        Assert.False(charlie.IsCurrent);
        Assert.True(alice.IsCurrent);
        Assert.True(bob.IsNext);
    }

    [Fact]
    public void WifiHelper_GetConnectedSsid_DoesNotThrow()
    {
        // Act & Assert - Should return a string or null without throwing any exception
        var exception = Record.Exception(() => WifiHelper.GetConnectedSsid());
        Assert.Null(exception);
    }

    [Fact]
    public void DjBannerFileManager_CreateConnectInstructionsBannerPng_GeneratesValidImageFile()
    {
        // Arrange
        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_connect_banner_{Guid.NewGuid():N}.png");

        try
        {
            // Act
            DjBannerFileManager.CreateConnectInstructionsBannerPng(tempPath, "TestWiFi", "secret123", "http://192.168.1.100:8080/request");

            // Assert
            Assert.True(System.IO.File.Exists(tempPath));
            var fileInfo = new System.IO.FileInfo(tempPath);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void DjBannerFileManager_CreatePersonalizedBirthdayBannerPng_GeneratesValid16x9ImageFile()
    {
        // Arrange
        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_birthday_banner_{Guid.NewGuid():N}.png");

        try
        {
            // Act
            DjBannerFileManager.CreatePersonalizedBirthdayBannerPng(tempPath, "Brenda");

            // Assert
            Assert.True(System.IO.File.Exists(tempPath));
            var fileInfo = new System.IO.FileInfo(tempPath);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void EnsureRotationStartFlag_SetsFirstActiveSingerWhenNoneFlagged()
    {
        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var list = new ObservableCollection<SingerEntry> { alice, bob };

        RotationHelpers.EnsureRotationStartFlag(list);

        Assert.True(alice.IsRotationStart);
        Assert.False(bob.IsRotationStart);
    }

    [Fact]
    public void SetRotationStartSinger_ClearsOtherFlagsAndSetsTargetSinger()
    {
        var alice = new SingerEntry { Name = "Alice", IsRotationStart = true };
        var bob = new SingerEntry { Name = "Bob" };
        var list = new ObservableCollection<SingerEntry> { alice, bob };

        RotationHelpers.SetRotationStartSinger(list, bob);

        Assert.False(alice.IsRotationStart);
        Assert.True(bob.IsRotationStart);
    }

    [Fact]
    public void ToggleRotationStartSinger_OnUnflaggedSinger_SetsFlagAndClearsOthers()
    {
        var alice = new SingerEntry { Name = "Alice", IsRotationStart = true };
        var bob = new SingerEntry { Name = "Bob" };
        var list = new ObservableCollection<SingerEntry> { alice, bob };

        RotationHelpers.ToggleRotationStartSinger(list, bob);

        Assert.False(alice.IsRotationStart);
        Assert.True(bob.IsRotationStart);
    }

    [Fact]
    public void ToggleRotationStartSinger_OnAlreadyFlaggedSinger_ClearsAndReassignsToNextActiveSequentially()
    {
        // Regression coverage for "accidentally flagged the wrong singer": toggling the badge
        // action again on the singer who already holds it must undo the mistake, not be a no-op.
        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob", IsRotationStart = true };
        var carol = new SingerEntry { Name = "Carol" };
        var list = new ObservableCollection<SingerEntry> { alice, bob, carol };

        RotationHelpers.ToggleRotationStartSinger(list, bob);

        Assert.False(bob.IsRotationStart);
        Assert.False(alice.IsRotationStart);
        Assert.True(carol.IsRotationStart);
    }

    [Fact]
    public void ToggleRotationStartSinger_OnAlreadyFlaggedSingerAtTopOfList_DoesNotReassignBackToSameSinger()
    {
        // The real-world shape of the reported bug: with FloatCurrentSingerToTop on, the current singer
        // sits at list[0] — the row a DJ is most likely to accidentally flag. A naive "reassign to first
        // active singer in list order" fallback would hand the flag right back to the same singer still
        // at index 0, making "Clear 1st Singer Badge" look like it did nothing.
        var alice = new SingerEntry { Name = "Alice", IsRotationStart = true };
        var bob = new SingerEntry { Name = "Bob" };
        var carol = new SingerEntry { Name = "Carol" };
        var list = new ObservableCollection<SingerEntry> { alice, bob, carol };

        RotationHelpers.ToggleRotationStartSinger(list, alice);

        Assert.False(alice.IsRotationStart);
        Assert.True(bob.IsRotationStart);
    }

    [Fact]
    public void SetCurrentSinger_WithFloatCurrentToTop_MovesToTopIndex()
    {
        var alice = new SingerEntry { Name = "Alice", IsRotationStart = true };
        var bob = new SingerEntry { Name = "Bob" };
        var carol = new SingerEntry { Name = "Carol" };
        var list = new ObservableCollection<SingerEntry> { alice, bob, carol };

        RotationHelpers.SetCurrentSinger(list, bob, floatCurrentToTop: true);

        Assert.Equal(bob, list[0]);
        Assert.True(bob.IsCurrent);
        Assert.True(alice.IsRotationStart);
    }

    [Fact]
    public void AdvanceRotationAfterFinished_WithFloatCurrentToTop_AdvancesAndFloatsNextToTop()
    {
        var alice = new SingerEntry { Name = "Alice", IsCurrent = true, IsRotationStart = true };
        var bob = new SingerEntry { Name = "Bob" };
        var carol = new SingerEntry { Name = "Carol" };
        var list = new ObservableCollection<SingerEntry> { alice, bob, carol };

        RotationHelpers.AdvanceRotationAfterFinished(list, alice, floatCurrentToTop: true);

        Assert.Equal(bob, list[0]);
        Assert.True(bob.IsCurrent);
        Assert.Equal(alice, list[2]);
        Assert.False(alice.IsCurrent);
        Assert.True(alice.IsRotationStart);
    }

    [Fact]
    public void HandleSingerRetiredOrRemoved_Moves1stFlagToNextActiveSinger()
    {
        var alice = new SingerEntry { Name = "Alice", IsRotationStart = true };
        var bob = new SingerEntry { Name = "Bob" };
        var carol = new SingerEntry { Name = "Carol" };
        var list = new ObservableCollection<SingerEntry> { alice, bob, carol };

        RotationHelpers.HandleSingerRetiredOrRemoved(list, alice);

        Assert.False(alice.IsRotationStart);
        Assert.True(bob.IsRotationStart);
        Assert.False(carol.IsRotationStart);
    }

    [Fact]
    public void HandleSingerRetiredOrRemoved_SkipsInactiveAndPausedSingers()
    {
        var alice = new SingerEntry { Name = "Alice", IsRotationStart = true };
        var bob = new SingerEntry { Name = "Bob", IsInactive = true };
        var carol = new SingerEntry { Name = "Carol", IsPaused = true };
        var dave = new SingerEntry { Name = "Dave" };
        var list = new ObservableCollection<SingerEntry> { alice, bob, carol, dave };

        RotationHelpers.HandleSingerRetiredOrRemoved(list, alice);

        Assert.False(alice.IsRotationStart);
        Assert.False(bob.IsRotationStart);
        Assert.False(carol.IsRotationStart);
        Assert.True(dave.IsRotationStart);
    }

    [Fact]
    public void HandleSingerRetiredOrRemoved_WrapsToBeginningIfAtEndOfList()
    {
        var alice = new SingerEntry { Name = "Alice" };
        var bob = new SingerEntry { Name = "Bob" };
        var carol = new SingerEntry { Name = "Carol", IsRotationStart = true };
        var list = new ObservableCollection<SingerEntry> { alice, bob, carol };

        RotationHelpers.HandleSingerRetiredOrRemoved(list, carol);

        Assert.False(carol.IsRotationStart);
        Assert.True(alice.IsRotationStart);
    }

    [Fact]
    public void EnsureRotationStartFlag_ClearsInactiveSingerAndAssignsToFirstActive()
    {
        var alice = new SingerEntry { Name = "Alice", IsInactive = true, IsRotationStart = true };
        var bob = new SingerEntry { Name = "Bob" };
        var carol = new SingerEntry { Name = "Carol" };
        var list = new ObservableCollection<SingerEntry> { alice, bob, carol };

        RotationHelpers.EnsureRotationStartFlag(list);

        Assert.False(alice.IsRotationStart);
        Assert.True(bob.IsRotationStart);
        Assert.False(carol.IsRotationStart);
    }

    [Fact]
    public void InsertNewSinger_EmptyList_AddsAndSetsRotationStart()
    {
        var list = new ObservableCollection<SingerEntry>();
        var newSinger = new SingerEntry { Name = "Alice" };

        RotationHelpers.InsertNewSinger(list, newSinger);

        var singer = Assert.Single(list);
        Assert.Same(newSinger, singer);
        Assert.True(newSinger.IsRotationStart);
    }

    [Fact]
    public void InsertNewSinger_WhenSingersHaveSungAndAnchoredAtBottom_InsertsBeforeAnchor()
    {
        // 10 singers in rotation. Singers 1 and 2 already sang, so they are at indices 8 and 9 (the bottom).
        // Singer 1 is the anchor (IsRotationStart = true) at index 8.
        var singers = new List<SingerEntry>();
        for (int i = 3; i <= 10; i++)
        {
            singers.Add(new SingerEntry { Name = $"Singer{i}" });
        }
        var s1 = new SingerEntry { Name = "Singer1", IsRotationStart = true };
        var s2 = new SingerEntry { Name = "Singer2" };
        singers.Add(s1); // index 8
        singers.Add(s2); // index 9

        var list = new ObservableCollection<SingerEntry>(singers);
        var newSinger = new SingerEntry { Name = "NewGuy" };

        RotationHelpers.InsertNewSinger(list, newSinger);

        // NewGuy should be placed at index 8 (before Singer1 and Singer2)
        Assert.Equal(11, list.Count);
        Assert.Same(newSinger, list[8]);
        Assert.Same(s1, list[9]);
        Assert.Same(s2, list[10]);
        Assert.True(s1.IsRotationStart);
    }

    [Fact]
    public void InsertNewSinger_WhenRotationStartIsAtZero_AppendsBeforeInactiveSingers()
    {
        var s1 = new SingerEntry { Name = "Singer1", IsRotationStart = true };
        var s2 = new SingerEntry { Name = "Singer2" };
        var s3 = new SingerEntry { Name = "Singer3" };
        var inactive = new SingerEntry { Name = "Inactive1", IsInactive = true };

        var list = new ObservableCollection<SingerEntry> { s1, s2, s3, inactive };
        var newSinger = new SingerEntry { Name = "NewGuy" };

        RotationHelpers.InsertNewSinger(list, newSinger);

        Assert.Equal(5, list.Count);
        Assert.Same(newSinger, list[3]);
        Assert.Same(inactive, list[4]);
    }

    [Fact]
    public void AdvanceRotationAfterFinished_LastRound_SkipsSungSingers()
    {
        var s1 = new SingerEntry { Name = "Alice", IsCurrent = true };
        var s2 = new SingerEntry { Name = "Bob", HasSungInLastRound = true };
        var s3 = new SingerEntry { Name = "Charlie" };

        var list = new ObservableCollection<SingerEntry> { s1, s2, s3 };

        s1.HasSungInLastRound = true;
        RotationHelpers.AdvanceRotationAfterFinished(list, s1, floatCurrentToTop: false, isLastRound: true);

        Assert.False(s1.IsCurrent);
        Assert.False(s2.IsCurrent);
        Assert.True(s3.IsCurrent);
    }

    [Fact]
    public void AdvanceRotationAfterFinished_LastRound_WhenAllSingersHaveSung_SetsNoCurrent()
    {
        var s1 = new SingerEntry { Name = "Alice", IsCurrent = true };
        var s2 = new SingerEntry { Name = "Bob", HasSungInLastRound = true };

        var list = new ObservableCollection<SingerEntry> { s1, s2 };

        s1.HasSungInLastRound = true;
        RotationHelpers.AdvanceRotationAfterFinished(list, s1, floatCurrentToTop: false, isLastRound: true);

        Assert.False(s1.IsCurrent);
        Assert.False(s2.IsCurrent);
        Assert.Null(RotationHelpers.GetCurrentSinger(list));
    }

    [Fact]
    public void GetNextActiveSingers_LastRound_ExcludesSungPerformers()
    {
        var s1 = new SingerEntry { Name = "Alice", IsCurrent = true };
        var s2 = new SingerEntry { Name = "Bob", HasSungInLastRound = true };
        var s3 = new SingerEntry { Name = "Charlie" };
        var s4 = new SingerEntry { Name = "Diana", HasSungInLastRound = true };
        var s5 = new SingerEntry { Name = "Eve" };

        var list = new List<SingerEntry> { s1, s2, s3, s4, s5 };

        var nextActive = RotationHelpers.GetNextActiveSingers(list, s1, 5, isLastRound: true);

        Assert.Equal(2, nextActive.Count);
        Assert.Equal("Charlie", nextActive[0].Name);
        Assert.Equal("Eve", nextActive[1].Name);
    }

    // Regression coverage for the DJ web remote's action dispatcher (ExecuteDjActionOnUi): unlike
    // next-singer/previous-singer, the older toggle-inactive/delete/restore/complete-round handlers
    // did not thread IsLastRound through to their own "who's next" candidate search or to the final
    // RotationHelpers.UpdateNextSingerHighlight call, so a singer who already performed during the
    // Last Round could be silently re-promoted to current or re-highlighted as next when a DJ used
    // the remote console (but not the native WPF grid, whose equivalent commands already passed
    // isLastRound to UpdateNextSingerHighlight).

    [Fact]
    public void DjToggleInactiveAction_DuringLastRound_SkipsSungSingerWhenPromotingNext()
    {
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = true };
        vm.Singers.Clear();

        var current = new SingerEntry { Name = "Alice", IsCurrent = true };
        var sungSinger = new SingerEntry { Name = "Bob" };
        var eligible = new SingerEntry { Name = "Carol" };
        vm.Singers.Add(current);
        vm.Singers.Add(sungSinger);
        vm.Singers.Add(eligible);

        vm.IsLastRound = true; // resets HasSungInLastRound on everyone first
        sungSinger.HasSungInLastRound = true; // Bob already performed his last-round song

        // Act - pause the current singer (no one flagged IsNext, so this falls back to index order,
        // which must skip Bob since he's done for the night)
        string result = InvokeDjAction(vm, "toggle-inactive", current.Id.ToString());

        Assert.Equal("", result);
        Assert.True(current.IsPaused);
        Assert.False(sungSinger.IsCurrent);
        Assert.True(eligible.IsCurrent);
    }

    [Fact]
    public void DjDeleteAction_DuringLastRound_SkipsSungSingerWhenPromotingNext()
    {
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = true };
        vm.Singers.Clear();

        var current = new SingerEntry { Name = "Alice", IsCurrent = true };
        var sungSinger = new SingerEntry { Name = "Bob" };
        var eligible = new SingerEntry { Name = "Carol" };
        vm.Singers.Add(current);
        vm.Singers.Add(sungSinger);
        vm.Singers.Add(eligible);

        vm.IsLastRound = true;
        sungSinger.HasSungInLastRound = true;

        // Act - delete the current singer via the DJ web console (no one flagged IsNext)
        string result = InvokeDjAction(vm, "delete", current.Id.ToString());

        Assert.Equal("", result);
        Assert.False(sungSinger.IsCurrent);
        Assert.True(eligible.IsCurrent);
    }

    [Fact]
    public void DjRestoreAction_DuringLastRound_NextHighlightSkipsSungSinger()
    {
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = true };
        vm.Singers.Clear();

        var current = new SingerEntry { Name = "Alice", IsCurrent = true };
        var sungSinger = new SingerEntry { Name = "Bob" };
        var toRestore = new SingerEntry { Name = "Diana", IsInactive = true };
        vm.Singers.Add(current);
        vm.Singers.Add(sungSinger);
        vm.Singers.Add(toRestore);

        vm.IsLastRound = true;
        sungSinger.HasSungInLastRound = true;

        // Act - restoring Diana recomputes the Next highlight off Alice (current); it must skip Bob
        // (already sung) and land on Diana, the only other eligible singer.
        string result = InvokeDjAction(vm, "restore", toRestore.Id.ToString());

        Assert.Equal("", result);
        Assert.False(toRestore.IsInactive);
        Assert.False(sungSinger.IsNext);
        Assert.True(toRestore.IsNext);
    }

    [Fact]
    public void DjCompleteRoundAction_DuringLastRound_NextHighlightSkipsSungSinger()
    {
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = true };
        vm.Singers.Clear();

        var current = new SingerEntry { Name = "Alice", IsCurrent = true };
        var sungSinger = new SingerEntry { Name = "Bob" };
        var eligible = new SingerEntry { Name = "Carol" };
        vm.Singers.Add(current);
        vm.Singers.Add(sungSinger);
        vm.Singers.Add(eligible);

        vm.IsLastRound = true;
        sungSinger.HasSungInLastRound = true;

        // Act - marking a round complete for Carol recomputes the Next highlight off Alice; it must
        // skip Bob (already sung) and land on Carol.
        string result = InvokeDjAction(vm, "complete-round", eligible.Id.ToString(), "1");

        Assert.Equal("", result);
        Assert.False(sungSinger.IsNext);
        Assert.True(eligible.IsNext);
    }

    [Fact]
    public void LoadTestData_MarksFirstSeededSingerAsRotationStart()
    {
        // Regression test: LoadTestData used to populate Singers via a raw Add() loop with no
        // RotationHelpers.EnsureRotationStartFlag call afterward, so nobody ended up holding the
        // "1st singer" (IsRotationStart) badge at all — the same bug pattern LoadDatabaseNow (session
        // restore) had. The DJ expects whoever was entered first to automatically be the 1st singer.
        //
        // LoadTestData genuinely runs from inside the constructor while _isInitializing is still
        // true, which suppresses OnSingersCollectionChanged's own reactive EnsureRotationStartFlag
        // call - so invoking LoadTestData via reflection *after* construction (when _isInitializing
        // is already false) would let that reactive safety net paper over the bug and pass either
        // way. Flip _isInitializing back to true first to reproduce the real timing.
        var vm = new KSRotation.ViewModels.MainViewModel { IsTestMode = true };
        vm.Singers.Clear();

        var isInitializingField = typeof(KSRotation.ViewModels.MainViewModel).GetField(
            "_isInitializing",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        isInitializingField.SetValue(vm, true);

        var method = typeof(KSRotation.ViewModels.MainViewModel).GetMethod(
            "LoadTestData",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        method.Invoke(vm, null);

        isInitializingField.SetValue(vm, false);

        Assert.NotEmpty(vm.Singers);
        Assert.Single(vm.Singers, s => s.IsRotationStart);
        Assert.True(vm.Singers[0].IsRotationStart);
    }
}



