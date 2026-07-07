using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    // Guards _data mutation and the settings.json read/write — this class is touched
    // from background scan threads (AddLibraryDirectory) as well as the UI thread,
    // so concurrent Save() calls could otherwise interleave and corrupt the file,
    // and concurrent check-then-add list mutations could race and duplicate entries.
    private static readonly object _lock = new();

    private static SettingsData _data = Load();

    private static SettingsData Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var data = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();

                // Clean up loaded categories to enforce the new 8-category limit
                if (data.ScaryokeCategories != null)
                {
                    data.ScaryokeCategories = data.ScaryokeCategories
                        .Where(c => !string.Equals(c, "Singer's Choice", StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(c, "DJ's Choice", StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(8)
                        .ToList();
                }

                return data;
            }
        }
        catch { /* Use defaults on any parse error */ }
        return new SettingsData();
    }

    private static void Save()
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(_settingsDir);
                var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { /* Best-effort; non-critical */ }
        }
    }

    // ─── Settings Properties ───────────────────────────────────────────────

    public static bool ShowSplashOnStartup
    {
        get => _data.ShowSplashOnStartup;
        set { _data.ShowSplashOnStartup = value; Save(); }
    }

    public static bool EnableHardwareAcceleration
    {
        get => _data.EnableHardwareAcceleration;
        set { _data.EnableHardwareAcceleration = value; Save(); }
    }

    public static bool EnableNoiseGate
    {
        get => _data.EnableNoiseGate;
        set { _data.EnableNoiseGate = value; Save(); }
    }

    public static bool EnableReverb
    {
        get => _data.EnableReverb;
        set { _data.EnableReverb = value; Save(); }
    }

    public static int SelectedBufferSize
    {
        get => _data.SelectedBufferSize;
        set { _data.SelectedBufferSize = value; Save(); }
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

    public static System.Collections.Generic.IReadOnlyList<string> ScaryokeCategories
    {
        get { lock (_lock) { return _data.ScaryokeCategories.ToList(); } }
    }

    public static void AddScaryokeCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return;

        lock (_lock)
        {
            if (_data.ScaryokeCategories.Count >= 8) return;
            if (!_data.ScaryokeCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
            {
                _data.ScaryokeCategories.Add(category);
                Save();
            }
        }
    }

    public static void RemoveScaryokeCategory(string category)
    {
        lock (_lock)
        {
            _data.ScaryokeCategories.Remove(category);
            Save();
        }
    }

    // ─── Venues & DJ ─────────────────────────────────────────────────────────

    public static string DjName
    {
        get { lock (_lock) { return _data.DjName; } }
        set { lock (_lock) { _data.DjName = value; Save(); } }
    }

    public static string SelectedVenue
    {
        get { lock (_lock) { return _data.SelectedVenue; } }
        set { lock (_lock) { _data.SelectedVenue = value; Save(); } }
    }

    public static string CrawlBannerType
    {
        get { lock (_lock) { return _data.CrawlBannerType; } }
        set { lock (_lock) { _data.CrawlBannerType = value; Save(); } }
    }

    public static string CrawlBannerCustomText
    {
        get { lock (_lock) { return _data.CrawlBannerCustomText; } }
        set { lock (_lock) { _data.CrawlBannerCustomText = value; Save(); } }
    }

    public static int CrawlSpaceshipFontSize
    {
        get { lock (_lock) { return _data.CrawlSpaceshipFontSize; } }
        set { lock (_lock) { _data.CrawlSpaceshipFontSize = value; Save(); } }
    }

    public static int CrawlSpaceshipDuration
    {
        get { lock (_lock) { return _data.CrawlSpaceshipDuration; } }
        set { lock (_lock) { _data.CrawlSpaceshipDuration = value; Save(); } }
    }

    public static int CrawlSpaceshipFrequency
    {
        get { lock (_lock) { return _data.CrawlSpaceshipFrequency; } }
        set { lock (_lock) { _data.CrawlSpaceshipFrequency = value; Save(); } }
    }

    public static System.Collections.Generic.List<Lyracist.Models.SpaceshipSnippet> CrawlSpaceshipSnippets
    {
        get { lock (_lock) { return _data.CrawlSpaceshipSnippets; } }
        set { lock (_lock) { _data.CrawlSpaceshipSnippets = value; Save(); } }
    }

    public static void SaveSpaceshipSnippets()
    {
        lock (_lock) { Save(); }
    }

    public static string GetActiveCrawlBannerTemplate()
    {
        lock (_lock)
        {
            return _data.CrawlBannerType switch
            {
                "Dramatic" => "In a tavern far, far away, known only as {venue}, the patrons have risen in glorious rebellion — and under the wicked command of their sinister DJ, {dj}, they have chosen their ultimate weapon… karaoke.",
                "Comedic" => "Somewhere in the distant reaches of the galaxy, inside a questionable establishment called {venue}, the patrons have staged a full‑blown uprising. Led by their diabolical DJ, {dj}, they now march toward their destiny: screaming karaoke like it’s a battle cry.",
                "Over-the-Top" => "In a tavern lost to time and space — a place whispered about only as {venue} — the patrons have revolted. Guided by the dark influence of DJ {dj}, they embark on a quest of unimaginable terror… karaoke night.",
                _ => _data.CrawlBannerCustomText
            };
        }
    }

    public static System.Collections.Generic.IReadOnlyList<string> Venues
    {
        get { lock (_lock) { return _data.Venues.ToList(); } }
    }

    public static void AddVenue(string venue)
    {
        if (string.IsNullOrWhiteSpace(venue)) return;

        lock (_lock)
        {
            if (!_data.Venues.Contains(venue, StringComparer.OrdinalIgnoreCase))
            {
                _data.Venues.Add(venue);
                Save();
            }
        }
    }

    public static void RemoveVenue(string venue)
    {
        lock (_lock)
        {
            _data.Venues.Remove(venue);
            if (string.Equals(_data.SelectedVenue, venue, StringComparison.OrdinalIgnoreCase))
            {
                _data.SelectedVenue = _data.Venues.FirstOrDefault() ?? string.Empty;
            }
            Save();
        }
    }

    // ─── Library Scan Directories ──────────────────────────────────────────

    public static IReadOnlyList<string> LibraryDirectories
    {
        get { lock (_lock) { return _data.LibraryDirectories.ToList(); } }
    }

    public static void AddLibraryDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        lock (_lock)
        {
            if (!_data.LibraryDirectories.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                _data.LibraryDirectories.Add(path);
                Save();
            }
        }
    }

    public static void RemoveLibraryDirectory(string path)
    {
        lock (_lock)
        {
            _data.LibraryDirectories.RemoveAll(d =>
                string.Equals(d, path, StringComparison.OrdinalIgnoreCase));
            Save();
        }
    }

    // ─── Data Model ────────────────────────────────────────────────────────

    private sealed class SettingsData
    {
        public bool ShowSplashOnStartup { get; set; } = true;
        public bool EnableHardwareAcceleration { get; set; } = true;
        public bool EnableNoiseGate { get; set; } = false;
        public bool EnableReverb { get; set; } = false;
        public int SelectedBufferSize { get; set; } = 256;
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

        // Scaryoke categories - dynamic configuration up to 8 sectors
        public List<string> ScaryokeCategories { get; set; } = new()
        {
            "Gender Bender", "Elvis", "Country", "Rock & Roll", "Pop", "80s Music",
            "60s Oldies", "Motown"
        };

        // Venues & DJ name
        public string DjName { get; set; } = "DJ Karaoke";
        public List<string> Venues { get; set; } = new() { "The Tavern", "The Stage", "The Pub" };
        public string SelectedVenue { get; set; } = "The Tavern";

        // Crawl banner type and custom text
        public string CrawlBannerType { get; set; } = "Dramatic";
        public string CrawlBannerCustomText { get; set; } = "Enter custom crawl text here...";

        // Spaceship overlay configurations
        public int CrawlSpaceshipFontSize { get; set; } = 26;
        public int CrawlSpaceshipDuration { get; set; } = 13;
        public int CrawlSpaceshipFrequency { get; set; } = 60;
        public List<Lyracist.Models.SpaceshipSnippet> CrawlSpaceshipSnippets { get; set; } = new()
        {
            new() { Text = "Tip your bartender! 🍹", IsEnabled = true },
            new() { Text = "Tip your servers! 💸", IsEnabled = true },
            new() { Text = "Buy a drink at the bar! 🍺", IsEnabled = true },
            new() { Text = "Keep the queue moving, request a song! 🎤", IsEnabled = true },
            new() { Text = "Remember to tip the staff! 💵", IsEnabled = true },
            new() { Text = "Scaryoke party mode active! 🎡", IsEnabled = true }
        };
    }
}
