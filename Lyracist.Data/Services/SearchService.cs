// Edited on Oct 30, 2026 @ 12:01:00 -> Add live FTS5 MATCH probe, corrupt FTS5 shadow-table recovery, and automatic self-healing on search fallback
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lyracist.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Lyracist.Data.Services
{
    public class SearchService(LyracistDbContext context)
    {
        private readonly LyracistDbContext _context = context;

        private static SqliteConnection? _sharedConnection;
        private static readonly System.Threading.Lock _connectionLock = new();
        private static readonly System.Threading.Lock _verificationLock = new();
        private static bool _ftsTableVerified = false;
        private static readonly System.Threading.SemaphoreSlim _querySemaphore = new(1, 1);

        private static async Task<SqliteConnection> GetSharedConnectionAsync()
        {
            lock (_connectionLock)
            {
                if (_sharedConnection != null && _sharedConnection.State == System.Data.ConnectionState.Open)
                {
                    return _sharedConnection;
                }
            }

            var conn = new SqliteConnection(LyracistDbContext.GetConnectionString());
            await conn.OpenAsync().ConfigureAwait(false);

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA busy_timeout=10000; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            lock (_connectionLock)
            {
                if (_sharedConnection != null && _sharedConnection.State == System.Data.ConnectionState.Open)
                {
                    conn.Dispose();
                    return _sharedConnection;
                }
                // The old connection (if any) is dead/closed at this point - dispose it before
                // dropping the reference so it doesn't leak its underlying handle.
                _sharedConnection?.Dispose();
                _sharedConnection = conn;
                return _sharedConnection;
            }
        }

        public static void ResetSharedConnection()
        {
            lock (_connectionLock)
            {
                try
                {
                    _sharedConnection?.Close();
                    _sharedConnection?.Dispose();
                }
                catch
                {
                }
                finally
                {
                    _sharedConnection = null;
                }
            }
        }

        // ==========================================
        // TEXT NORMALIZATION
        // ==========================================

        public string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Convert to lowercase
            string lower = input.ToLowerInvariant();

            // Decompose Unicode characters (separates diacritics from base characters)
            string normalized = lower.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (char c in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                // Keep only base characters (ignore non-spacing marks/accents)
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                    {
                        sb.Append(c);
                    }
                }
            }

            // Re-compose unicode format and collapse extra whitespaces
            string cleaned = sb.ToString().Normalize(NormalizationForm.FormC);
            return Regex.Replace(cleaned, @"\s+", " ").Trim();
        }

        // ==========================================
        // INDEXING OPERATION
        // ==========================================

        public async Task IndexSong(Song song)
        {
            EnsureFtsTableExists();

            var normalizedTitle = Normalize(song.Title);
            var normalizedArtist = Normalize(song.Artist);

            // Bind rowid = SongId so FTS5 replaces in-place instead of appending duplicates and
            // DELETE WHERE rowid = @SongId uses the B-tree primary key instead of a full-table scan.
            await _context.Database.ExecuteSqlRawAsync(
                "INSERT OR REPLACE INTO SongSearch (rowid, SongId, Title, Artist, NormalizedTitle, NormalizedArtist) VALUES ({0}, {0}, {1}, {2}, {3}, {4})",
                song.SongId,
                song.Title ?? string.Empty,
                song.Artist ?? string.Empty,
                normalizedTitle,
                normalizedArtist
            ).ConfigureAwait(false);
        }

        // ==========================================
        // FTS5 SEARCH OPERATION
        // ==========================================

        public void EnsureFtsTableExists()
        {
            EnsureFtsIndexSynchronized(forceCheck: false);
        }

        public void EnsureFtsIndexSynchronized(bool forceCheck = true)
        {
            // Held for the whole check-then-act sequence below, not just the flag read: releasing it
            // between the check and the DROP/CREATE let two threads both pass the check and race each
            // other (one dropping the table while the other queried it). See ScanningService callers
            // and Search()/SearchSync(), all of which can call this concurrently on a cold start.
            lock (_verificationLock)
            {
                if (!forceCheck && _ftsTableVerified) return;

                try
                {
                    var connection = _context.Database.GetDbConnection();
                    bool wasOpen = connection.State == System.Data.ConnectionState.Open;
                    if (!wasOpen)
                    {
                        connection.Open();
                    }

                    try
                    {
                        LyracistDbContext.EnsureSchemaColumnsAndTables(connection);

                        string sqlSchema = "";
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='SongSearch';";
                            var result = cmd.ExecuteScalar();
                            if (result != null)
                            {
                                sqlSchema = result.ToString() ?? "";
                            }
                        }

                        bool needsRebuild = string.IsNullOrEmpty(sqlSchema) || !sqlSchema.Contains("fts5", StringComparison.OrdinalIgnoreCase);
                        long songCount = 0;

                        if (!needsRebuild)
                        {
                            try
                            {
                                using var countCmd = connection.CreateCommand();
                                countCmd.CommandText = @"
                                    SELECT 
                                        (SELECT COUNT(*) FROM Songs),
                                        (SELECT COUNT(*) FROM SongSearch),
                                        EXISTS(SELECT 1 FROM SongSearch WHERE rowid != CAST(SongId AS INTEGER) LIMIT 1),
                                        EXISTS(SELECT 1 FROM Songs s WHERE NOT EXISTS (SELECT 1 FROM SongSearch f WHERE f.rowid = s.SongId) LIMIT 1),
                                        EXISTS(SELECT 1 FROM SongSearch WHERE (Title != '' AND NormalizedTitle = '') OR (Artist != '' AND NormalizedArtist = '') LIMIT 1);";
                                using var reader = countCmd.ExecuteReader();
                                if (reader.Read())
                                {
                                    songCount = reader.GetInt64(0);
                                    long ftsCount = reader.GetInt64(1);
                                    bool hasMismatchedRowId = reader.GetInt64(2) != 0;
                                    bool hasMissingSongId = reader.GetInt64(3) != 0;
                                    bool hasUnnormalizedRows = reader.GetInt64(4) != 0;
                                    if (songCount != ftsCount || hasMismatchedRowId || hasMissingSongId || hasUnnormalizedRows)
                                    {
                                        needsRebuild = true;
                                    }
                                }
                            }
                            catch
                            {
                                needsRebuild = true;
                            }
                        }

                        // Live FTS5 MATCH probe: verifies that the underlying FTS5 shadow tables
                        // (SongSearch_data, SongSearch_idx, etc.) are intact and actually return matches.
                        if (!needsRebuild && songCount > 0)
                        {
                            try
                            {
                                string? sampleToken = null;
                                using (var sampleCmd = connection.CreateCommand())
                                {
                                    sampleCmd.CommandText = "SELECT Title, Artist FROM Songs WHERE (Title IS NOT NULL AND Title != '') OR (Artist IS NOT NULL AND Artist != '') LIMIT 10;";
                                    using var sampleReader = sampleCmd.ExecuteReader();
                                    while (sampleReader.Read())
                                    {
                                        string t = sampleReader.IsDBNull(0) ? "" : sampleReader.GetString(0);
                                        string a = sampleReader.IsDBNull(1) ? "" : sampleReader.GetString(1);
                                        string candidate = Normalize(!string.IsNullOrWhiteSpace(t) ? t : a);
                                        if (!string.IsNullOrWhiteSpace(candidate))
                                        {
                                            sampleToken = candidate.Split([' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                                            if (!string.IsNullOrWhiteSpace(sampleToken))
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(sampleToken))
                                {
                                    using var probeCmd = connection.CreateCommand();
                                    probeCmd.CommandText = "SELECT SongId FROM SongSearch WHERE SongSearch MATCH @probe LIMIT 1;";
                                    var p = probeCmd.CreateParameter();
                                    p.ParameterName = "@probe";
                                    p.Value = $"\"{sampleToken}\"*";
                                    probeCmd.Parameters.Add(p);
                                    var probeResult = probeCmd.ExecuteScalar();
                                    if (probeResult == null || probeResult == DBNull.Value)
                                    {
                                        needsRebuild = true;
                                    }
                                }
                            }
                            catch
                            {
                                needsRebuild = true;
                            }
                        }

                        if (needsRebuild)
                        {
                            ResetSharedConnection();

                            try
                            {
                                using var dropCmd = connection.CreateCommand();
                                dropCmd.CommandText = "DROP TABLE IF EXISTS SongSearch;";
                                dropCmd.ExecuteNonQuery();
                            }
                            catch
                            {
                                // Corrupt FTS5 virtual table or shadow tables can prevent DROP TABLE SongSearch;
                                // drop shadow tables and purge SongSearch% from sqlite_master directly.
                                using var forceDropCmd = connection.CreateCommand();
                                forceDropCmd.CommandText = @"
                                    DROP TABLE IF EXISTS SongSearch_data;
                                    DROP TABLE IF EXISTS SongSearch_idx;
                                    DROP TABLE IF EXISTS SongSearch_content;
                                    DROP TABLE IF EXISTS SongSearch_docsize;
                                    DROP TABLE IF EXISTS SongSearch_config;
                                    PRAGMA writable_schema = ON;
                                    DELETE FROM sqlite_master WHERE name LIKE 'SongSearch%' OR tbl_name LIKE 'SongSearch%';
                                    PRAGMA writable_schema = OFF;
                                    PRAGMA integrity_check(1);
                                ";
                                forceDropCmd.ExecuteNonQuery();
                            }

                            using var transaction = connection.BeginTransaction();
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.Transaction = transaction;
                                cmd.CommandText = "CREATE VIRTUAL TABLE SongSearch USING fts5(SongId UNINDEXED, Title, Artist, NormalizedTitle, NormalizedArtist);";
                                cmd.ExecuteNonQuery();
                            }

                            using (var readCmd = connection.CreateCommand())
                            using (var insertCmd = connection.CreateCommand())
                            {
                                readCmd.Transaction = transaction;
                                readCmd.CommandText = "SELECT SongId, Title, Artist FROM Songs;";

                                insertCmd.Transaction = transaction;
                                insertCmd.CommandText = @"
                                    INSERT INTO SongSearch (rowid, SongId, Title, Artist, NormalizedTitle, NormalizedArtist)
                                    VALUES (@SongId, @SongId, @Title, @Artist, @NormalizedTitle, @NormalizedArtist);";

                                var songIdParam = insertCmd.CreateParameter();
                                songIdParam.ParameterName = "@SongId";
                                insertCmd.Parameters.Add(songIdParam);

                                var titleParam = insertCmd.CreateParameter();
                                titleParam.ParameterName = "@Title";
                                insertCmd.Parameters.Add(titleParam);

                                var artistParam = insertCmd.CreateParameter();
                                artistParam.ParameterName = "@Artist";
                                insertCmd.Parameters.Add(artistParam);

                                var normTitleParam = insertCmd.CreateParameter();
                                normTitleParam.ParameterName = "@NormalizedTitle";
                                insertCmd.Parameters.Add(normTitleParam);

                                var normArtistParam = insertCmd.CreateParameter();
                                normArtistParam.ParameterName = "@NormalizedArtist";
                                insertCmd.Parameters.Add(normArtistParam);

                                insertCmd.Prepare();

                                using var reader = readCmd.ExecuteReader();
                                while (reader.Read())
                                {
                                    int id = reader.GetInt32(0);
                                    string title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                                    string artist = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);

                                    songIdParam.Value = id;
                                    titleParam.Value = title;
                                    artistParam.Value = artist;
                                    normTitleParam.Value = Normalize(title);
                                    normArtistParam.Value = Normalize(artist);

                                    insertCmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                        }

                        _ftsTableVerified = true;
                    }
                    finally
                    {
                        if (!wasOpen)
                        {
                            connection.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", "SearchService.EnsureFtsTableExists", ex);
                }
            }
        }

        public async Task<List<Song>> Search(string query, bool? isKaraoke = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return [];
            }

            EnsureFtsTableExists();

            string ftsQuery = PrepareFtsQuery(query);
            if (string.IsNullOrWhiteSpace(ftsQuery))
            {
                return [];
            }

            await _querySemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var connection = await GetSharedConnectionAsync().ConfigureAwait(false);

                // Query matching songs and join with their respective audio settings, clamping to 150 items max
                const string sql = @"
                    SELECT s.*, a.* 
                    FROM Songs s
                    LEFT JOIN SongAudioSettings a ON s.SongId = a.SongId
                    WHERE s.SongId IN (
                        SELECT SongId FROM SongSearch WHERE SongSearch MATCH @ftsQuery
                    )
                    AND (@isKaraoke IS NULL OR s.IsKaraoke = @isKaraoke)
                    LIMIT 150";

                var results = (await connection.QueryAsync<Song, SongAudioSettings, Song>(
                    sql,
                    (song, audioSettings) =>
                    {
                        song.AudioSettings = audioSettings;
                        return song;
                    },
                    new { ftsQuery, isKaraoke },
                    splitOn: "SongAudioSettingsId"
                ).ConfigureAwait(false)).ToList();

                if (results.Count > 0)
                {
                    return results;
                }

                var fallbackResults = await SearchFallbackLikeAsync(connection, query, isKaraoke).ConfigureAwait(false);
                if (fallbackResults.Count > 0)
                {
                    // If the LIKE fallback found a Title/Artist prefix match that FTS5 missed,
                    // trigger a background FTS5 verification/rebuild so subsequent searches are fast.
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            using var repairDb = new LyracistDbContext();
                            new SearchService(repairDb).EnsureFtsIndexSynchronized(forceCheck: true);
                        }
                        catch
                        {
                        }
                    });
                }

                return fallbackResults;
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Dapper search execution failed", ex);
                try
                {
                    ResetSharedConnection();
                    EnsureFtsIndexSynchronized(forceCheck: true);
                    await using var fallbackConn = new SqliteConnection(LyracistDbContext.GetConnectionString());
                    await fallbackConn.OpenAsync().ConfigureAwait(false);
                    return await SearchFallbackLikeAsync(fallbackConn, query, isKaraoke).ConfigureAwait(false);
                }
                catch (Exception fallbackEx)
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", "Fallback LIKE search execution failed", fallbackEx);
                    return [];
                }
            }
            finally
            {
                _querySemaphore.Release();
            }
        }

        private (string Sql, DynamicParameters Params) BuildLikeFallbackQuery(string query, bool? isKaraoke)
        {
            string normalized = Normalize(query);
            var words = string.IsNullOrWhiteSpace(normalized)
                ? query.Split([' '], StringSplitOptions.RemoveEmptyEntries)
                : normalized.Split([' '], StringSplitOptions.RemoveEmptyEntries);

            var parameters = new DynamicParameters();
            parameters.Add("isKaraoke", isKaraoke);

            var whereClauses = new List<string>();
            for (int i = 0; i < words.Length; i++)
            {
                string paramName = $"w{i}";
                parameters.Add(paramName, $"%{words[i]}%");
                whereClauses.Add($"(s.Title LIKE @{paramName} OR s.Artist LIKE @{paramName} OR s.FilePath LIKE @{paramName})");
            }

            string wordFilter = whereClauses.Count > 0 ? string.Join(" AND ", whereClauses) : "1=1";
            string sql = $@"
                SELECT s.*, a.*
                FROM Songs s
                LEFT JOIN SongAudioSettings a ON s.SongId = a.SongId
                WHERE {wordFilter}
                AND (@isKaraoke IS NULL OR s.IsKaraoke = @isKaraoke)
                LIMIT 150";

            return (sql, parameters);
        }

        private async Task<List<Song>> SearchFallbackLikeAsync(SqliteConnection connection, string query, bool? isKaraoke)
        {
            var (sql, parameters) = BuildLikeFallbackQuery(query, isKaraoke);
            var results = await connection.QueryAsync<Song, SongAudioSettings, Song>(
                sql,
                (song, audioSettings) =>
                {
                    song.AudioSettings = audioSettings;
                    return song;
                },
                parameters,
                splitOn: "SongAudioSettingsId"
            ).ConfigureAwait(false);

            return [.. results];
        }

        private List<Song> SearchFallbackLikeSync(SqliteConnection connection, string query, bool? isKaraoke)
        {
            var (sql, parameters) = BuildLikeFallbackQuery(query, isKaraoke);
            var results = connection.Query<Song, SongAudioSettings, Song>(
                sql,
                (song, audioSettings) =>
                {
                    song.AudioSettings = audioSettings;
                    return song;
                },
                parameters,
                splitOn: "SongAudioSettingsId"
            );

            return [.. results];
        }

        // Helper to transform user search query into a safe FTS5 MATCH expression
        private string PrepareFtsQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            // Run the query through the same normalization used to build NormalizedTitle/
            // NormalizedArtist at index time (lowercase, diacritics stripped) - otherwise a query
            // that itself contains accented characters (typed via an IME, pasted, etc.) wouldn't
            // match a library entry whose accent was stripped when it was indexed.
            string normalizedQuery = Normalize(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery))
                return string.Empty;

            // Remove special characters that have syntax meaning in SQLite FTS5 (e.g. *, :, AND, OR)
            string cleaned = Regex.Replace(normalizedQuery, @"[^\w\s]", " ");

            // Split into words and wrap each word in double quotes before '*' for safe prefix matching
            var words = cleaned.Split([' '], StringSplitOptions.RemoveEmptyEntries)
                               .Select(word => $"\"{word}\"*")
                               .ToList();

            if (words.Count == 0)
                return string.Empty;

            // Join word prefixes with AND operator
            return string.Join(" AND ", words);
        }

        public async Task RemoveSongFromIndex(int songId)
        {
            EnsureFtsTableExists();

            await _context.Database.ExecuteSqlRawAsync(
                "DELETE FROM SongSearch WHERE rowid = {0}",
                songId
            ).ConfigureAwait(false);
        }

        public async Task RemoveSongsFromIndexBatch(IEnumerable<int> songIds)
        {
            var idList = songIds.ToList();
            if (idList.Count == 0) return;

            EnsureFtsTableExists();

            var connection = _context.Database.GetDbConnection();
            bool wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
            {
                await connection.OpenAsync().ConfigureAwait(false);
            }

            System.Data.Common.DbTransaction? localTransaction = null;
            try
            {
                await using var command = connection.CreateCommand();
                var currentTransaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                if (currentTransaction != null)
                {
                    command.Transaction = currentTransaction;
                }
                else
                {
                    localTransaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
                    command.Transaction = localTransaction;
                }

                command.CommandText = "DELETE FROM SongSearch WHERE rowid = @SongId";
                var songIdParam = command.CreateParameter();
                songIdParam.ParameterName = "@SongId";
                command.Parameters.Add(songIdParam);
                await command.PrepareAsync().ConfigureAwait(false);

                foreach (int songId in idList)
                {
                    songIdParam.Value = songId;
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                if (localTransaction != null)
                {
                    await localTransaction.CommitAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                if (localTransaction != null)
                {
                    await localTransaction.DisposeAsync().ConfigureAwait(false);
                }
                if (!wasOpen)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task IndexSongsBatch(IEnumerable<Song> songs)
        {
            var songList = songs.ToList();
            if (songList.Count == 0) return;

            EnsureFtsTableExists();

            var connection = _context.Database.GetDbConnection();
            bool wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
            {
                await connection.OpenAsync().ConfigureAwait(false);
            }

            System.Data.Common.DbTransaction? localTransaction = null;
            try
            {
                await using var command = connection.CreateCommand();
                var currentTransaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                if (currentTransaction != null)
                {
                    command.Transaction = currentTransaction;
                }
                else
                {
                    localTransaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
                    command.Transaction = localTransaction;
                }

                command.CommandText = @"
                    INSERT OR REPLACE INTO SongSearch 
                    (rowid, SongId, Title, Artist, NormalizedTitle, NormalizedArtist) 
                    VALUES (@SongId, @SongId, @Title, @Artist, @NormalizedTitle, @NormalizedArtist)";

                var songIdParam = command.CreateParameter();
                songIdParam.ParameterName = "@SongId";
                command.Parameters.Add(songIdParam);

                var titleParam = command.CreateParameter();
                titleParam.ParameterName = "@Title";
                command.Parameters.Add(titleParam);

                var artistParam = command.CreateParameter();
                artistParam.ParameterName = "@Artist";
                command.Parameters.Add(artistParam);

                var normTitleParam = command.CreateParameter();
                normTitleParam.ParameterName = "@NormalizedTitle";
                command.Parameters.Add(normTitleParam);

                var normArtistParam = command.CreateParameter();
                normArtistParam.ParameterName = "@NormalizedArtist";
                command.Parameters.Add(normArtistParam);

                await command.PrepareAsync().ConfigureAwait(false);

                foreach (var song in songList)
                {
                    songIdParam.Value = song.SongId;
                    titleParam.Value = song.Title ?? string.Empty;
                    artistParam.Value = song.Artist ?? string.Empty;
                    normTitleParam.Value = Normalize(song.Title ?? string.Empty);
                    normArtistParam.Value = Normalize(song.Artist ?? string.Empty);

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                if (localTransaction != null)
                {
                    await localTransaction.CommitAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                if (localTransaction != null)
                {
                    await localTransaction.DisposeAsync().ConfigureAwait(false);
                }
                if (!wasOpen)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }
        }

        public List<Song> SearchSync(string query, bool? isKaraoke = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return [];
            }

            EnsureFtsTableExists();

            string ftsQuery = PrepareFtsQuery(query);
            if (string.IsNullOrWhiteSpace(ftsQuery))
            {
                return [];
            }

            try
            {
                using var connection = new SqliteConnection(LyracistDbContext.GetConnectionString());
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA busy_timeout=10000; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
                    cmd.ExecuteNonQuery();
                }

                const string sql = @"
                    SELECT s.*, a.* 
                    FROM Songs s
                    LEFT JOIN SongAudioSettings a ON s.SongId = a.SongId
                    WHERE s.SongId IN (
                        SELECT SongId FROM SongSearch WHERE SongSearch MATCH @ftsQuery
                    )
                    AND (@isKaraoke IS NULL OR s.IsKaraoke = @isKaraoke)
                    LIMIT 150";

                var results = connection.Query<Song, SongAudioSettings, Song>(
                    sql,
                    (song, audioSettings) =>
                    {
                        song.AudioSettings = audioSettings;
                        return song;
                    },
                    new { ftsQuery, isKaraoke },
                    splitOn: "SongAudioSettingsId"
                ).ToList();

                if (results.Count > 0)
                {
                    return results;
                }

                var fallbackResults = SearchFallbackLikeSync(connection, query, isKaraoke);
                if (fallbackResults.Count > 0)
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            using var repairDb = new LyracistDbContext();
                            new SearchService(repairDb).EnsureFtsIndexSynchronized(forceCheck: true);
                        }
                        catch
                        {
                        }
                    });
                }

                return fallbackResults;
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Dapper sync search execution failed", ex);
                try
                {
                    ResetSharedConnection();
                    EnsureFtsIndexSynchronized(forceCheck: true);
                    using var fallbackConn = new SqliteConnection(LyracistDbContext.GetConnectionString());
                    fallbackConn.Open();
                    return SearchFallbackLikeSync(fallbackConn, query, isKaraoke);
                }
                catch (Exception fallbackEx)
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", "Fallback sync LIKE search execution failed", fallbackEx);
                    return [];
                }
            }
        }
    }
}
