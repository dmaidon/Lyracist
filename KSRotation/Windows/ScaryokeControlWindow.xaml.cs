// Created on Oct 26, 2026 @ 00:10:00 -> Code-behind for host Scaryoke wheel controller window
using KSRotation.Models;
using KSRotation.ViewModels;
using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace KSRotation.Windows
{
    public partial class ScaryokeControlWindow : Window
    {
        private const double Radius = 215;
        private readonly MainViewModel _vm;
        private double _currentAngle;

        public ScaryokeControlWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;

            BuildWheel();

            _vm.ScaryokeSpinStarted += OnSpinStarted;
            _vm.ScaryokeSpinFinished += OnSpinFinished;
            _vm.ScaryokeWheelSegments.CollectionChanged += OnSegmentsChanged;

            Closed += OnWindowClosed;
        }

        private void OnSegmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            BuildWheel();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _vm.ScaryokeSpinStarted -= OnSpinStarted;
            _vm.ScaryokeSpinFinished -= OnSpinFinished;
            _vm.ScaryokeWheelSegments.CollectionChanged -= OnSegmentsChanged;
        }

        private static Point PolarPoint(double angleDegrees, double radius)
        {
            double rad = angleDegrees * Math.PI / 180.0;
            return new Point(Radius + (radius * Math.Sin(rad)), Radius - (radius * Math.Cos(rad)));
        }

        private void BuildWheel()
        {
            HostWheelCanvas.Children.Clear();
            var segments = _vm.ScaryokeWheelSegments;
            if (segments == null || segments.Count == 0) return;

            var strokeBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x13, 0x3A));
            strokeBrush.Freeze();
            double start = 0;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                double sweep = seg.Sweep;
                double end = start + sweep;

                var figure = new PathFigure
                {
                    StartPoint = new Point(Radius, Radius),
                    IsClosed = true
                };
                figure.Segments.Add(new LineSegment(PolarPoint(start, Radius), true));
                figure.Segments.Add(new ArcSegment(
                    PolarPoint(end, Radius),
                    new Size(Radius, Radius),
                    0,
                    sweep > 180.0,
                    SweepDirection.Clockwise,
                    true));

                var sliceColor = (Color)ColorConverter.ConvertFromString(seg.Color);
                var slice = new Path
                {
                    Data = new PathGeometry(new[] { figure }),
                    Fill = new SolidColorBrush(sliceColor),
                    Stroke = strokeBrush,
                    StrokeThickness = 2
                };
                HostWheelCanvas.Children.Add(slice);

                // Label
                double mid = start + (sweep / 2);
                var labelPos = PolarPoint(mid, Radius * 0.62);
                double textAngle = mid - 90;
                if (mid > 180) textAngle += 180;

                var label = new Grid
                {
                    Width = 130,
                    Height = 24,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new RotateTransform(textAngle)
                };

                var textColor = (Color)ColorConverter.ConvertFromString(seg.TextColor);
                bool isDjsChoice = string.Equals(seg.Name, "DJ's Choice", StringComparison.OrdinalIgnoreCase);

                label.Children.Add(new TextBlock
                {
                    Text = isDjsChoice ? "💀" : seg.Name,
                    Foreground = new SolidColorBrush(textColor),
                    FontWeight = FontWeights.Bold,
                    FontSize = isDjsChoice ? 16 : 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                Canvas.SetLeft(label, labelPos.X - 65);
                Canvas.SetTop(label, labelPos.Y - 12);
                HostWheelCanvas.Children.Add(label);

                start = end;
            }
        }

        private void OnSpinStarted(object? sender, ScaryokeSpinEventArgs e)
        {
            double current = HostWheelRotate.Angle % 360;
            var animation = new DoubleAnimation(current, e.TargetAngle, TimeSpan.FromSeconds(e.Duration))
            {
                EasingFunction = new PowerEase { Power = 3.5, EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            HostWheelRotate.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        private void OnSpinFinished(object? sender, ScaryokeSpinCompletedEventArgs e)
        {
            _currentAngle = e.FinalAngle % 360;
            HostWheelRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            HostWheelRotate.Angle = _currentAngle;

            // Trigger result bounce animation
            var scaleAnim = new DoubleAnimation(0.75, 1.0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }
            };
            HostResultScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            HostResultScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }
    }
}
