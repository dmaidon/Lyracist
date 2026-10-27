// Edited on Oct 26, 2026 @ 15:55:00 -> Update DefaultDj to Guest DJ
using System.Collections.Generic;

namespace KSRotation.Services
{
    /// <summary>Persists the DJ name list. Logic lives in <see cref="StringListStore"/>.</summary>
    public static class DjService
    {
        private const string FileName = "ksrotation_djs.json";
        private const string LegacyFileName = "djs.json";
        private const string DefaultDj = "Guest DJ";

        /// <summary>Loads saved DJ names from disk.</summary>
        public static List<string> Load() => StringListStore.Load(FileName, DefaultDj, LegacyFileName);

        /// <summary>Saves the DJ names list to disk.</summary>
        public static void Save(IEnumerable<string> djs) => StringListStore.Save(djs, FileName);
    }
}
