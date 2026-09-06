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

    [Fact]
    public void RecalculateEstimatedWaits_UsesKnownDurationsAndFallsBackForUnknown_SingerModel()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true, EstimatedPerformanceSeconds = 200 };
        var bob = new Singer { Name = "Bob", EstimatedPerformanceSeconds = 100 };
        var charlie = new Singer { Name = "Charlie" }; // unknown duration -> falls back to 300s

        var list = new List<Singer> { alice, bob, charlie };

        RotationHelpers.RecalculateEstimatedWaits(list);

        Assert.Equal(0, alice.EstimatedWaitMinutes);
        // Bob's wait = Alice's 200s ahead of him, rounded to whole minutes (3.33 -> 3).
        Assert.Equal(3, bob.EstimatedWaitMinutes);
        // Charlie's wait = Alice's 200s + Bob's 100s = 300s = 5 minutes.
        Assert.Equal(5, charlie.EstimatedWaitMinutes);
    }

    [Fact]
    public void RecalculateEstimatedWaits_NoCurrentSinger_ClearsAllWaits_SingerModel()
    {
        var alice = new Singer { Name = "Alice", EstimatedWaitMinutes = 4 };
        var bob = new Singer { Name = "Bob", EstimatedWaitMinutes = 8 };
        var list = new List<Singer> { alice, bob };

        RotationHelpers.RecalculateEstimatedWaits(list);

        Assert.Equal(0, alice.EstimatedWaitMinutes);
        Assert.Equal(0, bob.EstimatedWaitMinutes);
    }

    [Fact]
    public void RecalculateEstimatedWaits_SkipsPausedSinger_AndDoesNotCountTheirTime_SingerModel()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true, EstimatedPerformanceSeconds = 300 };
        var bob = Paused("Bob");
        bob.EstimatedWaitMinutes = 7; // stale value from before being paused
        var charlie = new Singer { Name = "Charlie", EstimatedPerformanceSeconds = 300 };

        var list = new List<Singer> { alice, bob, charlie };

        RotationHelpers.RecalculateEstimatedWaits(list);

        Assert.Equal(0, bob.EstimatedWaitMinutes);
        // Only Alice's 300s counts ahead of Charlie - Bob is paused and skipped entirely.
        Assert.Equal(5, charlie.EstimatedWaitMinutes);
    }

    [Fact]
    public void RecalculateEstimatedWaits_UsesDjConfiguredDefaultForUnknownDuration_SingerModel()
    {
        // The DJ-configurable "default song length" (Settings page) overrides the constant
        // fallback used when a caller doesn't pass one - proven here with a value distinct from
        // both the old (300s) and new (285s) built-in constants.
        var alice = new Singer { Name = "Alice", IsCurrent = true }; // unknown duration
        var bob = new Singer { Name = "Bob" };

        var list = new List<Singer> { alice, bob };

        RotationHelpers.RecalculateEstimatedWaits(list, defaultEstimatedPerformanceSeconds: 240.0); // 4 min

        Assert.Equal(0, alice.EstimatedWaitMinutes);
        Assert.Equal(4, bob.EstimatedWaitMinutes);
    }

    [Fact]
    public void RecalculateEstimatedWaits_DefaultConstant_Is285Seconds_SingerModel()
    {
        // 4.75 minutes - closer to a typical song's actual runtime than the old flat 5 minutes.
        Assert.Equal(285.0, RotationHelpers.DefaultEstimatedPerformanceSeconds);
    }

    [Fact]
    public void RecalculateEstimatedWaits_LastRound_ExcludesSungPerformers_SingerModel()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true, EstimatedPerformanceSeconds = 300 };
        var bob = new Singer { Name = "Bob", HasSungInLastRound = true, EstimatedPerformanceSeconds = 300 };
        var charlie = new Singer { Name = "Charlie", EstimatedPerformanceSeconds = 300 };

        var list = new List<Singer> { alice, bob, charlie };

        RotationHelpers.RecalculateEstimatedWaits(list, isLastRound: true);

        Assert.Equal(0, bob.EstimatedWaitMinutes);
        // Bob already sang this round and is skipped, so only Alice's 300s counts ahead of Charlie.
        Assert.Equal(5, charlie.EstimatedWaitMinutes);
    }

    [Fact]
    public void RecalculateEstimatedWaits_DisabledByDj_ClearsEveryoneToZero_SingerModel()
    {
        // The DJ can turn wait-time badges off entirely; every display already hides the badge
        // when EstimatedWaitMinutes is 0, so disabling just needs to clear everyone to 0.
        var alice = new Singer { Name = "Alice", IsCurrent = true, EstimatedPerformanceSeconds = 300 };
        var bob = new Singer { Name = "Bob", EstimatedWaitMinutes = 7 }; // stale value from before being disabled
        var list = new List<Singer> { alice, bob };

        RotationHelpers.RecalculateEstimatedWaits(list, enabled: false);

        Assert.Equal(0, alice.EstimatedWaitMinutes);
        Assert.Equal(0, bob.EstimatedWaitMinutes);
    }

    [Fact]
    public void LinkSingers_SetsMutualLinkAndSnapsThemAdjacent_SingerModel()
    {
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var list = new List<Singer> { alice, carol, bob };

        RotationHelpers.LinkSingers(list, alice, bob);

        Assert.Equal(bob.Id, alice.LinkedSingerId);
        Assert.Equal(alice.Id, bob.LinkedSingerId);
        Assert.True(alice.IsLinked);
        Assert.True(bob.IsLinked);
        // Bob (originally after Carol) should have been pulled up to sit right after Alice.
        Assert.Equal(1, list.IndexOf(bob));
    }

    [Fact]
    public void LinkSingers_ReLinkingBreaksAnyPriorLinkOnEitherSide_SingerModel()
    {
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var list = new List<Singer> { alice, bob, carol };

        RotationHelpers.LinkSingers(list, alice, bob);
        RotationHelpers.LinkSingers(list, alice, carol);

        Assert.False(bob.IsLinked);
        Assert.Equal(carol.Id, alice.LinkedSingerId);
        Assert.Equal(alice.Id, carol.LinkedSingerId);
    }

    [Fact]
    public void UnlinkSinger_ClearsBothSides_SingerModel()
    {
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var list = new List<Singer> { alice, bob };

        RotationHelpers.LinkSingers(list, alice, bob);
        RotationHelpers.UnlinkSinger(list, alice);

        Assert.False(alice.IsLinked);
        Assert.False(bob.IsLinked);
    }

    [Fact]
    public void EnforceLinkedAdjacency_PullsPartnerForwardWhenSingerLandsBetweenThem_SingerModel()
    {
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var list = new List<Singer> { alice, bob };
        RotationHelpers.LinkSingers(list, alice, bob);

        // A third singer gets inserted directly between the linked pair (e.g. a new patron signup).
        var charlie = new Singer { Name = "Charlie" };
        list.Insert(1, charlie);
        Assert.Equal(["Alice", "Charlie", "Bob"], list.Select(s => s.Name));

        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(["Alice", "Bob", "Charlie"], list.Select(s => s.Name));
    }

    [Fact]
    public void EnforceLinkedAdjacency_PullsPartnerBackwardWhenSingerLandsBetweenThem_SingerModel()
    {
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var list = new List<Singer> { alice, bob };
        RotationHelpers.LinkSingers(list, alice, bob);

        var charlie = new Singer { Name = "Charlie" };
        list.Insert(1, charlie);
        Assert.Equal(["Alice", "Charlie", "Bob"], list.Select(s => s.Name));

        // Fix from Bob's perspective this time (mirrors whichever half of the pair the caller
        // happens to scan first) - same end state either way.
        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(1, list.IndexOf(bob) - list.IndexOf(alice));
    }

    [Fact]
    public void EnforceLinkedAdjacency_NoOpWhenAlreadyAdjacent_SingerModel()
    {
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol" };
        var list = new List<Singer> { alice, bob, carol };
        RotationHelpers.LinkSingers(list, alice, bob);

        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(["Alice", "Bob", "Carol"], list.Select(s => s.Name));
    }

    [Fact]
    public void EnforceLinkedAdjacency_StillEnforcedWhenPartnerIsPaused_SingerModel()
    {
        // A paused linked singer keeps its place and is just skipped over - it does NOT exempt the
        // pair from staying adjacent.
        var alice = new Singer { Name = "Alice" };
        var bob = Paused("Bob");
        var list = new List<Singer> { alice, bob };
        RotationHelpers.LinkSingers(list, alice, bob);

        var charlie = new Singer { Name = "Charlie" };
        list.Insert(1, charlie);

        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(["Alice", "Bob", "Charlie"], list.Select(s => s.Name));
    }

    [Fact]
    public void EnforceLinkedAdjacency_DoesNotPullAnInactivePartnerBack_SingerModel()
    {
        // Marking one half of a pair inactive ("out for the night") is different from pausing -
        // the still-active partner should NOT be forced to relocate next to a retired singer.
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var list = new List<Singer> { alice, bob };
        RotationHelpers.LinkSingers(list, alice, bob);

        var charlie = new Singer { Name = "Charlie" };
        list.Insert(1, charlie);
        bob.IsInactive = true;

        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(["Alice", "Charlie", "Bob"], list.Select(s => s.Name));
    }

    [Fact]
    public void EnforceLinkedAdjacency_DoesNotPullBackWhilePartnerIsCurrent_SingerModel()
    {
        // A linked pair is *expected* to separate when one finishes and the other (promoted to
        // perform next) is now IsCurrent - that's them performing back-to-back, the whole point of
        // linking them. Dragging the finished singer back up would undo that.
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob", IsCurrent = true };
        var carol = new Singer { Name = "Carol" };
        var list = new List<Singer> { bob, carol, alice }; // Alice floated to the bottom after finishing
        RotationHelpers.LinkSingers(list, alice, bob); // still linked, just not adjacent right now

        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(["Bob", "Carol", "Alice"], list.Select(s => s.Name));
    }

    [Fact]
    public void EnforceLinkedAdjacency_ReunitesPairOnceNeitherIsCurrentAnymore_SingerModel()
    {
        // Once both halves of a pair have had their turn (neither is current anymore), the pair is
        // no longer exempt - Linked Singers stays linked all night, so they get pulled back together
        // for their next joint turn instead of needing to be manually re-linked.
        var alice = new Singer { Name = "Alice" };
        var bob = new Singer { Name = "Bob" };
        var carol = new Singer { Name = "Carol", IsCurrent = true };
        var list = new List<Singer> { carol, bob, alice };
        RotationHelpers.LinkSingers(list, alice, bob);

        RotationHelpers.EnforceLinkedAdjacency(list);

        Assert.Equal(1, Math.Abs(list.IndexOf(alice) - list.IndexOf(bob)));
    }
}

