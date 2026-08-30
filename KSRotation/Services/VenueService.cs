// Edited on Aug 30, 2026 @ 08:26:00 -> Update filename to ksrotation_venues.json with legacy venues.json fallback
using System.Collections.Generic;

namespace KSRotation.Services
{
    /// <summary>Persists the DJ's venue name list. Logic lives in <see cref="StringListStore"/>.</summary>
    public static class VenueService
    {
        private const string FileName = "ksrotation_venues.json";
        private const string LegacyFileName = "venues.json";
        private const string DefaultVenue = "Karaoke Night";

        /// <summary>Loads saved venue names from disk.</summary>
        public static List<string> Load() => StringListStore.Load(FileName, DefaultVenue, LegacyFileName);

        /// <summary>Saves the venue names list to disk.</summary>
        public static void Save(IEnumerable<string> venues) => StringListStore.Save(venues, FileName);
    }
}
