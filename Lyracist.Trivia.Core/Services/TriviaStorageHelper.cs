// Edited on Aug 19, 2026 @ 09:42:00 -> Added GetBannerPathForPack helper method to TriviaStorageHelper
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

    public static string GetBannersDirectory()
    {
        string dir = Path.Combine(GetTriviaDataDirectory(), "Banners");
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

    public static string? GetBannerPathForPack(string? packIdOrCategory)
    {
        if (string.IsNullOrWhiteSpace(packIdOrCategory)) return null;

        string bannersDir = GetBannersDirectory();
        string clean = packIdOrCategory.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_").Replace("&", "and");

        System.Collections.Generic.Dictionary<string, string> mappings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["biker_trivia"] = "biker_trivia.png",
            ["biker-trivia"] = "biker_trivia.png",
            ["bikers_and_motorcycles"] = "biker_trivia.png",
            ["bikers_motorcycles"] = "biker_trivia.png",
            ["rock_and_roll"] = "rock_and_roll.png",
            ["rock-and-roll"] = "rock_and_roll.png",
            ["rock_roll"] = "rock_and_roll.png",
            ["country_music"] = "country_music.png",
            ["country-music"] = "country_music.png",
            ["geography"] = "geography.png",
            ["world_geography"] = "geography.png",
            ["state_capitals"] = "state_capitals.png",
            ["state-capitals"] = "state_capitals.png",
            ["history"] = "history.png",
            ["world_history"] = "history.png",
            ["complete_the_lyric"] = "complete_the_lyric.png",
            ["complete-the-lyric"] = "complete_the_lyric.png",
            ["tv_shows"] = "tv_shows.png",
            ["tv-shows"] = "tv_shows.png",
            ["sports"] = "sports.png",
            ["sports_and_athletes"] = "sports.png",
            ["logos_and_slogans"] = "logos_and_slogans.png",
            ["logos-and-slogans"] = "logos_and_slogans.png",
            ["logos_slogans"] = "logos_and_slogans.png",
            ["music_legends"] = "music_legends.png",
            ["music-legends"] = "music_legends.png",
            ["pop_culture_80s_90s"] = "pop_culture_80s_90s.png",
            ["pop-culture-80s-90s"] = "pop_culture_80s_90s.png",
            ["80s_and_90s_pop_culture"] = "pop_culture_80s_90s.png",
            ["80s_90s_pop_culture"] = "pop_culture_80s_90s.png",
            ["movie_soundtracks"] = "movie_soundtracks.png",
            ["movie-soundtracks"] = "movie_soundtracks.png",
            ["pub_general_knowledge"] = "pub_general_knowledge.png",
            ["pub-general-knowledge"] = "pub_general_knowledge.png",
            ["pub_trivia_all_stars"] = "pub_general_knowledge.png",
            ["pub-trivia-all-stars"] = "pub_general_knowledge.png"
        };

        if (mappings.TryGetValue(clean, out var fileName))
        {
            string full = Path.Combine(bannersDir, fileName);
            if (File.Exists(full)) return full;
        }

        string candidate = Path.Combine(bannersDir, $"{clean}.png");
        if (File.Exists(candidate)) return candidate;

        return null;
    }
}
