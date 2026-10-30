// Edited on Oct 30, 2026 @ 12:02:00 -> Add automatic database schema, migration, catalog, and FTS5 self-repair engine
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
        private static readonly System.Threading.Lock _repairLock = new();

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
        public DbSet<PerformanceRecording> PerformanceRecordings { get; set; } = null!;

        public static string GetDatabaseFilePath()
        {
            string dataDir = Lyracist.Shared.Globals.DataDir;
            System.IO.Directory.CreateDirectory(dataDir);
            return System.IO.Path.Combine(dataDir, "lyracist.db");
        }

        public static string GetConnectionString()
        {
            string dbPath = GetDatabaseFilePath();
            // Cache=Shared is the legacy workaround for SQLite concurrency and is only really needed
            // to share an in-memory database across connections - this is a real file, and WAL mode
            // (below) plus normal connection pooling is Microsoft's recommended concurrency setup.
            // Shared cache also introduces its own table-level SQLITE_LOCKED errors that busy_timeout
            // does not cover (busy_timeout only retries SQLITE_BUSY).
            return $"Data Source={dbPath}";
        }

        /// <summary>
        /// Proactively drops and purges any abandoned __EFMigrationsLock table and repairs corrupt sqlite_master entries
        /// left behind by an interrupted migration, power loss, or crash.
        /// </summary>
        public static void ClearAbandonedMigrationLocks()
        {
            lock (_repairLock)
            {
                string dbPath = GetDatabaseFilePath();
                if (!System.IO.File.Exists(dbPath))
                {
                    return;
                }

                Services.SearchService.ResetSharedConnection();
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                try
                {
                    using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Pooling=False");
                    try
                    {
                        conn.Open();
                        using var checkCmd = conn.CreateCommand();
                        checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
                        checkCmd.ExecuteScalar();
                    }
                    catch (Microsoft.Data.Sqlite.SqliteException openEx) when (openEx.SqliteErrorCode is 11 or 26)
                    {
                        // Corrupt WAL/SHM after a crash or file restore can cause Error 11/26 on open;
                        // remove the sidecar WAL/SHM files and retry before touching the database.
                        conn.Close();
                        TryDeleteWalSidecarFiles(dbPath);
                        conn.Open();
                    }

                    // 1. Standard DROP TABLE first while writable_schema is OFF so SQLite frees root pages cleanly
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

                    // 2. Direct catalog purge if __EFMigrationsLock, duplicate objects, or orphaned indexes remain in sqlite_master
                    try
                    {
                        using var repairCmd = conn.CreateCommand();
                        repairCmd.CommandText = @"
                            PRAGMA writable_schema = ON;
                            DELETE FROM sqlite_master
                            WHERE name = '__EFMigrationsLock'
                               OR tbl_name = '__EFMigrationsLock'
                               OR name = '1'
                               OR (name IS NOT NULL AND rowid NOT IN (
                                   SELECT MIN(rowid) FROM sqlite_master WHERE name IS NOT NULL GROUP BY type, name
                               ))
                               OR (type = 'index' AND name NOT LIKE 'sqlite_autoindex_%' AND tbl_name NOT IN (
                                   SELECT name FROM sqlite_master WHERE type = 'table'
                               ));
                            PRAGMA writable_schema = OFF;
                            PRAGMA integrity_check(1);
                        ";
                        repairCmd.ExecuteNonQuery();
                    }
                    catch (Exception catalogEx)
                    {
                        Lyracist.Shared.Globals.LogInfo("Lyracist", $"ClearAbandonedMigrationLocks: catalog purge step failed: {catalogEx.Message}");
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
                }
                catch (Exception ex)
                {
                    Lyracist.Shared.Globals.LogInfo("Lyracist", $"ClearAbandonedMigrationLocks: {ex.Message}");
                }
                finally
                {
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                }
            }
        }

        private static void TryDeleteWalSidecarFiles(string dbPath)
        {
            foreach (string ext in new[] { "-wal", "-shm" })
            {
                string sidecar = dbPath + ext;
                try
                {
                    if (System.IO.File.Exists(sidecar))
                    {
                        System.IO.File.Delete(sidecar);
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Performs a complete, self-healing check and repair of lyracist.db:
        /// 1. Clears abandoned migration locks and corrupt sqlite_master entries.
        /// 2. Attempts EF Core Migrate().
        /// 3. Regardless of whether Migrate() succeeded or threw, idempotently verifies and adds any
        ///    missing tables, columns, migration history records, and indexes.
        /// 4. Verifies and rebuilds the FTS5 SongSearch index if out of sync or corrupted.
        /// </summary>
        public static void EnsureSchemaAndRepair()
        {
            lock (_repairLock)
            {
                ClearAbandonedMigrationLocks();

                try
                {
                    using var db = new LyracistDbContext();
                    db.Database.Migrate();
                }
                catch (Exception migrateEx)
                {
                    Lyracist.Shared.Globals.LogInfo("Lyracist", $"EnsureSchemaAndRepair: Migrate() threw ({migrateEx.Message}); running direct schema repair.");
                    ClearAbandonedMigrationLocks();
                }

                try
                {
                    using var db = new LyracistDbContext();
                    var conn = db.Database.GetDbConnection();
                    conn.Open();
                    try
                    {
                        EnsureSchemaColumnsAndTables(conn);
                    }
                    finally
                    {
                        conn.Close();
                    }

                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                    using var verifyDb = new LyracistDbContext();
                    new Services.SearchService(verifyDb).EnsureFtsIndexSynchronized(forceCheck: true);
                }
                catch (Microsoft.Data.Sqlite.SqliteException sqlEx) when (sqlEx.SqliteErrorCode is 11 or 26)
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", "EnsureSchemaAndRepair detected severe SQLite corruption; salvaging database", sqlEx);
                    SalvageCorruptedDatabase();
                }
                catch (Exception ex)
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", "EnsureSchemaAndRepair failed", ex);
                }
            }
        }

        /// <summary>
        /// Idempotently ensures all tables, columns, indexes, and migration records exist on the given SQLite connection.
        /// Safe to call on any connection (including in-memory test databases and partially migrated legacy databases).
        /// </summary>
        public static void EnsureSchemaColumnsAndTables(DbConnection connection)
        {
            bool wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
            {
                connection.Open();
            }

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        PRAGMA journal_mode=WAL;
                        DROP TABLE IF EXISTS ""__EFMigrationsLock"";

                        CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                            ""MigrationId"" TEXT NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY,
                            ""ProductVersion"" TEXT NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS ""OccasionCategories"" (
                            ""OccasionCategoryId"" INTEGER NOT NULL CONSTRAINT ""PK_OccasionCategories"" PRIMARY KEY AUTOINCREMENT,
                            ""Name"" TEXT NOT NULL,
                            ""ParentCategoryId"" INTEGER NULL,
                            CONSTRAINT ""FK_OccasionCategories_OccasionCategories_ParentCategoryId"" FOREIGN KEY (""ParentCategoryId"") REFERENCES ""OccasionCategories"" (""OccasionCategoryId"") ON DELETE CASCADE
                        );

                        CREATE TABLE IF NOT EXISTS ""Singers"" (
                            ""SingerId"" INTEGER NOT NULL CONSTRAINT ""PK_Singers"" PRIMARY KEY AUTOINCREMENT,
                            ""Name"" TEXT NOT NULL,
                            ""JoinDate"" TEXT NOT NULL,
                            ""LastSang"" TEXT NULL,
                            ""TotalSongsSung"" INTEGER NOT NULL DEFAULT 0,
                            ""Notes"" TEXT NOT NULL DEFAULT '',
                            ""Score"" INTEGER NOT NULL DEFAULT 0,
                            ""RatingPoints"" INTEGER NOT NULL DEFAULT 0,
                            ""RatingCount"" INTEGER NOT NULL DEFAULT 0,
                            ""AverageRating"" REAL NOT NULL DEFAULT 0.0,
                            ""Email"" TEXT NOT NULL DEFAULT '',
                            ""PinCode"" TEXT NOT NULL DEFAULT '',
                            ""AvatarType"" TEXT NOT NULL DEFAULT 'None',
                            ""AvatarSource"" TEXT NOT NULL DEFAULT '',
                            ""VocalRange"" TEXT NOT NULL DEFAULT '',
                            ""CustomTitle"" TEXT NOT NULL DEFAULT '',
                            ""AllowRecording"" INTEGER NOT NULL DEFAULT 0
                        );

                        CREATE TABLE IF NOT EXISTS ""Songs"" (
                            ""SongId"" INTEGER NOT NULL CONSTRAINT ""PK_Songs"" PRIMARY KEY AUTOINCREMENT,
                            ""Title"" TEXT NOT NULL DEFAULT '',
                            ""Artist"" TEXT NOT NULL DEFAULT '',
                            ""FilePath"" TEXT NOT NULL DEFAULT '',
                            ""IsKaraoke"" INTEGER NOT NULL DEFAULT 1,
                            ""KaraokeType"" TEXT NOT NULL DEFAULT 'MP3G',
                            ""Genre"" TEXT NOT NULL DEFAULT '',
                            ""Tags"" TEXT NOT NULL DEFAULT '',
                            ""Duration"" REAL NOT NULL DEFAULT 0.0,
                            ""KeyDefault"" INTEGER NOT NULL DEFAULT 0,
                            ""TempoDefault"" REAL NOT NULL DEFAULT 1.0,
                            ""DateAdded"" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                            ""LastPlayed"" TEXT NULL,
                            ""PlayCount"" INTEGER NOT NULL DEFAULT 0,
                            ""MeasuredLoudnessLufs"" REAL NULL,
                            ""Difficulty"" TEXT NULL,
                            ""Key"" TEXT NULL,
                            ""BPM"" REAL NULL,
                            ""VocalPresence"" TEXT NULL,
                            ""Quality"" TEXT NULL,
                            ""MixInMs"" INTEGER NULL,
                            ""MixOutMs"" INTEGER NULL,
                            ""NeedsRepair"" INTEGER NOT NULL DEFAULT 0
                        );

                        CREATE TABLE IF NOT EXISTS ""OccasionItems"" (
                            ""OccasionItemId"" INTEGER NOT NULL CONSTRAINT ""PK_OccasionItems"" PRIMARY KEY AUTOINCREMENT,
                            ""OccasionCategoryId"" INTEGER NOT NULL,
                            ""Name"" TEXT NOT NULL DEFAULT '',
                            ""FilePath"" TEXT NOT NULL DEFAULT '',
                            ""Treble"" REAL NOT NULL DEFAULT 0.0,
                            ""Mid"" REAL NOT NULL DEFAULT 0.0,
                            ""Bass"" REAL NOT NULL DEFAULT 0.0,
                            ""Gain"" REAL NOT NULL DEFAULT 100.0,
                            ""Key"" INTEGER NOT NULL DEFAULT 0,
                            ""Tempo"" REAL NOT NULL DEFAULT 1.0,
                            ""Compressor"" REAL NOT NULL DEFAULT 0.0,
                            ""Limiter"" REAL NOT NULL DEFAULT 0.0,
                            ""Icon"" TEXT NOT NULL DEFAULT '',
                            ""Color"" TEXT NOT NULL DEFAULT '',
                            CONSTRAINT ""FK_OccasionItems_OccasionCategories_OccasionCategoryId"" FOREIGN KEY (""OccasionCategoryId"") REFERENCES ""OccasionCategories"" (""OccasionCategoryId"") ON DELETE CASCADE
                        );

                        CREATE TABLE IF NOT EXISTS ""MusicRequests"" (
                            ""MusicRequestId"" INTEGER NOT NULL CONSTRAINT ""PK_MusicRequests"" PRIMARY KEY AUTOINCREMENT,
                            ""SingerId"" INTEGER NOT NULL,
                            ""Title"" TEXT NOT NULL DEFAULT '',
                            ""Artist"" TEXT NOT NULL DEFAULT '',
                            ""Source"" TEXT NOT NULL DEFAULT 'Local',
                            ""RequestType"" TEXT NOT NULL DEFAULT 'Karaoke',
                            ""Key"" TEXT NOT NULL DEFAULT '0',
                            ""Notes"" TEXT NOT NULL DEFAULT '',
                            ""Status"" TEXT NOT NULL DEFAULT 'Pending',
                            ""Timestamp"" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                            CONSTRAINT ""FK_MusicRequests_Singers_SingerId"" FOREIGN KEY (""SingerId"") REFERENCES ""Singers"" (""SingerId"") ON DELETE CASCADE
                        );

                        CREATE TABLE IF NOT EXISTS ""SingerAudioSettings"" (
                            ""SingerAudioSettingsId"" INTEGER NOT NULL CONSTRAINT ""PK_SingerAudioSettings"" PRIMARY KEY AUTOINCREMENT,
                            ""SingerId"" INTEGER NOT NULL,
                            ""Treble"" REAL NOT NULL DEFAULT 0.0,
                            ""Mid"" REAL NOT NULL DEFAULT 0.0,
                            ""Bass"" REAL NOT NULL DEFAULT 0.0,
                            ""Gain"" REAL NOT NULL DEFAULT 100.0,
                            ""Key"" INTEGER NOT NULL DEFAULT 0,
                            ""Tempo"" REAL NOT NULL DEFAULT 1.0,
                            ""Compressor"" REAL NOT NULL DEFAULT 0.0,
                            ""Limiter"" REAL NOT NULL DEFAULT 0.0,
                            ""Notes"" TEXT NOT NULL DEFAULT '',
                            CONSTRAINT ""FK_SingerAudioSettings_Singers_SingerId"" FOREIGN KEY (""SingerId"") REFERENCES ""Singers"" (""SingerId"") ON DELETE CASCADE
                        );

                        CREATE TABLE IF NOT EXISTS ""EndRotationPlaylistItems"" (
                            ""EndRotationPlaylistItemId"" INTEGER NOT NULL CONSTRAINT ""PK_EndRotationPlaylistItems"" PRIMARY KEY AUTOINCREMENT,
                            ""SongId"" INTEGER NOT NULL,
                            ""Order"" INTEGER NOT NULL DEFAULT 0,
                            CONSTRAINT ""FK_EndRotationPlaylistItems_Songs_SongId"" FOREIGN KEY (""SongId"") REFERENCES ""Songs"" (""SongId"") ON DELETE CASCADE
                        );

                        CREATE TABLE IF NOT EXISTS ""FillInPlaylistItems"" (
                            ""FillInPlaylistItemId"" INTEGER NOT NULL CONSTRAINT ""PK_FillInPlaylistItems"" PRIMARY KEY AUTOINCREMENT,
                            ""SongId"" INTEGER NOT NULL,
                            ""Order"" INTEGER NOT NULL DEFAULT 0,
                            CONSTRAINT ""FK_FillInPlaylistItems_Songs_SongId"" FOREIGN KEY (""SongId"") REFERENCES ""Songs"" (""SongId"") ON DELETE CASCADE
                        );

                        CREATE TABLE IF NOT EXISTS ""OpeningPlaylistItems"" (
                            ""OpeningPlaylistItemId"" INTEGER NOT NULL CONSTRAINT ""PK_OpeningPlaylistItems"" PRIMARY KEY AUTOINCREMENT,
                            ""SongId"" INTEGER NOT NULL,
                            ""Order"" INTEGER NOT NULL DEFAULT 0,
                            CONSTRAINT ""FK_OpeningPlaylistItems_Songs_SongId"" FOREIGN KEY (""SongId"") REFERENCES ""Songs"" (""SongId"") ON DELETE CASCADE
                        );

                        CREATE TABLE IF NOT EXISTS ""RotationEntries"" (
                            ""RotationEntryId"" INTEGER NOT NULL CONSTRAINT ""PK_RotationEntries"" PRIMARY KEY AUTOINCREMENT,
                            ""SingerId"" INTEGER NOT NULL,
                            ""SongId"" INTEGER NOT NULL,
                            ""Status"" TEXT NOT NULL DEFAULT 'Queued',
                            ""Position"" INTEGER NOT NULL DEFAULT 0,
                            ""RequestedKey"" INTEGER NOT NULL DEFAULT 0,
                            ""RequestedTempo"" REAL NOT NULL DEFAULT 1.0,
                            ""TimestampAdded"" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                            CONSTRAINT ""FK_RotationEntries_Singers_SingerId"" FOREIGN KEY (""SingerId"") REFERENCES ""Singers"" (""SingerId"") ON DELETE CASCADE,
                            CONSTRAINT ""FK_RotationEntries_Songs_SongId"" FOREIGN KEY (""SongId"") REFERENCES ""Songs"" (""SongId"") ON DELETE CASCADE
                        );

                        CREATE TABLE IF NOT EXISTS ""SongAudioSettings"" (
                            ""SongAudioSettingsId"" INTEGER NOT NULL CONSTRAINT ""PK_SongAudioSettings"" PRIMARY KEY AUTOINCREMENT,
                            ""SongId"" INTEGER NOT NULL,
                            ""Treble"" REAL NOT NULL DEFAULT 0.0,
                            ""Mid"" REAL NOT NULL DEFAULT 0.0,
                            ""Bass"" REAL NOT NULL DEFAULT 0.0,
                            ""Gain"" REAL NOT NULL DEFAULT 100.0,
                            ""Key"" INTEGER NOT NULL DEFAULT 0,
                            ""Tempo"" REAL NOT NULL DEFAULT 1.0,
                            ""Compressor"" REAL NOT NULL DEFAULT 0.0,
                            ""Limiter"" REAL NOT NULL DEFAULT 0.0,
                            ""Notes"" TEXT NOT NULL DEFAULT '',
                            CONSTRAINT ""FK_SongAudioSettings_Songs_SongId"" FOREIGN KEY (""SongId"") REFERENCES ""Songs"" (""SongId"") ON DELETE CASCADE
                        );

                        CREATE TABLE IF NOT EXISTS ""PerformanceRecordings"" (
                            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PerformanceRecordings"" PRIMARY KEY AUTOINCREMENT,
                            ""SingerName"" TEXT NOT NULL DEFAULT '',
                            ""SingerId"" INTEGER NULL,
                            ""SongTitle"" TEXT NOT NULL DEFAULT '',
                            ""Artist"" TEXT NOT NULL DEFAULT '',
                            ""StartedUtc"" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                            ""DurationSeconds"" REAL NOT NULL DEFAULT 0.0,
                            ""FilePath"" TEXT NOT NULL DEFAULT ''
                        );

                        CREATE TABLE IF NOT EXISTS ""SingerHistory"" (
                            ""SingerHistoryId"" INTEGER PRIMARY KEY AUTOINCREMENT,
                            ""SingerName"" TEXT NOT NULL DEFAULT '',
                            ""SongTitle"" TEXT NOT NULL DEFAULT '',
                            ""Artist"" TEXT NOT NULL DEFAULT '',
                            ""Link"" TEXT NULL,
                            ""Source"" TEXT NOT NULL DEFAULT 'Local',
                            ""Timestamp"" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                            ""Key"" TEXT DEFAULT '0',
                            ""Tempo"" REAL DEFAULT 1.0
                        );
                    ";
                    cmd.ExecuteNonQuery();
                }

                // Ensure all Songs columns exist (heals databases where migrations 2..5 were skipped or interrupted)
                EnsureTableColumns(connection, "Songs",
                [
                    ("Genre", "TEXT NOT NULL DEFAULT ''"),
                    ("Tags", "TEXT NOT NULL DEFAULT ''"),
                    ("Duration", "REAL NOT NULL DEFAULT 0.0"),
                    ("KeyDefault", "INTEGER NOT NULL DEFAULT 0"),
                    ("TempoDefault", "REAL NOT NULL DEFAULT 1.0"),
                    ("DateAdded", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'"),
                    ("LastPlayed", "TEXT NULL"),
                    ("PlayCount", "INTEGER NOT NULL DEFAULT 0"),
                    ("MeasuredLoudnessLufs", "REAL NULL"),
                    ("Difficulty", "TEXT NULL"),
                    ("Key", "TEXT NULL"),
                    ("BPM", "REAL NULL"),
                    ("VocalPresence", "TEXT NULL"),
                    ("Quality", "TEXT NULL"),
                    ("MixInMs", "INTEGER NULL"),
                    ("MixOutMs", "INTEGER NULL"),
                    ("NeedsRepair", "INTEGER NOT NULL DEFAULT 0")
                ]);

                // Ensure all Singers columns exist
                EnsureTableColumns(connection, "Singers",
                [
                    ("Score", "INTEGER NOT NULL DEFAULT 0"),
                    ("RatingPoints", "INTEGER NOT NULL DEFAULT 0"),
                    ("RatingCount", "INTEGER NOT NULL DEFAULT 0"),
                    ("AverageRating", "REAL NOT NULL DEFAULT 0.0"),
                    ("Email", "TEXT NOT NULL DEFAULT ''"),
                    ("PinCode", "TEXT NOT NULL DEFAULT ''"),
                    ("AvatarType", "TEXT NOT NULL DEFAULT 'None'"),
                    ("AvatarSource", "TEXT NOT NULL DEFAULT ''"),
                    ("VocalRange", "TEXT NOT NULL DEFAULT ''"),
                    ("CustomTitle", "TEXT NOT NULL DEFAULT ''"),
                    ("AllowRecording", "INTEGER NOT NULL DEFAULT 0")
                ]);

                // Ensure all SingerHistory columns exist
                EnsureTableColumns(connection, "SingerHistory",
                [
                    ("Key", "TEXT DEFAULT '0'"),
                    ("Tempo", "REAL DEFAULT 1.0")
                ]);

                // Seed default OccasionCategories and record all applied migrations
                using (var seedCmd = connection.CreateCommand())
                {
                    seedCmd.CommandText = @"
                        INSERT OR IGNORE INTO OccasionCategories (OccasionCategoryId, Name, ParentCategoryId) VALUES
                            (1, 'Holiday', NULL),
                            (2, 'Birthday', NULL),
                            (3, 'Anniversary', NULL),
                            (4, 'Wedding', NULL),
                            (5, 'Graduation', NULL),
                            (6, 'Retirement', NULL);

                        INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES
                            ('20260806131321_InitialCreate', '10.0.11'),
                            ('20260905142014_AddMeasuredLoudnessToSong', '10.0.11'),
                            ('20260906152500_AddSmartImportFieldsToSong', '10.0.11'),
                            ('20261007234501_AddFillerAutomixAndRecording', '10.0.11'),
                            ('20261011141500_AddNeedsRepairToSong', '10.0.11');
                    ";
                    seedCmd.ExecuteNonQuery();
                }

                // Deduplicate Songs on FilePath and ensure indexes are valid
                try
                {
                    using var idxCmd = connection.CreateCommand();
                    idxCmd.CommandText = @"
                        DELETE FROM Songs WHERE SongId NOT IN (SELECT MIN(SongId) FROM Songs GROUP BY FilePath);
                        CREATE UNIQUE INDEX IF NOT EXISTS IX_Songs_FilePath ON Songs (FilePath);
                        CREATE INDEX IF NOT EXISTS IX_Songs_Title ON Songs (Title);
                        CREATE INDEX IF NOT EXISTS IX_Songs_Artist ON Songs (Artist);
                        CREATE INDEX IF NOT EXISTS IX_SingerHistory_SingerName ON SingerHistory (SingerName);
                    ";
                    idxCmd.ExecuteNonQuery();
                }
                catch
                {
                    // If an index B-tree is corrupt or non-unique, drop and recreate cleanly
                    using var rebuildIdxCmd = connection.CreateCommand();
                    rebuildIdxCmd.CommandText = @"
                        DROP INDEX IF EXISTS IX_Songs_FilePath;
                        DROP INDEX IF EXISTS IX_Songs_Title;
                        DROP INDEX IF EXISTS IX_Songs_Artist;
                        DELETE FROM Songs WHERE rowid NOT IN (SELECT MIN(rowid) FROM Songs GROUP BY FilePath);
                        CREATE UNIQUE INDEX IF NOT EXISTS IX_Songs_FilePath ON Songs (FilePath);
                        CREATE INDEX IF NOT EXISTS IX_Songs_Title ON Songs (Title);
                        CREATE INDEX IF NOT EXISTS IX_Songs_Artist ON Songs (Artist);
                    ";
                    rebuildIdxCmd.ExecuteNonQuery();
                }
            }
            finally
            {
                if (!wasOpen)
                {
                    connection.Close();
                }
            }
        }

        private static void EnsureTableColumns(DbConnection connection, string tableName, (string ColumnName, string ColumnDefinition)[] requiredColumns)
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var infoCmd = connection.CreateCommand())
            {
                infoCmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
                using var reader = infoCmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(1))
                    {
                        existingColumns.Add(reader.GetString(1));
                    }
                }
            }

            if (existingColumns.Count == 0)
            {
                return;
            }

            foreach (var (columnName, columnDef) in requiredColumns)
            {
                if (!existingColumns.Contains(columnName))
                {
                    try
                    {
                        using var alterCmd = connection.CreateCommand();
                        alterCmd.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDef};";
                        alterCmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Lyracist.Shared.Globals.LogInfo("Lyracist", $"EnsureTableColumns({tableName}.{columnName}): {ex.Message}");
                    }
                }
            }
        }

        private static void SalvageCorruptedDatabase()
        {
            string dbPath = GetDatabaseFilePath();
            if (!System.IO.File.Exists(dbPath))
            {
                return;
            }

            Services.SearchService.ResetSharedConnection();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            string backupPath = $"{dbPath}.corrupt_{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            try
            {
                TryDeleteWalSidecarFiles(dbPath);
                System.IO.File.Copy(dbPath, backupPath, overwrite: true);
            }
            catch
            {
            }

            var salvagedSongs = new List<(string Title, string Artist, string FilePath, bool IsKaraoke, string KaraokeType, string Genre, string Tags, double Duration, int KeyDefault, double TempoDefault, DateTime DateAdded)>();
            try
            {
                using var oldConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Pooling=False");
                oldConn.Open();
                using var cmd = oldConn.CreateCommand();
                cmd.CommandText = "SELECT Title, Artist, FilePath, IsKaraoke, KaraokeType, Genre, Tags, Duration, KeyDefault, TempoDefault, DateAdded FROM Songs;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    try
                    {
                        string filePath = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        if (string.IsNullOrWhiteSpace(filePath)) continue;

                        salvagedSongs.Add((
                            reader.IsDBNull(0) ? "" : reader.GetString(0),
                            reader.IsDBNull(1) ? "" : reader.GetString(1),
                            filePath,
                            !reader.IsDBNull(3) && reader.GetBoolean(3),
                            reader.IsDBNull(4) ? "MP3G" : reader.GetString(4),
                            reader.IsDBNull(5) ? "" : reader.GetString(5),
                            reader.IsDBNull(6) ? "" : reader.GetString(6),
                            reader.IsDBNull(7) ? 0.0 : reader.GetDouble(7),
                            reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                            reader.IsDBNull(9) ? 1.0 : reader.GetDouble(9),
                            reader.IsDBNull(10) || !DateTime.TryParse(reader.GetString(10), out var dt) ? DateTime.UtcNow : dt
                        ));
                    }
                    catch
                    {
                        // Skip unreadable corrupt row and continue salvaging remaining rows
                    }
                }
            }
            catch
            {
            }

            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                TryDeleteWalSidecarFiles(dbPath);
                System.IO.File.Delete(dbPath);

                using var newDb = new LyracistDbContext();
                newDb.Database.Migrate();

                var conn = newDb.Database.GetDbConnection();
                conn.Open();
                try
                {
                    EnsureSchemaColumnsAndTables(conn);
                    if (salvagedSongs.Count > 0)
                    {
                        using var tx = conn.BeginTransaction();
                        using var insertCmd = conn.CreateCommand();
                        insertCmd.Transaction = tx;
                        insertCmd.CommandText = @"
                            INSERT OR IGNORE INTO Songs
                            (Title, Artist, FilePath, IsKaraoke, KaraokeType, Genre, Tags, Duration, KeyDefault, TempoDefault, DateAdded, PlayCount, NeedsRepair)
                            VALUES ($title, $artist, $filePath, $isKaraoke, $karaokeType, $genre, $tags, $duration, $keyDefault, $tempoDefault, $dateAdded, 0, 0);";

                        var pTitle = insertCmd.CreateParameter(); pTitle.ParameterName = "$title"; insertCmd.Parameters.Add(pTitle);
                        var pArtist = insertCmd.CreateParameter(); pArtist.ParameterName = "$artist"; insertCmd.Parameters.Add(pArtist);
                        var pPath = insertCmd.CreateParameter(); pPath.ParameterName = "$filePath"; insertCmd.Parameters.Add(pPath);
                        var pKaraoke = insertCmd.CreateParameter(); pKaraoke.ParameterName = "$isKaraoke"; insertCmd.Parameters.Add(pKaraoke);
                        var pType = insertCmd.CreateParameter(); pType.ParameterName = "$karaokeType"; insertCmd.Parameters.Add(pType);
                        var pGenre = insertCmd.CreateParameter(); pGenre.ParameterName = "$genre"; insertCmd.Parameters.Add(pGenre);
                        var pTags = insertCmd.CreateParameter(); pTags.ParameterName = "$tags"; insertCmd.Parameters.Add(pTags);
                        var pDur = insertCmd.CreateParameter(); pDur.ParameterName = "$duration"; insertCmd.Parameters.Add(pDur);
                        var pKey = insertCmd.CreateParameter(); pKey.ParameterName = "$keyDefault"; insertCmd.Parameters.Add(pKey);
                        var pTempo = insertCmd.CreateParameter(); pTempo.ParameterName = "$tempoDefault"; insertCmd.Parameters.Add(pTempo);
                        var pDate = insertCmd.CreateParameter(); pDate.ParameterName = "$dateAdded"; insertCmd.Parameters.Add(pDate);
                        insertCmd.Prepare();

                        foreach (var s in salvagedSongs)
                        {
                            pTitle.Value = s.Title;
                            pArtist.Value = s.Artist;
                            pPath.Value = s.FilePath;
                            pKaraoke.Value = s.IsKaraoke ? 1 : 0;
                            pType.Value = s.KaraokeType;
                            pGenre.Value = s.Genre;
                            pTags.Value = s.Tags;
                            pDur.Value = s.Duration;
                            pKey.Value = s.KeyDefault;
                            pTempo.Value = s.TempoDefault;
                            pDate.Value = s.DateAdded.ToString("o");
                            insertCmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                    }
                }
                finally
                {
                    conn.Close();
                }

                new Services.SearchService(newDb).EnsureFtsIndexSynchronized(forceCheck: true);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "SalvageCorruptedDatabase failed", ex);
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
