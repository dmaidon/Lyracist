using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.Services.Tablet;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IDisplayService _display;
    private readonly ITabletLyricsServer _tablet;
    private readonly IShowFlowService _showFlow;
    private readonly INavigationService _navigation;
    private readonly ILibraryService _library;
    private readonly RotationViewModel _rotation;

    // Theme
    [ObservableProperty]
    private bool _isDarkMode = true;

    [ObservableProperty]
    private bool _isTestMode = AppSettings.IsTestMode;

    // Audio
    [ObservableProperty]
    private List<string> _audioDevices = new();

    [ObservableProperty]
    private string _selectedAudioDevice = string.Empty;

    [ObservableProperty]
    private int _volume = 80;

    [ObservableProperty]
    private int _latency = 40;

    // Display
    public List<ScreenInfo> Screens { get; }

    [ObservableProperty]
    private ScreenInfo? _rotationScreen;

    [ObservableProperty]
    private ScreenInfo? _lyricsScreen;

    [ObservableProperty]
    private bool _isLyricsMirrored;

    public List<string> ProjectionViews { get; } = new() { "Normal List", "Star Wars Crawl", "Vegas Marquee", "Vinyl Turntable" };

    [ObservableProperty]
    private string _selectedProjectionView = "Normal List";

    // Venues & DJ
    public ObservableCollection<string> Venues { get; } = new();

    [ObservableProperty]
    private string _djName = AppSettings.DjName;

    [ObservableProperty]
    private string? _selectedVenue = AppSettings.SelectedVenue;

    [ObservableProperty]
    private string _newVenueName = string.Empty;

    // Crawl Banner Settings
    public List<string> CrawlBannerTypes { get; } = new() { "Dramatic", "Comedic", "Over-the-Top", "Custom" };

    [ObservableProperty]
    private string _selectedCrawlBannerType = AppSettings.CrawlBannerType;

    [ObservableProperty]
    private string _crawlBannerCustomText = AppSettings.CrawlBannerCustomText;

    // Spaceship overlay settings
    [ObservableProperty]
    private int _crawlSpaceshipFontSize = AppSettings.CrawlSpaceshipFontSize;

    [ObservableProperty]
    private int _crawlSpaceshipDuration = AppSettings.CrawlSpaceshipDuration;

    [ObservableProperty]
    private int _crawlSpaceshipFrequency = AppSettings.CrawlSpaceshipFrequency;

    public ObservableCollection<Lyracist.Models.SpaceshipSnippet> CrawlSpaceshipSnippets { get; } = new(AppSettings.CrawlSpaceshipSnippets);

    [ObservableProperty]
    private string _newSpaceshipSnippetText = string.Empty;

    [ObservableProperty]
    private Lyracist.Models.SpaceshipSnippet? _selectedSpaceshipSnippet;

    public bool IsStarWarsCrawlSelected => SelectedProjectionView == "Star Wars Crawl";
    public bool IsCustomCrawlBannerSelected => SelectedCrawlBannerType == "Custom";

    public string CrawlBannerPreviewText
    {
        get
        {
            return SelectedCrawlBannerType switch
            {
                "Dramatic" => "Dramatic: \"In a tavern far, far away, known only as {venue}, the patrons have risen in glorious rebellion — and under the wicked command of their sinister DJ, {dj}, they have chosen their ultimate weapon… karaoke.\"",
                "Comedic" => "Comedic: \"Somewhere in the distant reaches of the galaxy, inside a questionable establishment called {venue}, the patrons have staged a full‑blown uprising. Led by their diabolical DJ, {dj}, they now march toward their destiny: screaming karaoke like it’s a battle cry.\"",
                "Over-the-Top" => "Over-the-Top: \"In a tavern lost to time and space — a place whispered about only as {venue} — the patrons have revolted. Guided by the dark influence of DJ {dj}, they embark on a quest of unimaginable terror… karaoke night.\"",
                _ => "Custom: \"" + CrawlBannerCustomText + "\""
            };
        }
    }

    // Tablet Server
    [ObservableProperty]
    private int _tabletPort = 5005;

    [ObservableProperty]
    private string _tabletStatus = "Running";

    // MediaEngine
    public List<string> CdgScalingModes { get; }

    [ObservableProperty]
    private string _selectedCdgScalingMode = "Nearest";

    public List<string> Mp4Backends { get; }

    [ObservableProperty]
    private string _selectedMp4Backend = "LibVLC";

    [ObservableProperty]
    private int _frameRate = 30;

    public List<int> FpsOptions { get; } = new() { 15, 30, 60 };

    [ObservableProperty]
    private bool _enableHardwareAcceleration = AppSettings.EnableHardwareAcceleration;

    [ObservableProperty]
    private bool _enableNoiseGate = AppSettings.EnableNoiseGate;

    [ObservableProperty]
    private bool _enableReverb = AppSettings.EnableReverb;

    public List<int> BufferSizes { get; } = new() { 64, 128, 256, 512, 1024 };

    [ObservableProperty]
    private int _selectedBufferSize = AppSettings.SelectedBufferSize;

    public List<string> AsioDevices => AudioDevices;

    public string SelectedAsioDevice
    {
        get => SelectedAudioDevice;
        set => SelectedAudioDevice = value;
    }

    partial void OnEnableHardwareAccelerationChanged(bool value) => AppSettings.EnableHardwareAcceleration = value;
    partial void OnEnableNoiseGateChanged(bool value) => AppSettings.EnableNoiseGate = value;
    partial void OnEnableReverbChanged(bool value) => AppSettings.EnableReverb = value;
    partial void OnSelectedBufferSizeChanged(int value) => AppSettings.SelectedBufferSize = value;

    partial void OnSelectedAudioDeviceChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedAsioDevice));
    }



    // ─── Service Logins & API Keys ──────────────────────────────────────────

    [ObservableProperty]
    private string _youTubeApiKey = AppSettings.YouTubeApiKey;

    [ObservableProperty]
    private string _spotifyClientId = AppSettings.SpotifyClientId;

    [ObservableProperty]
    private string _spotifyClientSecret = AppSettings.SpotifyClientSecret;

    [ObservableProperty]
    private string _amazonAccessKey = AppSettings.AmazonAccessKey;

    [ObservableProperty]
    private string _amazonSecretKey = AppSettings.AmazonSecretKey;

    [ObservableProperty]
    private string _partyTymeClientId = AppSettings.PartyTymeClientId;

    [ObservableProperty]
    private string _partyTymeClientSecret = AppSettings.PartyTymeClientSecret;

    partial void OnYouTubeApiKeyChanged(string value) => AppSettings.YouTubeApiKey = value;
    partial void OnSpotifyClientIdChanged(string value) => AppSettings.SpotifyClientId = value;
    partial void OnSpotifyClientSecretChanged(string value) => AppSettings.SpotifyClientSecret = value;
    partial void OnAmazonAccessKeyChanged(string value) => AppSettings.AmazonAccessKey = value;
    partial void OnAmazonSecretKeyChanged(string value) => AppSettings.AmazonSecretKey = value;
    partial void OnPartyTymeClientIdChanged(string value) => AppSettings.PartyTymeClientId = value;
    partial void OnPartyTymeClientSecretChanged(string value) => AppSettings.PartyTymeClientSecret = value;

    // ─── Music Library ─────────────────────────────────────────────────

    public ObservableCollection<string> LibraryDirectories { get; } = new();

    [ObservableProperty]
    private string? _selectedLibraryDirectory;

    [ObservableProperty]
    private string _libraryStatus = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    // ─── Scaryoke Configuration ────────────────────────────────────────

    public ObservableCollection<string> ScaryokeCategories { get; } = new();

    [ObservableProperty]
    private string? _selectedScaryokeCategory;

    [ObservableProperty]
    private string _newScaryokeCategoryName = string.Empty;

    // ─── Splash Screen ─────────────────────────────────────────────────

    [ObservableProperty]
    private bool _showSplashOnStartup = AppSettings.ShowSplashOnStartup;

    // ─── Background Music Channel Volumes (0–100) ──────────────────────

    [ObservableProperty]
    private int _openingVolume = AppSettings.OpeningVolume;

    [ObservableProperty]
    private int _fillInVolume = AppSettings.FillInVolume;

    [ObservableProperty]
    private int _endRotationVolume = AppSettings.EndRotationVolume;

    // ─── Background Music Channel Tone (–20 to +20 dB) ────────────────

    [ObservableProperty]
    private double _openingBass = AppSettings.OpeningBass;

    [ObservableProperty]
    private double _openingTreble = AppSettings.OpeningTreble;

    [ObservableProperty]
    private double _fillInBass = AppSettings.FillInBass;

    [ObservableProperty]
    private double _fillInTreble = AppSettings.FillInTreble;

    [ObservableProperty]
    private double _endRotationBass = AppSettings.EndRotationBass;

    [ObservableProperty]
    private double _endRotationTreble = AppSettings.EndRotationTreble;

    public SettingsViewModel(IDisplayService display,
                             ITabletLyricsServer tablet,
                             IShowFlowService showFlow,
                             INavigationService navigation,
                             ILibraryService library,
                             RotationViewModel rotation)
    {
        _display = display;
        _tablet = tablet;
        _showFlow = showFlow;
        _navigation = navigation;
        _library = library;
        _rotation = rotation;

        _library.LibraryUpdated += (_, _) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsScanning = false;
                RefreshLibraryStatus();
            });
        };

        // Apply persisted channel settings to the show flow service on load
        _showFlow.SetOpeningVolume(OpeningVolume);
        _showFlow.SetFillInVolume(FillInVolume);
        _showFlow.SetEndRotationVolume(EndRotationVolume);
        _showFlow.SetOpeningTone(OpeningBass, OpeningTreble, 0);
        _showFlow.SetFillInTone(FillInBass, FillInTreble, 0);
        _showFlow.SetEndRotationTone(EndRotationBass, EndRotationTreble, 0);

        // Load active screen list with a 'None' option first
        var screens = new List<ScreenInfo> { new ScreenInfo { Index = -1, DeviceName = "None (Do not show)" } };
        screens.AddRange(_display.GetScreens());
        Screens = screens;

        // Reflect the last saved monitor assignments/mirror state without
        // triggering the OnChanged side effects below (which would move the
        // projection windows during SettingsViewModel construction, before
        // the show has even started).
        var prefs = _display.GetPreferences();
        _rotationScreen = prefs.RotationScreenIndex.HasValue
            ? Screens.FirstOrDefault(s => s.Index == prefs.RotationScreenIndex.Value)
            : Screens.FirstOrDefault(s => s.Index == -1);
        _lyricsScreen = prefs.LyricsScreenIndex.HasValue
            ? Screens.FirstOrDefault(s => s.Index == prefs.LyricsScreenIndex.Value)
            : Screens.FirstOrDefault(s => s.Index == -1);
        _isLyricsMirrored = prefs.IsLyricsMirrored;
        _selectedProjectionView = prefs.RotationViewMode ?? "Normal List";

        // Seed available devices
        AudioDevices = new List<string>
        {
            "Default System Device",
            "Speakers (Realtek High Definition Audio)",
            "Headphones (USB Audio Device)",
            "Digital Output (HDMI)"
        };
        SelectedAudioDevice = AudioDevices[0];

        // Seed list values
        CdgScalingModes = new List<string> { "Nearest", "Linear" };
        Mp4Backends = new List<string> { "LibVLC", "FFME" };

        RefreshLibraryDirectories();
        RefreshLibraryStatus();
        RefreshScaryokeCategories();
        RefreshVenues();
    }



    private void RefreshLibraryDirectories()
    {
        LibraryDirectories.Clear();
        foreach (var dir in Core.Helpers.AppSettings.LibraryDirectories)
            LibraryDirectories.Add(dir);
    }

    private void RefreshScaryokeCategories()
    {
        ScaryokeCategories.Clear();
        foreach (var cat in AppSettings.ScaryokeCategories)
        {
            ScaryokeCategories.Add(cat);
        }
    }

    private void RefreshVenues()
    {
        Venues.Clear();
        foreach (var v in AppSettings.Venues)
        {
            Venues.Add(v);
        }
        SelectedVenue = AppSettings.SelectedVenue;
    }

    partial void OnDjNameChanged(string value)
    {
        AppSettings.DjName = value;
    }

    partial void OnSelectedVenueChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            AppSettings.SelectedVenue = value;
        }
    }

    partial void OnSelectedCrawlBannerTypeChanged(string value)
    {
        AppSettings.CrawlBannerType = value;
        OnPropertyChanged(nameof(IsCustomCrawlBannerSelected));
        OnPropertyChanged(nameof(CrawlBannerPreviewText));
        UpdateCrawlBannerOnWindow();
    }

    partial void OnCrawlBannerCustomTextChanged(string value)
    {
        AppSettings.CrawlBannerCustomText = value;
        OnPropertyChanged(nameof(CrawlBannerPreviewText));
        UpdateCrawlBannerOnWindow();
    }

    partial void OnCrawlSpaceshipFontSizeChanged(int value) => AppSettings.CrawlSpaceshipFontSize = value;
    partial void OnCrawlSpaceshipDurationChanged(int value) => AppSettings.CrawlSpaceshipDuration = value;
    partial void OnCrawlSpaceshipFrequencyChanged(int value) => AppSettings.CrawlSpaceshipFrequency = value;

    [RelayCommand]
    private void AddSpaceshipSnippet()
    {
        string text = NewSpaceshipSnippetText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return;

        if (CrawlSpaceshipSnippets.Count >= 10)
        {
            System.Windows.MessageBox.Show(
                "A maximum of 10 snippets is allowed.",
                "Max Snippets Reached",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var snippet = new Lyracist.Models.SpaceshipSnippet { Text = text, IsEnabled = true };
        CrawlSpaceshipSnippets.Add(snippet);
        NewSpaceshipSnippetText = string.Empty;
        SaveSpaceshipSnippets();
    }

    [RelayCommand]
    private void RemoveSpaceshipSnippet()
    {
        if (SelectedSpaceshipSnippet == null) return;
        CrawlSpaceshipSnippets.Remove(SelectedSpaceshipSnippet);
        SelectedSpaceshipSnippet = null;
        SaveSpaceshipSnippets();
    }

    [RelayCommand]
    private void SaveSpaceshipSnippets()
    {
        AppSettings.CrawlSpaceshipSnippets = CrawlSpaceshipSnippets.ToList();
    }

    private void UpdateCrawlBannerOnWindow()
    {
        string template = AppSettings.GetActiveCrawlBannerTemplate();
        _display.SetCrawlBannerText(template);
    }

    [RelayCommand]
    private void AddVenue()
    {
        string venue = NewVenueName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(venue)) return;

        AppSettings.AddVenue(venue);
        NewVenueName = string.Empty;
        RefreshVenues();
        SelectedVenue = venue;
    }

    [RelayCommand]
    private void RemoveVenue()
    {
        if (SelectedVenue == null) return;

        AppSettings.RemoveVenue(SelectedVenue);
        SelectedVenue = null;
        RefreshVenues();
    }

    [RelayCommand]
    private void AddScaryokeCategory()
    {
        string cat = NewScaryokeCategoryName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(cat)) return;

        if (AppSettings.ScaryokeCategories.Count >= 8)
        {
            System.Windows.MessageBox.Show(
                "A maximum of 8 categories is allowed for the Scaryoke wheel.",
                "Max Categories Reached",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        AppSettings.AddScaryokeCategory(cat);
        NewScaryokeCategoryName = string.Empty;
        RefreshScaryokeCategories();
    }

    [RelayCommand]
    private void RemoveScaryokeCategory()
    {
        if (SelectedScaryokeCategory == null) return;

        if (AppSettings.ScaryokeCategories.Count <= 2)
        {
            System.Windows.MessageBox.Show(
                "The Scaryoke wheel must have at least 2 categories to be playable.",
                "Minimum Categories Warning",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        AppSettings.RemoveScaryokeCategory(SelectedScaryokeCategory);
        SelectedScaryokeCategory = null;
        RefreshScaryokeCategories();
    }

    private void RefreshLibraryStatus()
    {
        int count = _library.GetSongCount();
        LibraryStatus = count == 0
            ? "No songs scanned yet — add a folder and scan."
            : $"{count:N0} songs in library.";
    }

    [RelayCommand]
    private void AddLibraryDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Music Library Folder to Scan"
        };
        if (dialog.ShowDialog() != true) return;

        IsScanning = true;
        // ScanDirectory persists to AppSettings internally.
        _library.ScanDirectory(dialog.FolderName);
        RefreshLibraryDirectories();
        LibraryStatus = "Scanning…";
    }

    [RelayCommand]
    private void RemoveLibraryDirectory()
    {
        if (SelectedLibraryDirectory == null) return;

        var confirm = System.Windows.MessageBox.Show(
            $"Remove '{SelectedLibraryDirectory}' from the scan list? Songs already indexed from this folder will also be removed from the library.",
            "Confirm Remove Directory",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        _library.RemoveSongsUnderDirectory(SelectedLibraryDirectory);
        Core.Helpers.AppSettings.RemoveLibraryDirectory(SelectedLibraryDirectory);
        SelectedLibraryDirectory = null;
        RefreshLibraryDirectories();
        RefreshLibraryStatus();
    }

    [RelayCommand]
    private void RescanLibrary()
    {
        if (LibraryDirectories.Count == 0) return;
        IsScanning = true;
        LibraryStatus = "Scanning…";
        _library.RescanAllDirectories();
    }

    [RelayCommand]
    private void ScanSelectedDirectory()
    {
        if (SelectedLibraryDirectory == null) return;
        IsScanning = true;
        LibraryStatus = "Scanning…";
        _library.ScanDirectory(SelectedLibraryDirectory);
    }

    [RelayCommand]
    private void ClearDatabase()
    {
        var confirm = System.Windows.MessageBox.Show(
            "Are you sure you want to clear the entire database? This will remove all songs, playlists, performer history, and active rotation queue.",
            "Confirm Clear Database",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            using (var db = new Lyracist.Data.LyracistDbContext())
            {
                db.Database.EnsureDeleted();
                // Use Migrate (not EnsureCreated) so __EFMigrationsHistory is populated
                // correctly — otherwise the next app startup's Migrate() call sees no
                // history and tries to re-apply migrations against tables that already exist.
                db.Database.Migrate();
            }

            // Clear lists in memory
            LibraryDirectories.Clear();
            RefreshLibraryDirectories();
            RefreshLibraryStatus();
            _rotation.ClearRotationQueue();

            System.Windows.MessageBox.Show("Database cleared successfully.", "Database Cleared", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Clear Database");
            System.Windows.MessageBox.Show($"Failed to clear database: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }



    partial void OnShowSplashOnStartupChanged(bool value)
    {
        AppSettings.ShowSplashOnStartup = value;
    }

    partial void OnOpeningVolumeChanged(int value)
    {
        AppSettings.OpeningVolume = value;
        _showFlow.SetOpeningVolume(value);
    }

    partial void OnFillInVolumeChanged(int value)
    {
        AppSettings.FillInVolume = value;
        _showFlow.SetFillInVolume(value);
    }

    partial void OnEndRotationVolumeChanged(int value)
    {
        AppSettings.EndRotationVolume = value;
        _showFlow.SetEndRotationVolume(value);
    }

    partial void OnOpeningBassChanged(double value)
    {
        AppSettings.OpeningBass = value;
        _showFlow.SetOpeningTone(value, OpeningTreble, 0);
    }

    partial void OnOpeningTrebleChanged(double value)
    {
        AppSettings.OpeningTreble = value;
        _showFlow.SetOpeningTone(OpeningBass, value, 0);
    }

    partial void OnFillInBassChanged(double value)
    {
        AppSettings.FillInBass = value;
        _showFlow.SetFillInTone(value, FillInTreble, 0);
    }

    partial void OnFillInTrebleChanged(double value)
    {
        AppSettings.FillInTreble = value;
        _showFlow.SetFillInTone(FillInBass, value, 0);
    }

    partial void OnEndRotationBassChanged(double value)
    {
        AppSettings.EndRotationBass = value;
        _showFlow.SetEndRotationTone(value, EndRotationTreble, 0);
    }

    partial void OnEndRotationTrebleChanged(double value)
    {
        AppSettings.EndRotationTreble = value;
        _showFlow.SetEndRotationTone(EndRotationBass, value, 0);
    }

    [RelayCommand]
    private void NavigateToPlaylists()
    {
        _navigation.Navigate(typeof(Views.Pages.PlaylistsPage));
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        var applicationTheme = value ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(applicationTheme);
        Lyracist.Themes.LyracistThemeManager.Apply(value);
    }

    partial void OnIsTestModeChanged(bool value)
    {
        AppSettings.IsTestMode = value;
        if (value)
        {
            _rotation.SeedSingers();
        }
        else
        {
            _rotation.ClearRotationQueue();
        }
    }

    partial void OnRotationScreenChanged(ScreenInfo? value)
    {
        if (value != null)
        {
            _display.MoveRotationToScreen(value.Index == -1 ? null : value.Index);
        }
    }

    partial void OnLyricsScreenChanged(ScreenInfo? value)
    {
        if (value != null)
        {
            _display.MoveLyricsToScreen(value.Index == -1 ? null : value.Index);
        }
    }

    partial void OnIsLyricsMirroredChanged(bool value)
    {
        _display.SetLyricsMirror(value);
    }

    partial void OnSelectedProjectionViewChanged(string value)
    {
        _display.SetRotationViewMode(value);
        OnPropertyChanged(nameof(IsStarWarsCrawlSelected));

        var karaokeVm = App.AppHost.Services.GetService(typeof(KaraokeViewModel)) as KaraokeViewModel;
        if (karaokeVm != null)
        {
            karaokeVm.RaiseSelectedProjectionViewChanged();
        }
    }

    [RelayCommand]
    private void RestoreLayout()
    {
        _display.RestoreAssignments();
    }

    [RelayCommand]
    private async Task StartTabletServer()
    {
        await _tablet.StartAsync();
        TabletStatus = "Running";
    }

    [RelayCommand]
    private async Task StopTabletServer()
    {
        await _tablet.StopAsync();
        TabletStatus = "Stopped";
    }

    [RelayCommand]
    private void BackupDatabase()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Backup Lyracist Database",
            FileName = $"lyracist_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
            Filter = "SQLite Database (*.db)|*.db|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        string selectedPath = dialog.FileName;

        try
        {
            if (System.IO.File.Exists(selectedPath))
            {
                System.IO.File.Delete(selectedPath);
            }

            using var db = new Lyracist.Data.LyracistDbContext();
#pragma warning disable EF1002
            db.Database.ExecuteSqlRaw($"VACUUM INTO '{selectedPath.Replace("'", "''")}';");
#pragma warning restore EF1002

            System.Windows.MessageBox.Show(
                $"Database backup created successfully at:{Environment.NewLine}{selectedPath}",
                "Backup Successful",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Database Backup");
            System.Windows.MessageBox.Show(
                $"Failed to backup database: {ex.Message}",
                "Backup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RestoreDatabase()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Restore Lyracist Database from Backup",
            Filter = "SQLite Database (*.db)|*.db|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        string selectedPath = dialog.FileName;

        var confirm = System.Windows.MessageBox.Show(
            "Restoring the database will overwrite all current settings, performers, playlists, and history. " +
            "The application will shutdown to complete the restore. Do you want to proceed?",
            "Confirm Database Restore",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            using (var db = new Lyracist.Data.LyracistDbContext())
            {
                db.Database.CloseConnection();
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dbPath = Path.Combine(baseDir, "Data", "lyracist.db");

            System.IO.File.Copy(selectedPath, dbPath, overwrite: true);

            string walPath = dbPath + "-wal";
            string shmPath = dbPath + "-shm";
            if (System.IO.File.Exists(walPath)) System.IO.File.Delete(walPath);
            if (System.IO.File.Exists(shmPath)) System.IO.File.Delete(shmPath);

            System.Windows.MessageBox.Show(
                "Database restored successfully. The application will now close. Please restart Lyracist.",
                "Restore Complete",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Database Restore");
            System.Windows.MessageBox.Show(
                $"Failed to restore database: {ex.Message}",
                "Restore Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}
