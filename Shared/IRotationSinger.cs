// Edited on Sep 5, 2026 @ 16:20:00 -> Add EstimatedPerformanceSeconds/EstimatedWaitMinutes for rotation-screen wait-time badges
namespace Lyracist.Shared
{
    public interface IRotationSinger
    {
        bool IsCurrent { get; set; }
        bool IsNext { get; set; }
        bool IsInactive { get; set; }
        bool IsPaused { get; set; }
        bool IsMusic { get; set; }
        bool IsRotationStart { get; set; }
        bool HasSungInLastRound { get; set; }

        /// <summary>
        /// This entry's own estimated performance length in seconds (typically the queued song's
        /// known duration + 30s), or &lt;= 0 if unknown/unresolved. Used by
        /// <see cref="RotationHelpers.RecalculateEstimatedWaits{T}"/> to add up how long everyone
        /// ahead of a waiting singer is expected to take.
        /// </summary>
        double EstimatedPerformanceSeconds { get; set; }

        /// <summary>
        /// Computed estimated wait, in whole minutes, until this entry is up - 0 for the current
        /// singer (already performing) or anyone not actively waiting. Set by
        /// <see cref="RotationHelpers.RecalculateEstimatedWaits{T}"/>.
        /// </summary>
        int EstimatedWaitMinutes { get; set; }
    }
}

