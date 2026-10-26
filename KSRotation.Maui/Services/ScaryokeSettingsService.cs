// Edited on Oct 26, 2026 @ 06:55:00 -> Add SpinHistory persistence to ScaryokeSettings in Maui
using System.IO;
using System.Linq;
using System.Text.Json;

namespace KSRotation.Maui.Services
{
    public class CategoryStyle
    {
        public double Weight { get; set; } = 1;
        public string? Color { get; set; }
    }

    public class CategoryPreset
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Categories { get; set; } = [];
        public Dictionary<string, CategoryStyle> Styles { get; set; } = [];
    }

    public class ScaryokeSettings
    {
        /// <summary>Per-category weight and colour overrides, keyed by category name.</summary>
        public Dictionary<string, CategoryStyle> Styles { get; set; } = [];

        public List<CategoryPreset> Presets { get; set; } = [];

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

        public List<string> SpinHistory { get; set; } = [];
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
                        settings.Styles ??= [];
                        settings.Presets ??= [];
                        settings.SpinHistory = [.. (settings.SpinHistory ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Take(8)];
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
                string tmp = SettingsPath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, SettingsPath, true);
            }
            catch
            {
                // Best effort
            }
        }
    }
}
