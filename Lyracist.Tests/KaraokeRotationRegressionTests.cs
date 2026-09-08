// Edited on Aug 20, 2026 @ 14:02:00 -> Explicitly initialize FloatCurrentSingerToTop to false in test helper
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.Shared;
using Lyracist.ViewModels;
using Moq;

namespace Lyracist.Tests;

/// <summary>
/// Regression tests for the "current/next singer" desync bug family (see CHANGELOG entries for
/// 26.7.9.0/26.7.10.0/26.7.10.1 and the July 2026 rotation-logic consolidation). These wire up the
/// real RotationViewModel and KaraokeViewModel with mocked service dependencies so the actual event
/// chain (RotationViewModel.RotationStateChanged -> KaraokeViewModel.UpdateNowNext) runs exactly as
/// it does in the app, rather than only exercising RotationHelpers in isolation.
/// </summary>
public class KaraokeRotationRegressionTests
{
    private static Singer MakeSinger(string name) => new() { Name = name };

    private static RotationViewModel CreateRotationViewModel()
    {
        var display = new Mock<IDisplayService>();
        var mediaEngine = new Mock<IMediaEngine>();
        var vm = new RotationViewModel(display.Object, mediaEngine.Object);
        SetFloatCurrentSingerToTop(vm, false);
        vm.Rotation.Clear();
        vm.InactiveSingers.Clear();
        return vm;
    }

    // Sets FloatCurrentSingerToTop via reflection on the backing field rather than the public
    // property, because the property setter's generated OnFloatCurrentSingerToTopChanged hook
    // persists the value to AppSettings (a static, file-backed singleton at
    // %AppData%\Lyracist\settings.json) — which would leak across tests within the same process
    // and write to the real user's settings file, exactly what TestAppBootstrap's module
    // initializer says this suite avoids.
    private static void SetFloatCurrentSingerToTop(RotationViewModel vm, bool value)
    {
        var field = typeof(RotationViewModel).GetField("_floatCurrentSingerToTop",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("RotationViewModel._floatCurrentSingerToTop field not found.");
        field.SetValue(vm, value);
    }

    // UpdateNowNext is private and normally only runs off Rotation.Rotation.CollectionChanged /
    // RotationStateChanged. Some Last Round scenarios below need to force a resync pass without an
    // actual singer-completion event (e.g. simulating desynced state from an external data import),
    // so invoke it directly via reflection.
    private static void InvokeUpdateNowNext(KaraokeViewModel vm)
    {
        var method = typeof(KaraokeViewModel).GetMethod(
            "UpdateNowNext",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("KaraokeViewModel.UpdateNowNext method not found.");
        method.Invoke(vm, null);
    }

    private static KaraokeViewModel CreateKaraokeViewModel(RotationViewModel rotationVm, out Mock<IMediaEngine> mediaEngine)
    {
        var display = new Mock<IDisplayService>();
        display.Setup(d => d.GetScreens()).Returns(new List<ScreenInfo>());
        display.Setup(d => d.GetPreferences()).Returns(new DisplayPreferences());

        mediaEngine = new Mock<IMediaEngine>();

        var library = new Mock<ILibraryService>();
        library.Setup(l => l.SearchAsync(It.IsAny<string>())).ReturnsAsync(new List<KaraokeSong>());

        var showFlow = new Mock<IShowFlowService>();

        var occasions = new Mock<IOccasionService>();
        occasions.Setup(o => o.GetMenuTree()).Returns(new List<OccasionNode>());

        var requests = new Mock<IRequestService>();
        requests.Setup(r => r.GetPending()).Returns(new List<RequestInfo>());

        var navigation = new Mock<Wpf.Ui.INavigationService>();

        return new KaraokeViewModel(
            mediaEngine.Object,
            display.Object,
            library.Object,
            showFlow.Object,
            occasions.Object,
            rotationVm,
            requests.Object,
            navigation.Object);
    }

    [Fact]
    public void SetCurrentSinger_ManualOverride_PreservesDisplacedSingerAsNext_AfterKaraokeViewModelResync()
    {
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        var carol = MakeSinger("Carol");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        rotationVm.Rotation.Add(carol);

        // Constructing KaraokeViewModel subscribes UpdateNowNext to RotationStateChanged, and its
        // constructor runs one UpdateNowNext() pass itself (bootstrapping Alice as current, since
        // nobody was previously flagged, and Bob — sequentially next — as next).
        var karaokeVm = CreateKaraokeViewModel(rotationVm, out _);
        Assert.True(alice.IsCurrent);
        Assert.True(bob.IsNext);

        // KJ manually promotes Bob mid-rotation (not the sequential-next singer, and not the
        // fallback case) via the star-toggle "Set as Current Performer" command.
        rotationVm.SetCurrentSingerCommand.Execute(bob);

        Assert.True(bob.IsCurrent);
        Assert.False(alice.IsCurrent);

        // Alice — the singer Bob displaced — should resume as next. Before the fix, the
        // KaraokeViewModel.UpdateNowNext handler that runs synchronously off RotationStateChanged
        // re-derived "next" as whoever sequentially follows Bob (Carol), silently discarding the
        // "displaced singer resumes as next" behavior RotationHelpers.SetCurrentSinger implements.
        Assert.True(alice.IsNext);
        Assert.False(carol.IsNext);
        Assert.False(bob.IsNext);
    }

    [Fact]
    public async Task TogglePauseSinger_OnDesignatedNext_SelfHealsViaKaraokeViewModelResync()
    {
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        var carol = MakeSinger("Carol");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        rotationVm.Rotation.Add(carol);

        CreateKaraokeViewModel(rotationVm, out _);
        Assert.True(alice.IsCurrent);
        Assert.True(bob.IsNext);

        // Bob (currently flagged "next") steps out to pause instead of singing.
        await rotationVm.TogglePauseSingerCommand.ExecuteAsync(bob);

        // KaraokeViewModel's resync must not leave a paused singer flagged as next.
        Assert.False(bob.IsNext);
        Assert.True(carol.IsNext);
    }

    [Fact]
    public void SetRotationStartSingerCommand_OnAlreadyFlaggedSinger_ClearsTheAccidentalBadge()
    {
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        var carol = MakeSinger("Carol");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        rotationVm.Rotation.Add(carol);

        // DJ meant to flag Alice but fat-fingered Bob instead.
        rotationVm.SetRotationStartSingerCommand.Execute(bob);
        Assert.True(bob.IsRotationStart);

        // Clicking the same badge action again on Bob should undo the mistake, not be a no-op —
        // it hands the flag to whoever sequentially follows Bob (Carol), not back to Bob himself.
        rotationVm.SetRotationStartSingerCommand.Execute(bob);
        Assert.False(bob.IsRotationStart);
        Assert.False(alice.IsRotationStart);
        Assert.True(carol.IsRotationStart);

        // DJ can now flag the singer they actually intended.
        rotationVm.SetRotationStartSingerCommand.Execute(alice);
        Assert.True(alice.IsRotationStart);
        Assert.False(bob.IsRotationStart);
        Assert.False(carol.IsRotationStart);
    }

    [Fact]
    public void SetRotationStartSingerCommand_OnFlaggedCurrentSingerFloatedToTop_ClearsInsteadOfReassigningToSelf()
    {
        // The real-world shape of the reported bug: with FloatCurrentSingerToTop on, the current singer
        // sits at Rotation[0] — the row a DJ is most likely to accidentally flag. Reassigning to "first
        // active singer in list order" on clear would hand the flag right back to the same singer.
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        RotationHelpers.SetCurrentSinger(rotationVm.Rotation, alice, floatCurrentToTop: true);

        // Adding singers above auto-assigns the flag to the first active singer (Alice) via
        // EnsureRotationStartFlag; explicitly (re)confirm the "already flagged" starting state the
        // rest of this test exercises, rather than relying on that incidental side effect.
        Assert.True(alice.IsRotationStart);

        rotationVm.SetRotationStartSingerCommand.Execute(alice);
        Assert.False(alice.IsRotationStart);
        Assert.True(bob.IsRotationStart);
    }

    [Fact]
    public void DoneSinger_PromotesDesignatedNextSinger_WithoutLeavingStaleIsNextFlag()
    {
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        var carol = MakeSinger("Carol");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        rotationVm.Rotation.Add(carol);

        // Establish state directly through the shared helper (no KaraokeViewModel involved), so
        // this test isolates RotationViewModel.DoneSinger's own bookkeeping rather than relying on
        // any downstream listener to clean up after it.
        RotationHelpers.SetCurrentSinger(rotationVm.Rotation, alice);
        Assert.True(bob.IsNext);

        rotationVm.DoneSingerCommand.Execute(alice);

        Assert.False(alice.IsCurrent);
        Assert.True(bob.IsCurrent);
        // Bob was promoted from "next" to "current" — his own IsNext flag must be cleared, not
        // left stale (a singer can never be simultaneously current and next).
        Assert.False(bob.IsNext);
        Assert.True(carol.IsNext);
    }

    [Fact]
    public void DoneSinger_NoDesignatedNextSinger_ClearsCurrentWithoutPromotingAnyone()
    {
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        rotationVm.Rotation.Add(alice);
        RotationHelpers.SetCurrentSinger(rotationVm.Rotation, alice);

        rotationVm.DoneSingerCommand.Execute(alice);

        Assert.False(alice.IsCurrent);
        Assert.False(alice.IsNext);
    }

    [Fact]
    public async Task RemoveSinger_DeletingCurrentSingerWithNoDesignatedNext_PromotesRotationOrder()
    {
        // Isolates RotationViewModel.RemoveSinger's own bookkeeping (no KaraokeViewModel resync
        // involved), matching the DoneSinger fallback test above.
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        var carol = MakeSinger("Carol");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        rotationVm.Rotation.Add(carol);
        alice.IsCurrent = true;

        rotationVm.SelectedSinger = alice;
        await rotationVm.RemoveSingerCommand.ExecuteAsync(null);

        Assert.Equal(2, rotationVm.Rotation.Count);
        Assert.True(bob.IsCurrent);
        Assert.True(carol.IsNext);
    }

    [Fact]
    public async Task RemoveSinger_DeletingCurrentSingerWithDesignatedNext_PromotesDesignatedNextSinger()
    {
        // With KaraokeViewModel wired up (as in the real app), its RotationStateChanged-driven
        // resync must not clobber the deleted singer's designated Next with a plain
        // first-active-in-list-order fallback.
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        var carol = MakeSinger("Carol");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        rotationVm.Rotation.Add(carol);

        CreateKaraokeViewModel(rotationVm, out _);
        Assert.True(alice.IsCurrent);
        Assert.True(bob.IsNext);

        // DJ manually promotes Carol to Next (displacing Bob's sequential slot), then deletes Alice.
        rotationVm.SetCurrentSingerCommand.Execute(carol);
        rotationVm.SetCurrentSingerCommand.Execute(alice);
        Assert.True(carol.IsNext);

        rotationVm.SelectedSinger = alice;
        await rotationVm.RemoveSingerCommand.ExecuteAsync(null);

        Assert.Equal(2, rotationVm.Rotation.Count);
        Assert.True(carol.IsCurrent);
        Assert.False(carol.IsNext);
        Assert.True(bob.IsNext);
    }

    [Fact]
    public async Task ToggleInactiveSinger_MarkingCurrentSingerAway_PromotesRotationOrder()
    {
        // "Inactive" is Lyracist's away-from-the-mic/skip-until-they-return marker (distinct from
        // TogglePauseSinger, which just pauses the music without leaving the current slot). Isolates
        // RotationViewModel's own bookkeeping, matching the RemoveSinger fallback test above.
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        var carol = MakeSinger("Carol");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        rotationVm.Rotation.Add(carol);
        alice.IsCurrent = true;

        await rotationVm.ToggleInactiveSingerCommand.ExecuteAsync(alice);

        Assert.Contains(alice, rotationVm.InactiveSingers);
        Assert.DoesNotContain(alice, rotationVm.Rotation);
        Assert.False(alice.IsCurrent);
        Assert.Equal(2, rotationVm.Rotation.Count);
        Assert.True(bob.IsCurrent);
        Assert.True(carol.IsNext);
    }

    [Fact]
    public void DoneSinger_WithFloatCurrentSingerToTop_MovesNewCurrentSingerToTopOfList()
    {
        // Regression test: RotationViewModel's Rotation.CollectionChanged handler used to re-invoke
        // RotationHelpers.FloatCurrentSingerToTop on every mutation, including the intermediate
        // RemoveAt/Insert pair that AdvanceRotationAfterFinished's float branch performs internally
        // while the finishing singer's IsCurrent flag was still true. That reentrant call floated the
        // finishing singer straight back to the top mid-operation, so AdvanceRotationAfterFinished's own
        // "find next singer starting from index 0" scan re-selected the finishing singer as its own
        // replacement — the rotation never advanced and nothing visibly moved.
        var rotationVm = CreateRotationViewModel();
        SetFloatCurrentSingerToTop(rotationVm, true);

        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        var carol = MakeSinger("Carol");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        rotationVm.Rotation.Add(carol);

        RotationHelpers.SetCurrentSinger(rotationVm.Rotation, alice, floatCurrentToTop: true);
        Assert.Equal(alice, rotationVm.Rotation[0]);

        rotationVm.DoneSingerCommand.Execute(alice);

        Assert.False(alice.IsCurrent);
        Assert.True(bob.IsCurrent);
        Assert.Equal(bob, rotationVm.Rotation[0]);
        Assert.Equal(alice, rotationVm.Rotation[^1]);
    }

    [Fact]
    public void DoneSinger_WithFloatCurrentSingerToTopAndLinkedPartner_StillMovesFinishedSingerToBottom()
    {
        // Regression test for a Linked Singers bug: DoneSinger used to call EnforceLinkedAdjacency
        // right after AdvanceRotationAfterFinished, which — when Alice (current) and Bob (her linked
        // partner, next in line) are adjacent — found the just-floated-to-the-bottom Alice no longer
        // adjacent to the newly-promoted-to-top Bob and dragged Alice straight back up next to him,
        // undoing the float-to-bottom entirely (Alice ended up in 2nd place instead of last). Linked
        // Singers must never override the normal "finished singer moves to the bottom" behavior.
        var rotationVm = CreateRotationViewModel();
        SetFloatCurrentSingerToTop(rotationVm, true);

        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        var carol = MakeSinger("Carol");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        rotationVm.Rotation.Add(carol);

        RotationHelpers.LinkSingers(rotationVm.Rotation, alice, bob);
        RotationHelpers.SetCurrentSinger(rotationVm.Rotation, alice, floatCurrentToTop: true);
        Assert.Equal(alice, rotationVm.Rotation[0]);

        rotationVm.DoneSingerCommand.Execute(alice);

        Assert.False(alice.IsCurrent);
        Assert.True(bob.IsCurrent);
        Assert.Equal(bob, rotationVm.Rotation[0]);
        // The regression: Alice must end up at the bottom of the list, not dragged back to sit
        // next to Bob (which would land her in 2nd place instead).
        Assert.Equal(alice, rotationVm.Rotation[^1]);
    }

    [Fact]
    public async Task ToggleInactiveSinger_MarkingCurrentSingerAwayWithDesignatedNext_PromotesDesignatedNextSinger()
    {
        // With KaraokeViewModel wired up, its resync must not clobber the designated Next with a
        // plain first-active-in-list-order fallback (same ordering hazard as RemoveSinger).
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        var carol = MakeSinger("Carol");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        rotationVm.Rotation.Add(carol);

        CreateKaraokeViewModel(rotationVm, out _);
        Assert.True(alice.IsCurrent);
        Assert.True(bob.IsNext);

        // DJ manually promotes Carol to Next (displacing Bob's sequential slot), then steps Alice away.
        rotationVm.SetCurrentSingerCommand.Execute(carol);
        rotationVm.SetCurrentSingerCommand.Execute(alice);
        Assert.True(carol.IsNext);

        await rotationVm.ToggleInactiveSingerCommand.ExecuteAsync(alice);

        Assert.Equal(2, rotationVm.Rotation.Count);
        Assert.True(carol.IsCurrent);
        Assert.False(carol.IsNext);
        Assert.True(bob.IsNext);
    }

    // Regression coverage for KaraokeViewModel.UpdateNowNext ignoring Last Round mode: its
    // activeSingers filter, its "current is stale" check, and its "existing Next is stale" check all
    // used to omit HasSungInLastRound, and its UpdateNextSingerHighlight recompute never passed
    // isLastRound — so this resync (hooked to every Rotation.Rotation.CollectionChanged and
    // RotationStateChanged event) could silently re-promote or re-highlight a singer who already
    // performed during the last round, right on the audience-facing Now/Next display.

    [Fact]
    public void UpdateNowNext_DuringLastRound_DoesNotResurrectSungSingerAsCurrentWhenNoneEligibleRemain()
    {
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);

        var karaokeVm = CreateKaraokeViewModel(rotationVm, out _);
        Assert.True(alice.IsCurrent);
        Assert.True(bob.IsNext);

        rotationVm.IsLastRound = true;
        bob.HasSungInLastRound = true; // Bob already performed his last-round song
        bob.IsNext = false;

        // Alice (current) is removed with nobody eligible left but Bob, who is done for the night.
        // This directly exercises UpdateNowNext's own activeSingers fallback (not
        // RotationViewModel.RemoveSinger's promotion search, which is isolated from KaraokeViewModel here).
        rotationVm.Rotation.Remove(alice);

        Assert.False(bob.IsCurrent);
        Assert.Equal("None", karaokeVm.NowSingingName);
    }

    [Fact]
    public void UpdateNowNext_DuringLastRound_TreatsStaleCurrentSingerWhoAlreadySangAsNeedingPromotion()
    {
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        var carol = MakeSinger("Carol");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        rotationVm.Rotation.Add(carol);

        var karaokeVm = CreateKaraokeViewModel(rotationVm, out _);
        Assert.True(alice.IsCurrent);

        rotationVm.IsLastRound = true;
        // Simulate desynced state (e.g. an imported KSRotationSyncService snapshot) where the
        // designated current singer is also flagged as already having sung this last round.
        alice.HasSungInLastRound = true;

        InvokeUpdateNowNext(karaokeVm);

        Assert.False(alice.IsCurrent);
        Assert.True(bob.IsCurrent);
        Assert.Equal("Bob", karaokeVm.NowSingingName);
    }

    [Fact]
    public void UpdateNowNext_DuringLastRound_RecomputesNextSkippingSingerWhoAlreadySang()
    {
        var rotationVm = CreateRotationViewModel();
        var alice = MakeSinger("Alice");
        var bob = MakeSinger("Bob");
        var carol = MakeSinger("Carol");
        rotationVm.Rotation.Add(alice);
        rotationVm.Rotation.Add(bob);
        rotationVm.Rotation.Add(carol);

        var karaokeVm = CreateKaraokeViewModel(rotationVm, out _);
        Assert.True(alice.IsCurrent);
        Assert.True(bob.IsNext);

        rotationVm.IsLastRound = true;
        // Bob finishes his last-round song but stays flagged Next (desynced state).
        bob.HasSungInLastRound = true;

        InvokeUpdateNowNext(karaokeVm);

        Assert.False(bob.IsNext);
        Assert.True(carol.IsNext);
        Assert.Equal("Carol", karaokeVm.NextUpName);
    }

    // Regression coverage for a DJ-reported issue (KSRotation, ported here for parity): clicking
    // "Last Round" while the Rotation Anchor (or anyone else) is already the current singer, mid-turn,
    // must not exempt that singer's upcoming performance from counting as their last-round turn -
    // otherwise, once the rotation wraps back around, they get picked again and sing twice.
    [Fact]
    public void LastRound_ToggledWhileSingerIsAlreadyCurrent_DoesNotGetThemASecondTurn()
    {
        var rotationVm = CreateRotationViewModel();
        var singers = new List<Singer>();
        for (int i = 1; i <= 15; i++)
        {
            var s = MakeSinger($"Singer{i}");
            rotationVm.Rotation.Add(s);
            singers.Add(s);
        }
        var anchor = singers[7];
        foreach (var s in singers) s.IsCurrent = false;
        anchor.IsCurrent = true;
        anchor.IsRotationStart = true;

        // DJ clicks "Last Round" while the Anchor is already up for their normal turn.
        rotationVm.IsLastRound = true;

        // Anchor performs and DJ clicks Done.
        rotationVm.DoneSingerCommand.Execute(anchor);

        // That performance happened during Last Round, so it must count as Anchor's last-round turn.
        Assert.True(anchor.HasSungInLastRound);

        // Everyone else takes their last-round turn; Anchor must never come up again.
        var current = RotationHelpers.GetCurrentSinger(rotationVm.Rotation);
        for (int i = 0; i < 14; i++)
        {
            Assert.NotNull(current);
            Assert.NotSame(anchor, current);
            rotationVm.DoneSingerCommand.Execute(current!);
            current = RotationHelpers.GetCurrentSinger(rotationVm.Rotation);
        }

        // All 15 singers (Anchor included) have now sung exactly once - nobody eligible remains.
        Assert.Null(current);
        Assert.All(singers, s => Assert.True(s.HasSungInLastRound));
    }

    [Fact]
    public void SeedSingers_MarksFirstSeededSingerAsRotationStart()
    {
        // A fresh rotation should always have exactly one singer holding the "1st singer"
        // (IsRotationStart) badge - defaulting to whoever was entered first, with the DJ free to
        // change it afterward. SeedSingers populates Rotation via a raw Add() loop rather than
        // RotationHelpers.InsertNewSinger (which enforces this itself), so this checks the
        // invariant still holds for that path too.
        var rotationVm = CreateRotationViewModel();

        rotationVm.SeedSingers();

        Assert.NotEmpty(rotationVm.Rotation);
        Assert.Single(rotationVm.Rotation, s => s.IsRotationStart);
        Assert.True(rotationVm.Rotation[0].IsRotationStart);
    }
}
