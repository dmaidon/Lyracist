// Edited on Aug 25, 2026 @ 06:15:00 -> Fix RCS1123 arithmetic parentheses
using System;
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

    private int _lastTickIndex = -1;
    private long _lastTickTimestamp;
    private System.Windows.Media.MediaPlayer? _laughMediaPlayer;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new ScaryokeWheelViewModel();
        DataContext = _vm;

        BuildWheel();

        // Redraw the wheel whenever the view model has repopulated its segments
        _vm.WheelSegmentsRebuilt += OnWheelSegmentsRebuilt;

        try
        {
            TickPlayer.Load();
        }
        catch { }
    }

    private void OnWheelSegmentsRebuilt(object? sender, EventArgs e)
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
        return new Point(Radius + (radius * Math.Sin(rad)), Radius - (radius * Math.Cos(rad)));
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
            double mid = start + (sweep / 2);
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
            bool isDjsChoice = string.Equals(segments[i].Name, "DJ's Choice", StringComparison.OrdinalIgnoreCase);
            label.Children.Add(new TextBlock
            {
                Text = isDjsChoice ? "💀" : segments[i].Name,
                Foreground = new SolidColorBrush(textColor),
                FontSize = isDjsChoice ? 14 : 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            Canvas.SetLeft(label, labelPos.X - (label.Width / 2));
            Canvas.SetTop(label, labelPos.Y - (label.Height / 2));
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
            long now = Environment.TickCount64;
            if (!_vm.IsMuted && now - _lastTickTimestamp >= 50)
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

    private void OnSpinClick(object sender, RoutedEventArgs e)
    {
        if (_isSpinning || _vm.WheelSegments.Count == 0) return;

        _isSpinning = true;
        _vm.IsSpinning = true;
        SpinButton.IsEnabled = false;
        _vm.ResultText = "Spinning...";
        ResultTextBlock.Foreground = ResultNormalBrush;

        if (!_vm.IsMuted)
        {
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        _lastTickIndex = -1;
        _lastTickTimestamp = 0;
        CompositionTarget.Rendering += OnRendering;

        // Rebuild segments on start spin to place "DJ's Choice" in a fresh random index
        _vm.RebuildWheelSegments();

        // Pick a landing point uniformly on the wheel (so slice size sets the odds), re-rolling
        // while it falls on a recently landed category. Falls back to the last roll if unlucky.
        double landing = 0;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            landing = Random.Shared.NextDouble() * 360;
            if (!_vm.IsRecentRepeat(SegmentAtPointerAngle(landing).Name)) break;
        }

        // Rotation that puts that point under the pointer, after 4-5 full turns
        double desired = (360 - landing) % 360;
        double baseAngle = _currentAngle + 1440 + (Random.Shared.Next(0, 2) * 360);
        double target = baseAngle + (((desired - (baseAngle % 360)) + 360) % 360);
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

            WheelSegment landedSegment = SegmentAtPointerAngle((360 - _currentAngle) % 360);

            _vm.ResultText = landedSegment.Name.ToUpper();
            _vm.AddHistory(landedSegment.Name);

            bool isDjsChoiceResult = string.Equals(landedSegment.Name?.Trim(), "DJ's Choice", StringComparison.OrdinalIgnoreCase);
            if (isDjsChoiceResult)
            {
                PlayDjsChoiceReveal();
            }

            if (_vm.IsMuted)
            {
                return;
            }

            if (isDjsChoiceResult)
            {
                try
                {
                    TickPlayer.Stop();

                    // Randomly select one of the 2 MP3 files in Assets folder
                    string[] laughFiles = ["evil-laugh-deep.mp3", "evil-laugh-reverb.mp3"];
                    string chosenFile = laughFiles[Random.Shared.Next(laughFiles.Length)];
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string filePath = System.IO.Path.Combine(appDir, "Assets", chosenFile);

                    if (File.Exists(filePath))
                    {
                        try
                        {
                            _laughMediaPlayer?.Close();
                            var laughPlayer = new System.Windows.Media.MediaPlayer();
                            _laughMediaPlayer = laughPlayer;
                            bool laughFailed = false;

                            // Unplayable file: Open() doesn't throw, it raises MediaFailed
                            laughPlayer.MediaFailed += (_, _) =>
                            {
                                if (laughFailed) return;
                                laughFailed = true;
                                if (ReferenceEquals(_laughMediaPlayer, laughPlayer))
                                {
                                    PlaySynthesizedLaughFallback();
                                }
                            };

                            laughPlayer.Open(new Uri(filePath));

                            // Chain play "be_afraid.mp3" immediately after the evil laugh completes
                            void endedHandler(object? s, EventArgs ev)
                            {
                                laughPlayer.MediaEnded -= endedHandler;
                                try
                                {
                                    string nextFile = System.IO.Path.Combine(appDir, "Assets", "be_afraid.mp3");
                                    if (File.Exists(nextFile) && ReferenceEquals(_laughMediaPlayer, laughPlayer))
                                    {
                                        laughPlayer.Open(new Uri(nextFile));
                                        laughPlayer.Play();
                                    }
                                }
                                catch { }
                            }

                            laughPlayer.MediaEnded += endedHandler;

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
                    System.Diagnostics.Debug.WriteLine($"Evil laugh failed: {ex}");
                    PlaySynthesizedLaughFallback();
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

    private static readonly Brush ResultNormalBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
    private static readonly Brush ResultDjsChoiceBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x3D, 0x00));

    private bool _isPresentationMode;

    /// <summary>Red flash across the window, a shake of the result text and an orange-red result.</summary>
    private void PlayDjsChoiceReveal()
    {
        ResultTextBlock.Foreground = ResultDjsChoiceBrush;

        var flash = new DoubleAnimationUsingKeyFrames();
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80))));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.05, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260))));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000))));
        FlashOverlay.BeginAnimation(OpacityProperty, flash);

        var shake = new DoubleAnimationUsingKeyFrames();
        double[] offsets = [-12, 12, -9, 9, -5, 5, 0];
        for (int i = 0; i < offsets.Length; i++)
        {
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(offsets[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds((i + 1) * 70))));
        }

        ResultShake.BeginAnimation(TranslateTransform.XProperty, shake);
    }

    private void TogglePresentationMode()
    {
        _isPresentationMode = !_isPresentationMode;

        TitleBar.Visibility = _isPresentationMode ? Visibility.Collapsed : Visibility.Visible;
        SettingsTab.Visibility = _isPresentationMode ? Visibility.Collapsed : Visibility.Visible;
        OptionsPanel.Visibility = _isPresentationMode ? Visibility.Collapsed : Visibility.Visible;
        ResultTextBlock.FontSize = _isPresentationMode ? 56 : 26;
        WindowState = _isPresentationMode ? WindowState.Maximized : WindowState.Normal;
    }

    /// <summary>Segment covering the given angle, measured clockwise from the wheel's 12 o'clock start.</summary>
    private WheelSegment SegmentAtPointerAngle(double angle)
    {
        double start = 0;
        foreach (var segment in _vm.WheelSegments)
        {
            double end = start + segment.Sweep;
            if (angle >= start && angle < end)
            {
                return segment;
            }
            start = end;
        }

        return _vm.WheelSegments[0];
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // F11 toggles presentation mode; Esc leaves it
        if (e.Key == Key.F11 || (e.Key == Key.Escape && _isPresentationMode))
        {
            e.Handled = true;
            TogglePresentationMode();
            return;
        }

        // Space spins from the wheel tab, unless the user is typing or a button already handles it
        if (e.Key != Key.Space || !SpinButton.IsVisible) return;
        if (e.OriginalSource is System.Windows.Controls.Primitives.TextBoxBase or System.Windows.Controls.Primitives.ButtonBase) return;

        e.Handled = true;
        OnSpinClick(SpinButton, new RoutedEventArgs());
    }

    private void PlaySynthesizedLaughFallback()
    {
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var laughStream = ScaryokeAudio.CreateEvilLaughStream();
                using var player = new SoundPlayer(laughStream);
                player.PlaySync();
            }
            catch (Exception ex)
            {
                // Audio is best-effort; never interrupt the show with a dialog
                System.Diagnostics.Debug.WriteLine($"Fallback evil laugh failed: {ex.Message}");
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