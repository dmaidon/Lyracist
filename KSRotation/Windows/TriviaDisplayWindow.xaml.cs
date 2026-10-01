// Edited on Oct 1, 2026 @ 07:22:00 -> Fix #8 dynamic full-width marquee score ticker to prevent pop-in and truncation
using System;
using System.Windows;
using System.Windows.Controls;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace KSRotation.Windows
{
    public partial class TriviaDisplayWindow : Window
    {
        public TriviaDisplayWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => RestartMarqueeAnimation();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleFullscreen();
            }
            else if (WindowState == WindowState.Normal)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CloseMenu_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleFullscreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullscreen();
        }

        private void ToggleFullscreen()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                Width = 1280;
                Height = 720;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                ResizeMode = ResizeMode.NoResize;
            }
        }

        private string? _lastMarqueeText;

        private void MarqueeBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RestartMarqueeAnimation();
        }

        private void MarqueeText_TargetUpdated(object? sender, System.Windows.Data.DataTransferEventArgs e)
        {
            if (MarqueeText != null && MarqueeText.Text != _lastMarqueeText)
            {
                _lastMarqueeText = MarqueeText.Text;
                RestartMarqueeAnimation();
            }
        }

        private void RestartMarqueeAnimation()
        {
            if (MarqueeBorder == null || MarqueeText == null) return;
            double containerWidth = MarqueeBorder.ActualWidth;
            if (containerWidth <= 0) return;

            MarqueeText.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            double textWidth = MarqueeText.DesiredSize.Width;
            if (textWidth <= 0) return;

            double speed = 65.0; // pixels per second constant velocity
            var anim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = containerWidth,
                To = -textWidth,
                Duration = TimeSpan.FromSeconds(Math.Max(5.0, (containerWidth + textWidth) / speed)),
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };

            MarqueeText.BeginAnimation(Canvas.LeftProperty, null);
            MarqueeText.BeginAnimation(Canvas.LeftProperty, anim);
        }
    }
}
