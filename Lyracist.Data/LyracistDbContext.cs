// Edited on Sep 6, 2026 @ 18:13:00 -> Add ClearAbandonedMigrationLocks to prevent SQLite Error 11 malformed schema lock errors
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyracist.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lyracist.Data
{
    public class LyracistDbContext : DbContext
    {
        public DbSet<Song> Songs { get; set; } = null!;
        public DbSet<SongAudioSettings> SongAudioSettings { get; set; } = null!;
        public DbSet<Singer> Singers { get; set; } = null!;
        public DbSet<SingerAudioSettings> SingerAudioSettings { get; set; } = null!;
        public DbSet<RotationEntry> RotationEntries { get; set; } = null!;
        public DbSet<MusicRequest> MusicRequests { get; set; } = null!;
        public DbSet<OpeningPlaylistItem> OpeningPlaylistItems { get; set; } = null!;
        public DbSet<FillInPlaylistItem> FillInPlaylistItems { get; set; } = null!;
        public DbSet<EndRotationPlaylistItem> EndRotationPlaylistItems { get; set; } = null!;
        public DbSet<OccasionCategory> OccasionCategories { get; set; } = null!;
        public DbSet<OccasionItem> OccasionItems { get; set; } = null!;
        public DbSet<SongSearch> SongSearches { get; set; } = null!;

        public static string GetConnectionString()
        {
            string dataDir = Lyracist.Shared.Globals.DataDir;
            System.IO.Directory.CreateDirectory(dataDir);
            string dbPath = System.IO.Path.Combine(dataDir, "lyracist.db");
            // Cache=Shared is the legacy workaround for SQLite concurrency and is only really needed
            // to share an in-memory database across connections - this is a real file, and WAL mode
            // (below) plus normal connection pooling is Microsoft's recommended concurrency setup.
            // Shared cache also introduces its own table-level SQLITE_LOCKED errors that busy_timeout
            // does not cover (busy_timeout only retries SQLITE_BUSY).
            return $"Data Source={dbPath}";
        }

        /// <summary>
        /// Proactively drops and purges any abandoned __EFMigrationsLock table left behind by an interrupted migration or crash.
        /// Uses PRAGMA writable_schema to repair SQLite Error 11: 'malformed database schema (__EFMigrationsLock) - table already exists'.
        /// </summary>
        public static void ClearAbandonedMigrationLocks()
        {
            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(GetConnectionString());
                conn.Open();

                // 1. Direct catalog purge: removes corrupted/duplicate __EFMigrationsLock entry from sqlite_master
                try
                {
                    using var repairCmd = conn.CreateCommand();
                    repairCmd.CommandText = @"
                        PRAGMA writable_schema = ON;
                        DELETE FROM sqlite_master WHERE name = '__EFMigrationsLock' OR tbl_name = '__EFMigrationsLock';
                        PRAGMA writable_schema = OFF;
                    ";
                    repairCmd.ExecuteNonQuery();
                }
                catch (Exception catalogEx)
                {
                    // Logged (not swallowed) so a repeat corruption is diagnosable instead of silent -
                    // this step failing outright would explain the rest of the repair being a no-op.
                    Lyracist.Shared.Globals.LogInfo("Lyracist", $"ClearAbandonedMigrationLocks: catalog purge step failed: {catalogEx.Message}");
                }

                // 2. Standard DROP TABLE to clean up any physical table/indexes if still registered
                try
                {
                    using var dropCmd = conn.CreateCommand();
                    dropCmd.CommandText = "DROP TABLE IF EXISTS \"__EFMigrationsLock\";";
                    dropCmd.ExecuteNonQuery();
                }
                catch (Exception dropEx)
                {
                    Lyracist.Shared.Globals.LogInfo("Lyracist", $"ClearAbandonedMigrationLocks: DROP TABLE step failed: {dropEx.Message}");
                }

                // 3. Truncate WAL to ensure in-flight lock records in wal file are flushed
                try
                {
                    using var walCmd = conn.CreateCommand();
                    walCmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                    walCmd.ExecuteNonQuery();
                }
                catch (Exception walEx)
                {
                    Lyracist.Shared.Globals.LogInfo("Lyracist", $"ClearAbandonedMigrationLocks: WAL checkpoint step failed: {walEx.Message}");
                }

                // 4. Verify the repair actually took: if a fresh query against the catalog still trips
                // the same corrupt-schema error, the steps above did not fix it (this has been observed
                // in production - see CHANGELOG entry for Sep 6, 2026 recurrence) and the caller's
                // subsequent Migrate() call is expected to fail again.
                try
                {
                    using var verifyCmd = conn.CreateCommand();
                    verifyCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = '__EFMigrationsLock';";
                    verifyCmd.ExecuteScalar();
                }
                catch (Exception verifyEx)
                {
                    Lyracist.Shared.Globals.LogInfo("Lyracist", $"ClearAbandonedMigrationLocks: repair did not take - schema still corrupt after repair attempt: {verifyEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogInfo("Lyracist", $"ClearAbandonedMigrationLocks: {ex.Message}");
            }
        }

        public LyracistDbContext()
        {
        }

        public LyracistDbContext(DbContextOptions<LyracistDbContext> options) : base(options)
        {
        }

        public async Task<StoreAnalyticsData> GetStoreAnalyticsAsync()
        {
            var data = new StoreAnalyticsData();
            var songs = await Songs.AsNoTracking().ToListAsync();
            data.TotalTracks = songs.Count;

            if (data.TotalTracks == 0) return data;

            static bool HasSub(string? val, string sub) => val != null && val.Contains(sub, StringComparison.OrdinalIgnoreCase);
            static bool HasEnd(string? val, string end) => val != null && val.EndsWith(end, StringComparison.OrdinalIgnoreCase);

            // A) Provider Statistics
            data.KvCount = songs.Count(s => HasSub(s.Tags, "Karaoke Version") || HasSub(s.FilePath, "(KV)") || HasSub(s.Tags, "KV"));
            data.PtCount = songs.Count(s => HasSub(s.Tags, "Party Tyme") || HasSub(s.FilePath, "(PT)") || HasSub(s.Tags, "PT"));
            data.SfCount = songs.Count(s => HasSub(s.Tags, "Sunfly") || HasSub(s.FilePath, "(SF)") || HasSub(s.Tags, "SF"));
            data.KcCount = songs.Count(s => HasSub(s.Tags, "Karaoke.com") || HasSub(s.FilePath, "(KCOM)") || HasSub(s.Tags, "KCOM"));
            data.LocalCount = songs.Count(s => !HasSub(s.Tags, "Karaoke Version") && !HasSub(s.Tags, "Party Tyme") &&
                                               !HasSub(s.Tags, "Sunfly") && !HasSub(s.Tags, "Karaoke.com") &&
                                               !HasSub(s.FilePath, "(KV)") && !HasSub(s.FilePath, "(PT)") &&
                                               !HasSub(s.FilePath, "(SF)") && !HasSub(s.FilePath, "(KCOM)"));

            data.KvPercent = Math.Round((data.KvCount / (double)data.TotalTracks) * 100.0, 1);
            data.PtPercent = Math.Round((data.PtCount / (double)data.TotalTracks) * 100.0, 1);
            data.SfPercent = Math.Round((data.SfCount / (double)data.TotalTracks) * 100.0, 1);
            data.KcPercent = Math.Round((data.KcCount / (double)data.TotalTracks) * 100.0, 1);
            data.LocalPercent = Math.Round((data.LocalCount / (double)data.TotalTracks) * 100.0, 1);

            var providerCounts = new (string name, int count)[]
            {
                ("Karaoke Version", data.KvCount),
                ("Party Tyme", data.PtCount),
                ("Sunfly", data.SfCount),
                ("Karaoke.com", data.KcCount),
                ("Local", data.LocalCount)
            };
            var topProvider = providerCounts.OrderByDescending(p => p.count).FirstOrDefault();
            data.MostFrequentProvider = topProvider.count > 0 ? $"{topProvider.name} ({topProvider.count})" : "None";

            // B) File Type Statistics
            data.ZipCdgCount = songs.Count(s => string.Equals(s.KaraokeType, "ZIPCDG", StringComparison.OrdinalIgnoreCase) || HasEnd(s.FilePath, ".zip"));
            data.Mp4Count = songs.Count(s => string.Equals(s.KaraokeType, "MP4", StringComparison.OrdinalIgnoreCase) || HasEnd(s.FilePath, ".mp4"));
            data.Mp3gCount = songs.Count(s => string.Equals(s.KaraokeType, "MP3G", StringComparison.OrdinalIgnoreCase) || (s.IsKaraoke && !HasEnd(s.FilePath, ".zip") && !HasEnd(s.FilePath, ".mp4")));
            data.AudioOnlyCount = songs.Count(s => !s.IsKaraoke);
            data.LyricsCount = songs.Count(s => HasSub(s.Tags, "Lyrics"));

            // C) FFmpeg Processing Statistics
            data.NormalizedCount = songs.Count(s => s.MeasuredLoudnessLufs != null || HasSub(s.Tags, "Normalized"));
            data.SilenceTrimmedCount = songs.Count(s => HasSub(s.Tags, "SilenceTrimmed") || HasSub(s.Tags, "silenceremove"));
            data.WaveformCount = songs.Count(s => HasSub(s.Tags, "Waveform"));

            // D) Quality & Difficulty Statistics
            data.QualityLowCount = songs.Count(s => string.Equals(s.Quality, "Low", StringComparison.OrdinalIgnoreCase) || HasSub(s.Tags, "Quality:Low"));
            data.QualityMediumCount = songs.Count(s => string.Equals(s.Quality, "Medium", StringComparison.OrdinalIgnoreCase) || HasSub(s.Tags, "Quality:Medium"));
            data.QualityHighCount = songs.Count(s => string.Equals(s.Quality, "High", StringComparison.OrdinalIgnoreCase) || HasSub(s.Tags, "Quality:High"));

            data.DifficultyEasyCount = songs.Count(s => string.Equals(s.Difficulty, "Easy", StringComparison.OrdinalIgnoreCase) || HasSub(s.Tags, "Easy"));
            data.DifficultyMediumCount = songs.Count(s => string.Equals(s.Difficulty, "Medium", StringComparison.OrdinalIgnoreCase) || HasSub(s.Tags, "Medium"));
            data.DifficultyHardCount = songs.Count(s => string.Equals(s.Difficulty, "Hard", StringComparison.OrdinalIgnoreCase) || HasSub(s.Tags, "Hard"));

            // Key Distribution (Top 8 keys)
            var keyGroups = songs
                .Where(s => !string.IsNullOrWhiteSpace(s.Key))
                .GroupBy(s => s.Key!.Trim())
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(8)
                .ToList();

            int maxKeyCount = keyGroups.Count > 0 ? keyGroups.Max(k => k.Count) : 1;
            foreach (var k in keyGroups)
            {
                data.KeyDistribution.Add(new KeyDistributionItem
                {
                    Key = k.Key,
                    Count = k.Count,
                    Percentage = Math.Round((k.Count / (double)data.TotalTracks) * 100.0, 1),
                    BarWidth = Math.Max(12, Math.Round((k.Count / (double)maxKeyCount) * 120.0))
                });
            }

            // BPM Histogram (5 standard tempo buckets)
            var bpmBuckets = new (string label, Func<Song, bool> predicate)[]
            {
                ("< 80 BPM", s => s.BPM.HasValue && s.BPM.Value < 80),
                ("80 - 100", s => s.BPM.HasValue && s.BPM.Value >= 80 && s.BPM.Value < 100),
                ("100 - 120", s => s.BPM.HasValue && s.BPM.Value >= 100 && s.BPM.Value < 120),
                ("120 - 140", s => s.BPM.HasValue && s.BPM.Value >= 120 && s.BPM.Value < 140),
                ("140+ BPM", s => s.BPM.HasValue && s.BPM.Value >= 140)
            };

            int maxBpmCount = 1;
            var bucketResults = new List<(string label, int count)>();
            foreach (var b in bpmBuckets)
            {
                int count = songs.Count(b.predicate);
                bucketResults.Add((b.label, count));
                if (count > maxBpmCount) maxBpmCount = count;
            }

            foreach (var b in bucketResults)
            {
                data.BpmHistogram.Add(new BpmBucketItem
                {
                    RangeLabel = b.label,
                    Count = b.count,
                    Percentage = Math.Round((b.count / (double)data.TotalTracks) * 100.0, 1),
                    BarHeight = Math.Max(4, Math.Round((b.count / (double)maxBpmCount) * 60.0))
                });
            }

            // E) Import Activity Summary (Last 14 days)
            var now = DateTime.UtcNow.Date;
            int maxDailyCount = 1;
            var dailyList = new List<DayActivityItem>();

            for (int i = 13; i >= 0; i--)
            {
                var targetDay = now.AddDays(-i);
                int dayCount = songs.Count(s => s.DateAdded.Date == targetDay);
                if (dayCount > maxDailyCount) maxDailyCount = dayCount;

                dailyList.Add(new DayActivityItem
                {
                    Date = targetDay,
                    DateLabel = targetDay.ToString("M/d"),
                    DayOfWeek = targetDay.ToString("ddd"),
                    Count = dayCount
                });
            }

            foreach (var item in dailyList)
            {
                item.BarHeight = Math.Max(3, Math.Round((item.Count / (double)maxDailyCount) * 55.0));
                data.DailyActivity.Add(item);
            }

            var busiestDayItem = dailyList.OrderByDescending(d => d.Count).FirstOrDefault();
            data.BusiestDay = busiestDayItem != null && busiestDayItem.Count > 0
                ? $"{busiestDayItem.DayOfWeek} {busiestDayItem.DateLabel} ({busiestDayItem.Count} tracks)"
                : "No recent activity";

            // Hourly Activity (24 hours heatmap)
            int maxHourCount = 1;
            var hourlyList = new List<HourActivityItem>();
            for (int h = 0; h < 24; h++)
            {
                int hCount = songs.Count(s => s.DateAdded.Hour == h);
                if (hCount > maxHourCount) maxHourCount = hCount;

                hourlyList.Add(new HourActivityItem
                {
                    Hour = h,
                    HourLabel = $"{h:00}",
                    Count = hCount,
                    Tooltip = $"{h:00}:00 - {h:00}:59: {hCount} track{(hCount == 1 ? "" : "s")}"
                });
            }

            foreach (var item in hourlyList)
            {
                if (item.Count == 0) item.IntensityLevel = 0;
                else if (item.Count <= Math.Max(1, maxHourCount * 0.25)) item.IntensityLevel = 1;
                else if (item.Count <= Math.Max(2, maxHourCount * 0.50)) item.IntensityLevel = 2;
                else if (item.Count <= Math.Max(3, maxHourCount * 0.75)) item.IntensityLevel = 3;
                else item.IntensityLevel = 4;

                data.HourlyActivity.Add(item);
            }

            var busiestHourItem = hourlyList.OrderByDescending(h => h.Count).FirstOrDefault();
            data.BusiestHour = busiestHourItem != null && busiestHourItem.Count > 0
                ? $"{busiestHourItem.Hour:00}:00 - {busiestHourItem.Hour:00}:59 ({busiestHourItem.Count} tracks)"
                : "No recent activity";

            return data;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite(GetConnectionString())
                              .AddInterceptors(new SqliteConnectionInterceptor());
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Song Indexes
            modelBuilder.Entity<Song>(entity =>
            {
                entity.HasIndex(s => s.Title);
                entity.HasIndex(s => s.Artist);
                entity.HasIndex(s => s.FilePath).IsUnique();
            });

            // Song <-> SongAudioSettings (1-to-1)
            modelBuilder.Entity<Song>()
                .HasOne(s => s.AudioSettings)
                .WithOne(a => a.Song)
                .HasForeignKey<SongAudioSettings>(a => a.SongId)
                .OnDelete(DeleteBehavior.Cascade);

            // Singer <-> SingerAudioSettings (1-to-1)
            modelBuilder.Entity<Singer>()
                .HasOne(s => s.AudioSettings)
                .WithOne(a => a.Singer)
                .HasForeignKey<SingerAudioSettings>(a => a.SingerId)
                .OnDelete(DeleteBehavior.Cascade);

            // RotationEntry (FK relations)
            modelBuilder.Entity<RotationEntry>(entity =>
            {
                entity.HasOne(r => r.Singer)
                    .WithMany(s => s.RotationEntries)
                    .HasForeignKey(r => r.SingerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Song)
                    .WithMany(s => s.RotationEntries)
                    .HasForeignKey(r => r.SongId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // MusicRequest
            modelBuilder.Entity<MusicRequest>()
                .HasOne(m => m.Singer)
                .WithMany(s => s.MusicRequests)
                .HasForeignKey(m => m.SingerId)
                .OnDelete(DeleteBehavior.Cascade);

            // OpeningPlaylistItem
            modelBuilder.Entity<OpeningPlaylistItem>()
                .HasOne(p => p.Song)
                .WithMany()
                .HasForeignKey(p => p.SongId)
                .OnDelete(DeleteBehavior.Cascade);

            // FillInPlaylistItem
            modelBuilder.Entity<FillInPlaylistItem>()
                .HasOne(p => p.Song)
                .WithMany()
                .HasForeignKey(p => p.SongId)
                .OnDelete(DeleteBehavior.Cascade);

            // EndRotationPlaylistItem
            modelBuilder.Entity<EndRotationPlaylistItem>()
                .HasOne(p => p.Song)
                .WithMany()
                .HasForeignKey(p => p.SongId)
                .OnDelete(DeleteBehavior.Cascade);

            // OccasionCategory (Self-referencing & Items)
            modelBuilder.Entity<OccasionCategory>(entity =>
            {
                entity.HasOne(c => c.ParentCategory)
                    .WithMany(c => c.SubCategories)
                    .HasForeignKey(c => c.ParentCategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // OccasionItem
            modelBuilder.Entity<OccasionItem>()
                .HasOne(i => i.OccasionCategory)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.OccasionCategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // SongSearch (FTS5 Virtual Table Configuration)
            modelBuilder.Entity<SongSearch>(entity =>
            {
                entity.HasKey(s => s.SongId);
                entity.ToTable("SongSearch", tb => tb.UseSqlReturningClause(false));
            });

            // Seeding default OccasionCategory entries
            modelBuilder.Entity<OccasionCategory>().HasData(
                new OccasionCategory { OccasionCategoryId = 1, Name = "Holiday" },
                new OccasionCategory { OccasionCategoryId = 2, Name = "Birthday" },
                new OccasionCategory { OccasionCategoryId = 3, Name = "Anniversary" },
                new OccasionCategory { OccasionCategoryId = 4, Name = "Wedding" },
                new OccasionCategory { OccasionCategoryId = 5, Name = "Graduation" },
                new OccasionCategory { OccasionCategoryId = 6, Name = "Retirement" }
            );
        }
    }

    public class SqliteConnectionInterceptor : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            using var command = connection.CreateCommand();
            // synchronous=NORMAL: WAL mode alone still fsyncs on every commit (synchronous=FULL is
            // the SQLite default). NORMAL is safe under WAL - a crash can only lose the most recent
            // commit(s), never corrupt the database - and is the standard desktop pairing with WAL.
            command.CommandText = "PRAGMA busy_timeout=10000; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            command.ExecuteNonQuery();
        }

        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await using var command = connection.CreateCommand();
            // synchronous=NORMAL: WAL mode alone still fsyncs on every commit (synchronous=FULL is
            // the SQLite default). NORMAL is safe under WAL - a crash can only lose the most recent
            // commit(s), never corrupt the database - and is the standard desktop pairing with WAL.
            command.CommandText = "PRAGMA busy_timeout=10000; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}