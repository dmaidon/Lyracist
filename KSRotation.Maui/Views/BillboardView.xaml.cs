// Edited on Oct 29, 2026 @ 14:53:00 -> Add Holiday character animations, themed view styling, and live Scaryoke wheel projection to BillboardView
using KSRotation.Maui.ViewModels;
using KSRotation.Models;
using KSRotation.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;
using System;
using System.ComponentModel;
using System.Linq;

namespace KSRotation.Maui.Views
{
    public partial class BillboardView : ContentView
    {
        private const int MaxActiveHolidaySprites = 7;

        private IDispatcherTimer? _clockTimer;
        private IDispatcherTimer? _holidayCrosserTimer;
        private IDispatcherTimer? _holidayAmbientTimer;
        private MainViewModel? _boundVm;
        private string _activeHolidayKey = string.Empty;
        private int _activeSpriteCount;

        private readonly ScaryokeWheelDrawable _wheelDrawable = new();
        private double _wheelCurrentAngle;
        private double _wheelLastTargetAngle = double.NaN;
        private bool _wheelWasSpinning;

        public BillboardView()
        {
            InitializeComponent();
            ProjectedWheelGraphicsView.Drawable = _wheelDrawable;
            UpdateClock();
        }

        protected override void OnBindingContextChanged()
        {
            if (_boundVm != null)
            {
                _boundVm.PropertyChanged -= OnViewModelPropertyChanged;
                _boundVm.MauiScaryokeStateChanged -= OnMauiScaryokeStateChanged;
            }

            base.OnBindingContextChanged();

            _boundVm = BindingContext as MainViewModel;
            if (_boundVm != null)
            {
                _boundVm.PropertyChanged += OnViewModelPropertyChanged;
                _boundVm.MauiScaryokeStateChanged += OnMauiScaryokeStateChanged;
                ApplyProjectionViewTheme(_boundVm.SelectedProjectionView);
                ApplyHolidayOverlay(_boundVm.ActiveHolidayTheme);
                OnMauiScaryokeStateChanged(_boundVm.MauiScaryokeState);
            }
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            if (Handler != null)
            {
                StartClock();
                if (_boundVm != null)
                {
                    ApplyProjectionViewTheme(_boundVm.SelectedProjectionView);
                    ApplyHolidayOverlay(_boundVm.ActiveHolidayTheme);
                    OnMauiScaryokeStateChanged(_boundVm.MauiScaryokeState);
                }
            }
            else
            {
                StopClock();
                StopHolidayTimers();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_boundVm == null) return;

            if (e.PropertyName == nameof(MainViewModel.SelectedProjectionView))
            {
                Dispatcher.Dispatch(() => ApplyProjectionViewTheme(_boundVm.SelectedProjectionView));
            }
            else if (e.PropertyName is nameof(MainViewModel.HolidayThemeChoice) or nameof(MainViewModel.ActiveHolidayTheme))
            {
                Dispatcher.Dispatch(() => ApplyHolidayOverlay(_boundVm.ActiveHolidayTheme));
            }
        }

        private void ApplyProjectionViewTheme(string? viewName)
        {
            var (bg, panel, card, accent, secondary) = (viewName ?? "Normal List") switch
            {
                "Vegas Marquee" => ("#1A0805", "#2B0D08", "#3D140D", "#FFD700", "#FF4500"),
                "Disco Ball" => ("#0E0521", "#190A38", "#261054", "#EC4899", "#06B6D4"),
                "Synthwave Grid" => ("#090218", "#140530", "#21094A", "#FF007F", "#00F0FF"),
                "Vinyl Turntable" => ("#111113", "#1A1A1E", "#25252B", "#D97706", "#A1A1AA"),
                "Concert Festival Lineup" => ("#180B20", "#271234", "#381A4A", "#F97316", "#FACC15"),
                "Casino Slot Reels" => ("#061A10", "#0C2B1B", "#123D27", "#FBBF24", "#EF4444"),
                "Jukebox" => ("#190B04", "#2B1308", "#3E1C0D", "#FB923C", "#2DD4BF"),
                "Stadium Jumbotron" => ("#040D1A", "#091930", "#102648", "#38BDF8", "#FACC15"),
                "Movie Theater 'Now Showing'" => ("#160408", "#26080E", "#380C15", "#FDE047", "#F87171"),
                "Purple Velvet Curtain" => ("#120422", "#1F083A", "#2E0D54", "#E879F9", "#FBBF24"),
                "Star Wars Crawl" => ("#000000", "#08080C", "#101018", "#FFE81F", "#FFE81F"),
                _ => ("#070B14", "#0E1626", "#152238", "#F59E0B", "#3B82F6")
            };

            BillboardRoot.BackgroundColor = Color.FromArgb(bg);
            if (HeaderBorder != null) HeaderBorder.BackgroundColor = Color.FromArgb(panel);
            if (UpcomingQueueBorder != null) UpcomingQueueBorder.BackgroundColor = Color.FromArgb(panel);
            if (NowPerformingBorder != null)
            {
                NowPerformingBorder.BackgroundColor = Color.FromArgb(card);
                NowPerformingBorder.Stroke = Color.FromArgb(accent);
            }
            if (UpNextBorder != null)
            {
                UpNextBorder.BackgroundColor = Color.FromArgb(panel);
                UpNextBorder.Stroke = Color.FromArgb(secondary);
            }
        }

        private void ApplyHolidayOverlay(HolidayTheme? theme)
        {
            if (HolidayOverlayRoot == null || HolidaySpriteCanvas == null || HolidayFogBox == null) return;

            if (theme == null || string.IsNullOrWhiteSpace(theme.Key))
            {
                HolidayOverlayRoot.IsVisible = false;
                _activeHolidayKey = string.Empty;
                StopHolidayTimers();
                return;
            }

            HolidayOverlayRoot.IsVisible = true;
            if (string.Equals(_activeHolidayKey, theme.Key, StringComparison.OrdinalIgnoreCase) && _holidayCrosserTimer != null)
            {
                return;
            }

            _activeHolidayKey = theme.Key;
            StopHolidayTimers();

            if (theme.FogOpacity > 0 && !string.IsNullOrWhiteSpace(theme.FogColor))
            {
                HolidayFogBox.IsVisible = true;
                HolidayFogBox.Color = Color.FromArgb(theme.FogColor);
                HolidayFogBox.Opacity = theme.FogOpacity;
            }
            else
            {
                HolidayFogBox.IsVisible = false;
            }

            if (Handler == null) return;

            if (theme.Crossers.Length > 0)
            {
                _holidayCrosserTimer = Dispatcher.CreateTimer();
                _holidayCrosserTimer.Interval = TimeSpan.FromSeconds(Math.Max(2.5, theme.CrossSeconds));
                _holidayCrosserTimer.Tick += (_, _) => SpawnHolidayCrosser(theme);
                _holidayCrosserTimer.Start();
                SpawnHolidayCrosser(theme);
            }

            if (theme.Ambient.Length > 0)
            {
                _holidayAmbientTimer = Dispatcher.CreateTimer();
                _holidayAmbientTimer.Interval = TimeSpan.FromSeconds(Math.Max(1.0, theme.AmbientSeconds));
                _holidayAmbientTimer.Tick += (_, _) => SpawnHolidayAmbient(theme);
                _holidayAmbientTimer.Start();
                SpawnHolidayAmbient(theme);
            }
        }

        private void StopHolidayTimers()
        {
            _holidayCrosserTimer?.Stop();
            _holidayCrosserTimer = null;
            _holidayAmbientTimer?.Stop();
            _holidayAmbientTimer = null;
            _activeSpriteCount = 0;
            HolidaySpriteCanvas?.Children.Clear();
        }

        private async void SpawnHolidayCrosser(HolidayTheme theme)
        {
            if (_activeSpriteCount >= MaxActiveHolidaySprites || HolidaySpriteCanvas == null || theme.Crossers.Length == 0)
            {
                return;
            }

            double width = Width > 100 ? Width : 1024;
            double height = Height > 100 ? Height : 640;

            _activeSpriteCount++;
            string glyph = theme.Crossers[Random.Shared.Next(theme.Crossers.Length)];
            string hex = theme.Palette.Length > 0 ? theme.Palette[Random.Shared.Next(theme.Palette.Length)] : "#FF6D00";

            var badge = CreateHolidaySpriteView(glyph, hex, 38);
            bool leftToRight = Random.Shared.Next(2) == 0;
            double startX = leftToRight ? -70 : width + 30;
            double endX = leftToRight ? width + 70 : -70;
            double startY = height * (0.10 + (Random.Shared.NextDouble() * 0.68));
            double deltaY = (Random.Shared.NextDouble() - 0.5) * 90;

            AbsoluteLayout.SetLayoutBounds(badge, new Rect(startX, startY, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            HolidaySpriteCanvas.Children.Add(badge);

            uint duration = (uint)Random.Shared.Next(7500, 12000);
            try
            {
                await badge.TranslateToAsync(endX - startX, deltaY, duration, Easing.SinInOut);
            }
            catch
            {
                // Ignore cancellation when view unloads
            }
            finally
            {
                HolidaySpriteCanvas?.Children.Remove(badge);
                _activeSpriteCount = Math.Max(0, _activeSpriteCount - 1);
            }
        }

        private async void SpawnHolidayAmbient(HolidayTheme theme)
        {
            if (_activeSpriteCount >= MaxActiveHolidaySprites || HolidaySpriteCanvas == null || theme.Ambient.Length == 0)
            {
                return;
            }

            double width = Width > 100 ? Width : 1024;
            double height = Height > 100 ? Height : 640;

            _activeSpriteCount++;
            string glyph = theme.Ambient[Random.Shared.Next(theme.Ambient.Length)];
            string hex = theme.Palette.Length > 0 ? theme.Palette[Random.Shared.Next(theme.Palette.Length)] : "#FF6D00";

            var badge = CreateHolidaySpriteView(glyph, hex, 28);
            double x = width * (0.05 + (Random.Shared.NextDouble() * 0.88));
            double startY = theme.AmbientMotion == HolidayMotion.Rise ? height + 40 : -50;

            AbsoluteLayout.SetLayoutBounds(badge, new Rect(x, startY, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            HolidaySpriteCanvas.Children.Add(badge);

            try
            {
                double dy = theme.AmbientMotion == HolidayMotion.Rise ? -(height + 100) : (height + 100);
                double dx = (Random.Shared.NextDouble() - 0.5) * 80;
                uint duration = (uint)Random.Shared.Next(6000, 9500);
                await badge.TranslateToAsync(dx, dy, duration, Easing.SinInOut);
            }
            catch
            {
                // Ignore cancellation when view unloads
            }
            finally
            {
                HolidaySpriteCanvas?.Children.Remove(badge);
                _activeSpriteCount = Math.Max(0, _activeSpriteCount - 1);
            }
        }

        private static Border CreateHolidaySpriteView(string glyph, string hexColor, double fontSize)
        {
            var baseColor = Color.FromArgb(hexColor);
            return new Border
            {
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
                BackgroundColor = baseColor.WithAlpha(0.26f),
                Stroke = baseColor.WithAlpha(0.65f),
                StrokeThickness = 1.5,
                Padding = new Thickness(8, 4),
                InputTransparent = true,
                Content = new Label
                {
                    Text = glyph,
                    FontSize = fontSize,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    InputTransparent = true
                }
            };
        }

        private void OnMauiScaryokeStateChanged(ScaryokeStateDto? state)
        {
            Dispatcher.Dispatch(async () =>
            {
                if (ScaryokeWheelOverlay == null || ProjectedWheelGraphicsView == null) return;

                if (state == null || !state.IsActive)
                {
                    ScaryokeWheelOverlay.IsVisible = false;
                    _wheelWasSpinning = false;
                    return;
                }

                ScaryokeWheelOverlay.IsVisible = true;
                if (ProjectedWheelSingerLabel != null)
                {
                    ProjectedWheelSingerLabel.Text = string.IsNullOrWhiteSpace(state.CurrentSingerName)
                        ? "Stage Open"
                        : $"Spinning for: {state.CurrentSingerName}";
                }
                if (ProjectedWheelResultLabel != null)
                {
                    ProjectedWheelResultLabel.Text = string.IsNullOrWhiteSpace(state.ResultText)
                        ? (state.IsSpinning ? "Spinning..." : "Spin the wheel!")
                        : state.ResultText;
                }

                if (state.Segments != null && state.Segments.Count > 0)
                {
                    _wheelDrawable.Segments = state.Segments
                        .Select(s => new WheelSegment
                        {
                            Name = s.Name,
                            Color = s.Color,
                            TextColor = s.TextColor,
                            Weight = s.Weight,
                            Sweep = s.Sweep
                        })
                        .ToList();
                    ProjectedWheelGraphicsView.Invalidate();
                }

                if (state.IsSpinning && (!_wheelWasSpinning || Math.Abs(_wheelLastTargetAngle - state.TargetAngle) > 0.01))
                {
                    _wheelWasSpinning = true;
                    _wheelLastTargetAngle = state.TargetAngle;
                    uint durationMs = (uint)Math.Max(1200, state.SpinDuration * 1000);
                    try
                    {
                        await ProjectedWheelGraphicsView.RotateToAsync(state.TargetAngle, durationMs, Easing.CubicOut);
                        _wheelCurrentAngle = state.TargetAngle % 360;
                        ProjectedWheelGraphicsView.Rotation = _wheelCurrentAngle;
                    }
                    catch
                    {
                        // Ignore animation interruption
                    }
                }
                else if (!state.IsSpinning)
                {
                    _wheelWasSpinning = false;
                    _wheelCurrentAngle = state.TargetAngle % 360;
                    ProjectedWheelGraphicsView.Rotation = _wheelCurrentAngle;
                }
            });
        }

        private void StartClock()
        {
            if (_clockTimer != null) return;
            _clockTimer = Dispatcher.CreateTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (_, _) => UpdateClock();
            _clockTimer.Start();
        }

        private void StopClock()
        {
            _clockTimer?.Stop();
            _clockTimer = null;
        }

        private void UpdateClock()
        {
            if (LiveClockLabel != null)
            {
                LiveClockLabel.Text = DateTime.Now.ToString("h:mm tt");
            }
        }
    }
}
