// Edited on Aug 30, 2026 @ 09:41:00 -> Replace external process probing and network throttling with high-speed in-memory TagLibSharp extraction and parallel batching
using Lyracist.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

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
        private const string UnknownArtist = "Unknown Artist";
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

            string artist = UnknownArtist;
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
            string artist = UnknownArtist;
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
                var (parsedArtist, parsedTitle) = ParseFilename(filePath);
                if (artist == "Unknown Artist" && parsedArtist != "Unknown Artist")
                {
                    artist = parsedArtist;
                }
                if (title == filename && parsedTitle != filename)
                {
                    title = parsedTitle;
                }
            }

            if (!string.IsNullOrEmpty(catalogCode))
            {
                title = $"{title} [{catalogCode}]";
            }

            if (ext == ".zip")
            {
                var (isZipKaraoke, _) = CheckZipKaraoke(filePath);
                if (isZipKaraoke)
                {
                    isKaraoke = true;
                    karaokeType = "ZIPCDG";
                }
            }
            else if (ext == ".mp4")
            {
                if (IsMp4Karaoke(filePath))
                {
                    isKaraoke = true;
                    karaokeType = "MP4";
                }
            }
            else if (isKaraoke)
            {
                karaokeType = "MP3G";
            }

            string typeLabel = isKaraoke ? (string.IsNullOrEmpty(karaokeType) ? "MP3G" : karaokeType) : "Audio";
            return (artist, title, typeLabel, isKaraoke);
        }

        private static bool IsMp4Karaoke(string filePath)
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

        private static (bool IsKaraoke, string AudioEntryName) CheckZipKaraoke(string zipPath)
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

            var searchService = new SearchService(_context);
            int processed = 0;

            // Query existing song paths for fast local duplicate checks
            var existingSongsMap = await _context.Songs
                .ToDictionaryAsync(s => s.FilePath, s => s, StringComparer.OrdinalIgnoreCase);

            // Clean up dead records for files that no longer exist under the scanned directory paths.
            var songsToRemove = existingSongsMap.Values
                .Where(s => paths.Any(p => IsPathUnderDirectory(s.FilePath, p)) && !File.Exists(s.FilePath))
                .ToList();

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

            const int batchSize = 200;
            var batch = new List<string>(batchSize);

            async Task ProcessBatchAsync(List<string> filesBatch)
            {
                var songsToInsert = new List<Song>();
                var songsToUpdate = new List<Song>();

                foreach (var file in filesBatch)
                {
                    var parsed = ParseStoreDownload(file);

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
        // BACKGROUND METADATA FILL-IN (HIGH-SPEED IN-MEMORY TAGLIB PROBING)
        // ==========================================

        public async Task ProbeMissingMetadataAsync(IProgress<ScanProgress>? progress = null)
        {
            var songsNeedingProbe = await _context.Songs
                .Where(s => s.Duration <= 0 || s.Tags == null || s.Tags == "" || s.Artist == "Unknown Artist" || s.Artist == null)
                .ToListAsync();

            int totalFiles = songsNeedingProbe.Count;
            if (totalFiles == 0) return;

            const int batchSize = 250;
            int processed = 0;

            var searchService = new SearchService(_context);
            int maxConcurrency = Math.Max(8, Environment.ProcessorCount * 2);

            for (int i = 0; i < songsNeedingProbe.Count; i += batchSize)
            {
                var batch = songsNeedingProbe.Skip(i).Take(batchSize).ToList();

                var semaphore = new System.Threading.SemaphoreSlim(maxConcurrency);
                var tasks = batch.Select(async song =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await ProbeSongMetadataAsync(song);
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

                // Re-index FTS5 index to support tag and genre searching immediately
                await searchService.IndexSongsBatch(batch);

                processed += batch.Count;
                progress?.Report(new ScanProgress
                {
                    TotalFilesFound = totalFiles,
                    FilesProcessed = processed,
                    CurrentFile = batch.Count > 0 ? Path.GetFileName(batch[^1].FilePath) : string.Empty
                });
            }

            int unknownArtistCount = await _context.Songs.CountAsync(s => s.Artist == "Unknown Artist" || s.Artist == null || s.Artist == "");
            if (unknownArtistCount > 0)
            {
                Lyracist.Shared.Globals.LogInfo("Lyracist", $"Metadata scan complete. {unknownArtistCount:N0} track(s) remain with 'Unknown Artist' (resolvable in LyracistDbEditor).");
            }
        }

        private static async Task ProbeSongMetadataAsync(Song song)
        {
            if (!File.Exists(song.FilePath)) return;

            bool probedSuccessfully = false;
            string ext = Path.GetExtension(song.FilePath).ToLowerInvariant();

            // 1. Direct TagLib in-memory probing for ZIP-CDG archives (reads audio entry stream without disk temp files)
            if (song.KaraokeType == "ZIPCDG" || ext == ".zip")
            {
                try
                {
                    var (isKaraoke, audioEntryName) = CheckZipKaraoke(song.FilePath);
                    if (isKaraoke && !string.IsNullOrEmpty(audioEntryName))
                    {
                        using var archive = ZipFile.OpenRead(song.FilePath);
                        var entry = archive.GetEntry(audioEntryName);
                        if (entry != null)
                        {
                            using var entryStream = entry.Open();
                            using var memStream = new MemoryStream();
                            await entryStream.CopyToAsync(memStream);
                            memStream.Position = 0;

                            var fileAbstraction = new StreamFileAbstraction(audioEntryName, memStream);
                            using var tagFile = TagLib.File.Create(fileAbstraction);

                            if (song.Duration <= 0 && tagFile.Properties != null && tagFile.Properties.Duration.TotalSeconds > 0)
                            {
                                song.Duration = tagFile.Properties.Duration.TotalSeconds;
                            }

                            if (tagFile.Tag != null)
                            {
                                if (string.IsNullOrWhiteSpace(song.Genre) && !string.IsNullOrWhiteSpace(tagFile.Tag.FirstGenre))
                                {
                                    song.Genre = tagFile.Tag.FirstGenre.Trim();
                                }

                                // If artist was unresolved from filename, check embedded ID3 tags
                                if ((song.Artist == "Unknown Artist" || string.IsNullOrEmpty(song.Artist)) &&
                                    (!string.IsNullOrWhiteSpace(tagFile.Tag.FirstPerformer) || !string.IsNullOrWhiteSpace(tagFile.Tag.FirstAlbumArtist)))
                                {
                                    song.Artist = (tagFile.Tag.FirstPerformer ?? tagFile.Tag.FirstAlbumArtist)!.Trim();
                                }

                                if ((string.IsNullOrEmpty(song.Title) || song.Title == Path.GetFileNameWithoutExtension(song.FilePath)) &&
                                    !string.IsNullOrWhiteSpace(tagFile.Tag.Title))
                                {
                                    song.Title = tagFile.Tag.Title.Trim();
                                }
                            }
                            probedSuccessfully = song.Duration > 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"TagLib zip probing failed for {song.FilePath}: {ex.Message}");
                }
            }
            else
            {
                // 2. Direct TagLib in-memory probing for MP3, MP4, M4A, WAV, FLAC, WMA
                try
                {
                    using var tagFile = TagLib.File.Create(song.FilePath);
                    if (song.Duration <= 0 && tagFile.Properties != null && tagFile.Properties.Duration.TotalSeconds > 0)
                    {
                        song.Duration = tagFile.Properties.Duration.TotalSeconds;
                    }

                    if (tagFile.Tag != null)
                    {
                        if (string.IsNullOrWhiteSpace(song.Genre) && !string.IsNullOrWhiteSpace(tagFile.Tag.FirstGenre))
                        {
                            song.Genre = tagFile.Tag.FirstGenre.Trim();
                        }

                        // If artist was unresolved from filename, check embedded ID3 tags
                        if ((song.Artist == "Unknown Artist" || string.IsNullOrEmpty(song.Artist)) &&
                            (!string.IsNullOrWhiteSpace(tagFile.Tag.FirstPerformer) || !string.IsNullOrWhiteSpace(tagFile.Tag.FirstAlbumArtist)))
                        {
                            song.Artist = (tagFile.Tag.FirstPerformer ?? tagFile.Tag.FirstAlbumArtist)!.Trim();
                        }

                        if ((string.IsNullOrEmpty(song.Title) || song.Title == Path.GetFileNameWithoutExtension(song.FilePath)) &&
                            !string.IsNullOrWhiteSpace(tagFile.Tag.Title))
                        {
                            song.Title = tagFile.Tag.Title.Trim();
                        }
                    }
                    probedSuccessfully = song.Duration > 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"TagLib direct probing failed for {song.FilePath}: {ex.Message}");
                }
            }

            // 3. Fall back to FFprobeRunner only if TagLib could not extract duration (e.g. rare video containers)
            if (!probedSuccessfully && song.Duration <= 0)
            {
                try
                {
                    var probeResult = await FFprobeRunner.ProbeFile(song.FilePath);
                    if (probeResult.Duration > 0)
                    {
                        song.Duration = probeResult.Duration;
                        if (string.IsNullOrWhiteSpace(song.Genre) && !string.IsNullOrWhiteSpace(probeResult.GenreTag))
                        {
                            song.Genre = probeResult.GenreTag;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FFprobe fallback failed for {song.FilePath}: {ex.Message}");
                }
            }

            // 4. Mark Tags as completed with local genre or 'none' only if artist is resolved, 
            // so unresolved Unknown Artists remain eligible for future metadata extraction.
            if (string.IsNullOrEmpty(song.Tags))
            {
                if (song.Artist != UnknownArtist && !string.IsNullOrEmpty(song.Artist))
                {
                    song.Tags = !string.IsNullOrWhiteSpace(song.Genre) ? song.Genre : "none";
                }
                else if (!string.IsNullOrWhiteSpace(song.Genre))
                {
                    song.Tags = song.Genre;
                }
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