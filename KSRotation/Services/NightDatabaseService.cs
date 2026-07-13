// Last Edit: Jun 30, 2026 06:47 - Switched night DB path resolution to shared AppPaths helper.
using KSRotation.Models;
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
        private const string DbFileName = "night_db.json";

        private static readonly JsonSerializerOptions SerializerOptions = AppJsonContext.Default.Options;

        private static string SettingsDirectoryPath => AppPaths.SettingsDirectoryPath;
        private static string DbFilePath => Path.Combine(SettingsDirectoryPath, DbFileName);

        public static NightDbState Load()
        {
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
