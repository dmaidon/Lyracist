// Created on Aug 27, 2026 @ 14:36:10 -> IConfigService and ConfigService for persisting and loading game settings
using System;
using System.IO;
using System.Text.Json;
using KnockoutTrivia.Models;

namespace KnockoutTrivia.Services;

public interface IConfigService
{
    KnockoutSettings LoadSettings();
    void SaveSettings(KnockoutSettings settings);
}

public class ConfigService : IConfigService
{
    private static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KoTrivia", "Config");
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "game_settings.json");

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
