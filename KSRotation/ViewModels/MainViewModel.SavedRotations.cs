// Edited on Nov 13, 2026 @ 16:12:00 -> Use RecalculateEstimatedWaits and RecalculateRoundEstimation when loading saved singers into rotation
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSRotation.Models;
using KSRotation.Services;
using Lyracist.Shared;

namespace KSRotation.ViewModels
{
    public partial class MainViewModel
    {
        public const int SavedRotationsMainTabIndex = 1;

        private List<SavedRotationReportItem> _allSavedReports = [];

        public ObservableCollection<SavedRotationReportItem> FilteredSavedReports { get; } = [];
        public ObservableCollection<SavedRotationSingerGroup> SelectedReportSingerGroups { get; } = [];
        public ObservableCollection<SavedRotationRow> SelectedReportRows { get; } = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedSavedReport))]
        [NotifyPropertyChangedFor(nameof(HasNoSelectedSavedReport))]
        [NotifyPropertyChangedFor(nameof(SelectedSavedReportHasPdf))]
        [NotifyPropertyChangedFor(nameof(SelectedSavedReportHasCsv))]
        [NotifyPropertyChangedFor(nameof(SelectedSavedReportTitle))]
        [NotifyPropertyChangedFor(nameof(SelectedSavedReportSubtitle))]
        private SavedRotationReportItem? _selectedSavedReport;

        public bool HasSelectedSavedReport => SelectedSavedReport != null;
        public bool HasNoSelectedSavedReport => SelectedSavedReport == null;
        public bool SelectedSavedReportHasPdf => SelectedSavedReport?.HasPdf == true;
        public bool SelectedSavedReportHasCsv => SelectedSavedReport?.HasCsv == true;

        public string SelectedSavedReportTitle => SelectedSavedReport == null
            ? "No Saved Rotation Selected"
            : $"{SelectedSavedReport.DisplayTitle} — {SelectedSavedReport.DisplayDate}";

        public string SelectedSavedReportSubtitle => SelectedSavedReport == null
            ? "Select a saved rotation report on the left to inspect its singers and songs."
            : $"File: {SelectedSavedReport.FileName}  |  {SelectedSavedReport.KaraokeSingerCount} Karaoke Singers ({SelectedSavedReport.ActiveKaraokeSingerCount} Sang)  |  {SelectedSavedReport.TotalKaraokeSongs} Songs Performed" +
              (SelectedSavedReport.TotalMusicTracks > 0 ? $"  |  {SelectedSavedReport.TotalMusicTracks} Music Tracks" : string.Empty);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSavedReportSearchText))]
        private string _savedReportSearchText = string.Empty;

        public bool HasSavedReportSearchText => !string.IsNullOrWhiteSpace(SavedReportSearchText);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSavedReportDetailFilterText))]
        private string _savedReportDetailFilterText = string.Empty;

        public bool HasSavedReportDetailFilterText => !string.IsNullOrWhiteSpace(SavedReportDetailFilterText);

        [ObservableProperty]
        private string _savedReportStatusMessage = string.Empty;

        partial void OnSavedReportSearchTextChanged(string value)
        {
            _ = value;
            ApplySavedReportsFilter();
        }

        partial void OnSavedReportDetailFilterTextChanged(string value)
        {
            _ = value;
            ApplySelectedReportDetailsFilter();
        }

        partial void OnSelectedSavedReportChanged(SavedRotationReportItem? value)
        {
            _ = value;
            ApplySelectedReportDetailsFilter();
        }

        [RelayCommand]
        public async Task RefreshSavedReportsAsync()
        {
            string? previousCsvPath = SelectedSavedReport?.CsvFilePath;
            string? previousFileName = SelectedSavedReport?.FileName;

            var loaded = await Task.Run(() => SavedRotationReportReader.LoadAllReports());
            _allSavedReports = loaded;
            ApplySavedReportsFilter(previousCsvPath, previousFileName);
            SavedReportStatusMessage = _allSavedReports.Count == 0
                ? $"No saved rotation lists found in {Globals.KSRotationReportsDir}"
                : $"{_allSavedReports.Count} saved rotation list{(_allSavedReports.Count == 1 ? "" : "s")} loaded";
        }

        [RelayCommand]
        private void ClearSavedReportSearch()
        {
            SavedReportSearchText = string.Empty;
        }

        [RelayCommand]
        private void ClearSavedReportDetailFilter()
        {
            SavedReportDetailFilterText = string.Empty;
        }

        private void ApplySavedReportsFilter(string? preferCsvPath = null, string? preferFileName = null)
        {
            string query = SavedReportSearchText?.Trim() ?? string.Empty;
            IEnumerable<SavedRotationReportItem> source = _allSavedReports;

            if (!string.IsNullOrEmpty(query))
            {
                source = source.Where(r =>
                    r.VenueName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    r.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    r.DisplayDate.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    r.Rows.Any(row =>
                        row.SingerName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        row.DuetPartnerName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        row.SongTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        row.Artist.Contains(query, StringComparison.OrdinalIgnoreCase)));
            }

            FilteredSavedReports.Clear();
            foreach (var item in source)
            {
                FilteredSavedReports.Add(item);
            }

            SavedRotationReportItem? match = null;
            if (!string.IsNullOrWhiteSpace(preferCsvPath))
            {
                match = FilteredSavedReports.FirstOrDefault(r => string.Equals(r.CsvFilePath, preferCsvPath, StringComparison.OrdinalIgnoreCase));
            }
            if (match == null && !string.IsNullOrWhiteSpace(preferFileName))
            {
                match = FilteredSavedReports.FirstOrDefault(r => string.Equals(r.FileName, preferFileName, StringComparison.OrdinalIgnoreCase));
            }

            SelectedSavedReport = match ?? FilteredSavedReports.FirstOrDefault();
        }

        private void ApplySelectedReportDetailsFilter()
        {
            SelectedReportSingerGroups.Clear();
            SelectedReportRows.Clear();

            if (SelectedSavedReport == null)
            {
                return;
            }

            string filter = SavedReportDetailFilterText?.Trim() ?? string.Empty;
            bool hasFilter = !string.IsNullOrEmpty(filter);

            foreach (var group in SelectedSavedReport.SingerGroups)
            {
                if (!hasFilter ||
                    group.SingerName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    group.SummaryLine.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedReportSingerGroups.Add(group);
                }
            }

            foreach (var row in SelectedSavedReport.Rows)
            {
                if (!hasFilter ||
                    row.SingerName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    row.DuetPartnerName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    row.SongTitle.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    row.Artist.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    row.EntryType.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedReportRows.Add(row);
                }
            }
        }

        [RelayCommand]
        private void BrowseSavedReportFile()
        {
#if WPF
            Directory.CreateDirectory(Globals.KSRotationReportsDir);
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open Saved Rotation List",
                InitialDirectory = Globals.KSRotationReportsDir,
                Filter = "Rotation Reports (*.csv;*.pdf)|*.csv;*.pdf|CSV Rotation Lists (*.csv)|*.csv|PDF Reports (*.pdf)|*.pdf|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var item = SavedRotationReportReader.LoadFromFile(dialog.FileName);
                if (item != null)
                {
                    var existing = _allSavedReports.FirstOrDefault(r =>
                        (!string.IsNullOrEmpty(item.CsvFilePath) && string.Equals(r.CsvFilePath, item.CsvFilePath, StringComparison.OrdinalIgnoreCase)) ||
                        string.Equals(r.FileName, item.FileName, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        int idx = _allSavedReports.IndexOf(existing);
                        _allSavedReports[idx] = item;
                    }
                    else
                    {
                        _allSavedReports.Insert(0, item);
                    }

                    SavedReportSearchText = string.Empty;
                    ApplySavedReportsFilter(item.CsvFilePath, item.FileName);
                    SavedReportStatusMessage = $"Opened {item.FileName}";
                }
            }
#endif
        }

        [RelayCommand]
        private void OpenSelectedReportPdf()
        {
            string? pdfPath = SelectedSavedReport?.PdfFilePath;
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.OpenSelectedReportPdf", ex);
            }
        }

        [RelayCommand]
        private void OpenSelectedReportCsv()
        {
            string? csvPath = SelectedSavedReport?.CsvFilePath;
            if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(csvPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.OpenSelectedReportCsv", ex);
            }
        }

        [RelayCommand]
        private void OpenReportsFolder()
        {
            try
            {
                Directory.CreateDirectory(Globals.KSRotationReportsDir);
                Process.Start(new ProcessStartInfo(Globals.KSRotationReportsDir) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.OpenReportsFolder", ex);
            }
        }

        [RelayCommand]
        private void LoadSavedReportSingersIntoRotation()
        {
            if (SelectedSavedReport == null)
            {
                return;
            }

            var karaokeGroups = SelectedSavedReport.SingerGroups
                .Where(g => !g.IsMusic && !string.IsNullOrWhiteSpace(g.SingerName))
                .ToList();

            if (karaokeGroups.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "This saved rotation report does not contain any karaoke singers to load.",
                    "No Singers Found",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            int addedCount = 0;
            foreach (var group in karaokeGroups)
            {
                bool alreadyInQueue = Singers.Any(s =>
                    !s.IsMusic && string.Equals(s.Name, group.SingerName, StringComparison.OrdinalIgnoreCase));

                if (alreadyInQueue)
                {
                    continue;
                }

                var entry = new SingerEntry
                {
                    Name = group.SingerName,
                    Song = string.Empty,
                    Artist = string.Empty
                };
                Singers.Add(entry);
                addedCount++;
            }

            if (addedCount > 0)
            {
                RotationHelpers.EnsureRotationStartFlag(Singers);
                UpdateNextSingerHighlight();
                RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);
                RecalculateRoundEstimation();
                QueueSaveDatabase();
                RebuildRotationJsonCacheNow();
            }

            SavedReportStatusMessage = addedCount > 0
                ? $"Loaded {addedCount} singer{(addedCount == 1 ? "" : "s")} from '{SelectedSavedReport.DisplayTitle}' into active rotation."
                : "All singers from this saved rotation are already in the active rotation.";
        }

        [RelayCommand]
        private void DeleteSelectedSavedReport()
        {
            if (SelectedSavedReport == null)
            {
                return;
            }

            var report = SelectedSavedReport;
            var confirm = System.Windows.MessageBox.Show(
                $"Are you sure you want to permanently delete the saved rotation report '{report.DisplayTitle} ({report.DisplayShortDate})'?",
                "Delete Saved Rotation",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(report.CsvFilePath) && File.Exists(report.CsvFilePath))
                {
                    File.Delete(report.CsvFilePath);
                }
                if (!string.IsNullOrWhiteSpace(report.PdfFilePath) && File.Exists(report.PdfFilePath))
                {
                    File.Delete(report.PdfFilePath);
                }
                if (!string.IsNullOrWhiteSpace(report.EmlFilePath) && File.Exists(report.EmlFilePath))
                {
                    File.Delete(report.EmlFilePath);
                }

                _allSavedReports.Remove(report);
                ApplySavedReportsFilter();
                SavedReportStatusMessage = $"Deleted '{report.FileName}'.";
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.DeleteSelectedSavedReport", ex);
                System.Windows.MessageBox.Show(
                    $"Failed to delete saved rotation report:\n{ex.Message}",
                    "Delete Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
