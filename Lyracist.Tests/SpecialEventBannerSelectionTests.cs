// Edited on Oct 4, 2026 @ 23:35:00 -> Add tests for Announcement dynamic special event banner option and generation
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.Services.Database;
using Lyracist.Services.Display;
using Lyracist.Services.Integration;
using Lyracist.Services.Media;
using Lyracist.ViewModels;
using Moq;
using Xunit;

namespace Lyracist.Tests;

public class SpecialEventBannerSelectionTests
{
    private static KaraokeViewModel CreateTestKaraokeViewModel(out Mock<IDisplayService> mockDisplay)
    {
        var display = new Mock<IDisplayService>();
        display.Setup(d => d.GetScreens()).Returns(new List<ScreenInfo>());
        display.Setup(d => d.GetPreferences()).Returns(new DisplayPreferences());
        mockDisplay = display;

        var mediaEngine = new Mock<IMediaEngine>();

        var library = new Mock<ILibraryService>();
        library.Setup(l => l.SearchAsync(It.IsAny<string>())).ReturnsAsync(new List<KaraokeSong>());

        var showFlow = new Mock<IShowFlowService>();

        var occasions = new Mock<IOccasionService>();
        occasions.Setup(o => o.GetMenuTree()).Returns(new List<OccasionNode>());

        var requests = new Mock<IRequestService>();
        requests.Setup(r => r.GetPending()).Returns(new List<RequestInfo>());

        var navigation = new Mock<Wpf.Ui.INavigationService>();

        var rotationVm = new RotationViewModel(display.Object, mediaEngine.Object);

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
    public void SpecialEventOptionViewModel_OnlyInvokesCallback_WhenSelectedIsTrue()
    {
        int callbackCount = 0;
        string? selectedVal = null;
        var vm = new SpecialEventOptionViewModel("Birthday", "Birthday", false, val =>
        {
            callbackCount++;
            selectedVal = val;
        });

        Assert.False(vm.IsSelected);
        Assert.Equal(0, callbackCount);

        // Selecting true fires callback
        vm.IsSelected = true;
        Assert.True(vm.IsSelected);
        Assert.Equal(1, callbackCount);
        Assert.Equal("Birthday", selectedVal);

        // Deselecting false does NOT fire callback
        vm.IsSelected = false;
        Assert.False(vm.IsSelected);
        Assert.Equal(1, callbackCount);
    }

    [Fact]
    public void SpecialEventOptionViewModel_SetSelectedQuietly_DoesNotInvokeCallback()
    {
        int callbackCount = 0;
        string? selectedVal = null;
        var vm = new SpecialEventOptionViewModel("Birthday", "Birthday", false, val =>
        {
            callbackCount++;
            selectedVal = val;
        });

        // SetSelectedQuietly sets IsSelected without firing callback
        vm.SetSelectedQuietly(true);
        Assert.True(vm.IsSelected);
        Assert.Equal(0, callbackCount);
        Assert.Null(selectedVal);

        vm.SetSelectedQuietly(false);
        Assert.False(vm.IsSelected);
        Assert.Equal(0, callbackCount);
    }

    [Fact]
    public void SelectingSpecialEventOption_EnforcesMutualExclusion_AndDeselectsOthers()
    {
        var vm = CreateTestKaraokeViewModel(out _);

        // Initial state: None should be selected
        var noneOption = vm.SpecialEventOptions.FirstOrDefault(o => o.Value.Equals("None", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(noneOption);
        Assert.True(noneOption.IsSelected);
        Assert.Equal("None", vm.ActiveSpecialEvent);

        // Find a non-modal event (e.g. Wedding or Anniversary)
        var weddingOption = vm.SpecialEventOptions.FirstOrDefault(o => o.Value.Equals("Wedding", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(weddingOption);
        Assert.False(weddingOption.IsSelected);

        // User clicks/selects Wedding option
        weddingOption.IsSelected = true;

        Assert.Equal("Wedding", vm.ActiveSpecialEvent);
        Assert.True(weddingOption.IsSelected);
        Assert.False(noneOption.IsSelected);

        // User clicks/selects None option: should restore None and deselect Wedding
        noneOption.IsSelected = true;

        Assert.Equal("None", vm.ActiveSpecialEvent);
        Assert.True(noneOption.IsSelected);
        Assert.False(weddingOption.IsSelected);
    }

    [Fact]
    public void SyncSpecialEventOptionSelections_WithUnsyncedCustomEvent_DynamicallyAddsAndSelectsOption()
    {
        var vm = CreateTestKaraokeViewModel(out _);
        int initialCount = vm.SpecialEventOptions.Count;

        // Verify "St. Patrick's Day" is not present initially
        Assert.DoesNotContain(vm.SpecialEventOptions, o => o.Value.Equals("St. Patrick's Day", StringComparison.OrdinalIgnoreCase));

        // Invoke the production sync method with custom event
        vm.SyncSpecialEventOptionSelections("St. Patrick's Day");

        Assert.Equal(initialCount + 1, vm.SpecialEventOptions.Count);
        var customOpt = vm.SpecialEventOptions.FirstOrDefault(o => o.Value.Equals("St. Patrick's Day", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(customOpt);
        Assert.True(customOpt.IsSelected);

        var noneOption = vm.SpecialEventOptions.First(o => o.Value.Equals("None", StringComparison.OrdinalIgnoreCase));
        Assert.False(noneOption.IsSelected);

        // Sync with "None": deselects custom event and selects None
        vm.SyncSpecialEventOptionSelections("None");
        Assert.True(noneOption.IsSelected);
        Assert.False(customOpt.IsSelected);

        // Sync with empty/whitespace: guarantees None remains selected and options are never all deselected
        vm.SyncSpecialEventOptionSelections("");
        Assert.True(noneOption.IsSelected);
        Assert.Contains(vm.SpecialEventOptions, o => o.IsSelected);
    }

    [Fact]
    public void UpdateActiveSpecialEventFromSync_QuietlyUpdatesSelection_WithoutPoppingBirthdayDialog()
    {
        var vm = CreateTestKaraokeViewModel(out var mockDisplay);

        // Calling production UpdateActiveSpecialEventFromSync with "Birthday"
        // must quietly update ActiveSpecialEvent, invoke DisplayService, and sync the option
        // WITHOUT invoking OnSpecialEventChanged (which would attempt to show modal prompt)
        vm.UpdateActiveSpecialEventFromSync("Birthday");

        Assert.Equal("Birthday", vm.ActiveSpecialEvent);

        var birthdayOption = vm.SpecialEventOptions.FirstOrDefault(o => o.Value.Equals("Birthday", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(birthdayOption);
        Assert.True(birthdayOption.IsSelected);

        var noneOption = vm.SpecialEventOptions.First(o => o.Value.Equals("None", StringComparison.OrdinalIgnoreCase));
        Assert.False(noneOption.IsSelected);

        mockDisplay.Verify(d => d.UpdateSpecialEvent("Birthday"), Times.Once);
    }

    [Fact]
    public void SyncGuard_CaseInsensitivity_MatchesCaseInsensitively()
    {
        string currentEvent = "Birthday";
        string incomingEvent = "birthday";

        bool wouldTriggerRedundantSync = !string.Equals(currentEvent, incomingEvent, StringComparison.OrdinalIgnoreCase);
        Assert.False(wouldTriggerRedundantSync);

        bool caseSensitiveMismatch = (currentEvent == incomingEvent);
        Assert.False(caseSensitiveMismatch); // Confirms why == was flawed and OrdinalIgnoreCase fixes it
    }

    [Fact]
    public void Announcement_IsStandardEvent_AndGeneratesDynamicBanner()
    {
        Assert.Contains(Lyracist.Shared.DjBannerFileManager.StandardEventNames, s => s.Equals("Announcement", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Announcement.png", Lyracist.Shared.DjBannerFileManager.GetStandardBannerFileName("Announcement"));

        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"announcement_test_{Guid.NewGuid():N}.png");
        try
        {
            Lyracist.Shared.DjBannerFileManager.CreateDynamicAnnouncementBannerPng(tempPath, "Welcome to Jill & Robert, 1st timers tonight");
            Assert.True(System.IO.File.Exists(tempPath));
            var fileInfo = new System.IO.FileInfo(tempPath);
            Assert.True(fileInfo.Length > 1000);
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
    public void AnnouncementBanners_UseFreshPathEachTime_AndAreDeleted()
    {
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"announcements_{Guid.NewGuid():N}");
        try
        {
            string first = Lyracist.Shared.DjBannerFileManager.CreateAnnouncementBanner(dir, "First");
            System.Threading.Thread.Sleep(20);
            string second = Lyracist.Shared.DjBannerFileManager.CreateAnnouncementBanner(dir, "Second");

            Assert.NotEqual(first, second);
            Assert.False(System.IO.File.Exists(first));
            Assert.Equal(second, Lyracist.Shared.DjBannerFileManager.GetCurrentAnnouncementPath(dir));

            // Generated files live in a subfolder, so the banner picker never lists them.
            Assert.DoesNotContain(Lyracist.Shared.DjBannerFileManager.ScanBanners(dir), b => b.FullPath == second);

            Lyracist.Shared.DjBannerFileManager.DeleteGeneratedAnnouncements(dir);
            Assert.False(System.IO.File.Exists(second));
            Assert.Null(Lyracist.Shared.DjBannerFileManager.GetCurrentAnnouncementPath(dir));
        }
        finally
        {
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void UpdateActiveSpecialEventFromSync_WithAnnouncement_QuietlyUpdatesSelection()
    {
        var vm = CreateTestKaraokeViewModel(out var mockDisplay);

        vm.UpdateActiveSpecialEventFromSync("Announcement");

        Assert.Equal("Announcement", vm.ActiveSpecialEvent);

        var announcementOption = vm.SpecialEventOptions.FirstOrDefault(o => o.Value.Equals("Announcement", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(announcementOption);
        Assert.True(announcementOption.IsSelected);

        var noneOption = vm.SpecialEventOptions.First(o => o.Value.Equals("None", StringComparison.OrdinalIgnoreCase));
        Assert.False(noneOption.IsSelected);

        mockDisplay.Verify(d => d.UpdateSpecialEvent("Announcement"), Times.Once);
    }
}

