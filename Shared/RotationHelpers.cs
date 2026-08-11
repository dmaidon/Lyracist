// Edited on Aug 10, 2026 @ 16:50:00 -> Optimized loops/allocations & added GetCurrentSinger, GetActiveSingerCount, ClearHighlights helpers
using System;
using System.Collections.Generic;

namespace Lyracist.Shared
{
    /// <summary>
    /// Pure rotation-list helpers that operate on a collection implementing <see cref="IRotationSinger"/>.
    /// Shared between Lyracist and KSRotation so their behavior cannot drift apart.
    /// </summary>
    public static class RotationHelpers
    {
        /// <summary>
        /// Gets the current active, non-paused singer in <paramref name="singers"/>, or null if none.
        /// </summary>
        public static T? GetCurrentSinger<T>(IList<T> singers) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);

            int count = singers.Count;
            for (int i = 0; i < count; i++)
            {
                T s = singers[i];
                if (s.IsCurrent && !s.IsInactive && !s.IsPaused)
                    return s;
            }
            return null;
        }

        /// <summary>
        /// Returns true if there is an active, non-paused current singer in <paramref name="singers"/>.
        /// </summary>
        public static bool HasActiveCurrentSinger<T>(IList<T> singers) where T : class, IRotationSinger
        {
            return GetCurrentSinger(singers) != null;
        }

        /// <summary>
        /// Counts the number of active, non-paused singers in <paramref name="singers"/>.
        /// </summary>
        public static int GetActiveSingerCount<T>(IList<T> singers) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);

            int count = singers.Count;
            int activeCount = 0;
            for (int i = 0; i < count; i++)
            {
                T s = singers[i];
                if (!s.IsInactive && !s.IsPaused)
                    activeCount++;
            }
            return activeCount;
        }

        /// <summary>
        /// Clears both <see cref="IRotationSinger.IsCurrent"/> and <see cref="IRotationSinger.IsNext"/> flags on all singers.
        /// </summary>
        public static void ClearHighlights<T>(IList<T> singers) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);

            int count = singers.Count;
            for (int i = 0; i < count; i++)
            {
                T s = singers[i];
                s.IsCurrent = false;
                s.IsNext = false;
            }
        }

        /// <summary>
        /// Sets <see cref="IRotationSinger.IsNext"/> on the first active singer after <paramref name="currentEntry"/>,
        /// wrapping around the list. Clears any previous IsNext flag first.
        /// </summary>
        public static void MarkNextSinger<T>(IList<T> singers, T currentEntry) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(currentEntry);

            int count = singers.Count;
            for (int i = 0; i < count; i++)
            {
                singers[i].IsNext = false;
            }

            int startIndex = singers.IndexOf(currentEntry);
            if (startIndex == -1) return;

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

            T? current = GetCurrentSinger(singers);
            if (current != null)
            {
                MarkNextSinger(singers, current);
            }
            else
            {
                int count = singers.Count;
                for (int i = 0; i < count; i++)
                {
                    singers[i].IsNext = false;
                }
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

            T? previousCurrent = GetCurrentSinger(singers);

            if (entry.IsInactive)
            {
                entry.IsInactive = false;
            }
            if (entry.IsPaused)
            {
                entry.IsPaused = false;
            }

            ClearHighlights(singers);

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
        /// Advances the rotation after <paramref name="finishedEntry"/> has completed their performance.
        /// Clears IsCurrent on all singers, finds the first active non-paused singer sequentially AFTER
        /// <paramref name="finishedEntry"/> (wrapping around the list to the top), promotes that singer to
        /// current, and updates <see cref="IRotationSinger.IsNext"/> highlights accordingly.
        /// </summary>
        public static T? AdvanceRotationAfterFinished<T>(IList<T> singers, T finishedEntry) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(finishedEntry);

            int currentIndex = singers.IndexOf(finishedEntry);
            int count = singers.Count;
            T? nextCurrent = null;

            if (currentIndex >= 0 && count > 0)
            {
                for (int i = 1; i < count; i++)
                {
                    T candidate = singers[(currentIndex + i) % count];
                    if (candidate != finishedEntry && !candidate.IsInactive && !candidate.IsPaused)
                    {
                        nextCurrent = candidate;
                        break;
                    }
                }
            }

            ClearHighlights(singers);

            if (nextCurrent != null)
            {
                nextCurrent.IsCurrent = true;
                MarkNextSinger(singers, nextCurrent);
            }

            return nextCurrent;
        }

        /// <summary>
        /// Gets up to <paramref name="maxCount"/> active, non-paused singers sequentially following <paramref name="currentEntry"/>.
        /// </summary>
        public static List<T> GetNextActiveSingers<T>(IList<T> singers, T currentEntry, int maxCount) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(currentEntry);

            if (maxCount <= 0) return new List<T>(0);

            int count = singers.Count;
            var list = new List<T>(Math.Min(count, maxCount));
            int startIndex = singers.IndexOf(currentEntry);
            if (startIndex == -1) return list;

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
