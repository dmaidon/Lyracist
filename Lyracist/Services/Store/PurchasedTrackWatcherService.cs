// Edited on Sep 6, 2026 @ 13:03:00 -> Trigger StoreNotificationService toast notifications for imports and audio processing
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Lyracist.Shared;
using Microsoft.EntityFrameworkCore;

namespace Lyracist.Services.Store;

public class PurchasedTrackItem
{
    public int SongId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Source { get; set; } = "Local"; // "Karaoke Version" | "Party Tyme" | "Karaoke.com" | "Sunfly" | "Local"
    public string FilePath { get; set; } = string.Empty;
    public string KaraokeType { get; set; } = string.Empty;
    public bool IsKaraoke { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Imported";
    public string? LyricsPath { get; set; }
    public bool HasDualAudio { get; set; }
    public bool Normalized { get; set; }
    public bool SilenceTrimmed { get; set; }
    public string? WaveformPath { get; set; }
    public double? DurationSeconds { get; set; }
    public int? BitrateKbps { get; set; }
    public string? AudioCodec { get; set; }
    public string? VideoStreamInfo { get; set; }
    public string? Genre { get; set; }
    public string? Difficulty { get; set; }
    public string? Key { get; set; }
    public double? BPM { get; set; }
    public string? VocalPresence { get; set; }
    public string? Quality { get; set; }
}

public class PurchasedImportLogItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string TrackTitle { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = "Success"; // "Success" | "Processing" | "Warning" | "Error"
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class PurchasedTrackWatcherService : IDisposable
{
    private readonly ILibraryService _libraryService;
    private FileSystemWatcher? _watcher;
    private readonly ConcurrentDictionary<string, DateTime> _recentlyProcessed = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<PurchasedImportLogItem> _recentLogs = new();
    private static readonly ConcurrentBag<double> ProcessingTimesMs = [];
    private readonly Lock _watcherLock = new();
    private bool _isDisposed;

    public static (double avg, double max, double min) GetProcessingStats()
    {
        if (ProcessingTimesMs.IsEmpty) return (0, 0, 0);
        var list = ProcessingTimesMs.ToList();
        return (Math.Round(list.Average(), 1), Math.Round(list.Max(), 1), Math.Round(list.Min(), 1));
    }

    public static void RecordProcessingTime(double ms)
    {
        if (ms > 0) ProcessingTimesMs.Add(ms);
    }

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".cdg", ".zip", ".mp4"
    };

    public event EventHandler<PurchasedTrackItem>? TrackImported;
    public event EventHandler<string>? WatcherStatusChanged;
    public event EventHandler<PurchasedImportLogItem>? ImportLogged;

    public IReadOnlyList<PurchasedImportLogItem> RecentLogs => _recentLogs.ToList();

    public static PurchasedTrackWatcherService? Current { get; private set; }

    public PurchasedTrackWatcherService(ILibraryService libraryService)
    {
        _libraryService = libraryService;
        Current = this;
        InitializeWatcher();
    }

    public void UpdateSettings()
    {
        lock (_watcherLock)
        {
            InitializeWatcher();
        }
    }

    public void LogImport(string title, string artist, string source, string status, string message, string? details = null)
    {
        var log = new PurchasedImportLogItem
        {
            TrackTitle = title,
            Artist = artist,
            Source = source,
            Status = status,
            Message = message,
            Details = details
        };

        _recentLogs.Enqueue(log);
        while (_recentLogs.Count > 20)
        {
            _recentLogs.TryDequeue(out _);
        }

        ImportLogged?.Invoke(this, log);
    }

    private void InitializeWatcher()
    {
        try
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnFileCreated;
                _watcher.Renamed -= OnFileRenamed;
                _watcher.Dispose();
                _watcher = null;
            }

            if (!AppSettings.StoreAutoImportEnabled)
            {
                WatcherStatusChanged?.Invoke(this, "Auto-import is disabled.");
                return;
            }

            string folder = AppSettings.StorePurchasedTracksFolder;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                WatcherStatusChanged?.Invoke(this, $"Watcher folder not found: {folder}");
                return;
            }

            _watcher = new FileSystemWatcher(folder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileCreated;
            _watcher.Renamed += OnFileRenamed;
            WatcherStatusChanged?.Invoke(this, $"Watching: {folder}");
            Globals.LogInfo("Lyracist", $"Store purchased tracks watcher active on: {folder}");
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", "Failed to initialize store purchased tracks watcher", ex);
            WatcherStatusChanged?.Invoke(this, $"Watcher error: {ex.Message}");
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        QueueFileProcessing(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        QueueFileProcessing(e.FullPath);
    }

    private void QueueFileProcessing(string filePath)
    {
        if (!AppSettings.StoreAutoImportEnabled) return;

        string ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext) || !SupportedExtensions.Contains(ext)) return;

        // Skip temporary files from browser downloads
        string filename = Path.GetFileName(filePath);
        if (filename.StartsWith("~$", StringComparison.Ordinal) ||
            filename.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase) ||
            filename.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
            filename.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Fire and forget background import with delay to allow download flush
        Task.Run(async () =>
        {
            await ProcessFileWithRetryAsync(filePath);
        });
    }

    private async Task ProcessFileWithRetryAsync(string filePath)
    {
        if (!_inFlight.TryAdd(filePath, 0)) return;

        try
        {
            // Debounce: ignore if processed within last 5 seconds
            if (_recentlyProcessed.TryGetValue(filePath, out var lastTime) && (DateTime.UtcNow - lastTime).TotalSeconds < 5)
            {
                return;
            }

            // Wait for file lock to release (browsers keep write locks until download completes)
            bool ready = await WaitForFileReadyAsync(filePath, TimeSpan.FromSeconds(30));
            if (!ready || !File.Exists(filePath))
            {
                return;
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            // Handle MP3+G pairs (.mp3 and .cdg)
            if (ext == ".cdg")
            {
                string partnerMp3 = Path.ChangeExtension(filePath, ".mp3");
                // Wait briefly for partner if arriving separately
                for (int i = 0; i < 8; i++)
                {
                    if (File.Exists(partnerMp3)) break;
                    await Task.Delay(500);
                }

                if (File.Exists(partnerMp3))
                {
                    await ImportTrackFilesAsync(partnerMp3, filePath);
                    _recentlyProcessed[filePath] = DateTime.UtcNow;
                    _recentlyProcessed[partnerMp3] = DateTime.UtcNow;
                    return;
                }
            }
            else if (ext == ".mp3")
            {
                string partnerCdg = Path.ChangeExtension(filePath, ".cdg");
                // Wait briefly in case the .cdg file is written moments later
                for (int i = 0; i < 8; i++)
                {
                    if (File.Exists(partnerCdg)) break;
                    await Task.Delay(500);
                }

                if (File.Exists(partnerCdg))
                {
                    bool cdgReady = await WaitForFileReadyAsync(partnerCdg, TimeSpan.FromSeconds(10));
                    if (cdgReady)
                    {
                        await ImportTrackFilesAsync(filePath, partnerCdg);
                        _recentlyProcessed[filePath] = DateTime.UtcNow;
                        _recentlyProcessed[partnerCdg] = DateTime.UtcNow;
                        return;
                    }
                }
            }

            // Single file import (.zip, .mp4, or standalone .mp3)
            await ImportTrackFilesAsync(filePath, null);
            _recentlyProcessed[filePath] = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", $"Error processing purchased track: {filePath}", ex);
        }
        finally
        {
            _inFlight.TryRemove(filePath, out _);
        }
    }

    public async Task<PurchasedTrackItem?> ImportFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(ext)) return null;

        string? partnerFile = null;
        if (ext == ".mp3")
        {
            string cdgCandidate = Path.ChangeExtension(filePath, ".cdg");
            if (File.Exists(cdgCandidate)) partnerFile = cdgCandidate;
        }
        else if (ext == ".cdg")
        {
            string mp3Candidate = Path.ChangeExtension(filePath, ".mp3");
            if (File.Exists(mp3Candidate))
            {
                // Swap so main file is the mp3 and partner is cdg
                partnerFile = filePath;
                filePath = mp3Candidate;
            }
        }

        return await ImportTrackFilesAsync(filePath, partnerFile);
    }

    public async Task<List<PurchasedTrackItem>> ImportBatchAsync(IEnumerable<string> filePaths)
    {
        var imported = new List<PurchasedTrackItem>();
        var handledPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in filePaths)
        {
            if (handledPairs.Contains(path)) continue;

            var item = await ImportFileAsync(path);
            if (item != null)
            {
                imported.Add(item);
                handledPairs.Add(item.FilePath);
                string partner = Path.ChangeExtension(item.FilePath, ".cdg");
                if (File.Exists(partner)) handledPairs.Add(partner);
            }
        }

        return imported;
    }

    private async Task<PurchasedTrackItem?> ImportTrackFilesAsync(string primaryFilePath, string? companionFilePath)
    {
        if (!File.Exists(primaryFilePath)) return null;

        var sw = Stopwatch.StartNew();

        // Parse title, artist, karaoke status from the file
        var parsed = ScanningService.ParseStoreDownload(primaryFilePath);

        // Check for matching lyric file (.lrc or .txt)
        string? lyricsPath = FindMatchingLyricsFile(primaryFilePath);

        // Run FFprobe to extract stream technical metadata and tags
        var probe = await FFprobeRunner.ProbeFile(primaryFilePath);

        // Advanced Provider Fingerprinting ("Provider Intelligence")
        string? detectedSource = null;
        string ext = Path.GetExtension(primaryFilePath).ToLowerInvariant();

        // A) ZIP inspection (internal folders, pairs, and signatures)
        if (ext == ".zip")
        {
            detectedSource = InspectZipForProvider(primaryFilePath);
        }

        // B) CDG header inspection (magic header bytes)
        if (string.IsNullOrEmpty(detectedSource))
        {
            string? cdgCandidate = ext == ".cdg" ? primaryFilePath : (companionFilePath?.EndsWith(".cdg", StringComparison.OrdinalIgnoreCase) == true ? companionFilePath : null);
            if (!string.IsNullOrEmpty(cdgCandidate) && File.Exists(cdgCandidate))
            {
                detectedSource = DetectProviderFromCdgHeader(cdgCandidate);
            }
        }

        // C) ID3 tag inspection (if MP3 audio is present)
        if (string.IsNullOrEmpty(detectedSource) && (ext == ".mp3" || companionFilePath?.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) == true))
        {
            detectedSource = DetectProviderFromId3(probe);
        }

        // D) MP4 metadata inspection (if MP4 video)
        if (string.IsNullOrEmpty(detectedSource) && ext == ".mp4")
        {
            detectedSource = DetectProviderFromMp4(probe);
        }

        // E) Filename heuristics fallback
        if (string.IsNullOrEmpty(detectedSource))
        {
            string heuristic = DetectSource(primaryFilePath, parsed.Title, parsed.Artist);
            if (heuristic != "Local")
            {
                detectedSource = heuristic;
            }
        }

        // F) Referrer hint fallback (last clicked store provider)
        if (string.IsNullOrEmpty(detectedSource) && !string.IsNullOrWhiteSpace(ReferrerHint))
        {
            detectedSource = ReferrerHint;
        }

        // G) Preferred Provider setting hint fallback
        if (string.IsNullOrEmpty(detectedSource) || detectedSource == "Local")
        {
            string pref = AppSettings.PreferredProvider;
            if (!string.IsNullOrWhiteSpace(pref))
            {
                string resolved = pref switch
                {
                    "KV" => "Karaoke Version",
                    "PT" => "Party Tyme",
                    "SF" or "Sunfly" => "Sunfly",
                    "KC" or "Karaoke.com" => "Karaoke.com",
                    _ => pref
                };
                detectedSource ??= resolved;
            }
        }

        string source = detectedSource ?? "Local";

        // ==========================================
        // SMART IMPORT RULES:
        // A) Auto-rename files: "Artist - Title (Provider).ext"
        // B) Auto-tag genres
        // C) Auto-tag difficulty
        // D) Auto-tag key
        // E) Auto-tag BPM
        // F) Auto-tag vocal presence
        // G) Auto-tag file quality
        // ==========================================
        RenameImportedFiles(ref primaryFilePath, ref companionFilePath, ref lyricsPath, parsed.Title, parsed.Artist, source);

        string? detectedGenre = FFprobeRunner.DetectGenre(source, probe);
        string detectedDifficulty = FFprobeRunner.DetectDifficulty(probe);
        string? detectedKey = FFprobeRunner.DetectKey(probe);
        double? detectedBpm = FFprobeRunner.DetectBpm(probe);
        string detectedVocalPresence = FFprobeRunner.DetectVocalPresence(probe);
        string detectedQuality = FFprobeRunner.DetectQuality(probe);

        LogImport(parsed.Title, parsed.Artist, source, "Processing", $"Importing {Path.GetFileName(primaryFilePath)}");

        // Determine destination folder routing
        bool isKaraoke = parsed.IsKaraoke || companionFilePath != null || primaryFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        string finalPrimaryPath = primaryFilePath;
        string? finalCompanionPath = companionFilePath;
        string? finalLyricsPath = lyricsPath;

        if (AppSettings.StoreMoveFilesToTarget)
        {
            string targetFolder = isKaraoke ? AppSettings.StoreTargetKaraokeFolder : AppSettings.StoreTargetMusicFolder;

            if (!string.IsNullOrWhiteSpace(targetFolder))
            {
                try
                {
                    Directory.CreateDirectory(targetFolder);

                    string primaryDest = GetUniqueDestinationPath(targetFolder, Path.GetFileName(primaryFilePath));
                    File.Move(primaryFilePath, primaryDest, true);
                    finalPrimaryPath = primaryDest;

                    if (!string.IsNullOrEmpty(companionFilePath) && File.Exists(companionFilePath))
                    {
                        string companionName = Path.GetFileNameWithoutExtension(primaryDest) + Path.GetExtension(companionFilePath);
                        string companionDest = Path.Combine(targetFolder, companionName);
                        File.Move(companionFilePath, companionDest, true);
                        finalCompanionPath = companionDest;
                    }

                    if (!string.IsNullOrEmpty(lyricsPath) && File.Exists(lyricsPath))
                    {
                        string lyricsName = Path.GetFileNameWithoutExtension(primaryDest) + Path.GetExtension(lyricsPath);
                        string lyricsDest = Path.Combine(targetFolder, lyricsName);
                        File.Move(lyricsPath, lyricsDest, true);
                        finalLyricsPath = lyricsDest;
                    }
                }
                catch (Exception ex)
                {
                    Globals.LogError("Lyracist", $"Failed to route purchased track to target folder for: {primaryFilePath}", ex);
                    finalPrimaryPath = primaryFilePath;
                    finalCompanionPath = companionFilePath;
                    finalLyricsPath = lyricsPath;
                }
            }
        }

        // Optional FFmpeg Processing Pipeline
        bool normalized = false;
        bool silenceTrimmed = false;
        string? waveformPath = null;

        string primaryExt = Path.GetExtension(finalPrimaryPath).ToLowerInvariant();
        bool isAudioOnly = primaryExt == ".mp3" && !finalPrimaryPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        if (isAudioOnly && AppSettings.StoreNormalizeAudioOnImport)
        {
            string normDest = Path.Combine(Path.GetDirectoryName(finalPrimaryPath)!, Path.GetFileNameWithoutExtension(finalPrimaryPath) + "_norm.mp3");
            bool ok = await FFmpegService.NormalizeAudioAsync(finalPrimaryPath, normDest);
            if (ok && File.Exists(normDest))
            {
                File.Move(normDest, finalPrimaryPath, true);
                normalized = true;
                LogImport(parsed.Title, parsed.Artist, source, "Processing", "Audio normalized to -16 LUFS via FFmpeg loudnorm");
                StoreNotificationService.Instance.ShowNormalizationComplete(parsed.Title, parsed.Artist);
            }
        }

        if (isAudioOnly && AppSettings.StoreTrimSilenceOnImport)
        {
            string trimDest = Path.Combine(Path.GetDirectoryName(finalPrimaryPath)!, Path.GetFileNameWithoutExtension(finalPrimaryPath) + "_trimmed.mp3");
            bool ok = await FFmpegService.TrimSilenceAsync(finalPrimaryPath, trimDest);
            if (ok && File.Exists(trimDest))
            {
                File.Move(trimDest, finalPrimaryPath, true);
                silenceTrimmed = true;
                LogImport(parsed.Title, parsed.Artist, source, "Processing", "Silence trimmed via FFmpeg silenceremove");
                StoreNotificationService.Instance.ShowSilenceTrimmed(parsed.Title, parsed.Artist);
            }
        }

        if (AppSettings.StoreGenerateWaveformOnImport)
        {
            string waveDest = Path.Combine(Path.GetDirectoryName(finalPrimaryPath)!, Path.GetFileNameWithoutExtension(finalPrimaryPath) + "_waveform.png");
            bool ok = await FFmpegService.GenerateWaveformPreviewAsync(finalPrimaryPath, waveDest);
            if (ok && File.Exists(waveDest))
            {
                waveformPath = waveDest;
                LogImport(parsed.Title, parsed.Artist, source, "Processing", "Waveform preview generated via FFmpeg");
                StoreNotificationService.Instance.ShowWaveformGenerated(parsed.Title, parsed.Artist);
            }
        }

        // Insert or update in Database
        try
        {
            using var context = new LyracistDbContext();
            var searchService = new SearchService(context);

            var existing = await context.Songs.FirstOrDefaultAsync(s => s.FilePath == finalPrimaryPath);
            int songId;

            string typeLabel = isKaraoke
                ? (string.IsNullOrEmpty(parsed.KaraokeType) ? (finalPrimaryPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? "ZIPCDG" : "MP3G") : parsed.KaraokeType)
                : "Audio";

            double duration = probe.Duration > 0 ? probe.Duration : 0;

            if (existing != null)
            {
                existing.Title = parsed.Title;
                existing.Artist = parsed.Artist;
                existing.IsKaraoke = isKaraoke;
                existing.KaraokeType = typeLabel;
                if (duration > 0) existing.Duration = duration;
                if (!string.IsNullOrWhiteSpace(detectedGenre)) existing.Genre = detectedGenre;
                existing.Difficulty = detectedDifficulty;
                if (!string.IsNullOrWhiteSpace(detectedKey)) existing.Key = detectedKey;
                if (detectedBpm.HasValue)
                {
                    existing.BPM = detectedBpm.Value;
                    existing.TempoDefault = detectedBpm.Value;
                }
                existing.VocalPresence = detectedVocalPresence;
                existing.Quality = detectedQuality;

                if (!string.IsNullOrWhiteSpace(source) && source != "Local")
                {
                    existing.Tags = AppendTag(existing.Tags, source);
                }
                if (probe.HasDualAudio)
                {
                    existing.Tags = AppendTag(existing.Tags, "Dual-Audio");
                }
                if (!string.IsNullOrWhiteSpace(detectedGenre)) existing.Tags = AppendTag(existing.Tags, detectedGenre);
                existing.Tags = AppendTag(existing.Tags, detectedDifficulty);
                if (!string.IsNullOrWhiteSpace(detectedKey)) existing.Tags = AppendTag(existing.Tags, $"Key:{detectedKey}");
                if (detectedBpm.HasValue) existing.Tags = AppendTag(existing.Tags, $"{detectedBpm}BPM");
                existing.Tags = AppendTag(existing.Tags, detectedVocalPresence);
                existing.Tags = AppendTag(existing.Tags, $"Quality:{detectedQuality}");

                await context.SaveChangesAsync();
                await searchService.IndexSongsBatch([existing]);
                songId = existing.SongId;
            }
            else
            {
                string tags = source;
                if (probe.HasDualAudio) tags = AppendTag(tags, "Dual-Audio");
                if (!string.IsNullOrWhiteSpace(detectedGenre)) tags = AppendTag(tags, detectedGenre);
                tags = AppendTag(tags, detectedDifficulty);
                if (!string.IsNullOrWhiteSpace(detectedKey)) tags = AppendTag(tags, $"Key:{detectedKey}");
                if (detectedBpm.HasValue) tags = AppendTag(tags, $"{detectedBpm}BPM");
                tags = AppendTag(tags, detectedVocalPresence);
                tags = AppendTag(tags, $"Quality:{detectedQuality}");

                var newSong = new Song
                {
                    Title = parsed.Title,
                    Artist = parsed.Artist,
                    FilePath = finalPrimaryPath,
                    IsKaraoke = isKaraoke,
                    KaraokeType = typeLabel,
                    Genre = detectedGenre ?? string.Empty,
                    Difficulty = detectedDifficulty,
                    Key = detectedKey,
                    BPM = detectedBpm,
                    VocalPresence = detectedVocalPresence,
                    Quality = detectedQuality,
                    Tags = tags,
                    Duration = duration,
                    KeyDefault = 0,
                    TempoDefault = detectedBpm ?? 1.0,
                    DateAdded = DateTime.UtcNow
                };

                context.Songs.Add(newSong);
                await context.SaveChangesAsync();
                await searchService.IndexSongsBatch([newSong]);
                songId = newSong.SongId;
            }

            _libraryService.NotifyLibraryUpdated();

            var trackItem = new PurchasedTrackItem
            {
                SongId = songId,
                Title = parsed.Title,
                Artist = parsed.Artist,
                Source = source,
                FilePath = finalPrimaryPath,
                KaraokeType = typeLabel,
                IsKaraoke = isKaraoke,
                AddedAt = DateTime.Now,
                Status = "Imported",
                LyricsPath = finalLyricsPath,
                HasDualAudio = probe.HasDualAudio,
                Normalized = normalized,
                SilenceTrimmed = silenceTrimmed,
                WaveformPath = waveformPath,
                DurationSeconds = probe.Duration > 0 ? probe.Duration : null,
                BitrateKbps = probe.BitrateKbps > 0 ? probe.BitrateKbps : null,
                AudioCodec = !string.IsNullOrEmpty(probe.AudioCodec) ? probe.AudioCodec : null,
                VideoStreamInfo = !string.IsNullOrEmpty(probe.VideoStreamInfo) ? probe.VideoStreamInfo : null,
                Genre = detectedGenre,
                Difficulty = detectedDifficulty,
                Key = detectedKey,
                BPM = detectedBpm,
                VocalPresence = detectedVocalPresence,
                Quality = detectedQuality
            };

            TrackImported?.Invoke(this, trackItem);

            StoreNotificationService.Instance.ShowTrackImported(
                parsed.Title,
                parsed.Artist,
                source,
                typeLabel,
                normalized,
                silenceTrimmed,
                !string.IsNullOrEmpty(waveformPath));

            string dualMsg = probe.HasDualAudio ? " [Dual Audio Streams]" : "";
            string smartTagRemarks = $" [Genre: {detectedGenre ?? "N/A"}, Diff: {detectedDifficulty}, Key: {detectedKey ?? "N/A"}, BPM: {detectedBpm?.ToString() ?? "N/A"}, Vocals: {detectedVocalPresence}, Quality: {detectedQuality}]";
            LogImport(parsed.Title, parsed.Artist, source, "Success", $"Imported successfully ({typeLabel}){dualMsg}{smartTagRemarks}", Path.GetFileName(finalPrimaryPath));
            Globals.LogInfo("Lyracist", $"Store imported track: '{parsed.Title}' by '{parsed.Artist}' [{source}] at {finalPrimaryPath}");

            sw.Stop();
            RecordProcessingTime(sw.Elapsed.TotalMilliseconds);

            return trackItem;
        }
        catch (Exception ex)
        {
            LogImport(parsed.Title, parsed.Artist, source, "Error", $"Database insertion failed: {ex.Message}");
            Globals.LogError("Lyracist", $"Database insertion failed for store track: {finalPrimaryPath}", ex);
            return null;
        }
    }

    public static string? FindMatchingLyricsFile(string filePath)
    {
        try
        {
            string dir = Path.GetDirectoryName(filePath)!;
            string baseName = Path.GetFileNameWithoutExtension(filePath);

            bool preferTxt = string.Equals(AppSettings.PreferredLyricsFormat, "TXT", StringComparison.OrdinalIgnoreCase);
            string firstExt = preferTxt ? ".txt" : ".lrc";
            string secondExt = preferTxt ? ".lrc" : ".txt";

            string first = Path.Combine(dir, baseName + firstExt);
            if (File.Exists(first)) return first;

            string second = Path.Combine(dir, baseName + secondExt);
            if (File.Exists(second)) return second;
        }
        catch { }

        return null;
    }

    /// <summary>
    /// In-memory referrer hint from the last clicked store provider button.
    /// </summary>
    public static string? ReferrerHint { get; set; }

    /// <summary>
    /// Inspects internal ZIP archive folder structure and file entries for provider signatures.
    /// </summary>
    public static string? InspectZipForProvider(string zipPath)
    {
        return ProviderRegistry.Instance.DetectProviderFromZip(zipPath)?.Name;
    }

    /// <summary>
    /// Reads the first 24 bytes of a CDG file to identify provider header fingerprints.
    /// KV -> 0x01 0x0F
    /// PT -> 0x02 0x0A
    /// SF -> 0x03 0x0C
    /// </summary>
    public static string? DetectProviderFromCdgHeader(string cdgPath)
    {
        return ProviderRegistry.Instance.DetectProviderFromCdg(cdgPath)?.Name;
    }

    /// <summary>
    /// Checks FFprobe ID3 tag extraction for provider signatures (TXXX:KV, TXXX:PT, TXXX:SF, TXXX:KCOM).
    /// </summary>
    public static string? DetectProviderFromId3(FFprobeResult metadata)
    {
        return ProviderRegistry.Instance.DetectProviderFromId3(metadata)?.Name;
    }

    /// <summary>
    /// Checks MP4 container metadata (title, artist, comment, and general tags) for provider signatures.
    /// </summary>
    public static string? DetectProviderFromMp4(FFprobeResult metadata)
    {
        return ProviderRegistry.Instance.DetectProviderFromMp4(metadata)?.Name;
    }

    public static string DetectSource(string filePath, string title, string artist)
    {
        string combined = $"{filePath} {title} {artist}";
        return ProviderRegistry.Instance.DetectProviderFromFilename(combined)?.Name ?? "Local";
    }

    private static string AppendTag(string? existingTags, string newTag)
    {
        if (string.IsNullOrWhiteSpace(existingTags)) return newTag;
        var tags = existingTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!tags.Contains(newTag, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(newTag);
        }
        return string.Join(", ", tags);
    }

    private static string GetUniqueDestinationPath(string folder, string fileName)
    {
        string destination = Path.Combine(folder, fileName);
        if (!File.Exists(destination)) return destination;

        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        int counter = 1;

        while (File.Exists(destination))
        {
            destination = Path.Combine(folder, $"{baseName} ({counter}){ext}");
            counter++;
        }

        return destination;
    }

    private static async Task<bool> WaitForFileReadyAsync(string filePath, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        long lastLength = -1;

        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                if (!File.Exists(filePath)) return false;

                var fileInfo = new FileInfo(filePath);
                long currentLength = fileInfo.Length;

                // Test opening with read/write sharing
                using (var stream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    // Successfully locked exclusively - check that file length has stabilized
                    if (currentLength > 0 && currentLength == lastLength)
                    {
                        return true;
                    }
                }

                lastLength = currentLength;
            }
            catch (IOException)
            {
                // File still being written or locked by browser
            }
            catch (UnauthorizedAccessException)
            {
                // In use or permission restriction
            }

            await Task.Delay(500);
        }

        return false;
    }

    // ==========================================
    // SMART IMPORT: AUTO-RENAME IMPORTED FILES
    // ==========================================

    public static string GetProviderAbbreviation(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider) || provider == "Local")
        {
            string pref = AppSettings.PreferredProvider;
            if (!string.IsNullOrWhiteSpace(pref))
            {
                return pref switch
                {
                    "KV" or "Karaoke Version" => "KV",
                    "PT" or "Party Tyme" => "PT",
                    "Sunfly" or "SF" => "SF",
                    "Karaoke.com" or "KC" or "KCOM" => "KCOM",
                    _ => pref
                };
            }
        }

        return provider switch
        {
            "Karaoke Version" => "KV",
            "Party Tyme" => "PT",
            "Sunfly" => "SF",
            "Karaoke.com" => "KCOM",
            _ => provider
        };
    }

    private static string SanitizeFilenamePart(string part)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder();
        foreach (char c in part)
        {
            if (Array.IndexOf(invalid, c) < 0 && c != '/' && c != '\\')
                sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    public static void RenameImportedFiles(
        ref string primaryFilePath,
        ref string? companionFilePath,
        ref string? lyricsPath,
        string title,
        string artist,
        string provider)
    {
        try
        {
            string abbr = GetProviderAbbreviation(provider);
            string cleanArtist = SanitizeFilenamePart(artist);
            if (string.IsNullOrWhiteSpace(cleanArtist)) cleanArtist = "Unknown Artist";
            string cleanTitle = SanitizeFilenamePart(title);
            if (string.IsNullOrWhiteSpace(cleanTitle)) cleanTitle = "Unknown Title";

            string newBaseName = $"{cleanArtist} - {cleanTitle} ({abbr})";
            string dir = Path.GetDirectoryName(primaryFilePath)!;
            string primaryExt = Path.GetExtension(primaryFilePath);
            string newPrimary = Path.Combine(dir, newBaseName + primaryExt);

            if (!primaryFilePath.Equals(newPrimary, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(primaryFilePath))
                {
                    File.Move(primaryFilePath, newPrimary, true);
                    primaryFilePath = newPrimary;
                }
            }

            if (!string.IsNullOrEmpty(companionFilePath) && File.Exists(companionFilePath))
            {
                string compExt = Path.GetExtension(companionFilePath);
                string newComp = Path.Combine(dir, newBaseName + compExt);
                if (!companionFilePath.Equals(newComp, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(companionFilePath, newComp, true);
                    companionFilePath = newComp;
                }
            }

            if (!string.IsNullOrEmpty(lyricsPath) && File.Exists(lyricsPath))
            {
                string lrcExt = Path.GetExtension(lyricsPath);
                string newLrc = Path.Combine(dir, newBaseName + lrcExt);
                if (!lyricsPath.Equals(newLrc, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(lyricsPath, newLrc, true);
                    lyricsPath = newLrc;
                }
            }
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", $"Failed to auto-rename imported track files: {primaryFilePath}", ex);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        lock (_watcherLock)
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnFileCreated;
                _watcher.Renamed -= OnFileRenamed;
                _watcher.Dispose();
                _watcher = null;
            }
        }
    }
}
