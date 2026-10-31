// Edited on Oct 31, 2026 @ 11:21:00 -> Strip non-trivia databases (lyracist.db) from SelectedSourcePaths when loading settings
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using KnockoutTrivia.Models;
using Lyracist.Shared;

namespace KnockoutTrivia.Services;

public interface IConfigService
{
    KnockoutSettings LoadSettings();
    bool SaveSettings(KnockoutSettings settings);
}

public class ConfigService : IConfigService
{
    private static readonly string ConfigDir = Globals.SettingsDir;
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "knockout_trivia_settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigService()
    {
        Directory.CreateDirectory(ConfigDir);
    }

    public KnockoutSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(ConfigFile))
            {
                // Fallback migration: Check legacy KoTrivia/Config/game_settings.json or Settings/game_settings.json
                string legacyInKoTrivia = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KoTrivia", "Config", "game_settings.json");
                string legacyInSettings = Path.Combine(ConfigDir, "game_settings.json");
                if (File.Exists(legacyInKoTrivia))
                {
                    Directory.CreateDirectory(ConfigDir);
                    File.Copy(legacyInKoTrivia, ConfigFile, true);
                }
                else if (File.Exists(legacyInSettings))
                {
                    File.Copy(legacyInSettings, ConfigFile, true);
                }
            }

            if (File.Exists(ConfigFile))
            {
                string json = File.ReadAllText(ConfigFile);
                var loaded = JsonSerializer.Deserialize<KnockoutSettings>(json, JsonOptions) ?? new KnockoutSettings();
                if (loaded.SelectedSourcePaths != null && loaded.SelectedSourcePaths.Count > 0)
                {
                    int before = loaded.SelectedSourcePaths.Count;
                    loaded.SelectedSourcePaths = loaded.SelectedSourcePaths
                        .Where(p => !TriviaDataService.IsExcludedNonTriviaDatabase(p))
                        .ToList();
                    if (loaded.SelectedSourcePaths.Count != before)
                    {
                        SaveSettings(loaded);
                    }
                }
                return loaded;
            }
        }
        catch (Exception ex)
        {
            Globals.LogError("KnockoutTrivia", "ConfigService.LoadSettings", ex);
            try
            {
                if (File.Exists(ConfigFile))
                {
                    string backupPath = $"{ConfigFile}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
                    File.Copy(ConfigFile, backupPath, true);
                }
            }
            catch { }
        }

        var defaults = new KnockoutSettings();
        SaveSettings(defaults);
        return defaults;
    }

    public bool SaveSettings(KnockoutSettings settings)
    {
        try
        {
            AtomicJsonFile.Serialize(ConfigFile, settings, JsonOptions);
            return true;
        }
        catch (Exception ex)
        {
            Globals.LogError("KnockoutTrivia", "ConfigService.SaveSettings", ex);
            return false;
        }
    }
}
