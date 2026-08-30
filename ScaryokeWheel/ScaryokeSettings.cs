// Edited on Aug 30, 2026 @ 08:26:00 -> Update settings path to Settings/scaryoke_settings.json with legacy fallback migration
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ScaryokeWheel;

public class ScaryokeSettings
{
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
                    if (settings.Categories.Count > 11)
                    {
                        settings.Categories = settings.Categories.GetRange(0, 11);
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

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            if (Categories.Count > 11)
            {
                Categories = Categories.GetRange(0, 11);
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
