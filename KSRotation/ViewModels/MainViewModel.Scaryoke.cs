// Edited on Oct 27, 2026 @ 17:05:00 -> Add project toggle, singer count selection, and configured spin workflow (project|select # of singers|spin)
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSRotation.Models;
using KSRotation.Windows;
using ScaryokeWheel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;

namespace KSRotation.ViewModels
{
    public partial class MainViewModel
    {
        private ScaryokeSettings _scaryokeSettings = ScaryokeSettings.Load();
        private ScaryokeControlWindow? _scaryokeControlWindow;
        private double _scaryokeCurrentAngle;
        private static readonly Random _scaryokeRng = new();

        public static readonly string[] ScaryokeSegmentColors =
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

        public static string ScaryokePaletteColor(int index) =>
            ScaryokeSegmentColors[((index % ScaryokeSegmentColors.Length) + ScaryokeSegmentColors.Length) % ScaryokeSegmentColors.Length];

        public IReadOnlyList<string> AvailableScaryokePaletteColors => ScaryokeSegmentColors;

        [ObservableProperty]
        public partial bool IsScaryokeActive { get; set; }

        [ObservableProperty]
        public partial bool IsScaryokeSpinning { get; set; }

        [ObservableProperty]
        public partial bool ScaryokeAvoidConsecutiveRepeats { get; set; } = true;

        [ObservableProperty]
        public partial bool ScaryokeShowSingersChoice { get; set; } = true;

        [ObservableProperty]
        public partial bool ScaryokeShowDjsChoice { get; set; } = true;

        [ObservableProperty]
        public partial string ScaryokeResultText { get; set; } = "Spin the wheel for a challenge!";

        [ObservableProperty]
        public partial string ScaryokeResultDetail { get; set; } = string.Empty;

        [ObservableProperty]
        public partial SingerEntry? SelectedSingerForScaryoke { get; set; }

        partial void OnSelectedSingerForScaryokeChanged(SingerEntry? value)
        {
            UpdateScaryokeTargetDisplay();
        }

        [ObservableProperty]
        private bool _isScaryokeProjected = true;

        partial void OnIsScaryokeProjectedChanged(bool value)
        {
            _displayWindowService.SetScaryokeProjected(value);
            OnPropertyChanged(nameof(ScaryokeProjectButtonText));
        }

        [ObservableProperty]
        private int _scaryokeBatchCount = 1;

        partial void OnScaryokeBatchCountChanged(int value)
        {
            OnPropertyChanged(nameof(IsSingleSingerSelected));
            OnPropertyChanged(nameof(ScaryokeSpinButtonText));
            UpdateScaryokeTargetDisplay();
        }

        public bool IsSingleSingerSelected => ScaryokeBatchCount == 1;

        public string ScaryokeProjectButtonText => IsScaryokeProjected ? "📺 Projected (Live on Screen)" : "📺 Booth Only (Off Screen)";

        public string ScaryokeSpinButtonText
        {
            get
            {
                if (IsBatchSpinning || IsScaryokeSpinning) return "⏹ STOP SPIN";
                if (ScaryokeBatchCount == 1)
                {
                    var name = SelectedSingerForScaryoke?.Name ?? CurrentSinger?.Name;
                    return string.IsNullOrWhiteSpace(name) ? "🎲 START SPIN" : $"🎲 START SPIN ({name})";
                }
                if (ScaryokeBatchCount == 0) return "🎲 START SPIN (ALL ACTIVE)";
                return $"🎲 START SPIN (NEXT {ScaryokeBatchCount})";
            }
        }

        public void UpdateScaryokeTargetDisplay()
        {
            if (ScaryokeBatchCount == 1)
            {
                var targetSinger = SelectedSingerForScaryoke ?? CurrentSinger ?? Singers.FirstOrDefault(s => !s.IsInactive && !s.IsMusic);
                ScaryokeTargetSingerName = targetSinger != null ? (targetSinger.IsDuet ? $"{targetSinger.Name} & {targetSinger.DuetPartnerName}" : targetSinger.Name) : "Stage Open";
            }
            else if (ScaryokeBatchCount == 0)
            {
                ScaryokeTargetSingerName = "Entire Rotation";
            }
            else
            {
                ScaryokeTargetSingerName = $"Next {ScaryokeBatchCount} Performers";
            }
            _displayWindowService.SetScaryokeTargetSinger(ScaryokeTargetSingerName);
            OnPropertyChanged(nameof(ScaryokeSpinButtonText));
        }

        [ObservableProperty]
        public partial string ScaryokeTargetSingerName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool ScaryokeIsMuted { get; set; }

        [ObservableProperty]
        public partial bool ScaryokeBatchOnlyUnassigned { get; set; } = true;

        [ObservableProperty]
        public partial double ScaryokeVolume { get; set; } = 1.0;

        [ObservableProperty]
        public partial string ScaryokeMonitorDevice { get; set; } = string.Empty;

        partial void OnScaryokeMonitorDeviceChanged(string value)
        {
            if (_isRefreshingMonitors) return;
            if (!string.IsNullOrEmpty(value))
            {
                _scaryokeSettings.SelectedMonitorDevice = value;
                _scaryokeSettings.Save();
            }
            _displayWindowService.SetScaryokeMonitor(value ?? string.Empty);
        }

        public ObservableCollection<ScaryokeWheelSegment> ScaryokeWheelSegments { get; } = [];

        public ObservableCollection<string> ScaryokeHistory { get; } = [];

        public ObservableCollection<string> ScaryokeCategories { get; } = [];

        [ObservableProperty]
        public partial string? SelectedScaryokeCategory { get; set; }

        [ObservableProperty]
        public partial string NewScaryokeCategoryName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial double SelectedScaryokeWeight { get; set; } = 1;

        [ObservableProperty]
        public partial string SelectedScaryokeColor { get; set; } = "Transparent";

        [ObservableProperty]
        public partial bool IsSelectedCategoryCustomColor { get; set; }

        [ObservableProperty]
        public partial string SelectedCategoryColorModeText { get; set; } = "Auto (by app)";

        public ObservableCollection<string> ScaryokePresetNames { get; } = [];

        [ObservableProperty]
        public partial string? SelectedScaryokePreset { get; set; }

        [ObservableProperty]
        public partial string NewScaryokePresetName { get; set; } = string.Empty;

        public event EventHandler<ScaryokeSpinEventArgs>? ScaryokeSpinStarted;
        public event EventHandler<ScaryokeSpinCompletedEventArgs>? ScaryokeSpinFinished;

        public void InitializeScaryoke()
        {
            try
            {
                _scaryokeSettings = ScaryokeSettings.Load();
                ScaryokeAvoidConsecutiveRepeats = _scaryokeSettings.AvoidRepeats;
                ScaryokeShowSingersChoice = _scaryokeSettings.ShowSingersChoice;
                ScaryokeShowDjsChoice = _scaryokeSettings.ShowDjsChoice;
                ScaryokeIsMuted = _scaryokeSettings.IsMuted;
                ScaryokeVolume = _scaryokeSettings.Volume;
                ScaryokeHistory.Clear();
                foreach (var spin in _scaryokeSettings.SpinHistory)
                {
                    ScaryokeHistory.Add(spin);
                }
                ScaryokeCategories.Clear();
                foreach (var cat in _scaryokeSettings.Categories)
                {
                    ScaryokeCategories.Add(cat);
                }
                RefreshScaryokePresets();
                SelectedScaryokeCategory = ScaryokeCategories.FirstOrDefault();

                if (!string.IsNullOrEmpty(_scaryokeSettings.SelectedMonitorDevice) &&
                    AvailableMonitors.Any(m => string.Equals(m.DeviceName, _scaryokeSettings.SelectedMonitorDevice, StringComparison.OrdinalIgnoreCase)))
                {
                    ScaryokeMonitorDevice = _scaryokeSettings.SelectedMonitorDevice;
                }
                else if (string.IsNullOrEmpty(ScaryokeMonitorDevice) || !AvailableMonitors.Any(m => string.Equals(m.DeviceName, ScaryokeMonitorDevice, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!string.IsNullOrEmpty(SelectedMonitorDevice) && AvailableMonitors.Any(m => string.Equals(m.DeviceName, SelectedMonitorDevice, StringComparison.OrdinalIgnoreCase)))
                    {
                        ScaryokeMonitorDevice = SelectedMonitorDevice;
                    }
                    else if (AvailableMonitors.Count > 1)
                    {
                        ScaryokeMonitorDevice = AvailableMonitors[1].DeviceName;
                    }
                    else if (AvailableMonitors.Count > 0)
                    {
                        ScaryokeMonitorDevice = AvailableMonitors[0].DeviceName;
                    }
                }
                _displayWindowService.SetScaryokeMonitor(ScaryokeMonitorDevice);
            }
            catch
            {
                _scaryokeSettings = new ScaryokeSettings();
            }
            RebuildScaryokeWheelSegments();
        }

        partial void OnSelectedScaryokeCategoryChanged(string? value) => UpdateSelectedScaryokeStyle();

        partial void OnScaryokeAvoidConsecutiveRepeatsChanged(bool value)
        {
            _scaryokeSettings.AvoidRepeats = value;
            _scaryokeSettings.Save();
        }

        partial void OnScaryokeShowSingersChoiceChanged(bool value)
        {
            _scaryokeSettings.ShowSingersChoice = value;
            _scaryokeSettings.Save();
            RebuildScaryokeWheelSegments();
            _displayWindowService.UpdateScaryokeWheel(ScaryokeWheelSegments);
        }

        partial void OnScaryokeShowDjsChoiceChanged(bool value)
        {
            _scaryokeSettings.ShowDjsChoice = value;
            _scaryokeSettings.Save();
            RebuildScaryokeWheelSegments();
            _displayWindowService.UpdateScaryokeWheel(ScaryokeWheelSegments);
        }

        partial void OnScaryokeIsMutedChanged(bool value)
        {
            _scaryokeSettings.IsMuted = value;
            _scaryokeSettings.Save();
        }

        partial void OnScaryokeVolumeChanged(double value)
        {
            _scaryokeSettings.Volume = value;
            _scaryokeVolumeSaveTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _scaryokeVolumeSaveTimer.Tick -= OnScaryokeVolumeSaveTick;
            _scaryokeVolumeSaveTimer.Tick += OnScaryokeVolumeSaveTick;
            _scaryokeVolumeSaveTimer.Stop();
            _scaryokeVolumeSaveTimer.Start();
        }

        private System.Windows.Threading.DispatcherTimer? _scaryokeVolumeSaveTimer;

        private void OnScaryokeVolumeSaveTick(object? sender, EventArgs e)
        {
            _scaryokeVolumeSaveTimer?.Stop();
            _scaryokeSettings.Save();
        }

        private void UpdateSelectedScaryokeStyle()
        {
            if (SelectedScaryokeCategory == null)
            {
                SelectedScaryokeWeight = 1;
                SelectedScaryokeColor = "Transparent";
                IsSelectedCategoryCustomColor = false;
                SelectedCategoryColorModeText = "None";
                return;
            }

            if (_scaryokeSettings.Styles.TryGetValue(SelectedScaryokeCategory, out var style))
            {
                SelectedScaryokeWeight = Math.Clamp(style.Weight, 1, 3);
                bool hasCustom = !string.IsNullOrWhiteSpace(style.Color);
                IsSelectedCategoryCustomColor = hasCustom;
                SelectedCategoryColorModeText = hasCustom ? "Custom Color" : "Auto (by app)";
                SelectedScaryokeColor = hasCustom ? style.Color! : ScaryokePaletteColor(ScaryokeCategories.IndexOf(SelectedScaryokeCategory));
            }
            else
            {
                SelectedScaryokeWeight = 1;
                IsSelectedCategoryCustomColor = false;
                SelectedCategoryColorModeText = "Auto (by app)";
                SelectedScaryokeColor = ScaryokePaletteColor(ScaryokeCategories.IndexOf(SelectedScaryokeCategory));
            }
        }

        [RelayCommand]
        public void AddScaryokeCategory()
        {
            if (IsScaryokeSpinning) return;
            string cat = NewScaryokeCategoryName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cat)) return;

            if (ScaryokeCategories.Count >= ScaryokeSettings.MaxCategories)
            {
                System.Windows.MessageBox.Show($"Maximum of {ScaryokeSettings.MaxCategories} categories allowed.", "Category Limit", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (ScaryokeSettings.ReservedNames.Contains(cat, StringComparer.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show($"'{cat}' is a reserved category name.", "Reserved Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ScaryokeCategories.Any(c => string.Equals(c, cat, StringComparison.OrdinalIgnoreCase)))
            {
                System.Windows.MessageBox.Show($"'{cat}' is already in the categories list.", "Duplicate Category", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _scaryokeSettings.Categories.Add(cat);
            _scaryokeSettings.Save();
            ScaryokeCategories.Add(cat);
            SelectedScaryokeCategory = cat;
            NewScaryokeCategoryName = string.Empty;
            RebuildScaryokeWheelSegments();
            _displayWindowService.UpdateScaryokeWheel(ScaryokeWheelSegments);
        }

        [RelayCommand]
        public void DeleteScaryokeCategory()
        {
            if (IsScaryokeSpinning) return;
            if (SelectedScaryokeCategory == null) return;
            string cat = SelectedScaryokeCategory;
            _scaryokeSettings.Categories.RemoveAll(c => string.Equals(c, cat, StringComparison.OrdinalIgnoreCase));
            _scaryokeSettings.Styles.Remove(cat);
            _scaryokeSettings.Save();
            ScaryokeCategories.Remove(cat);
            SelectedScaryokeCategory = ScaryokeCategories.FirstOrDefault();
            RebuildScaryokeWheelSegments();
            _displayWindowService.UpdateScaryokeWheel(ScaryokeWheelSegments);
        }

        [RelayCommand]
        public void ResetScaryokeCategories()
        {
            if (IsScaryokeSpinning) return;
            string[] defaults = ["Elvis", "Rock", "Country", "Pop", "Gender Bender", "Oldies", "80s Hits", "Disco", "Hard Rock", "Disney Songs", "90s Pop"];
            _scaryokeSettings.Categories = [.. defaults];
            _scaryokeSettings.Styles.Clear();
            _scaryokeSettings.Save();

            ScaryokeCategories.Clear();
            foreach (var cat in _scaryokeSettings.Categories)
            {
                ScaryokeCategories.Add(cat);
            }
            SelectedScaryokeCategory = ScaryokeCategories.FirstOrDefault();
            RebuildScaryokeWheelSegments();
            _displayWindowService.UpdateScaryokeWheel(ScaryokeWheelSegments);
        }

        [RelayCommand]
        public void CycleScaryokeWeight()
        {
            if (IsScaryokeSpinning) return;
            if (SelectedScaryokeCategory == null) return;
            string cat = SelectedScaryokeCategory;
            if (!_scaryokeSettings.Styles.TryGetValue(cat, out var style))
            {
                style = new CategoryStyle { Weight = 1 };
                _scaryokeSettings.Styles[cat] = style;
            }
            double w = style.Weight >= 3 ? 1 : style.Weight + 1;
            style.Weight = w;
            _scaryokeSettings.Save();
            UpdateSelectedScaryokeStyle();
            RebuildScaryokeWheelSegments();
            _displayWindowService.UpdateScaryokeWheel(ScaryokeWheelSegments);
        }

        [RelayCommand]
        public void CycleScaryokeColor()
        {
            if (IsScaryokeSpinning) return;
            if (SelectedScaryokeCategory == null) return;
            string cat = SelectedScaryokeCategory;
            if (!_scaryokeSettings.Styles.TryGetValue(cat, out var style))
            {
                style = new CategoryStyle();
                _scaryokeSettings.Styles[cat] = style;
            }
            int index = Array.FindIndex(ScaryokeSegmentColors, c => string.Equals(c, SelectedScaryokeColor, StringComparison.OrdinalIgnoreCase));
            string next = ScaryokeSegmentColors[(index + 1 + ScaryokeSegmentColors.Length) % ScaryokeSegmentColors.Length];
            style.Color = next;
            _scaryokeSettings.Save();
            UpdateSelectedScaryokeStyle();
            RebuildScaryokeWheelSegments();
            _displayWindowService.UpdateScaryokeWheel(ScaryokeWheelSegments);
        }

        [RelayCommand]
        public void SetScaryokeColor(string color)
        {
            if (IsScaryokeSpinning) return;
            if (SelectedScaryokeCategory == null || !ScaryokeSettings.IsValidHexColor(color)) return;
            string cat = SelectedScaryokeCategory;
            if (!_scaryokeSettings.Styles.TryGetValue(cat, out var style))
            {
                style = new CategoryStyle();
                _scaryokeSettings.Styles[cat] = style;
            }
            style.Color = color;
            _scaryokeSettings.Save();
            UpdateSelectedScaryokeStyle();
            RebuildScaryokeWheelSegments();
            _displayWindowService.UpdateScaryokeWheel(ScaryokeWheelSegments);
        }

        [RelayCommand]
        public void ResetScaryokeColor()
        {
            if (IsScaryokeSpinning) return;
            if (SelectedScaryokeCategory == null) return;
            string cat = SelectedScaryokeCategory;
            if (_scaryokeSettings.Styles.TryGetValue(cat, out var style))
            {
                style.Color = null;
                _scaryokeSettings.Save();
            }
            UpdateSelectedScaryokeStyle();
            RebuildScaryokeWheelSegments();
            _displayWindowService.UpdateScaryokeWheel(ScaryokeWheelSegments);
        }

        private void RefreshScaryokePresets()
        {
            ScaryokePresetNames.Clear();
            foreach (var preset in _scaryokeSettings.Presets)
            {
                if (!string.IsNullOrWhiteSpace(preset.Name))
                {
                    ScaryokePresetNames.Add(preset.Name);
                }
            }
        }

        [RelayCommand]
        public void SaveScaryokePreset()
        {
            string name = NewScaryokePresetName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) return;

            var existing = _scaryokeSettings.Presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Categories = [.. _scaryokeSettings.Categories];
                existing.Styles = _scaryokeSettings.Styles.ToDictionary(k => k.Key, v => new CategoryStyle { Weight = v.Value.Weight, Color = v.Value.Color, Enabled = v.Value.Enabled });
            }
            else
            {
                _scaryokeSettings.Presets.Add(new CategoryPreset
                {
                    Name = name,
                    Categories = [.. _scaryokeSettings.Categories],
                    Styles = _scaryokeSettings.Styles.ToDictionary(k => k.Key, v => new CategoryStyle { Weight = v.Value.Weight, Color = v.Value.Color, Enabled = v.Value.Enabled })
                });
            }
            _scaryokeSettings.Save();
            NewScaryokePresetName = string.Empty;
            RefreshScaryokePresets();
            SelectedScaryokePreset = name;
        }

        [RelayCommand]
        public void LoadScaryokePreset()
        {
            if (IsScaryokeSpinning) return;
            if (SelectedScaryokePreset == null) return;
            var preset = _scaryokeSettings.Presets.FirstOrDefault(p => string.Equals(p.Name, SelectedScaryokePreset, StringComparison.OrdinalIgnoreCase));
            if (preset == null) return;

            _scaryokeSettings.Categories = ScaryokeSettings.CleanCategories(preset.Categories);
            _scaryokeSettings.Styles = (preset.Styles ?? [])
                .Where(kv => kv.Value != null && _scaryokeSettings.Categories.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => new CategoryStyle
                {
                    Weight = Math.Clamp(kv.Value.Weight, 1, 3),
                    Color = kv.Value.Color,
                    Enabled = kv.Value.Enabled
                });
            _scaryokeSettings.Save();

            ScaryokeCategories.Clear();
            foreach (var cat in _scaryokeSettings.Categories)
            {
                ScaryokeCategories.Add(cat);
            }
            SelectedScaryokeCategory = ScaryokeCategories.FirstOrDefault();
            RebuildScaryokeWheelSegments();
            _displayWindowService.UpdateScaryokeWheel(ScaryokeWheelSegments);
        }

        [RelayCommand]
        public void DeleteScaryokePreset()
        {
            if (SelectedScaryokePreset == null) return;
            string name = SelectedScaryokePreset;
            _scaryokeSettings.Presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            _scaryokeSettings.Save();
            RefreshScaryokePresets();
            SelectedScaryokePreset = ScaryokePresetNames.FirstOrDefault();
        }

        [RelayCommand]
        public void ToggleScaryoke()
        {
            if (IsScaryokeActive)
            {
                StopScaryoke();
            }
            else
            {
                StartScaryoke();
            }
        }

        public void StartScaryoke()
        {
            if (IsScaryokeActive) return;

            IsScaryokeActive = true;
            _scaryokeSettings = ScaryokeSettings.Load();
            if (ScaryokeHistory.Count == 0 && _scaryokeSettings.SpinHistory.Count > 0)
            {
                foreach (var spin in _scaryokeSettings.SpinHistory)
                {
                    ScaryokeHistory.Add(spin);
                }
            }
            RebuildScaryokeWheelSegments();

            _displayWindowService.UpdateScaryokeWheel(ScaryokeWheelSegments);
            _displayWindowService.SetScaryokeActive(true, Singers);
            _displayWindowService.SetScaryokeProjected(IsScaryokeProjected);
            UpdateScaryokeTargetDisplay();

            // Open non-modal desktop control window
            var dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            dispatcher.InvokeAsync(() =>
            {
                if (_scaryokeControlWindow == null || !_scaryokeControlWindow.IsLoaded)
                {
                    _scaryokeControlWindow = new ScaryokeControlWindow(this);
                    _scaryokeControlWindow.Owner = System.Windows.Application.Current?.MainWindow;
                    _scaryokeControlWindow.Closed += (_, _) =>
                    {
                        _scaryokeControlWindow = null;
                        if (IsScaryokeActive)
                        {
                            StopScaryoke();
                        }
                    };
                    _scaryokeControlWindow.Show();
                }
                else
                {
                    _scaryokeControlWindow.Activate();
                }
            });
        }

        public void StopScaryoke()
        {
            if (!IsScaryokeActive) return;

            CancelBatchSpin();
            IsScaryokeActive = false;
            _displayWindowService.SetScaryokeActive(false);

            var dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            dispatcher.InvokeAsync(() =>
            {
                if (_scaryokeControlWindow != null && _scaryokeControlWindow.IsLoaded)
                {
                    _scaryokeControlWindow.Close();
                    _scaryokeControlWindow = null;
                }
            });
        }

        public void RebuildScaryokeWheelSegments()
        {
            ScaryokeWheelSegments.Clear();

            var categories = _scaryokeSettings.Categories
                .Where(c => !string.IsNullOrWhiteSpace(c) && !ScaryokeSettings.ReservedNames.Contains(c, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (categories.Count == 0)
            {
                categories.AddRange(new[] { "Elvis", "Rock", "Country", "Pop", "Oldies", "80s Hits", "Disco" });
            }

            var workingList = new List<(string Name, double Weight, string Color, string TextColor)>();

            for (int i = 0; i < categories.Count; i++)
            {
                string cat = categories[i];
                double weight = 1.0;
                string color = ScaryokeSegmentColors[i % ScaryokeSegmentColors.Length];

                if (_scaryokeSettings.Styles.TryGetValue(cat, out var style))
                {
                    if (!style.Enabled) continue;
                    weight = Math.Clamp(style.Weight, 1, 3);
                    if (ScaryokeSettings.IsValidHexColor(style.Color)) color = style.Color!;
                }

                workingList.Add((cat, weight, color, "#FFFFFF"));
            }

            // Add Singer's Choice
            if (ScaryokeShowSingersChoice)
            {
                workingList.Add(("Singer's Choice", 1.0, "#22C55E", "#000000"));
            }

            // Insert 2 small DJ's Choice slivers at separated random positions
            if (ScaryokeShowDjsChoice)
            {
                int count = workingList.Count;
                int idx1 = _scaryokeRng.Next(0, count);
                int idx2 = (idx1 + Math.Max(2, (count + 1) / 2)) % (count + 2);

                workingList.Insert(idx1, ("DJ's Choice", _scaryokeSettings.SliverWeight, "#0B0914", "#FF3D00"));
                workingList.Insert(idx2, ("DJ's Choice", _scaryokeSettings.SliverWeight, "#0B0914", "#FF3D00"));
            }

            double totalWeight = workingList.Sum(s => s.Weight);
            if (totalWeight <= 0) totalWeight = 1;

            foreach (var item in workingList)
            {
                double sweep = (item.Weight / totalWeight) * 360.0;
                ScaryokeWheelSegments.Add(new ScaryokeWheelSegment
                {
                    Name = item.Name,
                    Weight = item.Weight,
                    Color = item.Color,
                    TextColor = item.TextColor,
                    Sweep = sweep
                });
            }
        }

        private ScaryokeWheelSegment SegmentAtPointerAngle(double angle)
        {
            double normalized = (angle % 360 + 360) % 360;
            double start = 0;
            foreach (var seg in ScaryokeWheelSegments)
            {
                double end = start + seg.Sweep;
                if (normalized >= start && normalized < end)
                {
                    return seg;
                }
                start = end;
            }
            return ScaryokeWheelSegments.FirstOrDefault() ?? new ScaryokeWheelSegment { Name = "Singer's Choice" };
        }

        [RelayCommand]
        public void ToggleScaryokeProjected()
        {
            IsScaryokeProjected = !IsScaryokeProjected;
        }

        [RelayCommand]
        public void SetScaryokeBatchCount(object? parameter)
        {
            if (parameter is int intVal)
            {
                ScaryokeBatchCount = intVal;
            }
            else if (parameter is string strVal && int.TryParse(strVal, out int parsed))
            {
                ScaryokeBatchCount = parsed;
            }
        }

        [RelayCommand]
        public async Task SpinScaryokeConfiguredAsync()
        {
            if (IsScaryokeSpinning || IsBatchSpinning)
            {
                CancelBatchSpin();
                return;
            }

            if (ScaryokeBatchCount <= 1)
            {
                var target = SelectedSingerForScaryoke ?? CurrentSinger ?? Singers.FirstOrDefault(s => !s.IsInactive && !s.IsMusic);
                await SpinScaryokeForSingerAsync(target, projectToAudience: IsScaryokeProjected);
            }
            else
            {
                await BatchSpinScaryokeAsync(ScaryokeBatchCount);
            }
        }

        [RelayCommand]
        public Task SpinScaryokeWheelAsync() => SpinScaryokeConfiguredAsync();

        [RelayCommand]
        public Task SpinScaryokeAsync() => SpinScaryokeConfiguredAsync();

        [RelayCommand]
        public Task SpinScaryokeBoothAsync() => SpinScaryokeForSingerAsync(SelectedSingerForScaryoke ?? CurrentSinger ?? Singers.FirstOrDefault(s => !s.IsInactive && !s.IsMusic), projectToAudience: false);

        [RelayCommand]
        public Task SpinScaryokeForSingerCommand(SingerEntry? singer) => SpinScaryokeForSingerAsync(singer, projectToAudience: false);

        [RelayCommand]
        public Task SpinScaryokeForSingerProjectedCommand(SingerEntry? singer) => SpinScaryokeForSingerAsync(singer, projectToAudience: true);

        partial void OnIsScaryokeSpinningChanged(bool value)
        {
            OnPropertyChanged(nameof(ScaryokeSpinButtonText));
        }

        partial void OnIsBatchSpinningChanged(bool value)
        {
            OnPropertyChanged(nameof(ScaryokeSpinButtonText));
        }

        public async Task SpinScaryokeForSingerAsync(SingerEntry? singer, bool projectToAudience = false)
        {
            if (IsScaryokeSpinning || ScaryokeWheelSegments.Count == 0) return;

            var targetSinger = singer ?? SelectedSingerForScaryoke ?? CurrentSinger ?? Singers.FirstOrDefault(s => !s.IsInactive && !s.IsMusic);
            ScaryokeTargetSingerName = targetSinger != null ? (targetSinger.IsDuet ? $"{targetSinger.Name} & {targetSinger.DuetPartnerName}" : targetSinger.Name) : "Stage Open";
            _displayWindowService.SetScaryokeTargetSinger(ScaryokeTargetSingerName);

            // While the wheel is in motion, ensure no category is assigned or visible for the singer
            if (targetSinger != null)
            {
                targetSinger.ScaryokeCategory = null;
                targetSinger.ScaryokeColor = null;
                _displayWindowService.Update(Singers);
            }

            IsScaryokeSpinning = true;
            ScaryokeResultText = "Spinning...";
            ScaryokeResultDetail = targetSinger != null
                ? $"Spinning the wheel to choose a category for {targetSinger.Name}..."
                : "Spinning the wheel to seal a singer's fate...";

            RebuildScaryokeWheelSegments();
            _displayWindowService.UpdateScaryokeWheel(ScaryokeWheelSegments);

            // Determine landing point uniformly so segment sweep sets true probability.
            // If ScaryokeAvoidConsecutiveRepeats is enabled, ensure we don't land on the immediate previous winner.
            double landing = 0;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                landing = _scaryokeRng.NextDouble() * 360;
                var cand = SegmentAtPointerAngle(landing);

                if (!ScaryokeAvoidConsecutiveRepeats) break;

                // Strictly prevent 2 singers in a row getting the same result
                if (ScaryokeHistory.Count > 0 && string.Equals(ScaryokeHistory[0], cand.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                break;
            }

            var landedSegment = SegmentAtPointerAngle(landing);
            double desired = (360 - landing) % 360;
            double baseAngle = _scaryokeCurrentAngle + 1440 + (_scaryokeRng.Next(0, 2) * 360);
            double targetAngle = baseAngle + (((desired - (baseAngle % 360)) + 360) % 360);
            double duration = projectToAudience ? (4.0 + (_scaryokeRng.NextDouble() * 1.5)) : 2.0;

            // Audio: whoosh start & ticks
            if (!ScaryokeIsMuted)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        using var ms = ScaryokeAudio.CreateSpinStartStream(ScaryokeVolume);
                        using var sp = new SoundPlayer(ms);
                        sp.Play();
                    }
                    catch { }
                });
            }

            // Raise spin event for desktop host window and, if projected, the audience projection screen
            ScaryokeSpinStarted?.Invoke(this, new ScaryokeSpinEventArgs(targetAngle, duration));
            if (projectToAudience)
            {
                _displayWindowService.StartScaryokeSpin(targetAngle, duration);
            }

            // Wait for deceleration
            await Task.Delay(TimeSpan.FromSeconds(duration));

            _scaryokeCurrentAngle = targetAngle % 360;
            IsScaryokeSpinning = false;

            var landed = landedSegment;
            ScaryokeResultText = landed.Name.ToUpper();
            ScaryokeResultDetail = landed.Name switch
            {
                "Singer's Choice" => targetSinger != null ? $"{targetSinger.Name} picks any song they like!" : "Singer picks any song they like!",
                "DJ's Choice" => targetSinger != null ? $"The DJ picks the song for {targetSinger.Name}... be afraid." : "The DJ picks your song... be afraid.",
                _ => targetSinger != null ? $"{targetSinger.Name}: Pick a song from {landed.Name}!" : $"Pick a song from: {landed.Name}"
            };

            // Add to history
            ScaryokeHistory.Insert(0, landed.Name);
            while (ScaryokeHistory.Count > 8)
            {
                ScaryokeHistory.RemoveAt(ScaryokeHistory.Count - 1);
            }
            _scaryokeSettings.RecordSpin(landed.Name);

            // Assign category and color to target singer
            if (targetSinger != null)
            {
                targetSinger.ScaryokeCategory = landed.Name;
                targetSinger.ScaryokeColor = landed.Color;
                _displayWindowService.Update(Singers);
                RebuildRotationJsonCacheNow();
                QueueSaveDatabase();
            }

            bool isDjsChoice = string.Equals(landed.Name, "DJ's Choice", StringComparison.OrdinalIgnoreCase);

            // Raise spin finished for desktop host window
            ScaryokeSpinFinished?.Invoke(this, new ScaryokeSpinCompletedEventArgs(ScaryokeResultText, ScaryokeResultDetail, isDjsChoice, _scaryokeCurrentAngle));
            if (projectToAudience)
            {
                _displayWindowService.CompleteScaryokeSpin(ScaryokeResultText, ScaryokeResultDetail, isDjsChoice, _scaryokeCurrentAngle);
            }

            // Sound effect on finish
            if (!ScaryokeIsMuted)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        if (isDjsChoice)
                        {
                            using var laughStream = ScaryokeAudio.CreateEvilLaughStream(ScaryokeVolume);
                            using var sp = new SoundPlayer(laughStream);
                            sp.Play();
                        }
                        else
                        {
                            using var resStream = ScaryokeAudio.CreateResultStream(ScaryokeVolume);
                            using var sp = new SoundPlayer(resStream);
                            sp.Play();
                        }
                    }
                    catch { }
                });
            }
        }

        [RelayCommand]
        public void ClearScaryokeForSinger(SingerEntry? singer)
        {
            if (singer == null) return;
            singer.ScaryokeCategory = null;
            singer.ScaryokeColor = null;
            _displayWindowService.Update(Singers);
            RebuildRotationJsonCacheNow();
            QueueSaveDatabase();
        }

        public void AssignScaryokeCategory(SingerEntry? singer, string categoryName)
        {
            if (singer == null || string.IsNullOrWhiteSpace(categoryName)) return;
            var seg = ScaryokeWheelSegments.FirstOrDefault(s => string.Equals(s.Name, categoryName, StringComparison.OrdinalIgnoreCase));
            string color = seg?.Color ?? ScaryokePaletteColor(ScaryokeCategories.IndexOf(categoryName));
            singer.ScaryokeCategory = categoryName;
            singer.ScaryokeColor = color;
            _displayWindowService.Update(Singers);
            RebuildRotationJsonCacheNow();
            QueueSaveDatabase();
        }

        [ObservableProperty]
        private bool _isBatchSpinning;

        private CancellationTokenSource? _batchSpinCts;

        [RelayCommand]
        public void CancelBatchSpin()
        {
            if (_batchSpinCts != null && !_batchSpinCts.IsCancellationRequested)
            {
                try { _batchSpinCts.Cancel(); } catch { }
            }
            IsBatchSpinning = false;
        }

        [RelayCommand]
        public async Task BatchSpinScaryokeAsync(object? parameter)
        {
            if (IsScaryokeSpinning || IsBatchSpinning) return;

            int count = 0;
            if (parameter is int intVal)
            {
                count = intVal;
            }
            else if (parameter is string strVal && int.TryParse(strVal, out int parsed))
            {
                count = parsed;
            }

            RebuildScaryokeWheelSegments();
            if (ScaryokeWheelSegments.Count == 0) return;

            // Select active, non-music performers
            var eligibleSingers = Singers
                .Where(s => !s.IsInactive && !s.IsMusic)
                .Where(s => !ScaryokeBatchOnlyUnassigned || string.IsNullOrWhiteSpace(s.ScaryokeCategory))
                .ToList();

            if (eligibleSingers.Count == 0) return;

            var targetSingers = count > 0 ? eligibleSingers.Take(count).ToList() : eligibleSingers;
            if (targetSingers.Count == 0) return;

            _batchSpinCts?.Dispose();
            _batchSpinCts = new CancellationTokenSource();
            var token = _batchSpinCts.Token;

            IsBatchSpinning = true;

            try
            {
                for (int i = 0; i < targetSingers.Count; i++)
                {
                    if (token.IsCancellationRequested || !IsScaryokeActive) break;

                    var singer = targetSingers[i];
                    SelectedSingerForScaryoke = singer;

                    // Wheel spins for this singer; category is assigned and revealed only after the spin completes!
                    await SpinScaryokeForSingerAsync(singer, projectToAudience: IsScaryokeProjected);

                    if (token.IsCancellationRequested || !IsScaryokeActive) break;

                    if (i < targetSingers.Count - 1)
                    {
                        try
                        {
                            await Task.Delay(1000, token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                IsBatchSpinning = false;
            }
        }

        [RelayCommand]
        public void ClearAllScaryokeCategories()
        {
            bool modified = false;
            foreach (var singer in Singers)
            {
                if (singer.HasScaryokeCategory)
                {
                    singer.ScaryokeCategory = null;
                    singer.ScaryokeColor = null;
                    modified = true;
                }
            }

            if (modified)
            {
                _displayWindowService.Update(Singers);
                RebuildRotationJsonCacheNow();
                QueueSaveDatabase();
            }
        }

        public void BatchSpinScaryokeInternal(int count, bool onlyUnassigned)
        {
            RebuildScaryokeWheelSegments();
            if (ScaryokeWheelSegments.Count == 0) return;

            // Select active, non-music performers
            var eligibleSingers = Singers
                .Where(s => !s.IsInactive && !s.IsMusic)
                .Where(s => !onlyUnassigned || string.IsNullOrWhiteSpace(s.ScaryokeCategory))
                .ToList();

            if (eligibleSingers.Count == 0) return;

            var targetSingers = count > 0 ? eligibleSingers.Take(count).ToList() : eligibleSingers;
            if (targetSingers.Count == 0) return;

            string lastAssigned = ScaryokeHistory.FirstOrDefault() ?? string.Empty;
            bool modified = false;

            foreach (var singer in targetSingers)
            {
                ScaryokeWheelSegment? chosen = null;

                for (int attempt = 0; attempt < 50; attempt++)
                {
                    double landing = _scaryokeRng.NextDouble() * 360;
                    var cand = SegmentAtPointerAngle(landing);

                    if (ScaryokeAvoidConsecutiveRepeats && !string.IsNullOrWhiteSpace(lastAssigned) &&
                        string.Equals(cand.Name, lastAssigned, StringComparison.OrdinalIgnoreCase) &&
                        ScaryokeWheelSegments.Count > 1)
                    {
                        continue;
                    }

                    chosen = cand;
                    break;
                }

                chosen ??= ScaryokeWheelSegments[_scaryokeRng.Next(ScaryokeWheelSegments.Count)];
                lastAssigned = chosen.Name;

                singer.ScaryokeCategory = chosen.Name;
                singer.ScaryokeColor = chosen.Color;
                modified = true;

                ScaryokeHistory.Insert(0, chosen.Name);
                while (ScaryokeHistory.Count > 8)
                {
                    ScaryokeHistory.RemoveAt(ScaryokeHistory.Count - 1);
                }
                _scaryokeSettings.RecordSpin(chosen.Name);
            }

            if (modified)
            {
                _displayWindowService.Update(Singers);
                RebuildRotationJsonCacheNow();
                QueueSaveDatabase();
            }
        }

        public ScaryokeStateDto GetScaryokeStateDto()
        {
            var cur = Singers.FirstOrDefault(s => s.IsCurrent && !s.IsMusic);
            return new ScaryokeStateDto
            {
                IsActive = IsScaryokeActive,
                IsSpinning = IsScaryokeSpinning,
                TargetAngle = _scaryokeCurrentAngle,
                SpinDuration = 4.2,
                ResultText = ScaryokeResultText,
                ResultDetail = ScaryokeResultDetail,
                PreventConsecutiveRepeats = ScaryokeAvoidConsecutiveRepeats,
                CurrentSingerName = cur?.Name ?? "Stage Open",
                Segments = [.. ScaryokeWheelSegments]
            };
        }

        /// <summary>Clears Scaryoke past spin history from memory and disk.</summary>
        public void ClearScaryokeHistory()
        {
            ScaryokeHistory.Clear();
            _scaryokeSettings.ClearSpinHistory();
        }
    }

    public class ScaryokeSpinEventArgs(double targetAngle, double duration) : EventArgs
    {
        public double TargetAngle { get; } = targetAngle;
        public double Duration { get; } = duration;
    }

    public class ScaryokeSpinCompletedEventArgs(string resultText, string resultDetail, bool isDjsChoice, double finalAngle) : EventArgs
    {
        public string ResultText { get; } = resultText;
        public string ResultDetail { get; } = resultDetail;
        public bool IsDjsChoice { get; } = isDjsChoice;
        public double FinalAngle { get; } = finalAngle;
    }
}
