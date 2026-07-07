using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Core.Interfaces;
using Lyracist.Services.Display;

namespace Lyracist.ViewModels;

public class WheelSegment
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#8A2BE2";
    public string TextColor { get; set; } = "#FFFFFF";
    public double Sweep { get; set; }
}

public partial class ScaryokeViewModel : BaseViewModel
{
    /// <summary>The wheel segments, clockwise from the top.</summary>
    public static string[] WheelCategories => Core.Helpers.AppSettings.ScaryokeCategories.ToArray();

    public ObservableCollection<WheelSegment> WheelSegments { get; } = new();

    private readonly IDisplayService _display;
    private readonly RotationViewModel _rotation;

    [ObservableProperty]
    private string _resultText = "Spin the wheel to seal a singer's fate!";

    public ScaryokeViewModel(IDisplayService display, RotationViewModel rotation)
    {
        _display = display;
        _rotation = rotation;
        RebuildWheelSegments(); // Initial segments setup
    }

    public void RebuildWheelSegments()
    {
        var list = new List<WheelSegment>();
        var customCategories = Core.Helpers.AppSettings.ScaryokeCategories;
        var segmentColors = new[]
        {
            "#6A0DAD", "#FF6D00", "#00838F", "#C2185B", "#4527A0", "#EF6C00",
            "#00695C", "#AD1457", "#5E35B1", "#F57C00", "#00796B", "#D81B60"
        };

        for (int i = 0; i < customCategories.Count; i++)
        {
            list.Add(new WheelSegment
            {
                Name = customCategories[i],
                Color = segmentColors[i % segmentColors.Length],
                TextColor = "#FFFFFF"
            });
        }

        var singersChoice = new WheelSegment
        {
            Name = "Singer's Choice",
            Color = "#FFFFFF",
            TextColor = "#1E133A"
        };

        var leftSliver = new WheelSegment
        {
            Name = "DJ's Choice",
            Color = "#000000",
            TextColor = "#FF5722"
        };

        var rightSliver = new WheelSegment
        {
            Name = "DJ's Choice",
            Color = "#000000",
            TextColor = "#FF5722"
        };

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

    /// <summary>
    /// Handles the category the wheel landed on. Returns true when the wheel
    /// should immediately spin again ("Spin Again" segment).
    /// </summary>
    public bool ApplyResult(string category)
    {
        if (category == "Spin Again")
        {
            ResultText = "🔁 Spin Again!";
            _display.ShowLyricsOverlay("🎃 SCARYOKE: Spin Again!", 4);
            return true;
        }

        var singer = _rotation.SelectedSinger ?? _rotation.Rotation.FirstOrDefault();
        string singerName = singer?.Name ?? "The singer";

        switch (category)
        {
            case "Singer's Choice":
                ResultText = $"{singerName} sings whatever they want!";
                break;

            case "DJ's Choice":
                ResultText = $"The DJ picks {singerName}'s fate!";
                break;

            case "Gender Bender":
                ResultText = $"{singerName} must sing a song made famous by the opposite gender!";
                break;

            default:
                ResultText = $"{singerName} picks any song from: {category}!";
                break;
        }

        _display.ShowLyricsOverlay($"🎃 SCARYOKE: {category}!\n{ResultText}", 10);
        return false;
    }
}
