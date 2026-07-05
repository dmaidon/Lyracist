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
    private readonly IOccasionService _occasions;
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

    // Special Occasion editor
    public ObservableCollection<OccasionNode> OccasionCategories { get; } = new();
    public ObservableCollection<OccasionNode> OccasionItems { get; } = new();

    [ObservableProperty]
    private OccasionNode? _selectedOccasionCategory;

    [ObservableProperty]
    private OccasionNode? _selectedOccasionItem;

    [ObservableProperty]
    private string _newOccasionCategoryName = string.Empty;

    [ObservableProperty]
    private bool _addAsSubcategory;

    [ObservableProperty]
    private string _newOccasionItemName = string.Empty;

    [ObservableProperty]
    private double _occasionItemBass;

    [ObservableProperty]
    private double _occasionItemTreble;

    [ObservableProperty]
    private double _occasionItemGain;

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
                             IOccasionService occasions,
                             IShowFlowService showFlow,
                             INavigationService navigation,
                             ILibraryService library,
                             RotationViewModel rotation)
    {
        _display = display;
        _tablet = tablet;
        _occasions = occasions;
        _showFlow = showFlow;
        _navigation = navigation;
        _library = library;
        _rotation = rotation;

        _library.LibraryUpdated += (_, _) => RefreshLibraryStatus();

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

        RefreshOccasionCategories();
        RefreshLibraryDirectories();
        RefreshLibraryStatus();
    }

    private void RefreshOccasionCategories()
    {
        int? keepId = SelectedOccasionCategory?.Id;
        OccasionCategories.Clear();
        foreach (var category in _occasions.GetCategoriesFlat())
        {
            OccasionCategories.Add(category);
        }
        SelectedOccasionCategory = OccasionCategories.FirstOrDefault(c => c.Id == keepId)
                                   ?? OccasionCategories.FirstOrDefault();
    }

    private void RefreshOccasionItems()
    {
        OccasionItems.Clear();
        if (SelectedOccasionCategory == null) return;

        foreach (var item in _occasions.GetItems(SelectedOccasionCategory.Id))
        {
            OccasionItems.Add(item);
        }
    }

    private void RefreshLibraryDirectories()
    {
        LibraryDirectories.Clear();
        foreach (var dir in Core.Helpers.AppSettings.LibraryDirectories)
            LibraryDirectories.Add(dir);
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

        // ScanDirectory persists to AppSettings internally.
        _library.ScanDirectory(dialog.FolderName);
        RefreshLibraryDirectories();
        LibraryStatus = "Scanning…";
    }

    [RelayCommand]
    private void RemoveLibraryDirectory()
    {
        if (SelectedLibraryDirectory == null) return;
        Core.Helpers.AppSettings.RemoveLibraryDirectory(SelectedLibraryDirectory);
        SelectedLibraryDirectory = null;
        RefreshLibraryDirectories();
    }

    [RelayCommand]
    private void RescanLibrary()
    {
        if (LibraryDirectories.Count == 0) return;
        LibraryStatus = "Scanning…";
        _library.RescanAllDirectories();
    }

    partial void OnSelectedOccasionCategoryChanged(OccasionNode? value)
    {
        RefreshOccasionItems();
    }

    partial void OnSelectedOccasionItemChanged(OccasionNode? value)
    {
        if (value == null) return;
        OccasionItemBass = value.Bass;
        OccasionItemTreble = value.Treble;
        OccasionItemGain = value.Gain;
    }

    [RelayCommand]
    private void AddOccasionCategory()
    {
        if (string.IsNullOrWhiteSpace(NewOccasionCategoryName)) return;

        int? parentId = AddAsSubcategory ? SelectedOccasionCategory?.Id : null;
        _occasions.AddCategory(NewOccasionCategoryName, parentId);
        NewOccasionCategoryName = string.Empty;
        RefreshOccasionCategories();
    }

    [RelayCommand]
    private void RemoveOccasionCategory()
    {
        if (SelectedOccasionCategory == null) return;
        _occasions.RemoveCategory(SelectedOccasionCategory.Id);
        SelectedOccasionCategory = null;
        RefreshOccasionCategories();
    }

    [RelayCommand]
    private void AddOccasionItem()
    {
        if (SelectedOccasionCategory == null) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Occasion Music File",
            Filter = "Audio files (*.mp3;*.wav;*.m4a;*.flac)|*.mp3;*.wav;*.m4a;*.flac|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        string name = string.IsNullOrWhiteSpace(NewOccasionItemName)
            ? System.IO.Path.GetFileNameWithoutExtension(dialog.FileName)
            : NewOccasionItemName;

        _occasions.AddItem(SelectedOccasionCategory.Id, name, dialog.FileName);
        NewOccasionItemName = string.Empty;
        RefreshOccasionItems();
    }

    [RelayCommand]
    private void RemoveOccasionItem()
    {
        if (SelectedOccasionItem == null) return;
        _occasions.RemoveItem(SelectedOccasionItem.Id);
        SelectedOccasionItem = null;
        RefreshOccasionItems();
    }

    [RelayCommand]
    private void SaveOccasionItemAudio()
    {
        if (SelectedOccasionItem == null) return;
        _occasions.UpdateItemAudio(SelectedOccasionItem.Id, OccasionItemBass, OccasionItemTreble, OccasionItemGain);
        RefreshOccasionItems();
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
