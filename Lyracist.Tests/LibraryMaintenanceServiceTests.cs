// Created on Oct 7, 2026 @ 15:30:00 -> Tests for the directory-removal logic shared by Lyracist and LyracistDbEditor
using System;
using System.Linq;
using System.Threading.Tasks;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lyracist.Tests;

public class LibraryMaintenanceServiceTests : IDisposable
{
    private readonly string _root = $@"Z:\MaintTest_{Guid.NewGuid():N}";

    public LibraryMaintenanceServiceTests()
    {
        using var context = new LyracistDbContext();
        context.Database.Migrate();
    }

    public void Dispose()
    {
        try
        {
            using var context = new LyracistDbContext();
            var rows = context.Songs.Where(s => s.FilePath.StartsWith(_root)).ToList();
            context.Songs.RemoveRange(rows);
            context.SaveChanges();
        }
        catch { }
    }

    private async Task<int> AddSongAsync(string path)
    {
        using var context = new LyracistDbContext();
        var song = new Song { Title = "T", Artist = "A", FilePath = path };
        context.Songs.Add(song);
        await context.SaveChangesAsync();
        return song.SongId;
    }

    [Fact]
    public async Task Find_MatchesCaseInsensitively_AndIgnoresSiblingFoldersWithSharedPrefix()
    {
        int inside = await AddSongAsync($@"{_root}\Music\a.mp3");
        int differentCase = await AddSongAsync($@"{_root.ToUpperInvariant()}\MUSIC\b.mp3");
        int sibling = await AddSongAsync($@"{_root}\Music2\c.mp3");

        using var context = new LyracistDbContext();
        var ids = await new LibraryMaintenanceService(context)
            .FindSongIdsUnderDirectoryAsync($@"{_root}\Music", []);

        Assert.Contains(inside, ids);
        Assert.Contains(differentCase, ids);
        Assert.DoesNotContain(sibling, ids);
    }

    [Fact]
    public async Task Find_KeepsSongsOwnedByAnotherRegisteredDirectory()
    {
        int plain = await AddSongAsync($@"{_root}\Music\a.mp3");
        int nested = await AddSongAsync($@"{_root}\Music\Karaoke\b.mp3");

        using var context = new LyracistDbContext();
        var ids = await new LibraryMaintenanceService(context).FindSongIdsUnderDirectoryAsync(
            $@"{_root}\Music",
            [$@"{_root}\Music" + "\\", $@"{_root}\Music\Karaoke"]); // itself (trailing slash) is ignored

        Assert.Contains(plain, ids);
        Assert.DoesNotContain(nested, ids);
    }

    [Fact]
    public async Task RemoveSongs_DeletesRowsAndSearchIndexEntries()
    {
        int id = await AddSongAsync($@"{_root}\Music\a.mp3");
        using (var context = new LyracistDbContext())
        {
            var song = await context.Songs.SingleAsync(s => s.SongId == id, TestContext.Current.CancellationToken);
            await new SearchService(context).IndexSong(song);
        }

        using (var context = new LyracistDbContext())
        {
            int removed = await new LibraryMaintenanceService(context).RemoveSongsAsync([id]);
            Assert.Equal(1, removed);
        }

        using var verify = new LyracistDbContext();
        Assert.False(await verify.Songs.AnyAsync(s => s.SongId == id, TestContext.Current.CancellationToken));
        long indexed = await verify.Database
            .SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM SongSearch WHERE SongId = {0}", id)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, indexed);
    }
}
