// Last Edit: Jun 29, 2026 13:22 - Delegated to StringListStore to eliminate duplication with VenueService.
using System.Collections.Generic;

namespace KSRotation.Services
{
    /// <summary>Persists the DJ name list. Logic lives in <see cref="StringListStore"/>.</summary>
    public static class DjService
    {
        private const string FileName = "djs.json";
        private const string DefaultDj = "Default DJ";

        /// <summary>Loads saved DJ names from disk.</summary>
        public static List<string> Load() => StringListStore.Load(FileName, DefaultDj);

        /// <summary>Saves the DJ names list to disk.</summary>
        public static void Save(IEnumerable<string> djs) => StringListStore.Save(djs, FileName);
    }
}
