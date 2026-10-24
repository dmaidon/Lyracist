// Edited on Oct 24, 2026 @ 15:32:00 -> Fix nullable reference warnings in wheel label creation
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace ScaryokeWheel;

public partial class ScaryokeProjectionWindow : Window
{
    private const double WheelSize = 420;
    private const double Radius = WheelSize / 2;

    private readonly ScaryokeWheelViewModel _vm;
    private double _currentAngle;

    // Slice + label elements per segment, so the winner can be highlighted after a spin
    private readonly List<UIElement[]> _segmentVisuals = [];

    private static readonly Brush ResultNormalBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
    private static readonly Brush ResultDjsChoiceBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x3D, 0x00));

    public ScaryokeProjectionWindow(ScaryokeWheelViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        BuildWheel();

        _vm.WheelSegmentsRebuilt += OnWheelSegmentsRebuilt;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScaryokeWheelViewModel.ResultText))
        {
            if (string.Equals(_vm.ResultText, "Spinning...", StringComparison.OrdinalIgnoreCase))
            {
                ResultTextBlock.Foreground = ResultNormalBrush;
            }
        }
    }

    private void OnWheelSegmentsRebuilt(object? sender, EventArgs e)
    {
        WheelCanvas.Children.Clear();
        BuildWheel();
    }

    // ─── Wheel drawing ───────────────────────────────────────────────────

    private static Point Polar(double angleDegrees, double radius)
    {
        double rad = angleDegrees * Math.PI / 180.0;
        return new Point(Radius + (radius * Math.Sin(rad)), Radius - (radius * Math.Cos(rad)));
    }

    private static double LabelFontSize(string? text) => (text?.Length ?? 0) switch
    {
        <= 14 => 13,
        <= 20 => 11,
        _ => 9.5
    };

    private void BuildWheel()
    {
        _segmentVisuals.Clear();
        double start = 0;

        for (int i = 0; i < _vm.WheelSegments.Count; i++)
        {
            var segment = _vm.WheelSegments[i];
            double sweep = segment.Sweep;
            double end = start + sweep;

            // Pie slice
            Point p1 = Polar(start, Radius);
            Point p2 = Polar(end, Radius);
            bool largeArc = sweep > 180.0;

            var figure = new PathFigure
            {
                StartPoint = new Point(Radius, Radius),
                IsClosed = true,
                Segments =
                [
                    new LineSegment(p1, false),
                    new ArcSegment(p2, new Size(Radius, Radius), 0, largeArc, SweepDirection.Clockwise, false)
                ]
            };

            var geometry = new PathGeometry([figure]);
            var sliceColor = (Color)ColorConverter.ConvertFromString(segment.Color);
            bool isDjsChoice = string.Equals(segment.Name?.Trim(), "DJ's Choice", StringComparison.OrdinalIgnoreCase);

            var slice = new Path
            {
                Data = geometry,
                Fill = new SolidColorBrush(sliceColor),
                Stroke = new SolidColorBrush(isDjsChoice ? Color.FromRgb(0xFF, 0x57, 0x22) : Color.FromRgb(0x1E, 0x13, 0x3A)),
                StrokeThickness = isDjsChoice ? 2.5 : 1.5,
                Tag = segment
            };
            WheelCanvas.Children.Add(slice);

            // Centered text label along slice bisector
            double mid = start + (sweep / 2.0);
            Point labelPos = Polar(mid, Radius * 0.65);
            var textColor = (Color)ColorConverter.ConvertFromString(segment.TextColor);

            string labelText = segment.Name ?? string.Empty;
            if (isDjsChoice)
            {
                labelText = "DJ's Choice";
            }

            var label = new Border
            {
                Width = 140,
                Height = 44,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children =
                    [
                        new RotateTransform(mid - 90),
                        new TranslateTransform()
                    ]
                },
                Child = new TextBlock
                {
                    Text = labelText,
                    Foreground = new SolidColorBrush(textColor),
                    FontSize = isDjsChoice ? 14 : LabelFontSize(labelText),
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            Canvas.SetLeft(label, labelPos.X - (label.Width / 2));
            Canvas.SetTop(label, labelPos.Y - (label.Height / 2));
            WheelCanvas.Children.Add(label);

            _segmentVisuals.Add([slice, label]);
            start = end;
        }
    }

    private int SegmentIndexAtPointerAngle(double angle)
    {
        double start = 0;
        for (int i = 0; i < _vm.WheelSegments.Count; i++)
        {
            double end = start + _vm.WheelSegments[i].Sweep;
            if (angle >= start && angle < end)
            {
                return i;
            }
            start = end;
        }

        return 0;
    }

    // ─── Spin Synchronisation ─────────────────────────────────────────────

    /// <summary>
    /// Executes a synchronized spin animation matching the primary host window.
    /// </summary>
    public void SpinTo(double targetAngle, double durationSeconds)
    {
        var animation = new DoubleAnimation(_currentAngle, targetAngle, TimeSpan.FromSeconds(durationSeconds))
        {
            EasingFunction = new PowerEase { Power = 3.5, EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };

        animation.Completed += (_, _) =>
        {
            _currentAngle = targetAngle % 360;
            WheelRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            WheelRotate.Angle = _currentAngle;

            int landedIndex = SegmentIndexAtPointerAngle((360 - _currentAngle) % 360);
            HighlightWinner(landedIndex);

            if (_vm.WheelSegments.Count > landedIndex)
            {
                var landedSegment = _vm.WheelSegments[landedIndex];
                bool isDjsChoice = string.Equals(landedSegment.Name?.Trim(), "DJ's Choice", StringComparison.OrdinalIgnoreCase);
                if (isDjsChoice)
                {
                    PlayDjsChoiceReveal();
                }
            }
        };

        WheelRotate.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private void HighlightWinner(int winnerIndex)
    {
        for (int i = 0; i < _segmentVisuals.Count; i++)
        {
            foreach (var visual in _segmentVisuals[i])
            {
                if (i == winnerIndex)
                {
                    visual.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, 0.6, TimeSpan.FromMilliseconds(260))
                    {
                        AutoReverse = true,
                        RepeatBehavior = new RepeatBehavior(3)
                    });
                }
                else
                {
                    visual.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, 0.35, TimeSpan.FromMilliseconds(400))
                    {
                        FillBehavior = FillBehavior.HoldEnd
                    });
                }
            }
        }
    }

    private void PlayDjsChoiceReveal()
    {
        ResultTextBlock.Foreground = ResultDjsChoiceBrush;

        var flash = new DoubleAnimationUsingKeyFrames();
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80))));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.05, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260))));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000))));
        FlashOverlay.BeginAnimation(OpacityProperty, flash);

        var scale = (ScaleTransform)ResultScale;
        var pop = new DoubleAnimationUsingKeyFrames();
        pop.KeyFrames.Add(new SplineDoubleKeyFrame(1.3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160)), new KeySpline(0.1, 0.9, 0.2, 1.0)));
        pop.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(450))));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);

        var shake = (TranslateTransform)ResultShake;
        var vibrate = new DoubleAnimationUsingKeyFrames();
        for (int i = 0; i < 6; i++)
        {
            double offset = (i % 2 == 0 ? 1 : -1) * (6 - i);
            vibrate.KeyFrames.Add(new LinearDoubleKeyFrame(offset, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(40 * (i + 1)))));
        }
        vibrate.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280))));
        shake.BeginAnimation(TranslateTransform.XProperty, vibrate);
    }

    // ─── Window Controls ─────────────────────────────────────────────────

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CloseMenu_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleFullscreen_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.F11)
        {
            e.Handled = true;
            ToggleFullscreen_Click(sender, e);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleFullscreen_Click(sender, e);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _vm.WheelSegmentsRebuilt -= OnWheelSegmentsRebuilt;
        _vm.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
