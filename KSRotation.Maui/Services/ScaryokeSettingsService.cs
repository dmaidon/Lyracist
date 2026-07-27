// Created on Jul 27, 2026 @ 11:00:00 -> Persisted category list for the Scaryoke Wheel (Maui-only feature).
using System.IO;
using System.Text.Json;

namespace KSRotation.Maui.Services
{
    public class ScaryokeSettings
    {
        public List<string> Categories { get; set; } =
        [
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
        ];
    }

    public static class ScaryokeSettingsService
    {
        public const int MaxCategories = 11;

        private static string SettingsFolder => Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "Scaryoke");

        private static string SettingsPath => Path.Combine(SettingsFolder, "settings.json");

        public static ScaryokeSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<ScaryokeSettings>(json);
                    if (settings?.Categories != null)
                    {
                        if (settings.Categories.Count > MaxCategories)
                        {
                            settings.Categories = settings.Categories.GetRange(0, MaxCategories);
                        }
                        return settings;
                    }
                }
            }
            catch
            {
                // Fallback to defaults on error
            }

            return new ScaryokeSettings();
        }

        public static void Save(ScaryokeSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                if (settings.Categories.Count > MaxCategories)
                {
                    settings.Categories = settings.Categories.GetRange(0, MaxCategories);
                }
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Best effort
            }
        }
    }
}
