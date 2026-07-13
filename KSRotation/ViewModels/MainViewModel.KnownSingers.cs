// Last Edit: Jun 30, 2026 08:40 - Split known-singer database/autocomplete members out of MainViewModel into this partial.
using KSRotation.Services;

namespace KSRotation.ViewModels
{
    public partial class MainViewModel
    {
        public void LoadKnownSingers()
        {
            try
            {
                List<string> list = StringListStore.Load("singers_db.json", string.Empty);
                if (list.Count > 0)
                {
                    KnownSingers.Clear();
                    foreach (var s in list.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            KnownSingers.Add(s);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.LoadKnownSingers", ex);
            }
        }

        public void AddKnownSinger(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            if (string.Equals(name, "New Singer", StringComparison.OrdinalIgnoreCase)) return;
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
                StringListStore.Save(KnownSingers, "singers_db.json");
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
