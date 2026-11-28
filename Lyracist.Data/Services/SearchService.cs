// Edited on Nov 27, 2026 @ 12:58:00 -> Add artist abbreviation expansion, fuzzy typo tolerance fallback, and subfolder crate filtering
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

        /// <summary>
        /// Built-in dictionary of common karaoke artist abbreviations, acronyms, and frequent phonetic misspellings.
        /// Keys are lowercase normalized single tokens; values are their expanded normalized forms.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> ArtistAbbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gnr"] = "guns n roses",
            ["acdc"] = "ac dc",
            ["ccr"] = "creedence clearwater revival",
            ["ratm"] = "rage against the machine",
            ["stp"] = "stone temple pilots",
            ["rhcp"] = "red hot chili peppers",
            ["mj"] = "michael jackson",
            ["bsb"] = "backstreet boys",
            ["nkotb"] = "new kids on the block",
            ["elo"] = "electric light orchestra",
            ["bto"] = "bachman turner overdrive",
            ["reo"] = "reo speedwagon",
            ["skynard"] = "lynyrd skynyrd",
            ["lynard"] = "lynyrd skynyrd",
            ["skynyrd"] = "lynyrd skynyrd",
            ["ledzep"] = "led zeppelin",
            ["zep"] = "led zeppelin",
            ["zepplin"] = "zeppelin",
            ["deflepard"] = "def leppard",
            ["lepard"] = "leppard",
            ["britany"] = "britney",
            ["brittany"] = "britney",
            ["xtina"] = "christina aguilera",
            ["bfmv"] = "bullet for my valentine",
            ["a7x"] = "avenged sevenfold",
            ["soad"] = "system of a down",
            ["mcr"] = "my chemical romance",
            ["fob"] = "fall out boy",
            ["patd"] = "panic at the disco",
            ["swv"] = "sisters with voices",
            ["dmb"] = "dave matthews band",
            ["srv"] = "stevie ray vaughan",
            ["fgl"] = "florida georgia line",
            ["bnd"] = "brooks dunn",
            ["metallca"] = "metallica",
            ["metalica"] = "metallica",
            ["journeyy"] = "journey",
            ["bohemien"] = "bohemian",
            ["rapsody"] = "rhapsody",
            ["margaritavile"] = "margaritaville"
        };

        /// <summary>
        /// Expands any known artist abbreviation or common misspelling tokens in <paramref name="query"/>.
        /// </summary>
        public string ExpandAbbreviations(string query)
        {
            string norm = Normalize(query);
            if (string.IsNullOrWhiteSpace(norm)) return string.Empty;

            string[] tokens = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool changed = false;
            var expanded = new List<string>(tokens.Length + 2);
            foreach (string token in tokens)
            {
                if (ArtistAbbreviations.TryGetValue(token, out string? replacement))
                {
                    expanded.Add(replacement);
                    changed = true;
                }
                else
                {
                    expanded.Add(token);
                }
            }

            return changed ? string.Join(" ", expanded) : norm;
        }

        /// <summary>
        /// Computes optimal string alignment (restricted Damerau-Levenshtein) edit distance between two normalized tokens,
        /// handling insertions, deletions, substitutions, and adjacent character transpositions.
        /// </summary>
        public static int ComputeEditDistance(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return 0;
            int n = a.Length;
            int m = b.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            int[,] d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                    int val = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);

                    if (i > 1 && j > 1
                        && char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 2])
                        && char.ToLowerInvariant(a[i - 2]) == char.ToLowerInvariant(b[j - 1]))
                    {
                        val = Math.Min(val, d[i - 2, j - 2] + 1);
                    }
                    d[i, j] = val;
                }
            }
            return d[n, m];
        }

        /// <summary>
        /// Returns distinct subfolder names (immediate parent directory names) across the catalog for crate filtering.
        /// </summary>
        public List<string> GetDistinctSubfolderCrates(bool? isKaraoke = null)
        {
            try
            {
                var query = _context.Songs.AsNoTracking();
                if (isKaraoke.HasValue)
                {
                    query = query.Where(s => s.IsKaraoke == isKaraoke.Value);
                }

                var paths = query.Select(s => s.FilePath).ToList();
                var crates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string? p in paths)
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    try
                    {
                        string? dir = Path.GetDirectoryName(p);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            string folderName = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                            if (!string.IsNullOrWhiteSpace(folderName) && folderName.Length > 1 && !folderName.EndsWith(':'))
                            {
                                crates.Add(folderName);
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                return crates.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch
            {
                return [];
            }
        }

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
                                // Build probe queries with the same sanitizer real searches use, so titles that are
                                // pure punctuation never produce an unmatchable probe. Only report corruption when
                                // every usable probe fails.
                                var probes = new List<string>();
                                using (var sampleCmd = connection.CreateCommand())
                                {
                                    sampleCmd.CommandText = "SELECT Title, Artist FROM Songs WHERE (Title IS NOT NULL AND Title != '') OR (Artist IS NOT NULL AND Artist != '') LIMIT 10;";
                                    using var sampleReader = sampleCmd.ExecuteReader();
                                    while (sampleReader.Read())
                                    {
                                        string t = sampleReader.IsDBNull(0) ? "" : sampleReader.GetString(0);
                                        string a = sampleReader.IsDBNull(1) ? "" : sampleReader.GetString(1);
                                        string probe = PrepareFtsQuery(!string.IsNullOrWhiteSpace(t) ? t : a);
                                        if (!string.IsNullOrEmpty(probe))
                                        {
                                            // First word only keeps the probe cheap.
                                            probes.Add(probe.Split(" AND ")[0]);
                                        }
                                    }
                                }

                                if (probes.Count > 0)
                                {
                                    bool anyMatched = false;
                                    foreach (string probe in probes)
                                    {
                                        using var probeCmd = connection.CreateCommand();
                                        probeCmd.CommandText = "SELECT SongId FROM SongSearch WHERE SongSearch MATCH @probe LIMIT 1;";
                                        var p = probeCmd.CreateParameter();
                                        p.ParameterName = "@probe";
                                        p.Value = probe;
                                        probeCmd.Parameters.Add(p);
                                        var probeResult = probeCmd.ExecuteScalar();
                                        if (probeResult != null && probeResult != DBNull.Value)
                                        {
                                            anyMatched = true;
                                            break;
                                        }
                                    }

                                    if (!anyMatched)
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
                                TryBackupDatabaseFile(connection);
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

        private static (string? WinPattern, string? UnixPattern) BuildCratePatterns(string? subfolderCrate)
        {
            if (string.IsNullOrWhiteSpace(subfolderCrate) || string.Equals(subfolderCrate, "All Crates", StringComparison.OrdinalIgnoreCase))
            {
                return (null, null);
            }

            string escaped = subfolderCrate.Trim().Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");
            return ($"%\\{escaped}\\%", $"%/{escaped}/%");
        }

        // Matches songs whose immediate parent folder is the crate (the same rule the dropdown is built
        // with): the folder appears in the path with no further separator after it.
        private const string CrateMatchSql =
            "((s.FilePath LIKE @crateWin ESCAPE '!' AND s.FilePath NOT LIKE @crateWin || '\\%' ESCAPE '!') " +
            "OR (s.FilePath LIKE @crateUnix ESCAPE '!' AND s.FilePath NOT LIKE @crateUnix || '/%' ESCAPE '!'))";


        public async Task<List<Song>> Search(string query, bool? isKaraoke = null, string? subfolderCrate = null)
        {
            var (crateWin, crateUnix) = BuildCratePatterns(subfolderCrate);

            if (string.IsNullOrWhiteSpace(query))
            {
                if (crateWin == null) return [];
                await _querySemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    var conn = await GetSharedConnectionAsync().ConfigureAwait(false);
                    const string crateOnlySql = @"
                        SELECT s.*, a.*
                        FROM Songs s
                        LEFT JOIN SongAudioSettings a ON s.SongId = a.SongId
                        WHERE (@isKaraoke IS NULL OR s.IsKaraoke = @isKaraoke)
                        " + "AND " + CrateMatchSql + @"
                        ORDER BY s.Artist, s.Title
                        LIMIT 2000";
                    var rows = await conn.QueryAsync<Song, SongAudioSettings, Song>(
                        crateOnlySql,
                        (song, audioSettings) => { song.AudioSettings = audioSettings; return song; },
                        new { isKaraoke, crateWin, crateUnix },
                        splitOn: "SongAudioSettingsId").ConfigureAwait(false);
                    return [.. rows];
                }
                finally
                {
                    _querySemaphore.Release();
                }
            }

            EnsureFtsTableExists();

            string ftsQuery = PrepareExpandedFtsQuery(query);
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
                    " + "AND (@crateWin IS NULL OR " + CrateMatchSql + ")" + @"
                    LIMIT 150";

                var results = (await connection.QueryAsync<Song, SongAudioSettings, Song>(
                    sql,
                    (song, audioSettings) =>
                    {
                        song.AudioSettings = audioSettings;
                        return song;
                    },
                    new { ftsQuery, isKaraoke, crateWin, crateUnix },
                    splitOn: "SongAudioSettingsId"
                ).ConfigureAwait(false)).ToList();

                if (results.Count > 0)
                {
                    return results;
                }

                string expandedQuery = ExpandAbbreviations(query);
                var fallbackResults = await SearchFallbackLikeAsync(connection, expandedQuery, isKaraoke, crateWin, crateUnix).ConfigureAwait(false);
                if (fallbackResults.Count == 0 && !string.Equals(expandedQuery, Normalize(query), StringComparison.OrdinalIgnoreCase))
                {
                    fallbackResults = await SearchFallbackLikeAsync(connection, query, isKaraoke, crateWin, crateUnix).ConfigureAwait(false);
                }

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
                    return fallbackResults;
                }

                // Fuzzy typo tolerance fallback (handles misspellings and transposed letters)
                return await Task.Run(() => SearchFallbackFuzzySync(connection, expandedQuery, isKaraoke, crateWin, crateUnix)).ConfigureAwait(false);
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
                    var likeRes = await SearchFallbackLikeAsync(fallbackConn, ExpandAbbreviations(query), isKaraoke, crateWin, crateUnix).ConfigureAwait(false);
                    if (likeRes.Count > 0) return likeRes;
                    return SearchFallbackFuzzySync(fallbackConn, ExpandAbbreviations(query), isKaraoke, crateWin, crateUnix);
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

        private (string Sql, DynamicParameters Params) BuildLikeFallbackQuery(string query, bool? isKaraoke, string? crateWin = null, string? crateUnix = null)
        {
            string normalized = Normalize(query);
            var words = string.IsNullOrWhiteSpace(normalized)
                ? query.Split([' '], StringSplitOptions.RemoveEmptyEntries)
                : normalized.Split([' '], StringSplitOptions.RemoveEmptyEntries);

            var parameters = new DynamicParameters();
            parameters.Add("isKaraoke", isKaraoke);
            parameters.Add("crateWin", crateWin);
            parameters.Add("crateUnix", crateUnix);

            var whereClauses = new List<string>();
            for (int i = 0; i < words.Length; i++)
            {
                string paramName = $"w{i}";
                string escaped = words[i].Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");
                parameters.Add(paramName, $"%{escaped}%");
                whereClauses.Add($"(s.Title LIKE @{paramName} ESCAPE '!' OR s.Artist LIKE @{paramName} ESCAPE '!' OR s.FilePath LIKE @{paramName} ESCAPE '!')");
            }

            // Rank rows whose title or artist (not just the file path) contains every word first,
            // so the 150-row cap keeps the relevant matches.
            string titleOrArtistRank = whereClauses.Count > 0
                ? string.Join(" AND ", Enumerable.Range(0, whereClauses.Count).Select(i => $"(s.Title LIKE @w{i} ESCAPE '!' OR s.Artist LIKE @w{i} ESCAPE '!')"))
                : "1=1";
            string wordFilter = whereClauses.Count > 0 ? string.Join(" AND ", whereClauses) : "1=1";
            string sql = $@"
                SELECT s.*, a.*
                FROM Songs s
                LEFT JOIN SongAudioSettings a ON s.SongId = a.SongId
                WHERE {wordFilter}
                AND (@isKaraoke IS NULL OR s.IsKaraoke = @isKaraoke)
                AND (@crateWin IS NULL OR {CrateMatchSql})
                ORDER BY (CASE WHEN {titleOrArtistRank} THEN 0 ELSE 1 END), s.Title
                LIMIT 150";

            return (sql, parameters);
        }

        private static void TryBackupDatabaseFile(System.Data.Common.DbConnection connection)
        {
            try
            {
                string dbPath = connection.DataSource;
                if (!string.IsNullOrEmpty(dbPath) && System.IO.File.Exists(dbPath))
                {
                    using (var cp = connection.CreateCommand())
                    {
                        cp.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                        cp.ExecuteNonQuery();
                    }
                    System.IO.File.Copy(dbPath, $"{dbPath}.prerepair-{DateTime.Now:yyyyMMddHHmmss}.bak", overwrite: false);
                }
            }
            catch
            {
                // Best effort; the force-drop below is still the only way to recover a corrupt FTS table.
            }
        }

        private async Task<List<Song>> SearchFallbackLikeAsync(SqliteConnection connection, string query, bool? isKaraoke, string? crateWin = null, string? crateUnix = null)
        {
            var (sql, parameters) = BuildLikeFallbackQuery(query, isKaraoke, crateWin, crateUnix);
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

        private List<Song> SearchFallbackLikeSync(SqliteConnection connection, string query, bool? isKaraoke, string? crateWin = null, string? crateUnix = null)
        {
            var (sql, parameters) = BuildLikeFallbackQuery(query, isKaraoke, crateWin, crateUnix);
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

        private static int FuzzyTokenDistance(string queryToken, string targetToken, int maxAllowed)
        {
            if (targetToken.StartsWith(queryToken, StringComparison.OrdinalIgnoreCase)) return 0;

            // Compare against either the full target word or its prefix of the query's length.
            string compare = targetToken.Length > queryToken.Length + maxAllowed
                ? targetToken[..queryToken.Length]
                : targetToken;

            return Math.Abs(compare.Length - queryToken.Length) > maxAllowed
                ? int.MaxValue
                : ComputeEditDistance(queryToken, compare);
        }

        private sealed class FuzzyCandidateRow
        {
            public int SongId { get; set; }
            public string NormalizedTitle { get; set; } = string.Empty;
            public string NormalizedArtist { get; set; } = string.Empty;
        }

        /// <summary>
        /// Fuzzy typo-tolerant fallback search using Damerau-Levenshtein token distance when exact/prefix/LIKE searches return 0 results.
        /// Pre-filters candidate rows via 2-character prefix/suffix SQL probes so large libraries remain fast.
        /// </summary>
        private List<Song> SearchFallbackFuzzySync(SqliteConnection connection, string query, bool? isKaraoke, string? crateWin = null, string? crateUnix = null)
        {
            string normalized = Normalize(query);
            string[] queryTokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 3)
                .ToArray();
            if (queryTokens.Length == 0) return [];

            var parameters = new DynamicParameters();
            parameters.Add("isKaraoke", isKaraoke);
            parameters.Add("crateWin", crateWin);
            parameters.Add("crateUnix", crateUnix);

            var probeClauses = new List<string>();
            for (int i = 0; i < queryTokens.Length; i++)
            {
                string tok = queryTokens[i];
                string prefix2 = tok[..2];
                string suffix2 = tok[^2..];
                parameters.Add($"p{i}", $"%{prefix2}%");
                parameters.Add($"s{i}", $"%{suffix2}%");
                probeClauses.Add($"(f.NormalizedTitle LIKE @p{i} OR f.NormalizedArtist LIKE @p{i} OR f.NormalizedTitle LIKE @s{i} OR f.NormalizedArtist LIKE @s{i})");
            }

            string probeFilter = string.Join(" AND ", probeClauses);
            string candidateSql = $@"
                SELECT f.SongId, f.NormalizedTitle, f.NormalizedArtist
                FROM SongSearch f
                INNER JOIN Songs s ON s.SongId = f.rowid
                WHERE ({probeFilter})
                AND (@isKaraoke IS NULL OR s.IsKaraoke = @isKaraoke)
                AND (@crateWin IS NULL OR {CrateMatchSql})";

            // Titles and artists repeat the same words heavily, so each distinct word is only scored once.
            var distanceCache = queryTokens.Select(_ => new Dictionary<string, int>(StringComparer.Ordinal)).ToArray();

            var matchedIds = new List<(int SongId, int TotalDistance)>();
            try
            {
                // Unbuffered: rows are scored as they stream rather than materialized first.
                foreach (var c in connection.Query<FuzzyCandidateRow>(candidateSql, parameters, buffered: false))
                {
                    string[] targetTokens = string.Concat(c.NormalizedTitle, " ", c.NormalizedArtist)
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (targetTokens.Length == 0) continue;

                    int totalDist = 0;
                    bool allTokensMatched = true;

                    for (int q = 0; q < queryTokens.Length; q++)
                    {
                        string qTok = queryTokens[q];
                        int maxAllowed = qTok.Length >= 7 ? 2 : 1;
                        int bestDist = int.MaxValue;

                        foreach (string tTok in targetTokens)
                        {
                            if (!distanceCache[q].TryGetValue(tTok, out int dist))
                            {
                                dist = FuzzyTokenDistance(qTok, tTok, maxAllowed);
                                distanceCache[q][tTok] = dist;
                            }

                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                if (bestDist == 0) break;
                            }
                        }

                        if (bestDist > maxAllowed)
                        {
                            allTokensMatched = false;
                            break;
                        }
                        totalDist += bestDist;
                    }

                    if (allTokensMatched)
                    {
                        matchedIds.Add((c.SongId, totalDist));
                    }
                }
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Fuzzy search failed", ex);
                return [];
            }

            if (matchedIds.Count == 0) return [];

            var topIds = matchedIds
                .OrderBy(m => m.TotalDistance)
                .Take(150)
                .Select(m => m.SongId)
                .ToList();

            const string fetchSql = @"
                SELECT s.*, a.*
                FROM Songs s
                LEFT JOIN SongAudioSettings a ON s.SongId = a.SongId
                WHERE s.SongId IN @topIds";

            var fetched = connection.Query<Song, SongAudioSettings, Song>(
                fetchSql,
                (song, audioSettings) =>
                {
                    song.AudioSettings = audioSettings;
                    return song;
                },
                new { topIds },
                splitOn: "SongAudioSettingsId").ToDictionary(s => s.SongId);

            var ordered = new List<Song>(topIds.Count);
            foreach (int id in topIds)
            {
                if (fetched.TryGetValue(id, out var song))
                {
                    ordered.Add(song);
                }
            }
            return ordered;
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

        private string PrepareExpandedFtsQuery(string query)
        {
            string baseFts = PrepareFtsQuery(query);
            if (string.IsNullOrEmpty(baseFts)) return string.Empty;

            string expanded = ExpandAbbreviations(query);
            string normOrig = Normalize(query);
            if (!string.IsNullOrEmpty(expanded) && !string.Equals(expanded, normOrig, StringComparison.OrdinalIgnoreCase))
            {
                string expandedFts = PrepareFtsQuery(expanded);
                if (!string.IsNullOrEmpty(expandedFts) && !string.Equals(expandedFts, baseFts, StringComparison.Ordinal))
                {
                    return $"({expandedFts}) OR ({baseFts})";
                }
            }

            return baseFts;
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

        public List<Song> SearchSync(string query, bool? isKaraoke = null, string? subfolderCrate = null)
        {
            var (crateWin, crateUnix) = BuildCratePatterns(subfolderCrate);

            if (string.IsNullOrWhiteSpace(query))
            {
                if (crateWin == null) return [];
                using var crateConn = new SqliteConnection(LyracistDbContext.GetConnectionString());
                crateConn.Open();
                const string crateOnlySql = @"
                    SELECT s.*, a.*
                    FROM Songs s
                    LEFT JOIN SongAudioSettings a ON s.SongId = a.SongId
                    WHERE (@isKaraoke IS NULL OR s.IsKaraoke = @isKaraoke)
                    " + "AND " + CrateMatchSql + @"
                    ORDER BY s.Artist, s.Title
                    LIMIT 2000";
                return crateConn.Query<Song, SongAudioSettings, Song>(
                    crateOnlySql,
                    (song, audioSettings) => { song.AudioSettings = audioSettings; return song; },
                    new { isKaraoke, crateWin, crateUnix },
                    splitOn: "SongAudioSettingsId").ToList();
            }

            EnsureFtsTableExists();

            string ftsQuery = PrepareExpandedFtsQuery(query);
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
                    " + "AND (@crateWin IS NULL OR " + CrateMatchSql + ")" + @"
                    LIMIT 150";

                var results = connection.Query<Song, SongAudioSettings, Song>(
                    sql,
                    (song, audioSettings) =>
                    {
                        song.AudioSettings = audioSettings;
                        return song;
                    },
                    new { ftsQuery, isKaraoke, crateWin, crateUnix },
                    splitOn: "SongAudioSettingsId"
                ).ToList();

                if (results.Count > 0)
                {
                    return results;
                }

                string expandedQuery = ExpandAbbreviations(query);
                var fallbackResults = SearchFallbackLikeSync(connection, expandedQuery, isKaraoke, crateWin, crateUnix);
                if (fallbackResults.Count == 0 && !string.Equals(expandedQuery, Normalize(query), StringComparison.OrdinalIgnoreCase))
                {
                    fallbackResults = SearchFallbackLikeSync(connection, query, isKaraoke, crateWin, crateUnix);
                }

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
                    return fallbackResults;
                }

                return SearchFallbackFuzzySync(connection, expandedQuery, isKaraoke, crateWin, crateUnix);
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
                    var likeRes = SearchFallbackLikeSync(fallbackConn, ExpandAbbreviations(query), isKaraoke, crateWin, crateUnix);
                    if (likeRes.Count > 0) return likeRes;
                    return SearchFallbackFuzzySync(fallbackConn, ExpandAbbreviations(query), isKaraoke, crateWin, crateUnix);
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
