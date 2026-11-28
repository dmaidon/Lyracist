// Edited on Nov 27, 2026 @ 13:12:00 -> Add Broken MP3+G Pair & Corrupt Archive Audit, Multi-Rig USB Drive Synchronizer, and Built-In CDG Karaoke Player with 10-Band EQ
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace LyracistDbEditor;

public sealed class BrokenFileIssue
{
    public int? SongId { get; init; }
    public string IssueType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class DriveSyncCandidate
{
    public string RelativePath { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string Status { get; init; } = "New";
    public string FormattedSize => SizeBytes >= 1024 * 1024
        ? $"{SizeBytes / (1024.0 * 1024.0):F2} MB"
        : $"{Math.Max(1, SizeBytes / 1024)} KB";
}

public class DuplicateGroup
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public ObservableCollection<Song> Songs { get; set; } = [];
    public int Count => Songs.Count;

    /// <summary>The copy a bulk clean-up would keep: file exists, then longest duration, then largest file.</summary>
    public Song? Keeper { get; set; }
    public string KeeperText => Keeper == null ? string.Empty : "Suggested keep: " + System.IO.Path.GetFileName(Keeper.FilePath);
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
                var list = context.Songs
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

                foreach (var g in list)
                {
                    g.Keeper = PickKeeper(g.Songs);
                }

                return list;
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

    private static Song PickKeeper(System.Collections.Generic.IEnumerable<Song> songs)
    {
        static long SizeOf(string path)
        {
            try { return new FileInfo(path).Length; } catch { return -1; }
        }

        // Real file first, then longest duration, then largest file (a rough stand-in for bitrate).
        return songs
            .Select(s => (Song: s, Exists: File.Exists(s.FilePath), Size: SizeOf(s.FilePath)))
            .OrderByDescending(x => x.Exists)
            .ThenByDescending(x => x.Song.Duration)
            .ThenByDescending(x => x.Size)
            .ThenBy(x => x.Song.FilePath, StringComparer.OrdinalIgnoreCase)
            .First().Song;
    }

    [RelayCommand]
    private async Task RemoveAllButSuggestedDuplicates()
    {
        var targets = DuplicateGroups
            .Where(g => g.Keeper != null)
            .SelectMany(g => g.Songs.Where(s => s.SongId != g.Keeper!.SongId))
            .ToList();
        if (targets.Count == 0) return;

        var confirm = System.Windows.MessageBox.Show(
            $"Remove {targets.Count:N0} redundant song(s) from the database, keeping one copy per group?\n\nThe kept copy is the one whose file exists, then the longest duration, then the largest file. No physical files are deleted. A backup is made first.",
            "Confirm Bulk Duplicate Removal",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        if (!await DatabaseBackup.EnsureBackupAsync("RemoveDuplicates")) return;

        try
        {
            var ids = targets.Select(s => s.SongId).ToList();
            var paths = targets.Select(s => s.FilePath).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            using var context = new LyracistDbContext();
            var searchService = new SearchService(context);
            var dbSongs = await context.Songs.Where(s => ids.Contains(s.SongId)).ToListAsync();
            context.Songs.RemoveRange(dbSongs);
            await context.SaveChangesAsync();
            await searchService.RemoveSongsFromIndexBatch(ids);
            await NoArtistStore.RemoveBatchAsync(paths);

            DuplicateGroups.Clear();
            SelectedDuplicateGroup = null;
            SelectedDuplicateSong = null;
            DuplicateStatusText = $"Removed {dbSongs.Count:N0} redundant song(s). Scan again to re-check.";
            StatusMessage = DuplicateStatusText;

            RefreshStats();
            Search();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to remove duplicates: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                await NoArtistStore.RemoveAsync(dbSong.FilePath);
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
    private Song? _selectedOrphanedSong;

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
            $"Remove all {OrphanedSongs.Count:N0} orphaned entry(ies) from the database?\nThis only removes database rows; no physical files are touched. A backup is made first.",
            "Confirm Remove Orphaned Entries",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        if (!await DatabaseBackup.EnsureBackupAsync("RemoveOrphans")) return;

        try
        {
            var ids = OrphanedSongs.Select(s => s.SongId).ToList();
            var paths = OrphanedSongs.Select(s => s.FilePath).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

            using var context = new LyracistDbContext();
            var searchService = new SearchService(context);
            var dbSongs = await context.Songs.Where(s => ids.Contains(s.SongId)).ToListAsync();

            context.Songs.RemoveRange(dbSongs);
            await context.SaveChangesAsync();

            await searchService.RemoveSongsFromIndexBatch(ids);
            await NoArtistStore.RemoveBatchAsync(paths);

            OrphanedSongs.Clear();
            SelectedOrphanedSong = null;
            OrphanStatusText = $"Removed {dbSongs.Count} orphaned entry(ies).";

            RefreshStats();
            Search();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to remove orphaned entries: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedOrphanedFile()
    {
        if (SelectedOrphanedSong == null) return;
        var song = SelectedOrphanedSong;

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
                await NoArtistStore.RemoveAsync(dbSong.FilePath);
            }

            OrphanedSongs.Remove(song);
            SelectedOrphanedSong = OrphanedSongs.FirstOrDefault();
            OrphanStatusText = OrphanedSongs.Count == 0
                ? "All orphaned entries removed."
                : $"Removed '{song.Title}'. {OrphanedSongs.Count} orphaned entry(ies) remaining.";

            RefreshStats();
            Search();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to remove orphaned entry: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
    private int _missingBpmCount;

    [ObservableProperty]
    private bool _isRunningReadinessAudit;

    [ObservableProperty]
    private double _readinessAuditProgressPercent;

    [ObservableProperty]
    private string _readinessAuditStatusText = "Idle";

    public ObservableCollection<string> ReadinessAuditLog { get; } = [];

    private CancellationTokenSource? _readinessCts;
    private const string AuditSlotName = "Readiness audit";

    [RelayCommand]
    private void RefreshReadinessCounts()
    {
        try
        {
            using var context = new LyracistDbContext();
            MissingReadinessCount = context.Songs.Count(s =>
                s.IsKaraoke && (s.Key == null || s.BPM == null || s.Difficulty == null));
            MissingLoudnessCount = context.Songs.Count(s => s.MeasuredLoudnessLufs == null);
            var fillerIds = GetFillerSongIds(context);
            MissingBpmCount = context.Songs.Count(s => !s.IsKaraoke && s.BPM == null && fillerIds.Contains(s.SongId));
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

        if (!_operation.TryBegin(AuditSlotName))
        {
            StatusMessage = $"Can't start: {_operation.Current} is already running.";
            return;
        }

        IsRunningReadinessAudit = true;
        var cts = new CancellationTokenSource();
        _readinessCts = cts;
        ReadinessAuditLog.Clear();
        ReadinessAuditProgressPercent = 0;
        ReadinessAuditStatusText = "Preparing readiness audit...";

        _ = Task.Run(async () =>
        {
            try
            {
                await RunReadinessAuditAsync(cts.Token);
            }
            finally
            {
                // The flag only clears once the task has really finished, so a new audit can't
                // overlap one that is still winding down.
                RunOnUi(() => IsRunningReadinessAudit = false);
                _operation.End(AuditSlotName);
                if (ReferenceEquals(_readinessCts, cts)) _readinessCts = null;
                cts.Dispose();
            }
        });
    }

    [RelayCommand]
    private void StopReadinessAudit()
    {
        if (!IsRunningReadinessAudit) return;
        _readinessCts?.Cancel();
        ReadinessAuditStatusText = "Stopping audit...";
    }

    // BPM and cue-point analysis only matter for tracks that can actually play as background music, so the
    // audit is limited to songs in the Opening / Fill-In / End-of-Rotation playlists instead of every
    // non-karaoke track in the library (each analysis decodes audio, which is slow on a big library).
    private static List<int> GetFillerSongIds(LyracistDbContext context)
    {
        return context.OpeningPlaylistItems.Select(i => i.SongId)
            .Concat(context.FillInPlaylistItems.Select(i => i.SongId))
            .Concat(context.EndRotationPlaylistItems.Select(i => i.SongId))
            .Distinct()
            .ToList();
    }

    private async Task RunReadinessAuditAsync(CancellationToken token)
    {
        try
        {
            using var context = new LyracistDbContext();
            var fillerIds = GetFillerSongIds(context);
            var fillerSet = fillerIds.ToHashSet();

            // Audits key/BPM/difficulty for karaoke tracks, integrated loudness for any track,
            // and BPM analysis / mix-in/mix-out cue point detection for background filler tracks.
            var targets = await context.Songs
                .Where(s => (s.IsKaraoke && (s.Key == null || s.BPM == null || s.Difficulty == null))
                            || s.MeasuredLoudnessLufs == null
                            || (!s.IsKaraoke && fillerIds.Contains(s.SongId)
                                && (s.BPM == null || (s.MixInMs == null && s.MixOutMs == null))))
                .ToListAsync(token);

            int total = targets.Count;
            if (total == 0)
            {
                RunOnUi(() =>
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

                RunOnUi(() =>
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

                // Filler BPM analysis (Feature D): check tags first, then audio analysis
                if (!song.IsKaraoke && fillerSet.Contains(song.SongId) && song.BPM == null)
                {
                    var probe = await FFprobeRunner.ProbeFile(song.FilePath);
                    double? tagBpm = FFmpegService.DetectBpm(probe);
                    if (tagBpm.HasValue)
                    {
                        song.BPM = tagBpm.Value;
                        changed = true;
                    }
                    else
                    {
                        int? detected = await BpmAnalyzer.AnalyzeBpmAsync(song.FilePath, song.Duration, token);
                        if (detected.HasValue)
                        {
                            song.BPM = detected.Value;
                            changed = true;
                        }
                    }
                }

                // Filler cue points detection (Feature E): silencedetect
                if (!song.IsKaraoke && fillerSet.Contains(song.SongId) && song.MixInMs == null && song.MixOutMs == null)
                {
                    var (mixIn, mixOut) = await FFmpegService.DetectCuePointsAsync(song.FilePath, song.Duration, token);

                    // Nothing found (no leading/trailing silence) is recorded as 0/0 - the player ignores cue
                    // points <= 0 - so a clean track isn't re-decoded in full on every future audit.
                    song.MixInMs = mixIn ?? 0;
                    song.MixOutMs = mixOut ?? 0;
                    changed = true;
                }

                if (changed)
                {
                    await context.SaveChangesAsync(token);
                    updatedCount++;
                    LogReadiness($"Updated: {currentFile}");
                }
            }

            RunOnUi(() =>
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
            RunOnUi(() =>
            {
                ReadinessAuditStatusText = "Audit canceled.";
                IsRunningReadinessAudit = false;
            });
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor: Readiness audit failed", ex);
            RunOnUi(() =>
            {
                ReadinessAuditStatusText = $"Error: {ex.Message}";
                IsRunningReadinessAudit = false;
            });
        }
    }

    private void LogReadiness(string message)
    {
        RunOnUi(() =>
        {
            ReadinessAuditLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
            if (ReadinessAuditLog.Count > 100) ReadinessAuditLog.RemoveAt(ReadinessAuditLog.Count - 1);
        });
    }

    // ==========================================
    // LIBRARY HEALTH: BROKEN MP3+G PAIR & CORRUPT ARCHIVE AUDIT
    // ==========================================

    public ObservableCollection<BrokenFileIssue> BrokenFileIssues { get; } = [];

    [ObservableProperty]
    private BrokenFileIssue? _selectedBrokenIssue;

    [ObservableProperty]
    private bool _isFindingBrokenFiles;

    [ObservableProperty]
    private string _brokenFilesStatusText = "Not yet scanned.";

    [RelayCommand]
    private async Task FindBrokenPairsAndArchives()
    {
        if (IsFindingBrokenFiles) return;
        IsFindingBrokenFiles = true;
        BrokenFilesStatusText = "Auditing MP3+G pairs, CDG companions, and ZIP archives...";

        try
        {
            var dirsSnapshot = LibraryDirectories.ToList();
            var issues = await Task.Run(() =>
            {
                var found = new List<BrokenFileIssue>();
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using var context = new LyracistDbContext();
                var allSongs = context.Songs.AsNoTracking().ToList();

                // 1. Check cataloged songs for zero-byte files, missing .cdg companions, or corrupt/incomplete .zip archives
                foreach (var s in allSongs)
                {
                    if (string.IsNullOrWhiteSpace(s.FilePath) || !File.Exists(s.FilePath)) continue;

                    try
                    {
                        var fi = new FileInfo(s.FilePath);
                        if (fi.Length == 0)
                        {
                            found.Add(new BrokenFileIssue
                            {
                                SongId = s.SongId,
                                IssueType = "Zero-Byte File",
                                Title = $"{s.Artist} — {s.Title}",
                                FilePath = s.FilePath,
                                Description = "File size is 0 bytes (corrupt or incomplete download)."
                            });
                            seenPaths.Add(s.FilePath);
                            continue;
                        }

                        string ext = fi.Extension.ToLowerInvariant();
                        // "Original" uploads and LRC-lyric tracks are audio-only on purpose.
                        bool expectsCdg = s.KaraokeType == "MP3G"
                            || (s.IsKaraoke && (ext == ".mp3" || ext == ".wav")
                                && s.KaraokeType != "Original"
                                && !File.Exists(Path.ChangeExtension(s.FilePath, ".lrc")));
                        if (expectsCdg)
                        {
                            string cdgPath = Path.ChangeExtension(s.FilePath, ".cdg");
                            if (!File.Exists(cdgPath))
                            {
                                found.Add(new BrokenFileIssue
                                {
                                    SongId = s.SongId,
                                    IssueType = "Missing .CDG Pair",
                                    Title = $"{s.Artist} — {s.Title}",
                                    FilePath = s.FilePath,
                                    Description = "Karaoke audio file is missing its companion .cdg lyrics file."
                                });
                                seenPaths.Add(s.FilePath);
                            }
                            else if (new FileInfo(cdgPath).Length < 96)
                            {
                                found.Add(new BrokenFileIssue
                                {
                                    SongId = s.SongId,
                                    IssueType = "Corrupt .CDG",
                                    Title = $"{s.Artist} — {s.Title}",
                                    FilePath = cdgPath,
                                    Description = "Companion .cdg file is empty or truncated (< 96 bytes)."
                                });
                                seenPaths.Add(cdgPath);
                            }
                        }
                        else if (ext == ".zip")
                        {
                            try
                            {
                                using var archive = ZipFile.OpenRead(s.FilePath);
                                bool hasCdg = archive.Entries.Any(e => e.FullName.EndsWith(".cdg", StringComparison.OrdinalIgnoreCase) && e.Length >= 96);
                                bool hasAudio = archive.Entries.Any(e =>
                                    (e.FullName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                     e.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                                     e.FullName.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) ||
                                     e.FullName.EndsWith(".flac", StringComparison.OrdinalIgnoreCase)) && e.Length > 0);

                                if (!hasCdg || !hasAudio)
                                {
                                    found.Add(new BrokenFileIssue
                                    {
                                        SongId = s.SongId,
                                        IssueType = "Incomplete ZIP+G",
                                        Title = $"{s.Artist} — {s.Title}",
                                        FilePath = s.FilePath,
                                        Description = !hasCdg && !hasAudio
                                            ? "ZIP archive contains neither audio nor .cdg graphics."
                                            : (!hasCdg ? "ZIP archive is missing a valid .cdg entry." : "ZIP archive is missing an audio (.mp3/.wav) entry.")
                                    });
                                    seenPaths.Add(s.FilePath);
                                }
                            }
                            catch (Exception zipEx)
                            {
                                found.Add(new BrokenFileIssue
                                {
                                    SongId = s.SongId,
                                    IssueType = "Corrupt ZIP",
                                    Title = $"{s.Artist} — {s.Title}",
                                    FilePath = s.FilePath,
                                    Description = $"ZIP header corrupt: {zipEx.Message}"
                                });
                                seenPaths.Add(s.FilePath);
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                // 2. Scan registered library folders for orphaned .cdg files that have no matching .mp3/.wav companion
                foreach (string dir in dirsSnapshot)
                {
                    if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
                    try
                    {
                        foreach (string cdgFile in Directory.EnumerateFiles(dir, "*.cdg", SearchOption.AllDirectories))
                        {
                            if (seenPaths.Contains(cdgFile)) continue;
                            string mp3Path = Path.ChangeExtension(cdgFile, ".mp3");
                            string wavPath = Path.ChangeExtension(cdgFile, ".wav");
                            if (!File.Exists(mp3Path) && !File.Exists(wavPath))
                            {
                                found.Add(new BrokenFileIssue
                                {
                                    SongId = null,
                                    IssueType = "Orphan .CDG (No Audio)",
                                    Title = Path.GetFileNameWithoutExtension(cdgFile),
                                    FilePath = cdgFile,
                                    Description = "Standalone .cdg file on disk with no matching .mp3 or .wav audio file."
                                });
                                seenPaths.Add(cdgFile);
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                return found.OrderBy(f => f.IssueType).ThenBy(f => f.Title).ToList();
            });

            BrokenFileIssues.Clear();
            foreach (var issue in issues)
            {
                BrokenFileIssues.Add(issue);
            }

            BrokenFilesStatusText = issues.Count == 0
                ? "All MP3+G pairs and ZIP archives are healthy!"
                : $"Found {issues.Count:N0} broken pair / archive issue(s).";
        }
        catch (Exception ex)
        {
            BrokenFilesStatusText = $"Audit failed: {ex.Message}";
        }
        finally
        {
            IsFindingBrokenFiles = false;
        }
    }

    [RelayCommand]
    private void OpenBrokenIssueFolder()
    {
        if (SelectedBrokenIssue == null || string.IsNullOrWhiteSpace(SelectedBrokenIssue.FilePath)) return;
        try
        {
            if (File.Exists(SelectedBrokenIssue.FilePath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{SelectedBrokenIssue.FilePath}\"") { UseShellExecute = true });
            }
            else
            {
                string? dir = Path.GetDirectoryName(SelectedBrokenIssue.FilePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task RemoveSelectedBrokenIssue()
    {
        if (SelectedBrokenIssue == null) return;
        var issue = SelectedBrokenIssue;

        if (issue.SongId.HasValue)
        {
            var confirm = System.Windows.MessageBox.Show(
                $"Remove this song from the library database?\n\n{issue.Title}\n{issue.FilePath}\n\nThe file on disk is not deleted.",
                "Confirm Remove",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (confirm != System.Windows.MessageBoxResult.Yes) return;
        }

        if (issue.SongId.HasValue)
        {
            try
            {
                using var context = new LyracistDbContext();
                var dbSong = await context.Songs.FirstOrDefaultAsync(s => s.SongId == issue.SongId.Value);
                if (dbSong != null)
                {
                    var searchService = new SearchService(context);
                    context.Songs.Remove(dbSong);
                    await context.SaveChangesAsync();
                    await searchService.RemoveSongFromIndex(dbSong.SongId);
                    await NoArtistStore.RemoveAsync(dbSong.FilePath);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to remove database entry: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
        }

        BrokenFileIssues.Remove(issue);
        SelectedBrokenIssue = BrokenFileIssues.FirstOrDefault();
        BrokenFilesStatusText = BrokenFileIssues.Count == 0
            ? "All listed broken entries cleared."
            : $"{BrokenFileIssues.Count:N0} issue(s) remaining.";
        RefreshStats();
        Search();
    }

    // ==========================================
    // MULTI-RIG / EXTERNAL USB DRIVE LIBRARY SYNCHRONIZER
    // ==========================================

    [ObservableProperty]
    private string _syncSourceFolderPath = string.Empty;

    [ObservableProperty]
    private string _syncTargetFolderPath = string.Empty;

    [ObservableProperty]
    private bool _isDriveSyncRunning;

    [ObservableProperty]
    private double _driveSyncProgressPercent;

    [ObservableProperty]
    private string _driveSyncStatusText = "Select Source and Target folders, then click Compare Drives.";

    [ObservableProperty]
    private string _driveSyncDiskSpaceText = "Target Drive Space: Not analyzed yet.";

    public ObservableCollection<DriveSyncCandidate> DriveSyncCandidates { get; } = [];

    // A comparison is only valid for the folders it was made against.
    partial void OnSyncSourceFolderPathChanged(string value) => ClearDriveComparison();
    partial void OnSyncTargetFolderPathChanged(string value) => ClearDriveComparison();

    private void ClearDriveComparison()
    {
        if (DriveSyncCandidates.Count == 0) return;
        DriveSyncCandidates.Clear();
        DriveSyncStatusText = "Folders changed. Click Compare Drives again.";
    }

    private static bool IsSameOrInside(string path, string root) =>
        (path + Path.DirectorySeparatorChar).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private long? GetTargetFreeBytes()
    {
        try
        {
            string? root = Path.GetPathRoot(Path.GetFullPath(SyncTargetFolderPath));
            if (string.IsNullOrEmpty(root)) return null;
            var drive = new DriveInfo(root);
            return drive.IsReady ? drive.AvailableFreeSpace : null;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null; // e.g. a network path, where free space can't be read
        }
    }

    private CancellationTokenSource? _driveSyncCts;

    public void UpdateTargetDriveDiskSpaceInfo()
    {
        if (string.IsNullOrWhiteSpace(SyncTargetFolderPath))
        {
            DriveSyncDiskSpaceText = "Target Drive Space: Select a target folder.";
            return;
        }

        try
        {
            string? root = Path.GetPathRoot(Path.GetFullPath(SyncTargetFolderPath));
            if (string.IsNullOrEmpty(root))
            {
                DriveSyncDiskSpaceText = "Target Drive Space: Unknown drive root.";
                return;
            }

            var di = new DriveInfo(root);
            if (!di.IsReady)
            {
                DriveSyncDiskSpaceText = $"Drive {root} is not ready.";
                return;
            }

            double freeGb = di.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            double totalGb = di.TotalSize / (1024.0 * 1024.0 * 1024.0);
            long requiredBytes = DriveSyncCandidates.Sum(c => c.SizeBytes);
            double reqGb = requiredBytes / (1024.0 * 1024.0 * 1024.0);

            string fitNote = requiredBytes > 0
                ? (di.AvailableFreeSpace >= requiredBytes ? $" • Required: {reqGb:F2} GB (OK)" : $" • Required: {reqGb:F2} GB (⚠️ INSUFFICIENT SPACE)")
                : string.Empty;

            DriveSyncDiskSpaceText = $"Drive {di.Name} ({di.VolumeLabel}): {freeGb:F2} GB free of {totalGb:F2} GB{fitNote}";
        }
        catch (Exception ex)
        {
            DriveSyncDiskSpaceText = $"Drive analysis unavailable: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CompareDrivesForSync()
    {
        if (IsDriveSyncRunning) return;
        if (string.IsNullOrWhiteSpace(SyncSourceFolderPath) || !Directory.Exists(SyncSourceFolderPath))
        {
            System.Windows.MessageBox.Show("Please select a valid Source library folder first.", "Source Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(SyncTargetFolderPath))
        {
            System.Windows.MessageBox.Show("Please select a Target backup / USB drive folder.", "Target Required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        string srcRoot = Path.GetFullPath(SyncSourceFolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string dstRoot = Path.GetFullPath(SyncTargetFolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (IsSameOrInside(dstRoot, srcRoot) || IsSameOrInside(srcRoot, dstRoot))
        {
            System.Windows.MessageBox.Show("Source and Target must be separate folders, and neither can be inside the other.", "Invalid Folders", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        IsDriveSyncRunning = true;
        DriveSyncStatusText = "Comparing source library against target drive...";
        DriveSyncProgressPercent = 0;

        try
        {
            var supportedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mp3", ".cdg", ".zip", ".mp4", ".wav", ".flac", ".m4a", ".wma", ".ogg", ".lrc", ".txt"
            };

            var (candidates, totalSourceFiles) = await Task.Run(() =>
            {
                var list = new List<DriveSyncCandidate>();
                int srcCount = 0;

                foreach (string srcFile in Directory.EnumerateFiles(srcRoot, "*.*", SearchOption.AllDirectories))
                {
                    if (!supportedExts.Contains(Path.GetExtension(srcFile))) continue;
                    srcCount++;

                    string rel = Path.GetRelativePath(srcRoot, srcFile);
                    string dstFile = Path.Combine(dstRoot, rel);
                    var srcInfo = new FileInfo(srcFile);

                    if (!File.Exists(dstFile))
                    {
                        list.Add(new DriveSyncCandidate
                        {
                            RelativePath = rel,
                            SourcePath = srcFile,
                            TargetPath = dstFile,
                            SizeBytes = srcInfo.Length,
                            Status = "New File"
                        });
                    }
                    else
                    {
                        var dstInfo = new FileInfo(dstFile);
                        if (dstInfo.Length != srcInfo.Length)
                        {
                            list.Add(new DriveSyncCandidate
                            {
                                RelativePath = rel,
                                SourcePath = srcFile,
                                TargetPath = dstFile,
                                SizeBytes = srcInfo.Length,
                                Status = "Modified Size"
                            });
                        }
                    }
                }
                return (list, srcCount);
            });

            DriveSyncCandidates.Clear();
            foreach (var c in candidates)
            {
                DriveSyncCandidates.Add(c);
            }

            UpdateTargetDriveDiskSpaceInfo();
            double totalMb = candidates.Sum(c => c.SizeBytes) / (1024.0 * 1024.0);
            DriveSyncStatusText = candidates.Count == 0
                ? $"Target drive is 100% in sync! ({totalSourceFiles:N0} files checked)."
                : $"Found {candidates.Count:N0} file(s) to sync ({totalMb:N1} MB) out of {totalSourceFiles:N0} source files.";
        }
        catch (Exception ex)
        {
            DriveSyncStatusText = $"Compare failed: {ex.Message}";
        }
        finally
        {
            IsDriveSyncRunning = false;
        }
    }

    [RelayCommand]
    private async Task ExecuteDriveSync()
    {
        if (IsDriveSyncRunning) return;
        if (DriveSyncCandidates.Count == 0)
        {
            await CompareDrivesForSync();
            if (DriveSyncCandidates.Count == 0) return;
        }

        var itemsToCopy = DriveSyncCandidates.ToList();
        long totalBytes = itemsToCopy.Sum(c => c.SizeBytes);
        double totalMb = totalBytes / (1024.0 * 1024.0);

        if (GetTargetFreeBytes() is long freeBytes && freeBytes < totalBytes)
        {
            System.Windows.MessageBox.Show(
                $"The target drive needs {totalMb:N1} MB but has only {freeBytes / (1024.0 * 1024.0):N1} MB free. Free up space and try again.",
                "Not Enough Space",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"Copy {itemsToCopy.Count:N0} new/updated file(s) ({totalMb:N1} MB) to:\n{SyncTargetFolderPath}?",
            "Confirm Multi-Rig / USB Drive Sync",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        IsDriveSyncRunning = true;
        var cts = new CancellationTokenSource();
        _driveSyncCts = cts;
        var token = cts.Token;

        try
        {
            int copied = 0;
            int total = itemsToCopy.Count;
            var done = new List<DriveSyncCandidate>();
            var failures = new List<string>();

            await Task.Run(() =>
            {
                foreach (var item in itemsToCopy)
                {
                    if (token.IsCancellationRequested) break;

                    // Copy under a temp name and swap in, so an unplugged drive can't leave a half-written song.
                    string partialPath = item.TargetPath + ".partial";
                    try
                    {
                        string? targetDir = Path.GetDirectoryName(item.TargetPath);
                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        File.Copy(item.SourcePath, partialPath, overwrite: true);
                        File.Move(partialPath, item.TargetPath, overwrite: true);
                        copied++;
                        done.Add(item);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        failures.Add($"{item.RelativePath}: {ex.Message}");
                        try { File.Delete(partialPath); }
                        catch (Exception cleanupEx) when (cleanupEx is IOException or UnauthorizedAccessException) { }
                    }

                    int cur = copied + failures.Count;
                    double pct = (double)cur / total * 100.0;
                    RunOnUi(() =>
                    {
                        DriveSyncProgressPercent = pct;
                        DriveSyncStatusText = $"Syncing {cur:N0} / {total:N0}: {item.RelativePath}";
                    });
                }
            }, token);

            // Whatever wasn't copied stays listed so it can be retried.
            foreach (var item in done)
            {
                DriveSyncCandidates.Remove(item);
            }
            UpdateTargetDriveDiskSpaceInfo();
            DriveSyncStatusText = token.IsCancellationRequested
                ? $"Sync cancelled after copying {copied:N0} file(s)."
                : failures.Count == 0
                    ? $"Drive Sync complete! Copied {copied:N0} file(s) successfully."
                    : $"Copied {copied:N0} file(s); {failures.Count:N0} failed. First error: {failures[0]}";
        }
        catch (OperationCanceledException)
        {
            DriveSyncStatusText = "Drive Sync cancelled.";
        }
        catch (Exception ex)
        {
            DriveSyncStatusText = $"Drive Sync error: {ex.Message}";
        }
        finally
        {
            IsDriveSyncRunning = false;
            if (ReferenceEquals(_driveSyncCts, cts)) _driveSyncCts = null;
            cts.Dispose();
        }
    }

    [RelayCommand]
    private void CancelDriveSync()
    {
        _driveSyncCts?.Cancel();
    }

    // ==========================================
    // BUILT-IN MP3+G / CDG KARAOKE AUDITION PLAYER WITH 10-BAND EQ & KEY SHIFT
    // ==========================================

    private readonly CdgPreviewPlayer _auditionPlayer = new();
    private bool _playerEventsSubscribed;
    private bool _suppressPlayerSeek;

    [ObservableProperty]
    private ImageSource? _playerCdgFrame;

    [ObservableProperty]
    private string _playerNowPlayingTitle = "Select any track and click '▶ Audition Track' to test audio & CDG lyrics";

    [ObservableProperty]
    private string _playerNowPlayingPath = string.Empty;

    [ObservableProperty]
    private bool _isPlayerPlaying;

    [ObservableProperty]
    private double _playerPositionSeconds;

    [ObservableProperty]
    private double _playerDurationSeconds = 1.0;

    [ObservableProperty]
    private string _playerTimeDisplay = "0:00 / 0:00";

    [ObservableProperty]
    private int _playerKeySemitones;

    [ObservableProperty]
    private double _playerVolumePercent = 90.0;

    [ObservableProperty] private double _eqBand0; // 31 Hz
    [ObservableProperty] private double _eqBand1; // 62 Hz
    [ObservableProperty] private double _eqBand2; // 125 Hz
    [ObservableProperty] private double _eqBand3; // 250 Hz
    [ObservableProperty] private double _eqBand4; // 500 Hz
    [ObservableProperty] private double _eqBand5; // 1 kHz
    [ObservableProperty] private double _eqBand6; // 2 kHz
    [ObservableProperty] private double _eqBand7; // 4 kHz
    [ObservableProperty] private double _eqBand8; // 8 kHz
    [ObservableProperty] private double _eqBand9; // 16 kHz

    partial void OnPlayerVolumePercentChanged(double value) => _auditionPlayer.SetVolume(value);

    partial void OnPlayerPositionSecondsChanged(double value)
    {
        if (_suppressPlayerSeek) return;
        _auditionPlayer.Seek(TimeSpan.FromSeconds(Math.Max(0, value)));
    }

    private double[] GetCurrentEqBands() =>
        [EqBand0, EqBand1, EqBand2, EqBand3, EqBand4, EqBand5, EqBand6, EqBand7, EqBand8, EqBand9];

    private void EnsurePlayerSubscribed()
    {
        if (_playerEventsSubscribed) return;
        _playerEventsSubscribed = true;
        _auditionPlayer.FrameUpdated += frame => RunOnUi(() => PlayerCdgFrame = frame);
        _auditionPlayer.PlaybackStateChanged += () => RunOnUi(() =>
        {
            IsPlayerPlaying = _auditionPlayer.IsPlaying;
            _suppressPlayerSeek = true;
            PlayerPositionSeconds = _auditionPlayer.Position.TotalSeconds;
            double dur = _auditionPlayer.Duration.TotalSeconds;
            PlayerDurationSeconds = dur > 0.5 ? dur : Math.Max(1.0, PlayerPositionSeconds + 1.0);
            _suppressPlayerSeek = false;
            PlayerTimeDisplay = $"{ _auditionPlayer.Position:m\\:ss} / {_auditionPlayer.Duration:m\\:ss}";
        });
    }

    [RelayCommand]
    private async Task AuditionSong(Song? song)
    {
        song ??= SelectedSong ?? SelectedDuplicateSong;
        if (song == null || string.IsNullOrWhiteSpace(song.FilePath))
        {
            System.Windows.MessageBox.Show("Select a song from the Library or Duplicate list first.", "No Song Selected", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        if (!File.Exists(song.FilePath))
        {
            System.Windows.MessageBox.Show($"File not found on disk:\n{song.FilePath}", "File Missing", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        EnsurePlayerSubscribed();
        PlayerNowPlayingTitle = $"{song.Artist} — {song.Title} [{song.KaraokeType}]";
        PlayerNowPlayingPath = song.FilePath;
        try
        {
            await _auditionPlayer.LoadTrackAsync(song.FilePath, PlayerKeySemitones, GetCurrentEqBands(), PlayerVolumePercent);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            PlayerNowPlayingTitle = $"Could not open this track: {ex.Message}";
        }
    }

    [RelayCommand]
    private void TogglePlayerPlayPause()
    {
        EnsurePlayerSubscribed();
        if (_auditionPlayer.IsPlaying)
        {
            _auditionPlayer.Pause();
        }
        else
        {
            _auditionPlayer.Play();
        }
    }

    [RelayCommand]
    private void StopPlayer()
    {
        _auditionPlayer.Stop();
    }

    [RelayCommand]
    private async Task ApplyPlayerDsp()
    {
        EnsurePlayerSubscribed();
        var pos = _auditionPlayer.Position;
        bool wasPlaying = _auditionPlayer.IsPlaying;
        await _auditionPlayer.ApplyDspAndOpenAsync(PlayerKeySemitones, GetCurrentEqBands(), PlayerVolumePercent, pos, wasPlaying);
    }

    [RelayCommand]
    private async Task ResetPlayerEqAndKey()
    {
        PlayerKeySemitones = 0;
        EqBand0 = EqBand1 = EqBand2 = EqBand3 = EqBand4 = EqBand5 = EqBand6 = EqBand7 = EqBand8 = EqBand9 = 0;
        await ApplyPlayerDsp();
    }
}
