// Edited on Aug 30, 2026 @ 08:26:00 -> Update ConfigFile to Settings/knockout_trivia_settings.json with legacy game_settings.json migration
using System;
using System.IO;
using System.Text.Json;
using KnockoutTrivia.Models;
using Lyracist.Shared;

namespace KnockoutTrivia.Services;

public interface IConfigService
{
    KnockoutSettings LoadSettings();
    void SaveSettings(KnockoutSettings settings);
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
        catch
        {
            // Fallback to default
        }

        var defaults = new KnockoutSettings();
        SaveSettings(defaults);
        return defaults;
    }

    public void SaveSettings(KnockoutSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(ConfigFile, json);
        }
        catch
        {
            // Ignore write errors in stub
        }
    }
}
