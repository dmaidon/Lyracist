// Created on Aug 20, 2026 @ 10:00:00 -> Add unit tests for AutoAdvanceManager state machine, rotation helpers, and safety rules
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
using Xunit;

namespace Lyracist.Tests;

public class AutoAdvanceManagerTests
{
    [Fact]
    public void AutoAdvanceState_EnumValues_AreCorrect()
    {
        Assert.Equal(0, (int)AutoAdvanceState.Idle);
        Assert.Equal(1, (int)AutoAdvanceState.GracePeriod);
        Assert.Equal(2, (int)AutoAdvanceState.WaitingForSongSelection);
        Assert.Equal(3, (int)AutoAdvanceState.ReadyToStart);
        Assert.Equal(4, (int)AutoAdvanceState.StartingSong);
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
}
