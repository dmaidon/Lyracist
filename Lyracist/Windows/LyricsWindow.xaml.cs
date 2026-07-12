using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lyracist.ViewModels;

namespace Lyracist.Windows;

public partial class LyricsWindow : Window
{
    private readonly LyricsWindowViewModel _vm;

    public LyricsWindow(LyricsWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        Lyracist.Services.Tablet.LyricsHub.ReactionReceived += OnReactionReceived;
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
            case Key.M:
                _vm.ToggleMirror();
                e.Handled = true;
                break;
            case Key.F11:
                ToggleFullscreen();
                e.Handled = true;
                break;
        }

        base.OnKeyDown(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Registered as a singleton: a closed WPF window can never be shown
        // again, so hide instead and let app shutdown tear it down.
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ToggleFullscreen();
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _vm.RefreshMonitors();
    }

    private void OnToggleFullscreen(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void OnHide(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ToggleFullscreen()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnReactionReceived(string emoji)
    {
        Dispatcher.Invoke(() =>
        {
            if (ReactionsCanvas == null) return;

            var textBlock = new Emoji.Wpf.TextBlock
            {
                Text = emoji,
                FontSize = 86,
                RenderTransform = new System.Windows.Media.TranslateTransform()
            };

            double width = ActualWidth > 0 ? ActualWidth : 800;
            double height = ActualHeight > 0 ? ActualHeight : 600;

            // Random entry side: 0=bottom, 1=top, 2=left, 3=right
            var rng = new System.Random();
            int side = rng.Next(4);
            double toX, toY;

            switch (side)
            {
                case 0: // bottom -> travels up
                    Canvas.SetLeft(textBlock, rng.Next(50, (int)System.Math.Max(200, width - 100)));
                    Canvas.SetBottom(textBlock, rng.Next(20, 100));
                    toY = -(height - 150);
                    toX = rng.NextDouble() * 200 - 100;
                    break;
                case 1: // top -> travels down
                    Canvas.SetLeft(textBlock, rng.Next(50, (int)System.Math.Max(200, width - 100)));
                    Canvas.SetTop(textBlock, rng.Next(20, 100));
                    toY = height - 150;
                    toX = rng.NextDouble() * 200 - 100;
                    break;
                case 2: // left -> travels right
                    Canvas.SetTop(textBlock, rng.Next(50, (int)System.Math.Max(200, height - 100)));
                    Canvas.SetLeft(textBlock, rng.Next(20, 100));
                    toX = width - 150;
                    toY = rng.NextDouble() * 200 - 100;
                    break;
                default: // right -> travels left
                    Canvas.SetTop(textBlock, rng.Next(50, (int)System.Math.Max(200, height - 100)));
                    Canvas.SetRight(textBlock, rng.Next(20, 100));
                    toX = -(width - 150);
                    toY = rng.NextDouble() * 200 - 100;
                    break;
            }

            ReactionsCanvas.Children.Add(textBlock);

            var transform = (System.Windows.Media.TranslateTransform)textBlock.RenderTransform;
            var duration = System.TimeSpan.FromSeconds(5.5);

            var yAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = toY,
                Duration = duration
            };

            var xAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = toX,
                Duration = duration
            };

            var opacityAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = duration
            };

            opacityAnimation.Completed += (s, e) =>
            {
                ReactionsCanvas.Children.Remove(textBlock);
            };

            transform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, yAnimation);
            transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, xAnimation);
            textBlock.BeginAnimation(OpacityProperty, opacityAnimation);
        });
    }
}
