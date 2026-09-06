// Edited on Sep 6, 2026 @ 13:01:15 -> Expose StoreNotificationService and toast notifications collection in StoreViewModel
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Services.Store;
using Lyracist.Shared;
using Microsoft.Win32;

namespace Lyracist.ViewModels;

public partial class StoreViewModel : BaseViewModel, IDisposable
{
    private readonly PurchasedTrackWatcherService _watcherService;
    private readonly ILibraryService? _libraryService;
    private readonly PurchasedTrackSyncService _syncService;
    private bool _isDisposed;

    public AnalyticsViewModel Analytics { get; } = new();
    public TrackPreviewViewModel TrackPreview { get; } = new();
    public ProviderSettingsViewModel ProviderSettings { get; } = new();
    public StoreNotificationService NotificationService => StoreNotificationService.Instance;
    public ObservableCollection<StoreNotificationItem> Notifications => NotificationService.ActiveNotifications;

    [ObservableProperty]
    private bool _showProviderSettings;

    public bool IsPreferredKv => string.Equals(AppSettings.PreferredProvider, "KV", StringComparison.OrdinalIgnoreCase);
    public bool IsPreferredPt => string.Equals(AppSettings.PreferredProvider, "PT", StringComparison.OrdinalIgnoreCase);
    public bool IsPreferredKc => string.Equals(AppSettings.PreferredProvider, "Karaoke.com", StringComparison.OrdinalIgnoreCase);
    public bool IsPreferredSf => string.Equals(AppSettings.PreferredProvider, "Sunfly", StringComparison.OrdinalIgnoreCase);
    public string PreferredProviderName => AppSettings.PreferredProvider switch
    {
        "PT" => "Party Tyme",
        "Sunfly" => "Sunfly",
        "Karaoke.com" => "Karaoke.com",
        _ => "Karaoke Version"
    };

    [ObservableProperty]
    private PurchasedTrackItem? _selectedRecentImport;

    partial void OnSelectedRecentImportChanged(PurchasedTrackItem? value)
    {
        if (value != null)
        {
            _ = TrackPreview.LoadFromRecentImportAsync(value);
        }
    }

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _purchasedTracksFolder = string.Empty;

    [ObservableProperty]
    private string _targetKaraokeFolder = string.Empty;

    [ObservableProperty]
    private string _targetMusicFolder = string.Empty;

    [ObservableProperty]
    private bool _isAutoImportEnabled;

    [ObservableProperty]
    private bool _moveFilesToTarget;

    [ObservableProperty]
    private bool _normalizeAudioOnImport;

    [ObservableProperty]
    private bool _trimSilenceOnImport;

    [ObservableProperty]
    private bool _generateWaveformOnImport;

    [ObservableProperty]
    private bool _showAdditionalProviders;

    [ObservableProperty]
    private bool _showLogPanel = true;

    [ObservableProperty]
    private string _watcherStatus = string.Empty;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private string _importStatusMessage = "Ready";

    public ObservableCollection<PurchasedTrackItem> RecentImports { get; } = [];
    public ObservableCollection<PurchasedImportLogItem> ImportLogs { get; } = [];

    public StoreViewModel(
        PurchasedTrackWatcherService watcherService,
        ILibraryService? libraryService = null,
        PurchasedTrackSyncService? syncService = null)
    {
        _watcherService = watcherService;
        _libraryService = libraryService;
        _syncService = syncService ?? new PurchasedTrackSyncService(watcherService);

        // Load settings
        _purchasedTracksFolder = AppSettings.StorePurchasedTracksFolder;
        _targetKaraokeFolder = AppSettings.StoreTargetKaraokeFolder;
        _targetMusicFolder = AppSettings.StoreTargetMusicFolder;
        _isAutoImportEnabled = AppSettings.StoreAutoImportEnabled;
        _moveFilesToTarget = AppSettings.StoreMoveFilesToTarget;
        _normalizeAudioOnImport = AppSettings.StoreNormalizeAudioOnImport;
        _trimSilenceOnImport = AppSettings.StoreTrimSilenceOnImport;
        _generateWaveformOnImport = AppSettings.StoreGenerateWaveformOnImport;

        _watcherStatus = _isAutoImportEnabled ? $"Watching: {_purchasedTracksFolder}" : "Auto-import is disabled";

        foreach (var log in _watcherService.RecentLogs)
        {
            ImportLogs.Add(log);
        }

        _watcherService.TrackImported += OnTrackImported;
        _watcherService.WatcherStatusChanged += OnWatcherStatusChanged;
        _watcherService.ImportLogged += OnImportLogged;
        ProviderSettings.SettingsSaved += OnProviderSettingsSaved;

        // Initialize analytics
        _ = Analytics.RefreshAnalyticsAsync();
    }

    private void OnTrackImported(object? sender, PurchasedTrackItem item)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            RecentImports.Insert(0, item);
            ImportStatusMessage = $"Imported: {item.Title} by {item.Artist} ({item.Source})";
            _ = Analytics.RefreshAnalyticsAsync();
        });
    }

    private void OnWatcherStatusChanged(object? sender, string status)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            WatcherStatus = status;
        });
    }

    private void OnImportLogged(object? sender, PurchasedImportLogItem log)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            ImportLogs.Insert(0, log);
            while (ImportLogs.Count > 20)
            {
                ImportLogs.RemoveAt(ImportLogs.Count - 1);
            }
        });
    }

    partial void OnPurchasedTracksFolderChanged(string value)
    {
        AppSettings.StorePurchasedTracksFolder = value;
        _watcherService.UpdateSettings();
    }

    partial void OnTargetKaraokeFolderChanged(string value)
    {
        AppSettings.StoreTargetKaraokeFolder = value;
    }

    partial void OnTargetMusicFolderChanged(string value)
    {
        AppSettings.StoreTargetMusicFolder = value;
    }

    partial void OnIsAutoImportEnabledChanged(bool value)
    {
        AppSettings.StoreAutoImportEnabled = value;
        _watcherService.UpdateSettings();
        WatcherStatus = value ? $"Watching: {PurchasedTracksFolder}" : "Auto-import is disabled";
    }

    partial void OnMoveFilesToTargetChanged(bool value)
    {
        AppSettings.StoreMoveFilesToTarget = value;
    }

    partial void OnNormalizeAudioOnImportChanged(bool value)
    {
        AppSettings.StoreNormalizeAudioOnImport = value;
    }

    partial void OnTrimSilenceOnImportChanged(bool value)
    {
        AppSettings.StoreTrimSilenceOnImport = value;
    }

    partial void OnGenerateWaveformOnImportChanged(bool value)
    {
        AppSettings.StoreGenerateWaveformOnImport = value;
    }

    public IReadOnlyList<IStoreProvider> RegisteredProviders => ProviderRegistry.Instance.Providers;

    [RelayCommand]
    public void SearchProvider(IStoreProvider? provider)
    {
        if (provider == null) return;
        PurchasedTrackWatcherService.ReferrerHint = provider.Name;
        string query = SearchQuery?.Trim() ?? string.Empty;
        Uri uri = provider.BuildSearchUri(query);
        OpenBrowserUrl(uri.AbsoluteUri);
    }

    public void SearchProviderBySource(ProviderSource source)
    {
        var provider = ProviderRegistry.Instance.GetProviderBySource(source);
        if (provider != null)
        {
            SearchProvider(provider);
        }
    }

    [RelayCommand]
    private void SearchKaraokeVersion() => SearchProviderBySource(ProviderSource.KaraokeVersion);

    [RelayCommand]
    private void SearchPartyTyme() => SearchProviderBySource(ProviderSource.PartyTyme);

    [RelayCommand]
    private void SearchKaraokeDotCom() => SearchProviderBySource(ProviderSource.KaraokeCom);

    [RelayCommand]
    private void SearchSunfly() => SearchProviderBySource(ProviderSource.Sunfly);

    [RelayCommand]
    private void ToggleAdditionalProviders()
    {
        ShowAdditionalProviders = !ShowAdditionalProviders;
    }

    [RelayCommand]
    private void ToggleLogPanel()
    {
        ShowLogPanel = !ShowLogPanel;
    }

    [RelayCommand]
    private void ClearImportLogs()
    {
        ImportLogs.Clear();
    }

    [RelayCommand]
    private void BrowsePurchasedFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Purchased Tracks Download Folder",
            InitialDirectory = Directory.Exists(PurchasedTracksFolder) ? PurchasedTracksFolder : null
        };

        if (dialog.ShowDialog() == true)
        {
            PurchasedTracksFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseTargetKaraokeFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Target Folder for Karaoke Files (.zip, .cdg, .mp4)",
            InitialDirectory = Directory.Exists(TargetKaraokeFolder) ? TargetKaraokeFolder : null
        };

        if (dialog.ShowDialog() == true)
        {
            TargetKaraokeFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseTargetMusicFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Target Folder for Music Audio Files (.mp3)",
            InitialDirectory = Directory.Exists(TargetMusicFolder) ? TargetMusicFolder : null
        };

        if (dialog.ShowDialog() == true)
        {
            TargetMusicFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private async Task ImportPurchasedTrackAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Purchased Track File(s) to Import",
            Filter = "Karaoke & Audio Tracks (*.mp3;*.cdg;*.zip;*.mp4;*.lrc;*.txt)|*.mp3;*.cdg;*.zip;*.mp4;*.lrc;*.txt|ZIP Karaoke (*.zip)|*.zip|MP3+G Files (*.mp3;*.cdg)|*.mp3;*.cdg|MP4 Video (*.mp4)|*.mp4|All Files (*.*)|*.*",
            Multiselect = true,
            InitialDirectory = Directory.Exists(PurchasedTracksFolder) ? PurchasedTracksFolder : null
        };

        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0) return;

        IsImporting = true;
        ImportStatusMessage = $"Importing {dialog.FileNames.Length} file(s)...";

        try
        {
            var results = await _watcherService.ImportBatchAsync(dialog.FileNames);
            ImportStatusMessage = results.Count > 0
                ? $"Successfully imported {results.Count} track(s)."
                : "No valid tracks could be imported from selected files.";
        }
        catch (Exception ex)
        {
            ImportStatusMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    private async Task SyncPurchasedTracksAsync()
    {
        if (IsSyncing) return;

        IsSyncing = true;
        ImportStatusMessage = "Syncing purchased tracks from downloads...";

        try
        {
            var result = await _syncService.SyncPurchasedTracksAsync(PurchasedTracksFolder);
            ImportStatusMessage = $"Store sync complete: {result.TotalImported} imported, {result.TotalSkipped} skipped, {result.TotalErrors} errors.";

            // Display completion summary modal dialog
            if (System.Windows.Application.Current?.MainWindow != null)
            {
                var summaryWin = new Windows.StoreSyncSummaryWindow(result)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };
                summaryWin.ShowDialog();
            }

            // Refresh analytics after sync
            await Analytics.RefreshAnalyticsAsync();
        }
        catch (Exception ex)
        {
            ImportStatusMessage = $"Sync failed: {ex.Message}";
            Globals.LogError("Lyracist", "Store sync failed", ex);
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task OpenBulkImportAsync()
    {
        var bulkVm = new BulkImportViewModel(_libraryService);
        var window = new Windows.BulkImportWindow(bulkVm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        window.ShowDialog();

        // Refresh analytics when bulk import closes
        await Analytics.RefreshAnalyticsAsync();
    }

    [RelayCommand]
    private void OpenPurchasedFolder()
    {
        if (Directory.Exists(PurchasedTracksFolder))
        {
            Process.Start(new ProcessStartInfo(PurchasedTracksFolder) { UseShellExecute = true });
        }
    }

    [RelayCommand]
    private void OpenTargetKaraokeFolder()
    {
        if (Directory.Exists(TargetKaraokeFolder))
        {
            Process.Start(new ProcessStartInfo(TargetKaraokeFolder) { UseShellExecute = true });
        }
    }

    [RelayCommand]
    private void OpenTargetMusicFolder()
    {
        if (Directory.Exists(TargetMusicFolder))
        {
            Process.Start(new ProcessStartInfo(TargetMusicFolder) { UseShellExecute = true });
        }
    }

    [RelayCommand]
    private async Task PreviewTrackAsync(PurchasedTrackItem? item)
    {
        item ??= SelectedRecentImport;
        if (item != null)
        {
            await TrackPreview.LoadFromRecentImportAsync(item);
        }
    }

    [RelayCommand]
    private void ClearRecentImports()
    {
        RecentImports.Clear();
        ImportStatusMessage = "Recent list cleared.";
    }

    [RelayCommand]
    private void ToggleProviderSettings()
    {
        ShowProviderSettings = !ShowProviderSettings;
    }

    [RelayCommand]
    public void SearchPreferredProvider()
    {
        var source = AppSettings.PreferredProvider switch
        {
            "PT" => ProviderSource.PartyTyme,
            "Sunfly" => ProviderSource.Sunfly,
            "Karaoke.com" => ProviderSource.KaraokeCom,
            _ => ProviderSource.KaraokeVersion
        };
        SearchProviderBySource(source);
    }

    private void OnProviderSettingsSaved(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsPreferredKv));
        OnPropertyChanged(nameof(IsPreferredPt));
        OnPropertyChanged(nameof(IsPreferredKc));
        OnPropertyChanged(nameof(IsPreferredSf));
        OnPropertyChanged(nameof(PreferredProviderName));

        NormalizeAudioOnImport = AppSettings.DefaultNormalizeAudio;
        TrimSilenceOnImport = AppSettings.DefaultTrimSilence;
        GenerateWaveformOnImport = AppSettings.DefaultGenerateWaveform;
    }

    private static void OpenBrowserUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", $"Failed to open URL in browser: {url}", ex);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        TrackPreview.Dispose();
        _watcherService.TrackImported -= OnTrackImported;
        _watcherService.WatcherStatusChanged -= OnWatcherStatusChanged;
        _watcherService.ImportLogged -= OnImportLogged;
        ProviderSettings.SettingsSaved -= OnProviderSettingsSaved;
        GC.SuppressFinalize(this);
    }
}
