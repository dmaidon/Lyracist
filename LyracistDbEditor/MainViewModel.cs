// Edited on Oct 3, 2026 @ 08:24:00 -> Guard dispatcher progress updates against TaskCanceledException during metadata probe
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Lyracist.Shared;
using Microsoft.EntityFrameworkCore;

namespace LyracistDbEditor;

public partial class MainViewModel : ObservableObject
{
    private static readonly Lock _lock = new();

    [ObservableProperty]
    private int _totalSongCount;

    [ObservableProperty]
    private int _missingArtistCount;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<Song> SearchResults { get; } = [];

    [ObservableProperty]
    private Song? _selectedSong;

    [ObservableProperty]
    private string _editTitle = string.Empty;

    [ObservableProperty]
    private string _editArtist = string.Empty;

    [ObservableProperty]
    private bool _onlyShowMissingArtist;

    partial void OnSelectedSongChanged(Song? value)
    {
        if (value != null)
        {
            EditTitle = value.Title ?? string.Empty;
            EditArtist = value.Artist ?? string.Empty;
        }
        else
        {
            EditTitle = string.Empty;
            EditArtist = string.Empty;
        }
    }

    partial void OnOnlyShowMissingArtistChanged(bool value)
    {
        Search();
    }

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private double _scanProgressPercent;

    [ObservableProperty]
    private string _scanProgressText = string.Empty;

    public ObservableCollection<string> ScanLog { get; } = [];

    private CancellationTokenSource? _scanCts;

    // Library Folder Scanner properties
    public ObservableCollection<string> LibraryDirectories { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemoveDirectory))]
    [NotifyPropertyChangedFor(nameof(CanRescanDirectory))]
    private string? _selectedLibraryDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemoveDirectory))]
    [NotifyPropertyChangedFor(nameof(CanRescanDirectory))]
    private bool _isLibraryScanning;

    [ObservableProperty]
    private double _libraryScanProgressPercent;

    [ObservableProperty]
    private string _libraryScanStatusText = "Idle";

    [ObservableProperty]
    private int _exportCatalogIndex = 0; // 0 = Karaoke Only, 1 = All Songs

    [ObservableProperty]
    private int _exportFormatIndex = 0; // 0 = PDF, 1 = TXT, 2 = Word DOCX

    public bool CanRemoveDirectory => !string.IsNullOrWhiteSpace(SelectedLibraryDirectory) && !IsLibraryScanning;
    public bool CanRescanDirectory => !string.IsNullOrWhiteSpace(SelectedLibraryDirectory) && !IsLibraryScanning;

    public MainViewModel()
    {
        // Load library directories on startup
        var dirs = LibraryDirectoryStore.Load();
        foreach (var dir in dirs)
        {
            LibraryDirectories.Add(dir);
        }

        RefreshStats();
        Search();
        RefreshReadinessCounts();
    }

    // Marshals to the UI thread, but quietly does nothing once the app is shutting down
    // (Application.Current can be null or the dispatcher can throw TaskCanceledException).
    private static void RunOnUi(Action action)
    {
        try
        {
            if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } disp)
            {
                disp.Invoke(action);
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Cancels every cancellable background operation; called when the window closes.</summary>
    public void CancelAllOperations()
    {
        try { _scanCts?.Cancel(); } catch (ObjectDisposedException) { }
        try { _renameCts?.Cancel(); } catch (ObjectDisposedException) { }
        try { _readinessCts?.Cancel(); } catch (ObjectDisposedException) { }
    }

    // Extracts tags via FFprobe; for a .zip, the first mp3/wav inside is extracted to a temp file
    // that is always cleaned up, even if the probe throws.
    private static async Task<(string Artist, string Title, string Genre)> ProbeTagsAsync(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            var direct = await FFprobeRunner.ProbeFile(filePath);
            return (direct.ArtistTag, direct.TitleTag, direct.GenreTag);
        }

        using var archive = ZipFile.OpenRead(filePath);
        var audioEntry = archive.Entries.FirstOrDefault(e =>
            e.FullName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
            e.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
        if (audioEntry == null) return (string.Empty, string.Empty, string.Empty);

        string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(audioEntry.FullName));
        try
        {
            audioEntry.ExtractToFile(tempFile, overwrite: true);
            var probe = await FFprobeRunner.ProbeFile(tempFile);
            return (probe.ArtistTag, probe.TitleTag, probe.GenreTag);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [RelayCommand]
    private void Search()
    {
        try
        {
            using var context = new LyracistDbContext();
            List<Song> songs;
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                IQueryable<Song> query = context.Songs;
                if (OnlyShowMissingArtist)
                {
                    query = query.Where(s => s.Artist == "Unknown Artist" || s.Artist == "" || s.Artist == null);
                }
                songs = query.OrderBy(s => s.Artist).ThenBy(s => s.Title).Take(100).ToList();
            }
            else
            {
                var searchService = new SearchService(context);
                // SearchSync does the same work as Search() without wrapping it in a Task.Run just
                // to immediately block on it via .Result - that pattern added a threadpool
                // round-trip for no benefit, since this method is synchronous either way.
                songs = searchService.SearchSync(SearchText);
                if (OnlyShowMissingArtist)
                {
                    songs = songs.Where(s => s.Artist == "Unknown Artist" || string.IsNullOrEmpty(s.Artist)).ToList();
                }
            }

            SearchResults.Clear();
            foreach (var s in songs)
            {
                SearchResults.Add(s);
            }
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor: Search failed", ex);
        }
    }

    [RelayCommand]
    private async Task SaveSong()
    {
        if (SelectedSong == null) return;

        try
        {
            using var context = new LyracistDbContext();
            var dbSong = await context.Songs.FirstOrDefaultAsync(s => s.SongId == SelectedSong.SongId);
            if (dbSong != null)
            {
                dbSong.Artist = NameFormatting.ProperCase(EditArtist);
                dbSong.Title = NameFormatting.ProperCase(EditTitle);
                dbSong.Genre = SelectedSong.Genre;
                dbSong.Tags = SelectedSong.Tags;
                dbSong.Duration = SelectedSong.Duration;
                dbSong.IsKaraoke = SelectedSong.IsKaraoke;
                dbSong.KaraokeType = SelectedSong.KaraokeType;

                await context.SaveChangesAsync();

                // Update the FTS search index
                var searchService = new SearchService(context);
                await searchService.IndexSong(dbSong);

                RefreshStats();
                Search();

                System.Windows.MessageBox.Show("Song details updated successfully.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to save changes: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteSong()
    {
        if (SelectedSong == null) return;

        var confirm = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete '{SelectedSong.Title}' by '{SelectedSong.Artist}' from the database?\nThis will not delete the physical file.",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            using var context = new LyracistDbContext();
            var dbSong = await context.Songs.FirstOrDefaultAsync(s => s.SongId == SelectedSong.SongId);
            if (dbSong != null)
            {
                var searchService = new SearchService(context);
                context.Songs.Remove(dbSong);
                await context.SaveChangesAsync();

                await searchService.RemoveSongFromIndex(dbSong.SongId);

                SelectedSong = null;
                RefreshStats();
                Search();

                System.Windows.MessageBox.Show("Song deleted from index.", "Deleted", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to delete song: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ScanSongMetadata(Song? song)
    {
        song ??= SelectedSong;
        if (song == null) return;

        try
        {
            if (!File.Exists(song.FilePath))
            {
                System.Windows.MessageBox.Show("The physical file does not exist on disk.", "File Not Found", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Work on local copies so the bound list item isn't left showing values that were
            // never saved when no artist can be resolved.
            string artist = song.Artist ?? string.Empty;
            string title = song.Title ?? string.Empty;
            string genre = song.Genre ?? string.Empty;
            string? tags = song.Tags;

            var (resolvedArtist, resolvedTitle, resolvedGenre) = await ProbeTagsAsync(song.FilePath);

            // Update using local file tag resolution if resolved
            if (!string.IsNullOrWhiteSpace(resolvedArtist) && resolvedArtist.Trim() != "Unknown Artist")
            {
                artist = resolvedArtist.Trim();
                if (!string.IsNullOrWhiteSpace(resolvedTitle))
                {
                    title = resolvedTitle.Trim();
                }
                genre = resolvedGenre;
            }

            // Query online service for missing artist and tags
            try
            {
                var onlineMeta = await MetadataFetchService.FetchMetadataAsync(title, artist, CancellationToken.None);
                if (onlineMeta != null)
                {
                    if ((artist == "Unknown Artist" || string.IsNullOrEmpty(artist)) && !string.IsNullOrEmpty(onlineMeta.Artist))
                    {
                        artist = onlineMeta.Artist;
                        if (!string.IsNullOrEmpty(onlineMeta.Title))
                        {
                            title = onlineMeta.Title;
                        }
                    }

                    tags = onlineMeta.Tags.Count > 0 ? string.Join(", ", onlineMeta.Tags) : "none";
                }
                else
                {
                    tags = "none";
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Preserve empty tags to try again later
            }

            // If tags or artist resolved successfully, update the DB record
            if (!string.IsNullOrWhiteSpace(artist) && artist != "Unknown Artist")
            {
                using var context = new LyracistDbContext();
                var dbSong = await context.Songs.FirstOrDefaultAsync(s => s.SongId == song.SongId);
                if (dbSong != null)
                {
                    dbSong.Artist = artist;
                    dbSong.Title = title;
                    dbSong.Genre = genre;
                    dbSong.Tags = tags;

                    await context.SaveChangesAsync();

                    // Sync FTS Search
                    var searchService = new SearchService(context);
                    await searchService.IndexSong(dbSong);

                    RefreshStats();
                    Search();

                    System.Windows.MessageBox.Show($"Metadata updated: {dbSong.Artist} - {dbSong.Title}", "Metadata Scanned", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("No artist metadata could be found for this file.", "No Metadata Found", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error scanning metadata: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void StartScan()
    {
        // Rename shares the log/progress display, so the two never run together.
        if (IsScanning || IsRenaming) return;

        IsScanning = true;
        var cts = new CancellationTokenSource();
        _scanCts = cts;
        ScanLog.Clear();
        ScanProgressPercent = 0;
        ScanProgressText = "Initializing background scan...";

        _ = Task.Run(async () =>
        {
            try
            {
                await RunSlowScanAsync(cts.Token);
            }
            finally
            {
                // IsScanning stays true until the task has really finished, so a new scan can't
                // start while the old one is still winding down.
                RunOnUi(() => IsScanning = false);
                if (ReferenceEquals(_scanCts, cts)) _scanCts = null;
                cts.Dispose();
            }
        });
    }

    [RelayCommand]
    private void StopScan()
    {
        if (!IsScanning) return;

        _scanCts?.Cancel();
        ScanProgressText = "Stopping scan...";
    }

    private async Task RunSlowScanAsync(CancellationToken token)
    {
        var failedFiles = new List<string>();

        void ExportFailedFiles()
        {
            if (failedFiles.Count > 0)
            {
                try
                {
                    string reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                    Directory.CreateDirectory(reportsDir);
                    string fileName = $"Failed_Artist_Updates_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    string filePath = Path.Combine(reportsDir, fileName);
                    File.WriteAllLines(filePath, failedFiles);
                    
                    string logMsg = $"[{DateTime.Now:HH:mm:ss}] Exported {failedFiles.Count} unresolved tracks to: {filePath}";
                    RunOnUi(() =>
                    {
                        ScanLog.Insert(0, logMsg);
                        if (ScanLog.Count > 100) ScanLog.RemoveAt(ScanLog.Count - 1);
                    });
                }
                catch (Exception ex)
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor: Failed to write unresolved artist list", ex);
                }
            }
        }

        try
        {
            using var context = new LyracistDbContext();
            var searchService = new SearchService(context);

            var targetSongs = await context.Songs
                .Where(s => s.Artist == "Unknown Artist" || s.Artist == "" || s.Artist == null)
                .ToListAsync(token);

            int total = targetSongs.Count;
            if (total == 0)
            {
                RunOnUi(() =>
                {
                    ScanProgressText = "No songs with missing artists found.";
                    IsScanning = false;
                });
                return;
            }

            int processed = 0;
            int updatedCount = 0;

            foreach (var song in targetSongs)
            {
                if (token.IsCancellationRequested) break;

                processed++;
                double percentage = (double)processed / total * 100;
                string currentFile = Path.GetFileName(song.FilePath);

                RunOnUi(() =>
                {
                    ScanProgressPercent = percentage;
                    ScanProgressText = $"Processing {processed}/{total}: {currentFile}";
                });

                await Task.Delay(60, token);

                if (!File.Exists(song.FilePath))
                {
                    failedFiles.Add(song.FilePath);
                    continue;
                }

                string resolvedArtist = string.Empty;
                string resolvedTitle = string.Empty;
                string resolvedGenre = string.Empty;

                try
                {
                    (resolvedArtist, resolvedTitle, resolvedGenre) = await ProbeTagsAsync(song.FilePath);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor: Failed to read tags for " + song.FilePath, ex);
                }

                // Update using local file tag resolution if resolved
                if (!string.IsNullOrWhiteSpace(resolvedArtist) && resolvedArtist.Trim() != "Unknown Artist")
                {
                    song.Artist = resolvedArtist.Trim();
                    if (!string.IsNullOrWhiteSpace(resolvedTitle))
                    {
                        song.Title = resolvedTitle.Trim();
                    }
                    song.Genre = resolvedGenre;
                }

                // Query online service for missing artist and tags (only for karaoke files to keep scans fast)
                if (song.IsKaraoke)
                {
                    try
                    {
                        var onlineMeta = await MetadataFetchService.FetchMetadataAsync(song.Title, song.Artist, token);
                        if (onlineMeta != null)
                        {
                            if ((song.Artist == "Unknown Artist" || string.IsNullOrEmpty(song.Artist)) && !string.IsNullOrEmpty(onlineMeta.Artist))
                            {
                                song.Artist = onlineMeta.Artist;
                                if (!string.IsNullOrEmpty(onlineMeta.Title))
                                {
                                    song.Title = onlineMeta.Title;
                                }
                            }

                            if (onlineMeta.Tags.Count > 0)
                            {
                                song.Tags = string.Join(", ", onlineMeta.Tags);
                            }
                            else
                            {
                                song.Tags = "none";
                            }
                        }
                        else
                        {
                            song.Tags = "none";
                        }
                    }
                    catch (Exception) when (!token.IsCancellationRequested)
                    {
                        // Preserve empty tags to try again later
                    }
                }
                else
                {
                    // Non-karaoke standard music track: Fall back to local file genre, avoiding network rate-limit delay
                    if (song.Tags == null || song.Tags == "")
                    {
                        song.Tags = !string.IsNullOrWhiteSpace(song.Genre) ? song.Genre : "none";
                    }
                }

                // If tags or artist resolved successfully, update the DB record
                if (!string.IsNullOrWhiteSpace(song.Artist) && song.Artist != "Unknown Artist")
                {
                    // Save immediately in small transactions
                    await context.SaveChangesAsync(token);

                    // Sync FTS Search
                    await searchService.IndexSong(song);

                    updatedCount++;

                    string logMsg = $"[{DateTime.Now:HH:mm:ss}] Updated: {currentFile} -> Artist: {song.Artist}";
                    RunOnUi(() =>
                    {
                        ScanLog.Insert(0, logMsg);
                        if (ScanLog.Count > 100) ScanLog.RemoveAt(ScanLog.Count - 1);
                    });
                }
                else
                {
                    // Even if artist is not found, save the "none" tags update if set
                    await context.SaveChangesAsync(token);
                    failedFiles.Add(song.FilePath);
                }
            }

            ExportFailedFiles();

            RunOnUi(() =>
            {
                ScanProgressText = token.IsCancellationRequested
                    ? $"Scan stopped. Updated {updatedCount} tracks."
                    : $"Scan finished. Updated {updatedCount} tracks.";
                IsScanning = false;
                RefreshStats();
                Search(); // Sync updates list
            });
        }
        catch (OperationCanceledException)
        {
            ExportFailedFiles();
            RunOnUi(() =>
            {
                ScanProgressText = "Scan canceled.";
                IsScanning = false;
            });
        }
        catch (Exception ex)
        {
            ExportFailedFiles();
            RunOnUi(() =>
            {
                ScanProgressText = $"Error: {ex.Message}";
                IsScanning = false;
            });
        }
    }

    private void RefreshStats()
    {
        try
        {
            using var context = new LyracistDbContext();
            TotalSongCount = context.Songs.Count();
            MissingArtistCount = context.Songs.Count(s => s.Artist == "Unknown Artist" || s.Artist == "" || s.Artist == null);
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor: Failed to query database stats", ex);
        }
    }

    // ==========================================
    // LIBRARY DIRECTORY SCANNER
    // ==========================================
    // Uses the same ScanningService engine as Lyracist itself: an initial fast pass that
    // walks the folder(s) and inserts/updates songs, followed by a separate low-priority
    // background pass that fills in duration/genre via FFprobe. See ScanningService.cs.

    private void AppendLog(string message)
    {
        ScanLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        if (ScanLog.Count > 100) ScanLog.RemoveAt(ScanLog.Count - 1);
    }

    [RelayCommand]
    private void AddLibraryDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Music/Karaoke Folder or Drive to Scan"
        };
        if (dialog.ShowDialog() != true) return;

        string path = dialog.FolderName;
        if (!LibraryDirectories.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            LibraryDirectories.Add(path);
            LibraryDirectoryStore.Save(LibraryDirectories.ToList());
        }

        // Only adds to the list — scanning starts when the user clicks Rescan/Scan All,
        // so adding multiple directories in a row doesn't get blocked by an in-progress scan.
    }

    [RelayCommand]
    private async Task RemoveLibraryDirectory()
    {
        if (string.IsNullOrWhiteSpace(SelectedLibraryDirectory)) return;
        if (IsLibraryScanning) return; // the scanner would be re-inserting rows while we delete them
        string path = SelectedLibraryDirectory;

        var confirm = System.Windows.MessageBox.Show(
            $"Remove '{path}' from the scan list? Songs already indexed from this folder will also be removed from the database.",
            "Confirm Remove Directory",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            // Normalize so "C:\Music" also matches "C:\Music\" prefixed paths.
            static string AsPrefix(string dir) => dir.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string prefix = AsPrefix(path);

            // Other registered directories (nested inside or containing this one) keep their songs.
            var otherPrefixes = LibraryDirectories
                .Where(d => !string.Equals(d, path, StringComparison.OrdinalIgnoreCase))
                .Select(AsPrefix)
                .ToList();

            using var context = new LyracistDbContext();

            // Match in memory with OrdinalIgnoreCase rather than relying on how the provider
            // translates StartsWith (case sensitivity, escaping of backslashes).
            var candidates = await context.Songs
                .AsNoTracking()
                .Select(s => new { s.SongId, s.FilePath })
                .ToListAsync();

            var ids = candidates
                .Where(c => c.FilePath != null
                            && c.FilePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                            && !otherPrefixes.Any(o => c.FilePath.StartsWith(o, StringComparison.OrdinalIgnoreCase)))
                .Select(c => c.SongId)
                .ToList();

            var orphaned = ids.Count == 0
                ? []
                : await context.Songs.Where(s => ids.Contains(s.SongId)).ToListAsync();

            if (orphaned.Count > 0)
            {
                var searchService = new SearchService(context);
                context.Songs.RemoveRange(orphaned);
                await context.SaveChangesAsync();
                foreach (var s in orphaned)
                {
                    await searchService.RemoveSongFromIndex(s.SongId);
                }
            }

            LibraryDirectories.Remove(path);
            LibraryDirectoryStore.Save(LibraryDirectories.ToList());
            SelectedLibraryDirectory = null;

            RefreshStats();
            Search();

            AppendLog($"Removed directory '{path}' and {orphaned.Count} associated song(s).");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to remove directory: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RescanSelectedDirectory()
    {
        if (string.IsNullOrWhiteSpace(SelectedLibraryDirectory)) return;
        RunLibraryScan([SelectedLibraryDirectory]);
    }

    [RelayCommand]
    private void RescanAllLibraryDirectories()
    {
        if (LibraryDirectories.Count == 0) return;
        RunLibraryScan(LibraryDirectories.ToList());
    }

    private void RunLibraryScan(IEnumerable<string> dirs)
    {
        if (IsLibraryScanning) return;

        var dirList = dirs.ToList();
        IsLibraryScanning = true;
        LibraryScanProgressPercent = 0;
        LibraryScanStatusText = "Scanning folders...";
        AppendLog($"Starting scan of {dirList.Count} folder(s)...");

        Task.Run(async () =>
        {
            using var context = new LyracistDbContext();
            var scanner = new ScanningService(context);

            try
            {
                var scanProgress = new Progress<ScanProgress>(p =>
                {
                    RunOnUi(() =>
                    {
                        LibraryScanProgressPercent = p.Percentage;
                        LibraryScanStatusText = $"Scanning… {p.FilesProcessed:N0} / {p.TotalFilesFound:N0} files";
                    });
                });

                await scanner.ScanDirectories(dirList, scanProgress);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor library scan failed", ex);
                RunOnUi(() =>
                {
                    LibraryScanStatusText = $"Scan failed: {ex.Message}";
                    IsLibraryScanning = false;
                    AppendLog($"Scan failed: {ex.Message}");
                });
                return;
            }

            // IsLibraryScanning stays true through the metadata fill-in below so a second scan (or a
            // Remove Directory) can't overlap it; it's cleared once that phase has ended.
            RunOnUi(() =>
            {
                LibraryScanStatusText = "Scan complete. Filling in song details in the background...";
                RefreshStats();
                Search();
                AppendLog("Folder scan complete.");
            });

            // Duration/genre fill-in runs afterward, separately, so a large drive doesn't hold
            // up the scan itself — mirrors Lyracist's own approach (see ScanningService.cs).
            try
            {
                var probeProgress = new Progress<ScanProgress>(p =>
                {
                    try
                    {
                        if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } disp)
                        {
                            disp.Invoke(() =>
                            {
                                LibraryScanProgressPercent = p.Percentage;
                                LibraryScanStatusText = $"Filling in song details… {p.FilesProcessed:N0} / {p.TotalFilesFound:N0}";
                            });
                        }
                    }
                    catch (OperationCanceledException) { }
                });

                await scanner.ProbeMissingMetadataAsync(probeProgress);

                try
                {
                    if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } disp)
                    {
                        disp.Invoke(() =>
                        {
                            LibraryScanStatusText = "Metadata fill-in complete.";
                            RefreshStats();
                            Search();
                            AppendLog("Metadata fill-in complete.");

                            // Chain into the Slow Metadata Extractor automatically now that the folder
                            // scan and duration/genre fill-in are both done — but only if the app is
                            // actually idle (not already scanning or mid-rename), so this never steals
                            // a run the user started themselves.
                            if (!IsScanning && !IsRenaming)
                            {
                                AppendLog("Starting slow metadata scan for songs with an unknown artist...");
                                StartScan();
                            }
                        });
                    }
                }
                catch (OperationCanceledException) { }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor metadata probe failed", ex);
                try
                {
                    if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } disp)
                    {
                        disp.Invoke(() =>
                        {
                            LibraryScanStatusText = $"Metadata fill-in error: {ex.Message}";
                            AppendLog($"Metadata fill-in error: {ex.Message}");
                        });
                    }
                }
                catch (OperationCanceledException) { }
            }

            RunOnUi(() => IsLibraryScanning = false);
        });
    }

    // ==========================================
    // METADATA FILE RENAMER PROPERTIES & LOGIC
    // ==========================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRename))]
    private string _renameFolderPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRename))]
    private bool _isRenaming = false;

    public bool CanRename => !string.IsNullOrWhiteSpace(RenameFolderPath) && !IsRenaming;

    private CancellationTokenSource? _renameCts;

    [RelayCommand]
    private async Task RenameFiles()
    {
        if (IsRenaming) return;

        if (string.IsNullOrWhiteSpace(RenameFolderPath) || !Directory.Exists(RenameFolderPath))
        {
            System.Windows.MessageBox.Show("Please select a valid folder first.", "Invalid Path", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        // The rename shares the log and progress display with the slow metadata scan.
        if (IsScanning)
        {
            System.Windows.MessageBox.Show("Please wait for (or stop) the metadata scan before renaming files.", "Scan In Progress", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        IsRenaming = true;
        var cts = new CancellationTokenSource();
        _renameCts = cts;
        var token = cts.Token;
        string folder = RenameFolderPath;

        ScanLog.Clear();
        ScanLog.Add($"[START] Renaming files in folder: {folder}");
        ScanProgressText = "Preparing directory scan...";
        ScanProgressPercent = 0;

        void Log(string message) => RunOnUi(() => ScanLog.Add(message));

        try
        {
            // All file I/O and tag reading runs off the UI thread so the window stays responsive
            // and Cancel can be clicked.
            await Task.Run(async () =>
            {
                string[] allFiles = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);

                // Filter files that we can process: standard audio/video formats and zip archives.
                // Exclude matching .cdg files since they don't contain tag metadata; they are renamed dynamically in sync with their matching audio files.
                var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".mp3", ".mp4", ".m4a", ".wma", ".flac", ".wav", ".zip"
                };

                var targetFiles = allFiles
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f)))
                    .ToList();

                if (targetFiles.Count == 0)
                {
                    Log("No supported audio, video, or zip files found in the folder.");
                    RunOnUi(() => ScanProgressText = "No supported files found.");
                    return;
                }

                int processedCount = 0;
                int renameSuccessCount = 0;

                foreach (string filePath in targetFiles)
                {
                    if (token.IsCancellationRequested)
                    {
                        Log("[CANCEL] Operation cancelled by user.");
                        break;
                    }

                    processedCount++;
                    double percent = (double)processedCount / targetFiles.Count * 100;
                    int current = processedCount;
                    RunOnUi(() =>
                    {
                        ScanProgressPercent = percent;
                        ScanProgressText = $"Processing file {current} of {targetFiles.Count}";
                    });

                    string fileName = Path.GetFileName(filePath);
                    string ext = Path.GetExtension(filePath).ToLowerInvariant();

                    try
                    {
                        string? title = null;

                        if (ext == ".zip")
                        {
                            // Open zip to extract the main audio file metadata
                            using var archive = ZipFile.OpenRead(filePath);
                            var audioEntry = archive.Entries.FirstOrDefault(e =>
                                e.FullName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                e.FullName.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) ||
                                e.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                                e.FullName.EndsWith(".wma", StringComparison.OrdinalIgnoreCase));

                            if (audioEntry != null)
                            {
                                string tempFile = Path.Combine(Path.GetTempPath(), $"lyra_rename_{Guid.NewGuid()}_{audioEntry.Name}");
                                audioEntry.ExtractToFile(tempFile, true);
                                try
                                {
                                    using var tagFile = TagLib.File.Create(tempFile);
                                    title = tagFile.Tag.Title;
                                }
                                finally
                                {
                                    if (File.Exists(tempFile))
                                    {
                                        File.Delete(tempFile);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Direct audio file metadata
                            using var tagFile = TagLib.File.Create(filePath);
                            title = tagFile.Tag.Title;
                        }

                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            string sanitizedTitle = SanitizeFileName(title);
                            if (!string.IsNullOrWhiteSpace(sanitizedTitle))
                            {
                                string directory = Path.GetDirectoryName(filePath) ?? folder;
                                string baseNewPath = Path.Combine(directory, sanitizedTitle);
                                string newPath = baseNewPath + ext;

                                // Prevent duplicate filename overwrites. A path equal to the current file
                                // (ignoring case) is not a collision - the file is already correctly named.
                                int suffix = 1;
                                while (!string.Equals(newPath, filePath, StringComparison.OrdinalIgnoreCase)
                                       && (File.Exists(newPath) || File.Exists(Path.ChangeExtension(newPath, ".cdg"))))
                                {
                                    suffix++;
                                    newPath = $"{baseNewPath} ({suffix}){ext}";
                                }

                                string newFileName = Path.GetFileName(newPath);

                                if (!string.Equals(fileName, newFileName, StringComparison.OrdinalIgnoreCase))
                                {
                                    // Rename matching .cdg file if it exists (crucial for keeping CDG graphics synced)
                                    string oldCdgPath = Path.ChangeExtension(filePath, ".cdg");
                                    if (File.Exists(oldCdgPath))
                                    {
                                        string newCdgPath = Path.ChangeExtension(newPath, ".cdg");
                                        File.Move(oldCdgPath, newCdgPath);
                                        Log($"Renamed CDG: {Path.GetFileName(oldCdgPath)} -> {Path.GetFileName(newCdgPath)}");
                                    }

                                    File.Move(filePath, newPath);
                                    renameSuccessCount++;
                                    await UpdateRenamedSongPathAsync(filePath, newPath);
                                    Log($"Renamed: {fileName} -> {newFileName}");
                                }
                                else
                                {
                                    Log($"Skipped (Name matches): {fileName}");
                                }
                            }
                            else
                            {
                                Log($"Skipped (Sanitization empty): {fileName}");
                            }
                        }
                        else
                        {
                            Log($"Skipped (No Title tag): {fileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Error parsing '{fileName}': {ex.Message}");
                    }
                }

                RunOnUi(() => ScanProgressText = token.IsCancellationRequested ? "Cancelled" : "Completed");
                Log($"[FINISHED] Renamed {renameSuccessCount} files successfully.");
            });
        }
        catch (Exception ex)
        {
            ScanLog.Add($"Fatal Error: {ex.Message}");
            ScanProgressText = "Error during rename";
        }
        finally
        {
            IsRenaming = false;
            if (ReferenceEquals(_renameCts, cts)) _renameCts = null;
            cts.Dispose();
        }
    }

    // Keeps the database in step with a renamed file so the row doesn't become an orphan.
    private async Task UpdateRenamedSongPathAsync(string oldPath, string newPath)
    {
        try
        {
            using var context = new LyracistDbContext();
            var dbSongs = await context.Songs.Where(s => s.FilePath == oldPath).ToListAsync();
            if (dbSongs.Count == 0) return;

            var searchService = new SearchService(context);
            foreach (var dbSong in dbSongs)
            {
                dbSong.FilePath = newPath;
            }
            await context.SaveChangesAsync();
            foreach (var dbSong in dbSongs)
            {
                await searchService.IndexSong(dbSong);
            }
        }
        catch (Exception ex)
        {
            RunOnUi(() => ScanLog.Add($"Warning: renamed on disk but database path not updated for '{Path.GetFileName(newPath)}': {ex.Message}"));
            Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor: Failed to update FilePath after rename", ex);
        }
    }

    [RelayCommand]
    private void CancelRename()
    {
        _renameCts?.Cancel();
    }

    [RelayCommand]
    private async Task GenerateBook()
    {
        try
        {
            bool isKaraoke = ExportCatalogIndex == 0;
            int formatIndex = ExportFormatIndex;

            string filePath;
            if (formatIndex == 0)
            {
                filePath = Lyracist.Data.Services.CatalogBookGenerator.GeneratePdf(isKaraoke);
            }
            else if (formatIndex == 1)
            {
                filePath = Lyracist.Data.Services.CatalogBookGenerator.GenerateTxt(isKaraoke);
            }
            else
            {
                filePath = Lyracist.Data.Services.CatalogBookGenerator.GenerateDocx(isKaraoke);
            }

            System.Windows.MessageBox.Show($"Catalog book generated successfully!\nSaved to: {filePath}", "Export Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

            if (File.Exists(filePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to generate catalog book: {ex.Message}", "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName.Replace("__", "_").Trim();
    }
}
