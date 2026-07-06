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
    {
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
    };

    [ObservableProperty]
    private ObservableCollection<string> _customCategories = new();

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<WheelSegment> _wheelSegments = new();

    [ObservableProperty]
    private string _resultText = "Spin the wheel for a challenge!";

    [ObservableProperty]
    private bool _isSpinning;

    public string Version => "Version 26.7.6.106";
    public string Copyright => "© 2026 PAROLE Software - All rights reserved.";
    public string Authors => "Dennis Maidon";
    public string Company => "PAROLE Software";

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

        _settings.Categories = CustomCategories.ToList();
        _settings.Save();

        RebuildWheelSegments();
    }

    [RelayCommand]
    private void RemoveCategory(string? category)
    {
        if (category == null) return;

        CustomCategories.Remove(category);

        _settings.Categories = CustomCategories.ToList();
        _settings.Save();

        RebuildWheelSegments();
    }

    public void RebuildWheelSegments()
    {
        var list = new List<WheelSegment>();
        
        // Map custom categories to wheel segments with colors
        for (int i = 0; i < CustomCategories.Count; i++)
        {
            list.Add(new WheelSegment
            {
                Name = CustomCategories[i],
                Color = SegmentColors[i % SegmentColors.Length],
                TextColor = "#FFFFFF"
            });
        }

        // DJ's Choice is colored black
        var djsChoice = new WheelSegment
        {
            Name = "DJ's Choice",
            Color = "#000000",
            TextColor = "#FF5722" // Vibrant Spooky Orange-Red
        };

        // Singer's Choice slivers (white/cream with dark purple/black text)
        var leftSliver = new WheelSegment
        {
            Name = "Singer's Choice",
            Color = "#FFFFFF",
            TextColor = "#1E133A"
        };

        var rightSliver = new WheelSegment
        {
            Name = "Singer's Choice",
            Color = "#FFFFFF",
            TextColor = "#1E133A"
        };

        if (list.Count == 0)
        {
            list.Add(leftSliver);
            list.Add(djsChoice);
            list.Add(rightSliver);
        }
        else
        {
            int insertIndex = Rng.Next(0, list.Count + 1);
            list.Insert(insertIndex, leftSliver);
            list.Insert(insertIndex + 1, djsChoice);
            list.Insert(insertIndex + 2, rightSliver);
        }

        // Calculate custom non-uniform sweeps
        // Regular categories and DJ's Choice get weight 1.0. Slivers get weight 0.35.
        // There are exactly 2 slivers in the list.
        double totalWeight = (list.Count - 2) + (2 * 0.35);
        double baseSweep = 360.0 / totalWeight;

        foreach (var segment in list)
        {
            if (segment.Name == "Singer's Choice")
            {
                segment.Sweep = baseSweep * 0.35;
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
}
