// Edited on Sep 6, 2026 @ 07:28:45 -> Add Key and Tempo columns to SingerHistory and support per-song key/tempo save and recall
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

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
        public DateTime Timestamp { get; set; }
    }

    public static class SingerHistoryService
    {
        private static string GetConnectionString()
        {
            string dbPath = Path.Combine(Lyracist.Shared.Globals.DataDir, "lyracist.db");
            return $"Data Source={dbPath};Cache=Shared";
        }

        public static void EnsureTableCreated()
        {
            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS SingerHistory (
                        SingerHistoryId INTEGER PRIMARY KEY AUTOINCREMENT,
                        SingerName TEXT NOT NULL,
                        SongTitle TEXT NOT NULL,
                        Artist TEXT NOT NULL,
                        Link TEXT,
                        Source TEXT NOT NULL,
                        Timestamp TEXT NOT NULL,
                        Key TEXT DEFAULT '0',
                        Tempo REAL DEFAULT 1.0
                    );
                    CREATE INDEX IF NOT EXISTS IX_SingerHistory_SingerName ON SingerHistory (SingerName);
                ";
                command.ExecuteNonQuery();

                // Safe migrations for pre-existing tables lacking Key or Tempo columns
                try
                {
                    command.CommandText = "ALTER TABLE SingerHistory ADD COLUMN Key TEXT DEFAULT '0';";
                    command.ExecuteNonQuery();
                }
                catch { /* Column already exists */ }

                try
                {
                    command.CommandText = "ALTER TABLE SingerHistory ADD COLUMN Tempo REAL DEFAULT 1.0;";
                    command.ExecuteNonQuery();
                }
                catch { /* Column already exists */ }
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Error creating SingerHistory table", ex);
            }
        }

        public static void SaveHistory(string singerName, string songTitle, string artist, string source, string link, string key = "0", double tempo = 1.0)
        {
            if (string.IsNullOrWhiteSpace(singerName) || string.IsNullOrWhiteSpace(songTitle)) return;

            singerName = Lyracist.Shared.NameFormatting.ProperCase(singerName);
            songTitle = Lyracist.Shared.NameFormatting.ProperCase(songTitle);
            artist = Lyracist.Shared.NameFormatting.ProperCase(artist);

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                // Check if this singer already sang this song/link to avoid duplicates:
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM SingerHistory WHERE SingerName = $singerName COLLATE NOCASE AND SongTitle = $songTitle COLLATE NOCASE AND Artist = $artist COLLATE NOCASE";
                checkCmd.Parameters.AddWithValue("$singerName", singerName.Trim());
                checkCmd.Parameters.AddWithValue("$songTitle", songTitle.Trim());
                checkCmd.Parameters.AddWithValue("$artist", artist.Trim());
                long count = (long)(checkCmd.ExecuteScalar() ?? 0L);

                if (count > 0)
                {
                    // Update timestamp, key, and tempo of existing entry
                    using var updateCmd = connection.CreateCommand();
                    updateCmd.CommandText = @"
                        UPDATE SingerHistory 
                        SET Timestamp = $timestamp, Link = $link, Source = $source, Key = $key, Tempo = $tempo 
                        WHERE SingerName = $singerName COLLATE NOCASE AND SongTitle = $songTitle COLLATE NOCASE AND Artist = $artist COLLATE NOCASE";
                    updateCmd.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("o"));
                    updateCmd.Parameters.AddWithValue("$link", link ?? string.Empty);
                    updateCmd.Parameters.AddWithValue("$source", source ?? "Local");
                    updateCmd.Parameters.AddWithValue("$key", string.IsNullOrWhiteSpace(key) ? "0" : key.Trim());
                    updateCmd.Parameters.AddWithValue("$tempo", tempo <= 0 ? 1.0 : tempo);
                    updateCmd.Parameters.AddWithValue("$singerName", singerName.Trim());
                    updateCmd.Parameters.AddWithValue("$songTitle", songTitle.Trim());
                    updateCmd.Parameters.AddWithValue("$artist", artist.Trim());
                    updateCmd.ExecuteNonQuery();
                }
                else
                {
                    // Insert new entry
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = @"
                        INSERT INTO SingerHistory (SingerName, SongTitle, Artist, Link, Source, Timestamp, Key, Tempo)
                        VALUES ($singerName, $songTitle, $artist, $link, $source, $timestamp, $key, $tempo)
                    ";
                    insertCmd.Parameters.AddWithValue("$singerName", singerName.Trim());
                    insertCmd.Parameters.AddWithValue("$songTitle", songTitle.Trim());
                    insertCmd.Parameters.AddWithValue("$artist", artist.Trim());
                    insertCmd.Parameters.AddWithValue("$link", link ?? string.Empty);
                    insertCmd.Parameters.AddWithValue("$source", source ?? "Local");
                    insertCmd.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("o"));
                    insertCmd.Parameters.AddWithValue("$key", string.IsNullOrWhiteSpace(key) ? "0" : key.Trim());
                    insertCmd.Parameters.AddWithValue("$tempo", tempo <= 0 ? 1.0 : tempo);
                    insertCmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Error saving SingerHistory", ex);
            }
        }

        public static List<SingerHistoryEntry> GetHistory(string singerName)
        {
            var list = new List<SingerHistoryEntry>();
            if (string.IsNullOrWhiteSpace(singerName)) return list;

            singerName = Lyracist.Shared.NameFormatting.ProperCase(singerName);

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT SingerHistoryId, SingerName, SongTitle, Artist, Link, Source, Timestamp, Key, Tempo
                    FROM SingerHistory
                    WHERE SingerName = $singerName COLLATE NOCASE
                    ORDER BY Timestamp DESC
                    LIMIT 50
                ";
                command.Parameters.AddWithValue("$singerName", singerName.Trim());

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
                        Tempo = reader.IsDBNull(8) ? 1.0 : reader.GetDouble(8)
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

            singerName = Lyracist.Shared.NameFormatting.ProperCase(singerName);
            songTitle = Lyracist.Shared.NameFormatting.ProperCase(songTitle);

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
    }
}

