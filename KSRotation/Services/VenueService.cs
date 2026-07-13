// Last Edit: Jun 29, 2026 13:22 - Delegated to StringListStore to eliminate duplication with DjService.
using System.Collections.Generic;

namespace KSRotation.Services
{
    /// <summary>Persists the DJ's venue name list. Logic lives in <see cref="StringListStore"/>.</summary>
    public static class VenueService
    {
        private const string FileName = "venues.json";
        private const string DefaultVenue = "Karaoke Night";

        /// <summary>Loads saved venue names from disk.</summary>
        public static List<string> Load() => StringListStore.Load(FileName, DefaultVenue);

        /// <summary>Saves the venue names list to disk.</summary>
        public static void Save(IEnumerable<string> venues) => StringListStore.Save(venues, FileName);
    }
}
