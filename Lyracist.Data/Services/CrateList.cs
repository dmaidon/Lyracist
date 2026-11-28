// Created on Nov 28, 2026 -> Update a bound folder/crate list in place so the ComboBox keeps its selection
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Lyracist.Data.Services
{
    public static class CrateList
    {
        public const string AllCrates = "All Crates";

        /// <summary>
        /// Brings <paramref name="target"/> in line with <paramref name="crates"/> without clearing it.
        /// Clearing a bound ComboBox's items drops its selection.
        /// </summary>
        public static void Sync(ObservableCollection<string> target, IReadOnlyList<string> crates)
        {
            var wanted = new List<string> { AllCrates };
            wanted.AddRange(crates.Where(c => !string.Equals(c, AllCrates, StringComparison.OrdinalIgnoreCase)));
            var keep = new HashSet<string>(wanted, StringComparer.Ordinal);

            for (int i = target.Count - 1; i >= 0; i--)
            {
                if (!keep.Contains(target[i])) target.RemoveAt(i);
            }

            for (int i = 0; i < wanted.Count; i++)
            {
                if (i < target.Count && target[i] == wanted[i]) continue;
                int existing = target.IndexOf(wanted[i]);
                if (existing >= 0) target.Move(existing, i);
                else target.Insert(i, wanted[i]);
            }
        }
    }
}
