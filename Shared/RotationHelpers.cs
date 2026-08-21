// Edited on Aug 21, 2026 @ 07:49:00 -> Add InsertNewSinger to place new singers at end of current rotation round
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Lyracist.Shared
{
    /// <summary>
    /// Pure rotation-list helpers that operate on a collection implementing <see cref="IRotationSinger"/>.
    /// Shared between Lyracist and KSRotation so their behavior cannot drift apart.
    /// </summary>
    public static class RotationHelpers
    {
        /// <summary>
        /// Moves an item from <paramref name="oldIndex"/> to <paramref name="newIndex"/> in <paramref name="list"/>.
        /// Uses RemoveAt + Insert to ensure visual collection containers update deterministically across all platforms.
        /// </summary>
        public static void MoveSingerInList<T>(IList<T> list, int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex || oldIndex < 0 || newIndex < 0 || oldIndex >= list.Count || newIndex >= list.Count)
                return;

            T item = list[oldIndex];
            list.RemoveAt(oldIndex);
            list.Insert(newIndex, item);
        }

        /// <summary>
        /// Inserts a new active singer into <paramref name="singers"/> at the end of the current active rotation round.
        /// When previous singers have already performed in the current cycle and moved to the bottom
        /// (anchored by the active singer holding <see cref="IRotationSinger.IsRotationStart"/> at index > 0),
        /// the new singer is inserted right before that anchor (at that anchor index) so they perform in the current
        /// cycle before the round rolls over into the next cycle.
        /// Otherwise (if the anchor is at index 0 or not found), the new singer is inserted before any inactive singers
        /// (or appended to the list).
        /// </summary>
        public static void InsertNewSinger<T>(IList<T> singers, T newSinger, bool floatCurrentSingerToTop = false) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(newSinger);

            int count = singers.Count;
            if (count == 0)
            {
                singers.Add(newSinger);
                newSinger.IsRotationStart = true;
                return;
            }

            int startAnchorIndex = -1;
            int firstInactiveIndex = -1;

            for (int i = 0; i < count; i++)
            {
                T s = singers[i];
                if (s.IsInactive)
                {
                    if (firstInactiveIndex == -1)
                    {
                        firstInactiveIndex = i;
                    }
                }
                else if (s.IsRotationStart && !s.IsPaused)
                {
                    if (startAnchorIndex == -1)
                    {
                        startAnchorIndex = i;
                    }
                }
            }

            if (startAnchorIndex > 0)
            {
                // Previous singers in this round have already sung and are at the bottom starting at startAnchorIndex.
                // Insert the new singer at startAnchorIndex so they sing at the end of the current round.
                singers.Insert(startAnchorIndex, newSinger);
            }
            else if (firstInactiveIndex != -1)
            {
                // Insert before inactive singers
                singers.Insert(firstInactiveIndex, newSinger);
            }
            else
            {
                // Append to end of list
                singers.Add(newSinger);
            }

            EnsureRotationStartFlag(singers);
            UpdateNextSingerHighlight(singers);
        }

        /// <summary>
        /// Ensures exactly one active singer in <paramref name="singers"/> has <see cref="IRotationSinger.IsRotationStart"/> set.
        /// If no singer has the flag, sets it on the first active singer (or first singer in list).
        /// If an inactive or paused singer has the flag, clears it and reassigns to the first active singer.
        /// If multiple singers have the flag, clears duplicates and preserves only the first.
        /// </summary>
        public static void EnsureRotationStartFlag<T>(IList<T> singers) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);

            int count = singers.Count;
            if (count == 0) return;

            int firstActiveStartIdx = -1;
            for (int i = 0; i < count; i++)
            {
                if (singers[i].IsRotationStart)
                {
                    if (singers[i].IsInactive || singers[i].IsPaused)
                    {
                        singers[i].IsRotationStart = false;
                    }
                    else if (firstActiveStartIdx == -1)
                    {
                        firstActiveStartIdx = i;
                    }
                    else
                    {
                        singers[i].IsRotationStart = false;
                    }
                }
            }

            if (firstActiveStartIdx == -1)
            {
                // Assign to the first active non-paused singer, or first singer in the list
                for (int i = 0; i < count; i++)
                {
                    if (!singers[i].IsInactive && !singers[i].IsPaused)
                    {
                        singers[i].IsRotationStart = true;
                        return;
                    }
                }
                singers[0].IsRotationStart = true;
            }
        }

        /// <summary>
        /// Handles moving the 1st singer flag (IsRotationStart) to the next active singer when <paramref name="entry"/>
        /// is retired (made inactive) or deleted from the rotation list.
        /// </summary>
        public static void HandleSingerRetiredOrRemoved<T>(IList<T> singers, T entry) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(entry);

            if (!entry.IsRotationStart) return;

            entry.IsRotationStart = false;

            int count = singers.Count;
            int startIndex = singers.IndexOf(entry);
            if (startIndex >= 0 && count > 1)
            {
                for (int i = 1; i < count; i++)
                {
                    T candidate = singers[(startIndex + i) % count];
                    if (candidate != entry && !candidate.IsInactive && !candidate.IsPaused)
                    {
                        candidate.IsRotationStart = true;
                        return;
                    }
                }
            }

            EnsureRotationStartFlag(singers);
        }

        /// <summary>
        /// Explicitly designates <paramref name="entry"/> as the 1st singer (round start) in the rotation,
        /// clearing the flag on all other singers.
        /// </summary>
        public static void SetRotationStartSinger<T>(IList<T> singers, T entry) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(entry);

            int count = singers.Count;
            for (int i = 0; i < count; i++)
            {
                singers[i].IsRotationStart = (singers[i] == entry);
            }
        }

        /// <summary>
        /// Toggles <paramref name="entry"/>'s 1st singer (round start) designation. If <paramref name="entry"/>
        /// already holds the flag, clears it and reassigns it to the next active singer sequentially after
        /// <paramref name="entry"/> (wrapping around, same search <see cref="HandleSingerRetiredOrRemoved{T}"/>
        /// uses) — so a DJ who flagged the wrong singer by accident can undo it without having to pick a specific
        /// replacement. Reassigning to anyone OTHER than <paramref name="entry"/> matters here: falling back to
        /// <see cref="EnsureRotationStartFlag{T}"/> alone would reassign to the first active singer in list
        /// order, which is often <paramref name="entry"/> itself (e.g. the current singer floated to the top —
        /// the row most likely to be clicked by accident), making the "clear" a no-op. Otherwise, designates
        /// <paramref name="entry"/> explicitly via <see cref="SetRotationStartSinger{T}"/>, clearing the flag on
        /// everyone else.
        /// </summary>
        public static void ToggleRotationStartSinger<T>(IList<T> singers, T entry) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(entry);

            if (entry.IsRotationStart)
            {
                entry.IsRotationStart = false;

                int count = singers.Count;
                int startIndex = singers.IndexOf(entry);
                if (startIndex >= 0 && count > 1)
                {
                    for (int i = 1; i < count; i++)
                    {
                        T candidate = singers[(startIndex + i) % count];
                        if (candidate != entry && !candidate.IsInactive && !candidate.IsPaused)
                        {
                            candidate.IsRotationStart = true;
                            return;
                        }
                    }
                }

                // No other active singer exists to hand the flag to — fall back to the invariant-preserving
                // default, which will land back on entry itself if it's genuinely the only eligible singer.
                EnsureRotationStartFlag(singers);
            }
            else
            {
                SetRotationStartSinger(singers, entry);
            }
        }

        /// <summary>
        /// Moves the current active singer to index 0 of <paramref name="singers"/> if not already at the top.
        /// </summary>
        public static void FloatCurrentSingerToTop<T>(IList<T> singers) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);

            T? current = GetCurrentSinger(singers);
            if (current != null)
            {
                int index = singers.IndexOf(current);
                if (index > 0)
                {
                    MoveSingerInList(singers, index, 0);
                    UpdateNextSingerHighlight(singers);
                }
            }
        }

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
        /// If <paramref name="floatCurrentToTop"/> is true, moves <paramref name="entry"/> to index 0.
        /// </summary>
        public static void SetCurrentSinger<T>(IList<T> singers, T entry, bool floatCurrentToTop = false) where T : class, IRotationSinger
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

            if (floatCurrentToTop)
            {
                int index = singers.IndexOf(entry);
                if (index > 0)
                {
                    MoveSingerInList(singers, index, 0);
                }
                UpdateNextSingerHighlight(singers);
            }
            else
            {
                if (previousCurrent != null && previousCurrent != entry && !previousCurrent.IsInactive && !previousCurrent.IsPaused)
                {
                    previousCurrent.IsNext = true;
                }
                else
                {
                    UpdateNextSingerHighlight(singers);
                }
            }
        }

        /// <summary>
        /// Advances the rotation after <paramref name="finishedEntry"/> has completed their performance.
        /// Clears IsCurrent on all singers, finds the first active non-paused singer sequentially AFTER
        /// <paramref name="finishedEntry"/> (wrapping around the list to the top), promotes that singer to
        /// current, and updates <see cref="IRotationSinger.IsNext"/> highlights accordingly.
        /// If <paramref name="floatCurrentToTop"/> is true, moves <paramref name="finishedEntry"/> to the end of the active queue
        /// and ensures the next promoted singer is positioned at index 0.
        /// </summary>
        public static T? AdvanceRotationAfterFinished<T>(IList<T> singers, T finishedEntry, bool floatCurrentToTop = false) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(finishedEntry);

            if (!floatCurrentToTop)
            {
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
            else
            {
                int oldIdx = singers.IndexOf(finishedEntry);
                int count = singers.Count;

                // Move finishedEntry to the bottom of the active queue (before any inactive singers)
                if (oldIdx >= 0 && count > 1)
                {
                    int targetIdx = count - 1;
                    for (int i = 0; i < count; i++)
                    {
                        if (singers[i].IsInactive)
                        {
                            targetIdx = (oldIdx < i) ? (i - 1) : i;
                            break;
                        }
                    }
                    if (oldIdx != targetIdx && targetIdx >= 0 && targetIdx < count)
                    {
                        MoveSingerInList(singers, oldIdx, targetIdx);
                    }
                }

                ClearHighlights(singers);

                // Find the first active non-paused singer in the reordered list
                T? nextCurrent = null;
                for (int i = 0; i < singers.Count; i++)
                {
                    T candidate = singers[i];
                    if (!candidate.IsInactive && !candidate.IsPaused)
                    {
                        nextCurrent = candidate;
                        break;
                    }
                }

                if (nextCurrent != null)
                {
                    int nextIdx = singers.IndexOf(nextCurrent);
                    if (nextIdx > 0)
                    {
                        MoveSingerInList(singers, nextIdx, 0);
                    }
                    nextCurrent.IsCurrent = true;
                    MarkNextSinger(singers, nextCurrent);
                }

                return nextCurrent;
            }
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

