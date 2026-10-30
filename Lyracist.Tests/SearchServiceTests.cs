// Edited on Oct 30, 2026 @ 12:07:00 -> Add self-repair regression tests for legacy/unmigrated schemas, abandoned migration locks, and corrupt FTS5 shadow tables
using System;
using System.Collections.Generic;
using System.Linq;
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
        LyracistDbContext.EnsureSchemaAndRepair();
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
            await searchService.RemoveSongsFromIndexBatch(inserted.Select(s => s.SongId));
            context.Songs.RemoveRange(inserted);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Search_WithBooleanKeywordsAndTabItemTheme_MatchesAndStretchesDataGrid()
    {
        string marker = $"KW{Guid.NewGuid():N}";
        using var context = new LyracistDbContext();
        var searchService = new SearchService(context);

        var song = new Song
        {
            Title = $"{marker} Rock And Roll Now Or Never",
            Artist = "Test Band",
            FilePath = $"C:\\test\\{marker}-rock.mp3",
            IsKaraoke = true
        };

        try
        {
            context.Songs.Add(song);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Simulate a show-computer state where the song exists in Songs (with 'Elvis' in FilePath/Artist)
            // BEFORE being indexed into SongSearch, verifying the automatic Songs LIKE fallback finds it immediately.
            var preIndexMatches = await searchService.Search(marker, isKaraoke: true);
            Assert.Contains(preIndexMatches, s => s.SongId == song.SongId);

            // Also verify EnsureFtsIndexSynchronized heals any missing SongId in SongSearch
            searchService.EnsureFtsIndexSynchronized(forceCheck: true);

            var matches = await searchService.Search($"{marker} rock and roll");
            Assert.Contains(matches, s => s.SongId == song.SongId);
        }
        finally
        {
            await searchService.RemoveSongFromIndex(song.SongId);
            context.Songs.Remove(song);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Verify that LyracistTheme.xaml TabItem style stretches its DataGrid content rather than collapsing it
        Exception? threadEx = null;
        double dgWidth = 0;
        double dgHeight = 0;
        var t = new System.Threading.Thread(() =>
        {
            try
            {
                var app = System.Windows.Application.Current ?? new System.Windows.Application();
                var themeDict = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Lyracist;component/Themes/LyracistTheme.xaml", UriKind.Absolute)
                };

                var tab = new System.Windows.Controls.TabControl { Width = 400, Height = 500 };
                tab.Resources.MergedDictionaries.Add(themeDict);

                var dg = new System.Windows.Controls.DataGrid
                {
                    AutoGenerateColumns = false,
                    ItemsSource = new[] { new Lyracist.Models.KaraokeSong { Title = "Song1", Artist = "Artist1", IsKaraoke = true } }
                };
                dg.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "Song",
                    Binding = new System.Windows.Data.Binding("Title"),
                    Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
                });
                dg.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "Artist",
                    Binding = new System.Windows.Data.Binding("Artist"),
                    Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
                });

                var border = new System.Windows.Controls.Border
                {
                    Padding = new System.Windows.Thickness(4),
                    Margin = new System.Windows.Thickness(0, 8, 0, 0),
                    Child = dg
                };
                tab.Items.Add(new System.Windows.Controls.TabItem
                {
                    Header = "Karaoke Library",
                    Content = border
                });
                tab.SelectedIndex = 0;

                var win = new System.Windows.Window
                {
                    Width = 800,
                    Height = 600,
                    WindowStyle = System.Windows.WindowStyle.None,
                    ShowInTaskbar = false,
                    Left = -10000,
                    Top = -10000,
                    Content = tab
                };
                win.Show();
                win.UpdateLayout();

                dgWidth = dg.ActualWidth;
                dgHeight = dg.ActualHeight;
                win.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        t.SetApartmentState(System.Threading.ApartmentState.STA);
        t.Start();
        t.Join();

        if (threadEx != null) throw threadEx;
        Assert.True(dgWidth > 350, $"Expected TabItem DataGrid width > 350px, but got {dgWidth}px");
        Assert.True(dgHeight > 400, $"Expected TabItem DataGrid height > 400px, but got {dgHeight}px");
    }

    [Fact]
    public void LegacyDatabase_MissingColumnsAndCorruptFtsShadowTables_SelfRepairsWithoutDataLoss()
    {
        string tempDbPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"lyracist_legacy_{Guid.NewGuid():N}.db");
        try
        {
            using (var rawConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={tempDbPath};Pooling=False"))
            {
                rawConn.Open();
                using var cmd = rawConn.CreateCommand();
                // Simulate an old pre-October database with __EFMigrationsLock stuck, missing columns
                // (MixInMs, MixOutMs, NeedsRepair, AllowRecording), and corrupted FTS5 shadow tables.
                cmd.CommandText = @"
                    CREATE TABLE __EFMigrationsLock (Id INTEGER PRIMARY KEY, Timestamp TEXT);
                    INSERT INTO __EFMigrationsLock VALUES (1, '2026-09-06');

                    CREATE TABLE Songs (
                        SongId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        Artist TEXT NOT NULL,
                        FilePath TEXT NOT NULL,
                        IsKaraoke INTEGER NOT NULL,
                        KaraokeType TEXT NOT NULL,
                        Genre TEXT NOT NULL,
                        Tags TEXT NOT NULL,
                        Duration REAL NOT NULL,
                        KeyDefault INTEGER NOT NULL,
                        TempoDefault REAL NOT NULL,
                        DateAdded TEXT NOT NULL,
                        LastPlayed TEXT NULL,
                        PlayCount INTEGER NOT NULL
                    );

                    CREATE TABLE Singers (
                        SingerId INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        JoinDate TEXT NOT NULL,
                        LastSang TEXT NULL,
                        TotalSongsSung INTEGER NOT NULL,
                        Notes TEXT NOT NULL
                    );

                    INSERT INTO Songs (Title, Artist, FilePath, IsKaraoke, KaraokeType, Genre, Tags, Duration, KeyDefault, TempoDefault, DateAdded, PlayCount)
                    VALUES
                        ('Jailhouse Rock', 'Elvis Presley', 'E:\Karaoke\Elvis Presley - Jailhouse Rock.zip', 1, 'ZIPCDG', 'Rock', '', 155, 0, 1.0, '2026-08-10', 0),
                        ('Suspicious Minds', 'Elvis Presley', 'E:\Music\Elvis Presley - Suspicious Minds.mp3', 0, '', 'Rock', '', 260, 0, 1.0, '2026-08-10', 0);

                    CREATE VIRTUAL TABLE SongSearch USING fts5(SongId UNINDEXED, Title, Artist, NormalizedTitle, NormalizedArtist);
                    INSERT INTO SongSearch (rowid, SongId, Title, Artist, NormalizedTitle, NormalizedArtist)
                    VALUES (1, 1, 'Jailhouse Rock', 'Elvis Presley', '', '');
                    INSERT INTO SongSearch (rowid, SongId, Title, Artist, NormalizedTitle, NormalizedArtist)
                    VALUES (2, 2, 'Suspicious Minds', 'Elvis Presley', '', '');
                    -- Corrupt FTS5 shadow data so SongSearch count is 2 but MATCH returns 0 rows
                    DELETE FROM SongSearch_data WHERE id > 10;
                ";
                cmd.ExecuteNonQuery();
            }

            var options = new DbContextOptionsBuilder<LyracistDbContext>()
                .UseSqlite($"Data Source={tempDbPath};Pooling=False")
                .Options;

            using (var db = new LyracistDbContext(options))
            {
                LyracistDbContext.EnsureSchemaColumnsAndTables(db.Database.GetDbConnection());
                var searchService = new SearchService(db);
                searchService.EnsureFtsIndexSynchronized(forceCheck: true);

                // Verify EF Core can now query all Songs and Singers columns (NeedsRepair, MixInMs, AllowRecording) without throwing
                var allSongs = db.Songs.AsNoTracking().Where(s => !s.NeedsRepair).ToList();
                Assert.Equal(2, allSongs.Count);

                var allSingers = db.Singers.AsNoTracking().Where(s => !s.AllowRecording).ToList();
                Assert.Empty(allSingers);

                // Verify FTS5 index was automatically repaired and matches 'elvis'
                using var checkCmd = db.Database.GetDbConnection().CreateCommand();
                if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
                {
                    db.Database.GetDbConnection().Open();
                }
                checkCmd.CommandText = "SELECT COUNT(*) FROM SongSearch WHERE SongSearch MATCH '\"elvis\"*';";
                long elvisMatches = Convert.ToInt64(checkCmd.ExecuteScalar());
                Assert.Equal(2L, elvisMatches);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (string ext in new[] { "", "-wal", "-shm" })
            {
                try { if (System.IO.File.Exists(tempDbPath + ext)) System.IO.File.Delete(tempDbPath + ext); } catch { }
            }
        }
    }
}
