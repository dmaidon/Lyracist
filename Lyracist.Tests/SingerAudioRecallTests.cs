// Created on Sep 6, 2026 @ 07:37:30 -> Unit tests for per-singer key/tempo recall and history tracking
using System;
using System.IO;
using System.Linq;
using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.Services.Database;
using Lyracist.Services.Display;
using Lyracist.ViewModels;
using Moq;
using Xunit;

namespace Lyracist.Tests;

public class SingerAudioRecallTests
{
    private static RotationViewModel CreateRotationViewModel()
    {
        var display = new Mock<IDisplayService>();
        var mediaEngine = new Mock<IMediaEngine>();
        var vm = new RotationViewModel(display.Object, mediaEngine.Object);
        var field = typeof(RotationViewModel).GetField("_floatCurrentSingerToTop",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("RotationViewModel._floatCurrentSingerToTop field not found.");
        field.SetValue(vm, false);
        vm.Rotation.Clear();
        vm.InactiveSingers.Clear();
        return vm;
    }

    [Fact]
    public void SingerHistoryService_SavesAndRecallsKeyAndTempo()
    {
        SingerHistoryService.EnsureTableCreated();

        string singer = "TestSinger_" + Guid.NewGuid().ToString("N")[..8];
        string song = "My Song";
        string artist = "My Artist";

        SingerHistoryService.SaveHistory(singer, song, artist, "Local", string.Empty, key: "+2", tempo: 1.15);

        var recalled = SingerHistoryService.GetSongHistory(singer, song, artist);
        Assert.NotNull(recalled);
        Assert.Equal("+2", recalled.Value.Key);
        Assert.Equal(1.15, recalled.Value.Tempo, 2);

        var historyList = SingerHistoryService.GetHistory(singer);
        Assert.NotEmpty(historyList);
        var entry = historyList.First(h => h.SongTitle == song);
        Assert.Equal("+2", entry.Key);
        Assert.Equal(1.15, entry.Tempo, 2);
    }

    [Fact]
    public void RotationViewModel_AddSinger_AutoRecallsKeyAndTempoFromHistory()
    {
        SingerHistoryService.EnsureTableCreated();

        string singer = "RecallSinger_" + Guid.NewGuid().ToString("N")[..8];
        string song = "Sweet Caroline";
        string artist = "Neil Diamond";

        // Pre-seed singer's performance history with a specific key and tempo
        SingerHistoryService.SaveHistory(singer, song, artist, "Local", string.Empty, key: "-1", tempo: 0.95);

        var vm = CreateRotationViewModel();
        // Add singer without specifying key/tempo (default "0" and 1.0)
        vm.AddSinger(singer, song, artist, "0", string.Empty);

        var addedSinger = vm.Rotation.FirstOrDefault(s => s.Name.Equals(singer, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(addedSinger);
        Assert.Equal("-1", addedSinger.Key);
        Assert.Equal(0.95, addedSinger.Tempo, 2);
    }

    [Fact]
    public void RotationViewModel_MarkFinished_RecordsPerformanceKeyAndTempo()
    {
        SingerHistoryService.EnsureTableCreated();

        string singer = "FinishSinger_" + Guid.NewGuid().ToString("N")[..8];
        string song = "Don't Stop Believin'";
        string artist = "Journey";

        var vm = CreateRotationViewModel();
        vm.AddSinger(singer, song, artist, "+3", string.Empty, tempo: 1.08);

        var addedSinger = vm.Rotation.First(s => s.Name.Equals(singer, StringComparison.OrdinalIgnoreCase));
        vm.DoneSingerCommand.Execute(addedSinger);

        var performed = vm.SessionPerformedSongs.FirstOrDefault(p => p.SingerName.Equals(singer, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(performed);
        Assert.Equal("+3", performed.Key);
        Assert.Equal(1.08, performed.Tempo, 2);
        Assert.Contains("[Key: +3]", performed.FormattedText);
        Assert.Contains("[Speed: 1.1x]", performed.FormattedText);
    }
}
