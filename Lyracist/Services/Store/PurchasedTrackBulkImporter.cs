// Edited on Sep 6, 2026 @ 11:48:30 -> Fix MP3+G companion file deduplication in ScanFolderAsync
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Lyracist.Services.Media;
using Lyracist.Shared;
using Microsoft.EntityFrameworkCore;

namespace Lyracist.Services.Store;

public class BulkImportCandidate
{
    public string PrimaryFilePath { get; set; } = string.Empty;
    public string? CompanionFilePath { get; set; }
    public string? LyricsFilePath { get; set; }
    public string Filename => Path.GetFileName(PrimaryFilePath);
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Provider { get; set; } = "Local";
    public string FileType { get; set; } = "Audio"; // "MP3+G", "ZIPCDG", "MP4", "Audio"
    public double Duration { get; set; }
    public string DurationText => Duration > 0 ? TimeSpan.FromSeconds(Duration).ToString(@"m\:ss") : "--:--";
    public string Key { get; set; } = "--";
    public double? Bpm { get; set; }
    public string BpmText => Bpm.HasValue ? $"{Bpm.Value:F0}" : "--";
    public string Quality { get; set; } = "Medium";
    public string Difficulty { get; set; } = "Medium";
    public string VocalPresence { get; set; } = "no vocals";
    public bool HasDualAudio { get; set; }

    public bool WillNormalize { get; set; }
    public bool WillTrimSilence { get; set; }
    public bool WillGenerateWaveform { get; set; }
}

public class BulkImportOptions
{
    public bool MoveFilesToTarget { get; set; }
    public string TargetKaraokeFolder { get; set; } = string.Empty;
    public string TargetMusicFolder { get; set; } = string.Empty;
}

public class BulkImportProgressReport
{
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int SkippedCount { get; set; }
    public string CurrentItemName { get; set; } = string.Empty;
    public double PercentComplete => TotalItems > 0 ? (CompletedItems / (double)TotalItems) * 100.0 : 0.0;
}

public class BulkImportSummaryReport
{
    public int TotalImported { get; set; }
    public int TotalSkipped { get; set; }
    public int TotalErrors { get; set; }
    public List<string> ProvidersInvolved { get; set; } = [];
    public double AverageProcessingTimeMs { get; set; }
    public string AverageProcessingTimeText => AverageProcessingTimeMs >= 1000
        ? $"{AverageProcessingTimeMs / 1000.0:F1} s"
        : $"{AverageProcessingTimeMs:F0} ms";
}

public class PurchasedTrackBulkImporter
{
    private readonly ILibraryService? _libraryService;

    public PurchasedTrackBulkImporter(ILibraryService? libraryService = null)
    {
        _libraryService = libraryService;
    }

    public static async Task<List<BulkImportCandidate>> ScanFolderAsync(
        string folderPath,
        IProgress<string>? statusProgress = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<BulkImportCandidate>();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return candidates;

        statusProgress?.Report($"Scanning directory: {folderPath}...");

        var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
        var supportedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".cdg", ".zip", ".mp4" };

        var mediaFiles = allFiles
            .Where(f => supportedExts.Contains(Path.GetExtension(f)))
            .ToList();

        // Identify and pair MP3+G files
        var handledCompanionFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int totalFound = mediaFiles.Count;
        int processedIndex = 0;

        foreach (var file in mediaFiles)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (handledCompanionFiles.Contains(file)) continue;

            string ext = Path.GetExtension(file).ToLowerInvariant();
            string primaryPath = file;
            string? companionPath = null;

            if (ext == ".cdg")
            {
                string mp3Candidate = Path.ChangeExtension(file, ".mp3");
                if (File.Exists(mp3Candidate))
                {
                    // Swap so primary is MP3 and companion is CDG
                    primaryPath = mp3Candidate;
                    companionPath = file;
                    handledCompanionFiles.Add(file);
                    handledCompanionFiles.Add(mp3Candidate);
                }
                else
                {
                    // Standalone CDG without audio is skipped
                    handledCompanionFiles.Add(file);
                    continue;
                }
            }
            else if (ext == ".mp3")
            {
                handledCompanionFiles.Add(file);
                string cdgCandidate = Path.ChangeExtension(file, ".cdg");
                if (File.Exists(cdgCandidate))
                {
                    companionPath = cdgCandidate;
                    handledCompanionFiles.Add(cdgCandidate);
                }
            }
            else
            {
                handledCompanionFiles.Add(file);
            }

            processedIndex++;
            statusProgress?.Report($"Analyzing ({processedIndex}/{totalFound}): {Path.GetFileName(primaryPath)}");

            // Parse title & artist
            var parsed = ScanningService.ParseStoreDownload(primaryPath);

            // Locate matching lyric file
            string? lyricsPath = PurchasedTrackWatcherService.FindMatchingLyricsFile(primaryPath);

            // Probe stream for audio/video technical metadata
            var probe = await FFprobeRunner.ProbeFile(primaryPath);

            // Provider Intelligence Detection
            string? detectedSource = null;
            if (ext == ".zip")
            {
                detectedSource = PurchasedTrackWatcherService.InspectZipForProvider(primaryPath);
            }
            if (string.IsNullOrEmpty(detectedSource) && companionPath != null && companionPath.EndsWith(".cdg", StringComparison.OrdinalIgnoreCase))
            {
                detectedSource = PurchasedTrackWatcherService.DetectProviderFromCdgHeader(companionPath);
            }
            if (string.IsNullOrEmpty(detectedSource) && (ext == ".mp3" || companionPath?.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) == true))
            {
                detectedSource = PurchasedTrackWatcherService.DetectProviderFromId3(probe);
            }
            if (string.IsNullOrEmpty(detectedSource) && ext == ".mp4")
            {
                detectedSource = PurchasedTrackWatcherService.DetectProviderFromMp4(probe);
            }
            if (string.IsNullOrEmpty(detectedSource))
            {
                string heuristic = PurchasedTrackWatcherService.DetectSource(primaryPath, parsed.Title, parsed.Artist);
                if (heuristic != "Local") detectedSource = heuristic;
            }
            if (string.IsNullOrEmpty(detectedSource) && !string.IsNullOrWhiteSpace(PurchasedTrackWatcherService.ReferrerHint))
            {
                detectedSource = PurchasedTrackWatcherService.ReferrerHint;
            }

            string source = detectedSource ?? "Local";

            // Determine File Type
            string fileType = "Audio";
            if (ext == ".zip")
            {
                fileType = "ZIPCDG";
            }
            else if (ext == ".mp4")
            {
                fileType = "MP4";
            }
            else if (companionPath != null)
            {
                fileType = "MP3+G";
            }

            // Smart Import Tag analysis
            string detectedDifficulty = FFprobeRunner.DetectDifficulty(probe);
            string? detectedKey = FFprobeRunner.DetectKey(probe);
            double? detectedBpm = FFprobeRunner.DetectBpm(probe);
            string detectedVocalPresence = FFprobeRunner.DetectVocalPresence(probe);
            string detectedQuality = FFprobeRunner.DetectQuality(probe);

            var candidate = new BulkImportCandidate
            {
                PrimaryFilePath = primaryPath,
                CompanionFilePath = companionPath,
                LyricsFilePath = lyricsPath,
                Title = parsed.Title,
                Artist = parsed.Artist,
                Provider = source,
                FileType = fileType,
                Duration = probe.Duration,
                Key = detectedKey ?? "--",
                Bpm = detectedBpm,
                Quality = detectedQuality,
                Difficulty = detectedDifficulty,
                VocalPresence = detectedVocalPresence,
                HasDualAudio = probe.HasDualAudio,
                WillNormalize = AppSettings.StoreNormalizeAudioOnImport,
                WillTrimSilence = AppSettings.StoreTrimSilenceOnImport,
                WillGenerateWaveform = AppSettings.StoreGenerateWaveformOnImport
            };

            candidates.Add(candidate);
        }

        statusProgress?.Report($"Scan complete: {candidates.Count} candidate track(s) ready for import.");
        return candidates;
    }

    public async Task<BulkImportSummaryReport> ImportBatchAsync(
        IReadOnlyList<BulkImportCandidate> candidates,
        BulkImportOptions options,
        IProgress<BulkImportProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var summary = new BulkImportSummaryReport();
        if (candidates == null || candidates.Count == 0) return summary;

        var providersSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stopwatchList = new List<double>();

        int completed = 0;
        int successes = 0;
        int errors = 0;
        int skipped = 0;
        int total = candidates.Count;

        using var semaphore = new SemaphoreSlim(3, 3); // Parallel batches: max 3 concurrent FFmpeg operations

        var tasks = candidates.Select(async candidate =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Interlocked.Increment(ref skipped);
                Interlocked.Increment(ref completed);
                return;
            }

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Interlocked.Increment(ref skipped);
                    Interlocked.Increment(ref completed);
                    return;
                }

                var sw = Stopwatch.StartNew();
                string primaryPath = candidate.PrimaryFilePath;
                string? companionPath = candidate.CompanionFilePath;
                string? lyricsPath = candidate.LyricsFilePath;

                PurchasedTrackWatcherService.Current?.LogImport(
                    candidate.Title,
                    candidate.Artist,
                    candidate.Provider,
                    "Processing",
                    $"Bulk importing {candidate.Filename}");

                // Audio Pipeline (Normalize and/or Trim Silence)
                bool normalized = false;
                bool silenceTrimmed = false;

                if (candidate.WillNormalize || candidate.WillTrimSilence)
                {
                    string ext = Path.GetExtension(primaryPath).ToLowerInvariant();
                    if (ext == ".mp3")
                    {
                        string tempOut = Path.Combine(Path.GetDirectoryName(primaryPath)!, $"_proc_{Guid.NewGuid():N}.mp3");
                        bool ok = await FFmpegService.ProcessAudioPipelineBatchAsync(primaryPath, tempOut, candidate.WillNormalize, candidate.WillTrimSilence);
                        if (ok && File.Exists(tempOut))
                        {
                            try
                            {
                                File.Move(tempOut, primaryPath, true);
                                normalized = candidate.WillNormalize;
                                silenceTrimmed = candidate.WillTrimSilence;
                            }
                            catch (Exception ex)
                            {
                                Globals.LogError("Lyracist", $"Failed to replace audio with processed output for {primaryPath}", ex);
                            }
                        }
                    }
                }

                // Waveform Preview
                string? waveformPath = null;
                if (candidate.WillGenerateWaveform)
                {
                    string waveDest = Path.Combine(Path.GetDirectoryName(primaryPath)!, Path.GetFileNameWithoutExtension(primaryPath) + "_waveform.png");
                    bool ok = await FFmpegService.GenerateWaveformPreviewAsync(primaryPath, waveDest);
                    if (ok && File.Exists(waveDest))
                    {
                        waveformPath = waveDest;
                    }
                }

                // Smart Import Rules: Canonical Auto-Rename ("Artist - Title (Provider).ext")
                PurchasedTrackWatcherService.RenameImportedFiles(
                    ref primaryPath,
                    ref companionPath,
                    ref lyricsPath,
                    candidate.Title,
                    candidate.Artist,
                    candidate.Provider);

                bool isKaraoke = candidate.FileType != "Audio";
                string finalPrimary = primaryPath;
                string? finalCompanion = companionPath;
                string? finalLyrics = lyricsPath;

                // Move files to target folders if requested
                if (options.MoveFilesToTarget)
                {
                    string targetFolder = isKaraoke ? options.TargetKaraokeFolder : options.TargetMusicFolder;
                    if (!string.IsNullOrWhiteSpace(targetFolder))
                    {
                        try
                        {
                            Directory.CreateDirectory(targetFolder);

                            string primaryDest = GetUniqueDestinationPath(targetFolder, Path.GetFileName(finalPrimary));
                            File.Move(finalPrimary, primaryDest, true);
                            finalPrimary = primaryDest;

                            if (!string.IsNullOrEmpty(finalCompanion) && File.Exists(finalCompanion))
                            {
                                string compName = Path.GetFileNameWithoutExtension(primaryDest) + Path.GetExtension(finalCompanion);
                                string compDest = Path.Combine(targetFolder, compName);
                                File.Move(finalCompanion, compDest, true);
                                finalCompanion = compDest;
                            }

                            if (!string.IsNullOrEmpty(finalLyrics) && File.Exists(finalLyrics))
                            {
                                string lyrName = Path.GetFileNameWithoutExtension(primaryDest) + Path.GetExtension(finalLyrics);
                                string lyrDest = Path.Combine(targetFolder, lyrName);
                                File.Move(finalLyrics, lyrDest, true);
                                finalLyrics = lyrDest;
                            }

                            if (!string.IsNullOrEmpty(waveformPath) && File.Exists(waveformPath))
                            {
                                string waveName = Path.GetFileNameWithoutExtension(primaryDest) + "_waveform.png";
                                string waveDest = Path.Combine(targetFolder, waveName);
                                File.Move(waveformPath, waveDest, true);
                                waveformPath = waveDest;
                            }
                        }
                        catch (Exception ex)
                        {
                            Globals.LogError("Lyracist", $"Failed to route bulk files for {candidate.Title}", ex);
                        }
                    }
                }

                // Insert into Database
                using var context = new LyracistDbContext();
                var searchService = new SearchService(context);

                var existing = await context.Songs.FirstOrDefaultAsync(s => s.FilePath == finalPrimary, cancellationToken);
                int songId;

                if (existing != null)
                {
                    existing.Title = candidate.Title;
                    existing.Artist = candidate.Artist;
                    existing.IsKaraoke = isKaraoke;
                    existing.KaraokeType = candidate.FileType;
                    if (candidate.Duration > 0) existing.Duration = candidate.Duration;
                    existing.Difficulty = candidate.Difficulty;
                    if (candidate.Key != "--") existing.Key = candidate.Key;
                    if (candidate.Bpm.HasValue)
                    {
                        existing.BPM = candidate.Bpm.Value;
                        existing.TempoDefault = candidate.Bpm.Value;
                    }
                    existing.VocalPresence = candidate.VocalPresence;
                    existing.Quality = candidate.Quality;

                    await context.SaveChangesAsync(cancellationToken);
                    await searchService.IndexSongsBatch([existing]);
                    songId = existing.SongId;
                }
                else
                {
                    string tags = candidate.Provider;
                    if (candidate.HasDualAudio) tags = AppendTag(tags, "Dual-Audio");
                    if (normalized) tags = AppendTag(tags, "Normalized");
                    if (silenceTrimmed) tags = AppendTag(tags, "SilenceTrimmed");
                    if (waveformPath != null) tags = AppendTag(tags, "Waveform");
                    if (candidate.Difficulty != "Medium") tags = AppendTag(tags, candidate.Difficulty);
                    if (candidate.Key != "--") tags = AppendTag(tags, $"Key:{candidate.Key}");
                    if (candidate.Bpm.HasValue) tags = AppendTag(tags, $"{candidate.Bpm}BPM");
                    tags = AppendTag(tags, candidate.VocalPresence);
                    tags = AppendTag(tags, $"Quality:{candidate.Quality}");

                    var newSong = new Song
                    {
                        Title = candidate.Title,
                        Artist = candidate.Artist,
                        FilePath = finalPrimary,
                        IsKaraoke = isKaraoke,
                        KaraokeType = candidate.FileType,
                        Difficulty = candidate.Difficulty,
                        Key = candidate.Key != "--" ? candidate.Key : null,
                        BPM = candidate.Bpm,
                        VocalPresence = candidate.VocalPresence,
                        Quality = candidate.Quality,
                        Tags = tags,
                        Duration = candidate.Duration,
                        KeyDefault = 0,
                        TempoDefault = candidate.Bpm ?? 1.0,
                        DateAdded = DateTime.UtcNow
                    };

                    context.Songs.Add(newSong);
                    await context.SaveChangesAsync(cancellationToken);
                    await searchService.IndexSongsBatch([newSong]);
                    songId = newSong.SongId;
                }

                _libraryService?.NotifyLibraryUpdated();

                sw.Stop();
                double elapsedMs = sw.Elapsed.TotalMilliseconds;
                PurchasedTrackWatcherService.RecordProcessingTime(elapsedMs);
                lock (stopwatchList)
                {
                    stopwatchList.Add(elapsedMs);
                    providersSet.Add(candidate.Provider);
                }

                PurchasedTrackWatcherService.Current?.LogImport(
                    candidate.Title,
                    candidate.Artist,
                    candidate.Provider,
                    "Success",
                    $"Bulk imported successfully ({candidate.FileType})",
                    Path.GetFileName(finalPrimary));

                Interlocked.Increment(ref successes);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errors);
                PurchasedTrackWatcherService.Current?.LogImport(
                    candidate.Title,
                    candidate.Artist,
                    candidate.Provider,
                    "Error",
                    $"Bulk import failed: {ex.Message}");
                Globals.LogError("Lyracist", $"Bulk import failed for {candidate.PrimaryFilePath}", ex);
            }
            finally
            {
                semaphore.Release();
                int done = Interlocked.Increment(ref completed);

                progress?.Report(new BulkImportProgressReport
                {
                    TotalItems = total,
                    CompletedItems = done,
                    SuccessCount = successes,
                    ErrorCount = errors,
                    SkippedCount = skipped,
                    CurrentItemName = candidate.Filename
                });
            }
        });

        await Task.WhenAll(tasks);

        summary.TotalImported = successes;
        summary.TotalErrors = errors;
        summary.TotalSkipped = skipped;
        summary.ProvidersInvolved = providersSet.OrderBy(p => p).ToList();
        summary.AverageProcessingTimeMs = stopwatchList.Count > 0 ? Math.Round(stopwatchList.Average(), 1) : 0;

        return summary;
    }

    private static string AppendTag(string tags, string newTag)
    {
        if (string.IsNullOrWhiteSpace(tags)) return newTag;
        if (tags.Contains(newTag, StringComparison.OrdinalIgnoreCase)) return tags;
        return $"{tags};{newTag}";
    }

    public static string GetUniqueDestinationPath(string folder, string fileName)
    {
        string dest = Path.Combine(folder, fileName);
        if (!File.Exists(dest)) return dest;

        string nameOnly = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        int counter = 1;

        while (File.Exists(dest))
        {
            dest = Path.Combine(folder, $"{nameOnly} ({counter}){ext}");
            counter++;
        }

        return dest;
    }
}
