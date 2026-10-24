// Edited on Aug 30, 2026 @ 08:26:00 -> Update settings path to Settings/scaryoke_settings.json with legacy fallback migration
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ScaryokeWheel;

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
    public const int MaxCategories = 11;

    private static readonly string SettingsFolder = Lyracist.Shared.Globals.SettingsDir;

    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "scaryoke_settings.json");

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

    /// <summary>Per-category weight and colour overrides, keyed by category name.</summary>
    public Dictionary<string, CategoryStyle> Styles { get; set; } = [];

    public List<CategoryPreset> Presets { get; set; } = [];

    public bool IsMuted { get; set; }

    public bool AvoidRepeats { get; set; } = true;

    public static ScaryokeSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                // Fallback migration: Check legacy LocalAppData path
                string legacyPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ParoleSoftware",
                    "ScaryokeWheel",
                    "settings.json"
                );
                if (File.Exists(legacyPath))
                {
                    try
                    {
                        Directory.CreateDirectory(SettingsFolder);
                        File.Copy(legacyPath, SettingsPath, true);
                    }
                    catch { }
                }
            }

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

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            if (Categories.Count > MaxCategories)
            {
                Categories = Categories.GetRange(0, MaxCategories);
            }
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best effort
        }
    }
}
