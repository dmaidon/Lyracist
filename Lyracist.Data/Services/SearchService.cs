// Edited on Aug 25, 2026 @ 06:15:00 -> Fix RCS1261 async disposal on commands and RCS1118 const SQL strings
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
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA busy_timeout=10000; PRAGMA journal_mode=WAL;";
                await cmd.ExecuteNonQueryAsync();
            }

            lock (_connectionLock)
            {
                if (_sharedConnection != null && _sharedConnection.State == System.Data.ConnectionState.Open)
                {
                    conn.Dispose();
                    return _sharedConnection;
                }
                _sharedConnection = conn;
                return _sharedConnection;
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
            var normalizedTitle = Normalize(song.Title);
            var normalizedArtist = Normalize(song.Artist);

            // Execute an INSERT OR REPLACE raw SQL statement on the SongSearch virtual table
            await _context.Database.ExecuteSqlRawAsync(
                "INSERT OR REPLACE INTO SongSearch (SongId, Title, Artist, NormalizedTitle, NormalizedArtist) VALUES ({0}, {1}, {2}, {3}, {4})",
                song.SongId,
                song.Title ?? string.Empty,
                song.Artist ?? string.Empty,
                normalizedTitle,
                normalizedArtist
            );
        }

        // ==========================================
        // FTS5 SEARCH OPERATION
        // ==========================================

        public void EnsureFtsTableExists()
        {
            lock (_verificationLock)
            {
                if (_ftsTableVerified) return;
            }

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

                    if (string.IsNullOrEmpty(sqlSchema) || !sqlSchema.Contains("fts5", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var transaction = connection.BeginTransaction())
                        {
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.Transaction = transaction;
                                cmd.CommandText = "DROP TABLE IF EXISTS SongSearch;";
                                cmd.ExecuteNonQuery();

                                cmd.CommandText = "CREATE VIRTUAL TABLE SongSearch USING fts5(SongId UNINDEXED, Title, Artist, NormalizedTitle, NormalizedArtist);";
                                cmd.ExecuteNonQuery();
                            }
                            transaction.Commit();
                        }

                        // Reindex all songs
                        var songs = _context.Songs.AsNoTracking().ToList();
                        IndexSongsBatch(songs).GetAwaiter().GetResult();
                    }

                    lock (_verificationLock)
                    {
                        _ftsTableVerified = true;
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to ensure FTS table exists: {ex.Message}");
            }
        }

        public async Task<List<Song>> Search(string query)
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

            await _querySemaphore.WaitAsync();
            try
            {
                var connection = await GetSharedConnectionAsync();

                // Query matching songs and join with their respective audio settings, clamping to 150 items max
                const string sql = @"
                    SELECT s.*, a.* 
                    FROM Songs s
                    LEFT JOIN SongAudioSettings a ON s.SongId = a.SongId
                    WHERE s.SongId IN (
                        SELECT SongId FROM SongSearch WHERE SongSearch MATCH @ftsQuery
                    )
                    LIMIT 150";

                var results = await connection.QueryAsync<Song, SongAudioSettings, Song>(
                    sql,
                    (song, audioSettings) =>
                    {
                        song.AudioSettings = audioSettings;
                        return song;
                    },
                    new { ftsQuery },
                    splitOn: "SongAudioSettingsId"
                );

                return [.. results];
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dapper search execution failed: {ex.Message}");
                // Fallback to standard EF Core query (clamped) in case of connection exceptions
                return await _context.Songs
                    .FromSqlRaw("SELECT * FROM Songs WHERE SongId IN (SELECT SongId FROM SongSearch WHERE SongSearch MATCH {0})", ftsQuery)
                    .AsNoTracking()
                    .Include(s => s.AudioSettings)
                    .Take(150)
                    .ToListAsync();
            }
            finally
            {
                _querySemaphore.Release();
            }
        }

        // Helper to transform user search query into a safe FTS5 MATCH expression
        private string PrepareFtsQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            // Remove special characters that have syntax meaning in SQLite FTS5 (e.g. *, :, AND, OR)
            string cleaned = Regex.Replace(query, @"[^\w\s]", " ");

            // Split into words and append '*' to each word for prefix matching
            var words = cleaned.Split([' '], StringSplitOptions.RemoveEmptyEntries)
                               .Select(word => $"{word}*")
                               .ToList();

            if (words.Count == 0)
                return string.Empty;

            // Join word prefixes with AND operator
            return string.Join(" AND ", words);
        }

        public async Task RemoveSongFromIndex(int songId)
        {
            await _context.Database.ExecuteSqlRawAsync(
                "DELETE FROM SongSearch WHERE SongId = {0}",
                songId
            );
        }

        public async Task IndexSongsBatch(IEnumerable<Song> songs)
        {
            var songList = songs.ToList();
            if (songList.Count == 0) return;

            var connection = _context.Database.GetDbConnection();
            bool wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                var currentTransaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                if (currentTransaction != null)
                {
                    command.Transaction = currentTransaction;
                }

                command.CommandText = @"
                    INSERT OR REPLACE INTO SongSearch 
                    (SongId, Title, Artist, NormalizedTitle, NormalizedArtist) 
                    VALUES (@SongId, @Title, @Artist, @NormalizedTitle, @NormalizedArtist)";

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

                await command.PrepareAsync();

                foreach (var song in songList)
                {
                    songIdParam.Value = song.SongId;
                    titleParam.Value = song.Title ?? string.Empty;
                    artistParam.Value = song.Artist ?? string.Empty;
                    normTitleParam.Value = Normalize(song.Title ?? string.Empty);
                    normArtistParam.Value = Normalize(song.Artist ?? string.Empty);

                    await command.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                if (!wasOpen)
                {
                    await connection.CloseAsync();
                }
            }
        }

        public List<Song> SearchSync(string query)
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
                    cmd.CommandText = "PRAGMA busy_timeout=10000; PRAGMA journal_mode=WAL;";
                    cmd.ExecuteNonQuery();
                }

                const string sql = @"
                    SELECT s.*, a.* 
                    FROM Songs s
                    LEFT JOIN SongAudioSettings a ON s.SongId = a.SongId
                    WHERE s.SongId IN (
                        SELECT SongId FROM SongSearch WHERE SongSearch MATCH @ftsQuery
                    )
                    LIMIT 150";

                var results = connection.Query<Song, SongAudioSettings, Song>(
                    sql,
                    (song, audioSettings) =>
                    {
                        song.AudioSettings = audioSettings;
                        return song;
                    },
                    new { ftsQuery },
                    splitOn: "SongAudioSettingsId"
                );

                return [.. results];
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dapper sync search execution failed: {ex.Message}");
                return _context.Songs
                    .FromSqlRaw("SELECT * FROM Songs WHERE SongId IN (SELECT SongId FROM SongSearch WHERE SongSearch MATCH {0})", ftsQuery)
                    .AsNoTracking()
                    .Include(s => s.AudioSettings)
                    .Take(150)
                    .ToList();
            }
        }
    }
}
