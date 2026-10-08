// Created on Oct 7, 2026 @ 19:43:00 -> Smart shuffle next track selector with BPM matching, artist rotation, and history avoidance
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lyracist.Shared;

/// <summary>
/// Pure selection logic for Smart Shuffle in background filler music.
/// </summary>
public static class NextTrackSelector
{
    private static readonly Random DefaultRng = new();

    /// <summary>
    /// Picks the next track index from the candidate list based on BPM proximity (+-8%, allowing half/double tempo),
    /// avoiding same-artist back-to-back, and avoiding recently played tracks.
    /// </summary>
    public static int SelectNextIndex(
        FillInTrack? currentTrack,
        IReadOnlyList<FillInTrack> candidates,
        IReadOnlyCollection<string>? recentPaths = null,
        Random? rng = null)
    {
        if (candidates == null || candidates.Count == 0) return -1;
        if (candidates.Count == 1) return 0;

        rng ??= DefaultRng;
        recentPaths ??= [];

        var recentSet = new HashSet<string>(recentPaths, StringComparer.OrdinalIgnoreCase);

        // 1. Initial pool: exclude the currently playing track by path
        var pool = Enumerable.Range(0, candidates.Count)
            .Where(i => currentTrack == null || !string.Equals(candidates[i].Path, currentTrack.Path, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pool.Count == 0)
        {
            pool = Enumerable.Range(0, candidates.Count).ToList();
        }

        // 2. Filter out recent paths if that still leaves options
        var nonRecentPool = pool
            .Where(i => !recentSet.Contains(candidates[i].Path))
            .ToList();

        var workingPool = nonRecentPool.Count > 0 ? nonRecentPool : pool;

        // 3. Avoid same artist back-to-back if artist is known
        if (!string.IsNullOrWhiteSpace(currentTrack?.Artist))
        {
            string currentArtist = currentTrack.Artist.Trim();
            var diffArtistPool = workingPool
                .Where(i => string.IsNullOrWhiteSpace(candidates[i].Artist) ||
                            !string.Equals(candidates[i].Artist!.Trim(), currentArtist, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (diffArtistPool.Count > 0)
            {
                workingPool = diffArtistPool;
            }
        }

        // 4. BPM matching if current track has a known BPM
        if (currentTrack?.Bpm != null && currentTrack.Bpm > 0)
        {
            double targetBpm = currentTrack.Bpm.Value;
            var bpmMatchedPool = workingPool
                .Where(i => candidates[i].Bpm.HasValue &&
                            IsBpmCompatible(candidates[i].Bpm!.Value, targetBpm, 0.08))
                .ToList();

            if (bpmMatchedPool.Count > 0)
            {
                return bpmMatchedPool[rng.Next(bpmMatchedPool.Count)];
            }
        }

        // 5. Fallback: random selection from the working pool
        return workingPool[rng.Next(workingPool.Count)];
    }

    /// <summary>
    /// Checks if a candidate BPM matches the target tempo within the given tolerance (+-8%),
    /// allowing for double-time (2x) and half-time (0.5x) harmonic matches.
    /// </summary>
    public static bool IsBpmCompatible(double candidateBpm, double targetBpm, double tolerance = 0.08)
    {
        if (candidateBpm <= 0 || targetBpm <= 0) return false;

        // Direct tempo match
        if (Math.Abs(candidateBpm - targetBpm) / targetBpm <= tolerance) return true;

        // Double tempo match (e.g. 140 vs 70)
        double doubleTarget = targetBpm * 2.0;
        if (Math.Abs(candidateBpm - doubleTarget) / doubleTarget <= tolerance) return true;

        // Half tempo match (e.g. 75 vs 150)
        double halfTarget = targetBpm / 2.0;
        if (Math.Abs(candidateBpm - halfTarget) / halfTarget <= tolerance) return true;

        return false;
    }
}
