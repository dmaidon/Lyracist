using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lyracist.Data.Models;
using Microsoft.EntityFrameworkCore;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Lyracist.Data.Services
{
    public class SearchService(LyracistDbContext context)
    {
        private readonly LyracistDbContext _context = context;

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

        public async Task<List<Song>> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return [];
            }

            string ftsQuery = PrepareFtsQuery(query);
            if (string.IsNullOrWhiteSpace(ftsQuery))
            {
                return [];
            }

            try
            {
                using var connection = new SqliteConnection(LyracistDbContext.GetConnectionString());
                await connection.OpenAsync();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA busy_timeout=10000; PRAGMA journal_mode=WAL;";
                    await cmd.ExecuteNonQueryAsync();
                }

                // Query matching songs and join with their respective audio settings, clamping to 150 items max
                string sql = @"
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
    }
}
