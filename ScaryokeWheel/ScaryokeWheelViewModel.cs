// Edited on Oct 26, 2026 @ 07:35:00 -> Add ResetColor command to reset category color to auto (by app)
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

/// <summary>A user category with its optional weight, colour and enabled state.</summary>
public partial class CategoryItem : ObservableObject
{
    public CategoryItem(string name)
    {
        _name = name;
        CommittedName = name;
    }

    /// <summary>Editable in the list; the view model validates and reverts bad edits.</summary>
    [ObservableProperty]
    private string _name;

    /// <summary>The last name that passed validation.</summary>
    public string CommittedName { get; set; }

    /// <summary>Relative size on the wheel (1-3).</summary>
    [ObservableProperty]
    private double _weight = 1;

    /// <summary>Explicit colour override; null means "use the palette colour for this position".</summary>
    [ObservableProperty]
    private string? _color;

    /// <summary>The colour actually used on the wheel (set by the view model on rebuild).</summary>
    [ObservableProperty]
    private string _displayColor = "#FF6D00";

    /// <summary>Disabled categories stay in the list but are left off the wheel.</summary>
    [ObservableProperty]
    private bool _isEnabled = true;
}

public partial class ScaryokeWheelViewModel : ObservableObject
{
    private const double MaxWeight = 3;
    private const int HistoryLimit = 8;
    private const string SingersChoiceName = "Singer's Choice";
    private const string DjsChoiceName = "DJ's Choice";

    /// <summary>Selectable DJ's Choice sliver sizes, smallest first.</summary>
    public static readonly (string Label, double Weight)[] SliverSizes =
    [
        ("Small", 0.15),
        ("Medium", 0.30),
        ("Large", 0.50)
    ];

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
    private string _validationError = string.Empty;

    [ObservableProperty]
    private ObservableCollection<WheelSegment> _wheelSegments = [];

    [ObservableProperty]
    private string _resultText = "Spin the wheel for a challenge!";

    [ObservableProperty]
    private string _resultDetail = string.Empty;

    [ObservableProperty]
    private bool _isSpinning;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _avoidRepeats;

    [ObservableProperty]
    private double _volume = 1.0;

    [ObservableProperty]
    private bool _showSingersChoice = true;

    [ObservableProperty]
    private bool _showDjsChoice = true;

    [ObservableProperty]
    private int _sliverSizeIndex;

    [ObservableProperty]
    private ObservableCollection<Lyracist.Shared.MonitorInfo> _availableMonitors = [];

    [ObservableProperty]
    private Lyracist.Shared.MonitorInfo? _selectedMonitor;

    [ObservableProperty]
    private bool _isProjectionActive;

    /// <summary>Raised when the target display monitor selection changes.</summary>
    public event EventHandler<string>? TargetMonitorChanged;

    [ObservableProperty]
    private ObservableCollection<CategoryPreset> _presets = [];

    [ObservableProperty]
    private CategoryPreset? _selectedPreset;

    [ObservableProperty]
    private string _newPresetName = string.Empty;

    [ObservableProperty]
    private string _undoLabel = string.Empty;

    private CategoryItem? _lastRemoved;
    private int _lastRemovedIndex;

    public ObservableCollection<string> History { get; } = [];

    public IReadOnlyList<string> SliverSizeLabels { get; } = [.. SliverSizes.Select(s => s.Label)];

    /// <summary>Raised after <see cref="WheelSegments"/> has been fully repopulated.</summary>
    public event EventHandler? WheelSegmentsRebuilt;

    public string Version => $"Version {typeof(ScaryokeWheelViewModel).Assembly.GetName().Version}";
    public string Copyright => Lyracist.Shared.Globals.Copyright;
    public string Company => Lyracist.Shared.Globals.CompanyName;

    public int MaxCategories => ScaryokeSettings.MaxCategories;

    public ScaryokeSettings Settings => _settings;

    public ScaryokeWheelViewModel()
    {
        _settings = ScaryokeSettings.Load();
        _isMuted = _settings.IsMuted;
        _avoidRepeats = _settings.AvoidRepeats;
        _volume = _settings.Volume;
        _showSingersChoice = _settings.ShowSingersChoice;
        _showDjsChoice = _settings.ShowDjsChoice;
        _sliverSizeIndex = NearestSliverIndex(_settings.SliverWeight);

        foreach (var preset in _settings.Presets)
        {
            Presets.Add(preset);
        }

        foreach (string name in _settings.Categories)
        {
            _settings.Styles.TryGetValue(name, out var style);
            AddItem(name, style);
        }

        foreach (var item in _settings.SpinHistory)
        {
            History.Add(item);
        }

        RebuildWheelSegments();

        RefreshMonitors();
        Lyracist.Shared.MonitorEnumerator.MonitorsChanged += (_, _) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(RefreshMonitors);
        };
    }

    public void RefreshMonitors()
    {
        AvailableMonitors.Clear();
        foreach (var info in Lyracist.Shared.MonitorEnumerator.GetMonitors())
        {
            AvailableMonitors.Add(info);
        }

        if (AvailableMonitors.Count > 0)
        {
            var match = AvailableMonitors.FirstOrDefault(m => string.Equals(m.DeviceName, _settings.SelectedMonitorDevice, StringComparison.OrdinalIgnoreCase));
            // Default to secondary (external/audience) monitor if multiple displays exist, otherwise primary
            SelectedMonitor = match ?? (AvailableMonitors.Count > 1
                ? AvailableMonitors.FirstOrDefault(m => !m.IsPrimary) ?? AvailableMonitors[1]
                : AvailableMonitors[0]);
        }
        else
        {
            SelectedMonitor = null;
        }
    }

    partial void OnSelectedMonitorChanged(Lyracist.Shared.MonitorInfo? value)
    {
        if (value != null)
        {
            _settings.SelectedMonitorDevice = value.DeviceName;
            _settings.Save();
            TargetMonitorChanged?.Invoke(this, value.DeviceName);
        }
    }

    private static int NearestSliverIndex(double weight)
    {
        int best = 0;
        for (int i = 1; i < SliverSizes.Length; i++)
        {
            if (Math.Abs(SliverSizes[i].Weight - weight) < Math.Abs(SliverSizes[best].Weight - weight))
            {
                best = i;
            }
        }
        return best;
    }

    private bool CanEdit() => !IsSpinning;

    partial void OnIsSpinningChanged(bool value)
    {
        // Editing the wheel mid-spin would redraw it under the animation
        AddCategoryCommand.NotifyCanExecuteChanged();
        RemoveCategoryCommand.NotifyCanExecuteChanged();
        CycleColorCommand.NotifyCanExecuteChanged();
        ResetColorCommand.NotifyCanExecuteChanged();
        CycleWeightCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        UndoRemoveCommand.NotifyCanExecuteChanged();
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

    partial void OnVolumeChanged(double value)
    {
        _settings.Volume = Math.Clamp(value, 0, 1);
        _volumeSaveTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _volumeSaveTimer.Tick -= OnVolumeSaveTick;
        _volumeSaveTimer.Tick += OnVolumeSaveTick;
        _volumeSaveTimer.Stop();
        _volumeSaveTimer.Start();
    }

    private System.Windows.Threading.DispatcherTimer? _volumeSaveTimer;

    private void OnVolumeSaveTick(object? sender, EventArgs e)
    {
        _volumeSaveTimer?.Stop();
        _settings.Save();
    }

    partial void OnShowSingersChoiceChanged(bool value)
    {
        _settings.ShowSingersChoice = value;
        _settings.Save();
        if (!IsSpinning)
        {
            RebuildWheelSegments();
        }
    }

    partial void OnShowDjsChoiceChanged(bool value)
    {
        _settings.ShowDjsChoice = value;
        _settings.Save();
        if (!IsSpinning)
        {
            RebuildWheelSegments();
        }
    }

    partial void OnSliverSizeIndexChanged(int value)
    {
        _settings.SliverWeight = SliverSizes[Math.Clamp(value, 0, SliverSizes.Length - 1)].Weight;
        _settings.Save();
        if (!IsSpinning)
        {
            RebuildWheelSegments();
        }
    }

    public void AddHistory(string name)
    {
        History.Insert(0, name);
        while (History.Count > HistoryLimit)
        {
            History.RemoveAt(History.Count - 1);
        }
        _settings.RecordSpin(name);
    }

    [RelayCommand]
    public void ClearHistory()
    {
        History.Clear();
        _settings.ClearSpinHistory();
    }

    /// <summary>
    /// True when <paramref name="name"/> was landed on recently enough to be skipped.
    /// Strictly prevents 2 singers in a row from getting the same result, and avoids recent duplicates.
    /// The window is kept smaller than the wheel so a valid segment always remains.
    /// </summary>
    public bool IsRecentRepeat(string name)
    {
        if (!AvoidRepeats) return false;

        int distinct = WheelSegments.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (distinct < 2) return false;

        // Strictly prevent 2 singers in a row getting the same result
        if (History.Count > 0 && string.Equals(History[0], name, StringComparison.OrdinalIgnoreCase))
            return true;

        int window = Math.Min(2, distinct - 1);
        return History.Take(window).Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Sets the result headline and a short instruction for the landed segment.</summary>
    public void ShowResult(string segmentName)
    {
        ResultText = segmentName.ToUpper();
        ResultDetail = segmentName switch
        {
            SingersChoiceName => "Singer picks any song they like!",
            DjsChoiceName => "The DJ picks your song... be afraid.",
            _ => $"Pick a song from: {segmentName}"
        };
    }

    private CategoryItem AddItem(string name, CategoryStyle? style, int? index = null)
    {
        var item = new CategoryItem(name)
        {
            Weight = Math.Clamp(style?.Weight ?? 1, 1, MaxWeight),
            Color = style?.Color,
            IsEnabled = style?.Enabled ?? true
        };
        item.PropertyChanged += OnItemPropertyChanged;
        if (index.HasValue)
        {
            CustomCategories.Insert(index.Value, item);
        }
        else
        {
            CustomCategories.Add(item);
        }
        return item;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CategoryItem item) return;

        if (e.PropertyName == nameof(CategoryItem.Name))
        {
            ApplyRename(item);
            return;
        }

        // DisplayColor is an output of the rebuild, not an edit
        if (e.PropertyName is nameof(CategoryItem.Weight) or nameof(CategoryItem.Color) or nameof(CategoryItem.IsEnabled))
        {
            SaveAndRebuild();
        }
    }

    private void ApplyRename(CategoryItem item)
    {
        string name = item.Name?.Trim() ?? string.Empty;
        if (name == item.CommittedName && item.Name == name) return;

        string? error = null;
        if (name.Length == 0)
        {
            error = "A category needs a name.";
        }
        else if (ScaryokeSettings.ReservedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            error = $"\"{name}\" is added to the wheel automatically.";
        }
        else if (CustomCategories.Any(c => !ReferenceEquals(c, item) && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            error = "This category already exists.";
        }

        if (error != null)
        {
            ValidationError = error;
            item.Name = item.CommittedName;
            return;
        }

        ValidationError = string.Empty;
        item.CommittedName = name;
        if (item.Name != name)
        {
            item.Name = name;
        }

        if (IsSpinning) return;
        SaveAndRebuild();
    }

    private Dictionary<string, CategoryStyle> CurrentStyles() =>
        CustomCategories
            .Where(c => c.Weight != 1 || c.Color != null || !c.IsEnabled)
            .ToDictionary(c => c.CommittedName, c => new CategoryStyle { Weight = c.Weight, Color = c.Color, Enabled = c.IsEnabled });

    private void SaveAndRebuild()
    {
        _settings.Categories = [.. CustomCategories.Select(c => c.CommittedName)];
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
            ValidationError = $"A maximum of {MaxCategories} custom categories is allowed.";
            return;
        }

        if (ScaryokeSettings.ReservedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            ValidationError = $"\"{name}\" is added to the wheel automatically.";
            return;
        }

        if (CustomCategories.Any(c => string.Equals(c.CommittedName, name, StringComparison.OrdinalIgnoreCase)))
        {
            ValidationError = "This category already exists.";
            return;
        }

        ValidationError = string.Empty;
        AddItem(name, null);
        NewCategoryName = string.Empty;

        SaveAndRebuild();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void RemoveCategory(CategoryItem? category)
    {
        if (category == null) return;

        // Remembered so one click can bring it back
        _lastRemovedIndex = CustomCategories.IndexOf(category);
        _lastRemoved = category;
        UndoLabel = $"Undo remove \"{category.CommittedName}\"";

        category.PropertyChanged -= OnItemPropertyChanged;
        CustomCategories.Remove(category);

        SaveAndRebuild();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void UndoRemove()
    {
        if (_lastRemoved == null) return;

        var item = _lastRemoved;
        bool duplicate = CustomCategories.Any(c => string.Equals(c.CommittedName, item.CommittedName, StringComparison.OrdinalIgnoreCase));
        if (CustomCategories.Count < MaxCategories && !duplicate)
        {
            item.PropertyChanged += OnItemPropertyChanged;
            CustomCategories.Insert(Math.Clamp(_lastRemovedIndex, 0, CustomCategories.Count), item);
            SaveAndRebuild();
        }

        _lastRemoved = null;
        UndoLabel = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void MoveUp(CategoryItem? category) => Move(category, -1);

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void MoveDown(CategoryItem? category) => Move(category, 1);

    private void Move(CategoryItem? category, int offset)
    {
        if (category == null) return;

        int from = CustomCategories.IndexOf(category);
        int to = from + offset;
        if (from < 0 || to < 0 || to >= CustomCategories.Count) return;

        CustomCategories.Move(from, to);
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
    private void ResetColor(CategoryItem? category)
    {
        if (category == null) return;

        category.Color = null;
        SaveAndRebuild();
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
            Categories = [.. CustomCategories.Select(c => c.CommittedName)],
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
        _lastRemoved = null;
        UndoLabel = string.Empty;

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

        // Map enabled categories to wheel segments; colour falls back by list position so it stays
        // stable when another category is toggled off
        for (int i = 0; i < CustomCategories.Count; i++)
        {
            var item = CustomCategories[i];
            string color = item.Color ?? SegmentColors[i % SegmentColors.Length];
            item.DisplayColor = color;
            if (!item.IsEnabled) continue;

            list.Add(new WheelSegment
            {
                Name = item.CommittedName,
                Color = color,
                TextColor = GetContrastingTextColor(color),
                Weight = item.Weight
            });
        }

        double sliverWeight = SliverSizes[Math.Clamp(SliverSizeIndex, 0, SliverSizes.Length - 1)].Weight;
        WheelSegment NewSliver() => new()
        {
            Name = DjsChoiceName,
            Color = "#000000",
            TextColor = "#FF5722", // Vibrant Spooky Orange-Red
            Weight = sliverWeight
        };

        if (ShowSingersChoice && ShowDjsChoice)
        {
            var singersChoice = new WheelSegment
            {
                Name = SingersChoiceName,
                Color = "#228B22",
                TextColor = GetContrastingTextColor("#228B22")
            };

            // Singer's Choice sits between the two slivers at a fresh random position each rebuild
            int insertIndex = Rng.Next(0, list.Count + 1);
            list.Insert(insertIndex, NewSliver());
            list.Insert(insertIndex + 1, singersChoice);
            list.Insert(insertIndex + 2, NewSliver());
        }
        else if (ShowSingersChoice && !ShowDjsChoice)
        {
            var singersChoice = new WheelSegment
            {
                Name = SingersChoiceName,
                Color = "#228B22",
                TextColor = GetContrastingTextColor("#228B22")
            };

            int insertIndex = Rng.Next(0, list.Count + 1);
            list.Insert(insertIndex, singersChoice);
        }
        else if (!ShowSingersChoice && ShowDjsChoice)
        {
            // Without Singer's Choice the slivers are scattered independently
            list.Insert(Rng.Next(0, list.Count + 1), NewSliver());
            list.Insert(Rng.Next(0, list.Count + 1), NewSliver());
        }

        double totalWeight = list.Sum(s => s.Weight);
        if (totalWeight > 0)
        {
            foreach (var segment in list)
            {
                segment.Sweep = 360.0 * segment.Weight / totalWeight;
            }
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
