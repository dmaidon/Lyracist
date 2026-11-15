// Edited on Nov 14, 2026 @ 15:33:00 -> Store SingerHistory and Singers in dedicated Data/singers.db database
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Lyracist.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Lyracist.Services.Database
{
    public class SingerHistoryEntry
    {
        public int SingerHistoryId { get; set; }
        public string SingerName { get; set; } = string.Empty;
        public string SongTitle { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Source { get; set; } = "Local";
        public string Key { get; set; } = "0";
        public double Tempo { get; set; } = 1.0;
        public string Venue { get; set; } = string.Empty;
        public int TimesPerformed { get; set; } = 1;
        public DateTime Timestamp { get; set; }
    }

    public static class SingerHistoryService
    {
        private static string GetConnectionString()
        {
            return Lyracist.Data.LyracistDbContext.GetSingersConnectionString();
        }

        public static void EnsureTableCreated()
        {
            try
            {
                Lyracist.Data.LyracistDbContext.EnsureAndSyncDedicatedSingersDatabase();

                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Singers (
                        SingerId INTEGER NOT NULL CONSTRAINT PK_Singers PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL DEFAULT '',
                        JoinDate TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                        LastSang TEXT NULL,
                        TotalSongsSung INTEGER NOT NULL DEFAULT 0,
                        Notes TEXT NOT NULL DEFAULT '',
                        Score INTEGER NOT NULL DEFAULT 0,
                        RatingPoints INTEGER NOT NULL DEFAULT 0,
                        RatingCount INTEGER NOT NULL DEFAULT 0,
                        AverageRating REAL NOT NULL DEFAULT 0.0,
                        Email TEXT NOT NULL DEFAULT '',
                        PinCode TEXT NOT NULL DEFAULT '',
                        AvatarType TEXT NOT NULL DEFAULT 'None',
                        AvatarSource TEXT NOT NULL DEFAULT '',
                        VocalRange TEXT NOT NULL DEFAULT '',
                        CustomTitle TEXT NOT NULL DEFAULT '',
                        AllowRecording INTEGER NOT NULL DEFAULT 0,
                        Venues TEXT NOT NULL DEFAULT '',
                        IsDistinctName INTEGER NOT NULL DEFAULT 0
                    );
                    CREATE TABLE IF NOT EXISTS SingerHistory (
                        SingerHistoryId INTEGER PRIMARY KEY AUTOINCREMENT,
                        SingerName TEXT NOT NULL,
                        SongTitle TEXT NOT NULL,
                        Artist TEXT NOT NULL,
                        Link TEXT,
                        Source TEXT NOT NULL,
                        Timestamp TEXT NOT NULL,
                        Key TEXT DEFAULT '0',
                        Tempo REAL DEFAULT 1.0,
                        Venue TEXT DEFAULT '',
                        TimesPerformed INTEGER NOT NULL DEFAULT 1
                    );
                    CREATE INDEX IF NOT EXISTS IX_SingerHistory_SingerName ON SingerHistory (SingerName);
                ";
                command.ExecuteNonQuery();

                // Safe migrations for pre-existing tables lacking Key, Tempo, Venue, or TimesPerformed columns
                TryAddColumn(command, "SingerHistory", "Key", "TEXT DEFAULT '0'");
                TryAddColumn(command, "SingerHistory", "Tempo", "REAL DEFAULT 1.0");
                TryAddColumn(command, "SingerHistory", "Venue", "TEXT DEFAULT ''");
                TryAddColumn(command, "SingerHistory", "TimesPerformed", "INTEGER NOT NULL DEFAULT 1");
                TryAddColumn(command, "Singers", "Venues", "TEXT NOT NULL DEFAULT ''");
                TryAddColumn(command, "Singers", "IsDistinctName", "INTEGER NOT NULL DEFAULT 0");
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Error creating SingerHistory table", ex);
            }
        }

        private static void TryAddColumn(SqliteCommand command, string table, string column, string definition)
        {
            try
            {
                command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
                command.ExecuteNonQuery();
            }
            catch
            {
                // Column already exists or table not yet created
            }
        }

        private static string NormalizeVenueName(string? venue)
        {
            if (string.IsNullOrWhiteSpace(venue)) return string.Empty;
            string trimmed = venue.Trim();
            if (string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Saved Rotation", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Karaoke Night", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Main Venue", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            return trimmed;
        }

        /// <summary>
        /// Checks whether a singer name qualifies to be stored in the permanent database:
        /// either a multi-word name (First &amp; Last name or distinct identifier like "Bar Mike" / "Bar Mike Gilmore")
        /// or an existing profile explicitly flagged with <c>IsDistinctName = 1</c>.
        /// </summary>
        public static bool IsEligibleDatabaseSinger(string? singerName, SqliteConnection? openConnection = null)
        {
            if (string.IsNullOrWhiteSpace(singerName)) return false;
            string cleaned = NameFormatting.CleanSingerName(singerName);
            if (string.IsNullOrWhiteSpace(cleaned)) return false;

            if (NameFormatting.IsQualifyingDatabaseSingerName(cleaned, isDistinctSingleName: false))
                return true;

            try
            {
                bool disposeConn = false;
                SqliteConnection conn;
                if (openConnection != null)
                {
                    conn = openConnection;
                }
                else
                {
                    conn = new SqliteConnection(GetConnectionString());
                    conn.Open();
                    disposeConn = true;
                }

                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT IsDistinctName FROM Singers WHERE Name = $name COLLATE NOCASE LIMIT 1;";
                    cmd.Parameters.AddWithValue("$name", cleaned);
                    object? val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value && Convert.ToInt32(val) != 0)
                    {
                        return NameFormatting.IsQualifyingDatabaseSingerName(cleaned, isDistinctSingleName: true);
                    }

                    // Also check main lyracist.db in case a distinct single-name singer was just inserted via LyracistDbContext
                    string mainDbPath = Lyracist.Data.LyracistDbContext.GetDatabaseFilePath();
                    if (File.Exists(mainDbPath))
                    {
                        using var mainConn = new SqliteConnection(Lyracist.Data.LyracistDbContext.GetConnectionString());
                        mainConn.Open();
                        using var mainCmd = mainConn.CreateCommand();
                        mainCmd.CommandText = "SELECT IsDistinctName FROM Singers WHERE Name = $name COLLATE NOCASE LIMIT 1;";
                        mainCmd.Parameters.AddWithValue("$name", cleaned);
                        object? mainVal = mainCmd.ExecuteScalar();
                        if (mainVal != null && mainVal != DBNull.Value && Convert.ToInt32(mainVal) != 0)
                        {
                            return NameFormatting.IsQualifyingDatabaseSingerName(cleaned, isDistinctSingleName: true);
                        }
                    }
                }
                finally
                {
                    if (disposeConn)
                    {
                        conn.Dispose();
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        public static void SaveHistory(
            string singerName,
            string songTitle,
            string artist,
            string source,
            string link,
            string key = "0",
            double tempo = 1.0,
            string venue = "",
            DateTime? timestampUtc = null,
            bool incrementTimesPerformed = true)
        {
            if (string.IsNullOrWhiteSpace(singerName) || string.IsNullOrWhiteSpace(songTitle)) return;

            singerName = NameFormatting.CleanSingerName(singerName);
            if (string.IsNullOrWhiteSpace(singerName)) return;

            songTitle = NameFormatting.ProperCase(songTitle.Trim());
            artist = NameFormatting.ProperCase((artist ?? string.Empty).Trim());
            string cleanVenue = NormalizeVenueName(venue);
            DateTime ts = timestampUtc ?? DateTime.UtcNow;

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                // Only record in the database if the singer has a 1st & last name or a distinct name
                if (!IsEligibleDatabaseSinger(singerName, connection))
                {
                    return;
                }

                // Check if this singer already sang this song (matching exact artist or consolidating a blank-artist row)
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = @"
                    SELECT SingerHistoryId, Artist, Venue, TimesPerformed
                    FROM SingerHistory
                    WHERE SingerName = $singerName COLLATE NOCASE
                      AND SongTitle = $songTitle COLLATE NOCASE
                      AND (Artist = $artist COLLATE NOCASE OR Artist = '' OR $artist = '')
                    ORDER BY CASE WHEN Artist = $artist COLLATE NOCASE THEN 0 WHEN Artist != '' THEN 1 ELSE 2 END,
                             SingerHistoryId ASC
                    LIMIT 1;
                ";
                checkCmd.Parameters.AddWithValue("$singerName", singerName);
                checkCmd.Parameters.AddWithValue("$songTitle", songTitle);
                checkCmd.Parameters.AddWithValue("$artist", artist);

                int? existingId = null;
                string existingArtist = string.Empty;
                string existingVenue = string.Empty;
                int existingTimes = 1;
                using (var reader = checkCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        existingId = reader.GetInt32(0);
                        existingArtist = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        existingVenue = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                        existingTimes = reader.IsDBNull(3) ? 1 : Math.Max(1, reader.GetInt32(3));
                    }
                }

                string resolvedArtist = !string.IsNullOrWhiteSpace(artist) ? artist : existingArtist;
                string mergedSongVenues = DatabaseService.MergeVenueLists(existingVenue, cleanVenue);

                if (existingId.HasValue)
                {
                    int newTimes = incrementTimesPerformed ? existingTimes + 1 : existingTimes;
                    using var updateCmd = connection.CreateCommand();
                    updateCmd.CommandText = @"
                        UPDATE SingerHistory 
                        SET Artist = $artist,
                            Timestamp = $timestamp,
                            Link = $link,
                            Source = $source,
                            Key = $key,
                            Tempo = $tempo,
                            Venue = $venue,
                            TimesPerformed = $timesPerformed
                        WHERE SingerHistoryId = $id;
                    ";
                    updateCmd.Parameters.AddWithValue("$artist", resolvedArtist);
                    updateCmd.Parameters.AddWithValue("$timestamp", ts.ToString("o"));
                    updateCmd.Parameters.AddWithValue("$link", link ?? string.Empty);
                    updateCmd.Parameters.AddWithValue("$source", string.IsNullOrWhiteSpace(source) ? "Local" : source);
                    updateCmd.Parameters.AddWithValue("$key", string.IsNullOrWhiteSpace(key) ? "0" : key.Trim());
                    updateCmd.Parameters.AddWithValue("$tempo", tempo <= 0 ? 1.0 : tempo);
                    updateCmd.Parameters.AddWithValue("$venue", mergedSongVenues);
                    updateCmd.Parameters.AddWithValue("$timesPerformed", newTimes);
                    updateCmd.Parameters.AddWithValue("$id", existingId.Value);
                    updateCmd.ExecuteNonQuery();
                }
                else
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = @"
                        INSERT INTO SingerHistory (SingerName, SongTitle, Artist, Link, Source, Timestamp, Key, Tempo, Venue, TimesPerformed)
                        VALUES ($singerName, $songTitle, $artist, $link, $source, $timestamp, $key, $tempo, $venue, 1);
                    ";
                    insertCmd.Parameters.AddWithValue("$singerName", singerName);
                    insertCmd.Parameters.AddWithValue("$songTitle", songTitle);
                    insertCmd.Parameters.AddWithValue("$artist", artist);
                    insertCmd.Parameters.AddWithValue("$link", link ?? string.Empty);
                    insertCmd.Parameters.AddWithValue("$source", string.IsNullOrWhiteSpace(source) ? "Local" : source);
                    insertCmd.Parameters.AddWithValue("$timestamp", ts.ToString("o"));
                    insertCmd.Parameters.AddWithValue("$key", string.IsNullOrWhiteSpace(key) ? "0" : key.Trim());
                    insertCmd.Parameters.AddWithValue("$tempo", tempo <= 0 ? 1.0 : tempo);
                    insertCmd.Parameters.AddWithValue("$venue", mergedSongVenues);
                    insertCmd.ExecuteNonQuery();
                }

                // Ensure the singer exists in Singers table with updated Venues
                UpsertSingerVenueOnConnection(connection, singerName, cleanVenue, incrementSongsSung: false, lastSangUtc: ts);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Error saving SingerHistory", ex);
            }
        }

        /// <summary>
        /// Records a completed live performance for a qualifying singer (First &amp; Last name or Distinct name),
        /// saving the song and venue into <c>SingerHistory</c> and updating <c>Singers</c> (<c>TotalSongsSung</c>, <c>LastSang</c>, and <c>Venues</c>).
        /// </summary>
        public static void RecordCompletedPerformance(
            string? singerName,
            string? songTitle,
            string? artist,
            string? venue,
            string key = "0",
            double tempo = 1.0,
            string source = "Local",
            string link = "",
            DateTime? timestampUtc = null,
            bool incrementTotalSongs = true)
        {
            if (string.IsNullOrWhiteSpace(singerName)) return;

            string cleanName = NameFormatting.CleanSingerName(singerName);
            if (string.IsNullOrWhiteSpace(cleanName)) return;

            string cleanTitle = NameFormatting.ProperCase((songTitle ?? string.Empty).Trim());
            string cleanArtist = NameFormatting.ProperCase((artist ?? string.Empty).Trim());
            string cleanVenue = NormalizeVenueName(venue);
            DateTime ts = timestampUtc ?? DateTime.UtcNow;

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                if (!IsEligibleDatabaseSinger(cleanName, connection))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(cleanTitle))
                {
                    SaveHistory(cleanName, cleanTitle, cleanArtist, source, link, key, tempo, cleanVenue, ts);
                }

                UpsertSingerVenueOnConnection(connection, cleanName, cleanVenue, incrementSongsSung: incrementTotalSongs, lastSangUtc: ts);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Error recording completed performance", ex);
            }
        }

        /// <summary>
        /// Ensures a qualifying singer exists in the <c>Singers</c> table and merges any new venue into their <c>Venues</c> list.
        /// Non-qualifying generic single-word names are ignored.
        /// </summary>
        public static void EnsureSingerAndVenue(string? singerName, string? venue, bool isDistinctSingleName = false)
        {
            if (string.IsNullOrWhiteSpace(singerName)) return;
            string cleanName = NameFormatting.CleanSingerName(singerName);
            if (string.IsNullOrWhiteSpace(cleanName)) return;

            string cleanVenue = NormalizeVenueName(venue);

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                if (!isDistinctSingleName && !IsEligibleDatabaseSinger(cleanName, connection))
                {
                    return;
                }

                UpsertSingerVenueOnConnection(connection, cleanName, cleanVenue, incrementSongsSung: false, lastSangUtc: null, markDistinct: isDistinctSingleName);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Error in EnsureSingerAndVenue", ex);
            }
        }

        private static void UpsertSingerVenueOnConnection(
            SqliteConnection connection,
            string cleanName,
            string cleanVenue,
            bool incrementSongsSung,
            DateTime? lastSangUtc,
            bool markDistinct = false)
        {
            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT SingerId, Venues, TotalSongsSung, IsDistinctName FROM Singers WHERE Name = $name COLLATE NOCASE LIMIT 1;";
            selectCmd.Parameters.AddWithValue("$name", cleanName);

            int? singerId = null;
            string existingVenues = string.Empty;
            int totalSongs = 0;
            bool existingDistinct = false;

            using (var reader = selectCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    singerId = reader.GetInt32(0);
                    existingVenues = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    totalSongs = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    existingDistinct = !reader.IsDBNull(3) && reader.GetInt32(3) != 0;
                }
            }

            // Also gather any venues from SingerHistory for this singer so the singer's Venues column stays complete
            string historyVenues = GetConsolidatedHistoryVenuesOnConnection(connection, cleanName);
            string mergedVenues = DatabaseService.MergeVenueLists(DatabaseService.MergeVenueLists(existingVenues, historyVenues), cleanVenue);

            if (singerId.HasValue)
            {
                int newTotal = incrementSongsSung ? totalSongs + 1 : totalSongs;
                bool newDistinct = existingDistinct || markDistinct;

                using var updateCmd = connection.CreateCommand();
                if (lastSangUtc.HasValue)
                {
                    updateCmd.CommandText = @"
                        UPDATE Singers
                        SET Venues = $venues,
                            TotalSongsSung = $totalSongs,
                            LastSang = $lastSang,
                            IsDistinctName = $isDistinct
                        WHERE SingerId = $id;
                    ";
                    updateCmd.Parameters.AddWithValue("$lastSang", lastSangUtc.Value.ToString("o"));
                }
                else
                {
                    updateCmd.CommandText = @"
                        UPDATE Singers
                        SET Venues = $venues,
                            TotalSongsSung = $totalSongs,
                            IsDistinctName = $isDistinct
                        WHERE SingerId = $id;
                    ";
                }
                updateCmd.Parameters.AddWithValue("$venues", mergedVenues);
                updateCmd.Parameters.AddWithValue("$totalSongs", newTotal);
                updateCmd.Parameters.AddWithValue("$isDistinct", newDistinct ? 1 : 0);
                updateCmd.Parameters.AddWithValue("$id", singerId.Value);
                updateCmd.ExecuteNonQuery();
            }
            else
            {
                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO Singers (
                        Name, JoinDate, LastSang, TotalSongsSung, Notes, Score,
                        RatingPoints, RatingCount, AverageRating, Email, PinCode,
                        AvatarType, AvatarSource, VocalRange, CustomTitle, AllowRecording,
                        Venues, IsDistinctName
                    )
                    VALUES (
                        $name, $joinDate, $lastSang, $totalSongs, '', 0,
                        0, 0, 0.0, '', '',
                        'None', '', 'Any / Unspecified', '', 0,
                        $venues, $isDistinct
                    );
                ";
                insertCmd.Parameters.AddWithValue("$name", cleanName);
                insertCmd.Parameters.AddWithValue("$joinDate", DateTime.UtcNow.ToString("o"));
                insertCmd.Parameters.AddWithValue("$lastSang", lastSangUtc.HasValue ? lastSangUtc.Value.ToString("o") : (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("$totalSongs", incrementSongsSung ? 1 : 0);
                insertCmd.Parameters.AddWithValue("$venues", mergedVenues);
                insertCmd.Parameters.AddWithValue("$isDistinct", markDistinct ? 1 : 0);
                insertCmd.ExecuteNonQuery();
            }

            Lyracist.Data.LyracistDbContext.EnsureAndSyncDedicatedSingersDatabase();
        }

        private static string GetConsolidatedHistoryVenuesOnConnection(SqliteConnection connection, string singerName)
        {
            var venues = new List<string>();
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT Venue FROM SingerHistory WHERE SingerName = $name COLLATE NOCASE AND Venue IS NOT NULL AND Venue != '';";
                cmd.Parameters.AddWithValue("$name", singerName);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0))
                    {
                        venues.Add(reader.GetString(0));
                    }
                }
            }
            catch
            {
            }
            return DatabaseService.MergeVenueLists(string.Join(", ", venues), null);
        }

        public static List<SingerHistoryEntry> GetHistory(string singerName)
        {
            var list = new List<SingerHistoryEntry>();
            if (string.IsNullOrWhiteSpace(singerName)) return list;

            singerName = NameFormatting.ProperCase(singerName.Trim());

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT SingerHistoryId, SingerName, SongTitle, Artist, Link, Source, Timestamp, Key, Tempo, Venue, TimesPerformed
                    FROM SingerHistory
                    WHERE SingerName = $singerName COLLATE NOCASE
                    ORDER BY Timestamp DESC
                ";
                command.Parameters.AddWithValue("$singerName", singerName);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new SingerHistoryEntry
                    {
                        SingerHistoryId = reader.GetInt32(0),
                        SingerName = reader.GetString(1),
                        SongTitle = reader.GetString(2),
                        Artist = reader.GetString(3),
                        Link = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        Source = reader.GetString(5),
                        Timestamp = DateTime.TryParse(reader.GetString(6), out var dt) ? dt : DateTime.UtcNow,
                        Key = reader.IsDBNull(7) ? "0" : reader.GetString(7),
                        Tempo = reader.IsDBNull(8) ? 1.0 : reader.GetDouble(8),
                        Venue = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                        TimesPerformed = reader.IsDBNull(10) ? 1 : Math.Max(1, reader.GetInt32(10))
                    });
                }
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Error loading SingerHistory", ex);
            }

            return list;
        }

        public static (string Key, double Tempo)? GetSongHistory(string singerName, string songTitle, string artist)
        {
            if (string.IsNullOrWhiteSpace(singerName) || string.IsNullOrWhiteSpace(songTitle)) return null;

            singerName = NameFormatting.ProperCase(singerName);
            songTitle = NameFormatting.ProperCase(songTitle);

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Key, Tempo
                    FROM SingerHistory
                    WHERE SingerName = $singerName COLLATE NOCASE AND SongTitle = $songTitle COLLATE NOCASE
                    ORDER BY Timestamp DESC
                    LIMIT 1
                ";
                command.Parameters.AddWithValue("$singerName", singerName.Trim());
                command.Parameters.AddWithValue("$songTitle", songTitle.Trim());

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    string key = reader.IsDBNull(0) ? "0" : reader.GetString(0);
                    double tempo = reader.IsDBNull(1) ? 1.0 : reader.GetDouble(1);
                    return (key, tempo <= 0 ? 1.0 : tempo);
                }
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Error querying GetSongHistory", ex);
            }

            return null;
        }

        public static void MergeHistory(string sourceSingerName, string targetSingerName)
        {
            if (string.IsNullOrWhiteSpace(sourceSingerName) || string.IsNullOrWhiteSpace(targetSingerName)) return;
            if (string.Equals(sourceSingerName.Trim(), targetSingerName.Trim(), StringComparison.OrdinalIgnoreCase)) return;

            sourceSingerName = NameFormatting.ProperCase(sourceSingerName.Trim());
            targetSingerName = NameFormatting.ProperCase(targetSingerName.Trim());

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                // 1. Merge venues and TimesPerformed on overlapping songs before deleting duplicates
                var sourceRows = GetHistory(sourceSingerName);
                var targetRows = GetHistory(targetSingerName);
                foreach (var src in sourceRows)
                {
                    var match = targetRows.FirstOrDefault(t =>
                        string.Equals(t.SongTitle, src.SongTitle, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(t.Artist, src.Artist, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        string mergedVenue = DatabaseService.MergeVenueLists(match.Venue, src.Venue);
                        int combinedTimes = Math.Max(1, match.TimesPerformed) + Math.Max(1, src.TimesPerformed);
                        using var mergeVenueCmd = connection.CreateCommand();
                        mergeVenueCmd.CommandText = "UPDATE SingerHistory SET Venue = $venue, TimesPerformed = $timesPerformed WHERE SingerHistoryId = $id;";
                        mergeVenueCmd.Parameters.AddWithValue("$venue", mergedVenue);
                        mergeVenueCmd.Parameters.AddWithValue("$timesPerformed", combinedTimes);
                        mergeVenueCmd.Parameters.AddWithValue("$id", match.SingerHistoryId);
                        mergeVenueCmd.ExecuteNonQuery();
                    }
                }

                // 2. Reassign non-overlapping song history from source singer to target singer
                using var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = @"
                    UPDATE SingerHistory
                    SET SingerName = $targetSingerName
                    WHERE SingerName = $sourceSingerName COLLATE NOCASE
                      AND SongTitle NOT IN (
                          SELECT SongTitle FROM SingerHistory WHERE SingerName = $targetSingerName COLLATE NOCASE
                      );
                ";
                updateCmd.Parameters.AddWithValue("$targetSingerName", targetSingerName);
                updateCmd.Parameters.AddWithValue("$sourceSingerName", sourceSingerName);
                updateCmd.ExecuteNonQuery();

                // 3. Remove remaining overlapping entries for the source singer
                using var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM SingerHistory WHERE SingerName = $sourceSingerName COLLATE NOCASE;";
                deleteCmd.Parameters.AddWithValue("$sourceSingerName", sourceSingerName);
                deleteCmd.ExecuteNonQuery();

                // 4. Refresh target singer's consolidated Venues
                UpsertSingerVenueOnConnection(connection, targetSingerName, string.Empty, incrementSongsSung: false, lastSangUtc: null);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"Error merging SingerHistory from '{sourceSingerName}' to '{targetSingerName}'", ex);
            }
        }

        public static void DeleteHistoryEntry(int singerHistoryId, bool removeFromReports = false, string? reportsDirectory = null)
        {
            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                string singerName = string.Empty;
                string songTitle = string.Empty;
                string artist = string.Empty;

                using (var selectCmd = connection.CreateCommand())
                {
                    selectCmd.CommandText = "SELECT SingerName, SongTitle, Artist FROM SingerHistory WHERE SingerHistoryId = $id LIMIT 1;";
                    selectCmd.Parameters.AddWithValue("$id", singerHistoryId);
                    using var reader = selectCmd.ExecuteReader();
                    if (reader.Read())
                    {
                        singerName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                        songTitle = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        artist = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    }
                }

                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM SingerHistory WHERE SingerHistoryId = $id;";
                command.Parameters.AddWithValue("$id", singerHistoryId);
                command.ExecuteNonQuery();

                if (removeFromReports && !string.IsNullOrWhiteSpace(singerName) && !string.IsNullOrWhiteSpace(songTitle))
                {
                    SavedRotationReportReader.RemoveSingerSongFromReports(singerName, songTitle, artist, reportsDirectory);
                }

                if (!string.IsNullOrWhiteSpace(singerName))
                {
                    RefreshSingerAggregatesOnConnection(connection, singerName);
                }
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"Error deleting SingerHistory entry #{singerHistoryId}", ex);
            }
        }

        /// <summary>
        /// Updates an existing <c>SingerHistory</c> entry (or merges it into a matching song row if editing the title/artist
        /// matches another entry for the same singer), and updates any matching rows in saved rotation CSV reports.
        /// </summary>
        public static bool UpdateHistoryEntry(
            int singerHistoryId,
            string singerName,
            string newSongTitle,
            string newArtist,
            int newTimesPerformed,
            string newVenue,
            string newKey,
            double newTempo,
            out string statusMessage,
            string? reportsDirectory = null)
        {
            statusMessage = string.Empty;
            if (singerHistoryId <= 0)
            {
                statusMessage = "Invalid history entry selected.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(newSongTitle))
            {
                statusMessage = "Song title cannot be empty.";
                return false;
            }

            string cleanSinger = NameFormatting.CleanSingerName(singerName);
            string cleanTitle = NameFormatting.ProperCase(newSongTitle.Trim());
            string cleanArtist = NameFormatting.ProperCase((newArtist ?? string.Empty).Trim());
            string cleanVenue = NormalizeVenueList(newVenue);
            string cleanKey = string.IsNullOrWhiteSpace(newKey) ? "0" : newKey.Trim();
            double cleanTempo = newTempo <= 0 ? 1.0 : Math.Round(newTempo, 2);
            int cleanTimes = Math.Max(1, newTimesPerformed);

            try
            {
                EnsureTableCreated();
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                string oldSinger = cleanSinger;
                string oldTitle = string.Empty;
                string oldArtist = string.Empty;

                using (var currentCmd = connection.CreateCommand())
                {
                    currentCmd.CommandText = @"
                        SELECT SingerName, SongTitle, Artist
                        FROM SingerHistory
                        WHERE SingerHistoryId = $id
                        LIMIT 1;
                    ";
                    currentCmd.Parameters.AddWithValue("$id", singerHistoryId);
                    using var reader = currentCmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        statusMessage = "Could not find the selected history record.";
                        return false;
                    }

                    oldSinger = reader.IsDBNull(0) ? cleanSinger : reader.GetString(0);
                    oldTitle = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    oldArtist = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                }

                if (string.IsNullOrWhiteSpace(cleanSinger))
                {
                    cleanSinger = NameFormatting.CleanSingerName(oldSinger);
                }

                // Check if another row for the same singer now matches this song title & artist (or has a blank artist)
                int? duplicateRowId = null;
                string duplicateArtist = string.Empty;
                string duplicateVenue = string.Empty;
                int duplicateTimes = 0;

                using (var dupCmd = connection.CreateCommand())
                {
                    dupCmd.CommandText = @"
                        SELECT SingerHistoryId, Artist, Venue, TimesPerformed
                        FROM SingerHistory
                        WHERE SingerHistoryId != $id
                          AND SingerName = $singerName COLLATE NOCASE
                          AND SongTitle = $songTitle COLLATE NOCASE
                          AND (Artist = $artist COLLATE NOCASE OR Artist = '' OR $artist = '')
                        ORDER BY CASE WHEN Artist = $artist COLLATE NOCASE THEN 0 WHEN Artist != '' THEN 1 ELSE 2 END,
                                 SingerHistoryId ASC
                        LIMIT 1;
                    ";
                    dupCmd.Parameters.AddWithValue("$id", singerHistoryId);
                    dupCmd.Parameters.AddWithValue("$singerName", cleanSinger);
                    dupCmd.Parameters.AddWithValue("$songTitle", cleanTitle);
                    dupCmd.Parameters.AddWithValue("$artist", cleanArtist);

                    using var dupReader = dupCmd.ExecuteReader();
                    if (dupReader.Read())
                    {
                        duplicateRowId = dupReader.GetInt32(0);
                        duplicateArtist = dupReader.IsDBNull(1) ? string.Empty : dupReader.GetString(1);
                        duplicateVenue = dupReader.IsDBNull(2) ? string.Empty : dupReader.GetString(2);
                        duplicateTimes = dupReader.IsDBNull(3) ? 1 : Math.Max(1, dupReader.GetInt32(3));
                    }
                }

                if (duplicateRowId.HasValue)
                {
                    string resolvedArtist = !string.IsNullOrWhiteSpace(cleanArtist) ? cleanArtist : duplicateArtist;
                    string mergedVenue = DatabaseService.MergeVenueLists(duplicateVenue, cleanVenue);
                    int combinedTimes = duplicateTimes + cleanTimes;

                    using (var mergeCmd = connection.CreateCommand())
                    {
                        mergeCmd.CommandText = @"
                            UPDATE SingerHistory
                            SET SongTitle = $songTitle,
                                Artist = $artist,
                                Venue = $venue,
                                TimesPerformed = $timesPerformed,
                                Key = $key,
                                Tempo = $tempo
                            WHERE SingerHistoryId = $targetId;
                        ";
                        mergeCmd.Parameters.AddWithValue("$songTitle", cleanTitle);
                        mergeCmd.Parameters.AddWithValue("$artist", resolvedArtist);
                        mergeCmd.Parameters.AddWithValue("$venue", mergedVenue);
                        mergeCmd.Parameters.AddWithValue("$timesPerformed", combinedTimes);
                        mergeCmd.Parameters.AddWithValue("$key", cleanKey);
                        mergeCmd.Parameters.AddWithValue("$tempo", cleanTempo);
                        mergeCmd.Parameters.AddWithValue("$targetId", duplicateRowId.Value);
                        mergeCmd.ExecuteNonQuery();
                    }

                    using (var delCmd = connection.CreateCommand())
                    {
                        delCmd.CommandText = "DELETE FROM SingerHistory WHERE SingerHistoryId = $id;";
                        delCmd.Parameters.AddWithValue("$id", singerHistoryId);
                        delCmd.ExecuteNonQuery();
                    }

                    if (!string.IsNullOrWhiteSpace(oldTitle))
                    {
                        SavedRotationReportReader.UpdateSingerSongInReports(
                            cleanSinger,
                            oldTitle,
                            oldArtist,
                            cleanTitle,
                            resolvedArtist,
                            reportsDirectory);
                    }

                    RefreshSingerAggregatesOnConnection(connection, cleanSinger);
                    statusMessage = $"Merged duplicate entry into '{cleanTitle}' ({combinedTimes}x).";
                    return true;
                }
                else
                {
                    using (var updateCmd = connection.CreateCommand())
                    {
                        updateCmd.CommandText = @"
                            UPDATE SingerHistory
                            SET SongTitle = $songTitle,
                                Artist = $artist,
                                Venue = $venue,
                                TimesPerformed = $timesPerformed,
                                Key = $key,
                                Tempo = $tempo
                            WHERE SingerHistoryId = $id;
                        ";
                        updateCmd.Parameters.AddWithValue("$songTitle", cleanTitle);
                        updateCmd.Parameters.AddWithValue("$artist", cleanArtist);
                        updateCmd.Parameters.AddWithValue("$venue", cleanVenue);
                        updateCmd.Parameters.AddWithValue("$timesPerformed", cleanTimes);
                        updateCmd.Parameters.AddWithValue("$key", cleanKey);
                        updateCmd.Parameters.AddWithValue("$tempo", cleanTempo);
                        updateCmd.Parameters.AddWithValue("$id", singerHistoryId);
                        updateCmd.ExecuteNonQuery();
                    }

                    bool titleOrArtistChanged =
                        !string.Equals(oldTitle, cleanTitle, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(oldArtist, cleanArtist, StringComparison.OrdinalIgnoreCase);

                    if (titleOrArtistChanged && !string.IsNullOrWhiteSpace(oldTitle))
                    {
                        SavedRotationReportReader.UpdateSingerSongInReports(
                            cleanSinger,
                            oldTitle,
                            oldArtist,
                            cleanTitle,
                            cleanArtist,
                            reportsDirectory);
                    }

                    RefreshSingerAggregatesOnConnection(connection, cleanSinger);
                    statusMessage = $"Updated '{cleanTitle}' in {cleanSinger}'s performance history.";
                    return true;
                }
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"Error updating SingerHistory entry #{singerHistoryId}", ex);
                statusMessage = "Failed to update performance history entry.";
                return false;
            }
        }

        /// <summary>
        /// Adds a new song performance history record for a singer (or updates/merges if the song already exists).
        /// </summary>
        public static bool AddManualHistoryEntry(
            string singerName,
            string songTitle,
            string artist,
            int timesPerformed,
            string venue,
            string key,
            double tempo,
            out string statusMessage)
        {
            statusMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(singerName) || string.IsNullOrWhiteSpace(songTitle))
            {
                statusMessage = "Singer name and song title are required.";
                return false;
            }

            string cleanSinger = NameFormatting.CleanSingerName(singerName);
            string cleanTitle = NameFormatting.ProperCase(songTitle.Trim());
            string cleanArtist = NameFormatting.ProperCase((artist ?? string.Empty).Trim());
            string cleanVenue = NormalizeVenueList(venue);
            string cleanKey = string.IsNullOrWhiteSpace(key) ? "0" : key.Trim();
            double cleanTempo = tempo <= 0 ? 1.0 : Math.Round(tempo, 2);
            int cleanTimes = Math.Max(1, timesPerformed);

            try
            {
                EnsureTableCreated();
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = @"
                    SELECT SingerHistoryId, Artist, Venue, TimesPerformed
                    FROM SingerHistory
                    WHERE SingerName = $singerName COLLATE NOCASE
                      AND SongTitle = $songTitle COLLATE NOCASE
                      AND (Artist = $artist COLLATE NOCASE OR Artist = '' OR $artist = '')
                    ORDER BY CASE WHEN Artist = $artist COLLATE NOCASE THEN 0 WHEN Artist != '' THEN 1 ELSE 2 END,
                             SingerHistoryId ASC
                    LIMIT 1;
                ";
                checkCmd.Parameters.AddWithValue("$singerName", cleanSinger);
                checkCmd.Parameters.AddWithValue("$songTitle", cleanTitle);
                checkCmd.Parameters.AddWithValue("$artist", cleanArtist);

                int? existingId = null;
                string existingArtist = string.Empty;
                string existingVenue = string.Empty;
                int existingTimes = 0;

                using (var reader = checkCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        existingId = reader.GetInt32(0);
                        existingArtist = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        existingVenue = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                        existingTimes = reader.IsDBNull(3) ? 1 : Math.Max(1, reader.GetInt32(3));
                    }
                }

                string resolvedArtist = !string.IsNullOrWhiteSpace(cleanArtist) ? cleanArtist : existingArtist;
                string mergedVenue = DatabaseService.MergeVenueLists(existingVenue, cleanVenue);

                if (existingId.HasValue)
                {
                    int combinedTimes = existingTimes + cleanTimes;
                    using var updateCmd = connection.CreateCommand();
                    updateCmd.CommandText = @"
                        UPDATE SingerHistory
                        SET Artist = $artist,
                            Venue = $venue,
                            TimesPerformed = $timesPerformed,
                            Key = $key,
                            Tempo = $tempo,
                            Timestamp = $timestamp
                        WHERE SingerHistoryId = $id;
                    ";
                    updateCmd.Parameters.AddWithValue("$artist", resolvedArtist);
                    updateCmd.Parameters.AddWithValue("$venue", mergedVenue);
                    updateCmd.Parameters.AddWithValue("$timesPerformed", combinedTimes);
                    updateCmd.Parameters.AddWithValue("$key", cleanKey);
                    updateCmd.Parameters.AddWithValue("$tempo", cleanTempo);
                    updateCmd.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("o"));
                    updateCmd.Parameters.AddWithValue("$id", existingId.Value);
                    updateCmd.ExecuteNonQuery();

                    RefreshSingerAggregatesOnConnection(connection, cleanSinger);
                    statusMessage = $"Merged into existing '{cleanTitle}' ({combinedTimes}x).";
                    return true;
                }
                else
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = @"
                        INSERT INTO SingerHistory (SingerName, SongTitle, Artist, Link, Source, Timestamp, Key, Tempo, Venue, TimesPerformed)
                        VALUES ($singerName, $songTitle, $artist, '', 'Local', $timestamp, $key, $tempo, $venue, $timesPerformed);
                    ";
                    insertCmd.Parameters.AddWithValue("$singerName", cleanSinger);
                    insertCmd.Parameters.AddWithValue("$songTitle", cleanTitle);
                    insertCmd.Parameters.AddWithValue("$artist", resolvedArtist);
                    insertCmd.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("o"));
                    insertCmd.Parameters.AddWithValue("$key", cleanKey);
                    insertCmd.Parameters.AddWithValue("$tempo", cleanTempo);
                    insertCmd.Parameters.AddWithValue("$venue", mergedVenue);
                    insertCmd.Parameters.AddWithValue("$timesPerformed", cleanTimes);
                    insertCmd.ExecuteNonQuery();

                    RefreshSingerAggregatesOnConnection(connection, cleanSinger);
                    statusMessage = $"Added '{cleanTitle}' to {cleanSinger}'s performance history.";
                    return true;
                }
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Error adding manual SingerHistory entry", ex);
                statusMessage = "Failed to add song to performance history.";
                return false;
            }
        }

        private static string NormalizeVenueList(string? rawVenueList)
        {
            if (string.IsNullOrWhiteSpace(rawVenueList)) return string.Empty;
            var cleanedParts = rawVenueList
                .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeVenueName)
                .Where(v => !string.IsNullOrWhiteSpace(v));
            return DatabaseService.MergeVenueLists(string.Join(", ", cleanedParts), null);
        }

        private static void RefreshSingerAggregatesOnConnection(SqliteConnection connection, string singerName)
        {
            UpsertSingerVenueOnConnection(connection, singerName, string.Empty, incrementSongsSung: false, lastSangUtc: null);

            using var syncCountCmd = connection.CreateCommand();
            syncCountCmd.CommandText = @"
                UPDATE Singers
                SET TotalSongsSung = (
                    SELECT COALESCE(SUM(TimesPerformed), 0) FROM SingerHistory WHERE SingerHistory.SingerName = $name COLLATE NOCASE
                )
                WHERE Name = $name COLLATE NOCASE;
            ";
            syncCountCmd.Parameters.AddWithValue("$name", singerName);
            syncCountCmd.ExecuteNonQuery();

            Lyracist.Data.LyracistDbContext.EnsureAndSyncDedicatedSingersDatabase();
        }

        /// <summary>
        /// Scans saved rotation reports (<c>Reports/*.csv</c>), imports qualifying singers (1st &amp; last name or distinct name),
        /// their performed songs, and the report venues into <c>Singers</c> and <c>SingerHistory</c>, and purges any
        /// non-qualifying generic single-name entries that do not have <c>IsDistinctName</c> enabled.
        /// </summary>
        public static (int SingersSynced, int SongsSynced) SyncFromSavedReportsAndCleanup(string? reportsDirectory = null)
        {
            int singersSynced = 0;
            int songsSynced = 0;

            try
            {
                EnsureTableCreated();

                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                // 1. Purge non-qualifying generic single-word names (where IsDistinctName = 0) from Singers & SingerHistory
                PurgeNonQualifyingEntriesOnConnection(connection);

                // 2. Import all qualifying singers, songs, performance counts, and venues from saved rotation reports
                var reports = SavedRotationReportReader.LoadAllReports(reportsDirectory);
                var syncedSingerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var singerLatestDates = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                var singerVenuesMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var songAggregates = new Dictionary<(string Singer, string Title, string Artist), (int Count, string Venues, DateTime LatestUtc)>(
                    new SingerSongKeyComparer());

                void AccumulateSongPerformance(string singer, string rawTitle, string? rawArtist, string venue, DateTime perfUtc)
                {
                    string cleanTitle = NameFormatting.ProperCase(rawTitle.Trim());
                    string cleanArtist = NameFormatting.ProperCase((rawArtist ?? string.Empty).Trim());
                    var key = (singer, cleanTitle, cleanArtist);
                    if (songAggregates.TryGetValue(key, out var existing))
                    {
                        string mergedVenues = DatabaseService.MergeVenueLists(existing.Venues, venue);
                        DateTime newest = perfUtc > existing.LatestUtc ? perfUtc : existing.LatestUtc;
                        songAggregates[key] = (existing.Count + 1, mergedVenues, newest);
                    }
                    else
                    {
                        songAggregates[key] = (1, DatabaseService.MergeVenueLists(null, venue), perfUtc);
                    }
                }

                // Process oldest to newest so timestamps and LastSang reflect the latest show
                foreach (var report in reports.OrderBy(r => r.ReportDate).ThenBy(r => r.LastModified))
                {
                    string reportVenue = NormalizeVenueName(report.VenueName);
                    DateTime reportDateUtc = report.ReportDate != DateTime.MinValue
                        ? DateTime.SpecifyKind(report.ReportDate, DateTimeKind.Local).ToUniversalTime()
                        : report.LastModified.ToUniversalTime();

                    foreach (var group in report.SingerGroups)
                    {
                        if (group.IsMusic) continue;

                        string cleanSinger = NameFormatting.CleanSingerName(group.SingerName);
                        if (!IsEligibleDatabaseSinger(cleanSinger, connection))
                        {
                            continue;
                        }

                        if (syncedSingerNames.Add(cleanSinger))
                        {
                            singersSynced++;
                        }

                        singerVenuesMap[cleanSinger] = DatabaseService.MergeVenueLists(
                            singerVenuesMap.GetValueOrDefault(cleanSinger),
                            reportVenue);

                        DateTime? latestPerformanceUtc = null;
                        foreach (var perf in group.Performances)
                        {
                            if (string.IsNullOrWhiteSpace(perf.SongTitle)) continue;

                            DateTime perfUtc = perf.Timestamp.HasValue
                                ? perf.Timestamp.Value.ToUniversalTime()
                                : reportDateUtc;
                            latestPerformanceUtc = perfUtc;

                            AccumulateSongPerformance(cleanSinger, perf.SongTitle, perf.Artist, reportVenue, perfUtc);
                            songsSynced++;

                            // If there is a qualifying duet partner on the row, record the song & venue for them too
                            if (perf.HasDuetPartner)
                            {
                                string cleanPartner = NameFormatting.CleanSingerName(perf.DuetPartnerName);
                                if (IsEligibleDatabaseSinger(cleanPartner, connection))
                                {
                                    if (syncedSingerNames.Add(cleanPartner))
                                    {
                                        singersSynced++;
                                    }
                                    singerVenuesMap[cleanPartner] = DatabaseService.MergeVenueLists(
                                        singerVenuesMap.GetValueOrDefault(cleanPartner),
                                        reportVenue);
                                    if (!singerLatestDates.TryGetValue(cleanPartner, out var existingPartnerDt) || perfUtc > existingPartnerDt)
                                    {
                                        singerLatestDates[cleanPartner] = perfUtc;
                                    }
                                    AccumulateSongPerformance(cleanPartner, perf.SongTitle, perf.Artist, reportVenue, perfUtc);
                                }
                            }
                        }

                        DateTime groupLatest = latestPerformanceUtc ?? reportDateUtc;
                        if (!singerLatestDates.TryGetValue(cleanSinger, out var prevDt) || groupLatest > prevDt)
                        {
                            singerLatestDates[cleanSinger] = groupLatest;
                        }
                    }
                }

                // Consolidate any blank-artist song occurrences into a matching non-empty artist occurrence for the same singer & song
                var blankArtistKeys = songAggregates.Keys
                    .Where(k => string.IsNullOrWhiteSpace(k.Artist))
                    .ToList();

                foreach (var blankKey in blankArtistKeys)
                {
                    var matchingNonEmptyKey = songAggregates.Keys.FirstOrDefault(k =>
                        !string.IsNullOrWhiteSpace(k.Artist) &&
                        string.Equals(k.Singer, blankKey.Singer, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(k.Title, blankKey.Title, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrWhiteSpace(matchingNonEmptyKey.Artist) &&
                        songAggregates.TryGetValue(blankKey, out var blankVal) &&
                        songAggregates.TryGetValue(matchingNonEmptyKey, out var targetVal))
                    {
                        string mergedVenues = DatabaseService.MergeVenueLists(targetVal.Venues, blankVal.Venues);
                        DateTime newest = blankVal.LatestUtc > targetVal.LatestUtc ? blankVal.LatestUtc : targetVal.LatestUtc;
                        songAggregates[matchingNonEmptyKey] = (targetVal.Count + blankVal.Count, mergedVenues, newest);
                        songAggregates.Remove(blankKey);
                    }
                }

                // Consolidate any pre-existing blank-artist duplicate rows already stored in SingerHistory
                ConsolidateBlankArtistDuplicatesOnConnection(connection);

                foreach (var kvp in songAggregates)
                {
                    SaveHistoryOnConnection(
                        connection,
                        kvp.Key.Singer,
                        kvp.Key.Title,
                        kvp.Key.Artist,
                        kvp.Value.Venues,
                        kvp.Value.LatestUtc,
                        kvp.Value.Count);
                }

                // Run consolidation again in case a newly-imported artist row matched a pre-existing blank-artist row
                ConsolidateBlankArtistDuplicatesOnConnection(connection);

                foreach (string singer in syncedSingerNames)
                {
                    UpsertSingerVenueOnConnection(
                        connection,
                        singer,
                        singerVenuesMap.GetValueOrDefault(singer, string.Empty),
                        incrementSongsSung: false,
                        lastSangUtc: singerLatestDates.TryGetValue(singer, out var dt) ? dt : null);
                }

                // 3. Ensure every singer's TotalSongsSung is at least the sum of their recorded TimesPerformed in SingerHistory
                using var syncCountCmd = connection.CreateCommand();
                syncCountCmd.CommandText = @"
                    UPDATE Singers
                    SET TotalSongsSung = (
                        SELECT COALESCE(SUM(TimesPerformed), 0) FROM SingerHistory WHERE SingerHistory.SingerName = Singers.Name COLLATE NOCASE
                    )
                    WHERE TotalSongsSung < (
                        SELECT COALESCE(SUM(TimesPerformed), 0) FROM SingerHistory WHERE SingerHistory.SingerName = Singers.Name COLLATE NOCASE
                    );
                ";
                syncCountCmd.ExecuteNonQuery();

                Lyracist.Data.LyracistDbContext.EnsureAndSyncDedicatedSingersDatabase();
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Error in SyncFromSavedReportsAndCleanup", ex);
            }

            return (singersSynced, songsSynced);
        }

        private sealed class SingerSongKeyComparer : IEqualityComparer<(string Singer, string Title, string Artist)>
        {
            public bool Equals((string Singer, string Title, string Artist) x, (string Singer, string Title, string Artist) y)
            {
                return string.Equals(x.Singer, y.Singer, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(x.Title, y.Title, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(x.Artist, y.Artist, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode((string Singer, string Title, string Artist) obj)
            {
                return HashCode.Combine(
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Singer ?? string.Empty),
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Title ?? string.Empty),
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Artist ?? string.Empty));
            }
        }

        private static void SaveHistoryOnConnection(
            SqliteConnection connection,
            string singerName,
            string songTitle,
            string artist,
            string cleanVenue,
            DateTime timestampUtc,
            int reportPerformanceCount = 1)
        {
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = @"
                SELECT SingerHistoryId, Artist, Venue, Timestamp, TimesPerformed
                FROM SingerHistory
                WHERE SingerName = $singerName COLLATE NOCASE
                  AND SongTitle = $songTitle COLLATE NOCASE
                  AND (Artist = $artist COLLATE NOCASE OR Artist = '' OR $artist = '')
                ORDER BY CASE WHEN Artist = $artist COLLATE NOCASE THEN 0 WHEN Artist != '' THEN 1 ELSE 2 END,
                         SingerHistoryId ASC
                LIMIT 1;
            ";
            checkCmd.Parameters.AddWithValue("$singerName", singerName);
            checkCmd.Parameters.AddWithValue("$songTitle", songTitle);
            checkCmd.Parameters.AddWithValue("$artist", artist);

            int? existingId = null;
            string existingArtist = string.Empty;
            string existingVenue = string.Empty;
            DateTime existingTimestamp = DateTime.MinValue;
            int existingTimes = 1;

            using (var reader = checkCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    existingId = reader.GetInt32(0);
                    existingArtist = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    existingVenue = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    if (!reader.IsDBNull(3) && DateTime.TryParse(reader.GetString(3), out var dt))
                    {
                        existingTimestamp = dt;
                    }
                    existingTimes = reader.IsDBNull(4) ? 1 : Math.Max(1, reader.GetInt32(4));
                }
            }

            string resolvedArtist = !string.IsNullOrWhiteSpace(artist) ? artist : existingArtist;
            string mergedVenue = DatabaseService.MergeVenueLists(existingVenue, cleanVenue);
            DateTime newestTs = timestampUtc > existingTimestamp ? timestampUtc : existingTimestamp;
            int newTimes = Math.Max(existingTimes, Math.Max(1, reportPerformanceCount));

            if (existingId.HasValue)
            {
                using var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = @"
                    UPDATE SingerHistory
                    SET Artist = $artist,
                        Venue = $venue,
                        Timestamp = $timestamp,
                        TimesPerformed = $timesPerformed
                    WHERE SingerHistoryId = $id;
                ";
                updateCmd.Parameters.AddWithValue("$artist", resolvedArtist);
                updateCmd.Parameters.AddWithValue("$venue", mergedVenue);
                updateCmd.Parameters.AddWithValue("$timestamp", newestTs.ToString("o"));
                updateCmd.Parameters.AddWithValue("$timesPerformed", newTimes);
                updateCmd.Parameters.AddWithValue("$id", existingId.Value);
                updateCmd.ExecuteNonQuery();
            }
            else
            {
                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO SingerHistory (SingerName, SongTitle, Artist, Link, Source, Timestamp, Key, Tempo, Venue, TimesPerformed)
                    VALUES ($singerName, $songTitle, $artist, '', 'Local', $timestamp, '0', 1.0, $venue, $timesPerformed);
                ";
                insertCmd.Parameters.AddWithValue("$singerName", singerName);
                insertCmd.Parameters.AddWithValue("$songTitle", songTitle);
                insertCmd.Parameters.AddWithValue("$artist", resolvedArtist);
                insertCmd.Parameters.AddWithValue("$timestamp", timestampUtc.ToString("o"));
                insertCmd.Parameters.AddWithValue("$venue", mergedVenue);
                insertCmd.Parameters.AddWithValue("$timesPerformed", Math.Max(1, reportPerformanceCount));
                insertCmd.ExecuteNonQuery();
            }
        }

        private static void ConsolidateBlankArtistDuplicatesOnConnection(SqliteConnection connection)
        {
            var rows = new List<(int Id, string Singer, string Title, string Artist, string Venue, int Times, DateTime Ts)>();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT SingerHistoryId, SingerName, SongTitle, Artist, Venue, TimesPerformed, Timestamp
                    FROM SingerHistory;
                ";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string singer = reader.IsDBNull(1) ? string.Empty : NameFormatting.CleanSingerName(reader.GetString(1));
                    string title = reader.IsDBNull(2) ? string.Empty : NameFormatting.ProperCase(reader.GetString(2).Trim());
                    string artist = reader.IsDBNull(3) ? string.Empty : NameFormatting.ProperCase(reader.GetString(3).Trim());
                    string venue = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                    int times = reader.IsDBNull(5) ? 1 : Math.Max(1, reader.GetInt32(5));
                    DateTime ts = !reader.IsDBNull(6) && DateTime.TryParse(reader.GetString(6), out var dt) ? dt : DateTime.MinValue;
                    rows.Add((id, singer, title, artist, venue, times, ts));
                }
            }

            // Group by (Singer, Title) case-insensitively
            var groups = rows
                .GroupBy(r => (Singer: r.Singer.ToUpperInvariant(), Title: r.Title.ToUpperInvariant()))
                .Where(g => g.Count() > 1);

            foreach (var group in groups)
            {
                var blankRows = group.Where(r => string.IsNullOrWhiteSpace(r.Artist)).ToList();
                var withArtistRows = group.Where(r => !string.IsNullOrWhiteSpace(r.Artist)).ToList();

                if (blankRows.Count > 0 && withArtistRows.Count > 0)
                {
                    var primary = withArtistRows.OrderByDescending(r => r.Times).ThenBy(r => r.Id).First();
                    string mergedVenue = primary.Venue;
                    int totalTimes = primary.Times;
                    DateTime latestTs = primary.Ts;

                    foreach (var blank in blankRows)
                    {
                        mergedVenue = DatabaseService.MergeVenueLists(mergedVenue, blank.Venue);
                        totalTimes += blank.Times;
                        if (blank.Ts > latestTs) latestTs = blank.Ts;

                        using var delCmd = connection.CreateCommand();
                        delCmd.CommandText = "DELETE FROM SingerHistory WHERE SingerHistoryId = $id;";
                        delCmd.Parameters.AddWithValue("$id", blank.Id);
                        delCmd.ExecuteNonQuery();
                    }

                    using var updCmd = connection.CreateCommand();
                    updCmd.CommandText = @"
                        UPDATE SingerHistory
                        SET Venue = $venue,
                            TimesPerformed = $timesPerformed,
                            Timestamp = $timestamp
                        WHERE SingerHistoryId = $id;
                    ";
                    updCmd.Parameters.AddWithValue("$venue", mergedVenue);
                    updCmd.Parameters.AddWithValue("$timesPerformed", totalTimes);
                    updCmd.Parameters.AddWithValue("$timestamp", (latestTs != DateTime.MinValue ? latestTs : DateTime.UtcNow).ToString("o"));
                    updCmd.Parameters.AddWithValue("$id", primary.Id);
                    updCmd.ExecuteNonQuery();
                }
            }
        }

        private static void PurgeNonQualifyingEntriesOnConnection(SqliteConnection connection)
        {
            var distinctSingleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var singerIdsToRemove = new List<int>();
            var singerNamesToRemove = new List<string>();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT SingerId, Name, IsDistinctName FROM Singers;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    bool isDistinct = !reader.IsDBNull(2) && reader.GetInt32(2) != 0;

                    if (NameFormatting.IsQualifyingDatabaseSingerName(name, isDistinct))
                    {
                        if (isDistinct)
                        {
                            distinctSingleNames.Add(NameFormatting.CleanSingerName(name));
                        }
                    }
                    else
                    {
                        singerIdsToRemove.Add(id);
                        singerNamesToRemove.Add(name);
                    }
                }
            }

            foreach (int id in singerIdsToRemove)
            {
                using var delCmd = connection.CreateCommand();
                delCmd.CommandText = "DELETE FROM Singers WHERE SingerId = $id;";
                delCmd.Parameters.AddWithValue("$id", id);
                delCmd.ExecuteNonQuery();
            }

            // Also purge non-qualifying generic single-name entries from main lyracist.db if present
            string mainDbPath = Lyracist.Data.LyracistDbContext.GetDatabaseFilePath();
            if (File.Exists(mainDbPath))
            {
                try
                {
                    using var mainConn = new SqliteConnection(Lyracist.Data.LyracistDbContext.GetConnectionString());
                    mainConn.Open();
                    var mainIdsToRemove = new List<int>();
                    using (var mainCmd = mainConn.CreateCommand())
                    {
                        mainCmd.CommandText = "SELECT SingerId, Name, IsDistinctName FROM Singers;";
                        using var mainReader = mainCmd.ExecuteReader();
                        while (mainReader.Read())
                        {
                            int id = mainReader.GetInt32(0);
                            string name = mainReader.IsDBNull(1) ? string.Empty : mainReader.GetString(1);
                            bool isDistinct = !mainReader.IsDBNull(2) && mainReader.GetInt32(2) != 0;
                            if (!NameFormatting.IsQualifyingDatabaseSingerName(name, isDistinct))
                            {
                                mainIdsToRemove.Add(id);
                            }
                        }
                    }

                    foreach (int id in mainIdsToRemove)
                    {
                        using var delMainCmd = mainConn.CreateCommand();
                        delMainCmd.CommandText = "DELETE FROM Singers WHERE SingerId = $id;";
                        delMainCmd.Parameters.AddWithValue("$id", id);
                        delMainCmd.ExecuteNonQuery();
                    }
                }
                catch
                {
                }
            }

            var historyIdsToRemove = new List<int>();
            using (var histCmd = connection.CreateCommand())
            {
                histCmd.CommandText = "SELECT SingerHistoryId, SingerName FROM SingerHistory;";
                using var reader = histCmd.ExecuteReader();
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    string clean = NameFormatting.CleanSingerName(name);
                    bool isDistinct = distinctSingleNames.Contains(clean);
                    if (!NameFormatting.IsQualifyingDatabaseSingerName(clean, isDistinct))
                    {
                        historyIdsToRemove.Add(id);
                    }
                }
            }

            foreach (int id in historyIdsToRemove)
            {
                using var delHistCmd = connection.CreateCommand();
                delHistCmd.CommandText = "DELETE FROM SingerHistory WHERE SingerHistoryId = $id;";
                delHistCmd.Parameters.AddWithValue("$id", id);
                delHistCmd.ExecuteNonQuery();
            }
        }
    }
}
