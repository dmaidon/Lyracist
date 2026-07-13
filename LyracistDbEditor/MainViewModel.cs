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

    public ObservableCollection<Song> Songs { get; } = [];
    public ObservableCollection<string> ScanLog { get; } = [];

    public MainViewModel()
    {
        // Resolve FFmpeg/FFprobe paths for metadata parsing
        FFmpegService.ResolvePaths();

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
                    
                    try { File.Delete(tempFile); } catch {}
                }
            }
            else
            {
                var probeResult = await FFprobeRunner.ProbeFile(song.FilePath);
                resolvedArtist = probeResult.ArtistTag;
                resolvedTitle = probeResult.TitleTag;
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
                    continue;
                }

                string ext = Path.GetExtension(song.FilePath).ToLowerInvariant();
                string resolvedArtist = string.Empty;
                string resolvedTitle = string.Empty;

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
                }

                // If tags resolved a valid artist name, update the DB record
                if (!string.IsNullOrWhiteSpace(resolvedArtist) && resolvedArtist.Trim() != "Unknown Artist")
                {
                    song.Artist = resolvedArtist.Trim();
                    
                    if (!string.IsNullOrWhiteSpace(resolvedTitle))
                    {
                        song.Title = resolvedTitle.Trim();
                    }

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
            }

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
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ScanProgressText = "Scan canceled.";
                IsScanning = false;
            });
        }
        catch (Exception ex)
        {
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

    private static string SanitizeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName.Replace("__", "_").Trim();
    }
}
