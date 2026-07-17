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
        vm.Rotation.Clear();
        vm.InactiveSingers.Clear();
        return vm;
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

        var partyTyme = new Mock<IPartyTymeService>();

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
            partyTyme.Object,
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
}
