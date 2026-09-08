// Created on Sep 8, 2026 @ 13:48:00 -> Add unit test verifying IsKaraoke SQL filter prevents search limit starvation
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lyracist.Tests;

public class SearchServiceTests
{
    public SearchServiceTests()
    {
        using var context = new LyracistDbContext();
        context.Database.Migrate();
    }

    // Regression coverage for a DJ-reported bug: searching the Music Library tab always returned
    // karaoke tracks, tinted gold as if they were music. Root cause: SearchService.Search/SearchSync
    // fetched up to 150 FTS-matched rows *before* any IsKaraoke filter, and LibraryService then
    // filtered by category client-side afterward - so in a library dominated by karaoke tracks, the
    // 150-row cap filled up with karaoke matches before ever reaching the handful of real music
    // matches for the same query, starving the Music tab of results (or of the right results). The
    // fix pushes the IsKaraoke filter into the SQL query itself, ahead of the LIMIT.
    [Fact]
    public async Task Search_WithIsKaraokeFilter_DoesNotStarveMusicMatchesBehindThe150RowLimit()
    {
        string marker = $"SEARCHTEST{Guid.NewGuid():N}";
        using var context = new LyracistDbContext();
        var searchService = new SearchService(context);

        var inserted = new List<Song>();
        try
        {
            // 160 karaoke tracks sharing the marker - more than the 150-row cap on their own, so a
            // filter applied only after the LIMIT would never even see the music tracks below.
            for (int i = 0; i < 160; i++)
            {
                inserted.Add(new Song
                {
                    Title = $"{marker} Karaoke Track {i}",
                    Artist = "Test Artist",
                    FilePath = $"C:\\test\\{marker}-karaoke-{i}.mp3",
                    IsKaraoke = true
                });
            }
            // 3 real music tracks sharing the same marker.
            for (int i = 0; i < 3; i++)
            {
                inserted.Add(new Song
                {
                    Title = $"{marker} Music Track {i}",
                    Artist = "Test Artist",
                    FilePath = $"C:\\test\\{marker}-music-{i}.mp3",
                    IsKaraoke = false
                });
            }

            context.Songs.AddRange(inserted);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await searchService.IndexSongsBatch(inserted);

            var musicResults = await searchService.Search(marker, isKaraoke: false);

            Assert.Equal(3, musicResults.Count);
            Assert.All(musicResults, s => Assert.False(s.IsKaraoke));

            var karaokeResults = searchService.SearchSync(marker, isKaraoke: true);
            Assert.Equal(150, karaokeResults.Count); // capped by LIMIT 150, but never mixed with music
            Assert.All(karaokeResults, s => Assert.True(s.IsKaraoke));
        }
        finally
        {
            foreach (var song in inserted)
            {
                await searchService.RemoveSongFromIndex(song.SongId);
            }
            context.Songs.RemoveRange(inserted);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }
}
