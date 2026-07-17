using System;
using System.Collections.Generic;
using System.Linq;

namespace Lyracist.Shared
{
    /// <summary>
    /// Pure rotation-list helpers that operate on a collection implementing <see cref="IRotationSinger"/>.
    /// Shared between Lyracist and KSRotation so their behavior cannot drift apart.
    /// </summary>
    public static class RotationHelpers
    {
        /// <summary>
        /// Sets <see cref="IRotationSinger.IsNext"/> on the first active singer after <paramref name="currentEntry"/>,
        /// wrapping around the list. Clears any previous IsNext flag first.
        /// </summary>
        public static void MarkNextSinger<T>(IList<T> singers, T currentEntry) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(currentEntry);

            foreach (T s in singers)
                s.IsNext = false;

            int startIndex = singers.IndexOf(currentEntry);
            if (startIndex == -1) return;

            int count = singers.Count;

            for (int i = 1; i < count; i++)
            {
                T candidate = singers[(startIndex + i) % count];
                if (!candidate.IsInactive && !candidate.IsPaused)
                {
                    candidate.IsNext = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Recalculates and applies the IsNext highlight for the next active singer after
        /// whichever entry currently has <see cref="IRotationSinger.IsCurrent"/> set.
        /// Clears all IsNext flags when there is no current singer.
        /// </summary>
        public static void UpdateNextSingerHighlight<T>(IList<T> singers) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);

            T? current = singers.FirstOrDefault(s => s.IsCurrent && !s.IsInactive && !s.IsPaused);
            if (current != null)
            {
                MarkNextSinger(singers, current);
            }
            else
            {
                foreach (T s in singers)
                    s.IsNext = false;
            }
        }

        /// <summary>
        /// Promotes <paramref name="entry"/> to the current singer: reactivates it if paused, clears
        /// IsCurrent/IsNext on every other singer, and — if there was a previous current singer — marks
        /// that singer as IsNext so they resume right after. Falls back to standard rotation order when
        /// there was no previous current singer (or it was inactive).
        /// </summary>
        public static void SetCurrentSinger<T>(IList<T> singers, T entry) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(entry);

            T? previousCurrent = singers.FirstOrDefault(s => s.IsCurrent);

            if (entry.IsInactive)
            {
                entry.IsInactive = false;
            }
            if (entry.IsPaused)
            {
                entry.IsPaused = false;
            }

            foreach (T s in singers)
            {
                s.IsCurrent = false;
                s.IsNext = false;
            }

            entry.IsCurrent = true;

            if (previousCurrent != null && previousCurrent != entry && !previousCurrent.IsInactive && !previousCurrent.IsPaused)
            {
                previousCurrent.IsNext = true;
            }
            else
            {
                UpdateNextSingerHighlight(singers);
            }
        }

        /// <summary>
        /// Gets up to <paramref name="maxCount"/> active, non-paused singers sequentially following <paramref name="currentEntry"/>.
        /// </summary>
        public static List<T> GetNextActiveSingers<T>(IList<T> singers, T currentEntry, int maxCount) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(currentEntry);

            var list = new List<T>();
            int startIndex = singers.IndexOf(currentEntry);
            if (startIndex == -1) return list;

            int count = singers.Count;
            for (int i = 1; i <= count && list.Count < maxCount; i++)
            {
                T candidate = singers[(startIndex + i) % count];
                if (candidate != currentEntry && !candidate.IsInactive && !candidate.IsPaused)
                {
                    list.Add(candidate);
                }
            }
            return list;
        }
    }
}
