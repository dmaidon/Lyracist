// Edited on Oct 1, 2026 @ 08:46:00 -> Use AtomicJsonFile and back up corrupted settings file before defaulting
using System;
using System.IO;
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
                return JsonSerializer.Deserialize<KnockoutSettings>(json, JsonOptions) ?? new KnockoutSettings();
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
