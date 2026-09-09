// Edited on Sep 9, 2026 @ 16:30:00 -> Add MasterOutputBoostDb and EnableAudioLimiter for USB mixer gain staging
using System;
using System.IO;
using System.Text.Json;
using Lyracist.Shared;

namespace Lyracist.Core.Helpers;

/// <summary>
/// Lightweight key/value application settings backed by a JSON file
/// at Settings\lyracist_settings.json.
/// </summary>
public static class AppSettings
{
    private static readonly string _settingsDir = Lyracist.Shared.Globals.SettingsDir;

    private static readonly string _settingsPath =
        Path.Combine(_settingsDir, "lyracist_settings.json");

    // Guards _data mutation and the settings.json read/write — this class is touched
    // from background scan threads (AddLibraryDirectory) as well as the UI thread,
    // so concurrent Save() calls could otherwise interleave and corrupt the file,
    // and concurrent check-then-add list mutations could race and duplicate entries.
    private static readonly Lock _lock = new();

    // Every property setter calls Save(), and several settings (e.g. slider-bound volume/EQ
    // values) fire on every UI value change, not just when the user finishes dragging. Writing
    // straight to disk there was a full fsync-and-rename per pixel of slider movement — a plain
    // System.Threading.Timer (not DispatcherTimer) is used because this class is also touched
    // from background threads with no dispatcher. Coalesces bursts into one write.
    private const int SaveDebounceMs = 400;
    private static readonly System.Threading.Timer _saveTimer = new(_ => FlushIfDirty(), null, Timeout.Infinite, Timeout.Infinite);
    private static bool _dirty;

    private static readonly SettingsData _data = Load();

    private static SettingsData Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                // Fallback migration: Check legacy %AppData%\Lyracist\settings.json
                string legacyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyracist", "settings.json");
                if (File.Exists(legacyPath))
                {
                    Directory.CreateDirectory(_settingsDir);
                    File.Copy(legacyPath, _settingsPath, true);
                }
            }

            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                SettingsData data;
                try
                {
                    data = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
                }
                catch (JsonException ex)
                {
                    // Quarantine the corrupt file instead of silently falling through to defaults
                    // - the next Save() would otherwise overwrite it with those defaults, and a DJ
                    // whose settings.json got truncated (e.g. a crash mid-write, or hand-editing)
                    // would lose every preference with no message and nothing left to recover.
                    Lyracist.Shared.Globals.LogError("Lyracist", "AppSettings: settings file is corrupt, quarantining and using defaults", ex);
                    try
                    {
                        string quarantinePath = _settingsPath + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
                        File.Move(_settingsPath, quarantinePath, overwrite: true);
                    }
                    catch { /* Best-effort; the original file is left in place if the move fails */ }

                    return new SettingsData();
                }

                // Clean up loaded categories to enforce the new 8-category limit
                if (data.ScaryokeCategories != null)
                {
                    data.ScaryokeCategories = [.. data.ScaryokeCategories
                        .Where(c => !string.Equals(c, "Singer's Choice", StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(c, "DJ's Choice", StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(8)];
                }

                return data;
            }
        }
        catch { /* I/O error (permissions, locked file, etc.) - use defaults without touching the file */ }
        return new SettingsData();
    }

    private static void Save()
    {
        lock (_lock)
        {
            _dirty = true;
        }
        _saveTimer.Change(SaveDebounceMs, Timeout.Infinite);
    }

    private static void FlushIfDirty()
    {
        lock (_lock)
        {
            if (!_dirty) return;
            try
            {
                Lyracist.Shared.AtomicJsonFile.Serialize(_settingsPath, _data, new JsonSerializerOptions { WriteIndented = true });
            }
            catch { /* Best-effort; non-critical */ }
            _dirty = false;
        }
    }

    /// <summary>Forces any pending debounced save to write immediately. Call this on app shutdown -
    /// otherwise a change made in the last &lt;<see cref="SaveDebounceMs"/>ms before exit (e.g. a
    /// slider release right before closing the window) is lost when the process ends before the
    /// debounce timer fires.</summary>
    public static void Flush()
    {
        _saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        FlushIfDirty();
    }

    // ─── Settings Properties ───────────────────────────────────────────────

    public static event System.Action<string>? ThemeModeChanged;

    public static string ThemeMode
    {
        get => _data.ThemeMode;
        set
        {
            if (_data.ThemeMode != value)
            {
                _data.ThemeMode = value;
                Save();
                ThemeModeChanged?.Invoke(value);
            }
        }
    }

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
        get => Lyracist.Shared.EncryptionHelper.Decrypt(_data.YouTubeApiKey);
        set { _data.YouTubeApiKey = Lyracist.Shared.EncryptionHelper.Encrypt(value); Save(); }
    }

    public static string SpotifyClientId
    {
        get => Lyracist.Shared.EncryptionHelper.Decrypt(_data.SpotifyClientId);
        set { _data.SpotifyClientId = Lyracist.Shared.EncryptionHelper.Encrypt(value); Save(); }
    }

    public static string SpotifyClientSecret
    {
        get => Lyracist.Shared.EncryptionHelper.Decrypt(_data.SpotifyClientSecret);
        set { _data.SpotifyClientSecret = Lyracist.Shared.EncryptionHelper.Encrypt(value); Save(); }
    }

    public static string AmazonAccessKey
    {
        get => Lyracist.Shared.EncryptionHelper.Decrypt(_data.AmazonAccessKey);
        set { _data.AmazonAccessKey = Lyracist.Shared.EncryptionHelper.Encrypt(value); Save(); }
    }

    public static string AmazonSecretKey
    {
        get => Lyracist.Shared.EncryptionHelper.Decrypt(_data.AmazonSecretKey);
        set { _data.AmazonSecretKey = Lyracist.Shared.EncryptionHelper.Encrypt(value); Save(); }
    }

    public static bool EnableAutoAdvance
    {
        get => _data.EnableAutoAdvance;
        set { _data.EnableAutoAdvance = value; Save(); }
    }

    public static int AutoAdvanceCountdownSeconds
    {
        get => _data.AutoAdvanceCountdownSeconds;
        set { _data.AutoAdvanceCountdownSeconds = value; Save(); }
    }

    public static string StaticIPAddress
    {
        get => _data.StaticIPAddress;
        set { _data.StaticIPAddress = value; Save(); }
    }

    public static int TabletPort
    {
        get => _data.TabletPort;
        set { _data.TabletPort = value; Save(); }
    }

    public static bool KSRotationSyncEnabled
    {
        get => _data.KSRotationSyncEnabled;
        set { _data.KSRotationSyncEnabled = value; Save(); }
    }

    public static string KSRotationIpAddress
    {
        get => _data.KSRotationIpAddress;
        set { _data.KSRotationIpAddress = value ?? "127.0.0.1"; Save(); }
    }

    public static int KSRotationPort
    {
        get => _data.KSRotationPort;
        set { _data.KSRotationPort = value; Save(); }
    }

    public static int KSRotationSyncIntervalSeconds
    {
        get => _data.KSRotationSyncIntervalSeconds;
        set { _data.KSRotationSyncIntervalSeconds = value; Save(); }
    }

    public static string GetLocalIPAddress()
    {
        try
        {
            foreach (var netInterface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (netInterface.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                    (netInterface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 ||
                     netInterface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet))
                {
                    foreach (var ip in netInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            var ipStr = ip.Address.ToString();
                            if (!ipStr.StartsWith("127.") && !ipStr.StartsWith("169.254"))
                            {
                                return ipStr;
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return "127.0.0.1";
    }

    public static string GetActiveIPAddress()
    {
        if (!string.IsNullOrWhiteSpace(StaticIPAddress))
        {
            return StaticIPAddress.Trim();
        }
        return GetLocalIPAddress();
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

    public static event System.Action? VenueOrDjChanged;

    public static string DjName
    {
        get { lock (_lock) { return _data.DjName; } }
        set
        {
            lock (_lock)
            {
                if (_data.DjName != value)
                {
                    _data.DjName = value;
                    Save();
                    VenueOrDjChanged?.Invoke();
                }
            }
        }
    }

    public static string SelectedVenue
    {
        get { lock (_lock) { return _data.SelectedVenue; } }
        set
        {
            lock (_lock)
            {
                if (_data.SelectedVenue != value)
                {
                    _data.SelectedVenue = value;
                    Save();
                    VenueOrDjChanged?.Invoke();
                }
            }
        }
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

    public static System.Collections.Generic.IReadOnlyList<string> GetVenueGraphics(string venue)
    {
        if (string.IsNullOrWhiteSpace(venue)) return [];
        lock (_lock)
        {
            if (_data.VenueGraphics.TryGetValue(venue, out var list))
            {
                return [.. list];
            }
            return [];
        }
    }

    public static void AddVenueGraphic(string venue, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(venue) || string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return;

        try
        {
            string destDir = Path.Combine(_settingsDir, "VenueGraphics", venue);
            Directory.CreateDirectory(destDir);
            string destFileName = Path.GetFileName(sourcePath);
            string destPath = Path.Combine(destDir, destFileName);
            
            // To handle duplicates safely
            int counter = 1;
            while (File.Exists(destPath))
            {
                string ext = Path.GetExtension(destFileName);
                string nameNoExt = Path.GetFileNameWithoutExtension(destFileName);
                destPath = Path.Combine(destDir, $"{nameNoExt}_{counter++}{ext}");
            }

            File.Copy(sourcePath, destPath);

            lock (_lock)
            {
                if (!_data.VenueGraphics.TryGetValue(venue, out var list))
                {
                    list = [];
                    _data.VenueGraphics[venue] = list;
                }
                if (!list.Contains(destPath))
                {
                    list.Add(destPath);
                    Save();
                }
            }
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to copy venue graphic", ex);
        }
    }

    public static void RemoveVenueGraphic(string venue, string path)
    {
        if (string.IsNullOrWhiteSpace(venue) || string.IsNullOrWhiteSpace(path))
            return;

        lock (_lock)
        {
            if (_data.VenueGraphics.TryGetValue(venue, out var list))
            {
                if (list.Remove(path))
                {
                    Save();
                }
            }
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to delete venue graphic file", ex);
        }
    }

    // ─── Requests Settings ─────────────────────────────────────────────────────

    /// <summary>When true, incoming mobile-portal song requests are approved automatically
    /// instead of waiting in the Pending queue for the DJ to approve.</summary>
    public static bool AutoAcceptRequests
    {
        get => _data.AutoAcceptRequests;
        set { _data.AutoAcceptRequests = value; Save(); }
    }

    // ─── Rating System Settings ────────────────────────────────────────────────

    public static bool IsRatingSystemEnabled
    {
        get => _data.IsRatingSystemEnabled;
        set { _data.IsRatingSystemEnabled = value; Save(); }
    }

    public static bool ShowQrCodeOnRotationScreen
    {
        get => _data.ShowQrCodeOnRotationScreen;
        set { _data.ShowQrCodeOnRotationScreen = value; Save(); }
    }

    public static bool ShowQrCodeOnLyricsScreen
    {
        get => _data.ShowQrCodeOnLyricsScreen;
        set { _data.ShowQrCodeOnLyricsScreen = value; Save(); }
    }

    public static string SelectedRatingIcon
    {
        get => _data.SelectedRatingIcon;
        set { _data.SelectedRatingIcon = value; Save(); }
    }

    public static System.Collections.Generic.IReadOnlyList<string> AvailableRatingIcons
    {
        get { lock (_lock) { return _data.AvailableRatingIcons.ToList(); } }
    }

    public static void AddAvailableRatingIcon(string icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return;

        lock (_lock)
        {
            if (!_data.AvailableRatingIcons.Contains(icon, StringComparer.OrdinalIgnoreCase))
            {
                _data.AvailableRatingIcons.Add(icon);
                Save();
            }
        }
    }

    public static void RemoveAvailableRatingIcon(string icon)
    {
        lock (_lock)
        {
            _data.AvailableRatingIcons.Remove(icon);
            if (string.Equals(_data.SelectedRatingIcon, icon, StringComparison.OrdinalIgnoreCase))
            {
                _data.SelectedRatingIcon = _data.AvailableRatingIcons.FirstOrDefault() ?? string.Empty;
            }
            Save();
        }
    }

    public static string ActiveRatingIconSymbol
    {
        get
        {
            string full = SelectedRatingIcon;
            if (string.IsNullOrWhiteSpace(full)) return "⭐";
            var parts = full.Split(' ', 2);
            return parts[0];
        }
    }

    public static int FillInDelaySeconds
    {
        get => _data.FillInDelaySeconds;
        set { _data.FillInDelaySeconds = value; Save(); }
    }

    // ─── CDG Video Backdrop Settings ──────────────────────────────────────────

    public static string CdgBackdropMode
    {
        get => _data.CdgBackdropMode;
        set { _data.CdgBackdropMode = value ?? "Original Color"; Save(); }
    }

    public static bool IsCdgChromaKeyEnabled => CdgBackdropMode != "Original Color";

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

    // ─── Registration Settings ──────────────────────────────────────────────

    public static string RegFirstName
    {
        get => _data.RegFirstName;
        set { _data.RegFirstName = value ?? string.Empty; Save(); }
    }

    public static string RegLastName
    {
        get => _data.RegLastName;
        set { _data.RegLastName = value ?? string.Empty; Save(); }
    }

    public static string RegStageName
    {
        get => _data.RegStageName;
        set { _data.RegStageName = value ?? string.Empty; Save(); }
    }

    public static string RegEmail
    {
        get => _data.RegEmail;
        set { _data.RegEmail = value ?? string.Empty; Save(); }
    }

    public static string RegLicenseKey
    {
        get => _data.RegLicenseKey;
        set { _data.RegLicenseKey = value ?? string.Empty; Save(); }
    }

    public static bool IsRegistered
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_data.RegLicenseKey)) return false;
            return LicenseValidator.ValidateKey(_data.RegFirstName, _data.RegLastName, _data.RegStageName, _data.RegEmail, _data.RegLicenseKey);
        }
    }

    // ─── Hotkeys & KillVocal Settings ────────────────────────────────────────

    public static Dictionary<string, string> Hotkeys
    {
        get
        {
            lock (_lock)
            {
                if (_data.Hotkeys == null || _data.Hotkeys.Count == 0)
                {
                    _data.Hotkeys = GetDefaultHotkeys();
                }
                return new Dictionary<string, string>(_data.Hotkeys, StringComparer.OrdinalIgnoreCase);
            }
        }
        set
        {
            lock (_lock)
            {
                _data.Hotkeys = value ?? GetDefaultHotkeys();
                Save();
            }
        }
    }

    private static Dictionary<string, string> GetDefaultHotkeys()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Space", "PlayPause" },
            { "Return", "DoneSinger" },
            { "Escape", "Stop" },
            { "Back", "ToggleBanner" },
            { "F5", "ToggleLyricsWindow" },
            { "F6", "ToggleRotationWindow" }
        };
    }

    public static bool EnableKillVocal
    {
        get => _data.EnableKillVocal;
        set { _data.EnableKillVocal = value; Save(); }
    }

    // ─── Audio Output Settings ─────────────────────────────────────────────

    public static string SelectedKaraokeAudioDevice
    {
        get => _data.SelectedKaraokeAudioDevice;
        set { _data.SelectedKaraokeAudioDevice = value; Save(); }
    }

    public static string SelectedBgmAudioDevice
    {
        get => _data.SelectedBgmAudioDevice;
        set { _data.SelectedBgmAudioDevice = value; Save(); }
    }

    public static bool IsHardwareMixerMode
    {
        get => _data.IsHardwareMixerMode;
        set { _data.IsHardwareMixerMode = value; Save(); }
    }

    public static bool NormalizeVolumeEnabled
    {
        get => _data.NormalizeVolumeEnabled;
        set { _data.NormalizeVolumeEnabled = value; Save(); }
    }

    public static double TargetLoudnessLufs
    {
        get => _data.TargetLoudnessLufs;
        set { _data.TargetLoudnessLufs = value; Save(); }
    }

    public static double MasterOutputBoostDb
    {
        get => _data.MasterOutputBoostDb;
        set { _data.MasterOutputBoostDb = Math.Clamp(value, 0.0, 12.0); Save(); }
    }

    public static bool EnableAudioLimiter
    {
        get => _data.EnableAudioLimiter;
        set { _data.EnableAudioLimiter = value; Save(); }
    }

    public static bool AutoPlayRotationMusic
    {
        get => _data.AutoPlayRotationMusic;
        set { _data.AutoPlayRotationMusic = value; Save(); }
    }

    public static int RotationMusicDelaySeconds
    {
        get => _data.RotationMusicDelaySeconds;
        set { _data.RotationMusicDelaySeconds = value; Save(); }
    }

    public static string ConnectInstructionsScreen
    {
        get => _data.ConnectInstructionsScreen;
        set { _data.ConnectInstructionsScreen = value; Save(); }
    }

    public static bool IsDjBannerQrCodeEnabled
    {
        get => _data.IsDjBannerQrCodeEnabled;
        set { _data.IsDjBannerQrCodeEnabled = value; Save(); }
    }

    public static string WifiPassword
    {
        get => _data.WifiPassword;
        set { _data.WifiPassword = value; Save(); }
    }

    public static List<Lyracist.Shared.SpecialEventConfig> SpecialEvents
    {
        get => _data.SpecialEvents;
        set { _data.SpecialEvents = value; Save(); }
    }

    public static bool FloatCurrentSingerToTop
    {
        get => _data.FloatCurrentSingerToTop;
        set { _data.FloatCurrentSingerToTop = value; Save(); }
    }

    /// <summary>Default estimated song length in minutes, used by the rotation-screen "estimated
    /// wait time" badge whenever a queued song's actual duration isn't known/resolvable.</summary>
    public static double DefaultSongLengthMinutes
    {
        get => _data.DefaultSongLengthMinutes;
        set { _data.DefaultSongLengthMinutes = value; Save(); }
    }

    /// <summary>Whether the rotation-screen "estimated wait time" badge is shown at all. Some DJs
    /// prefer not to display wait estimates to the audience; defaults on.</summary>
    public static bool ShowEstimatedWaitTime
    {
        get => _data.ShowEstimatedWaitTime;
        set { _data.ShowEstimatedWaitTime = value; Save(); }
    }

    /// <summary>When true, patrons cannot request songs via the portal that have already been performed or queued in the current session.</summary>
    public static bool BlockDuplicateSongsInSession
    {
        get => _data.BlockDuplicateSongsInSession;
        set { _data.BlockDuplicateSongsInSession = value; Save(); }
    }

    /// <summary>When true, song requests from patron portal and kiosk are restricted to scheduled session hours.</summary>
    public static bool EnableSessionSchedule
    {
        get => _data.EnableSessionSchedule;
        set { _data.EnableSessionSchedule = value; Save(); }
    }

    /// <summary>Start time of the scheduled session (e.g. "8:00 PM" or "20:00").</summary>
    public static string SessionStartTime
    {
        get => _data.SessionStartTime;
        set { _data.SessionStartTime = value; Save(); }
    }

    /// <summary>Stop time of the scheduled session (e.g. "2:00 AM" or "02:00").</summary>
    public static string SessionStopTime
    {
        get => _data.SessionStopTime;
        set { _data.SessionStopTime = value; Save(); }
    }

    /// <summary>When true, song requests from patron portal and kiosk are cut off at the specified last request time.</summary>
    public static bool EnableLastRequestTime
    {
        get => _data.EnableLastRequestTime;
        set { _data.EnableLastRequestTime = value; Save(); }
    }

    /// <summary>Cutoff time for receiving new song requests (e.g. "1:30 AM" or "01:30").</summary>
    public static string LastRequestTime
    {
        get => _data.LastRequestTime;
        set { _data.LastRequestTime = value; Save(); }
    }

    /// <summary>Evaluates whether request submissions are currently permitted based on schedule and cutoff settings.</summary>
    public static bool IsRequestSubmissionAllowed(out string reason, DateTime? now = null)
    {
        return SessionScheduleHelper.IsRequestSubmissionAllowed(
            EnableSessionSchedule,
            SessionStartTime,
            SessionStopTime,
            EnableLastRequestTime,
            LastRequestTime,
            out reason,
            now);
    }

    /// <summary>Folder to monitor for newly downloaded/purchased tracks (defaults to user's Downloads folder).</summary>
    public static string StorePurchasedTracksFolder
    {
        get => string.IsNullOrWhiteSpace(_data.StorePurchasedTracksFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            : _data.StorePurchasedTracksFolder;
        set { _data.StorePurchasedTracksFolder = value; Save(); }
    }

    /// <summary>Target folder where purchased karaoke tracks (.zip, .cdg/.mp3, .mp4) should be routed.</summary>
    public static string StoreTargetKaraokeFolder
    {
        get => _data.StoreTargetKaraokeFolder;
        set { _data.StoreTargetKaraokeFolder = value; Save(); }
    }

    /// <summary>Target folder where purchased music tracks (standalone .mp3) should be routed.</summary>
    public static string StoreTargetMusicFolder
    {
        get => _data.StoreTargetMusicFolder;
        set { _data.StoreTargetMusicFolder = value; Save(); }
    }

    /// <summary>When true, files detected in the purchased tracks folder are automatically imported into the library.</summary>
    public static bool StoreAutoImportEnabled
    {
        get => _data.StoreAutoImportEnabled;
        set { _data.StoreAutoImportEnabled = value; Save(); }
    }

    /// <summary>When true and target folders are specified, imported files are moved to their target folder instead of kept in downloads.</summary>
    public static bool StoreMoveFilesToTarget
    {
        get => _data.StoreMoveFilesToTarget;
        set { _data.StoreMoveFilesToTarget = value; Save(); }
    }

    /// <summary>When true, imported audio files are loudness-normalized to -16 LUFS via FFmpeg loudnorm.</summary>
    public static bool StoreNormalizeAudioOnImport
    {
        get => _data.StoreNormalizeAudioOnImport;
        set { _data.StoreNormalizeAudioOnImport = value; Save(); }
    }

    /// <summary>When true, leading and trailing silence below -50dB is trimmed from imported audio via FFmpeg.</summary>
    public static bool StoreTrimSilenceOnImport
    {
        get => _data.StoreTrimSilenceOnImport;
        set { _data.StoreTrimSilenceOnImport = value; Save(); }
    }

    /// <summary>When true, a visual PNG waveform preview is rendered on import via FFmpeg showwavespic.</summary>
    public static bool StoreGenerateWaveformOnImport
    {
        get => _data.StoreGenerateWaveformOnImport;
        set { _data.StoreGenerateWaveformOnImport = value; Save(); }
    }

    /// <summary>Preferred commercial karaoke provider (KV, PT, Sunfly, Karaoke.com).</summary>
    public static string PreferredProvider
    {
        get => string.IsNullOrWhiteSpace(_data.PreferredProvider) ? "KV" : _data.PreferredProvider;
        set { _data.PreferredProvider = value; Save(); }
    }

    /// <summary>Preferred karaoke file packaging type (MP3+G, MP4, Audio-only).</summary>
    public static string PreferredFileType
    {
        get => string.IsNullOrWhiteSpace(_data.PreferredFileType) ? "MP3+G" : _data.PreferredFileType;
        set { _data.PreferredFileType = value; Save(); }
    }

    /// <summary>Default setting for loudness normalization on import.</summary>
    public static bool DefaultNormalizeAudio
    {
        get => _data.DefaultNormalizeAudio;
        set { _data.DefaultNormalizeAudio = value; Save(); }
    }

    /// <summary>Default setting for silence trimming on import.</summary>
    public static bool DefaultTrimSilence
    {
        get => _data.DefaultTrimSilence;
        set { _data.DefaultTrimSilence = value; Save(); }
    }

    /// <summary>Default setting for waveform preview generation on import.</summary>
    public static bool DefaultGenerateWaveform
    {
        get => _data.DefaultGenerateWaveform;
        set { _data.DefaultGenerateWaveform = value; Save(); }
    }

    /// <summary>Preferred target folder category for downloaded purchases (Karaoke or Music).</summary>
    public static string PreferredTargetFolder
    {
        get => string.IsNullOrWhiteSpace(_data.PreferredTargetFolder) ? "Karaoke" : _data.PreferredTargetFolder;
        set { _data.PreferredTargetFolder = value; Save(); }
    }

    /// <summary>Preferred lyrics companion format (LRC or TXT).</summary>
    public static string PreferredLyricsFormat
    {
        get => string.IsNullOrWhiteSpace(_data.PreferredLyricsFormat) ? "LRC" : _data.PreferredLyricsFormat;
        set { _data.PreferredLyricsFormat = value; Save(); }
    }

    // ─── Data Model ────────────────────────────────────────────────────────

    private sealed class SettingsData
    {
        public Dictionary<string, string> Hotkeys { get; set; } = [];
        public string WifiPassword { get; set; } = string.Empty;
        public string ConnectInstructionsScreen { get; set; } = "All Screens / Monitors";
        public bool IsDjBannerQrCodeEnabled { get; set; } = true;
        public bool EnableKillVocal { get; set; } = false;
        public bool FloatCurrentSingerToTop { get; set; } = false;
        public double DefaultSongLengthMinutes { get; set; } = 4.75;
        public bool ShowEstimatedWaitTime { get; set; } = true;
        public bool BlockDuplicateSongsInSession { get; set; } = true;
        public bool EnableSessionSchedule { get; set; } = false;
        public string SessionStartTime { get; set; } = "8:00 PM";
        public string SessionStopTime { get; set; } = "2:00 AM";
        public bool EnableLastRequestTime { get; set; } = false;
        public string LastRequestTime { get; set; } = "1:30 AM";
        public string StorePurchasedTracksFolder { get; set; } = string.Empty;
        public string StoreTargetKaraokeFolder { get; set; } = string.Empty;
        public string StoreTargetMusicFolder { get; set; } = string.Empty;
        public bool StoreAutoImportEnabled { get; set; } = false;
        public bool StoreMoveFilesToTarget { get; set; } = true;
        public bool StoreNormalizeAudioOnImport { get; set; } = false;
        public bool StoreTrimSilenceOnImport { get; set; } = false;
        public bool StoreGenerateWaveformOnImport { get; set; } = false;
        public string PreferredProvider { get; set; } = "KV";
        public string PreferredFileType { get; set; } = "MP3+G";
        public bool DefaultNormalizeAudio { get; set; } = true;
        public bool DefaultTrimSilence { get; set; } = true;
        public bool DefaultGenerateWaveform { get; set; } = true;
        public string PreferredTargetFolder { get; set; } = "Karaoke";
        public string PreferredLyricsFormat { get; set; } = "LRC";

        // Registration data
        public string RegFirstName { get; set; } = string.Empty;
        public string RegLastName { get; set; } = string.Empty;
        public string RegStageName { get; set; } = string.Empty;
        public string RegEmail { get; set; } = string.Empty;
        public string RegLicenseKey { get; set; } = string.Empty;

        public string ThemeMode { get; set; } = "Dark";
        public bool ShowSplashOnStartup { get; set; } = false;
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
        public string StaticIPAddress { get; set; } = string.Empty;
        public int TabletPort { get; set; } = 5005;

        public bool KSRotationSyncEnabled { get; set; } = false;
        public string KSRotationIpAddress { get; set; } = "127.0.0.1";
        public int KSRotationPort { get; set; } = 5000;
        public int KSRotationSyncIntervalSeconds { get; set; } = 2;

        public bool AutoAcceptRequests { get; set; } = false;

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
        public List<string> LibraryDirectories { get; set; } = [];

        // Scaryoke categories - dynamic configuration up to 8 sectors
        public List<string> ScaryokeCategories { get; set; } =
        [
            "Gender Bender", "Elvis", "Country", "Rock & Roll", "Pop", "80s Music",
            "60s Oldies", "Motown"
        ];

        // Venues & DJ name
        public string DjName { get; set; } = "DJ Karaoke";

        public List<string> Venues { get; set; } = ["The Tavern", "The Stage", "The Pub"];
        public string SelectedVenue { get; set; } = "The Tavern";

        // Crawl banner type and custom text
        public string CrawlBannerType { get; set; } = "Dramatic";

        public string CrawlBannerCustomText { get; set; } = "Enter custom crawl text here...";

        // Spaceship overlay configurations
        public int CrawlSpaceshipFontSize { get; set; } = 26;

        public int CrawlSpaceshipDuration { get; set; } = 13;
        public int CrawlSpaceshipFrequency { get; set; } = 60;

        public List<Lyracist.Models.SpaceshipSnippet> CrawlSpaceshipSnippets { get; set; } =
        [
            new() { Text = "Tip your bartender! 🍹", IsEnabled = true },
            new() { Text = "Tip your servers! 💸", IsEnabled = true },
            new() { Text = "Buy a drink at the bar! 🍺", IsEnabled = true },
            new() { Text = "Keep the queue moving, request a song! 🎤", IsEnabled = true },
            new() { Text = "Remember to tip the staff! 💵", IsEnabled = true },
            new() { Text = "Scaryoke party mode active! 🎡", IsEnabled = true }
        ];

        public bool IsRatingSystemEnabled { get; set; } = true;
        public bool ShowQrCodeOnRotationScreen { get; set; } = true;
        public bool ShowQrCodeOnLyricsScreen { get; set; } = true;
        public string SelectedRatingIcon { get; set; } = "⭐ Star";
        public List<string> AvailableRatingIcons { get; set; } = ["⭐ Star", "❤️ Heart", "🔥 Fire", "🎵 Note", "🏆 Trophy", "👑 Crown", "👍 Like"];
        public string CdgBackdropMode { get; set; } = "Original Color";
        public int FillInDelaySeconds { get; set; } = 5;
        public Dictionary<string, List<string>> VenueGraphics { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool EnableAutoAdvance { get; set; } = true;
        public int AutoAdvanceCountdownSeconds { get; set; } = 10;
        public string SelectedKaraokeAudioDevice { get; set; } = string.Empty;
        public string SelectedBgmAudioDevice { get; set; } = string.Empty;
        public bool IsHardwareMixerMode { get; set; } = false;
        public bool NormalizeVolumeEnabled { get; set; } = true;
        public double TargetLoudnessLufs { get; set; } = -12.0;
        public double MasterOutputBoostDb { get; set; } = 6.0;
        public bool EnableAudioLimiter { get; set; } = true;
        public bool AutoPlayRotationMusic { get; set; } = true;
        public int RotationMusicDelaySeconds { get; set; } = 0;
        public List<Lyracist.Shared.SpecialEventConfig> SpecialEvents { get; set; } =
        [
            new() { EventName = "Birthday", BannerFileName = "Birthday.png" },
            new() { EventName = "Wedding", BannerFileName = "Wedding.png" },
            new() { EventName = "Engagement", BannerFileName = "Engagement.png" },
            new() { EventName = "Anniversary", BannerFileName = "Anniversary.png" },
            new() { EventName = "Last Song", BannerFileName = "LastSong.png" }
        ];
    }
}