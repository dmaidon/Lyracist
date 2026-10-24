// Edited on Oct 24, 2026 @ 12:00:00 -> Category weights, colours and presets; edits locked while spinning
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ScaryokeWheel;

public class WheelSegment
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#FF6D00";
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

    /// <summary>Relative size on the wheel (1-3).</summary>
    [ObservableProperty]
    private double _weight = 1;

    /// <summary>Explicit colour override; null means "use the palette colour for this position".</summary>
    [ObservableProperty]
    private string? _color;

    /// <summary>The colour actually used on the wheel (set by the view model on rebuild).</summary>
    [ObservableProperty]
    private string _displayColor = "#FF6D00";
}

public partial class ScaryokeWheelViewModel : ObservableObject
{
    private const double SliverWeight = 0.15;
    private const double MaxWeight = 3;
    private const int HistoryLimit = 8;

    private readonly ScaryokeSettings _settings;
    private static readonly Random Rng = new();

    private static readonly string[] SegmentColors =
    [
        "#6A0DAD", // Purple
        "#FF6D00", // Orange
        "#00838F", // Teal
        "#C2185B", // Pink
        "#4527A0", // Deep Blue
        "#EF6C00", // Bright Orange
        "#00695C", // Deep Teal
        "#AD1457", // Deep Pink
        "#5E35B1", // Dark Purple
        "#F57C00", // Yellow Orange
        "#00796B"  // Teal Blue
    ];

    [ObservableProperty]
    private ObservableCollection<CategoryItem> _customCategories = [];

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<WheelSegment> _wheelSegments = [];

    [ObservableProperty]
    private string _resultText = "Spin the wheel for a challenge!";

    [ObservableProperty]
    private bool _isSpinning;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _avoidRepeats;

    [ObservableProperty]
    private ObservableCollection<CategoryPreset> _presets = [];

    [ObservableProperty]
    private CategoryPreset? _selectedPreset;

    [ObservableProperty]
    private string _newPresetName = string.Empty;

    public ObservableCollection<string> History { get; } = [];

    /// <summary>Raised after <see cref="WheelSegments"/> has been fully repopulated.</summary>
    public event EventHandler? WheelSegmentsRebuilt;

    public string Version => $"Version {typeof(ScaryokeWheelViewModel).Assembly.GetName().Version}";
    public string Copyright => Lyracist.Shared.Globals.Copyright;
    public string Company => Lyracist.Shared.Globals.CompanyName;

    public int MaxCategories => ScaryokeSettings.MaxCategories;

    public ScaryokeWheelViewModel()
    {
        _settings = ScaryokeSettings.Load();
        _isMuted = _settings.IsMuted;
        _avoidRepeats = _settings.AvoidRepeats;

        foreach (var preset in _settings.Presets)
        {
            Presets.Add(preset);
        }

        foreach (string name in _settings.Categories)
        {
            _settings.Styles.TryGetValue(name, out var style);
            AddItem(name, style);
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

    partial void OnIsMutedChanged(bool value)
    {
        _settings.IsMuted = value;
        _settings.Save();
    }

    partial void OnAvoidRepeatsChanged(bool value)
    {
        _settings.AvoidRepeats = value;
        _settings.Save();
    }

    public void AddHistory(string name)
    {
        History.Insert(0, name);
        while (History.Count > HistoryLimit)
        {
            History.RemoveAt(History.Count - 1);
        }
    }

    [RelayCommand]
    private void ClearHistory() => History.Clear();

    /// <summary>
    /// True when <paramref name="name"/> was landed on recently enough to be skipped.
    /// The window is kept smaller than the wheel so a valid segment always remains.
    /// </summary>
    public bool IsRecentRepeat(string name)
    {
        if (!AvoidRepeats) return false;

        int distinct = WheelSegments.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        int window = Math.Min(2, distinct - 1);
        return History.Take(window).Contains(name, StringComparer.OrdinalIgnoreCase);
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
        _settings.Save();

        RebuildWheelSegments();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void AddCategory()
    {
        string name = NewCategoryName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) return;

        if (CustomCategories.Count >= MaxCategories)
        {
            System.Windows.MessageBox.Show(
                $"A maximum of {MaxCategories} custom categories is allowed.",
                "Category Limit",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning
            );
            return;
        }

        if (CustomCategories.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            System.Windows.MessageBox.Show(
                "This category already exists.",
                "Duplicate Category",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning
            );
            return;
        }

        AddItem(name, null);
        NewCategoryName = string.Empty;

        SaveAndRebuild();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void RemoveCategory(CategoryItem? category)
    {
        if (category == null) return;

        category.PropertyChanged -= OnItemPropertyChanged;
        CustomCategories.Remove(category);

        SaveAndRebuild();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void CycleColor(CategoryItem? category)
    {
        if (category == null) return;

        int index = Array.FindIndex(SegmentColors, c => string.Equals(c, category.DisplayColor, StringComparison.OrdinalIgnoreCase));
        category.Color = SegmentColors[(index + 1) % SegmentColors.Length];
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void CycleWeight(CategoryItem? category)
    {
        if (category == null) return;

        category.Weight = category.Weight >= MaxWeight ? 1 : category.Weight + 1;
    }

    [RelayCommand]
    private void SavePreset()
    {
        string name = NewPresetName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name) || CustomCategories.Count == 0) return;

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
        if (SelectedPreset == null) return;

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
        if (SelectedPreset == null) return;

        Presets.Remove(SelectedPreset);
        SelectedPreset = null;
        SavePresets();
    }

    private void SavePresets()
    {
        _settings.Presets = [.. Presets];
        _settings.Save();
    }

    public void RebuildWheelSegments()
    {
        var list = new List<WheelSegment>();

        // Map custom categories to wheel segments with colors
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

        // Singer's Choice is a regular-sized segment
        var singersChoice = new WheelSegment
        {
            Name = "Singer's Choice",
            Color = "#228B22",
            TextColor = GetContrastingTextColor("#228B22")
        };

        // DJ's Choice slivers are the thin black segments with spooky orange text
        var leftSliver = new WheelSegment
        {
            Name = "DJ's Choice",
            Color = "#000000",
            TextColor = "#FF5722", // Vibrant Spooky Orange-Red
            Weight = SliverWeight
        };

        var rightSliver = new WheelSegment
        {
            Name = "DJ's Choice",
            Color = "#000000",
            TextColor = "#FF5722",
            Weight = SliverWeight
        };

        if (list.Count == 0)
        {
            list.Add(leftSliver);
            list.Add(singersChoice);
            list.Add(rightSliver);
        }
        else
        {
            // Singer's Choice sits between the slivers at a fresh random position each rebuild
            int insertIndex = Rng.Next(0, list.Count + 1);
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

        WheelSegmentsRebuilt?.Invoke(this, EventArgs.Empty);
    }

    private static string GetContrastingTextColor(string hexColor)
    {
        if (string.IsNullOrEmpty(hexColor)) return "#FFFFFF";
        string hex = hexColor.TrimStart('#');
        if (hex.Length == 3)
        {
            hex = new string([hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]]);
        }
        if (hex.Length == 6)
        {
            if (int.TryParse(hex.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r) &&
                int.TryParse(hex.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g) &&
                int.TryParse(hex.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b))
            {
                double brightness = (r * 0.299) + (g * 0.587) + (b * 0.114);
                return brightness > 130 ? "#1E133A" : "#FFFFFF";
            }
        }
        return "#FFFFFF";
    }
}
