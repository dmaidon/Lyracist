using System;
using System.Collections.Specialized;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ScaryokeWheel;

public partial class MainWindow : Window
{
    private const double WheelSize = 420;
    private const double Radius = WheelSize / 2;

    private readonly ScaryokeWheelViewModel _vm;
    private double _currentAngle;
    private bool _isSpinning;

    // Pre-loaded synthesized WAV streams and players
    private static readonly MemoryStream TickStream = ScaryokeAudio.CreateTickStream();
    private static readonly SoundPlayer TickPlayer = new(TickStream);

    private static readonly MemoryStream LaughStream = ScaryokeAudio.CreateEvilLaughStream();
    private static readonly SoundPlayer LaughPlayer = new(LaughStream);

    private int _lastTickIndex = -1;
    private System.Windows.Media.MediaPlayer? _laughMediaPlayer;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new ScaryokeWheelViewModel();
        DataContext = _vm;

        BuildWheel();

        // Rebuild wheel dynamically when categories change
        _vm.CustomCategories.CollectionChanged += OnCategoriesChanged;
    }

    private void OnCategoriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildWheel();
    }

    private void RebuildWheel()
    {
        WheelCanvas.Children.Clear();
        BuildWheel();
    }

    private static Point Polar(double angleDegrees, double radius)
    {
        double rad = angleDegrees * Math.PI / 180.0;
        return new Point(Radius + radius * Math.Sin(rad), Radius - radius * Math.Cos(rad));
    }

    private void BuildWheel()
    {
        var segments = _vm.WheelSegments;
        if (segments == null || segments.Count == 0) return;

        var strokeBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x18, 0x22));
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

            // Labels
            double mid = start + sweep / 2;
            var labelPos = Polar(mid, Radius * 0.62);
            double textAngle = mid - 90;
            if (mid > 180) textAngle += 180;

            var label = new Grid
            {
                Width = 140,
                Height = 26,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(textAngle)
            };
            
            var textColor = (Color)ColorConverter.ConvertFromString(segments[i].TextColor);
            bool isSingersChoice = string.Equals(segments[i].Name, "Singer's Choice", StringComparison.OrdinalIgnoreCase);
            label.Children.Add(new TextBlock
            {
                Text = isSingersChoice ? "🎤" : segments[i].Name,
                Foreground = new SolidColorBrush(textColor),
                FontSize = isSingersChoice ? 14 : 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            Canvas.SetLeft(label, labelPos.X - label.Width / 2);
            Canvas.SetTop(label, labelPos.Y - label.Height / 2);
            WheelCanvas.Children.Add(label);

            start = end;
        }
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
            try
            {
                TickStream.Position = 0;
                TickPlayer.Play();
            }
            catch { /* Best-effort */ }
            _lastTickIndex = currentTickIndex;
        }
    }

    private void OnSpinClick(object sender, RoutedEventArgs e)
    {
        if (_isSpinning || _vm.WheelSegments.Count == 0) return;

        _isSpinning = true;
        _vm.IsSpinning = true;
        SpinButton.IsEnabled = false;
        _vm.ResultText = "Spinning...";

        try
        {
            SystemSounds.Asterisk.Play();
        }
        catch { }

        _lastTickIndex = -1;
        CompositionTarget.Rendering += OnRendering;

        // Rebuild segments on start spin to place "DJ's Choice" in a fresh random index
        _vm.RebuildWheelSegments();
        RebuildWheel();

        // 4-6 full turns plus a random landing angle
        double target = _currentAngle + 1440 + Random.Shared.NextDouble() * 720;
        var animation = new DoubleAnimation(_currentAngle, target, TimeSpan.FromSeconds(4.2))
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
            _vm.IsSpinning = false;
            SpinButton.IsEnabled = true;

            double targetAngle = (360 - _currentAngle) % 360;
            WheelSegment landedSegment = _vm.WheelSegments[0];
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

            _vm.ResultText = landedSegment.Name.ToUpper();

            if (string.Equals(landedSegment.Name?.Trim(), "DJ's Choice", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    TickPlayer.Stop();

                    // Randomly select one of the 2 MP3 files in Assets folder
                    string[] laughFiles = { "evil-laugh-deep.mp3", "evil-laugh-reverb.mp3" };
                    string chosenFile = laughFiles[Random.Shared.Next(laughFiles.Length)];
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string filePath = System.IO.Path.Combine(appDir, "Assets", chosenFile);

                    if (File.Exists(filePath))
                    {
                        try
                        {
                            _laughMediaPlayer?.Close();
                            _laughMediaPlayer = new System.Windows.Media.MediaPlayer();
                            _laughMediaPlayer.Open(new Uri(filePath));
                            _laughMediaPlayer.Play();
                        }
                        catch
                        {
                            PlaySynthesizedLaughFallback();
                        }
                    }
                    else
                    {
                        PlaySynthesizedLaughFallback();
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to play evil laugh: {ex.Message}\n{ex.StackTrace}", "Sound Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            else
            {
                try
                {
                    SystemSounds.Exclamation.Play();
                }
                catch { }
            }
        };

        WheelRotate.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private void PlaySynthesizedLaughFallback()
    {
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using (var laughStream = ScaryokeAudio.CreateEvilLaughStream())
                using (var player = new SoundPlayer(laughStream))
                {
                    player.PlaySync();
                }
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show($"Failed to play fallback evil laugh: {ex.Message}", "Sound Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                });
            }
        });
    }

    // Window controls
    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException) { }
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}