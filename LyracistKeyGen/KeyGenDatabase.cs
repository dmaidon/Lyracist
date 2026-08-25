// Edited on Aug 25, 2026 @ 06:15:00 -> Fix RCS1037 trailing whitespace
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace LyracistKeyGen
{
    public record LicenseRecord(int Id, string FirstName, string LastName, string StageName, string Email, string LicenseKey, DateTime CreatedAt);

    public static class KeyGenDatabase
    {
        private static string DbPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keygen.db");
        private static string ConnectionString => $"Data Source={DbPath}";

        public static void Initialize()
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Licenses (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FirstName TEXT NOT NULL,
                    LastName TEXT NOT NULL,
                    StageName TEXT,
                    Email TEXT NOT NULL,
                    LicenseKey TEXT NOT NULL UNIQUE,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );";
            cmd.ExecuteNonQuery();
        }

        public static void SaveLicense(string firstName, string lastName, string stageName, string email, string key)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO Licenses (FirstName, LastName, StageName, Email, LicenseKey)
                VALUES ($firstName, $lastName, $stageName, $email, $key);";

            cmd.Parameters.AddWithValue("$firstName", firstName.Trim());
            cmd.Parameters.AddWithValue("$lastName", lastName.Trim());
            cmd.Parameters.AddWithValue("$stageName", string.IsNullOrWhiteSpace(stageName) ? "None" : stageName.Trim());
            cmd.Parameters.AddWithValue("$email", email.Trim());
            cmd.Parameters.AddWithValue("$key", key.Trim());

            cmd.ExecuteNonQuery();
        }

        public static List<LicenseRecord> GetHistory()
        {
            var list = new List<LicenseRecord>();
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, FirstName, LastName, StageName, Email, LicenseKey, CreatedAt FROM Licenses ORDER BY Id DESC;";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new LicenseRecord(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        reader.GetString(5),
                        reader.GetDateTime(6)
                    ));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to read history from database: {ex.Message}");
            }
            return list;
        }
    }
}
