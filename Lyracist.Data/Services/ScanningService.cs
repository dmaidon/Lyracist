// Edited on Aug 6, 2026 @ 07:01:27 -> Fix dead-file cleanup matching sibling folders that share a scanned path's prefix (e.g. C:\Music vs C:\Music2)
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

    public class ScanningService(LyracistDbContext context)
    {
        private readonly LyracistDbContext _context = context;

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
            var parts = cleaned.Split([" - "], StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                artist = parts[0].Trim();
                title = string.Join(" - ", parts.Skip(1)).Trim();
            }

            return (artist, title);
        }

        public static (string Artist, string Title, string KaraokeType, bool IsKaraoke) ParseStoreDownload(string filePath)
        {
            string filename = Path.GetFileNameWithoutExtension(filePath);
            string artist = "Unknown Artist";
            string title = filename.Trim();
            string karaokeType = "";
            bool isKaraoke = false;
            string catalogCode = "";
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            string pathLower = filePath.ToLowerInvariant();
            if (pathLower.Contains("karaoke") ||
                pathLower.Contains("instrumental") ||
                pathLower.Contains("sing-along") ||
                pathLower.Contains("backing track"))
            {
                isKaraoke = true;
            }

            // 2. Sunfly Pattern Matching
            var sfRegex = new Regex(@"^(SF\s*\d+)\s*[-_]?\s*(\d+)\s*-\s*(.+)$", RegexOptions.IgnoreCase);
            var sfMatch = sfRegex.Match(filename);
            if (sfMatch.Success)
            {
                string catNum = sfMatch.Groups[1].Value.Replace(" ", "").ToUpperInvariant();
                string trackNum = sfMatch.Groups[2].Value;
                catalogCode = $"{catNum}-{trackNum}";
                isKaraoke = true;

                string remaining = sfMatch.Groups[3].Value.Trim();
                var parts = remaining.Split([" - "], StringSplitOptions.None);
                if (parts.Length >= 2)
                {
                    artist = parts[0].Trim();
                    title = string.Join(" - ", parts.Skip(1)).Trim();
                }
                else
                {
                    title = remaining;
                }
            }
            else if (pathLower.Contains("sunfly"))
            {
                isKaraoke = true;
                catalogCode = "SF";
            }

            // 3. Karaoke Version Pattern Matching
            bool isKv = false;
            string cleaned = filename;

            if (filename.Contains("Karaoke Version", StringComparison.OrdinalIgnoreCase))
            {
                isKv = true;
                isKaraoke = true;
                cleaned = Regex.Replace(cleaned, @"\s*[-_(\[]\s*Karaoke Version\s*[\)\]]?", "", RegexOptions.IgnoreCase);
            }

            var kvCodeRegex = new Regex(@"^KV\s*(\d+)\s*-\s*(.+)$", RegexOptions.IgnoreCase);
            var kvMatch = kvCodeRegex.Match(cleaned);
            if (kvMatch.Success)
            {
                isKv = true;
                isKaraoke = true;
                catalogCode = "KV-" + kvMatch.Groups[1].Value;
                cleaned = kvMatch.Groups[2].Value;
            }

            if (isKv)
            {
                var parts = cleaned.Split([" - "], StringSplitOptions.None);
                if (parts.Length >= 2)
                {
                    artist = parts[0].Trim();
                    title = string.Join(" - ", parts.Skip(1)).Trim();
                }
                else
                {
                    title = cleaned.Trim();
                }
            }
            else if (pathLower.Contains("karaoke version") || pathLower.Contains("karaoke-version"))
            {
                isKaraoke = true;
                if (string.IsNullOrEmpty(catalogCode))
                {
                    catalogCode = "KV";
                }
            }

            // 4. Default ParseFilename fallback
            if (artist == "Unknown Artist" || title == filename)
            {
                var (Artist, Title) = ParseFilename(filePath);
                if (artist == "Unknown Artist" && Artist != "Unknown Artist")
                {
                    artist = Artist;
                }
                if (title == filename && Title != filename)
                {
                    title = Title;
                }
            }

            if (!string.IsNullOrEmpty(catalogCode))
            {
                title = $"{title} [{catalogCode}]";
            }

            if (isKaraoke)
            {
                if (ext == ".zip")
                    karaokeType = "ZIPCDG";
                else if (ext == ".mp4")
                    karaokeType = "MP4";
                else
                    karaokeType = "MP3G";
            }

            return (artist, title, karaokeType, isKaraoke);
        }

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
        // CORE SCAN ENGINE (HIGH-PERFORMANCE BATCH SCAN)
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
                    var files = SafeEnumerateFiles(path);
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

            // Query existing song paths for fast local duplicate checks
            var existingSongsMap = await _context.Songs
                .ToDictionaryAsync(s => s.FilePath, s => s, StringComparer.OrdinalIgnoreCase);

            // Clean up dead records for files that no longer exist under the scanned directory paths.
            // Committed immediately (not batched with the scan below) since this list is normally small.
            var songsToRemove = new List<Song>();
            foreach (var kvp in existingSongsMap)
            {
                var s = kvp.Value;
                bool isInScannedPath = false;
                foreach (var path in paths)
                {
                    if (IsPathUnderDirectory(s.FilePath, path))
                    {
                        isInScannedPath = true;
                        break;
                    }
                }

                if (isInScannedPath && !File.Exists(s.FilePath))
                {
                    songsToRemove.Add(s);
                }
            }

            if (songsToRemove.Count > 0)
            {
                _context.Songs.RemoveRange(songsToRemove);
                await _context.SaveChangesAsync();
                foreach (var s in songsToRemove)
                {
                    await searchService.RemoveSongFromIndex(s.SongId);
                    existingSongsMap.Remove(s.FilePath);
                }
            }

            // Process and commit in batches rather than one giant transaction spanning the
            // entire (potentially huge, multi-drive) scan. A large real-world library can take
            // a very long time to probe (one external ffprobe process per file); batching means
            // an interruption partway through (crash, closed app, one bad file) only loses the
            // in-flight batch instead of rolling back every song found so far.
            const int batchSize = 200;
            var batch = new List<string>(batchSize);

            async Task ProcessBatchAsync(List<string> filesBatch)
            {
                var songsToInsert = new List<Song>();
                var songsToUpdate = new List<Song>();

                foreach (var file in filesBatch)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();

                    // Standalone CDGs are skipped since they are processed in tandem with MP3 files
                    if (ext == ".cdg") continue;

                    var parsed = ParseStoreDownload(file);
                    if (!parsed.IsKaraoke && ext == ".mp3")
                    {
                        string cdgPath = Path.ChangeExtension(file, ".cdg");
                        if (cdgFileSet.Contains(cdgPath))
                        {
                            parsed = (parsed.Artist, parsed.Title, "MP3G", true);
                        }
                    }
                    else if (!parsed.IsKaraoke && ext == ".mp4")
                    {
                        if (IsMp4Karaoke(file))
                        {
                            parsed = (parsed.Artist, parsed.Title, "MP4", true);
                        }
                    }
                    else if (ext == ".zip")
                    {
                        var (isKaraoke, _) = CheckZipKaraoke(file);
                        if (isKaraoke)
                        {
                            parsed = (parsed.Artist, parsed.Title, "ZIPCDG", true);
                        }
                    }

                    string typeLabel = parsed.IsKaraoke ? parsed.KaraokeType : "Audio";

                    if (existingSongsMap.TryGetValue(file, out var existing))
                    {
                        existing.Title = parsed.Title;
                        existing.Artist = parsed.Artist;
                        existing.IsKaraoke = parsed.IsKaraoke;
                        existing.KaraokeType = typeLabel;
                        songsToUpdate.Add(existing);
                    }
                    else
                    {
                        // Duration/Genre are left at defaults here so the scan itself stays fast
                        // (no external ffprobe process per file). ProbeMissingMetadataAsync fills
                        // these in afterward as a separate low-priority background pass.
                        var newSong = new Song
                        {
                            Title = parsed.Title,
                            Artist = parsed.Artist,
                            FilePath = file,
                            IsKaraoke = parsed.IsKaraoke,
                            KaraokeType = typeLabel,
                            Duration = 0,
                            KeyDefault = 0,
                            TempoDefault = 1.0,
                            DateAdded = DateTime.UtcNow
                        };
                        songsToInsert.Add(newSong);
                        existingSongsMap[file] = newSong;
                    }
                }

                if (songsToInsert.Count > 0)
                {
                    _context.Songs.AddRange(songsToInsert);
                }

                if (songsToUpdate.Count > 0)
                {
                    _context.Songs.UpdateRange(songsToUpdate);
                }

                if (songsToInsert.Count > 0 || songsToUpdate.Count > 0)
                {
                    await _context.SaveChangesAsync();

                    // Index into the FTS5 search table in batches
                    var allChangedSongs = songsToInsert.Concat(songsToUpdate);
                    await searchService.IndexSongsBatch(allChangedSongs);
                }
            }

            foreach (var file in candidateFiles)
            {
                batch.Add(file);
                processed++;

                bool isLastFile = processed == totalFiles;
                if (batch.Count >= batchSize || isLastFile)
                {
                    try
                    {
                        await ProcessBatchAsync(batch);
                    }
                    catch (Exception ex)
                    {
                        Lyracist.Shared.Globals.LogError("Lyracist", $"Directory scanning failed at file {processed}/{totalFiles} ({file})", ex);
                        throw;
                    }
                    finally
                    {
                        batch.Clear();
                    }

                    progress?.Report(new ScanProgress
                    {
                        TotalFilesFound = totalFiles,
                        FilesProcessed = processed,
                        CurrentFile = Path.GetFileName(file)
                    });
                }
            }
        }

        // ==========================================
        // BACKGROUND METADATA FILL-IN (DURATION / GENRE)
        // ==========================================

        // Probes duration/genre for any song still missing it (Duration <= 0). Kept separate
        // from ScanDirectories so folder scans stay fast — this is meant to be run afterward as
        // a low-priority background pass. It always re-queries songs with Duration <= 0 from the
        // database, so it naturally resumes wherever it left off if interrupted (app closed,
        // crash) or run again later, without needing any separate state to track progress.
        public async Task ProbeMissingMetadataAsync(IProgress<ScanProgress>? progress = null)
        {
            var songsNeedingProbe = await _context.Songs
                .Where(s => s.Duration <= 0)
                .ToListAsync();

            int totalFiles = songsNeedingProbe.Count;
            if (totalFiles == 0) return;

            const int batchSize = 100;
            int processed = 0;

            for (int i = 0; i < songsNeedingProbe.Count; i += batchSize)
            {
                var batch = songsNeedingProbe.Skip(i).Take(batchSize).ToList();

                // Lower concurrency than the scan-time probing used to have — this now runs
                // unattended in the background, potentially while the app is actively being used
                // for a live show, so it should be gentle on disk I/O rather than maximize throughput.
                var semaphore = new System.Threading.SemaphoreSlim(3);
                var tasks = batch.Select(async song =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        if (!File.Exists(song.FilePath)) return;

                        if (song.KaraokeType == "ZIPCDG")
                        {
                            var (isKaraoke, audioEntryName) = CheckZipKaraoke(song.FilePath);
                            if (isKaraoke && !string.IsNullOrEmpty(audioEntryName))
                            {
                                string tempPath = string.Empty;
                                try
                                {
                                    using var archive = ZipFile.OpenRead(song.FilePath);
                                    var entry = archive.GetEntry(audioEntryName);
                                    if (entry != null)
                                    {
                                        tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(audioEntryName));
                                        entry.ExtractToFile(tempPath);

                                        var probeResult = await FFprobeRunner.ProbeFile(tempPath);
                                        song.Duration = probeResult.Duration;
                                        song.Genre = probeResult.GenreTag;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Failed to extract and probe zip entry {audioEntryName} in {song.FilePath}: {ex.Message}");
                                }
                                finally
                                {
                                    if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                                    {
                                        try { File.Delete(tempPath); } catch { }
                                    }
                                }
                            }
                        }
                        else
                        {
                            var probeResult = await FFprobeRunner.ProbeFile(song.FilePath);
                            song.Duration = probeResult.Duration;
                            song.Genre = probeResult.GenreTag;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to probe file {song.FilePath}: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                await Task.WhenAll(tasks);

                _context.Songs.UpdateRange(batch);
                await _context.SaveChangesAsync();

                processed += batch.Count;
                progress?.Report(new ScanProgress
                {
                    TotalFilesFound = totalFiles,
                    FilesProcessed = processed,
                    CurrentFile = batch.Count > 0 ? Path.GetFileName(batch[^1].FilePath) : string.Empty
                });
            }
        }

        /// <summary>
        /// True if filePath is inside directoryPath (or a subfolder of it). A plain StartsWith
        /// on the raw strings would also match an unrelated sibling folder that happens to share
        /// the prefix (e.g. scanning "C:\Music" would match "C:\Music2\..."), so this normalizes
        /// with a trailing separator before comparing.
        /// </summary>
        private static bool IsPathUnderDirectory(string filePath, string directoryPath)
        {
            string normalizedDir = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return filePath.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> SafeEnumerateFiles(string path)
        {
            var files = new List<string>();
            var dirs = new Queue<string>();
            dirs.Enqueue(path);

            while (dirs.Count > 0)
            {
                string currentDir = dirs.Dequeue();
                try
                {
                    foreach (var f in Directory.EnumerateFiles(currentDir))
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext == ".mp3" || ext == ".cdg" || ext == ".mp4" || ext == ".zip")
                        {
                            files.Add(f);
                        }
                    }

                    foreach (var d in Directory.EnumerateDirectories(currentDir))
                    {
                        try
                        {
                            var di = new DirectoryInfo(d);
                            // Skip hidden or system directories to avoid permissions issues/recycle bin/system volume info
                            if ((di.Attributes & FileAttributes.Hidden) != 0 || 
                                (di.Attributes & FileAttributes.System) != 0)
                            {
                                continue;
                            }
                            dirs.Enqueue(d);
                        }
                        catch
                        {
                            // Skip directories we cannot read attributes for
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip folders we do not have permission to read
                }
                catch (DirectoryNotFoundException)
                {
                    // Skip folders deleted during the scan
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to enumerate directory {currentDir}: {ex.Message}");
                }
            }

            return files;
        }
    }
}
