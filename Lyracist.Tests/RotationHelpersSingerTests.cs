// Edited on Sep 3, 2026 @ 23:57:00 -> Add Last Round unit tests for Singer model
using Lyracist.Models;
using Lyracist.Shared;

namespace Lyracist.Tests;

/// <summary>
/// Covers Lyracist.Shared.RotationHelpers through Lyracist's own Singer model (an
/// ObservableObject with [ObservableProperty] IsCurrent/IsNext/IsPaused/IsInactive), as a
/// counterpart to KSRotation.Tests' coverage of the same helper through SingerEntry.
/// </summary>
public class RotationHelpersSingerTests
{
    private static Singer Active(string name) => new() { Name = name };
    private static Singer Inactive(string name) => new() { Name = name, IsInactive = true };
    private static Singer Paused(string name) => new() { Name = name, IsPaused = true };

    [Fact]
    public void SetCurrentSinger_PromotesEntryAndMarksPreviousCurrentAsNext()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var singers = new List<Singer> { alice, bob, carol };

        // Manually promote Carol (e.g. the KJ's "Set as Current Performer" override).
        RotationHelpers.SetCurrentSinger(singers, carol);

        Assert.True(carol.IsCurrent);
        Assert.False(alice.IsCurrent);
        // The displaced current singer (Alice) should resume as next, not whoever is
        // sequentially after Carol (Bob) — this is the behavior that was being silently
        // undone by KaraokeViewModel re-deriving state after the fact.
        Assert.True(alice.IsNext);
        Assert.False(bob.IsNext);
    }

    [Fact]
    public void SetCurrentSinger_NoPreviousCurrent_FallsBackToSequentialNext()
    {
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var singers = new List<Singer> { alice, bob, carol };

        RotationHelpers.SetCurrentSinger(singers, bob);

        Assert.True(bob.IsCurrent);
        Assert.True(carol.IsNext);
        Assert.False(alice.IsNext);
    }

    [Fact]
    public void SetCurrentSinger_PreviousCurrentIsInactive_FallsBackToSequentialNext()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true, IsInactive = true };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var singers = new List<Singer> { alice, bob, carol };

        RotationHelpers.SetCurrentSinger(singers, bob);

        Assert.True(bob.IsCurrent);
        // Alice is inactive, so she can't become next — Carol (sequentially next) should be.
        Assert.False(alice.IsNext);
        Assert.True(carol.IsNext);
    }

    [Fact]
    public void SetCurrentSinger_ReactivatesPausedOrInactiveEntry()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true };
        var bob = Paused("Bob");
        bob.IsInactive = true;
        var singers = new List<Singer> { alice, bob };

        RotationHelpers.SetCurrentSinger(singers, bob);

        Assert.True(bob.IsCurrent);
        Assert.False(bob.IsPaused);
        Assert.False(bob.IsInactive);
    }

    [Fact]
    public void SetCurrentSinger_PromotingTheAlreadyCurrentSinger_IsIdempotent()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true };
        var bob = new Singer { Name = "Bob" };
        var singers = new List<Singer> { alice, bob };

        RotationHelpers.SetCurrentSinger(singers, alice);

        Assert.True(alice.IsCurrent);
        Assert.True(bob.IsNext);
    }

    [Fact]
    public void MarkNextSinger_SkipsPausedAndInactiveSingers()
    {
        var singers = new List<Singer>
        {
            Active("Alice"),
            Paused("Bob"),
            Inactive("Carol"),
            Active("Dave")
        };

        RotationHelpers.MarkNextSinger(singers, singers[0]);

        Assert.False(singers[1].IsNext);
        Assert.False(singers[2].IsNext);
        Assert.True(singers[3].IsNext);
    }

    [Fact]
    public void GetNextActiveSingers_ReturnsUpToMaxCountInSequentialOrder()
    {
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var carol = Inactive("Carol");
        var dave = new Singer { Name = "Dave" };
        var singers = new List<Singer> { alice, bob, carol, dave };

        var next = RotationHelpers.GetNextActiveSingers(singers, alice, maxCount: 5);

        Assert.Equal(["Bob", "Dave"], next.Select(s => s.Name));
    }

    [Fact]
    public void EnsureRotationStartFlag_SetsFirstActiveSingerWhenNoneFlagged()
    {
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var list = new List<Singer> { alice, bob };

        RotationHelpers.EnsureRotationStartFlag(list);

        Assert.True(alice.IsRotationStart);
        Assert.False(bob.IsRotationStart);
    }

    [Fact]
    public void SetRotationStartSinger_ClearsOtherFlagsAndSetsChosenSinger()
    {
        var alice = new Singer { Name = "Alice", IsRotationStart = true };
        var bob = new Singer { Name = "Bob" };
        var list = new List<Singer> { alice, bob };

        RotationHelpers.SetRotationStartSinger(list, bob);

        Assert.False(alice.IsRotationStart);
        Assert.True(bob.IsRotationStart);
    }

    [Fact]
    public void ToggleRotationStartSinger_OnUnflaggedSinger_SetsFlagAndClearsOthers()
    {
        var alice = new Singer { Name = "Alice", IsRotationStart = true };
        var bob = new Singer { Name = "Bob" };
        var list = new List<Singer> { alice, bob };

        RotationHelpers.ToggleRotationStartSinger(list, bob);

        Assert.False(alice.IsRotationStart);
        Assert.True(bob.IsRotationStart);
    }

    [Fact]
    public void ToggleRotationStartSinger_OnAlreadyFlaggedSinger_ClearsAndReassignsToNextActiveSequentially()
    {
        // Regression coverage for "accidentally flagged the wrong singer": clicking the badge
        // action again on the singer who already holds it must undo the mistake rather than
        // being a no-op (which is what unconditionally calling SetRotationStartSinger would do,
        // since it would just re-set the same singer as its own chosen target).
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob", IsRotationStart = true };
        var carol = new Singer { Name = "Carol" };
        var list = new List<Singer> { alice, bob, carol };

        RotationHelpers.ToggleRotationStartSinger(list, bob);

        Assert.False(bob.IsRotationStart);
        // Reassigns to whoever sequentially follows bob (Carol), matching HandleSingerRetiredOrRemoved's
        // search — not to "first active in list order," which would have picked Alice regardless of
        // where the cleared singer sat, and could just reassign it right back to bob himself (see the
        // next test).
        Assert.False(alice.IsRotationStart);
        Assert.True(carol.IsRotationStart);
    }

    [Fact]
    public void ToggleRotationStartSinger_OnAlreadyFlaggedSingerAtTopOfList_DoesNotReassignBackToSameSinger()
    {
        // This is the exact real-world shape of the bug: with FloatCurrentSingerToTop on, the current
        // singer sits at list[0]. If a DJ flags the current/top singer by mistake and clears it, a naive
        // "reassign to first active singer in list order" fallback would immediately hand the flag right
        // back to the same singer (still at index 0), making "clear" look like it did nothing.
        var alice = new Singer { Name = "Alice", IsRotationStart = true };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var list = new List<Singer> { alice, bob, carol };

        RotationHelpers.ToggleRotationStartSinger(list, alice);

        Assert.False(alice.IsRotationStart);
        Assert.True(bob.IsRotationStart);
    }

    [Fact]
    public void ToggleRotationStartSinger_OnAlreadyFlaggedSinger_WithNoOtherActiveSingers_ReassignsBackToSameSinger()
    {
        var alice = new Singer { Name = "Alice", IsRotationStart = true };
        var bob = new Singer { Name = "Bob", IsInactive = true };
        var list = new List<Singer> { alice, bob };

        RotationHelpers.ToggleRotationStartSinger(list, alice);

        // With nobody else eligible, EnsureRotationStartFlag's invariant reclaims Alice as the
        // only sane default — clearing to "nobody" isn't a supported end state.
        Assert.True(alice.IsRotationStart);
    }

    [Fact]
    public void SetCurrentSinger_WithFloatCurrentToTop_FloatsCurrentToTop()
    {
        var alice = new Singer { Name = "Alice", IsRotationStart = true };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var list = new List<Singer> { alice, bob, carol };

        RotationHelpers.SetCurrentSinger(list, bob, floatCurrentToTop: true);

        Assert.Equal(bob, list[0]);
        Assert.True(bob.IsCurrent);
        Assert.True(alice.IsRotationStart); // Flag remains intact
    }

    [Fact]
    public void SetCurrentSinger_LastRound_DoesNotResumePreviousCurrentIfTheyAlreadySang()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true, HasSungInLastRound = true };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var singers = new List<Singer> { alice, bob, carol };

        // A KJ manually promotes Carol while Alice (the displaced current singer) has already
        // performed her last-round song. Without isLastRound threaded through, SetCurrentSinger
        // would resume Alice as Next even though she's done for the night.
        RotationHelpers.SetCurrentSinger(singers, carol, isLastRound: true);

        Assert.True(carol.IsCurrent);
        Assert.False(alice.IsNext);
        Assert.True(bob.IsNext);
    }

    [Fact]
    public void SetCurrentSinger_LastRoundWithFloatCurrentToTop_NextHighlightSkipsSungSinger()
    {
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob", HasSungInLastRound = true };
        var carol = new Singer { Name = "Carol" };
        var singers = new List<Singer> { alice, bob, carol };

        RotationHelpers.SetCurrentSinger(singers, alice, floatCurrentToTop: true, isLastRound: true);

        Assert.True(alice.IsCurrent);
        Assert.False(bob.IsNext);
        Assert.True(carol.IsNext);
    }

    [Fact]
    public void AdvanceRotationAfterFinished_WithFloatCurrentToTop_AdvancesAndFloatsNextToTop()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true, IsRotationStart = true };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var list = new List<Singer> { alice, bob, carol };

        RotationHelpers.AdvanceRotationAfterFinished(list, alice, floatCurrentToTop: true);

        // After finished, Alice moves to end of active queue, Bob becomes current and floats to index 0
        Assert.Equal(bob, list[0]);
        Assert.True(bob.IsCurrent);
        Assert.Equal(alice, list[2]);
        Assert.False(alice.IsCurrent);
        Assert.True(alice.IsRotationStart);
    }

    [Fact]
    public void HandleSingerRetiredOrRemoved_MovesFlagToNextActiveSinger()
    {
        var alice = new Singer { Name = "Alice", IsRotationStart = true };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var list = new List<Singer> { alice, bob, carol };

        RotationHelpers.HandleSingerRetiredOrRemoved(list, alice);

        Assert.False(alice.IsRotationStart);
        Assert.True(bob.IsRotationStart);
        Assert.False(carol.IsRotationStart);
    }

    [Fact]
    public void HandleSingerRetiredOrRemoved_SkipsInactiveAndPausedSingers()
    {
        var alice = new Singer { Name = "Alice", IsRotationStart = true };
        var bob = Inactive("Bob");
        var carol = Paused("Carol");
        var dave = new Singer { Name = "Dave" };
        var list = new List<Singer> { alice, bob, carol, dave };

        RotationHelpers.HandleSingerRetiredOrRemoved(list, alice);

        Assert.False(alice.IsRotationStart);
        Assert.False(bob.IsRotationStart);
        Assert.False(carol.IsRotationStart);
        Assert.True(dave.IsRotationStart);
    }

    [Fact]
    public void AdvanceRotationAfterFinished_LastRound_SkipsSungSingers_SingerModel()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true };
        var bob = new Singer { Name = "Bob", HasSungInLastRound = true };
        var charlie = new Singer { Name = "Charlie" };

        var list = new List<Singer> { alice, bob, charlie };

        alice.HasSungInLastRound = true;
        RotationHelpers.AdvanceRotationAfterFinished(list, alice, floatCurrentToTop: false, isLastRound: true);

        Assert.False(alice.IsCurrent);
        Assert.False(bob.IsCurrent);
        Assert.True(charlie.IsCurrent);
    }

    [Fact]
    public void AdvanceRotationAfterFinished_LastRound_WhenAllSingersHaveSung_SetsNoCurrent_SingerModel()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true };
        var bob = new Singer { Name = "Bob", HasSungInLastRound = true };

        var list = new List<Singer> { alice, bob };

        alice.HasSungInLastRound = true;
        RotationHelpers.AdvanceRotationAfterFinished(list, alice, floatCurrentToTop: false, isLastRound: true);

        Assert.False(alice.IsCurrent);
        Assert.False(bob.IsCurrent);
        Assert.Null(RotationHelpers.GetCurrentSinger(list));
    }

    [Fact]
    public void GetNextActiveSingers_LastRound_ExcludesSungPerformers_SingerModel()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true };
        var bob = new Singer { Name = "Bob", HasSungInLastRound = true };
        var charlie = new Singer { Name = "Charlie" };
        var diana = new Singer { Name = "Diana", HasSungInLastRound = true };
        var eve = new Singer { Name = "Eve" };

        var list = new List<Singer> { alice, bob, charlie, diana, eve };

        var nextActive = RotationHelpers.GetNextActiveSingers(list, alice, 5, isLastRound: true);

        Assert.Equal(2, nextActive.Count);
        Assert.Equal("Charlie", nextActive[0].Name);
        Assert.Equal("Eve", nextActive[1].Name);
    }
}

