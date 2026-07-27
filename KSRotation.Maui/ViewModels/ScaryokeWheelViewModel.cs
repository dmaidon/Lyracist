// Created on Jul 27, 2026 @ 11:00:00 -> Weighted category wheel, tied to the live rotation queue for
// current-singer messaging. Segment math mirrors the WPF ScaryokeWheel/Lyracist ScaryokeViewModel
// implementations so the mechanic (categories + Singer's Choice + DJ's Choice) stays the same.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSRotation.Maui.Services;
using KSRotation.ViewModels;
using System.Collections.ObjectModel;

namespace KSRotation.Maui.ViewModels
{
    public class WheelSegment
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#8A2BE2";
        public string TextColor { get; set; } = "#FFFFFF";
        public double Sweep { get; set; }
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

        private readonly MainViewModel _rotation;

        public ObservableCollection<string> CustomCategories { get; } = [];

        public ObservableCollection<WheelSegment> WheelSegments { get; } = [];

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

            var settings = ScaryokeSettingsService.Load();
            foreach (var category in settings.Categories)
            {
                CustomCategories.Add(category);
            }

            RebuildWheelSegments();
        }

        [RelayCommand]
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

            if (CustomCategories.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                ValidationError = "This category already exists.";
                return;
            }

            CustomCategories.Add(name);
            NewCategoryName = string.Empty;
            SaveCategories();
            RebuildWheelSegments();
        }

        [RelayCommand]
        private void RemoveCategory(string? category)
        {
            if (category == null)
            {
                return;
            }

            CustomCategories.Remove(category);
            SaveCategories();
            RebuildWheelSegments();
        }

        private void SaveCategories()
        {
            ScaryokeSettingsService.Save(new ScaryokeSettings { Categories = [.. CustomCategories] });
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
                string color = SegmentColors[i % SegmentColors.Length];
                list.Add(new WheelSegment
                {
                    Name = CustomCategories[i],
                    Color = color,
                    TextColor = GetContrastingTextColor(color)
                });
            }

            var singersChoice = new WheelSegment
            {
                Name = SingersChoice,
                Color = "#228B22",
                TextColor = GetContrastingTextColor("#228B22")
            };

            var leftSliver = new WheelSegment { Name = DjsChoice, Color = "#000000", TextColor = "#FF5722" };
            var rightSliver = new WheelSegment { Name = DjsChoice, Color = "#000000", TextColor = "#FF5722" };

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

            // Regular categories and Singer's Choice get weight 1.0; the two DJ's Choice slivers get 0.15.
            double totalWeight = (list.Count - 2) + (2 * 0.15);
            double baseSweep = 360.0 / totalWeight;

            foreach (var segment in list)
            {
                segment.Sweep = segment.Name == DjsChoice ? baseSweep * 0.15 : baseSweep;
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
