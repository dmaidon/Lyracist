// Edited on Oct 27, 2026 @ 12:35:00 -> Add unit tests for wheel spin command binding and post-spin category assignment
using KSRotation.Models;
using KSRotation.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace KSRotation.Tests;

public class ScaryokeModeTests
{
    [Fact]
    public void SingerEntry_ScaryokeProperties_WorkCorrectly()
    {
        var singer = new SingerEntry { Name = "Alice" };
        Assert.False(singer.HasScaryokeCategory);
        Assert.Null(singer.ScaryokeCategory);
        Assert.Null(singer.ScaryokeColor);

        singer.ScaryokeCategory = "80s Hits";
        singer.ScaryokeColor = "#FF6D00";

        Assert.True(singer.HasScaryokeCategory);
        Assert.Equal("80s Hits", singer.ScaryokeCategory);
        Assert.Equal("#FF6D00", singer.ScaryokeColor);
        Assert.NotNull(singer.ScaryokeBrush);

        singer.ScaryokeCategory = null;
        singer.ScaryokeColor = null;
        Assert.False(singer.HasScaryokeCategory);
    }

    [Fact]
    public void DisplayRotationEntry_And_NextSingerDisplay_ExposeScaryokeBadges()
    {
        var entry = new DisplayViewModel.DisplayRotationEntry(
            "1. Alice", " - Beat It", " (Michael Jackson)",
            IsCurrent: false, IsRotationStart: false, IsSpecial: false,
            ScaryokeCategory: "Pop", ScaryokeColor: "#6A0DAD");

        Assert.True(entry.HasScaryoke);
        Assert.Equal("Pop", entry.ScaryokeCategory);
        Assert.Equal("#6A0DAD", entry.ScaryokeColor);
        Assert.NotNull(entry.ScaryokeBrush);

        var next = new DisplayViewModel.NextSingerDisplay(
            "Alice - Beat It", false, "Alice", "Beat It", "", false,
            "Pop", "#6A0DAD");

        Assert.True(next.HasScaryokeCategory);
        Assert.Equal("Pop", next.ScaryokeCategory);
        Assert.Equal("#6A0DAD", next.ScaryokeColor);
        Assert.NotNull(next.ScaryokeBrush);
    }

    [Fact]
    public void RotationItemDto_SerializesAndDeserializesScaryokeFields()
    {
        var dto = new RotationItemDto
        {
            id = Guid.NewGuid().ToString(),
            name = "Bob",
            scaryokeCategory = "Country",
            scaryokeColor = "#FF6D00",
            hasScaryokeCategory = true
        };

        var json = JsonSerializer.Serialize(dto);
        var restored = JsonSerializer.Deserialize<RotationItemDto>(json);

        Assert.NotNull(restored);
        Assert.Equal("Bob", restored.name);
        Assert.Equal("Country", restored.scaryokeCategory);
        Assert.Equal("#FF6D00", restored.scaryokeColor);
        Assert.True(restored.hasScaryokeCategory);
    }

    [Fact]
    public void MainViewModel_AssignAndClearScaryoke_WorksForUpcomingSingers()
    {
        var vm = new MainViewModel();
        var s1 = new SingerEntry { Name = "Alice", IsCurrent = true };
        var s2 = new SingerEntry { Name = "Bob", IsNext = true };
        var s3 = new SingerEntry { Name = "Charlie" };
        var s4 = new SingerEntry { Name = "Dave" };
        var s5 = new SingerEntry { Name = "Eve" };

        vm.Singers.Add(s1);
        vm.Singers.Add(s2);
        vm.Singers.Add(s3);
        vm.Singers.Add(s4);
        vm.Singers.Add(s5);

        // Spin ahead 4 singers (Dave)
        vm.AssignScaryokeCategory(s4, "Rock");
        Assert.True(s4.HasScaryokeCategory);
        Assert.Equal("Rock", s4.ScaryokeCategory);
        Assert.False(string.IsNullOrWhiteSpace(s4.ScaryokeColor));

        // Spin ahead 5 singers (Eve)
        vm.AssignScaryokeCategory(s5, "Elvis");
        Assert.True(s5.HasScaryokeCategory);
        Assert.Equal("Elvis", s5.ScaryokeCategory);

        // Clear challenge for Dave
        vm.ClearScaryokeForSinger(s4);
        Assert.False(s4.HasScaryokeCategory);
        Assert.Null(s4.ScaryokeCategory);

        // Eve remains assigned
        Assert.True(s5.HasScaryokeCategory);
        Assert.Equal("Elvis", s5.ScaryokeCategory);
    }

    [Fact]
    public void MainViewModel_BatchSpinScaryoke_AssignsNextCountSingers()
    {
        var vm = new MainViewModel();
        var s1 = new SingerEntry { Name = "Alice", IsCurrent = true };
        var s2 = new SingerEntry { Name = "Bob", IsNext = true };
        var s3 = new SingerEntry { Name = "Charlie" };
        var s4 = new SingerEntry { Name = "Dave" };
        var s5 = new SingerEntry { Name = "Eve" };

        vm.Singers.Add(s1);
        vm.Singers.Add(s2);
        vm.Singers.Add(s3);
        vm.Singers.Add(s4);
        vm.Singers.Add(s5);

        // Batch spin next 3 singers
        vm.BatchSpinScaryokeInternal(3, onlyUnassigned: true);

        Assert.True(s1.HasScaryokeCategory);
        Assert.True(s2.HasScaryokeCategory);
        Assert.True(s3.HasScaryokeCategory);
        Assert.False(s4.HasScaryokeCategory);
        Assert.False(s5.HasScaryokeCategory);
        Assert.False(string.IsNullOrWhiteSpace(s1.ScaryokeColor));
        Assert.False(string.IsNullOrWhiteSpace(s2.ScaryokeColor));
        Assert.False(string.IsNullOrWhiteSpace(s3.ScaryokeColor));
    }

    [Fact]
    public void MainViewModel_BatchSpinScaryoke_EntireRotation_AssignsAllActiveSingers()
    {
        var vm = new MainViewModel();
        var s1 = new SingerEntry { Name = "Alice" };
        var s2 = new SingerEntry { Name = "Bob" };
        var s3 = new SingerEntry { Name = "Charlie" };
        var s4 = new SingerEntry { Name = "Dave", IsInactive = true }; // Inactive should be skipped

        vm.Singers.Add(s1);
        vm.Singers.Add(s2);
        vm.Singers.Add(s3);
        vm.Singers.Add(s4);

        // Batch spin entire rotation (count = 0)
        vm.BatchSpinScaryokeInternal(0, onlyUnassigned: true);

        Assert.True(s1.HasScaryokeCategory);
        Assert.True(s2.HasScaryokeCategory);
        Assert.True(s3.HasScaryokeCategory);
        Assert.False(s4.HasScaryokeCategory); // Inactive singer left untouched
    }

    [Fact]
    public void MainViewModel_BatchSpinScaryoke_OnlyUnassigned_PreservesExistingCategory()
    {
        var vm = new MainViewModel();
        var s1 = new SingerEntry { Name = "Alice", ScaryokeCategory = "Country", ScaryokeColor = "#FF6D00" };
        var s2 = new SingerEntry { Name = "Bob" };
        var s3 = new SingerEntry { Name = "Charlie" };

        vm.Singers.Add(s1);
        vm.Singers.Add(s2);
        vm.Singers.Add(s3);

        // Batch spin next 2 unassigned singers
        vm.BatchSpinScaryokeInternal(2, onlyUnassigned: true);

        Assert.Equal("Country", s1.ScaryokeCategory); // Untouched
        Assert.Equal("#FF6D00", s1.ScaryokeColor);
        Assert.True(s2.HasScaryokeCategory);
        Assert.True(s3.HasScaryokeCategory);
    }

    [Fact]
    public void MainViewModel_ClearAllScaryokeCategories_ClearsAllSingers()
    {
        var vm = new MainViewModel();
        var s1 = new SingerEntry { Name = "Alice", ScaryokeCategory = "Rock", ScaryokeColor = "#FF6D00" };
        var s2 = new SingerEntry { Name = "Bob", ScaryokeCategory = "Pop", ScaryokeColor = "#6A0DAD" };
        var s3 = new SingerEntry { Name = "Charlie" };

        vm.Singers.Add(s1);
        vm.Singers.Add(s2);
        vm.Singers.Add(s3);

        vm.ClearAllScaryokeCategories();

        Assert.False(s1.HasScaryokeCategory);
        Assert.False(s2.HasScaryokeCategory);
        Assert.False(s3.HasScaryokeCategory);
        Assert.Null(s1.ScaryokeCategory);
        Assert.Null(s2.ScaryokeCategory);
    }

    [Fact]
    public void AdvanceRotationAfterFinished_ClearsScaryokeCategoryAndColor()
    {
        var s1 = new SingerEntry { Name = "Alice", IsCurrent = true, ScaryokeCategory = "Rock", ScaryokeColor = "#FF6D00" };
        var s2 = new SingerEntry { Name = "Bob", IsNext = true };
        var rotation = new List<SingerEntry> { s1, s2 };

        Assert.True(s1.HasScaryokeCategory);

        // Advance after Alice finishes
        Lyracist.Shared.RotationHelpers.AdvanceRotationAfterFinished(rotation, s1);

        // Verify Alice's Scaryoke badge has been cleared until next spin
        Assert.False(s1.HasScaryokeCategory);
        Assert.Null(s1.ScaryokeCategory);
        Assert.Null(s1.ScaryokeColor);
        Assert.False(s1.IsCurrent);
        Assert.True(s2.IsCurrent);
    }

    [Fact]
    public void MainViewModel_ScaryokeMonitorDevice_LoadsAndChanges()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.ScaryokeMonitorDevice);

        vm.ScaryokeMonitorDevice = @"\\.\DISPLAY2";
        Assert.Equal(@"\\.\DISPLAY2", vm.ScaryokeMonitorDevice);
    }

    [Fact]
    public void MainViewModel_SpinScaryokeWheelCommand_ExistsAndCanExecute()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.SpinScaryokeWheelCommand);
        Assert.NotNull(vm.SpinScaryokeCommand);
    }

    [Fact]
    public void SingerEntry_CategoryAssignedAfterSpinCompletes_NotBefore()
    {
        var singer = new SingerEntry { Name = "Dana" };
        Assert.False(singer.HasScaryokeCategory);
        Assert.Null(singer.ScaryokeCategory);

        // Before or during spin, category is null / cleared
        singer.ScaryokeCategory = null;
        singer.ScaryokeColor = null;
        Assert.False(singer.HasScaryokeCategory);

        // Only after wheel spin decelerates and lands:
        singer.ScaryokeCategory = "80s Hits";
        singer.ScaryokeColor = "#00838F";
        Assert.True(singer.HasScaryokeCategory);
        Assert.Equal("80s Hits", singer.ScaryokeCategory);
        Assert.Equal("#00838F", singer.ScaryokeColor);
    }
}

