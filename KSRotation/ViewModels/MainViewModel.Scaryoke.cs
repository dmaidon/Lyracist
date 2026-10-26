// Edited on Oct 26, 2026 @ 07:45:00 -> Add max 11 categories, custom/auto color management, presets, and wheel toggles
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
        public partial bool ScaryokeIsMuted { get; set; }

        [ObservableProperty]
        public partial double ScaryokeVolume { get; set; } = 1.0;

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
        public async Task SpinScaryokeWheelAsync()
        {
            if (IsScaryokeSpinning || ScaryokeWheelSegments.Count == 0) return;

            IsScaryokeSpinning = true;
            ScaryokeResultText = "Spinning...";
            ScaryokeResultDetail = "Spinning the wheel to seal a singer's fate...";

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
            double duration = 4.0 + (_scaryokeRng.NextDouble() * 1.5);

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

            // Raise spin event for desktop host window and projection screen
            ScaryokeSpinStarted?.Invoke(this, new ScaryokeSpinEventArgs(targetAngle, duration));
            _displayWindowService.StartScaryokeSpin(targetAngle, duration);

            // Wait for deceleration
            await Task.Delay(TimeSpan.FromSeconds(duration));

            _scaryokeCurrentAngle = targetAngle % 360;
            IsScaryokeSpinning = false;

            var landed = landedSegment;
            ScaryokeResultText = landed.Name.ToUpper();
            ScaryokeResultDetail = landed.Name switch
            {
                "Singer's Choice" => "Singer picks any song they like!",
                "DJ's Choice" => "The DJ picks your song... be afraid.",
                _ => $"Pick a song from: {landed.Name}"
            };

            // Add to history
            ScaryokeHistory.Insert(0, landed.Name);
            while (ScaryokeHistory.Count > 8)
            {
                ScaryokeHistory.RemoveAt(ScaryokeHistory.Count - 1);
            }
            _scaryokeSettings.RecordSpin(landed.Name);

            bool isDjsChoice = string.Equals(landed.Name, "DJ's Choice", StringComparison.OrdinalIgnoreCase);

            // Raise spin finished for desktop host window and projection screen
            ScaryokeSpinFinished?.Invoke(this, new ScaryokeSpinCompletedEventArgs(ScaryokeResultText, ScaryokeResultDetail, isDjsChoice, _scaryokeCurrentAngle));
            _displayWindowService.CompleteScaryokeSpin(ScaryokeResultText, ScaryokeResultDetail, isDjsChoice, _scaryokeCurrentAngle);

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
