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
    {
        "#6A0DAD", "#FF6D00", "#00838F", "#C2185B", "#4527A0", "#EF6C00",
        "#00695C", "#AD1457", "#5E35B1", "#F57C00", "#00796B", "#D81B60"
    };

    private readonly ScaryokeViewModel _vm;
    private double _currentAngle;
    private bool _isSpinning;

    private static readonly System.IO.MemoryStream TickStream = CreateTickStream();
    private static readonly System.Media.SoundPlayer TickPlayer = new(TickStream);
    private int _lastTickIndex = -1;

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
        double angle = WheelRotate.Angle;
        int currentTickIndex = (int)Math.Floor(angle / SegmentSweep);

        if (_lastTickIndex == -1)
        {
            _lastTickIndex = currentTickIndex;
        }
        else if (currentTickIndex != _lastTickIndex)
        {
            try
            {
                TickStream.Position = 0;
                TickPlayer.Play();
            }
            catch { /* Best-effort */ }
            _lastTickIndex = currentTickIndex;
        }
    }

    /// <summary>Sound/effect hooks for the spin lifecycle.</summary>
    public event EventHandler? SpinStarted;
    public event EventHandler<string>? SpinCompleted;

    public ScaryokeWindow(ScaryokeViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        BuildWheel();
    }

    public void RebuildWheel()
    {
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
        var categories = ScaryokeViewModel.WheelCategories;
        var strokeBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x13, 0x3A));

        for (int i = 0; i < categories.Length; i++)
        {
            double start = i * SegmentSweep;
            double end = start + SegmentSweep;

            var figure = new PathFigure { StartPoint = new Point(Radius, Radius), IsClosed = true };
            figure.Segments.Add(new LineSegment(Polar(start, Radius), true));
            figure.Segments.Add(new ArcSegment(Polar(end, Radius), new Size(Radius, Radius), 0,
                false, SweepDirection.Clockwise, true));

            var slice = new Path
            {
                Data = new PathGeometry(new[] { figure }),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SegmentColors[i % SegmentColors.Length])),
                Stroke = strokeBrush,
                StrokeThickness = 2
            };
            WheelCanvas.Children.Add(slice);

            // Label reads along the radius; flipped on the left half so it
            // isn't upside down.
            double mid = start + SegmentSweep / 2;
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
            label.Children.Add(new TextBlock
            {
                Text = categories[i],
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            Canvas.SetLeft(label, labelPos.X - label.Width / 2);
            Canvas.SetTop(label, labelPos.Y - label.Height / 2);
            WheelCanvas.Children.Add(label);
        }
    }

    private void OnSpinClick(object sender, RoutedEventArgs e) => Spin();

    private void Spin()
    {
        if (_isSpinning) return;
        _isSpinning = true;
        SpinButton.IsEnabled = false;

        SpinStarted?.Invoke(this, EventArgs.Empty);
        System.Media.SystemSounds.Asterisk.Play();

        _lastTickIndex = -1;
        CompositionTarget.Rendering += OnRendering;

        // 4-6 full turns plus a random landing offset, easing to a stop.
        double target = _currentAngle + 1440 + Random.Shared.NextDouble() * 720;
        var animation = new DoubleAnimation(_currentAngle, target, TimeSpan.FromSeconds(4.5))
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

            int index = (int)(((360 - _currentAngle) % 360) / SegmentSweep) % ScaryokeViewModel.WheelCategories.Length;
            string category = ScaryokeViewModel.WheelCategories[index];

            System.Media.SystemSounds.Exclamation.Play();
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
