// Edited on Aug 30, 2026 @ 10:58:00 -> Resolve consolidated paths using AppDomain.CurrentDomain.BaseDirectory for MAUI cross-platform compatibility
using System;
using System.IO;

namespace Lyracist.Trivia.Core.Services;

public static class TriviaStorageHelper
{
    private static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;

    public static string GetTriviaDataDirectory()
    {
        string dir = Path.Combine(BaseDir, "Data");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static string GetDatabasePath() => Path.Combine(GetTriviaDataDirectory(), "trivia.db");

    public static string GetPacksDirectory()
    {
        string dir = Path.Combine(BaseDir, "Packs");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static string GetBannersDirectory()
    {
        string dir = Path.Combine(BaseDir, "Banners", "LyracistTrivia", "CategoryBanners");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static string GetSettingsPath()
    {
        string dir = Path.Combine(BaseDir, "Settings");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return Path.Combine(dir, "lyracist_trivia_settings.json");
    }

    public static Models.TriviaSettings LoadSettings()
    {
        try
        {
            string path = GetSettingsPath();
            if (!File.Exists(path))
            {
                // Fallback migration: Check legacy TriviaData/trivia_settings.json or Settings/trivia_settings.json
                string legacyInTriviaData = Path.Combine(BaseDir, "TriviaData", "trivia_settings.json");
                string settingsDir = Path.Combine(BaseDir, "Settings");
                string legacyInSettings = Path.Combine(settingsDir, "trivia_settings.json");
                if (File.Exists(legacyInTriviaData))
                {
                    try
                    {
                        Directory.CreateDirectory(settingsDir);
                        File.Copy(legacyInTriviaData, path, true);
                    }
                    catch { }
                }
                else if (File.Exists(legacyInSettings))
                {
                    try
                    {
                        File.Copy(legacyInSettings, path, true);
                    }
                    catch { }
                }
            }

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
            // No per-app logger available here (this library is shared across several host apps
            // with different names) - Trace.TraceError still isn't Debug-only, unlike Debug.WriteLine.
            System.Diagnostics.Trace.TraceError($"Error loading trivia settings: {ex}");
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
            System.Diagnostics.Trace.TraceError($"Error saving trivia settings: {ex}");
        }
    }

    private static readonly System.Collections.Generic.Dictionary<string, string> BannerFileNameMappings = new(StringComparer.OrdinalIgnoreCase)
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

    public static string? GetBannerPathForPack(string? packIdOrCategory)
    {
        if (string.IsNullOrWhiteSpace(packIdOrCategory)) return null;

        string bannersDir = GetBannersDirectory();
        string clean = packIdOrCategory.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_").Replace("&", "and");

        if (BannerFileNameMappings.TryGetValue(clean, out var fileName))
        {
            string full = Path.Combine(bannersDir, fileName);
            if (File.Exists(full)) return full;
        }

        string candidate = Path.Combine(bannersDir, $"{clean}.png");
        if (File.Exists(candidate)) return candidate;

        return null;
    }
}
