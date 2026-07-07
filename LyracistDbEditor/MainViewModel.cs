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

    public ObservableCollection<Song> Songs { get; } = new();
    public ObservableCollection<string> ScanLog { get; } = new();

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
}
