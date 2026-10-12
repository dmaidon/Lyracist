// Edited on Oct 11, 2026 @ 13:38:00 -> Track initialized database paths per dbPath to support dynamic overrides
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Lyracist.Data.Models;
using Microsoft.Data.Sqlite;

namespace Lyracist.Data.Services
{
    /// <summary>
    /// Manages the standalone noartist.db database, which tracks songs lacking artist metadata
    /// independently of lyracist.db. If lyracist.db is cleared or deleted, noartist.db persists,
    /// allowing subsequent scans to skip re-probing these tracks and retaining the repair audit list.
    /// </summary>
    public static class NoArtistStore
    {
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private static readonly HashSet<string> _initializedPaths = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Optional path override for testing environments.
        /// </summary>
        public static string? OverrideDbPath { get; set; }

        public static string GetDatabasePath()
        {
            if (!string.IsNullOrEmpty(OverrideDbPath))
            {
                return OverrideDbPath;
            }

            string dataDir = Lyracist.Shared.Globals.DataDir;
            Directory.CreateDirectory(dataDir);
            return Path.Combine(dataDir, "noartist.db");
        }

        private static string GetConnectionString()
        {
            return $"Data Source={GetDatabasePath()}";
        }

        private static async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
            string dbPath = GetDatabasePath();
            if (_initializedPaths.Contains(dbPath)) return;

            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_initializedPaths.Contains(dbPath)) return;

                string? dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await using var connection = new SqliteConnection(GetConnectionString());
                await connection.OpenAsync(cancellationToken);

                // Enable WAL mode for high concurrency
                await connection.ExecuteAsync("PRAGMA journal_mode=WAL;");

                const string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS UnresolvedTracks (
                        FilePath TEXT PRIMARY KEY,
                        Title TEXT NOT NULL,
                        Duration REAL NOT NULL,
                        KaraokeType TEXT NOT NULL,
                        IsKaraoke INTEGER NOT NULL,
                        DateFlagged TEXT NOT NULL,
                        Notes TEXT
                    );";

                await connection.ExecuteAsync(createTableSql);
                await connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_UnresolvedTracks_Title ON UnresolvedTracks(Title);");
                _initializedPaths.Add(dbPath);
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task<Dictionary<string, UnresolvedTrack>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT FilePath, Title, Duration, KaraokeType, IsKaraoke, DateFlagged, Notes FROM UnresolvedTracks;";
            var rows = await connection.QueryAsync<UnresolvedTrackDto>(sql);

            var dict = new Dictionary<string, UnresolvedTrack>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows)
            {
                dict[r.FilePath] = new UnresolvedTrack
                {
                    FilePath = r.FilePath,
                    Title = r.Title,
                    Duration = r.Duration,
                    KaraokeType = r.KaraokeType,
                    IsKaraoke = r.IsKaraoke == 1,
                    DateFlagged = DateTime.TryParse(r.DateFlagged, out var dt) ? dt : DateTime.UtcNow,
                    Notes = r.Notes
                };
            }

            return dict;
        }

        public static async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM UnresolvedTracks;");
        }

        public static async Task<bool> ContainsAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            await EnsureInitializedAsync(cancellationToken);

            await using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync(cancellationToken);

            int count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM UnresolvedTracks WHERE FilePath = @FilePath;",
                new { FilePath = filePath });

            return count > 0;
        }

        public static async Task UpsertAsync(UnresolvedTrack track, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(track.FilePath)) return;

            await UpsertBatchAsync([track], cancellationToken);
        }

        public static async Task UpsertBatchAsync(IEnumerable<UnresolvedTrack> tracks, CancellationToken cancellationToken = default)
        {
            var trackList = tracks.Where(t => !string.IsNullOrWhiteSpace(t.FilePath)).ToList();
            if (trackList.Count == 0) return;

            await EnsureInitializedAsync(cancellationToken);

            await _lock.WaitAsync(cancellationToken);
            try
            {
                await using var connection = new SqliteConnection(GetConnectionString());
                await connection.OpenAsync(cancellationToken);
                await using var tx = await connection.BeginTransactionAsync(cancellationToken);

                const string sql = @"
                    INSERT INTO UnresolvedTracks (FilePath, Title, Duration, KaraokeType, IsKaraoke, DateFlagged, Notes)
                    VALUES (@FilePath, @Title, @Duration, @KaraokeType, @IsKaraoke, @DateFlagged, @Notes)
                    ON CONFLICT(FilePath) DO UPDATE SET
                        Title = excluded.Title,
                        Duration = excluded.Duration,
                        KaraokeType = excluded.KaraokeType,
                        IsKaraoke = excluded.IsKaraoke,
                        DateFlagged = excluded.DateFlagged,
                        Notes = COALESCE(excluded.Notes, UnresolvedTracks.Notes);
                ";

                foreach (var track in trackList)
                {
                    await connection.ExecuteAsync(sql, new
                    {
                        track.FilePath,
                        track.Title,
                        track.Duration,
                        track.KaraokeType,
                        IsKaraoke = track.IsKaraoke ? 1 : 0,
                        DateFlagged = track.DateFlagged.ToString("o"),
                        track.Notes
                    }, transaction: tx);
                }

                await tx.CommitAsync(cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task RemoveAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            await RemoveBatchAsync([filePath], cancellationToken);
        }

        public static async Task RemoveBatchAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
        {
            var pathList = filePaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            if (pathList.Count == 0) return;

            await EnsureInitializedAsync(cancellationToken);

            await _lock.WaitAsync(cancellationToken);
            try
            {
                await using var connection = new SqliteConnection(GetConnectionString());
                await connection.OpenAsync(cancellationToken);
                await using var tx = await connection.BeginTransactionAsync(cancellationToken);

                const string sql = "DELETE FROM UnresolvedTracks WHERE FilePath = @FilePath;";

                foreach (var path in pathList)
                {
                    await connection.ExecuteAsync(sql, new { FilePath = path }, transaction: tx);
                }

                await tx.CommitAsync(cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);

            await _lock.WaitAsync(cancellationToken);
            try
            {
                await using var connection = new SqliteConnection(GetConnectionString());
                await connection.OpenAsync(cancellationToken);
                await connection.ExecuteAsync("DELETE FROM UnresolvedTracks;");
            }
            finally
            {
                _lock.Release();
            }
        }

        // Internal DTO for Dapper mapping
        private sealed class UnresolvedTrackDto
        {
            public string FilePath { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public double Duration { get; set; }
            public string KaraokeType { get; set; } = string.Empty;
            public int IsKaraoke { get; set; }
            public string DateFlagged { get; set; } = string.Empty;
            public string? Notes { get; set; }
        }
    }
}
