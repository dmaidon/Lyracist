// Created on Sep 19, 2026 @ 00:00:00 -> Library Health tab: duplicate detection, orphaned-file audit, and readiness/loudness backlog scan
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace LyracistDbEditor;

public class DuplicateGroup
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public ObservableCollection<Song> Songs { get; set; } = [];
    public int Count => Songs.Count;
}

public partial class MainViewModel
{
    // ==========================================
    // LIBRARY HEALTH: DUPLICATE DETECTION
    // ==========================================

    public ObservableCollection<DuplicateGroup> DuplicateGroups { get; } = [];

    [ObservableProperty]
    private bool _isFindingDuplicates;

    [ObservableProperty]
    private string _duplicateStatusText = "Not yet scanned.";

    [ObservableProperty]
    private DuplicateGroup? _selectedDuplicateGroup;

    [ObservableProperty]
    private Song? _selectedDuplicateSong;

    [RelayCommand]
    private async Task FindDuplicates()
    {
        if (IsFindingDuplicates) return;
        IsFindingDuplicates = true;
        DuplicateStatusText = "Scanning for duplicate title/artist matches...";

        try
        {
            var groups = await Task.Run(() =>
            {
                using var context = new LyracistDbContext();
                return context.Songs
                    .AsNoTracking()
                    .ToList()
                    .GroupBy(s => (Title: NormalizeKey(s.Title), Artist: NormalizeKey(s.Artist)))
                    .Where(g => g.Key.Title.Length > 0 && g.Count() > 1)
                    .Select(g => new DuplicateGroup
                    {
                        Title = g.First().Title,
                        Artist = g.First().Artist,
                        Songs = new ObservableCollection<Song>(g.OrderBy(s => s.FilePath))
                    })
                    .OrderByDescending(g => g.Count)
                    .ThenBy(g => g.Title)
                    .ToList();
            });

            DuplicateGroups.Clear();
            foreach (var g in groups) DuplicateGroups.Add(g);

            int totalDupes = groups.Sum(g => g.Count - 1);
            DuplicateStatusText = groups.Count == 0
                ? "No duplicate title/artist matches found."
                : $"Found {groups.Count} duplicate group(s), {totalDupes} redundant track(s).";
        }
        catch (Exception ex)
        {
            DuplicateStatusText = $"Scan failed: {ex.Message}";
            Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor: FindDuplicates failed", ex);
        }
        finally
        {
            IsFindingDuplicates = false;
        }
    }

    [RelayCommand]
    private async Task RemoveDuplicateSong()
    {
        if (SelectedDuplicateSong == null) return;

        var song = SelectedDuplicateSong;
        // The inner song list doesn't select its parent group, so find the owner from the song itself.
        var group = DuplicateGroups.FirstOrDefault(g => g.Songs.Contains(song));
        if (group == null) return;
        var confirm = System.Windows.MessageBox.Show(
            $"Remove '{song.Title}' by '{song.Artist}' ({song.FilePath}) from the database?\nThis will not delete the physical file.",
            "Confirm Remove Duplicate",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            using var context = new LyracistDbContext();
            var dbSong = await context.Songs.FirstOrDefaultAsync(s => s.SongId == song.SongId);
            if (dbSong != null)
            {
                var searchService = new SearchService(context);
                context.Songs.Remove(dbSong);
                await context.SaveChangesAsync();
                await searchService.RemoveSongFromIndex(dbSong.SongId);
            }

            SelectedDuplicateSong = null;
            group.Songs.Remove(song);
            if (group.Songs.Count <= 1)
            {
                DuplicateGroups.Remove(group);
                if (SelectedDuplicateGroup == group) SelectedDuplicateGroup = null;
            }

            RefreshStats();
            Search();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to remove duplicate: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private static string NormalizeKey(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();

    // ==========================================
    // LIBRARY HEALTH: ORPHANED FILE AUDIT
    // ==========================================

    public ObservableCollection<Song> OrphanedSongs { get; } = [];

    [ObservableProperty]
    private bool _isFindingOrphans;

    [ObservableProperty]
    private string _orphanStatusText = "Not yet scanned.";

    [RelayCommand]
    private async Task FindOrphanedFiles()
    {
        if (IsFindingOrphans) return;
        IsFindingOrphans = true;
        OrphanStatusText = "Checking database entries against disk...";

        try
        {
            int skippedUnavailable = 0;
            var orphans = await Task.Run(() =>
            {
                using var context = new LyracistDbContext();
                // A drive/share that isn't mounted would make every song on it look orphaned, so
                // songs whose root is unreachable are skipped rather than reported.
                var rootAvailable = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                bool RootIsAvailable(string path)
                {
                    string root;
                    try { root = Path.GetPathRoot(path) ?? string.Empty; }
                    catch { return true; }
                    if (root.Length == 0) return true;
                    if (!rootAvailable.TryGetValue(root, out bool ok))
                    {
                        try { ok = Directory.Exists(root); } catch { ok = false; }
                        rootAvailable[root] = ok;
                    }
                    return ok;
                }

                var list = new List<Song>();
                foreach (var s in context.Songs.AsNoTracking().ToList())
                {
                    if (string.IsNullOrWhiteSpace(s.FilePath)) { list.Add(s); continue; }
                    if (!RootIsAvailable(s.FilePath)) { skippedUnavailable++; continue; }
                    if (!File.Exists(s.FilePath)) list.Add(s);
                }
                return list.OrderBy(s => s.Artist).ThenBy(s => s.Title).ToList();
            });

            OrphanedSongs.Clear();
            foreach (var s in orphans) OrphanedSongs.Add(s);

            string skippedNote = skippedUnavailable > 0
                ? $" Skipped {skippedUnavailable} song(s) on drives/shares that aren't currently available - reconnect them and scan again."
                : string.Empty;
            OrphanStatusText = (orphans.Count == 0
                ? "No orphaned entries found. Every reachable song row points to a file on disk."
                : $"Found {orphans.Count} orphaned entry(ies) with missing files.") + skippedNote;
        }
        catch (Exception ex)
        {
            OrphanStatusText = $"Scan failed: {ex.Message}";
            Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor: FindOrphanedFiles failed", ex);
        }
        finally
        {
            IsFindingOrphans = false;
        }
    }

    [RelayCommand]
    private async Task RemoveAllOrphanedFiles()
    {
        if (OrphanedSongs.Count == 0) return;

        var confirm = System.Windows.MessageBox.Show(
            $"Remove all {OrphanedSongs.Count} orphaned entry(ies) from the database?\nThis only removes database rows; no physical files are touched.",
            "Confirm Remove Orphaned Entries",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            var ids = OrphanedSongs.Select(s => s.SongId).ToList();

            using var context = new LyracistDbContext();
            var searchService = new SearchService(context);
            var dbSongs = await context.Songs.Where(s => ids.Contains(s.SongId)).ToListAsync();

            context.Songs.RemoveRange(dbSongs);
            await context.SaveChangesAsync();

            foreach (var id in ids)
            {
                await searchService.RemoveSongFromIndex(id);
            }

            OrphanedSongs.Clear();
            OrphanStatusText = $"Removed {dbSongs.Count} orphaned entry(ies).";

            RefreshStats();
            Search();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to remove orphaned entries: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    // ==========================================
    // LIBRARY HEALTH: QUEUE-READINESS & LOUDNESS BACKLOG
    // ==========================================

    [ObservableProperty]
    private int _missingReadinessCount;

    [ObservableProperty]
    private int _missingLoudnessCount;

    [ObservableProperty]
    private bool _isRunningReadinessAudit;

    [ObservableProperty]
    private double _readinessAuditProgressPercent;

    [ObservableProperty]
    private string _readinessAuditStatusText = "Idle";

    public ObservableCollection<string> ReadinessAuditLog { get; } = [];

    private CancellationTokenSource? _readinessCts;

    [RelayCommand]
    private void RefreshReadinessCounts()
    {
        try
        {
            using var context = new LyracistDbContext();
            MissingReadinessCount = context.Songs.Count(s =>
                s.IsKaraoke && (s.Key == null || s.BPM == null || s.Difficulty == null));
            MissingLoudnessCount = context.Songs.Count(s => s.MeasuredLoudnessLufs == null);
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor: RefreshReadinessCounts failed", ex);
        }
    }

    [RelayCommand]
    private void StartReadinessAudit()
    {
        if (IsRunningReadinessAudit) return;

        IsRunningReadinessAudit = true;
        _readinessCts = new CancellationTokenSource();
        ReadinessAuditLog.Clear();
        ReadinessAuditProgressPercent = 0;
        ReadinessAuditStatusText = "Preparing readiness audit...";

        Task.Run(() => RunReadinessAuditAsync(_readinessCts.Token));
    }

    [RelayCommand]
    private void StopReadinessAudit()
    {
        if (!IsRunningReadinessAudit) return;
        _readinessCts?.Cancel();
        IsRunningReadinessAudit = false;
        ReadinessAuditStatusText = "Audit stopped by host.";
    }

    private async Task RunReadinessAuditAsync(CancellationToken token)
    {
        try
        {
            using var context = new LyracistDbContext();

            // Two independent backlogs share one pass: fill in Key/BPM/Difficulty for karaoke
            // tracks missing them, and measure integrated loudness for anything never processed —
            // both reuse the same FFmpegService probes Lyracist's own smart-import pipeline uses.
            var targets = await context.Songs
                .Where(s => (s.IsKaraoke && (s.Key == null || s.BPM == null || s.Difficulty == null))
                            || s.MeasuredLoudnessLufs == null)
                .ToListAsync(token);

            int total = targets.Count;
            if (total == 0)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ReadinessAuditStatusText = "Nothing to process — library is fully audited.";
                    IsRunningReadinessAudit = false;
                });
                return;
            }

            int processed = 0;
            int updatedCount = 0;

            foreach (var song in targets)
            {
                if (token.IsCancellationRequested) break;

                processed++;
                double percentage = (double)processed / total * 100;
                string currentFile = Path.GetFileName(song.FilePath);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ReadinessAuditProgressPercent = percentage;
                    ReadinessAuditStatusText = $"Processing {processed}/{total}: {currentFile}";
                });

                if (!File.Exists(song.FilePath))
                {
                    LogReadiness($"Skipped (file missing): {currentFile}");
                    continue;
                }

                bool changed = false;

                if (song.IsKaraoke && (song.Key == null || song.BPM == null || song.Difficulty == null))
                {
                    var probe = await FFprobeRunner.ProbeFile(song.FilePath);
                    song.Key ??= FFmpegService.DetectKey(probe);
                    song.BPM ??= FFmpegService.DetectBpm(probe);
                    song.Difficulty ??= FFmpegService.DetectDifficulty(probe);
                    changed = true;
                }

                if (song.MeasuredLoudnessLufs == null)
                {
                    song.MeasuredLoudnessLufs = await FFmpegService.MeasureIntegratedLoudness(song.FilePath);
                    changed = true;
                }

                if (changed)
                {
                    await context.SaveChangesAsync(token);
                    updatedCount++;
                    LogReadiness($"Updated: {currentFile}");
                }
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ReadinessAuditStatusText = token.IsCancellationRequested
                    ? $"Audit canceled. Updated {updatedCount}/{processed} track(s)."
                    : $"Audit complete. Updated {updatedCount} track(s).";
                IsRunningReadinessAudit = false;
                RefreshReadinessCounts();
            });
        }
        catch (OperationCanceledException)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ReadinessAuditStatusText = "Audit canceled.";
                IsRunningReadinessAudit = false;
            });
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor: Readiness audit failed", ex);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ReadinessAuditStatusText = $"Error: {ex.Message}";
                IsRunningReadinessAudit = false;
            });
        }
    }

    private void LogReadiness(string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ReadinessAuditLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
            if (ReadinessAuditLog.Count > 100) ReadinessAuditLog.RemoveAt(ReadinessAuditLog.Count - 1);
        });
    }
}
