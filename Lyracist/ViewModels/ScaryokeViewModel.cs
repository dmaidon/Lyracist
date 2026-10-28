// Edited on Oct 27, 2026 @ 12:35:00 -> Assign and show Scaryoke category on current singer after wheel finishes spinning
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Services.Display;
using System.Collections.ObjectModel;

namespace Lyracist.ViewModels;

public class WheelSegment
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#8A2BE2";
    public string TextColor { get; set; } = "#FFFFFF";
    public double Weight { get; set; } = 1.0;
    public double Sweep { get; set; }
}

public partial class ScaryokeViewModel : BaseViewModel
{
    /// <summary>The wheel segments, clockwise from the top.</summary>
    public static string[] WheelCategories => [.. Core.Helpers.AppSettings.ScaryokeCategories];

    public ObservableCollection<WheelSegment> WheelSegments { get; } = [];

    private const double SliverWeight = 0.15;

    /// <summary>Wheel palette, also used by Settings to cycle a category's colour.</summary>
    public static readonly string[] Palette =
    [
        "#6A0DAD", "#FF6D00", "#00838F", "#C2185B", "#4527A0", "#EF6C00",
        "#00695C", "#AD1457", "#5E35B1", "#F57C00", "#00796B", "#D81B60"
    ];

    public static string PaletteColor(int index) => Palette[index % Palette.Length];

    private const int RecentLimit = 11;
    private readonly List<string> _recent = [];

    [ObservableProperty]
    private bool _avoidRepeats = Core.Helpers.AppSettings.ScaryokeAvoidRepeats;

    /// <summary>
    /// True when <paramref name="name"/> was landed on in the last spin and <see cref="AvoidRepeats"/>
    /// is enabled, preventing two singers in a row from getting the same result.
    /// </summary>
    public bool IsRecentRepeat(string name)
    {
        if (!AvoidRepeats || !Core.Helpers.AppSettings.ScaryokeAvoidRepeats) return false;
        if (_recent.Count == 0) return false;
        return string.Equals(_recent[0], name, StringComparison.OrdinalIgnoreCase);
    }

    private readonly IDisplayService _display;
    private readonly RotationViewModel _rotation;

    [ObservableProperty]
    private string _resultText = "Spin the wheel to seal a singer's fate!";

    public ScaryokeViewModel(IDisplayService display, RotationViewModel rotation)
    {
        _display = display;
        _rotation = rotation;
        _recent.AddRange(Core.Helpers.AppSettings.ScaryokeSpinHistory);
        _rotation.RotationCleared += ClearHistory;
        RebuildWheelSegments(); // Initial segments setup
    }

    public void RebuildWheelSegments()
    {
        var list = new List<WheelSegment>();
        var customCategories = Core.Helpers.AppSettings.ScaryokeCategories;

        for (int i = 0; i < customCategories.Count; i++)
        {
            var style = Core.Helpers.AppSettings.GetScaryokeStyle(customCategories[i]);
            string color = style.Color ?? PaletteColor(i);
            list.Add(new WheelSegment
            {
                Name = customCategories[i],
                Color = color,
                TextColor = GetContrastingTextColor(color),
                Weight = Math.Clamp(style.Weight, 1, 3)
            });
        }

        bool showSingersChoice = Core.Helpers.AppSettings.ScaryokeShowSingersChoice;
        bool showDjsChoice = Core.Helpers.AppSettings.ScaryokeShowDjsChoice;

        var specialSegments = new List<WheelSegment>();

        if (showDjsChoice)
        {
            specialSegments.Add(new WheelSegment
            {
                Name = "DJ's Choice",
                Color = "#000000",
                TextColor = "#FF5722",
                Weight = SliverWeight
            });
        }

        if (showSingersChoice)
        {
            specialSegments.Add(new WheelSegment
            {
                Name = "Singer's Choice",
                Color = "#228B22",
                TextColor = GetContrastingTextColor("#228B22")
            });
        }

        if (showDjsChoice)
        {
            specialSegments.Add(new WheelSegment
            {
                Name = "DJ's Choice",
                Color = "#000000",
                TextColor = "#FF5722",
                Weight = SliverWeight
            });
        }

        if (specialSegments.Count > 0)
        {
            if (list.Count == 0)
            {
                list.AddRange(specialSegments);
            }
            else
            {
                int insertIndex = Random.Shared.Next(0, list.Count + 1);
                list.InsertRange(insertIndex, specialSegments);
            }
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

    /// <summary>
    /// Handles the category the wheel landed on. Returns true when the wheel
    /// should immediately spin again ("Spin Again" segment).
    /// </summary>
    private Models.Singer? _spinSinger;

    /// <summary>Captures the singer the spin is for and clears their previous challenge, so the new category is revealed only once the wheel stops.</summary>
    public void BeginSpin()
    {
        _spinSinger = _rotation.SelectedSinger ?? _rotation.Rotation.FirstOrDefault();
        if (_spinSinger != null)
        {
            _spinSinger.ScaryokeCategory = null;
            _spinSinger.ScaryokeColor = null;
        }
    }

    public bool ApplyResult(string category)
    {
        if (category != "Spin Again")
        {
            _recent.Insert(0, category);
            if (_recent.Count > RecentLimit)
            {
                _recent.RemoveAt(_recent.Count - 1);
            }
            Core.Helpers.AppSettings.RecordScaryokeSpin(category);
        }

        if (category == "Spin Again")
        {
            ResultText = "🔁 Spin Again!";
            _display.ShowLyricsOverlay("🎃 SCARYOKE: Spin Again!", 4);
            return true;
        }

        var singer = _spinSinger ?? _rotation.SelectedSinger ?? _rotation.Rotation.FirstOrDefault();
        _spinSinger = null;
        string singerName = singer?.Name ?? "The singer";

        if (singer != null)
        {
            singer.ScaryokeCategory = category;
            var matchedSegment = WheelSegments.FirstOrDefault(s => string.Equals(s.Name, category, StringComparison.OrdinalIgnoreCase));
            singer.ScaryokeColor = matchedSegment?.Color ?? "#FF6D00";
        }

        ResultText = category switch
        {
            "Singer's Choice" => $"{singerName} sings whatever they want!",
            "DJ's Choice" => $"The DJ picks {singerName}'s fate!",
            "Gender Bender" => $"{singerName} must sing a song made famous by the opposite gender!",
            _ => $"{singerName} picks any song from: {category}!",
        };
        _display.ShowLyricsOverlay($"🎃 SCARYOKE: {category}!\n{ResultText}", 10);
        return false;
    }

    /// <summary>Clears the landed category history from memory and settings.</summary>
    public void ClearHistory()
    {
        _recent.Clear();
        Core.Helpers.AppSettings.ClearScaryokeSpinHistory();
    }
}