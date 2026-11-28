// Created on Nov 28, 2026 -> Regression tests for Undo Finish and the Originals upload helpers
using KSRotation.Models;
using KSRotation.Services;
using KSRotation.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace KSRotation.Tests;

public class UndoFinishAndUploadTests
{
    private static (MainViewModel Vm, SingerEntry First, SingerEntry Second) CreateRotation()
    {
        var vm = new MainViewModel();
        vm.Singers.Clear();
        vm.IncomingRequests.Clear();
        vm.AutoAcceptRequests = false;
        var first = new SingerEntry { Name = "Alice", Song = "First Song", Artist = "Artist A", IsCurrent = true };
        var second = new SingerEntry { Name = "Bob", Song = "Other Song", Artist = "Artist B" };
        vm.Singers.Add(first);
        vm.Singers.Add(second);
        return (vm, first, second);
    }

    [Fact]
    public void Undo_ReturnsARequestThatFinishingMergedIn()
    {
        var (vm, first, _) = CreateRotation();
        var request = new PatronRequest { Name = "Alice", Songs = [new RequestedSong("Requested Song", "Artist C")] };
        vm.IncomingRequests.Add(request);

        vm.FinishSingerSongCommand.Execute(first);
        Assert.Empty(vm.IncomingRequests);
        Assert.Equal("Requested Song", first.Song);

        vm.UndoLastFinish();

        Assert.Equal("First Song", first.Song);
        Assert.Same(request, Assert.Single(vm.IncomingRequests));
        Assert.Empty(first.QueuedSongs);
        Assert.False(first.Song1Completed);
        Assert.True(first.IsCurrent);
    }

    [Fact]
    public void Undo_RemovesRequestSongsAddedBehindAQueuedSong()
    {
        var (vm, first, _) = CreateRotation();
        first.QueuedSongs.Add(new QueuedSong("Queued Song", "Artist Q"));
        vm.IncomingRequests.Add(new PatronRequest { Name = "Alice", Songs = [new RequestedSong("Requested Song", "Artist C")] });

        vm.FinishSingerSongCommand.Execute(first);
        vm.UndoLastFinish();

        Assert.Equal("Queued Song", Assert.Single(first.QueuedSongs).Song);
        Assert.Single(vm.IncomingRequests);
    }

    [Fact]
    public void Undo_KeepsRoundTenWhenAllRoundsWereAlreadyComplete()
    {
        var (vm, first, _) = CreateRotation();
        for (int round = 1; round <= 10; round++) first.SetRoundCompleted(round, true);

        vm.FinishSingerSongCommand.Execute(first);
        vm.UndoLastFinish();

        Assert.True(first.Song10Completed);
    }

    [Fact]
    public void Undo_IsForgottenWhenTheSingerIsRemoved()
    {
        var (vm, first, _) = CreateRotation();
        vm.FinishSingerSongCommand.Execute(first);

        vm.RemoveSingerDirectly(first);

        Assert.False(vm.CanUndoLastFinish);
        vm.UndoLastFinish();
        Assert.DoesNotContain(first, vm.Singers);
    }

    [Fact]
    public void Undo_IsForgottenWhenTheRotationIsCleared()
    {
        var (vm, first, _) = CreateRotation();
        vm.FinishSingerSongCommand.Execute(first);

        vm.Singers.Clear();

        Assert.False(vm.CanUndoLastFinish);
    }

    [Fact]
    public void UploadBodyLimit_FitsAMaximumSizeTrack()
    {
        string base64 = Convert.ToBase64String(new byte[OriginalUploadStore.MaxTrackBytes]);

        Assert.True(OriginalUploadStore.MaxUploadBodyBytes > base64.Length + 4096);
    }

    [Fact]
    public void UploadPath_NeverReplacesAnExistingFile()
    {
        string dir = Directory.CreateTempSubdirectory("originals_test_").FullName;
        try
        {
            string first = OriginalUploadStore.GetUniquePath(dir, "AC/DC", "Why?", ".mp3");
            File.WriteAllText(first, "x");
            string second = OriginalUploadStore.GetUniquePath(dir, "AC/DC", "Why?", ".mp3");

            Assert.NotEqual(first, second);
            Assert.Equal(dir, Path.GetDirectoryName(second));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void UploadZip_WithoutLyricsIsRejected()
    {
        Assert.False(OriginalUploadStore.IsPlayableZip(new byte[128]));
    }
}
