// Edited on Sep 6, 2026 @ 13:04:00 -> Trigger ShowBulkImportCompleted toast notification and expose Notifications collection
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Services.Store;
using Microsoft.Win32;

namespace Lyracist.ViewModels;

public partial class BulkImportItemViewModel : ObservableObject
{
    public BulkImportCandidate Model { get; }

    public string Filename => Model.Filename;
    public string Title => Model.Title;
    public string Artist => Model.Artist;
    public string Provider => Model.Provider;
    public string FileType => Model.FileType;
    public string DurationText => Model.DurationText;
    public string Key => Model.Key;
    public string BpmText => Model.BpmText;
    public string Quality => Model.Quality;
    public string Difficulty => Model.Difficulty;
    public string VocalPresence => Model.VocalPresence;
    public bool HasDualAudio => Model.HasDualAudio;

    [ObservableProperty]
    private bool _willNormalize;

    [ObservableProperty]
    private bool _willTrimSilence;

    [ObservableProperty]
    private bool _willGenerateWaveform;

    public BulkImportItemViewModel(BulkImportCandidate model)
    {
        Model = model;
        _willNormalize = model.WillNormalize;
        _willTrimSilence = model.WillTrimSilence;
        _willGenerateWaveform = model.WillGenerateWaveform;
    }

    public BulkImportCandidate ToCandidate()
    {
        Model.WillNormalize = WillNormalize;
        Model.WillTrimSilence = WillTrimSilence;
        Model.WillGenerateWaveform = WillGenerateWaveform;
        return Model;
    }
}

public partial class BulkImportViewModel : BaseViewModel
{
    private readonly PurchasedTrackBulkImporter _importer;
    private CancellationTokenSource? _cts;

    public event Action? RequestClose;

    public TrackPreviewViewModel TrackPreview { get; } = new();
    public StoreNotificationService NotificationService => StoreNotificationService.Instance;
    public ObservableCollection<StoreNotificationItem> Notifications => NotificationService.ActiveNotifications;

    [ObservableProperty]
    private BulkImportItemViewModel? _selectedCandidate;

    partial void OnSelectedCandidateChanged(BulkImportItemViewModel? value)
    {
        if (value != null)
        {
            _ = TrackPreview.LoadFromBulkCandidateAsync(value.Model);
        }
    }

    [ObservableProperty]
    private string _selectedFolder = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private string _statusText = "Select a folder containing karaoke tracks (.mp3, .cdg, .zip, .mp4) to begin.";

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private int _totalTracks;

    [ObservableProperty]
    private int _successCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _skippedCount;

    [ObservableProperty]
    private string _currentProcessingFile = string.Empty;

    [ObservableProperty]
    private BulkImportSummaryReport? _summaryReport;

    [ObservableProperty]
    private bool _normalizeAll;

    [ObservableProperty]
    private bool _trimSilenceAll;

    [ObservableProperty]
    private bool _generateWaveformAll;

    [ObservableProperty]
    private bool _moveFilesToTarget;

    [ObservableProperty]
    private string _targetKaraokeFolder = string.Empty;

    [ObservableProperty]
    private string _targetMusicFolder = string.Empty;

    public ObservableCollection<BulkImportItemViewModel> PreviewItems { get; } = [];

    public BulkImportViewModel(ILibraryService? libraryService = null)
    {
        _importer = new PurchasedTrackBulkImporter(libraryService);
        _selectedFolder = AppSettings.StorePurchasedTracksFolder;
        _normalizeAll = AppSettings.DefaultNormalizeAudio;
        _trimSilenceAll = AppSettings.DefaultTrimSilence;
        _generateWaveformAll = AppSettings.DefaultGenerateWaveform;
        _moveFilesToTarget = AppSettings.StoreMoveFilesToTarget;
        _targetKaraokeFolder = AppSettings.StoreTargetKaraokeFolder;
        _targetMusicFolder = AppSettings.StoreTargetMusicFolder;
    }

    partial void OnNormalizeAllChanged(bool value)
    {
        foreach (var item in PreviewItems)
        {
            item.WillNormalize = value;
        }
    }

    partial void OnTrimSilenceAllChanged(bool value)
    {
        foreach (var item in PreviewItems)
        {
            item.WillTrimSilence = value;
        }
    }

    partial void OnGenerateWaveformAllChanged(bool value)
    {
        foreach (var item in PreviewItems)
        {
            item.WillGenerateWaveform = value;
        }
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Folder with Purchased Tracks",
            InitialDirectory = Directory.Exists(SelectedFolder) ? SelectedFolder : null
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFolder = dialog.FolderName;
            _ = ScanFolderAsync();
        }
    }

    [RelayCommand]
    public async Task ScanFolderAsync()
    {
        if (IsScanning || IsImporting) return;
        if (string.IsNullOrWhiteSpace(SelectedFolder) || !Directory.Exists(SelectedFolder))
        {
            StatusText = "Please select a valid folder path.";
            return;
        }

        try
        {
            IsScanning = true;
            IsComplete = false;
            SummaryReport = null;
            PreviewItems.Clear();
            ProgressPercent = 0;

            _cts = new CancellationTokenSource();
            var progress = new Progress<string>(msg =>
            {
                StatusText = msg;
            });

            var candidates = await PurchasedTrackBulkImporter.ScanFolderAsync(SelectedFolder, progress, _cts.Token);

            string preferredType = AppSettings.PreferredFileType;
            var orderedCandidates = candidates.OrderByDescending(c =>
                string.Equals(c.FileType, preferredType, StringComparison.OrdinalIgnoreCase) ||
                (preferredType == "Audio-only" && c.FileType == "Audio")).ToList();

            foreach (var c in orderedCandidates)
            {
                c.WillNormalize = NormalizeAll;
                c.WillTrimSilence = TrimSilenceAll;
                c.WillGenerateWaveform = GenerateWaveformAll;
                PreviewItems.Add(new BulkImportItemViewModel(c));
            }

            TotalTracks = PreviewItems.Count;
            StatusText = TotalTracks > 0
                ? $"Scanned {TotalTracks} track(s). Review options and click 'Start Bulk Import'."
                : "No supported media files (.mp3, .cdg, .zip, .mp4) found in selected folder.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Folder scan was canceled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    public async Task StartImportAsync()
    {
        if (IsImporting || IsScanning || PreviewItems.Count == 0) return;

        try
        {
            IsImporting = true;
            IsComplete = false;
            ProgressPercent = 0;
            SuccessCount = 0;
            ErrorCount = 0;
            SkippedCount = 0;

            _cts = new CancellationTokenSource();

            var candidates = PreviewItems.Select(vm => vm.ToCandidate()).ToList();
            var options = new BulkImportOptions
            {
                MoveFilesToTarget = MoveFilesToTarget,
                TargetKaraokeFolder = TargetKaraokeFolder,
                TargetMusicFolder = TargetMusicFolder
            };

            var progress = new Progress<BulkImportProgressReport>(report =>
            {
                ProgressPercent = report.PercentComplete;
                SuccessCount = report.SuccessCount;
                ErrorCount = report.ErrorCount;
                SkippedCount = report.SkippedCount;
                CurrentProcessingFile = report.CurrentItemName;
                StatusText = $"Processing ({report.CompletedItems}/{report.TotalItems}): {report.CurrentItemName}";
            });

            SummaryReport = await _importer.ImportBatchAsync(candidates, options, progress, _cts.Token);

            IsComplete = true;
            StatusText = $"Bulk import complete! {SummaryReport.TotalImported} imported, {SummaryReport.TotalSkipped} skipped, {SummaryReport.TotalErrors} error(s).";
            StoreNotificationService.Instance.ShowBulkImportCompleted(SummaryReport.TotalImported);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Bulk import was canceled by user.";
        }
        catch (Exception ex)
        {
            StatusText = $"Bulk import failed: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        StatusText = "Canceling operation...";
    }

    [RelayCommand]
    private void Close()
    {
        if (IsImporting)
        {
            var result = System.Windows.MessageBox.Show(
                "A bulk import operation is currently running. Do you want to cancel and close?",
                "Cancel Bulk Import",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes) return;
            _cts?.Cancel();
        }

        RequestClose?.Invoke();
    }
}
