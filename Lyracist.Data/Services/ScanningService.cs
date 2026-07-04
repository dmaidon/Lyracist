using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lyracist.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Lyracist.Data.Services
{
    public class ScanProgress
    {
        public int TotalFilesFound { get; set; }
        public int FilesProcessed { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public double Percentage => TotalFilesFound > 0 ? (double)FilesProcessed / TotalFilesFound * 100 : 0;
    }

    public class ScanningService
    {
        private readonly LyracistDbContext _context;

        public ScanningService(LyracistDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // FILENAME METADATA PARSER
        // ==========================================

        public static (string Artist, string Title) ParseFilename(string filePath)
        {
            string filename = Path.GetFileNameWithoutExtension(filePath);

            // 1. Strip producer codes (e.g., "SF321-04 - ...", "DK123-15 - ...")
            string cleaned = Regex.Replace(filename, @"^[A-Za-z0-9]+-\d+\s*-\s*", "");

            // 2. Strip track/index numbers (e.g., "01 - ...", "01. ...")
            cleaned = Regex.Replace(cleaned, @"^\d+\s*[-.]\s*", "");

            string artist = "Unknown Artist";
            string title = cleaned.Trim();

            // 3. Split by " - " to differentiate Artist and Title
            var parts = cleaned.Split(new[] { " - " }, StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                artist = parts[0].Trim();
                title = string.Join(" - ", parts.Skip(1)).Trim();
            }

            return (artist, title);
        }

        // ==========================================
        // MP4 KARAOKE DETECTION
        // ==========================================

        private bool IsMp4Karaoke(string filePath)
        {
            string pathLower = filePath.ToLowerInvariant();
            return pathLower.Contains("karaoke") ||
                   pathLower.Contains("instrumental") ||
                   pathLower.Contains("singalong") ||
                   pathLower.Contains("sing-along") ||
                   pathLower.Contains("backing track");
        }

        // ==========================================
        // ZIP/CDG ARCHIVE CHECKER
        // ==========================================

        private (bool IsKaraoke, string AudioEntryName) CheckZipKaraoke(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                bool hasCdg = false;
                string audioEntryName = string.Empty;

                foreach (var entry in archive.Entries)
                {
                    string ext = Path.GetExtension(entry.FullName).ToLowerInvariant();
                    if (ext == ".cdg")
                    {
                        hasCdg = true;
                    }
                    else if (ext == ".mp3" || ext == ".wav")
                    {
                        audioEntryName = entry.FullName;
                    }
                }

                return (hasCdg && !string.IsNullOrEmpty(audioEntryName), audioEntryName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read zip archive {zipPath}: {ex.Message}");
                return (false, string.Empty);
            }
        }

        // ==========================================
        // CORE SCAN ENGINE
        // ==========================================

        public async Task ScanDirectories(IEnumerable<string> paths, IProgress<ScanProgress>? progress = null)
        {
            var candidateFiles = new List<string>();

            // 1. Gather all files recursively from directories
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                    continue;

                try
                {
                    var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(f =>
                        {
                            string ext = Path.GetExtension(f).ToLowerInvariant();
                            return ext == ".mp3" || ext == ".cdg" || ext == ".mp4" || ext == ".zip";
                        });

                    candidateFiles.AddRange(files);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to crawl directory {path}: {ex.Message}");
                }
            }

            int totalFiles = candidateFiles.Count;
            if (totalFiles == 0) return;

            // Build a set of all CDG file paths for quick validation in MP3+G detection
            var cdgFileSet = candidateFiles
                .Where(f => Path.GetExtension(f).ToLowerInvariant() == ".cdg")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var searchService = new SearchService(_context);
            int processed = 0;

            // Start a single database transaction to maximize SQLite performance
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var file in candidateFiles)
                {
                    processed++;
                    progress?.Report(new ScanProgress
                    {
                        TotalFilesFound = totalFiles,
                        FilesProcessed = processed,
                        CurrentFile = Path.GetFileName(file)
                    });

                    string ext = Path.GetExtension(file).ToLowerInvariant();

                    // Standalone CDGs are skipped since they are processed in tandem with MP3 files
                    if (ext == ".cdg") continue;

                    Song? song = null;

                    if (ext == ".mp3")
                    {
                        string cdgPath = Path.ChangeExtension(file, ".cdg");
                        bool hasCdg = cdgFileSet.Contains(cdgPath);

                        var (artist, title) = ParseFilename(file);
                        var probe = await FFprobeRunner.ProbeFile(file);

                        song = new Song
                        {
                            Title = title,
                            Artist = artist,
                            FilePath = file,
                            IsKaraoke = hasCdg,
                            KaraokeType = hasCdg ? "MP3G" : "",
                            Duration = probe.Duration,
                            KeyDefault = 0,
                            TempoDefault = 1.0,
                            DateAdded = DateTime.UtcNow
                        };
                    }
                    else if (ext == ".mp4")
                    {
                        bool isKaraoke = IsMp4Karaoke(file);
                        var (artist, title) = ParseFilename(file);
                        var probe = await FFprobeRunner.ProbeFile(file);

                        song = new Song
                        {
                            Title = title,
                            Artist = artist,
                            FilePath = file,
                            IsKaraoke = isKaraoke,
                            KaraokeType = isKaraoke ? "MP4" : "",
                            Duration = probe.Duration,
                            KeyDefault = 0,
                            TempoDefault = 1.0,
                            DateAdded = DateTime.UtcNow
                        };
                    }
                    else if (ext == ".zip")
                    {
                        var (isKaraoke, audioEntryName) = CheckZipKaraoke(file);
                        if (isKaraoke)
                        {
                            var (artist, title) = ParseFilename(file);
                            double duration = 0;

                            string tempPath = string.Empty;
                            try
                            {
                                using var archive = ZipFile.OpenRead(file);
                                var entry = archive.GetEntry(audioEntryName);
                                if (entry != null)
                                {
                                    tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(audioEntryName));
                                    entry.ExtractToFile(tempPath);

                                    var probe = await FFprobeRunner.ProbeFile(tempPath);
                                    duration = probe.Duration;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to extract and probe zip entry {audioEntryName} in {file}: {ex.Message}");
                            }
                            finally
                            {
                                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                                {
                                    try { File.Delete(tempPath); } catch { }
                                }
                            }

                            song = new Song
                            {
                                Title = title,
                                Artist = artist,
                                FilePath = file,
                                IsKaraoke = true,
                                KaraokeType = "ZIPCDG",
                                Duration = duration,
                                KeyDefault = 0,
                                TempoDefault = 1.0,
                                DateAdded = DateTime.UtcNow
                            };
                        }
                    }

                    if (song != null)
                    {
                        // Check for duplicate FilePath to decide if we update or insert
                        var existing = await _context.Songs.FirstOrDefaultAsync(s => s.FilePath == song.FilePath);
                        if (existing != null)
                        {
                            existing.Title = song.Title;
                            existing.Artist = song.Artist;
                            existing.IsKaraoke = song.IsKaraoke;
                            existing.KaraokeType = song.KaraokeType;
                            existing.Duration = song.Duration;
                            _context.Songs.Update(existing);
                            await _context.SaveChangesAsync();

                            // Re-index FTS5
                            await searchService.IndexSong(existing);
                        }
                        else
                        {
                            _context.Songs.Add(song);
                            await _context.SaveChangesAsync();

                            // Index FTS5
                            await searchService.IndexSong(song);
                        }
                    }
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error scanning directories, rolling back transaction: {ex.Message}");
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
