// Last Edit: Jun 29, 2026 13:26 - Extracted rotation-list helper logic from MainViewModel.
using KSRotation.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KSRotation.Services
{
    /// <summary>
    /// Pure rotation-list helpers that operate on a <see cref="SingerEntry"/> collection.
    /// Extracted from <c>MainViewModel</c> so the logic can be unit-tested without a ViewModel instance.
    /// </summary>
    public static class RotationHelpers
    {
        /// <summary>
        /// Sets <see cref="SingerEntry.IsNext"/> on the first active singer after <paramref name="currentEntry"/>,
        /// wrapping around the list. Clears any previous IsNext flag first.
        /// </summary>
        public static void MarkNextSinger(ObservableCollection<SingerEntry> singers, SingerEntry currentEntry)
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(currentEntry);

            foreach (SingerEntry s in singers)
                s.IsNext = false;

            int startIndex = singers.IndexOf(currentEntry);
            int count = singers.Count;

            for (int i = 1; i < count; i++)
            {
                SingerEntry candidate = singers[(startIndex + i) % count];
                if (!candidate.IsInactive)
                {
                    candidate.IsNext = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Recalculates and applies the IsNext highlight for the next active singer after
        /// whichever entry currently has <see cref="SingerEntry.IsCurrent"/> set.
        /// Clears all IsNext flags when there is no current singer.
        /// </summary>
        public static void UpdateNextSingerHighlight(ObservableCollection<SingerEntry> singers)
        {
            ArgumentNullException.ThrowIfNull(singers);

            SingerEntry? current = singers.FirstOrDefault(s => s.IsCurrent && !s.IsInactive);
            if (current != null)
            {
                MarkNextSinger(singers, current);
            }
            else
            {
                foreach (SingerEntry s in singers)
                    s.IsNext = false;
            }
        }

        /// <summary>
        /// Promotes <paramref name="entry"/> to the current singer: reactivates it if paused, clears
        /// IsCurrent/IsNext on every other singer, and — if there was a previous current singer — marks
        /// that singer as IsNext so they resume right after. Falls back to standard rotation order when
        /// there was no previous current singer (or it was inactive).
        /// </summary>
        /// <remarks>
        /// This is the single implementation for "set current singer" shared by every UI entry point
        /// (desktop button, DJ web portal, MAUI console) so their behavior can't drift apart.
        /// </remarks>
        public static void SetCurrentSinger(ObservableCollection<SingerEntry> singers, SingerEntry entry)
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(entry);

            SingerEntry? previousCurrent = singers.FirstOrDefault(s => s.IsCurrent);

            if (entry.IsInactive)
            {
                entry.IsInactive = false;
            }

            foreach (SingerEntry s in singers)
            {
                s.IsCurrent = false;
                s.IsNext = false;
            }

            entry.IsCurrent = true;

            if (previousCurrent != null && previousCurrent != entry && !previousCurrent.IsInactive)
            {
                previousCurrent.IsNext = true;
            }
            else
            {
                UpdateNextSingerHighlight(singers);
            }
        }
    }
}
