// Edited on Oct 26, 2026 @ 06:55:00 -> Persist and restore SpinHistory across shutdowns and enforce consecutive duplicate prevention
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSRotation.Maui.Services;
using KSRotation.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace KSRotation.Maui.ViewModels
{
    public class WheelSegment
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#8A2BE2";
        public string TextColor { get; set; } = "#FFFFFF";
        public double Weight { get; set; } = 1.0;
        public double Sweep { get; set; }
    }

    /// <summary>A user category with its optional weight and colour override.</summary>
    public partial class CategoryItem : ObservableObject
    {
        public CategoryItem(string name)
        {
            Name = name;
        }

        public string Name { get; }

        /// <summary>Relative slice size (1-3).</summary>
        [ObservableProperty]
        public partial double Weight { get; set; } = 1;

        /// <summary>Explicit colour override; null means "use the palette colour for this position".</summary>
        [ObservableProperty]
        public partial string? Color { get; set; }

        /// <summary>The colour actually used on the wheel (set by the view model on rebuild).</summary>
        [ObservableProperty]
        public partial string DisplayColor { get; set; } = "#8A2BE2";
    }

    public partial class ScaryokeWheelViewModel : ObservableObject
    {
        private static readonly string[] SegmentColors =
        [
            "#6A0DAD", "#FF6D00", "#00838F", "#C2185B", "#4527A0", "#EF6C00",
            "#00695C", "#AD1457", "#5E35B1", "#F57C00", "#00796B", "#D81B60"
        ];

        private const string SingersChoice = "Singer's Choice";
        private const string DjsChoice = "DJ's Choice";
        private const double SliverWeight = 0.15;
        private const double MaxWeight = 3;

        private const int RecentLimit = 8;
        private readonly List<string> _recent = [];

        private readonly MainViewModel _rotation;
        private readonly ScaryokeSettings _settings;

        /// <summary>
        /// True when <paramref name="name"/> was landed on in the last two spins, so a spin can skip
        /// it. The window is kept smaller than the wheel so a valid segment always remains.
        /// </summary>
        public bool IsRecentRepeat(string name)
        {
            int distinct = WheelSegments.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (distinct < 2) return false;

            if (_recent.Count > 0 && string.Equals(_recent[0], name, StringComparison.OrdinalIgnoreCase))
                return true;

            int window = Math.Min(2, distinct - 1);
            return _recent.Take(window).Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        public ObservableCollection<CategoryItem> CustomCategories { get; } = [];

        public ObservableCollection<CategoryPreset> Presets { get; } = [];

        public ObservableCollection<WheelSegment> WheelSegments { get; } = [];

        [ObservableProperty]
        public partial CategoryPreset? SelectedPreset { get; set; }

        [ObservableProperty]
        public partial string NewPresetName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string NewCategoryName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ValidationError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ResultText { get; set; } = "Spin the wheel for a challenge!";

        [ObservableProperty]
        public partial bool IsSpinning { get; set; }

        public int MaxCategories => ScaryokeSettingsService.MaxCategories;

        public ScaryokeWheelViewModel(MainViewModel rotation)
        {
            _rotation = rotation;

            _settings = ScaryokeSettingsService.Load();
            foreach (var preset in _settings.Presets)
            {
                Presets.Add(preset);
            }

            foreach (string category in _settings.Categories)
            {
                _settings.Styles.TryGetValue(category, out var style);
                AddItem(category, style);
            }

            foreach (var item in _settings.SpinHistory)
            {
                _recent.Add(item);
            }

            RebuildWheelSegments();
        }

        private bool CanEdit() => !IsSpinning;

        partial void OnIsSpinningChanged(bool value)
        {
            // Editing the wheel mid-spin would redraw it under the animation
            AddCategoryCommand.NotifyCanExecuteChanged();
            RemoveCategoryCommand.NotifyCanExecuteChanged();
            CycleColorCommand.NotifyCanExecuteChanged();
            CycleWeightCommand.NotifyCanExecuteChanged();
            LoadPresetCommand.NotifyCanExecuteChanged();
        }

        private CategoryItem AddItem(string name, CategoryStyle? style)
        {
            var item = new CategoryItem(name)
            {
                Weight = Math.Clamp(style?.Weight ?? 1, 1, MaxWeight),
                Color = style?.Color
            };
            item.PropertyChanged += OnItemPropertyChanged;
            CustomCategories.Add(item);
            return item;
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // DisplayColor is an output of the rebuild, not an edit
            if (e.PropertyName is nameof(CategoryItem.Weight) or nameof(CategoryItem.Color))
            {
                SaveAndRebuild();
            }
        }

        private Dictionary<string, CategoryStyle> CurrentStyles() =>
            CustomCategories
                .Where(c => c.Weight != 1 || c.Color != null)
                .ToDictionary(c => c.Name, c => new CategoryStyle { Weight = c.Weight, Color = c.Color });

        private void SaveAndRebuild()
        {
            _settings.Categories = [.. CustomCategories.Select(c => c.Name)];
            _settings.Styles = CurrentStyles();
            ScaryokeSettingsService.Save(_settings);

            RebuildWheelSegments();
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void AddCategory()
        {
            ValidationError = string.Empty;
            string name = NewCategoryName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (CustomCategories.Count >= MaxCategories)
            {
                ValidationError = $"A maximum of {MaxCategories} custom categories is allowed.";
                return;
            }

            if (CustomCategories.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                ValidationError = "This category already exists.";
                return;
            }

            AddItem(name, null);
            NewCategoryName = string.Empty;
            SaveAndRebuild();
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void RemoveCategory(CategoryItem? category)
        {
            if (category == null)
            {
                return;
            }

            category.PropertyChanged -= OnItemPropertyChanged;
            CustomCategories.Remove(category);
            SaveAndRebuild();
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void CycleColor(CategoryItem? category)
        {
            if (category == null)
            {
                return;
            }

            int index = Array.FindIndex(SegmentColors, c => string.Equals(c, category.DisplayColor, StringComparison.OrdinalIgnoreCase));
            category.Color = SegmentColors[(index + 1) % SegmentColors.Length];
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void CycleWeight(CategoryItem? category)
        {
            if (category == null)
            {
                return;
            }

            category.Weight = category.Weight >= MaxWeight ? 1 : category.Weight + 1;
        }

        [RelayCommand]
        private void SavePreset()
        {
            string name = NewPresetName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name) || CustomCategories.Count == 0)
            {
                return;
            }

            var preset = new CategoryPreset
            {
                Name = name,
                Categories = [.. CustomCategories.Select(c => c.Name)],
                Styles = CurrentStyles()
            };

            // Saving under an existing name replaces that preset
            var existing = Presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                Presets[Presets.IndexOf(existing)] = preset;
            }
            else
            {
                Presets.Add(preset);
            }

            SelectedPreset = preset;
            NewPresetName = string.Empty;
            SavePresets();
        }

        [RelayCommand(CanExecute = nameof(CanEdit))]
        private void LoadPreset()
        {
            if (SelectedPreset == null)
            {
                return;
            }

            foreach (var item in CustomCategories)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
            CustomCategories.Clear();

            foreach (string name in SelectedPreset.Categories.Take(MaxCategories))
            {
                SelectedPreset.Styles.TryGetValue(name, out var style);
                AddItem(name, style);
            }

            SaveAndRebuild();
        }

        [RelayCommand]
        private void DeletePreset()
        {
            if (SelectedPreset == null)
            {
                return;
            }

            Presets.Remove(SelectedPreset);
            SelectedPreset = null;
            SavePresets();
        }

        private void SavePresets()
        {
            _settings.Presets = [.. Presets];
            ScaryokeSettingsService.Save(_settings);
        }

        /// <summary>
        /// Rebuilds the wheel's weighted segments: user categories plus a full-weight "Singer's Choice"
        /// segment and two small (0.15x) "DJ's Choice" slivers, inserted at a random position each spin.
        /// </summary>
        public void RebuildWheelSegments()
        {
            var list = new List<WheelSegment>();

            for (int i = 0; i < CustomCategories.Count; i++)
            {
                var item = CustomCategories[i];
                string color = item.Color ?? SegmentColors[i % SegmentColors.Length];
                item.DisplayColor = color;
                list.Add(new WheelSegment
                {
                    Name = item.Name,
                    Color = color,
                    TextColor = GetContrastingTextColor(color),
                    Weight = item.Weight
                });
            }

            var singersChoice = new WheelSegment
            {
                Name = SingersChoice,
                Color = "#228B22",
                TextColor = GetContrastingTextColor("#228B22")
            };

            var leftSliver = new WheelSegment { Name = DjsChoice, Color = "#000000", TextColor = "#FF5722", Weight = SliverWeight };
            var rightSliver = new WheelSegment { Name = DjsChoice, Color = "#000000", TextColor = "#FF5722", Weight = SliverWeight };

            if (list.Count == 0)
            {
                list.Add(leftSliver);
                list.Add(singersChoice);
                list.Add(rightSliver);
            }
            else
            {
                int insertIndex = Random.Shared.Next(0, list.Count + 1);
                list.Insert(insertIndex, leftSliver);
                list.Insert(insertIndex + 1, singersChoice);
                list.Insert(insertIndex + 2, rightSliver);
            }

            double totalWeight = list.Sum(s => s.Weight);
            foreach (var segment in list)
            {
                segment.Sweep = 360.0 * segment.Weight / totalWeight;
            }

            WheelSegments.Clear();
            foreach (var segment in list)
            {
                WheelSegments.Add(segment);
            }
        }

        /// <summary>Builds the result message for the category the wheel landed on, naming whoever is
        /// currently up in the rotation (falling back to the first active singer if nobody is flagged
        /// current yet).</summary>
        public void ApplyResult(string category)
        {
            _recent.Insert(0, category);
            if (_recent.Count > RecentLimit)
            {
                _recent.RemoveAt(_recent.Count - 1);
            }
            _settings.SpinHistory = [.. _recent];
            ScaryokeSettingsService.Save(_settings);

            var singer = _rotation.Singers.FirstOrDefault(s => s.IsCurrent)
                ?? _rotation.Singers.FirstOrDefault(s => !s.IsInactive);
            string singerName = singer?.Name ?? "The singer";

            ResultText = category switch
            {
                SingersChoice => $"{singerName} sings whatever they want!",
                DjsChoice => $"The DJ picks {singerName}'s fate!",
                "Gender Bender" => $"{singerName} must sing a song made famous by the opposite gender!",
                _ => $"{singerName} picks any song from: {category}!",
            };
        }

        public void ClearHistory()
        {
            _recent.Clear();
            _settings.SpinHistory.Clear();
            ScaryokeSettingsService.Save(_settings);
        }

        private static string GetContrastingTextColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor)) return "#FFFFFF";
            string hex = hexColor.TrimStart('#');
            if (hex.Length == 3)
            {
                hex = new string([hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]]);
            }
            if (hex.Length == 6
                && int.TryParse(hex.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r)
                && int.TryParse(hex.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g)
                && int.TryParse(hex.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b))
            {
                double brightness = (r * 0.299) + (g * 0.587) + (b * 0.114);
                return brightness > 130 ? "#1E133A" : "#FFFFFF";
            }
            return "#FFFFFF";
        }
    }
}
