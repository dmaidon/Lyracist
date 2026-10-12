// Edited on Oct 11, 2026 @ 13:35:00 -> Add tests for standalone noartist.db persistence across scans
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lyracist.Tests;

public class ScanningServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _ghostDir;

    public ScanningServiceTests()
    {
        using var context = new LyracistDbContext();
        context.Database.Migrate();

        string id = Guid.NewGuid().ToString("N");
        _tempDir = Path.Combine(Path.GetTempPath(), "ScanTest_" + id);
        Directory.CreateDirectory(_tempDir);
        // A path that never exists, standing in for an unplugged external drive or share.
        _ghostDir = Path.Combine(Path.GetTempPath(), "ScanTestGhost_" + id);

        NoArtistStore.OverrideDbPath = Path.Combine(_tempDir, "test_noartist.db");
    }

    public void Dispose()
    {
        try
        {
            using var context = new LyracistDbContext();
            var search = new SearchService(context);
            var leftovers = context.Songs
                .Where(s => s.FilePath.StartsWith(_tempDir) || s.FilePath.StartsWith(_ghostDir))
                .ToList();
            context.Songs.RemoveRange(leftovers);
            context.SaveChanges();
            foreach (var s in leftovers)
            {
                search.RemoveSongFromIndex(s.SongId).GetAwaiter().GetResult();
            }
        }
        catch { }
        NoArtistStore.OverrideDbPath = null;
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static async Task ScanAsync(params string[] dirs)
    {
        using var context = new LyracistDbContext();
        await new ScanningService(context).ScanDirectories(dirs);
    }

    private static async Task<Song> LoadAsync(string path)
    {
        using var context = new LyracistDbContext();
        return await context.Songs.AsNoTracking().SingleAsync(s => s.FilePath == path);
    }

    // A cancelled scan stops at a batch boundary with OperationCanceledException instead of
    // running to completion (Lyracist and the DbEditor cancel scans on shutdown).
    [Fact]
    public async Task Scan_WithCancelledToken_ThrowsAndStopsBeforeProcessingFiles()
    {
        string path = Path.Combine(_tempDir, "Toto - Africa.mp3");
        File.WriteAllBytes(path, [0, 1, 2]);

        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();

        using (var context = new LyracistDbContext())
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => new ScanningService(context).ScanDirectories([_tempDir], null, cts.Token));
        }

        using var verify = new LyracistDbContext();
        Assert.False(await verify.Songs.AnyAsync(s => s.FilePath == path, TestContext.Current.CancellationToken));
    }

    // A scan that includes a missing path (unmounted drive) alongside a real folder must not treat
    // every song under the missing path as "deleted" and remove it from the library.
    [Fact]
    public async Task Scan_WithUnmountedDirectory_DoesNotRemoveItsSongs()
    {
        string realFile = Path.Combine(_tempDir, "Toto - Africa.mp3");
        File.WriteAllBytes(realFile, [0, 1, 2]);

        string ghostFile = Path.Combine(_ghostDir, "Queen - Bohemian Rhapsody.mp3");
        using (var context = new LyracistDbContext())
        {
            context.Songs.Add(new Song { Title = "Bohemian Rhapsody", Artist = "Queen", FilePath = ghostFile });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ScanAsync(_tempDir, _ghostDir);

        using var verify = new LyracistDbContext();
        Assert.True(await verify.Songs.AnyAsync(s => s.FilePath == ghostFile, TestContext.Current.CancellationToken),
            "Songs under a directory that doesn't exist (unmounted drive) must be left alone.");
        Assert.True(await verify.Songs.AnyAsync(s => s.FilePath == realFile, TestContext.Current.CancellationToken));
    }

    // Counterpart: a file genuinely deleted from a directory that IS mounted is still cleaned up.
    [Fact]
    public async Task Scan_RemovesRowsForFilesDeletedFromScannedDirectory()
    {
        string keep = Path.Combine(_tempDir, "Toto - Africa.mp3");
        string gone = Path.Combine(_tempDir, "Journey - Faithfully.mp3");
        File.WriteAllBytes(keep, [0, 1, 2]);
        File.WriteAllBytes(gone, [0, 1, 2]);

        await ScanAsync(_tempDir);
        File.Delete(gone);
        await ScanAsync(_tempDir);

        using var verify = new LyracistDbContext();
        Assert.True(await verify.Songs.AnyAsync(s => s.FilePath == keep, TestContext.Current.CancellationToken));
        Assert.False(await verify.Songs.AnyAsync(s => s.FilePath == gone, TestContext.Current.CancellationToken));
    }

    // ChangeTracker.Clear() runs after every 200-file batch; updates to existing rows used to be
    // persisted only for the first batch because the later batches' entities had been detached.
    [Fact]
    public async Task Rescan_PersistsUpdatesAcrossAllBatches()
    {
        const int count = 450; // > 2 batches of 200
        for (int i = 0; i < count; i++)
        {
            File.WriteAllBytes(Path.Combine(_tempDir, $"Artist{i} - Title{i}.mp3"), [0, 1, 2]);
        }

        await ScanAsync(_tempDir);

        // Make every row look unresolved/stale so the next scan has real updates to apply.
        using (var context = new LyracistDbContext())
        {
            var rows = await context.Songs.Where(s => s.FilePath.StartsWith(_tempDir)).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(count, rows.Count);
            foreach (var r in rows)
            {
                r.Artist = "Unknown Artist";
                r.Title = "stale";
            }
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ScanAsync(_tempDir);

        using var verify = new LyracistDbContext();
        var after = await verify.Songs.AsNoTracking().Where(s => s.FilePath.StartsWith(_tempDir)).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(count, after.Count);
        Assert.DoesNotContain(after, s => s.Artist == "Unknown Artist" || s.Title == "stale");
    }

    // A rescan re-parses the filename, which is only a guess. It must not revert an artist/title
    // that was resolved from tags, fetched online, or edited by hand in LyracistDbEditor.
    [Fact]
    public async Task Rescan_DoesNotOverwriteResolvedArtistOrTitle()
    {
        string path = Path.Combine(_tempDir, "Toto - Africa.mp3");
        File.WriteAllBytes(path, [0, 1, 2]);
        await ScanAsync(_tempDir);

        using (var context = new LyracistDbContext())
        {
            var song = await context.Songs.SingleAsync(s => s.FilePath == path, TestContext.Current.CancellationToken);
            song.Artist = "Manual Artist";
            song.Title = "Manual Title";
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ScanAsync(_tempDir);

        var reloaded = await LoadAsync(path);
        Assert.Equal("Manual Artist", reloaded.Artist);
        Assert.Equal("Manual Title", reloaded.Title);
    }

    // A file renamed to just its title (as LyracistDbEditor's Rename Files does) parses as
    // "Unknown Artist"; a rescan must not wipe the artist already stored for that row.
    [Fact]
    public async Task Rescan_DoesNotReplaceKnownArtistWithUnknownArtist()
    {
        string path = Path.Combine(_tempDir, "Africa.mp3");
        File.WriteAllBytes(path, [0, 1, 2]);
        await ScanAsync(_tempDir);

        using (var context = new LyracistDbContext())
        {
            var song = await context.Songs.SingleAsync(s => s.FilePath == path, TestContext.Current.CancellationToken);
            Assert.Equal("Unknown Artist", song.Artist);
            song.Artist = "Toto";
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ScanAsync(_tempDir);

        Assert.Equal("Toto", (await LoadAsync(path)).Artist);
    }

    // Unresolved songs are still filled in from the filename on rescan (the behavior the guard
    // above must not break).
    [Fact]
    public async Task Rescan_StillFillsInUnresolvedArtistFromFilename()
    {
        string path = Path.Combine(_tempDir, "Toto - Africa.mp3");
        File.WriteAllBytes(path, [0, 1, 2]);
        await ScanAsync(_tempDir);

        using (var context = new LyracistDbContext())
        {
            var song = await context.Songs.SingleAsync(s => s.FilePath == path, TestContext.Current.CancellationToken);
            song.Artist = "Unknown Artist";
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ScanAsync(_tempDir);

        Assert.Equal("Toto", (await LoadAsync(path)).Artist);
    }

    [Fact]
    public async Task ProbeMissingMetadata_SetsNeedsRepair_WhenArtistCannotBeResolved()
    {
        string path = Path.Combine(_tempDir, "MysterySong.mp3");
        File.WriteAllBytes(path, [0, 1, 2]);

        await ScanAsync(_tempDir);

        using (var context = new LyracistDbContext())
        {
            var scanner = new ScanningService(context);
            await scanner.ProbeMissingMetadataAsync(null, TestContext.Current.CancellationToken);
        }

        var loaded = await LoadAsync(path);
        Assert.True(loaded.NeedsRepair);
        Assert.Equal("Unknown Artist", loaded.Artist);
        Assert.Equal("none", loaded.Tags);
    }

    [Fact]
    public async Task ProbeMissingMetadata_SkipsTracksFlaggedAsNeedsRepair()
    {
        string path = Path.Combine(_tempDir, "AlreadyFlagged.mp3");
        File.WriteAllBytes(path, [0, 1, 2]);

        using (var context = new LyracistDbContext())
        {
            context.Songs.Add(new Song
            {
                Title = "Already Flagged",
                Artist = "Unknown Artist",
                FilePath = path,
                NeedsRepair = true,
                Duration = 0,
                Tags = ""
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new LyracistDbContext())
        {
            var scanner = new ScanningService(context);
            await scanner.ProbeMissingMetadataAsync(null, TestContext.Current.CancellationToken);
        }

        var loaded = await LoadAsync(path);
        Assert.Equal(0, loaded.Duration);
        Assert.True(loaded.NeedsRepair);
    }

    [Fact]
    public async Task ProbeMissingMetadata_PersistsUnresolvedTracksToNoArtistStore()
    {
        string path = Path.Combine(_tempDir, "MysteryTrack.mp3");
        File.WriteAllBytes(path, [0, 1, 2]);

        await ScanAsync(_tempDir);

        using (var context = new LyracistDbContext())
        {
            var scanner = new ScanningService(context);
            await scanner.ProbeMissingMetadataAsync(null, TestContext.Current.CancellationToken);
        }

        Assert.True(await NoArtistStore.ContainsAsync(path, TestContext.Current.CancellationToken));
        var all = await NoArtistStore.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.True(all.ContainsKey(path));
        Assert.Equal("MysteryTrack", all[path].Title);
    }

    [Fact]
    public async Task Scan_UsesNoArtistStore_DuringColdScan_WhenMainDbWasDeleted()
    {
        string path = Path.Combine(_tempDir, "MysteryTrackWithNoArtist.mp3");
        File.WriteAllBytes(path, [0, 1, 2]);

        // Pre-populate noartist.db (as if this track was probed in an earlier database that was cleared)
        await NoArtistStore.UpsertAsync(new UnresolvedTrack
        {
            FilePath = path,
            Title = "Mystery Track",
            Duration = 215.5,
            KaraokeType = "MP3G",
            IsKaraoke = true,
            DateFlagged = DateTime.UtcNow
        }, TestContext.Current.CancellationToken);

        // Verify lyracist.db does NOT have this song yet (cold scan after db cleared)
        using (var check = new LyracistDbContext())
        {
            Assert.False(await check.Songs.AnyAsync(s => s.FilePath == path, TestContext.Current.CancellationToken));
        }

        // Perform scan
        await ScanAsync(_tempDir);

        // The song should be loaded straight into lyracist.db using cached info from noartist.db
        var loaded = await LoadAsync(path);
        Assert.Equal("Mystery Track", loaded.Title);
        Assert.Equal("Unknown Artist", loaded.Artist);
        Assert.Equal(215.5, loaded.Duration);
        Assert.Equal("MP3G", loaded.KaraokeType);
        Assert.True(loaded.IsKaraoke);
        Assert.True(loaded.NeedsRepair);
    }
}

