// Created on Oct 26, 2026 @ 00:05:00 -> Scaryoke Wheel host viewmodel logic, commands, sound synthesis, and tablet synchronization
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

        private static readonly string[] ScaryokeSegmentColors =
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
        public partial bool IsScaryokeActive { get; set; }

        [ObservableProperty]
        public partial bool IsScaryokeSpinning { get; set; }

        [ObservableProperty]
        public partial bool ScaryokeAvoidConsecutiveRepeats { get; set; } = true;

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

        public event EventHandler<ScaryokeSpinEventArgs>? ScaryokeSpinStarted;
        public event EventHandler<ScaryokeSpinCompletedEventArgs>? ScaryokeSpinFinished;

        public void InitializeScaryoke()
        {
            try
            {
                _scaryokeSettings = ScaryokeSettings.Load();
                ScaryokeAvoidConsecutiveRepeats = _scaryokeSettings.AvoidRepeats;
                ScaryokeIsMuted = _scaryokeSettings.IsMuted;
                ScaryokeVolume = _scaryokeSettings.Volume;
            }
            catch
            {
                _scaryokeSettings = new ScaryokeSettings();
            }
            RebuildScaryokeWheelSegments();
        }

        partial void OnScaryokeAvoidConsecutiveRepeatsChanged(bool value)
        {
            _scaryokeSettings.AvoidRepeats = value;
            _scaryokeSettings.Save();
        }

        partial void OnScaryokeIsMutedChanged(bool value)
        {
            _scaryokeSettings.IsMuted = value;
            _scaryokeSettings.Save();
        }

        partial void OnScaryokeVolumeChanged(double value)
        {
            _scaryokeSettings.Volume = value;
            _scaryokeSettings.Save();
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
                    if (!string.IsNullOrWhiteSpace(style.Color)) color = style.Color;
                }

                workingList.Add((cat, weight, color, "#FFFFFF"));
            }

            // Add Singer's Choice
            workingList.Add(("Singer's Choice", 1.0, "#22C55E", "#000000"));

            // Insert 2 small DJ's Choice slivers at separated random positions
            int count = workingList.Count;
            int idx1 = _scaryokeRng.Next(0, count);
            int idx2 = (idx1 + Math.Max(2, count / 2)) % (count + 1);

            workingList.Insert(idx1, ("DJ's Choice", _scaryokeSettings.SliverWeight, "#0B0914", "#FF3D00"));
            workingList.Insert(idx2, ("DJ's Choice", _scaryokeSettings.SliverWeight, "#0B0914", "#FF3D00"));

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

            var landed = SegmentAtPointerAngle(landing);
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
