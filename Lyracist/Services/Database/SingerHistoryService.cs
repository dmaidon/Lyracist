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
                        Timestamp TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS IX_SingerHistory_SingerName ON SingerHistory (SingerName);
                ";
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating SingerHistory table: {ex.Message}");
            }
        }

        public static void SaveHistory(string singerName, string songTitle, string artist, string source, string link)
        {
            if (string.IsNullOrWhiteSpace(singerName) || string.IsNullOrWhiteSpace(songTitle)) return;

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();

                // Check if this singer already sang this song/link to avoid duplicates:
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM SingerHistory WHERE SingerName = $singerName AND SongTitle = $songTitle AND Artist = $artist";
                checkCmd.Parameters.AddWithValue("$singerName", singerName.Trim());
                checkCmd.Parameters.AddWithValue("$songTitle", songTitle.Trim());
                checkCmd.Parameters.AddWithValue("$artist", artist.Trim());
                long count = (long)(checkCmd.ExecuteScalar() ?? 0L);

                if (count > 0)
                {
                    // Update timestamp of existing entry
                    using var updateCmd = connection.CreateCommand();
                    updateCmd.CommandText = "UPDATE SingerHistory SET Timestamp = $timestamp, Link = $link, Source = $source WHERE SingerName = $singerName AND SongTitle = $songTitle AND Artist = $artist";
                    updateCmd.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("o"));
                    updateCmd.Parameters.AddWithValue("$link", link ?? string.Empty);
                    updateCmd.Parameters.AddWithValue("$source", source ?? "Local");
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
                        INSERT INTO SingerHistory (SingerName, SongTitle, Artist, Link, Source, Timestamp)
                        VALUES ($singerName, $songTitle, $artist, $link, $source, $timestamp)
                    ";
                    insertCmd.Parameters.AddWithValue("$singerName", singerName.Trim());
                    insertCmd.Parameters.AddWithValue("$songTitle", songTitle.Trim());
                    insertCmd.Parameters.AddWithValue("$artist", artist.Trim());
                    insertCmd.Parameters.AddWithValue("$link", link ?? string.Empty);
                    insertCmd.Parameters.AddWithValue("$source", source ?? "Local");
                    insertCmd.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("o"));
                    insertCmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving SingerHistory: {ex.Message}");
            }
        }

        public static List<SingerHistoryEntry> GetHistory(string singerName)
        {
            var list = new List<SingerHistoryEntry>();
            if (string.IsNullOrWhiteSpace(singerName)) return list;

            try
            {
                using var connection = new SqliteConnection(GetConnectionString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT SingerHistoryId, SingerName, SongTitle, Artist, Link, Source, Timestamp
                    FROM SingerHistory
                    WHERE SingerName = $singerName
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
                        Timestamp = DateTime.TryParse(reader.GetString(6), out var dt) ? dt : DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading SingerHistory: {ex.Message}");
            }

            return list;
        }
    }
}
