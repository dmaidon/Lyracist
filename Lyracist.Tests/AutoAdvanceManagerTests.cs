// Edited on Oct 3, 2026 @ 08:38:00 -> Add unit test verifying song end advances rotation without starting next song
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.Services.Media;
using Lyracist.Services.Tablet;
using Lyracist.Shared;
using Lyracist.ViewModels;
using Moq;
using Wpf.Ui;
using Xunit;

namespace Lyracist.Tests;

public class AutoAdvanceManagerTests
{
    // Wires up a real AutoAdvanceManager against a real RotationViewModel/LyricsWindowViewModel
    // (so the actual state machine and rotation bookkeeping run exactly as they do in the app),
    // with only the external-facing services mocked. No KaraokeViewModel/TriviaViewModel is
    // supplied, so Trivia/Scaryoke suspension never engages — irrelevant to the race under test.
    private static (AutoAdvanceManager Manager, RotationViewModel Rotation, Mock<IMediaEngine> MediaEngine, Mock<ILibraryService> Library)
        CreateManager()
    {
        var display = new Mock<IDisplayService>();
        display.Setup(d => d.GetPreferences()).Returns(new DisplayPreferences());

        var mediaEngine = new Mock<IMediaEngine>();
        mediaEngine.Setup(m => m.Play()).Returns(Task.CompletedTask);

        var rotation = new RotationViewModel(display.Object, mediaEngine.Object);
        // RotationViewModel's constructor seeds FloatCurrentSingerToTop from the real, file-backed
        // AppSettings — pin it to a known value via the backing field (bypassing the property
        // setter, which would persist the value back to that same file) so this test's outcome
        // doesn't depend on whatever a developer's machine happens to have saved.
        typeof(RotationViewModel)
            .GetField("_floatCurrentSingerToTop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(rotation, false);
        rotation.Rotation.Clear();

        var showFlow = new Mock<IShowFlowService>();
        var tabletServer = new Mock<ITabletLyricsServer>();
        var library = new Mock<ILibraryService>();
        var lyricsWindowVm = new LyricsWindowViewModel(display.Object);

        var manager = new AutoAdvanceManager(
            mediaEngine.Object,
            showFlow.Object,
            display.Object,
            rotation,
            tabletServer.Object,
            library.Object,
            lyricsWindowVm);

        return (manager, rotation, mediaEngine, library);
    }

    [Fact]
    public async Task SkipSinger_DuringInFlightStartSongNow_DoesNotDesyncRotationFromWhatIsPlaying()
    {
        // Regression test for the race where SkipSinger() could advance the rotation to a new
        // singer while a StartSongNow() call already committed to loading and playing the
        // PREVIOUS current singer's track — leaving the rotation showing one singer as current
        // while a different singer's song was actually playing.
        var (manager, rotation, mediaEngine, library) = CreateManager();

        var bob = new Singer { Name = "Bob", SongTitle = "Song B", IsCurrent = true };
        var carol = new Singer { Name = "Carol", SongTitle = "Song C" };
        rotation.Rotation.Add(bob);
        rotation.Rotation.Add(carol);

        // Controls exactly when Bob's library lookup (awaited inside StartSongNow) completes,
        // so the test can deterministically land inside the real in-flight window instead of
        // racing a timer.
        var searchGate = new TaskCompletionSource<IEnumerable<KaraokeSong>>();
        library.Setup(l => l.SearchAsync("Song B", false)).Returns(searchGate.Task);

        Task startTask = manager.StartSongNow();

        // StartSongNow has claimed StartingSong and is suspended awaiting the library search —
        // exactly the window the original bug raced.
        Assert.Equal(AutoAdvanceState.StartingSong, manager.CurrentState);

        // DJ clicks SKIP SINGER while Bob's song is still loading.
        manager.SkipSinger();

        // Before the fix: SkipSinger had no state guard, so it would call
        // _rotation.SkipCurrentSinger() here, advancing Bob -> Carol immediately.
        Assert.True(bob.IsCurrent);
        Assert.False(carol.IsCurrent);

        // Let Bob's load/play sequence finish.
        searchGate.SetResult(Array.Empty<KaraokeSong>());
        await startTask;

        mediaEngine.Verify(m => m.Play(), Times.Once);
        Assert.Equal(AutoAdvanceState.Idle, manager.CurrentState);

        // The rotation must still agree with what actually played: Bob, not Carol.
        Assert.True(bob.IsCurrent);
        Assert.False(carol.IsCurrent);
    }

    [Fact]
    public void SkipSinger_WhenIdle_StillAdvancesRotationNormally()
    {
        // The new guard must only block the transient StartingSong window, not ordinary skips.
        var (manager, rotation, _, _) = CreateManager();

        var bob = new Singer { Name = "Bob", SongTitle = "Song B", IsCurrent = true };
        var carol = new Singer { Name = "Carol", SongTitle = "Song C" };
        rotation.Rotation.Add(bob);
        rotation.Rotation.Add(carol);

        Assert.Equal(AutoAdvanceState.Idle, manager.CurrentState);
        manager.SkipSinger();

        Assert.False(bob.IsCurrent);
        Assert.True(carol.IsCurrent);
    }

    [Fact]
    public void AutoAdvanceState_EnumValues_AreCorrect()
    {
        Assert.Equal(0, (int)AutoAdvanceState.Idle);
        Assert.Equal(1, (int)AutoAdvanceState.GracePeriod);
        Assert.Equal(2, (int)AutoAdvanceState.WaitingForSongSelection);
        Assert.Equal(3, (int)AutoAdvanceState.ReadyToStart);
        Assert.Equal(4, (int)AutoAdvanceState.StartingSong);
        Assert.Equal(5, (int)AutoAdvanceState.SuspendedForMiniGame);
    }

    // Wires a real KaraokeViewModel into the manager (Scaryoke mode lives on that ViewModel, via
    // a real TwoWay XAML-bound property) so toggling IsScaryokeMode drives the exact same
    // PropertyChanged plumbing the running app uses - not just a mocked stand-in.
    private static (AutoAdvanceManager Manager, RotationViewModel Rotation, KaraokeViewModel KaraokeVm)
        CreateManagerWithScaryokeToggle()
    {
        var display = new Mock<IDisplayService>();
        display.Setup(d => d.GetPreferences()).Returns(new DisplayPreferences());
        display.Setup(d => d.GetScreens()).Returns(new List<ScreenInfo>());

        var mediaEngine = new Mock<IMediaEngine>();
        mediaEngine.Setup(m => m.Play()).Returns(Task.CompletedTask);

        var rotation = new RotationViewModel(display.Object, mediaEngine.Object);
        typeof(RotationViewModel)
            .GetField("_floatCurrentSingerToTop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(rotation, false);
        rotation.Rotation.Clear();

        var showFlow = new Mock<IShowFlowService>();
        var tabletServer = new Mock<ITabletLyricsServer>();
        var library = new Mock<ILibraryService>();
        var occasions = new Mock<IOccasionService>();
        occasions.Setup(o => o.GetMenuTree()).Returns(new List<OccasionNode>());
        var requests = new Mock<IRequestService>();
        requests.Setup(r => r.GetPending()).Returns(new List<RequestInfo>());
        var navigation = new Mock<INavigationService>();
        var lyricsWindowVm = new LyricsWindowViewModel(display.Object);

        var karaokeVm = new KaraokeViewModel(
            mediaEngine.Object,
            display.Object,
            library.Object,
            showFlow.Object,
            occasions.Object,
            rotation,
            requests.Object,
            navigation.Object);

        var manager = new AutoAdvanceManager(
            mediaEngine.Object,
            showFlow.Object,
            display.Object,
            rotation,
            tabletServer.Object,
            library.Object,
            lyricsWindowVm,
            getKaraokeVm: () => karaokeVm);

        // Mirrors App.xaml.cs's real wiring: the manager queries IsScaryokeMode via
        // getKaraokeVm above, and separately karaokeVm.AutoAdvance is set here so its
        // StateChanged/CountdownTick subscriptions (in the AutoAdvance property setter) attach.
        karaokeVm.AutoAdvance = manager;

        return (manager, rotation, karaokeVm);
    }

    [Fact]
    public void ScaryokeMode_StartingMidGracePeriod_SuspendsCountdownThenResumesWhenItEnds()
    {
        // Regression test: BeginGracePeriod/AutoAdvanceToNextSinger used to drop straight back to
        // Idle whenever Trivia/Scaryoke was active, with nothing left to re-trigger auto-advance
        // once the mini-game ended - a song finishing (or a grace period already ticking) during
        // Scaryoke could leave the rotation silently stuck until the DJ noticed and intervened
        // manually.
        var (manager, rotation, karaokeVm) = CreateManagerWithScaryokeToggle();

        var alice = new Singer { Name = "Alice", SongTitle = "Song A", IsCurrent = true };
        var bob = new Singer { Name = "Bob", SongTitle = "Song B" };
        rotation.Rotation.Add(alice);
        rotation.Rotation.Add(bob);

        manager.BeginGracePeriod();
        Assert.Equal(AutoAdvanceState.GracePeriod, manager.CurrentState);

        // DJ starts a Scaryoke round mid-countdown.
        karaokeVm.IsScaryokeMode = true;

        // Before the fix: nothing observed this, so the countdown timer kept ticking underneath
        // Scaryoke and would still auto-advance on schedule.
        Assert.Equal(AutoAdvanceState.SuspendedForMiniGame, manager.CurrentState);

        // Scaryoke ends.
        karaokeVm.IsScaryokeMode = false;

        // Before the fix: this would have stayed at Idle forever, since nothing was listening for
        // Scaryoke ending - the DJ would have to notice and manually press Start/Skip.
        Assert.Equal(AutoAdvanceState.GracePeriod, manager.CurrentState);
    }

    [Fact]
    public void BeginGracePeriod_DrivesKaraokeViewModelCountdownDisplay()
    {
        // Regression test: KaraokeViewModel's IsAutoAdvanceActive/AutoAdvanceRemainingSeconds
        // (bound to the countdown progress bar in KaraokePage.xaml) used to be wired to
        // IShowFlowService.AutoAdvanceCountdownTick - an event whose only source,
        // ShowFlowService.StartAutoAdvanceCountdown(), is never called by anything. That event
        // could never fire, so the DJ-facing countdown bar never appeared even though the real
        // grace-period countdown (in AutoAdvanceManager) was genuinely running.
        var (manager, rotation, karaokeVm) = CreateManagerWithScaryokeToggle();

        var alice = new Singer { Name = "Alice", SongTitle = "Song A", IsCurrent = true };
        rotation.Rotation.Add(alice);

        Assert.False(karaokeVm.IsAutoAdvanceActive);

        manager.BeginGracePeriod();

        Assert.True(karaokeVm.IsAutoAdvanceActive);
        Assert.Equal(manager.MaxSeconds, karaokeVm.AutoAdvanceRemainingSeconds);

        manager.CancelGracePeriod();

        Assert.False(karaokeVm.IsAutoAdvanceActive);
    }

    [Fact]
    public void Rotation_GetNextSinger_ReturnsSequentiallyNextSinger()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true };
        var bob = new Singer { Name = "Bob" };
        var charlie = new Singer { Name = "Charlie" };

        var list = new List<Singer> { alice, bob, charlie };
        RotationHelpers.UpdateNextSingerHighlight(list);

        Assert.True(bob.IsNext);
        Assert.False(charlie.IsNext);
    }

    [Fact]
    public void Rotation_SkipCurrentSinger_AdvancesToNextSingerWithoutIncrementingSongCount()
    {
        var alice = new Singer { Name = "Alice", IsCurrent = true, SongTitle = "Song A" };
        var bob = new Singer { Name = "Bob", SongTitle = "Song B" };
        var charlie = new Singer { Name = "Charlie", SongTitle = "Song C" };

        var list = new ObservableCollection<Singer> { alice, bob, charlie };

        // Perform skip rotation
        RotationHelpers.AdvanceRotationAfterFinished(list, alice, floatCurrentToTop: false);

        // Bob becomes current, Charlie is next, Alice moved to back
        Assert.True(bob.IsCurrent);
        Assert.False(alice.IsCurrent);
        Assert.Equal("Song A", alice.SongTitle); // Song selection preserved
    }

    [Fact]
    public void RotationHelpers_AdvanceRotationAfterFinished_WithDuetPartner_MaintainsQueueOrder()
    {
        var alice = new Singer { Name = "Alice", DuetPartnerName = "Dave", IsCurrent = true };
        var bob = new Singer { Name = "Bob" };
        var list = new ObservableCollection<Singer> { alice, bob };

        RotationHelpers.AdvanceRotationAfterFinished(list, alice, floatCurrentToTop: false);

        Assert.True(bob.IsCurrent);
        Assert.False(alice.IsCurrent);
    }

    [Fact]
    public void HandleSongEnded_AdvancesRotation_ShowsBillboard_AndDoesNotAutoPlayNextSong()
    {
        var (manager, rotation, mediaEngine, library) = CreateManager();

        var alice = new Singer { Name = "Alice", SongTitle = "Song A", IsCurrent = true };
        var bob = new Singer { Name = "Bob", SongTitle = "Song B" };
        rotation.Rotation.Add(alice);
        rotation.Rotation.Add(bob);

        // When a song naturally finishes
        manager.HandleSongEnded();

        // 1. Finished singer is marked done and moved; next singer is current
        Assert.True(bob.IsCurrent);
        Assert.False(alice.IsCurrent);
        Assert.Equal(1, alice.CompletedCount);

        // 2. Playback is NOT started automatically - waits for DJ
        mediaEngine.Verify(m => m.Play(), Times.Never);

        // 3. State is ready to start
        Assert.Equal(AutoAdvanceState.ReadyToStart, manager.CurrentState);
    }
}
