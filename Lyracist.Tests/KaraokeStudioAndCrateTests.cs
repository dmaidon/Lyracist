// Created on Nov 28, 2026 -> Regression tests for Karaoke Studio, FFmpeg filters, crate list, and Undo Finish
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Lyracist.Core.Interfaces;
using Lyracist.Data.Services;
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.Services.Media;
using Lyracist.ViewModels;
using Moq;

namespace Lyracist.Tests;

public class KaraokeStudioAndCrateTests
{
    [Fact]
    public void ParseLyrics_SkipsLrcHeadersAndReadsSingerTags()
    {
        var lines = StudioLyricLine.ParseLyrics("[ti:Song]\n[ar:Band]\n[00:10.50][S2] Hello | Hola\n[00:15.00][BOTH] Together");

        Assert.Equal(2, lines.Count);
        Assert.Equal("Hello", lines[0].LyricText);
        Assert.Equal("Hola", lines[0].TranslationText);
        Assert.Equal("Singer 2", lines[0].SingerAssignment);
        Assert.Equal(10.5, lines[0].StartTimeSeconds, 3);
        Assert.Equal("Both (Duet)", lines[1].SingerAssignment);
    }

    [Fact]
    public void ScaleForTempo_MovesLyricsWithTheAudioWithoutChangingTheOriginals()
    {
        var original = new StudioLyricLine { StartTimeSeconds = 40, EndTimeSeconds = 44, LyricText = "x" };

        var scaled = StudioLyricLine.ScaleForTempo([original], 80);

        Assert.Equal(50, scaled[0].StartTimeSeconds, 3);
        Assert.Equal(55, scaled[0].EndTimeSeconds, 3);
        Assert.Equal(40, original.StartTimeSeconds, 3);
    }

    [Fact]
    public void PitchFilter_ResamplesBeforeChangingTheSampleRate()
    {
        string filter = FfmpegRunner.BuildPitchTempoFilter(3, 1.0);

        Assert.StartsWith("aresample=44100,asetrate=", filter);
        Assert.Empty(FfmpegRunner.BuildPitchTempoFilter(0, 1.0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Export_LeavesTheSourceTrackUntouched_EvenWithAMatchingName(int exportFormat)
    {
        if (!IsFfmpegAvailable()) Assert.Skip("FFmpeg is not installed.");

        string dir = Directory.CreateTempSubdirectory("studio_test_").FullName;
        try
        {
            // 48 kHz source named exactly like the export, in the export folder - the worst case.
            string source = Path.Combine(dir, "Band - Song.mp3");
            await FfmpegRunner.RunAsync($"-y -f lavfi -i sine=frequency=440:duration=12:sample_rate=48000 -ac 2 \"{source}\"", TestContext.Current.CancellationToken);
            byte[] before = await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken);

            var options = new KaraokeStudioExportOptions
            {
                InputAudioPath = source,
                OutputDirectory = dir,
                SongTitle = "Song",
                ArtistName = "Band",
                VocalIsolationMode = 1,
                GuideVocalPercent = 40,
                KeySemitones = 2,
                TempoPercent = 50,
                ExportFormatIndex = exportFormat,
                Lines = [new StudioLyricLine { StartTimeSeconds = 4, EndTimeSeconds = 7, LyricText = "Hello" }]
            };

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            string result = await KaraokeStudioExporter.ExportCustomKaraokeTrackAsync(options, null, timeout.Token);

            Assert.NotEqual(source, result);
            Assert.True(new FileInfo(result).Length > 0);
            Assert.Equal(before, await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken));
            using var tag = TagLib.File.Create(result);
            Assert.InRange(tag.Properties.Duration.TotalSeconds, 22, 26); // 12 s at 50% tempo
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Ffmpeg_FailureIsReportedInsteadOfIgnored()
    {
        if (!IsFfmpegAvailable()) Assert.Skip("FFmpeg is not installed.");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FfmpegRunner.RunAsync("-y -i does-not-exist.mp3 out.mp3", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CrateList_SyncKeepsExistingEntries()
    {
        var target = new ObservableCollection<string> { CrateList.AllCrates, "Country", "Old" };
        bool selectedRemoved = false;
        target.CollectionChanged += (_, e) =>
            selectedRemoved |= e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset
                || (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems!.Contains("Country"));

        CrateList.Sync(target, ["Blues", "Country"]);

        Assert.Equal([CrateList.AllCrates, "Blues", "Country"], target);
        Assert.False(selectedRemoved);
    }

    [Fact]
    public void Undo_IsForgottenWhenTheRotationIsCleared()
    {
        var vm = new RotationViewModel(new Mock<IDisplayService>().Object, new Mock<IMediaEngine>().Object);
        vm.Rotation.Clear();
        var singer = new Singer { Name = "Alice", SongTitle = "Song" };
        vm.Rotation.Add(singer);
        vm.DoneSingerCommand.Execute(singer);
        Assert.True(vm.CanUndoLastFinish);

        vm.Rotation.Clear();

        Assert.False(vm.CanUndoLastFinish);
    }

    [Fact]
    public async Task CrateFilter_MatchesOnlyTheImmediateFolder_AndFuzzySearchStillFindsTypos()
    {
        Lyracist.Data.LyracistDbContext.EnsureSchemaAndRepair();
        string crate = $"Crate{Guid.NewGuid():N}";
        using var context = new Lyracist.Data.LyracistDbContext();
        var search = new SearchService(context);
        var songs = new List<Lyracist.Data.Models.Song>
        {
            new() { Title = "Zyxwvut Anthem", Artist = "Direct", FilePath = Path.Combine(Path.GetTempPath(), crate, "a.mp3"), IsKaraoke = true },
            new() { Title = "Nested", Artist = "Deeper", FilePath = Path.Combine(Path.GetTempPath(), crate, "sub", "b.mp3"), IsKaraoke = true }
        };
        try
        {
            context.Songs.AddRange(songs);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await search.IndexSongsBatch(songs);

            Assert.Equal("Direct", Assert.Single(search.SearchSync("", true, crate)).Artist);
            Assert.Equal("Direct", Assert.Single(await search.Search("", true, crate)).Artist);
            Assert.Equal("Direct", Assert.Single(search.SearchSync("zyxwvut", true, crate)).Artist);
            Assert.Equal("Direct", Assert.Single(await search.Search("zyxwuvt", true, crate)).Artist);
        }
        finally
        {
            await search.RemoveSongsFromIndexBatch(songs.Select(s => s.SongId));
            context.Songs.RemoveRange(songs);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void RevertCompletedPerformance_UndoesOneRecording()
    {
        Lyracist.Services.Database.SingerHistoryService.EnsureTableCreated();
        string name = "Undo Tester" + new string(Guid.NewGuid().ToString("N").Where(char.IsLetter).ToArray());
        try
        {
            Lyracist.Services.Database.SingerHistoryService.RecordCompletedPerformance(name, "Some Song", "Some Artist", "Venue");
            Lyracist.Services.Database.SingerHistoryService.RecordCompletedPerformance(name, "Some Song", "Some Artist", "Venue");

            Lyracist.Services.Database.SingerHistoryService.RevertCompletedPerformance(name, "Some Song", "Some Artist");
            Assert.Equal(1, Assert.Single(Lyracist.Services.Database.SingerHistoryService.GetHistory(name)).TimesPerformed);

            Lyracist.Services.Database.SingerHistoryService.RevertCompletedPerformance(name, "Some Song", "Some Artist");
            Assert.Empty(Lyracist.Services.Database.SingerHistoryService.GetHistory(name));
        }
        finally
        {
            Lyracist.Services.Database.SingerHistoryService.DeleteSingerAndHistory(name);
        }
    }

    private static bool IsFfmpegAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(FfmpegRunner.ResolveFfmpegPath(), "-version")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            });
            process!.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
