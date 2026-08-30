// Edited on Aug 30, 2026 @ 08:26:00 -> Relocated night database to Data/ksrotation_night_db.json with legacy migration
using KSRotation.Models;
using Lyracist.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace KSRotation.Services
{
    public class NightDbState
    {
        public List<SingerEntry> ActiveQueue { get; set; } = [];
        public List<SongPerformance> PerformanceHistory { get; set; } = [];
    }

    public static class NightDatabaseService
    {
        private const string DbFileName = "ksrotation_night_db.json";

        private static readonly JsonSerializerOptions SerializerOptions = AppJsonContext.Default.Options;

        private static string DataDirectoryPath => AppPaths.DataDirectoryPath;
        private static string DbFilePath => Path.Combine(DataDirectoryPath, DbFileName);

        public static NightDbState Load()
        {
            if (!File.Exists(DbFilePath))
            {
                // Fallback migration: Check legacy Settings/night_db.json or Data/night_db.json
                string legacyInSettings = Path.Combine(AppPaths.SettingsDirectoryPath, "night_db.json");
                string legacyInData = Path.Combine(DataDirectoryPath, "night_db.json");
                if (File.Exists(legacyInSettings))
                {
                    try
                    {
                        Directory.CreateDirectory(DataDirectoryPath);
                        File.Copy(legacyInSettings, DbFilePath, true);
                    }
                    catch { }
                }
                else if (File.Exists(legacyInData))
                {
                    try
                    {
                        File.Copy(legacyInData, DbFilePath, true);
                    }
                    catch { }
                }
            }

            if (!File.Exists(DbFilePath))
            {
                return new NightDbState();
            }

            try
            {
                using FileStream stream = File.OpenRead(DbFilePath);
                NightDbState? state = JsonSerializer.Deserialize<NightDbState>(stream, SerializerOptions);
                return state ?? new NightDbState();
            }
            catch (Exception ex)
            {
                LoggerService.LogError("NightDatabaseService.Load", ex);
                return new NightDbState();
            }
        }

        public static void Save(IEnumerable<SingerEntry> queue, IEnumerable<SongPerformance> history)
        {
            try
            {
                var state = new NightDbState
                {
                    ActiveQueue = [.. queue],
                    PerformanceHistory = [.. history]
                };
                AtomicJsonFile.Serialize(DbFilePath, state, SerializerOptions);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("NightDatabaseService.Save", ex);
            }
        }

        public static void Flush()
        {
            try
            {
                if (File.Exists(DbFilePath))
                {
                    File.Delete(DbFilePath);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("NightDatabaseService.Flush", ex);
            }
        }
    }
}
