// Edited on Oct 24, 2026 @ 14:00:00 -> Sanitized loading, corrupt-file quarantine, sliver size, Singer's Choice toggle, volume, window bounds
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ScaryokeWheel;

public class CategoryStyle
{
    public double Weight { get; set; } = 1;
    public string? Color { get; set; }
    public bool Enabled { get; set; } = true;
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
    public const int MaxNameLength = 30;

    /// <summary>Names the wheel adds itself, so a user category may not use them.</summary>
    public static readonly string[] ReservedNames = ["Singer's Choice", "DJ's Choice"];

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

    /// <summary>Per-category weight, colour and enabled overrides, keyed by category name.</summary>
    public Dictionary<string, CategoryStyle> Styles { get; set; } = [];

    public List<CategoryPreset> Presets { get; set; } = [];

    public bool IsMuted { get; set; }

    public bool AvoidRepeats { get; set; } = true;

    /// <summary>Master volume for generated sounds and the laugh clips (0-1).</summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>Weight of each thin DJ's Choice sliver relative to a regular segment.</summary>
    public double SliverWeight { get; set; } = 0.15;

    public bool ShowSingersChoice { get; set; } = true;

    // Last window placement (null until the window has been closed once)
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }

    /// <summary>Trims, drops blank/reserved/duplicate names and clamps to the category limit.</summary>
    public static List<string> CleanCategories(IEnumerable<string?> names) =>
    [
        .. names
            .Select(n => n?.Trim() ?? string.Empty)
            .Where(n => n.Length > 0 && n.Length <= MaxNameLength)
            .Where(n => !ReservedNames.Contains(n, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxCategories)
    ];

    private void Sanitize()
    {
        Categories = CleanCategories(Categories ?? []);
        Styles = (Styles ?? [])
            .Where(kv => kv.Value != null && Categories.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => new CategoryStyle
            {
                Weight = Math.Clamp(kv.Value.Weight, 1, 3),
                Color = kv.Value.Color,
                Enabled = kv.Value.Enabled
            });
        Presets = [.. (Presets ?? []).Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name))];
        foreach (var preset in Presets)
        {
            preset.Categories = CleanCategories(preset.Categories ?? []);
            preset.Styles ??= [];
        }
        Volume = Math.Clamp(Volume, 0, 1);
        SliverWeight = Math.Clamp(SliverWeight, 0.05, 0.6);
    }

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
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Scaryoke settings migration failed: {ex.Message}");
                    }
                }
            }

            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                try
                {
                    var settings = JsonSerializer.Deserialize<ScaryokeSettings>(json);
                    if (settings != null)
                    {
                        settings.Sanitize();
                        return settings;
                    }
                }
                catch (JsonException ex)
                {
                    // Keep the unreadable file so the next Save() doesn't wipe it
                    Debug.WriteLine($"Scaryoke settings are corrupt, quarantining: {ex.Message}");
                    try
                    {
                        File.Move(SettingsPath, SettingsPath + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}", true);
                    }
                    catch (Exception moveEx)
                    {
                        Debug.WriteLine($"Could not quarantine settings: {moveEx.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Scaryoke settings load failed, using defaults: {ex.Message}");
        }

        return new ScaryokeSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            Categories = CleanCategories(Categories);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Scaryoke settings save failed: {ex.Message}");
        }
    }
}
