// Created on Sep 23, 2026 @ 11:44:00 -> Data bridge for Scaryoke JSON settings persistence
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Scaryoke.Unity.Data
{
    [Serializable]
    public class ScaryokeSettingsModel
    {
        public List<string> Categories = new List<string>
        {
            "Elvis",
            "Rock",
            "Country",
            "Pop",
            "Gender Bender",
            "Oldies",
            "80s Hits",
            "Disco",
            "Hard Rock",
            "Disney Songs",
            "90s Pop"
        };
    }

    public static class ScaryokeDataBridge
    {
        private const string SettingsFileName = "scaryoke_settings.json";

        public static string ResolveSettingsPath()
        {
            // 1. Solution-level Settings directory (in Editor or stand-alone alongside suite)
            string rootDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string solutionSettings = Path.Combine(rootDir, "Settings", SettingsFileName);
            if (File.Exists(solutionSettings))
            {
                return solutionSettings;
            }

            // 2. Standalone build directory (Settings/ relative to executable)
            string localSettings = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", SettingsFileName);
            if (File.Exists(localSettings))
            {
                return localSettings;
            }

            // 3. Unity persistent data path fallback
            return Path.Combine(Application.persistentDataPath, SettingsFileName);
        }

        public static ScaryokeSettingsModel LoadSettings()
        {
            try
            {
                string path = ResolveSettingsPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonUtility.FromJson<ScaryokeSettingsModel>(json);
                    if (loaded != null && loaded.Categories != null && loaded.Categories.Count > 0)
                    {
                        if (loaded.Categories.Count > 11)
                        {
                            loaded.Categories = loaded.Categories.GetRange(0, 11);
                        }
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ScaryokeDataBridge] Failed to load settings from disk: {ex.Message}");
            }

            return new ScaryokeSettingsModel();
        }

        public static bool SaveSettings(ScaryokeSettingsModel settings)
        {
            try
            {
                string path = ResolveSettingsPath();
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonUtility.ToJson(settings, true);
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScaryokeDataBridge] Failed to save settings to disk: {ex.Message}");
                return false;
            }
        }
    }
}
