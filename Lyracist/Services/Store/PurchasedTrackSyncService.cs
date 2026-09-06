// Edited on Sep 6, 2026 @ 13:03:30 -> Trigger StoreNotificationService toast on sync completion
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyracist.Core.Helpers;
using Lyracist.Data;
using Lyracist.Shared;
using Microsoft.EntityFrameworkCore;

namespace Lyracist.Services.Store;

public class StoreSyncResult
{
    public int TotalScanned { get; set; }
    public int TotalImported { get; set; }
    public int TotalSkipped { get; set; }
    public int TotalErrors { get; set; }
    public List<string> ProvidersInvolved { get; set; } = new();
    public TimeSpan AverageProcessingTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public string ScannedFolder { get; set; } = string.Empty;

    public string AverageProcessingTimeText => AverageProcessingTime.TotalSeconds >= 1
        ? $"{AverageProcessingTime.TotalSeconds:F1}s"
        : $"{AverageProcessingTime.TotalMilliseconds:F0}ms";

    public string TotalDurationText => TotalDuration.TotalSeconds >= 1
        ? $"{TotalDuration.TotalSeconds:F1}s"
        : $"{TotalDuration.TotalMilliseconds:F0}ms";

    public string ProvidersInvolvedText => ProvidersInvolved.Count > 0
        ? string.Join(", ", ProvidersInvolved)
        : "None";
}

public class PurchasedTrackSyncService
{
    private readonly PurchasedTrackWatcherService _watcherService;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".cdg", ".zip", ".mp4"
    };

    public PurchasedTrackSyncService(PurchasedTrackWatcherService watcherService)
    {
        _watcherService = watcherService;
    }

    /// <summary>
    /// Scans the target folder for new purchased tracks (ZIP, MP3+G, MP4, Lyrics) and processes them into the library.
    /// </summary>
    public async Task<StoreSyncResult> SyncPurchasedTracksAsync(
        string? folderPath = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var totalSw = Stopwatch.StartNew();
        var result = new StoreSyncResult();

        // 1. Resolve target folder
        string targetFolder = ResolveSyncFolder(folderPath);
        result.ScannedFolder = targetFolder;

        if (!Directory.Exists(targetFolder))
        {
            progress?.Report($"Folder not found: {targetFolder}");
            totalSw.Stop();
            result.TotalDuration = totalSw.Elapsed;
            return result;
        }

        // Use PreferredProvider as fallback hint for Store Sync if ReferrerHint not already set
        if (string.IsNullOrWhiteSpace(PurchasedTrackWatcherService.ReferrerHint))
        {
            PurchasedTrackWatcherService.ReferrerHint = AppSettings.PreferredProvider switch
            {
                "KV" => "Karaoke Version",
                "PT" => "Party Tyme",
                "Sunfly" => "Sunfly",
                "Karaoke.com" => "Karaoke.com",
                _ => AppSettings.PreferredProvider
            };
        }

        progress?.Report($"Scanning {targetFolder} for purchased tracks...");

        // 2. Discover media files
        var allFiles = Directory.GetFiles(targetFolder, "*.*", SearchOption.TopDirectoryOnly);
        var mediaFiles = allFiles
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
            .Where(f => !IsTemporaryFile(f))
            .OrderBy(f => f)
            .ToList();

        result.TotalScanned = mediaFiles.Count;
        if (mediaFiles.Count == 0)
        {
            progress?.Report("No purchased tracks found to sync.");
            totalSw.Stop();
            result.TotalDuration = totalSw.Elapsed;
            return result;
        }

        // 3. Pair MP3+G files
        var candidatePrimaries = new List<string>();
        var handledCompanionFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in mediaFiles)
        {
            if (handledCompanionFiles.Contains(file)) continue;

            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".cdg")
            {
                string mp3Candidate = Path.ChangeExtension(file, ".mp3");
                if (File.Exists(mp3Candidate))
                {
                    handledCompanionFiles.Add(file);
                    handledCompanionFiles.Add(mp3Candidate);
                    candidatePrimaries.Add(mp3Candidate);
                }
                else
                {
                    // Standalone CDG without audio is skipped
                    handledCompanionFiles.Add(file);
                    result.TotalSkipped++;
                }
            }
            else if (ext == ".mp3")
            {
                handledCompanionFiles.Add(file);
                string cdgCandidate = Path.ChangeExtension(file, ".cdg");
                if (File.Exists(cdgCandidate))
                {
                    handledCompanionFiles.Add(cdgCandidate);
                }
                candidatePrimaries.Add(file);
            }
            else
            {
                handledCompanionFiles.Add(file);
                candidatePrimaries.Add(file);
            }
        }

        // 4. Ingest and process candidate tracks
        var processingTimes = new List<double>();
        var providersSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < candidatePrimaries.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.TotalSkipped += (candidatePrimaries.Count - i);
                break;
            }

            string primaryPath = candidatePrimaries[i];
            progress?.Report($"Importing ({i + 1}/{candidatePrimaries.Count}): {Path.GetFileName(primaryPath)}");

            // Check if already in database (if files are kept in place without moving)
            try
            {
                using var context = new LyracistDbContext();
                bool alreadyInDb = await context.Songs.AnyAsync(s => s.FilePath == primaryPath, cancellationToken);
                if (alreadyInDb)
                {
                    result.TotalSkipped++;
                    continue;
                }
            }
            catch (Exception ex)
            {
                Globals.LogError("Lyracist", $"Error querying DB for existing track: {primaryPath}", ex);
            }

            var itemSw = Stopwatch.StartNew();
            try
            {
                var imported = await _watcherService.ImportFileAsync(primaryPath);
                itemSw.Stop();

                if (imported != null)
                {
                    result.TotalImported++;
                    processingTimes.Add(itemSw.Elapsed.TotalMilliseconds);

                    if (!string.IsNullOrWhiteSpace(imported.Source))
                    {
                        providersSet.Add(imported.Source);
                    }
                }
                else
                {
                    result.TotalSkipped++;
                }
            }
            catch (Exception ex)
            {
                itemSw.Stop();
                result.TotalErrors++;
                Globals.LogError("Lyracist", $"Store sync error on {primaryPath}", ex);
            }
        }

        result.ProvidersInvolved = providersSet.OrderBy(p => p).ToList();
        if (processingTimes.Count > 0)
        {
            result.AverageProcessingTime = TimeSpan.FromMilliseconds(processingTimes.Average());
        }

        totalSw.Stop();
        result.TotalDuration = totalSw.Elapsed;

        progress?.Report($"Store sync complete: {result.TotalImported} imported, {result.TotalSkipped} skipped, {result.TotalErrors} errors.");
        StoreNotificationService.Instance.ShowSyncCompleted(result.TotalImported);
        return result;
    }

    public static string ResolveSyncFolder(string? preferredFolder)
    {
        if (!string.IsNullOrWhiteSpace(preferredFolder) && Directory.Exists(preferredFolder))
            return preferredFolder;

        if (!string.IsNullOrWhiteSpace(AppSettings.StorePurchasedTracksFolder) && Directory.Exists(AppSettings.StorePurchasedTracksFolder))
            return AppSettings.StorePurchasedTracksFolder;

        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads))
            return downloads;

        return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    }

    private static bool IsTemporaryFile(string filePath)
    {
        string filename = Path.GetFileName(filePath);
        return filename.StartsWith("~$", StringComparison.Ordinal) ||
               filename.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase) ||
               filename.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
               filename.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }
}
