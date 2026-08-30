// Edited on Aug 30, 2026 @ 08:26:00 -> Update filename to ksrotation_djs.json with legacy djs.json fallback
using System.Collections.Generic;

namespace KSRotation.Services
{
    /// <summary>Persists the DJ name list. Logic lives in <see cref="StringListStore"/>.</summary>
    public static class DjService
    {
        private const string FileName = "ksrotation_djs.json";
        private const string LegacyFileName = "djs.json";
        private const string DefaultDj = "Default DJ";

        /// <summary>Loads saved DJ names from disk.</summary>
        public static List<string> Load() => StringListStore.Load(FileName, DefaultDj, LegacyFileName);

        /// <summary>Saves the DJ names list to disk.</summary>
        public static void Save(IEnumerable<string> djs) => StringListStore.Save(djs, FileName);
    }
}
