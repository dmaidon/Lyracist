// Last Edit: Jul 03, 2026 07:31 - Optimized split-flap transitions to flip directly to target chars to prevent typing lag while Flip Tile view is active.
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace KSRotation.Windows
{
    public partial class SplitFlapCharControl : System.Windows.Controls.UserControl
    {
        // Valid character set for the split flap simulation.
        private const string CharSet = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789★-&().!?+";

        private char _displayedChar = ' ';
        private char _targetChar = ' ';
        private readonly DispatcherTimer _timer;

        public static readonly DependencyProperty TargetCharProperty =
            DependencyProperty.Register(
                nameof(TargetChar),
                typeof(char),
                typeof(SplitFlapCharControl),
                new PropertyMetadata(' ', OnTargetCharChanged));

        public char TargetChar
        {
            get => (char)GetValue(TargetCharProperty);
            set => SetValue(TargetCharProperty, value);
        }

        public SplitFlapCharControl()
        {
            InitializeComponent();

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(75)
            };
            _timer.Tick += Timer_Tick;

            // Set initial state
            StaticTopText.Text = " ";
            StaticBottomText.Text = " ";
            TopFlapperText.Text = " ";
            BottomFlapperText.Text = " ";

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_displayedChar != _targetChar && !_timer.IsEnabled)
            {
                _timer.Start();
                StepTransition();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
        }

        private static void OnTargetCharChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SplitFlapCharControl control)
            {
                control.UpdateTargetChar();
            }
        }

        private void UpdateTargetChar()
        {
            char raw = char.ToUpperInvariant(TargetChar);
            char normalized = CharSet.Contains(raw.ToString(), StringComparison.Ordinal) ? raw : ' ';

            if (_targetChar == normalized) return;

            _targetChar = normalized;

            // Start cycling if loaded and not matching
            if (IsLoaded && _displayedChar != _targetChar && !_timer.IsEnabled)
            {
                _timer.Start();
                StepTransition();
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            StepTransition();
        }

        private void StepTransition()
        {
            if (_displayedChar == _targetChar)
            {
                _timer.Stop();
                return;
            }

            char nextChar = _targetChar;

            AnimateFlip(_displayedChar, nextChar);
            _displayedChar = nextChar;
            _timer.Stop();
        }

        private void AnimateFlip(char oldChar, char newChar)
        {
            // Set text values on elements
            StaticTopText.Text = newChar.ToString();
            StaticBottomText.Text = oldChar.ToString();
            TopFlapperText.Text = oldChar.ToString();
            BottomFlapperText.Text = newChar.ToString();

            // Create animations
            var topAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(30)
            };

            var bottomAnim = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(40),
                BeginTime = TimeSpan.FromMilliseconds(30)
            };

            // Subtle bounce to simulate a mechanical snap
            var ease = new BackEase
            {
                Amplitude = 0.3,
                EasingMode = EasingMode.EaseOut
            };
            bottomAnim.EasingFunction = ease;

            // Trigger animations
            TopFlapperScale.BeginAnimation(ScaleTransform.ScaleYProperty, topAnim);
            BottomFlapperScale.BeginAnimation(ScaleTransform.ScaleYProperty, bottomAnim);
        }
    }
}
