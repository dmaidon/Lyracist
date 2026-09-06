// Edited on Sep 6, 2026 @ 09:01:00 -> Add unit tests for SessionScheduleHelper, session schedules, and last request cutoffs
using System;
using System.IO;
using System.Linq;
using Lyracist.Core.Interfaces;
using Lyracist.Core.Models;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Lyracist.Models;
using Lyracist.Services.Database;
using Lyracist.Services.Display;
using Lyracist.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Lyracist.Tests;

public class SessionDuplicateAndUserTests
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
        vm.SessionPerformedSongs.Clear();
        return vm;
    }

    [Fact]
    public void IsSongInCurrentSession_DetectsQueuedAndPerformedSongs()
    {
        var vm = CreateRotationViewModel();

        // Initially no songs
        Assert.False(vm.IsSongInCurrentSession("Bohemian Rhapsody"));

        // Add to active rotation queue
        vm.Rotation.Add(new Lyracist.Models.Singer
        {
            Name = "Freddie",
            SongTitle = "Bohemian Rhapsody",
            Artist = "Queen"
        });

        // Case-insensitive match on title
        Assert.True(vm.IsSongInCurrentSession("bohemian rhapsody"));
        Assert.True(vm.IsSongInCurrentSession("Bohemian Rhapsody", "Queen"));
        Assert.False(vm.IsSongInCurrentSession("Radio Ga Ga"));

        // Same title, different (known) artist is not a duplicate
        Assert.False(vm.IsSongInCurrentSession("Bohemian Rhapsody", "The Muppets"));

        // Test already performed list
        vm.SessionPerformedSongs.Add(new PerformedSong
        {
            SingerName = "Elton",
            SongTitle = "Rocket Man",
            Artist = "Elton John",
            PerformedAt = DateTime.Now
        });

        Assert.True(vm.IsSongInCurrentSession("Rocket Man"));
        Assert.True(vm.IsSongInCurrentSession("rocket man", "Elton John"));
        Assert.False(vm.IsSongInCurrentSession("Tiny Dancer"));
    }

    [Fact]
    public void SingerHistoryService_MergeHistory_ConsolidatesRecords()
    {
        SingerHistoryService.EnsureTableCreated();

        string sourceSinger = "DuplicateSinger_" + Guid.NewGuid().ToString("N")[..8];
        string targetSinger = "TargetSinger_" + Guid.NewGuid().ToString("N")[..8];

        SingerHistoryService.SaveHistory(sourceSinger, "Song 1", "Artist A", "Local", "", key: "+1", tempo: 1.0);
        SingerHistoryService.SaveHistory(sourceSinger, "Song 2", "Artist B", "Local", "", key: "-2", tempo: 1.1);
        SingerHistoryService.SaveHistory(targetSinger, "Song 3", "Artist C", "Local", "", key: "0", tempo: 1.0);

        var sourceBefore = SingerHistoryService.GetHistory(sourceSinger);
        var targetBefore = SingerHistoryService.GetHistory(targetSinger);

        Assert.Equal(2, sourceBefore.Count);
        Assert.Single(targetBefore);

        // Merge
        SingerHistoryService.MergeHistory(sourceSinger, targetSinger);

        var sourceAfter = SingerHistoryService.GetHistory(sourceSinger);
        var targetAfter = SingerHistoryService.GetHistory(targetSinger);

        Assert.Empty(sourceAfter);
        Assert.Equal(3, targetAfter.Count);
        Assert.Contains(targetAfter, h => h.SongTitle == "Song 1" && h.Key == "+1");
        Assert.Contains(targetAfter, h => h.SongTitle == "Song 2" && h.Key == "-2");
    }

    [Fact]
    public async Task DatabaseService_MergeSingers_MergesAccountsAndCleansUpDuplicate()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<LyracistDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new LyracistDbContext(options);
        context.Database.EnsureCreated();

        var dbService = new DatabaseService(context);

        var target = new Lyracist.Data.Models.Singer
        {
            Name = "Primary Account",
            PinCode = "1234",
            Score = 150,
            TotalSongsSung = 2,
            VocalRange = "Tenor",
            AudioSettings = new SingerAudioSettings { Key = 1, Tempo = 1.05 }
        };

        var duplicate = new Lyracist.Data.Models.Singer
        {
            Name = "Secondary Account",
            PinCode = "",
            Score = 200,
            TotalSongsSung = 3,
            Notes = "Duplicate notes to keep"
        };

        context.Singers.AddRange(target, duplicate);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Merge duplicate into target
        bool success = await dbService.MergeSingers(target.SingerId, duplicate.SingerId);
        Assert.True(success);

        var remainingSingers = await dbService.GetAllSingers();
        Assert.Single(remainingSingers);

        var updatedTarget = remainingSingers.First();
        Assert.Equal("Primary Account", updatedTarget.Name);
        Assert.Equal(350, updatedTarget.Score); // 150 + 200
        Assert.Equal(5, updatedTarget.TotalSongsSung); // 2 + 3
        Assert.Contains("Duplicate notes to keep", updatedTarget.Notes);
    }

    [Fact]
    public void SessionScheduleHelper_TryParseTime_HandlesVariousFormats()
    {
        Assert.True(Lyracist.Shared.SessionScheduleHelper.TryParseTime("8:00 PM", out var t1));
        Assert.Equal(new TimeSpan(20, 0, 0), t1);

        Assert.True(Lyracist.Shared.SessionScheduleHelper.TryParseTime("2:00 AM", out var t2));
        Assert.Equal(new TimeSpan(2, 0, 0), t2);

        Assert.True(Lyracist.Shared.SessionScheduleHelper.TryParseTime("20:30", out var t3));
        Assert.Equal(new TimeSpan(20, 30, 0), t3);

        Assert.True(Lyracist.Shared.SessionScheduleHelper.TryParseTime("01:15", out var t4));
        Assert.Equal(new TimeSpan(1, 15, 0), t4);

        Assert.False(Lyracist.Shared.SessionScheduleHelper.TryParseTime("", out _));
        Assert.False(Lyracist.Shared.SessionScheduleHelper.TryParseTime("invalid", out _));
    }

    [Fact]
    public void SessionScheduleHelper_IsTimeInWindow_HandlesDaytimeAndOvernight()
    {
        // Daytime: 13:00 to 18:00
        var dayStart = new TimeSpan(13, 0, 0);
        var dayStop = new TimeSpan(18, 0, 0);

        Assert.False(Lyracist.Shared.SessionScheduleHelper.IsTimeInWindow(new TimeSpan(12, 0, 0), dayStart, dayStop));
        Assert.True(Lyracist.Shared.SessionScheduleHelper.IsTimeInWindow(new TimeSpan(13, 0, 0), dayStart, dayStop));
        Assert.True(Lyracist.Shared.SessionScheduleHelper.IsTimeInWindow(new TimeSpan(15, 30, 0), dayStart, dayStop));
        Assert.True(Lyracist.Shared.SessionScheduleHelper.IsTimeInWindow(new TimeSpan(18, 0, 0), dayStart, dayStop));
        Assert.False(Lyracist.Shared.SessionScheduleHelper.IsTimeInWindow(new TimeSpan(18, 30, 0), dayStart, dayStop));

        // Overnight: 20:00 (8 PM) to 02:00 (2 AM)
        var nightStart = new TimeSpan(20, 0, 0);
        var nightStop = new TimeSpan(2, 0, 0);

        Assert.False(Lyracist.Shared.SessionScheduleHelper.IsTimeInWindow(new TimeSpan(19, 0, 0), nightStart, nightStop));
        Assert.True(Lyracist.Shared.SessionScheduleHelper.IsTimeInWindow(new TimeSpan(20, 0, 0), nightStart, nightStop));
        Assert.True(Lyracist.Shared.SessionScheduleHelper.IsTimeInWindow(new TimeSpan(23, 59, 0), nightStart, nightStop));
        Assert.True(Lyracist.Shared.SessionScheduleHelper.IsTimeInWindow(new TimeSpan(0, 30, 0), nightStart, nightStop));
        Assert.True(Lyracist.Shared.SessionScheduleHelper.IsTimeInWindow(new TimeSpan(2, 0, 0), nightStart, nightStop));
        Assert.False(Lyracist.Shared.SessionScheduleHelper.IsTimeInWindow(new TimeSpan(2, 30, 0), nightStart, nightStop));
    }

    [Fact]
    public void SessionScheduleHelper_IsRequestSubmissionAllowed_SessionScheduleEvaluation()
    {
        // Session: 8:00 PM to 2:00 AM
        var testDate = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Local);

        // Before session start (7:30 PM) -> blocked
        var beforeStart = testDate.Add(new TimeSpan(19, 30, 0));
        bool allowedBefore = Lyracist.Shared.SessionScheduleHelper.IsRequestSubmissionAllowed(
            enableSessionSchedule: true,
            sessionStartTime: "8:00 PM",
            sessionStopTime: "2:00 AM",
            enableLastRequestTime: false,
            lastRequestTime: "1:30 AM",
            out string reasonBefore,
            now: beforeStart);

        Assert.False(allowedBefore);
        Assert.Contains("currently closed", reasonBefore, StringComparison.OrdinalIgnoreCase);

        // During session (10:00 PM) -> allowed
        var midSession = testDate.Add(new TimeSpan(22, 0, 0));
        bool allowedMid = Lyracist.Shared.SessionScheduleHelper.IsRequestSubmissionAllowed(
            enableSessionSchedule: true,
            sessionStartTime: "8:00 PM",
            sessionStopTime: "2:00 AM",
            enableLastRequestTime: false,
            lastRequestTime: "1:30 AM",
            out string reasonMid,
            now: midSession);

        Assert.True(allowedMid);
        Assert.Empty(reasonMid);

        // After midnight during session (1:00 AM) -> allowed
        var postMidnight = testDate.Add(new TimeSpan(1, 0, 0));
        bool allowedPostMidnight = Lyracist.Shared.SessionScheduleHelper.IsRequestSubmissionAllowed(
            enableSessionSchedule: true,
            sessionStartTime: "8:00 PM",
            sessionStopTime: "2:00 AM",
            enableLastRequestTime: false,
            lastRequestTime: "1:30 AM",
            out string reasonPostMidnight,
            now: postMidnight);

        Assert.True(allowedPostMidnight);
        Assert.Empty(reasonPostMidnight);

        // After session end (2:30 AM) -> blocked
        var afterStop = testDate.Add(new TimeSpan(2, 30, 0));
        bool allowedAfter = Lyracist.Shared.SessionScheduleHelper.IsRequestSubmissionAllowed(
            enableSessionSchedule: true,
            sessionStartTime: "8:00 PM",
            sessionStopTime: "2:00 AM",
            enableLastRequestTime: false,
            lastRequestTime: "1:30 AM",
            out string reasonAfter,
            now: afterStop);

        Assert.False(allowedAfter);
        Assert.Contains("currently closed", reasonAfter, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SessionScheduleHelper_IsRequestSubmissionAllowed_LastRequestCutoffEvaluation()
    {
        var testDate = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Local);

        // Session 8:00 PM to 2:00 AM with 1:30 AM cutoff
        // Time is 1:15 AM (before cutoff) -> allowed
        var beforeCutoff = testDate.Add(new TimeSpan(1, 15, 0));
        bool allowed1 = Lyracist.Shared.SessionScheduleHelper.IsRequestSubmissionAllowed(
            enableSessionSchedule: true,
            sessionStartTime: "8:00 PM",
            sessionStopTime: "2:00 AM",
            enableLastRequestTime: true,
            lastRequestTime: "1:30 AM",
            out string reason1,
            now: beforeCutoff);

        Assert.True(allowed1);
        Assert.Empty(reason1);

        // Time is 1:35 AM (in session, but past cutoff) -> blocked with cutoff message
        var afterCutoff = testDate.Add(new TimeSpan(1, 35, 0));
        bool allowed2 = Lyracist.Shared.SessionScheduleHelper.IsRequestSubmissionAllowed(
            enableSessionSchedule: true,
            sessionStartTime: "8:00 PM",
            sessionStopTime: "2:00 AM",
            enableLastRequestTime: true,
            lastRequestTime: "1:30 AM",
            out string reason2,
            now: afterCutoff);

        Assert.False(allowed2);
        Assert.Contains("closed for tonight", reason2, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1:30 AM", reason2);

        // When schedule is disabled, cutoff still applies relative to the active evening
        bool allowed3 = Lyracist.Shared.SessionScheduleHelper.IsRequestSubmissionAllowed(
            enableSessionSchedule: false,
            sessionStartTime: "8:00 PM",
            sessionStopTime: "2:00 AM",
            enableLastRequestTime: true,
            lastRequestTime: "1:30 AM",
            out string reason3,
            now: afterCutoff);

        Assert.False(allowed3);
        Assert.Contains("cutoff time for requests was 1:30 AM", reason3, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SessionScheduleHelper_Disabled_AlwaysAllowsRequests()
    {
        bool allowed = Lyracist.Shared.SessionScheduleHelper.IsRequestSubmissionAllowed(
            enableSessionSchedule: false,
            sessionStartTime: "8:00 PM",
            sessionStopTime: "2:00 AM",
            enableLastRequestTime: false,
            lastRequestTime: "1:30 AM",
            out string reason);

        Assert.True(allowed);
        Assert.Empty(reason);
    }
}

