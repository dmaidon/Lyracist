// Edited on Nov 14, 2026 @ 09:17:00 -> Filter KnownSingers database to only singers with first & last names or distinct names
using KSRotation.Services;
using Lyracist.Shared;

namespace KSRotation.ViewModels
{
    public partial class MainViewModel
    {
        public void LoadKnownSingers()
        {
            try
            {
                List<string> list = StringListStore.Load("ksrotation_singers.json", string.Empty, "singers_db.json");
                if (list.Count > 0)
                {
                    KnownSingers.Clear();
                    foreach (var s in list.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(s) && NameFormatting.IsQualifyingDatabaseSingerName(s))
                        {
                            KnownSingers.Add(NameFormatting.CleanSingerName(s));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.LoadKnownSingers", ex);
            }
        }

        public void AddKnownSinger(string name, bool isDistinctSingleName = false)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            name = NameFormatting.CleanSingerName(name);
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!NameFormatting.IsQualifyingDatabaseSingerName(name, isDistinctSingleName)) return;
            if (KnownSingers.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase))) return;

            int insertAt = 0;
            while (insertAt < KnownSingers.Count &&
                   string.Compare(KnownSingers[insertAt], name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertAt++;
            }
            KnownSingers.Insert(insertAt, name);
            SaveKnownSingers();
        }

        private void SaveKnownSingers()
        {
            if (IsTestMode) return; // Don't persist test mode singers to disk

            try
            {
                StringListStore.Save(KnownSingers, "ksrotation_singers.json");
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.SaveKnownSingers", ex);
            }
        }

        public void FilterKnownSingers(string text)
        {
            FilteredSingers.Clear();
            if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "New Singer", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var s in KnownSingers)
                {
                    FilteredSingers.Add(s);
                }
            }
            else
            {
                var matches = KnownSingers
                    .Where(s => s.Contains(text, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var s in matches)
                {
                    FilteredSingers.Add(s);
                }
            }
        }
    }
}
