// Edited on Aug 5, 2026 @ 07:07:00 -> Fix scope of ExportFailedFiles local function in RunSlowScanAsync
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
using Microsoft.EntityFrameworkCore;

namespace LyracistDbEditor;

public partial class MainViewModel : ObservableObject
{
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _onlyShowMissingArtist = false;

    [ObservableProperty]
    private Song? _selectedSong;

    [ObservableProperty]
    private string _editTitle = string.Empty;

    [ObservableProperty]
    private string _editArtist = string.Empty;

    [ObservableProperty]
    private bool _isScanning = false;

    [ObservableProperty]
    private double _scanProgressPercent = 0;

    [ObservableProperty]
    private string _scanProgressText = "Idle";

    [ObservableProperty]
    private int _totalSongCount = 0;

    [ObservableProperty]
    private int _missingArtistCount = 0;

    [ObservableProperty]
    private int _exportCatalogIndex = 0;

    [ObservableProperty]
    private int _exportFormatIndex = 0;

    [ObservableProperty]
    private string? _selectedLibraryDirectory;

    [ObservableProperty]
    private bool _isLibraryScanning = false;

    [ObservableProperty]
    private double _libraryScanProgressPercent = 0;

    [ObservableProperty]
    private string _libraryScanStatusText = "Idle";

    public ObservableCollection<Song> Songs { get; } = [];
    public ObservableCollection<string> ScanLog { get; } = [];
    public ObservableCollection<string> LibraryDirectories { get; } = [];

    public MainViewModel()
    {
        // Resolve FFmpeg/FFprobe paths for metadata parsing
        FFmpegService.ResolvePaths();

        // Ensure database is created and up to date
        try
        {
            using var context = new LyracistDbContext();
            context.Database.Migrate();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to migrate database: {ex.Message}");
        }

        foreach (var dir in LibraryDirectoryStore.Load())
        {
            LibraryDirectories.Add(dir);
        }

        RefreshStats();
        Search();
    }

    [RelayCommand]
    private void Search()
    {
        try
        {
            using var context = new LyracistDbContext();
            
            IQueryable<Song> query = context.Songs.AsNoTracking();

            if (OnlyShowMissingArtist)
            {
                query = query.Where(s => s.Artist == "Unknown Artist" || s.Artist == "" || s.Artist == null);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.ToLower();
                query = query.Where(s => s.Title.ToLower().Contains(search) || 
                                         s.Artist.ToLower().Contains(search) || 
                                         s.FilePath.ToLower().Contains(search));
            }

            // Order by title and limit results for performance
            var results = query.OrderBy(s => s.Title).Take(150).ToList();

            Songs.Clear();
            foreach (var song in results)
            {
                Songs.Add(song);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to query songs: {ex.Message}");
        }
    }

    partial void OnSelectedSongChanged(Song? value)
    {
        EditTitle = value?.Title ?? string.Empty;
        EditArtist = value?.Artist ?? string.Empty;
    }

    partial void OnOnlyShowMissingArtistChanged(bool value)
    {
        Search();
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
                dbSong.Title = EditTitle.Trim();
                dbSong.Artist = EditArtist.Trim();

                await context.SaveChangesAsync();

                // Re-index full-text search (FTS5)
                var searchService = new SearchService(context);
                await searchService.IndexSong(dbSong);

                RefreshStats();
                Search(); // Re-fetch to synchronize the UI

                System.Windows.MessageBox.Show("Song details updated successfully.", "Changes Saved", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to save changes: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ScanSongMetadata(Song? song)
    {
        if (song == null) return;
        
        try
        {
            if (!File.Exists(song.FilePath))
            {
                System.Windows.MessageBox.Show("The physical file does not exist.", "File Not Found", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            string ext = Path.GetExtension(song.FilePath).ToLowerInvariant();
            string resolvedArtist = string.Empty;
            string resolvedTitle = string.Empty;
            string resolvedGenre = string.Empty;

            if (ext == ".zip")
            {
                using var archive = ZipFile.OpenRead(song.FilePath);
                var audioEntry = archive.Entries.FirstOrDefault(e => 
                    e.FullName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || 
                    e.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
                
                if (audioEntry != null)
                {
                    string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(audioEntry.FullName));
                    audioEntry.ExtractToFile(tempFile, overwrite: true);
                    
                    var probeResult = await FFprobeRunner.ProbeFile(tempFile);
                    resolvedArtist = probeResult.ArtistTag;
                    resolvedTitle = probeResult.TitleTag;
                    resolvedGenre = probeResult.GenreTag;
                    
                    try { File.Delete(tempFile); } catch {}
                }
            }
            else
            {
                var probeResult = await FFprobeRunner.ProbeFile(song.FilePath);
                resolvedArtist = probeResult.ArtistTag;
                resolvedTitle = probeResult.TitleTag;
                resolvedGenre = probeResult.GenreTag;
            }

            if (!string.IsNullOrWhiteSpace(resolvedArtist))
            {
                using var context = new LyracistDbContext();
                var dbSong = await context.Songs.FirstOrDefaultAsync(s => s.SongId == song.SongId);
                if (dbSong != null)
                {
                    dbSong.Artist = resolvedArtist.Trim();
                    if (!string.IsNullOrWhiteSpace(resolvedTitle))
                    {
                        dbSong.Title = resolvedTitle.Trim();
                    }
                    dbSong.Genre = resolvedGenre;

                    await context.SaveChangesAsync();

                    // Sync FTS
                    var searchService = new SearchService(context);
                    await searchService.IndexSong(dbSong);

                    RefreshStats();
                    Search();

                    System.Windows.MessageBox.Show($"Metadata extracted successfully!\nArtist: {dbSong.Artist}\nTitle: {dbSong.Title}", "Metadata Scanned", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("No metadata tags were found in the file.", "No Metadata Found", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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
        if (IsScanning) return;
        
        IsScanning = true;
        _scanCts = new CancellationTokenSource();
        ScanLog.Clear();
        ScanProgressPercent = 0;
        ScanProgressText = "Initializing background scan...";
        
        Task.Run(() => RunSlowScanAsync(_scanCts.Token));
    }

    [RelayCommand]
    private void StopScan()
    {
        if (!IsScanning) return;
        
        _scanCts?.Cancel();
        IsScanning = false;
        ScanProgressText = "Scan stopped by host.";
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
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ScanLog.Insert(0, logMsg);
                        if (ScanLog.Count > 100) ScanLog.RemoveAt(ScanLog.Count - 1);
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to write unresolved artist list: {ex.Message}");
                }
            }
        }

        try
        {
            using var context = new LyracistDbContext();
            var searchService = new SearchService(context);

            // Fetch songs with unknown artists
            var targetSongs = await context.Songs
                .Where(s => s.Artist == "Unknown Artist" || s.Artist == "" || s.Artist == null)
                .ToListAsync(token);

            int total = targetSongs.Count;
            if (total == 0)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ScanProgressPercent = percentage;
                    ScanProgressText = $"Processing {processed}/{total}: {currentFile}";
                });

                // Yield CPU and database locks to preserve live performance in Lyracist
                await Task.Delay(60, token);

                if (!File.Exists(song.FilePath))
                {
                    failedFiles.Add(song.FilePath);
                    continue;
                }

                string ext = Path.GetExtension(song.FilePath).ToLowerInvariant();
                string resolvedArtist = string.Empty;
                string resolvedTitle = string.Empty;
                string resolvedGenre = string.Empty;

                if (ext == ".zip")
                {
                    try
                    {
                        using var archive = ZipFile.OpenRead(song.FilePath);
                        var audioEntry = archive.Entries.FirstOrDefault(e => 
                            e.FullName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || 
                            e.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
                        
                        if (audioEntry != null)
                        {
                            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(audioEntry.FullName));
                            audioEntry.ExtractToFile(tempFile, overwrite: true);
                            
                            var probeResult = await FFprobeRunner.ProbeFile(tempFile);
                            resolvedArtist = probeResult.ArtistTag;
                            resolvedTitle = probeResult.TitleTag;
                            resolvedGenre = probeResult.GenreTag;
                            
                            try { File.Delete(tempFile); } catch {}
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to parse zip tags: {ex.Message}");
                    }
                }
                else
                {
                    var probeResult = await FFprobeRunner.ProbeFile(song.FilePath);
                    resolvedArtist = probeResult.ArtistTag;
                    resolvedTitle = probeResult.TitleTag;
                    resolvedGenre = probeResult.GenreTag;
                }

                // If tags resolved a valid artist name, update the DB record
                if (!string.IsNullOrWhiteSpace(resolvedArtist) && resolvedArtist.Trim() != "Unknown Artist")
                {
                    song.Artist = resolvedArtist.Trim();
                    
                    if (!string.IsNullOrWhiteSpace(resolvedTitle))
                    {
                        song.Title = resolvedTitle.Trim();
                    }
                    song.Genre = resolvedGenre;

                    // Save immediately in small transactions
                    await context.SaveChangesAsync(token);

                    // Sync FTS Search
                    await searchService.IndexSong(song);

                    updatedCount++;

                    string logMsg = $"[{DateTime.Now:HH:mm:ss}] Updated: {currentFile} -> Artist: {song.Artist}";
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ScanLog.Insert(0, logMsg);
                        if (ScanLog.Count > 100) ScanLog.RemoveAt(ScanLog.Count - 1);
                    });
                }
                else
                {
                    failedFiles.Add(song.FilePath);
                }
            }

            ExportFailedFiles();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ScanProgressText = $"Scan finished. Updated {updatedCount} tracks.";
                IsScanning = false;
                RefreshStats();
                Search(); // Sync updates list
            });
        }
        catch (OperationCanceledException)
        {
            ExportFailedFiles();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ScanProgressText = "Scan canceled.";
                IsScanning = false;
            });
        }
        catch (Exception ex)
        {
            ExportFailedFiles();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
            Debug.WriteLine($"Failed to query database stats: {ex.Message}");
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
            string prefix = path.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;

            using var context = new LyracistDbContext();
            var orphaned = await context.Songs.Where(s => s.FilePath.StartsWith(prefix)).ToListAsync();

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
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LibraryScanStatusText = $"Scan failed: {ex.Message}";
                    IsLibraryScanning = false;
                    AppendLog($"Scan failed: {ex.Message}");
                });
                return;
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsLibraryScanning = false;
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
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        LibraryScanProgressPercent = p.Percentage;
                        LibraryScanStatusText = $"Filling in song details… {p.FilesProcessed:N0} / {p.TotalFilesFound:N0}";
                    });
                });

                await scanner.ProbeMissingMetadataAsync(probeProgress);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "LyracistDbEditor metadata probe failed", ex);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LibraryScanStatusText = $"Metadata fill-in error: {ex.Message}";
                    AppendLog($"Metadata fill-in error: {ex.Message}");
                });
            }
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
        if (string.IsNullOrWhiteSpace(RenameFolderPath) || !Directory.Exists(RenameFolderPath))
        {
            System.Windows.MessageBox.Show("Please select a valid folder first.", "Invalid Path", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        IsRenaming = true;
        _renameCts = new CancellationTokenSource();
        var token = _renameCts.Token;

        ScanLog.Clear();
        ScanLog.Add($"[START] Renaming files in folder: {RenameFolderPath}");
        ScanProgressText = "Preparing directory scan...";
        ScanProgressPercent = 0;

        try
        {
            // Scan for all files in the directory
            string[] allFiles = await Task.Run(() => Directory.GetFiles(RenameFolderPath, "*.*", SearchOption.TopDirectoryOnly), token);
            
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
                ScanLog.Add("No supported audio, video, or zip files found in the folder.");
                ScanProgressText = "No supported files found.";
                IsRenaming = false;
                return;
            }

            int processedCount = 0;
            int renameSuccessCount = 0;

            foreach (string filePath in targetFiles)
            {
                if (token.IsCancellationRequested)
                {
                    ScanLog.Add("[CANCEL] Operation cancelled by user.");
                    break;
                }

                processedCount++;
                double percent = (double)processedCount / targetFiles.Count * 100;
                ScanProgressPercent = percent;
                ScanProgressText = $"Processing file {processedCount} of {targetFiles.Count}";

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
                            string directory = Path.GetDirectoryName(filePath) ?? RenameFolderPath;
                            string baseNewPath = Path.Combine(directory, sanitizedTitle);
                            string newPath = baseNewPath + ext;

                            // Prevent duplicate filename overwrites
                            int suffix = 1;
                            while (File.Exists(newPath))
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
                                    ScanLog.Add($"Renamed CDG: {Path.GetFileName(oldCdgPath)} -> {Path.GetFileName(newCdgPath)}");
                                }

                                File.Move(filePath, newPath);
                                renameSuccessCount++;
                                ScanLog.Add($"Renamed: {fileName} -> {newFileName}");
                            }
                            else
                            {
                                ScanLog.Add($"Skipped (Name matches): {fileName}");
                            }
                        }
                        else
                        {
                            ScanLog.Add($"Skipped (Sanitization empty): {fileName}");
                        }
                    }
                    else
                    {
                        ScanLog.Add($"Skipped (No Title tag): {fileName}");
                    }
                }
                catch (Exception ex)
                {
                    ScanLog.Add($"Error parsing '{fileName}': {ex.Message}");
                }
            }

            ScanProgressText = token.IsCancellationRequested ? "Cancelled" : "Completed";
            ScanLog.Add($"[FINISHED] Renamed {renameSuccessCount} files successfully.");
        }
        catch (Exception ex)
        {
            ScanLog.Add($"Fatal Error: {ex.Message}");
            ScanProgressText = "Error during rename";
        }
        finally
        {
            IsRenaming = false;
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
