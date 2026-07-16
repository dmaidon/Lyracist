using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ScaryokeWheel;

public class WheelSegment
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#FF6D00";
    public string TextColor { get; set; } = "#FFFFFF";
    public double Sweep { get; set; }
}

public partial class ScaryokeWheelViewModel : ObservableObject
{
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
    private ObservableCollection<string> _customCategories = [];

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<WheelSegment> _wheelSegments = [];

    [ObservableProperty]
    private string _resultText = "Spin the wheel for a challenge!";

    [ObservableProperty]
    private bool _isSpinning;

    public string Version => "Version 26.7.6.106";
    public string Copyright => Lyracist.Shared.Globals.Copyright;
    public string Company => Lyracist.Shared.Globals.CompanyName;

    public ScaryokeWheelViewModel()
    {
        _settings = ScaryokeSettings.Load();
        foreach (var cat in _settings.Categories)
        {
            CustomCategories.Add(cat);
        }

        RebuildWheelSegments();
    }

    [RelayCommand]
    private void AddCategory()
    {
        string name = NewCategoryName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) return;

        if (CustomCategories.Count >= 11)
        {
            System.Windows.MessageBox.Show(
                "A maximum of 11 custom categories is allowed.",
                "Category Limit",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning
            );
            return;
        }

        if (CustomCategories.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show(
                "This category already exists.",
                "Duplicate Category",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning
            );
            return;
        }

        CustomCategories.Add(name);
        NewCategoryName = string.Empty;

        _settings.Categories = [.. CustomCategories];
        _settings.Save();

        RebuildWheelSegments();
    }

    [RelayCommand]
    private void RemoveCategory(string? category)
    {
        if (category == null) return;

        CustomCategories.Remove(category);

        _settings.Categories = [.. CustomCategories];
        _settings.Save();

        RebuildWheelSegments();
    }

    public void RebuildWheelSegments()
    {
        var list = new List<WheelSegment>();

        // Map custom categories to wheel segments with colors
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

        // Singer's Choice is now the regular-sized segment (colored white/cream, text dark purple)
        var singersChoice = new WheelSegment
        {
            Name = "Singer's Choice",
            Color = "#228B22",
            TextColor = GetContrastingTextColor("#228B22")
        };

        // DJ's Choice slivers are now the smaller slivers (colored black, text spooky orange)
        var leftSliver = new WheelSegment
        {
            Name = "DJ's Choice",
            Color = "#000000",
            TextColor = "#FF5722" // Vibrant Spooky Orange-Red
        };

        var rightSliver = new WheelSegment
        {
            Name = "DJ's Choice",
            Color = "#000000",
            TextColor = "#FF5722" // Vibrant Spooky Orange-Red
        };

        if (list.Count == 0)
        {
            list.Add(leftSliver);
            list.Add(singersChoice);
            list.Add(rightSliver);
        }
        else
        {
            int insertIndex = Rng.Next(0, list.Count + 1);
            list.Insert(insertIndex, leftSliver);
            list.Insert(insertIndex + 1, singersChoice);
            list.Insert(insertIndex + 2, rightSliver);
        }

        // Calculate custom non-uniform sweeps
        // Regular categories and Singer's Choice get weight 1.0. DJ's Choice slivers get weight 0.15.
        // There are exactly 2 slivers in the list.
        double totalWeight = (list.Count - 2) + (2 * 0.15);
        double baseSweep = 360.0 / totalWeight;

        foreach (var segment in list)
        {
            if (segment.Name == "DJ's Choice")
            {
                segment.Sweep = baseSweep * 0.15;
            }
            else
            {
                segment.Sweep = baseSweep;
            }
        }

        WheelSegments.Clear();
        foreach (var segment in list)
        {
            WheelSegments.Add(segment);
        }
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