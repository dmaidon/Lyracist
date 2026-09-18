// Edited on Sep 17, 2026 @ 23:31:00 -> Add IsSkipped property for single-round singer skip
using System;

namespace Lyracist.Shared
{
    public interface IRotationSinger
    {
        /// <summary>Stable identity, unique for the lifetime of this entry - used to reference a
        /// linked partner reliably even if <c>Name</c> is edited later.</summary>
        Guid Id { get; }

        /// <summary>
        /// When set, the <see cref="Id"/> of another entry this one is linked to - the pair always
        /// stays adjacent in the rotation (see <see cref="RotationHelpers.EnforceLinkedAdjacency{T}"/>)
        /// and no other singer can be inserted between them. A link is always mutual: if A is linked
        /// to B, B.LinkedSingerId equals A.Id. Set/cleared via
        /// <see cref="RotationHelpers.LinkSingers{T}"/>/<see cref="RotationHelpers.UnlinkSinger{T}"/>.
        /// </summary>
        Guid? LinkedSingerId { get; set; }

        bool IsCurrent { get; set; }
        bool IsNext { get; set; }
        bool IsInactive { get; set; }
        bool IsPaused { get; set; }
        bool IsSkipped { get; set; }
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

