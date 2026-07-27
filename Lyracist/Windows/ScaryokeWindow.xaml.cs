// Edited on Jul 27, 2026 @ 13:35:00 -> Add tick sound buffering and throttling to prevent laptop audio driver choke
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Lyracist.ViewModels;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;

namespace Lyracist.Windows;

public partial class ScaryokeWindow : Window
{
    private const double WheelSize = 420;
    private const double Radius = WheelSize / 2;
    private double SegmentSweep => 360.0 / ScaryokeViewModel.WheelCategories.Length;

    private static readonly string[] SegmentColors =
    [
        "#6A0DAD", "#FF6D00", "#00838F", "#C2185B", "#4527A0", "#EF6C00",
        "#00695C", "#AD1457", "#5E35B1", "#F57C00", "#00796B", "#D81B60"
    ];

    private readonly ScaryokeViewModel _vm;
    private double _currentAngle;
    private bool _isSpinning;

    private static readonly System.IO.MemoryStream TickStream = CreateTickStream();
    private static readonly System.Media.SoundPlayer TickPlayer = new(TickStream);
    private int _lastTickIndex = -1;
    private long _lastTickTimestamp;

    private static System.IO.MemoryStream CreateTickStream()
    {
        var ms = new System.IO.MemoryStream();
        using (var writer = new System.IO.BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            writer.Write("RIFF".ToCharArray());
            writer.Write(0); // Placeholder
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // Mono
            writer.Write(11025); // Sample rate
            writer.Write(11025 * 2); // Byte rate
            writer.Write((short)2); // Block align
            writer.Write((short)16); // Bits per sample
            writer.Write("data".ToCharArray());
            writer.Write(0); // Placeholder

            // Ticking sound: a very short click wave
            int sampleCount = 120; // ~10 ms
            for (int i = 0; i < sampleCount; i++)
            {
                double fade = (double)(sampleCount - i) / sampleCount;
                short value = (short)(Math.Sin(i * 1.8) * fade * 16000);
                writer.Write(value);
            }

            long endPos = ms.Position;
            ms.Position = 4;
            writer.Write((int)(endPos - 8));
            ms.Position = 40;
            writer.Write((int)(sampleCount * 2));
            ms.Position = endPos;
        }
        ms.Position = 0;
        return ms;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        double angle = (360 - (WheelRotate.Angle % 360)) % 360;
        int currentTickIndex = 0;
        double currentStart = 0;

        for (int i = 0; i < _vm.WheelSegments.Count; i++)
        {
            double currentEnd = currentStart + _vm.WheelSegments[i].Sweep;
            if (angle >= currentStart && angle < currentEnd)
            {
                currentTickIndex = i;
                break;
            }
            currentStart = currentEnd;
        }

        if (_lastTickIndex == -1)
        {
            _lastTickIndex = currentTickIndex;
        }
        else if (currentTickIndex != _lastTickIndex)
        {
            long now = Environment.TickCount64;
            if (now - _lastTickTimestamp >= 50)
            {
                try
                {
                    TickPlayer.Play();
                    _lastTickTimestamp = now;
                }
                catch { /* Best-effort */ }
            }
            _lastTickIndex = currentTickIndex;
        }
    }

    /// <summary>Sound/effect hooks for the spin lifecycle.</summary>
    public event EventHandler? SpinStarted;
    public event EventHandler<string>? SpinCompleted;

    public ScaryokeViewModel ViewModel => _vm;
    private System.Windows.Media.MediaPlayer? _laughMediaPlayer;

    public ScaryokeWindow(ScaryokeViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.RebuildWheelSegments();
        BuildWheel();

        try
        {
            TickPlayer.Load();
        }
        catch { }
    }

    public void RebuildWheel()
    {
        _vm.RebuildWheelSegments();
        WheelCanvas.Children.Clear();
        BuildWheel();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Registered as a singleton: hide instead of destroying the window.
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    private void OnHide(object sender, RoutedEventArgs e) => Hide();

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch (InvalidOperationException) { }
        }
    }

    // Polar helper: angle measured clockwise from 12 o'clock, wheel-local coords.
    private static Point Polar(double angleDegrees, double radius)
    {
        double rad = angleDegrees * Math.PI / 180.0;
        return new Point(Radius + radius * Math.Sin(rad), Radius - radius * Math.Cos(rad));
    }

    private void BuildWheel()
    {
        var segments = _vm.WheelSegments;
        if (segments == null || segments.Count == 0) return;
        var strokeBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x13, 0x3A));
        double start = 0;

        for (int i = 0; i < segments.Count; i++)
        {
            double sweep = segments[i].Sweep;
            double end = start + sweep;

            var figure = new PathFigure { StartPoint = new Point(Radius, Radius), IsClosed = true };
            figure.Segments.Add(new LineSegment(Polar(start, Radius), true));
            figure.Segments.Add(new ArcSegment(Polar(end, Radius), new Size(Radius, Radius), 0,
                false, SweepDirection.Clockwise, true));

            var sliceColor = (Color)ColorConverter.ConvertFromString(segments[i].Color);
            var slice = new System.Windows.Shapes.Path
            {
                Data = new PathGeometry(new[] { figure }),
                Fill = new SolidColorBrush(sliceColor),
                Stroke = strokeBrush,
                StrokeThickness = 2
            };
            WheelCanvas.Children.Add(slice);

            // Label
            double mid = start + sweep / 2;
            var labelPos = Polar(mid, Radius * 0.62);
            double textAngle = mid - 90;
            if (mid > 180) textAngle += 180;

            var label = new Grid
            {
                Width = 150,
                Height = 26,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(textAngle)
            };

            var textColor = (Color)ColorConverter.ConvertFromString(segments[i].TextColor);
            bool isDjsChoice = string.Equals(segments[i].Name, "DJ's Choice", StringComparison.OrdinalIgnoreCase);
            label.Children.Add(new TextBlock
            {
                Text = isDjsChoice ? "💀" : segments[i].Name,
                Foreground = new SolidColorBrush(textColor),
                FontSize = isDjsChoice ? 16 : 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            Canvas.SetLeft(label, labelPos.X - label.Width / 2);
            Canvas.SetTop(label, labelPos.Y - label.Height / 2);
            WheelCanvas.Children.Add(label);

            start = end;
        }
    }

    private void OnSpinClick(object sender, RoutedEventArgs e) => Spin();

    public double TargetAngle { get; private set; }
    public double SpinDuration { get; private set; }

    public void Spin(double? forceTarget = null)
    {
        if (_isSpinning) return;
        _isSpinning = true;
        SpinButton.IsEnabled = false;

        _lastTickIndex = -1;
        _lastTickTimestamp = 0;
        CompositionTarget.Rendering += OnRendering;

        // Rebuild segments on start spin to place "DJ's Choice" in a fresh random index
        RebuildWheel();

        // 4-7 full turns plus a random landing offset, easing to a stop.
        double target = forceTarget ?? (_currentAngle + 1440 + Random.Shared.NextDouble() * 1080);
        TargetAngle = target;

        // Calculate dynamic duration based on rotation angle (harder spin = more rotations = longer duration)
        double totalDegrees = target - _currentAngle;
        double durationSec = 3.2 + (totalDegrees / 360.0) * 0.4;
        SpinDuration = durationSec;

        SpinStarted?.Invoke(this, EventArgs.Empty);
        System.Media.SystemSounds.Asterisk.Play();

        var animation = new DoubleAnimation(_currentAngle, target, TimeSpan.FromSeconds(SpinDuration))
        {
            DecelerationRatio = 0.9,
            FillBehavior = FillBehavior.HoldEnd
        };

        animation.Completed += (_, _) =>
        {
            CompositionTarget.Rendering -= OnRendering;

            _currentAngle = target % 360;
            WheelRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            WheelRotate.Angle = _currentAngle;

            _isSpinning = false;
            SpinButton.IsEnabled = true;

            // Determine landed segment using non-uniform sweeps
            double targetAngle = (360 - _currentAngle) % 360;
            Lyracist.ViewModels.WheelSegment landedSegment = _vm.WheelSegments[0];
            double currentStart = 0;
            for (int i = 0; i < _vm.WheelSegments.Count; i++)
            {
                double currentEnd = currentStart + _vm.WheelSegments[i].Sweep;
                if (targetAngle >= currentStart && targetAngle < currentEnd)
                {
                    landedSegment = _vm.WheelSegments[i];
                    break;
                }
                currentStart = currentEnd;
            }
            string category = landedSegment.Name;

            // Play custom laughter if landing on DJ's Choice
            if (string.Equals(category, "DJ's Choice", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string[] laughFiles = ["evil-laugh-deep.mp3", "evil-laugh-reverb.mp3"];
                    string chosenFile = laughFiles[Random.Shared.Next(laughFiles.Length)];
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string filePath = System.IO.Path.Combine(appDir, "Assets", chosenFile);

                    if (System.IO.File.Exists(filePath))
                    {
                        _laughMediaPlayer?.Close();
                        _laughMediaPlayer = new System.Windows.Media.MediaPlayer();
                        _laughMediaPlayer.Open(new Uri(filePath));

                        void endedHandler(object? s, EventArgs ev)
                        {
                            if (_laughMediaPlayer != null)
                            {
                                _laughMediaPlayer.MediaEnded -= endedHandler;
                                try
                                {
                                    string nextFile = System.IO.Path.Combine(appDir, "Assets", "be_afraid.mp3");
                                    if (System.IO.File.Exists(nextFile))
                                    {
                                        _laughMediaPlayer.Open(new Uri(nextFile));
                                        _laughMediaPlayer.Play();
                                    }
                                }
                                catch { }
                            }
                        }

                        _laughMediaPlayer.MediaEnded += endedHandler;
                        _laughMediaPlayer.Play();
                    }
                }
                catch { }
            }
            else
            {
                System.Media.SystemSounds.Exclamation.Play();
            }

            SpinCompleted?.Invoke(this, category);

            if (_vm.ApplyResult(category))
            {
                // Landed on "Spin Again" -- give the crowd a beat, then respin.
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                timer.Tick += (_, _) => { timer.Stop(); Spin(); };
                timer.Start();
            }
        };

        WheelRotate.BeginAnimation(RotateTransform.AngleProperty, animation);
    }
}
