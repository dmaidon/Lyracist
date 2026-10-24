// Edited on Oct 24, 2026 @ 14:00:00 -> Spin feel, pitched ticks, themed sounds, volume, winner pulse, true fullscreen, label fitting, saved window bounds
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ScaryokeWheel;

public partial class MainWindow : Window
{
    private const double WheelSize = 420;
    private const double Radius = WheelSize / 2;
    private const double MinWindowWidth = 700;
    private const double MinWindowHeight = 520;

    private readonly ScaryokeWheelViewModel _vm;
    private double _currentAngle;
    private bool _isSpinning;

    // Synthesized sounds are rebuilt whenever the volume changes. Ticks come in three pitches so
    // the click drops in tone as the wheel slows down.
    private readonly SoundPlayer?[] _tickPlayers = new SoundPlayer?[3];
    private SoundPlayer? _spinPlayer;
    private SoundPlayer? _resultPlayer;
    private readonly List<MemoryStream> _soundStreams = [];

    private int _lastTickIndex = -1;
    private long _lastTickTimestamp;
    private double _lastRawAngle;
    private long _lastRenderTimestamp;
    private System.Windows.Media.MediaPlayer? _laughMediaPlayer;

    // Slice + label elements per segment, so the winner can be highlighted after a spin
    private readonly List<UIElement[]> _segmentVisuals = [];

    private bool _isPresentationMode;
    private Rect _restoreBounds;
    private WindowState _restoreState;
    private bool _restoreTopmost;
    private ResizeMode _restoreResizeMode;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new ScaryokeWheelViewModel();
        DataContext = _vm;

        RestoreWindowBounds();
        BuildSounds();
        BuildWheel();

        // Redraw the wheel whenever the view model has repopulated its segments
        _vm.WheelSegmentsRebuilt += OnWheelSegmentsRebuilt;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScaryokeWheelViewModel.Volume))
        {
            BuildSounds();
            if (_laughMediaPlayer != null)
            {
                _laughMediaPlayer.Volume = _vm.Volume;
            }
        }
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

    // ─── Sounds ──────────────────────────────────────────────────────────

    private SoundPlayer? NewPlayer(MemoryStream stream)
    {
        _soundStreams.Add(stream);
        try
        {
            var player = new SoundPlayer(stream);
            player.Load();
            return player;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sound load failed: {ex.Message}");
            return null;
        }
    }

    private void BuildSounds()
    {
        foreach (var player in _tickPlayers)
        {
            player?.Dispose();
        }
        _spinPlayer?.Dispose();
        _resultPlayer?.Dispose();
        foreach (var stream in _soundStreams)
        {
            stream.Dispose();
        }
        _soundStreams.Clear();

        double volume = _vm.Volume;
        double[] pitches = [1.0, 0.75, 0.55];
        for (int i = 0; i < pitches.Length; i++)
        {
            _tickPlayers[i] = NewPlayer(ScaryokeAudio.CreateTickStream(pitches[i], volume));
        }
        _spinPlayer = NewPlayer(ScaryokeAudio.CreateSpinStartStream(volume));
        _resultPlayer = NewPlayer(ScaryokeAudio.CreateResultStream(volume));
    }

    private void PlaySound(SoundPlayer? player)
    {
        if (_vm.IsMuted || player == null) return;

        try
        {
            player.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sound play failed: {ex.Message}");
        }
    }

    // ─── Wheel drawing ───────────────────────────────────────────────────

    private static Point Polar(double angleDegrees, double radius)
    {
        double rad = angleDegrees * Math.PI / 180.0;
        return new Point(Radius + (radius * Math.Sin(rad)), Radius - (radius * Math.Cos(rad)));
    }

    /// <summary>Font size that keeps a category name inside its 140px label box.</summary>
    private static double LabelFontSize(string text) => text.Length switch
    {
        <= 14 => 13,
        <= 20 => 11,
        _ => 9.5
    };

    private void BuildWheel()
    {
        _segmentVisuals.Clear();

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
                Height = 30,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(textAngle)
            };

            var textColor = (Color)ColorConverter.ConvertFromString(segments[i].TextColor);
            bool isDjsChoice = string.Equals(segments[i].Name, "DJ's Choice", StringComparison.OrdinalIgnoreCase);
            string labelText = isDjsChoice ? "💀" : segments[i].Name;
            label.Children.Add(new TextBlock
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
            });

            Canvas.SetLeft(label, labelPos.X - (label.Width / 2));
            Canvas.SetTop(label, labelPos.Y - (label.Height / 2));
            WheelCanvas.Children.Add(label);

            _segmentVisuals.Add([slice, label]);
            start = end;
        }
    }

    /// <summary>Index of the segment covering the given angle, measured clockwise from the wheel's 12 o'clock start.</summary>
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

    private WheelSegment SegmentAtPointerAngle(double angle) => _vm.WheelSegments[SegmentIndexAtPointerAngle(angle)];

    /// <summary>Dims every segment except the winner and pulses the winner a few times.</summary>
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

    // ─── Spinning ────────────────────────────────────────────────────────

    private void OnRendering(object? sender, EventArgs e)
    {
        double raw = WheelRotate.Angle;
        long now = Environment.TickCount64;
        double angle = (360 - (raw % 360)) % 360;
        int currentTickIndex = SegmentIndexAtPointerAngle(angle);

        // Wheel speed in degrees per second, to choose the tick pitch
        double speed = 0;
        if (_lastRenderTimestamp != 0 && now > _lastRenderTimestamp)
        {
            speed = Math.Abs(raw - _lastRawAngle) * 1000.0 / (now - _lastRenderTimestamp);
        }
        _lastRawAngle = raw;
        _lastRenderTimestamp = now;

        if (_lastTickIndex == -1)
        {
            _lastTickIndex = currentTickIndex;
        }
        else if (currentTickIndex != _lastTickIndex)
        {
            if (now - _lastTickTimestamp >= 50)
            {
                int pitchIndex = speed > 900 ? 0 : speed > 300 ? 1 : 2;
                PlaySound(_tickPlayers[pitchIndex]);
                _lastTickTimestamp = now;
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
        _vm.ResultDetail = string.Empty;
        ResultTextBlock.Foreground = ResultNormalBrush;

        PlaySound(_spinPlayer);

        _lastTickIndex = -1;
        _lastTickTimestamp = 0;
        _lastRenderTimestamp = 0;
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

        // Varied length, with a long creeping tail so the last few ticks land one at a time
        double seconds = 4.0 + (Random.Shared.NextDouble() * 1.5);
        var animation = new DoubleAnimation(_currentAngle, target, TimeSpan.FromSeconds(seconds))
        {
            EasingFunction = new PowerEase { Power = 3.5, EasingMode = EasingMode.EaseOut },
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

            int landedIndex = SegmentIndexAtPointerAngle((360 - _currentAngle) % 360);
            WheelSegment landedSegment = _vm.WheelSegments[landedIndex];

            _vm.ShowResult(landedSegment.Name);
            _vm.AddHistory(landedSegment.Name);
            HighlightWinner(landedIndex);

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
                PlayLaugh();
            }
            else
            {
                PlaySound(_resultPlayer);
            }
        };

        WheelRotate.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private void PlayLaugh()
    {
        try
        {
            foreach (var player in _tickPlayers)
            {
                player?.Stop();
            }

            // Randomly select one of the 2 MP3 files in Assets folder
            string[] laughFiles = ["evil-laugh-deep.mp3", "evil-laugh-reverb.mp3"];
            string chosenFile = laughFiles[Random.Shared.Next(laughFiles.Length)];
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = System.IO.Path.Combine(appDir, "Assets", chosenFile);

            if (!File.Exists(filePath))
            {
                PlaySynthesizedLaughFallback();
                return;
            }

            _laughMediaPlayer?.Close();
            var laughPlayer = new System.Windows.Media.MediaPlayer { Volume = _vm.Volume };
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"be_afraid failed: {ex.Message}");
                }
            }

            laughPlayer.MediaEnded += endedHandler;
            laughPlayer.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Evil laugh failed: {ex}");
            PlaySynthesizedLaughFallback();
        }
    }

    private static readonly Brush ResultNormalBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
    private static readonly Brush ResultDjsChoiceBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x3D, 0x00));

    /// <summary>Red flash across the window, a shake and pop of the result text, and an orange-red result.</summary>
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

        // Pop: the result text swells to 1.4x and settles back
        var pop = new DoubleAnimationUsingKeyFrames();
        pop.KeyFrames.Add(new LinearDoubleKeyFrame(1.4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
        pop.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))));
        ResultScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        ResultScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    // ─── Presentation mode ───────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private const uint MonitorDefaultToNearest = 2;

    /// <summary>Full bounds (including the taskbar) of the monitor the window is on, in WPF units.</summary>
    private Rect CurrentMonitorBounds()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (handle != IntPtr.Zero && GetMonitorInfo(MonitorFromWindow(handle, MonitorDefaultToNearest), ref info))
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return new Rect(
                info.Monitor.Left / dpi.DpiScaleX,
                info.Monitor.Top / dpi.DpiScaleY,
                (info.Monitor.Right - info.Monitor.Left) / dpi.DpiScaleX,
                (info.Monitor.Bottom - info.Monitor.Top) / dpi.DpiScaleY);
        }

        return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
    }

    private void TogglePresentationMode()
    {
        _isPresentationMode = !_isPresentationMode;

        TitleBar.Visibility = _isPresentationMode ? Visibility.Collapsed : Visibility.Visible;
        SettingsTab.Visibility = _isPresentationMode ? Visibility.Collapsed : Visibility.Visible;
        OptionsPanel.Visibility = _isPresentationMode ? Visibility.Collapsed : Visibility.Visible;
        ResultTextBlock.FontSize = _isPresentationMode ? 56 : 26;

        if (_isPresentationMode)
        {
            _restoreState = WindowState;
            _restoreTopmost = Topmost;
            _restoreResizeMode = ResizeMode;
            _restoreBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;

            // Cover the whole monitor, taskbar included, rather than just its work area
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.NoResize;
            var bounds = CurrentMonitorBounds();
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            Topmost = true;
        }
        else
        {
            Topmost = _restoreTopmost;
            ResizeMode = _restoreResizeMode;
            Left = _restoreBounds.Left;
            Top = _restoreBounds.Top;
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;
            WindowState = _restoreState;
        }
    }

    // ─── Window placement ────────────────────────────────────────────────

    private void RestoreWindowBounds()
    {
        MinWidth = MinWindowWidth;
        MinHeight = MinWindowHeight;

        var s = _vm.Settings;
        if (s.WindowLeft is not double left || s.WindowTop is not double top ||
            s.WindowWidth is not double width || s.WindowHeight is not double height)
        {
            return;
        }

        width = Math.Max(width, MinWindowWidth);
        height = Math.Max(height, MinWindowHeight);

        // Ignore a saved position that is no longer on any screen (e.g. a disconnected monitor)
        var saved = new Rect(left, top, width, height);
        var screens = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var visible = Rect.Intersect(saved, screens);
        if (visible.IsEmpty || visible.Width < 120 || visible.Height < 60)
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        CompositionTarget.Rendering -= OnRendering;

        // Leave presentation mode and maximized states out of the saved placement
        var bounds = _isPresentationMode ? _restoreBounds : (WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds);
        if (!bounds.IsEmpty)
        {
            var s = _vm.Settings;
            s.WindowLeft = bounds.Left;
            s.WindowTop = bounds.Top;
            s.WindowWidth = bounds.Width;
            s.WindowHeight = bounds.Height;
            s.Save();
        }

        _laughMediaPlayer?.Close();
        _laughMediaPlayer = null;
        foreach (var player in _tickPlayers)
        {
            player?.Dispose();
        }
        _spinPlayer?.Dispose();
        _resultPlayer?.Dispose();
        foreach (var stream in _soundStreams)
        {
            stream.Dispose();
        }
        _soundStreams.Clear();
    }

    // ─── Input ───────────────────────────────────────────────────────────

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
        double volume = _vm.Volume;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var laughStream = ScaryokeAudio.CreateEvilLaughStream(volume);
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
