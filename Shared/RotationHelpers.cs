// Edited on Sep 18, 2026 @ 08:45:00 -> Add Special Singer support to AdvanceRotationAfterFinished, EnsureRotationStartFlag, and HandleSingerRetiredOrRemoved
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Lyracist.Shared
{
    /// <summary>
    /// Pure rotation-list helpers that operate on a collection implementing <see cref="IRotationSinger"/>.
    /// Shared between Lyracist and KSRotation so their behavior cannot drift apart.
    /// </summary>
    public static class RotationHelpers
    {
        /// <summary>
        /// Cleans a name/title/artist string for comparison: replaces non-breaking spaces and tabs (which
        /// patron devices and copy-pasted song titles commonly introduce) with regular spaces, collapses
        /// runs of whitespace to a single space, and trims. Does not change case - callers compare with
        /// <see cref="StringComparison.OrdinalIgnoreCase"/> so casing differences don't matter separately.
        /// </summary>
        public static string NormalizeForComparison(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string clean = value.Replace((char)0x00A0, ' ').Replace('\t', ' ').Trim();
            return Regex.Replace(clean, @"\s+", " ");
        }

        /// <summary>True if two singer names are the same after <see cref="NormalizeForComparison"/>.</summary>
        public static bool IsSameSingerName(string? name1, string? name2)
        {
            if (name1 == null || name2 == null) return false;
            return string.Equals(NormalizeForComparison(name1), NormalizeForComparison(name2), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Strict song match: both title and artist must match (after normalization). Used where a
        /// specific already-known song is being compared against another specific song, e.g. checking
        /// whether one singer already has this exact song queued.
        /// </summary>
        public static bool IsSameSong(string? title1, string? artist1, string? title2, string? artist2)
        {
            return string.Equals(NormalizeForComparison(title1), NormalizeForComparison(title2), StringComparison.OrdinalIgnoreCase)
                && string.Equals(NormalizeForComparison(artist1), NormalizeForComparison(artist2), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Lenient song match for session-wide duplicate detection: the title must match, and the artist
        /// only has to match when BOTH sides have a known (non-blank) artist - an unknown artist on either
        /// side falls back to a title-only match, so a request/history entry with no artist attached still
        /// gets caught instead of silently bypassing the check.
        /// </summary>
        public static bool IsSameSongLenient(string? candidateTitle, string? candidateArtist, string queryTitle, string queryArtist)
        {
            if (!string.Equals(NormalizeForComparison(candidateTitle), NormalizeForComparison(queryTitle), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string cleanCandidateArtist = NormalizeForComparison(candidateArtist);
            string cleanQueryArtist = NormalizeForComparison(queryArtist);
            if (cleanCandidateArtist.Length == 0 || cleanQueryArtist.Length == 0)
            {
                return true;
            }

            return string.Equals(cleanCandidateArtist, cleanQueryArtist, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Runs a read against a collection that may be concurrently mutated on another thread (e.g. a
        /// background HTTP request-handling thread reading a rotation ObservableCollection the UI
        /// thread owns and mutates via Add/Remove/Move). Enumeration throws InvalidOperationException
        /// if the collection changes mid-enumeration; this retries once (the mutation that caused it
        /// will very likely have completed by then) and falls back to <paramref name="fallback"/> if
        /// it's still racing, rather than propagating the exception.
        /// Deliberately does not use Dispatcher.Invoke to marshal onto the owning thread instead - that
        /// requires an actively-pumped message loop, which isn't guaranteed to exist (e.g. a unit test's
        /// bare, un-started WPF Application) and would hang forever waiting for one that never runs.
        /// </summary>
        public static T ReadWithConcurrentRetry<T>(Func<T> read, T fallback)
        {
            try
            {
                return read();
            }
            catch (InvalidOperationException)
            {
                try
                {
                    return read();
                }
                catch (InvalidOperationException)
                {
                    return fallback;
                }
            }
        }

        /// <summary>
        /// Moves an item from <paramref name="oldIndex"/> to <paramref name="newIndex"/> in <paramref name="list"/>.
        /// Uses RemoveAt + Insert to ensure visual collection containers update deterministically across all platforms.
        /// </summary>
        public static void MoveSingerInList<T>(IList<T> list, int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex || oldIndex < 0 || newIndex < 0 || oldIndex >= list.Count || newIndex >= list.Count)
                return;

            if (list is ObservableCollection<T> oc)
            {
                oc.Move(oldIndex, newIndex);
                return;
            }

            T item = list[oldIndex];
            list.RemoveAt(oldIndex);
            list.Insert(newIndex, item);
        }

        /// <summary>
        /// Helper to check if any singer in <paramref name="singers"/> is active, non-paused, and not a special singer.
        /// </summary>
        private static bool HasActiveNonSpecialSinger<T>(IList<T> singers) where T : class, IRotationSinger
        {
            for (int i = 0; i < singers.Count; i++)
            {
                if (!singers[i].IsInactive && !singers[i].IsPaused && !singers[i].IsSpecial)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Places a special (one-time performance) singer at the top of the list (index 0) and sets them as the current singer.
        /// If another singer was currently performing, that singer is marked as Next so rotation resumes with them once the special
        /// performance concludes.
        /// </summary>
        public static void PromoteSpecialSingerToCurrent<T>(IList<T> singers, T specialSinger) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(specialSinger);

            specialSinger.IsSpecial = true;
            specialSinger.IsInactive = false;
            specialSinger.IsPaused = false;
            specialSinger.IsSkipped = false;

            T? previousCurrent = GetCurrentSinger(singers);

            int existingIndex = singers.IndexOf(specialSinger);
            if (existingIndex < 0)
            {
                singers.Insert(0, specialSinger);
            }
            else if (existingIndex > 0)
            {
                MoveSingerInList(singers, existingIndex, 0);
            }

            ClearHighlights(singers);
            specialSinger.IsCurrent = true;

            if (previousCurrent != null && previousCurrent != specialSinger && !previousCurrent.IsInactive && !previousCurrent.IsPaused && !previousCurrent.IsSkipped)
            {
                previousCurrent.IsNext = true;
            }
            else
            {
                UpdateNextSingerHighlight(singers);
            }

            EnsureRotationStartFlag(singers);
        }

        /// <summary>
        /// Inserts a new active singer into <paramref name="singers"/> at the end of the current active rotation round.
        /// If the new singer is a special singer (<see cref="IRotationSinger.IsSpecial"/>), they are placed at the top of
        /// the list (index 0) and set as the current performer immediately.
        /// When previous singers have already performed in the current cycle and moved to the bottom
        /// (anchored by the active singer holding <see cref="IRotationSinger.IsRotationStart"/> at index > 0),
        /// the new singer is inserted right before that anchor (at that anchor index) so they perform in the current
        /// cycle before the round rolls over into the next cycle.
        /// Otherwise (if the anchor is at index 0 or not found), the new singer is inserted before any inactive singers
        /// (or appended to the list).
        /// The anchor-index math above is correct regardless of "float current singer to top" mode - the anchor
        /// is found by its IsRotationStart flag, not by list position, so it needs no float-mode parameter of its own.
        /// </summary>
        public static void InsertNewSinger<T>(IList<T> singers, T newSinger) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(newSinger);

            if (newSinger.IsSpecial)
            {
                PromoteSpecialSingerToCurrent(singers, newSinger);
                return;
            }

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
                    if (singers[i].IsInactive || singers[i].IsPaused || (singers[i].IsSpecial && count > 1 && HasActiveNonSpecialSinger(singers)))
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
                // Prefer assigning to the first active non-paused non-special singer
                for (int i = 0; i < count; i++)
                {
                    if (!singers[i].IsInactive && !singers[i].IsPaused && !singers[i].IsSpecial)
                    {
                        singers[i].IsRotationStart = true;
                        return;
                    }
                }
                // Fall back to first active non-paused singer if all are special
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
                    if (candidate != entry && !candidate.IsInactive && !candidate.IsPaused && !candidate.IsSpecial)
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
                if (s.IsCurrent && !s.IsInactive && !s.IsPaused && !s.IsSkipped)
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
                if (!s.IsInactive && !s.IsPaused && !s.IsSkipped)
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
        public static void MarkNextSinger<T>(IList<T> singers, T currentEntry, bool isLastRound = false) where T : class, IRotationSinger
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
                if (!candidate.IsInactive && !candidate.IsPaused && !candidate.IsSkipped && (!isLastRound || !candidate.HasSungInLastRound))
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
        public static void UpdateNextSingerHighlight<T>(IList<T> singers, bool isLastRound = false) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);

            T? current = GetCurrentSinger(singers);
            if (current != null)
            {
                MarkNextSinger(singers, current, isLastRound);
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
        /// If <paramref name="isLastRound"/> is true, a previous current singer who already performed
        /// during the last round is never resumed as IsNext — the standard rotation-order fallback is
        /// used instead, which itself skips other performers who have already sung.
        /// </summary>
        public static void SetCurrentSinger<T>(IList<T> singers, T entry, bool floatCurrentToTop = false, bool isLastRound = false) where T : class, IRotationSinger
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
            if (entry.IsSkipped)
            {
                entry.IsSkipped = false;
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
                UpdateNextSingerHighlight(singers, isLastRound);
            }
            else
            {
                if (previousCurrent != null && previousCurrent != entry && !previousCurrent.IsInactive && !previousCurrent.IsPaused && !previousCurrent.IsSkipped
                    && (!isLastRound || !previousCurrent.HasSungInLastRound))
                {
                    previousCurrent.IsNext = true;
                }
                else
                {
                    UpdateNextSingerHighlight(singers, isLastRound);
                }
            }
        }

        /// <summary>
        /// Advances the rotation after <paramref name="finishedEntry"/> has completed their performance.
        /// Clears IsCurrent on all singers, finds the first active non-paused non-skipped singer sequentially AFTER
        /// <paramref name="finishedEntry"/> (wrapping around the list to the top), promotes that singer to
        /// current, and updates <see cref="IRotationSinger.IsNext"/> highlights accordingly.
        /// If <paramref name="floatCurrentToTop"/> is true, moves <paramref name="finishedEntry"/> to the end of the active queue
        /// and ensures the next promoted singer is positioned at index 0.
        /// When advancing reaches or crosses the round anchor (<see cref="IRotationSinger.IsRotationStart"/>),
        /// all active singers who were passed over in that round have <see cref="IRotationSinger.IsSkipped"/> reset to false.
        /// </summary>
        public static T? AdvanceRotationAfterFinished<T>(IList<T> singers, T finishedEntry, bool floatCurrentToTop = false, bool isLastRound = false) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(finishedEntry);

            // If someone was designated as Next (e.g. displaced when a special singer or manual override sang),
            // preserve them so the rotation resumes with them seamlessly.
            T? designatedNext = null;
            for (int i = 0; i < singers.Count; i++)
            {
                T s = singers[i];
                if (s != finishedEntry && s.IsNext && !s.IsInactive && !s.IsPaused && !s.IsSkipped && (!isLastRound || !s.HasSungInLastRound))
                {
                    designatedNext = s;
                    break;
                }
            }

            if (finishedEntry.IsSpecial)
            {
                if (finishedEntry.IsRotationStart)
                {
                    HandleSingerRetiredOrRemoved(singers, finishedEntry);
                }
                finishedEntry.IsInactive = true;
                finishedEntry.IsCurrent = false;
            }

            if (!floatCurrentToTop)
            {
                int currentIndex = singers.IndexOf(finishedEntry);
                int count = singers.Count;
                T? nextCurrent = designatedNext;
                bool passedRoundAnchor = false;

                int anchorIndex = -1;
                for (int i = 0; i < count; i++)
                {
                    if (singers[i].IsRotationStart)
                    {
                        anchorIndex = i;
                        break;
                    }
                }
                if (anchorIndex == -1 && count > 0) anchorIndex = 0;

                if (nextCurrent == null && currentIndex >= 0 && count > 0)
                {
                    for (int i = 1; i < count; i++)
                    {
                        int candidateIdx = (currentIndex + i) % count;
                        if (candidateIdx == anchorIndex)
                        {
                            passedRoundAnchor = true;
                        }

                        T candidate = singers[candidateIdx];
                        if (candidate != finishedEntry && !candidate.IsInactive && !candidate.IsPaused && !candidate.IsSkipped && (!isLastRound || !candidate.HasSungInLastRound))
                        {
                            nextCurrent = candidate;
                            break;
                        }
                    }
                }
                else if (nextCurrent != null && currentIndex >= 0 && count > 0)
                {
                    int nextIdx = singers.IndexOf(nextCurrent);
                    if (anchorIndex >= 0 && ((currentIndex < anchorIndex && anchorIndex <= nextIdx) || (currentIndex > nextIdx && (anchorIndex > currentIndex || anchorIndex <= nextIdx))))
                    {
                        passedRoundAnchor = true;
                    }
                }

                ClearHighlights(singers);

                if (nextCurrent != null)
                {
                    if (passedRoundAnchor || nextCurrent.IsRotationStart)
                    {
                        for (int i = 0; i < singers.Count; i++)
                        {
                            if (singers[i] != nextCurrent && singers[i].IsSkipped)
                            {
                                singers[i].IsSkipped = false;
                            }
                        }
                    }

                    nextCurrent.IsCurrent = true;
                    MarkNextSinger(singers, nextCurrent, isLastRound);
                }

                return nextCurrent;
            }
            else
            {
                int oldIdx = singers.IndexOf(finishedEntry);
                int count = singers.Count;

                // Move finishedEntry to the bottom of the active queue (before any inactive singers,
                // or to the inactive section at the bottom if finishedEntry is now inactive)
                if (oldIdx >= 0 && count > 1)
                {
                    int targetIdx = count - 1;
                    if (!finishedEntry.IsInactive)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if (singers[i].IsInactive && singers[i] != finishedEntry)
                            {
                                targetIdx = (oldIdx < i) ? (i - 1) : i;
                                break;
                            }
                        }
                    }
                    if (oldIdx != targetIdx && targetIdx >= 0 && targetIdx < count)
                    {
                        MoveSingerInList(singers, oldIdx, targetIdx);
                    }
                }

                // If any skipped singers are at the top of the queue, they are passed over for this round;
                // move them to the bottom of the active queue to preserve relative rotation order.
                int maxMoves = singers.Count;
                while (maxMoves-- > 0 && singers.Count > 1)
                {
                    T top = singers[0];
                    if (!top.IsInactive && !top.IsPaused && top.IsSkipped)
                    {
                        int targetIdx = singers.Count - 1;
                        for (int i = 0; i < singers.Count; i++)
                        {
                            if (singers[i].IsInactive && singers[i] != top)
                            {
                                targetIdx = i - 1;
                                break;
                            }
                        }
                        if (targetIdx > 0)
                        {
                            MoveSingerInList(singers, 0, targetIdx);
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                ClearHighlights(singers);

                // If a designated next singer was identified, promote them; otherwise find the first active non-paused non-skipped singer
                T? nextCurrent = designatedNext;
                if (nextCurrent == null)
                {
                    for (int i = 0; i < singers.Count; i++)
                    {
                        T candidate = singers[i];
                        if (!candidate.IsInactive && !candidate.IsPaused && !candidate.IsSkipped && (!isLastRound || !candidate.HasSungInLastRound))
                        {
                            nextCurrent = candidate;
                            break;
                        }
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

                    if (nextCurrent.IsRotationStart)
                    {
                        for (int i = 0; i < singers.Count; i++)
                        {
                            if (singers[i] != nextCurrent && singers[i].IsSkipped)
                            {
                                singers[i].IsSkipped = false;
                            }
                        }
                    }

                    MarkNextSinger(singers, nextCurrent, isLastRound);
                }

                return nextCurrent;
            }
        }

        /// <summary>
        /// Fallback estimated performance length (seconds) used for a queued singer whose song
        /// duration is unknown/unresolved, when a caller doesn't pass its own DJ-configured value to
        /// <see cref="RecalculateEstimatedWaits{T}"/>. 4.75 minutes - closer to a typical song's
        /// actual runtime than a flat 5.
        /// </summary>
        public const double DefaultEstimatedPerformanceSeconds = 285.0; // 4.75 minutes

        /// <summary>
        /// Recalculates every singer's <see cref="IRotationSinger.EstimatedWaitMinutes"/>: the cumulative
        /// estimated performance length (rounded to whole minutes) of everyone ahead of them in the
        /// active rotation, starting with however long the current singer's own performance is
        /// estimated to take (since they're already up and haven't finished yet). Walks the queue in
        /// the same wrapped order as <see cref="MarkNextSinger{T}"/>/<see cref="GetNextActiveSingers{T}"/>.
        /// An entry with no resolvable <see cref="IRotationSinger.EstimatedPerformanceSeconds"/> (&lt;= 0)
        /// falls back to <paramref name="defaultEstimatedPerformanceSeconds"/> (a DJ-configurable
        /// per-app setting; defaults to <see cref="DefaultEstimatedPerformanceSeconds"/> if the caller
        /// doesn't have one) so an unknown song never produces a wait estimate of 0 for anyone behind
        /// it. The current singer's own wait is set to 0 (already up); inactive/paused singers - and,
        /// when <paramref name="isLastRound"/> is true, anyone who already sang this round - are
        /// skipped and their wait cleared to 0, matching how they're already excluded from "next"
        /// traversal elsewhere in this file. If there is no current singer, or <paramref name="enabled"/>
        /// is false (the DJ has turned the feature off), every entry's wait is cleared to 0 - every
        /// display already hides the badge whenever the wait is 0, so this alone is enough to hide it.
        /// </summary>
        public static void RecalculateEstimatedWaits<T>(IList<T> singers, bool isLastRound = false, double defaultEstimatedPerformanceSeconds = DefaultEstimatedPerformanceSeconds, bool enabled = true) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);

            int count = singers.Count;
            T? current = enabled ? GetCurrentSinger(singers) : null;

            if (current == null)
            {
                for (int i = 0; i < count; i++)
                {
                    singers[i].EstimatedWaitMinutes = 0;
                }
                return;
            }

            current.EstimatedWaitMinutes = 0;
            double cumulativeSeconds = ResolveEstimatedSeconds(current, defaultEstimatedPerformanceSeconds);

            int startIndex = singers.IndexOf(current);
            for (int i = 1; i < count; i++)
            {
                T candidate = singers[(startIndex + i) % count];
                if (candidate == current) continue;

                if (candidate.IsInactive || candidate.IsPaused || candidate.IsSkipped || (isLastRound && candidate.HasSungInLastRound))
                {
                    candidate.EstimatedWaitMinutes = 0;
                    continue;
                }

                candidate.EstimatedWaitMinutes = (int)Math.Round(cumulativeSeconds / 60.0, MidpointRounding.AwayFromZero);
                cumulativeSeconds += ResolveEstimatedSeconds(candidate, defaultEstimatedPerformanceSeconds);
            }
        }

        private static double ResolveEstimatedSeconds<T>(T singer, double defaultEstimatedPerformanceSeconds) where T : class, IRotationSinger
        {
            return singer.EstimatedPerformanceSeconds > 0 ? singer.EstimatedPerformanceSeconds : defaultEstimatedPerformanceSeconds;
        }

        /// <summary>
        /// Gets up to <paramref name="maxCount"/> active, non-paused, non-skipped singers sequentially following <paramref name="currentEntry"/>.
        /// </summary>
        public static List<T> GetNextActiveSingers<T>(IList<T> singers, T currentEntry, int maxCount, bool isLastRound = false) where T : class, IRotationSinger
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
                if (candidate != currentEntry && !candidate.IsInactive && !candidate.IsPaused && !candidate.IsSkipped && (!isLastRound || !candidate.HasSungInLastRound))
                {
                    list.Add(candidate);
                }
            }
            return list;
        }

        /// <summary>
        /// Resets the <see cref="IRotationSinger.IsSkipped"/> flag on all singers in <paramref name="singers"/>.
        /// </summary>
        public static void ResetSkippedSingers<T>(IList<T> singers) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            for (int i = 0; i < singers.Count; i++)
            {
                singers[i].IsSkipped = false;
            }
        }

        /// <summary>
        /// Links <paramref name="a"/> and <paramref name="b"/> so they always stay adjacent in the
        /// rotation (see <see cref="EnforceLinkedAdjacency{T}"/>) and no other singer can land
        /// between them. Breaks any existing link either one already has first, so an entry is
        /// never linked to more than one partner at a time. Immediately snaps the pair adjacent.
        /// </summary>
        public static void LinkSingers<T>(IList<T> singers, T a, T b) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            if (a == b) return;

            UnlinkSinger(singers, a);
            UnlinkSinger(singers, b);

            a.LinkedSingerId = b.Id;
            b.LinkedSingerId = a.Id;

            EnforceLinkedAdjacency(singers);
        }

        /// <summary>
        /// Clears <paramref name="entry"/>'s link, if any, and the reciprocal link on its partner
        /// (found by <see cref="IRotationSinger.LinkedSingerId"/>) so no dangling one-sided link remains.
        /// </summary>
        public static void UnlinkSinger<T>(IList<T> singers, T entry) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(entry);

            if (!entry.LinkedSingerId.HasValue) return;

            Guid partnerId = entry.LinkedSingerId.Value;
            entry.LinkedSingerId = null;

            foreach (T s in singers)
            {
                if (s.Id == partnerId)
                {
                    s.LinkedSingerId = null;
                    break;
                }
            }
        }

        /// <summary>
        /// Restores adjacency for every linked pair in <paramref name="singers"/>: whenever a linked
        /// entry's partner isn't sitting immediately next to it, moves the partner to close the gap
        /// (preserving which of the two was ahead of the other). Call this after any operation that
        /// can reorder the list - inserting a new singer, manual drag/drop reorder, move up/down, or
        /// advancing the rotation - so a linked pair can never end up with another singer wedged
        /// between them. The link itself is never broken by this - "stay linked" persists all night
        /// until the DJ explicitly unlinks - so a pair legitimately separated by one of them
        /// performing and cycling to the bottom is re-united automatically once it's safe to do so
        /// (see the IsCurrent exemption below), rather than needing to be manually re-linked.
        ///
        /// A partner no longer present in <paramref name="singers"/> (removed/retired for the
        /// night) is left alone; nothing to enforce. Pausing a linked singer does NOT exempt it here
        /// - a paused singer keeps its place and is simply skipped over, same as any other paused
        /// singer, so its partner still needs to stay adjacent to it. Marking one half
        /// <see cref="IRotationSinger.IsInactive"/> ("out for the night") is different: that singer
        /// is effectively leaving the rotation, so its still-active partner is NOT forced to follow
        /// it - adjacency is only enforced while both halves of the pair are still active.
        ///
        /// A pair is also left alone (for now) while either half is <see cref="IRotationSinger.IsCurrent"/>:
        /// that's exactly the moment a linked pair is *expected* to be apart - one just finished and
        /// floated away while the other was promoted to perform next (back-to-back, the whole point
        /// of linking them) - dragging the just-finished singer back up would undo that float and
        /// prevent them from ever separating to take their turns. Once neither is current anymore
        /// (both have had their turn), the next call finds no exemption and pulls them back together
        /// for their next joint turn.
        /// </summary>
        public static void EnforceLinkedAdjacency<T>(IList<T> singers) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);

            var handled = new HashSet<T>();
            for (int i = 0; i < singers.Count; i++)
            {
                T entry = singers[i];
                if (!entry.LinkedSingerId.HasValue || handled.Contains(entry) || entry.IsInactive || entry.IsCurrent) continue;

                T? partner = null;
                int partnerIndex = -1;
                for (int j = 0; j < singers.Count; j++)
                {
                    if (j != i && singers[j].Id == entry.LinkedSingerId.Value)
                    {
                        partner = singers[j];
                        partnerIndex = j;
                        break;
                    }
                }

                if (partner == null || partner.IsInactive || partner.IsCurrent)
                {
                    handled.Add(entry);
                    continue;
                }

                int entryIndex = singers.IndexOf(entry);
                if (Math.Abs(partnerIndex - entryIndex) != 1)
                {
                    // MoveSingerInList's newIndex must already be the post-removal index: when
                    // partnerIndex < entryIndex the removal happens before entry's position, so
                    // entry (and the "immediately before it" slot) shifts down by one; when
                    // partnerIndex > entryIndex, removal happens after entry so its position is
                    // unaffected and the "immediately after it" slot is entryIndex + 1 as-is.
                    if (partnerIndex < entryIndex)
                    {
                        MoveSingerInList(singers, partnerIndex, entryIndex - 1);
                    }
                    else
                    {
                        MoveSingerInList(singers, partnerIndex, entryIndex + 1);
                    }
                }

                handled.Add(entry);
                handled.Add(partner);
            }
        }

        /// <summary>
        /// Finds the active, non-current linked partner of <paramref name="singer"/> in <paramref name="singers"/>,
        /// or null if the singer is not linked, or the partner is inactive, current, or missing.
        /// </summary>
        public static T? GetActiveLinkedPartner<T>(IList<T> singers, T singer) where T : class, IRotationSinger
        {
            if (singers == null || singer == null || !singer.LinkedSingerId.HasValue || singer.IsInactive || singer.IsCurrent)
                return null;

            Guid partnerId = singer.LinkedSingerId.Value;
            for (int i = 0; i < singers.Count; i++)
            {
                T candidate = singers[i];
                if (candidate != singer && candidate.Id == partnerId)
                {
                    if (!candidate.IsInactive && !candidate.IsCurrent)
                    {
                        return candidate;
                    }
                    break;
                }
            }
            return null;
        }

        /// <summary>
        /// Attempts to move <paramref name="entry"/> up in <paramref name="singers"/>.
        /// If the singer immediately above is part of an active linked pair (and not <paramref name="entry"/>'s partner),
        /// jumps past the entire linked pair so the pair is not broken apart.
        /// If <paramref name="entry"/> and the singer immediately above are linked partners, swaps their order within the pair.
        /// If <paramref name="entry"/> is the leading partner of an active linked pair, moves the pair up as a unit.
        /// Enforces active/inactive boundary restrictions.
        /// Returns true if a move occurred; false otherwise.
        /// </summary>
        public static bool MoveSingerUp<T>(IList<T> singers, T entry) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            if (entry == null) return false;

            int index = singers.IndexOf(entry);
            if (index <= 0) return false;

            T above = singers[index - 1];

            // Inactive singer cannot move into the active partition
            if (entry.IsInactive && !above.IsInactive)
            {
                return false;
            }

            T? entryPartner = GetActiveLinkedPartner(singers, entry);
            T? abovePartner = GetActiveLinkedPartner(singers, above);

            // Case 1: entry is linked to above (they are adjacent partners). Swap within pair.
            if (entryPartner != null && entryPartner == above)
            {
                MoveSingerInList(singers, index, index - 1);
                return true;
            }

            // Case 2: above is part of an active linked pair (and not entry's partner).
            // Check if above's partner is at index - 2.
            if (abovePartner != null && index >= 2 && singers[index - 2] == abovePartner)
            {
                if (entry.IsInactive && !abovePartner.IsInactive)
                {
                    return false;
                }

                // If entry is itself part of an active linked pair with entryPartner at index + 1:
                if (entryPartner != null && index + 1 < singers.Count && singers[index + 1] == entryPartner)
                {
                    // Move both partners [entry, entryPartner] past [abovePartner, above]
                    MoveSingerInList(singers, index, index - 2);
                    int partnerOldIdx = singers.IndexOf(entryPartner);
                    MoveSingerInList(singers, partnerOldIdx, index - 1);
                    return true;
                }

                // Normal singer jumps past the linked pair to index - 2
                MoveSingerInList(singers, index, index - 2);
                return true;
            }

            // Case 3: entry is the leading partner of an active linked pair with entryPartner at index + 1.
            // Move the pair up together past above.
            if (entryPartner != null && index + 1 < singers.Count && singers[index + 1] == entryPartner)
            {
                MoveSingerInList(singers, index, index - 1);
                int partnerIdx = singers.IndexOf(entryPartner);
                MoveSingerInList(singers, partnerIdx, index);
                return true;
            }

            // Normal move up 1 position
            MoveSingerInList(singers, index, index - 1);
            return true;
        }

        /// <summary>
        /// Attempts to move <paramref name="entry"/> down in <paramref name="singers"/>.
        /// If the singer immediately below is part of an active linked pair (and not <paramref name="entry"/>'s partner),
        /// jumps past the entire linked pair so the pair is not broken apart.
        /// If <paramref name="entry"/> and the singer immediately below are linked partners, swaps their order within the pair.
        /// If <paramref name="entry"/> is the trailing partner of an active linked pair, moves the pair down as a unit.
        /// Enforces active/inactive boundary restrictions.
        /// Returns true if a move occurred; false otherwise.
        /// </summary>
        public static bool MoveSingerDown<T>(IList<T> singers, T entry) where T : class, IRotationSinger
        {
            ArgumentNullException.ThrowIfNull(singers);
            if (entry == null) return false;

            int index = singers.IndexOf(entry);
            if (index < 0 || index >= singers.Count - 1) return false;

            T below = singers[index + 1];

            // Active singer cannot move into the inactive partition
            if (!entry.IsInactive && below.IsInactive)
            {
                return false;
            }

            T? entryPartner = GetActiveLinkedPartner(singers, entry);
            T? belowPartner = GetActiveLinkedPartner(singers, below);

            // Case 1: entry is linked to below (they are adjacent partners). Swap within pair.
            if (entryPartner != null && entryPartner == below)
            {
                MoveSingerInList(singers, index, index + 1);
                return true;
            }

            // Case 2: below is part of an active linked pair (and not entry's partner).
            // Check if below's partner is at index + 2.
            if (belowPartner != null && index + 2 < singers.Count && singers[index + 2] == belowPartner)
            {
                if (!entry.IsInactive && belowPartner.IsInactive)
                {
                    return false;
                }

                // If entry is itself the trailing partner of an active linked pair [entryPartner, entry]:
                if (entryPartner != null && index >= 1 && singers[index - 1] == entryPartner)
                {
                    // Move [below, belowPartner] before [entryPartner, entry]
                    int b1Idx = singers.IndexOf(below);
                    MoveSingerInList(singers, b1Idx, index - 1);
                    int b2Idx = singers.IndexOf(belowPartner);
                    MoveSingerInList(singers, b2Idx, index);
                    return true;
                }

                // Normal singer jumps past the linked pair (lands at index + 2)
                MoveSingerInList(singers, index, index + 2);
                return true;
            }

            // Case 3: entry is the trailing partner of an active linked pair with entryPartner at index - 1.
            // Move the pair down together past below (by moving below before entryPartner).
            if (entryPartner != null && index >= 1 && singers[index - 1] == entryPartner)
            {
                int belowIdx = singers.IndexOf(below);
                MoveSingerInList(singers, belowIdx, index - 1);
                return true;
            }

            // Normal move down 1 position
            MoveSingerInList(singers, index, index + 1);
            return true;
        }
    }
}

