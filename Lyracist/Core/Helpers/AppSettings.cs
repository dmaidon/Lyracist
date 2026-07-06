using System;
using System.IO;
using System.Text.Json;

namespace Lyracist.Core.Helpers;

/// <summary>
/// Lightweight key/value application settings backed by a JSON file
/// at %AppData%\Lyracist\settings.json.
/// </summary>
public static class AppSettings
{
    private static readonly string _settingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyracist");

    private static readonly string _settingsPath =
        Path.Combine(_settingsDir, "settings.json");

    private static SettingsData _data = Load();

    private static SettingsData Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
        }
        catch { /* Use defaults on any parse error */ }
        return new SettingsData();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(_settingsDir);
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { /* Best-effort; non-critical */ }
    }

    // ─── Settings Properties ───────────────────────────────────────────────

    public static bool ShowSplashOnStartup
    {
        get => _data.ShowSplashOnStartup;
        set { _data.ShowSplashOnStartup = value; Save(); }
    }

    public static bool IsTestMode
    {
        get => _data.IsTestMode;
        set { _data.IsTestMode = value; Save(); }
    }

    public static string YouTubeApiKey
    {
        get => _data.YouTubeApiKey;
        set { _data.YouTubeApiKey = value; Save(); }
    }

    public static string SpotifyClientId
    {
        get => _data.SpotifyClientId;
        set { _data.SpotifyClientId = value; Save(); }
    }

    public static string SpotifyClientSecret
    {
        get => _data.SpotifyClientSecret;
        set { _data.SpotifyClientSecret = value; Save(); }
    }

    public static string AmazonAccessKey
    {
        get => _data.AmazonAccessKey;
        set { _data.AmazonAccessKey = value; Save(); }
    }

    public static string AmazonSecretKey
    {
        get => _data.AmazonSecretKey;
        set { _data.AmazonSecretKey = value; Save(); }
    }

    public static string PartyTymeClientId
    {
        get => _data.PartyTymeClientId;
        set { _data.PartyTymeClientId = value; Save(); }
    }

    public static string PartyTymeClientSecret
    {
        get => _data.PartyTymeClientSecret;
        set { _data.PartyTymeClientSecret = value; Save(); }
    }

    public static int OpeningVolume
    {
        get => _data.OpeningVolume;
        set { _data.OpeningVolume = Math.Clamp(value, 0, 100); Save(); }
    }

    public static int FillInVolume
    {
        get => _data.FillInVolume;
        set { _data.FillInVolume = Math.Clamp(value, 0, 100); Save(); }
    }

    public static int EndRotationVolume
    {
        get => _data.EndRotationVolume;
        set { _data.EndRotationVolume = Math.Clamp(value, 0, 100); Save(); }
    }

    public static double OpeningBass
    {
        get => _data.OpeningBass;
        set { _data.OpeningBass = Math.Clamp(value, -20, 20); Save(); }
    }

    public static double OpeningTreble
    {
        get => _data.OpeningTreble;
        set { _data.OpeningTreble = Math.Clamp(value, -20, 20); Save(); }
    }

    public static double FillInBass
    {
        get => _data.FillInBass;
        set { _data.FillInBass = Math.Clamp(value, -20, 20); Save(); }
    }

    public static double FillInTreble
    {
        get => _data.FillInTreble;
        set { _data.FillInTreble = Math.Clamp(value, -20, 20); Save(); }
    }

    public static double EndRotationBass
    {
        get => _data.EndRotationBass;
        set { _data.EndRotationBass = Math.Clamp(value, -20, 20); Save(); }
    }

    public static double EndRotationTreble
    {
        get => _data.EndRotationTreble;
        set { _data.EndRotationTreble = Math.Clamp(value, -20, 20); Save(); }
    }

    // ─── Scaryoke Categories ──────────────────────────────────────────────────

    public static System.Collections.Generic.IReadOnlyList<string> ScaryokeCategories =>
        _data.ScaryokeCategories.AsReadOnly();

    public static void AddScaryokeCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return;
        if (_data.ScaryokeCategories.Count >= 12) return;
        if (!_data.ScaryokeCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
        {
            _data.ScaryokeCategories.Add(category);
            Save();
        }
    }

    public static void RemoveScaryokeCategory(string category)
    {
        _data.ScaryokeCategories.Remove(category);
        Save();
    }

    // ─── Library Scan Directories ──────────────────────────────────────────

    public static IReadOnlyList<string> LibraryDirectories =>
        _data.LibraryDirectories.AsReadOnly();

    public static void AddLibraryDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!_data.LibraryDirectories.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            _data.LibraryDirectories.Add(path);
            Save();
        }
    }

    public static void RemoveLibraryDirectory(string path)
    {
        _data.LibraryDirectories.RemoveAll(d =>
            string.Equals(d, path, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    // ─── Data Model ────────────────────────────────────────────────────────

    private sealed class SettingsData
    {
        public bool ShowSplashOnStartup { get; set; } = true;
        public bool IsTestMode { get; set; } = false;

        public string YouTubeApiKey { get; set; } = string.Empty;
        public string SpotifyClientId { get; set; } = string.Empty;
        public string SpotifyClientSecret { get; set; } = string.Empty;
        public string AmazonAccessKey { get; set; } = string.Empty;
        public string AmazonSecretKey { get; set; } = string.Empty;
        public string PartyTymeClientId { get; set; } = string.Empty;
        public string PartyTymeClientSecret { get; set; } = string.Empty;

        // Channel volumes (0–100)
        public int OpeningVolume { get; set; } = 80;
        public int FillInVolume { get; set; } = 70;
        public int EndRotationVolume { get; set; } = 80;

        // Channel tone (–20 to +20 dB)
        public double OpeningBass { get; set; } = 0;
        public double OpeningTreble { get; set; } = 0;
        public double FillInBass { get; set; } = 0;
        public double FillInTreble { get; set; } = 0;
        public double EndRotationBass { get; set; } = 0;
        public double EndRotationTreble { get; set; } = 0;

        // Library scan roots — persisted so the app can rescan on demand
        public List<string> LibraryDirectories { get; set; } = new();

        // Scaryoke categories - dynamic configuration up to 12 sectors
        public List<string> ScaryokeCategories { get; set; } = new()
        {
            "Gender Bender", "Elvis", "Country", "Rock & Roll", "Pop", "80s Music",
            "70s Music", "60s Oldies", "Singer's Choice", "Spin Again", "DJ's Choice", "Motown"
        };
    }
}
