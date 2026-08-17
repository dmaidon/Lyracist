// Edited on Aug 17, 2026 @ 15:47:30 -> Added LoadSettings and SaveSettings methods to TriviaStorageHelper
using System;
using System.IO;

namespace Lyracist.Trivia.Core.Services;

public static class TriviaStorageHelper
{
    private static string? _cachedTriviaDataPath;

    public static string GetTriviaDataDirectory()
    {
        if (_cachedTriviaDataPath != null && Directory.Exists(_cachedTriviaDataPath))
        {
            return _cachedTriviaDataPath;
        }

        // 1. Direct standard solution path
        const string standardPath = @"C:\VB26\Lyracist\TriviaData";
        if (Directory.Exists(standardPath))
        {
            _cachedTriviaDataPath = standardPath;
            return standardPath;
        }

        // 2. Search upwards for Lyracist solution root or TriviaData folder
        string? current = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 7 && current != null; i++)
        {
            string candidateTrivia = Path.Combine(current, "TriviaData");
            if (Directory.Exists(candidateTrivia))
            {
                _cachedTriviaDataPath = Path.GetFullPath(candidateTrivia);
                return _cachedTriviaDataPath;
            }

            string lyracistTrivia = Path.Combine(current, "Lyracist", "TriviaData");
            if (Directory.Exists(lyracistTrivia))
            {
                _cachedTriviaDataPath = Path.GetFullPath(lyracistTrivia);
                return _cachedTriviaDataPath;
            }

            // Check if solution file is in current directory
            if (File.Exists(Path.Combine(current, "Lyracist.slnx")) || File.Exists(Path.Combine(current, "Lyracist.sln")))
            {
                string slnData = Path.Combine(current, "TriviaData");
                if (!Directory.Exists(slnData))
                {
                    Directory.CreateDirectory(slnData);
                }
                _cachedTriviaDataPath = Path.GetFullPath(slnData);
                return _cachedTriviaDataPath;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        // 3. Fallback to app directory TriviaData
        string localFallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TriviaData");
        if (!Directory.Exists(localFallback))
        {
            Directory.CreateDirectory(localFallback);
        }
        _cachedTriviaDataPath = Path.GetFullPath(localFallback);
        return _cachedTriviaDataPath;
    }

    public static string GetDatabasePath()
    {
        string dir = GetTriviaDataDirectory();
        return Path.Combine(dir, "trivia.db");
    }

    public static string GetPacksDirectory()
    {
        string dir = Path.Combine(GetTriviaDataDirectory(), "packs");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static string GetSettingsPath()
    {
        string dir = GetTriviaDataDirectory();
        return Path.Combine(dir, "trivia_settings.json");
    }

    public static Models.TriviaSettings LoadSettings()
    {
        try
        {
            string path = GetSettingsPath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<Models.TriviaSettings>(json, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (loaded != null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading trivia settings: {ex.Message}");
        }

        return new Models.TriviaSettings();
    }

    public static void SaveSettings(Models.TriviaSettings settings)
    {
        try
        {
            string path = GetSettingsPath();
            string json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving trivia settings: {ex.Message}");
        }
    }
}
