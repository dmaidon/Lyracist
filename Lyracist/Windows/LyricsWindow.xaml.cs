// Edited on Aug 20, 2026 @ 09:54:00 -> Add LoadSong methods to LyricsWindow for ProjectionWindow synchronization
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Lyracist.Windows;

public partial class LyricsWindow : Window
{
    private readonly LyricsWindowViewModel _vm;
    private readonly System.Random _rng = new();
    private double[] _barHeights = new double[40];
    private double[] _targetHeights = new double[40];

    public LyricsWindow(LyricsWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        Lyracist.Services.Tablet.LyricsHub.ReactionReceived += OnReactionReceived;
        System.Windows.Media.CompositionTarget.Rendering += OnCompositionTargetRendering;
    }

    public void LoadSong(string songTitle, string artist)
    {
        _vm.LoadSong(songTitle, artist);
    }

    public void LoadSong(Singer singer)
    {
        _vm.LoadSong(singer);
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                {
                    var displayService = App.AppHost.Services.GetService(typeof(IDisplayService)) as IDisplayService;
                    if (displayService != null)
                    {
                        displayService.IsLyricsActive = false;
                    }
                    else
                    {
                        Hide();
                    }
                }
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
        var displayService = App.AppHost.Services.GetService(typeof(IDisplayService)) as IDisplayService;
        if (displayService != null)
        {
            displayService.IsLyricsActive = false;
        }
        else
        {
            Hide();
        }
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
        var displayService = App.AppHost.Services.GetService(typeof(IDisplayService)) as IDisplayService;
        if (displayService != null)
        {
            displayService.IsLyricsActive = false;
        }
        else
        {
            Hide();
        }
    }

    private void ToggleFullscreen()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCompositionTargetRendering(object? sender, System.EventArgs e)
    {
        if (!IsVisible || VisualizerCanvas == null) return;

        double width = VisualizerCanvas.ActualWidth;
        double height = VisualizerCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var karaoke = App.AppHost.Services.GetService<KaraokeViewModel>();
        bool isPlaying = karaoke != null && karaoke.IsPlaying;

        int numBars = 40;
        double barWidth = width / numBars;
        double decay = 0.15;
        double rise = 0.4;

        // Populate visualizer rectangles once and reuse them to prevent constant layout recalculations
        if (VisualizerCanvas.Children.Count != numBars)
        {
            VisualizerCanvas.Children.Clear();
            for (int i = 0; i < numBars; i++)
            {
                var rect = new System.Windows.Shapes.Rectangle
                {
                    RadiusX = 3,
                    RadiusY = 3,
                    Fill = new System.Windows.Media.LinearGradientBrush
                    {
                        StartPoint = new System.Windows.Point(0, 1),
                        EndPoint = new System.Windows.Point(0, 0),
                        GradientStops =
                        {
                            new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(37, 99, 235), 0.0), // Blue
                            new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(168, 85, 247), 0.5), // Purple
                            new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(236, 72, 153), 1.0) // Pink
                        }
                    }
                };
                VisualizerCanvas.Children.Add(rect);
            }
        }

        for (int i = 0; i < numBars; i++)
        {
            if (isPlaying)
            {
                if (_rng.Next(10) > 7)
                {
                    double volFactor = karaoke != null ? (karaoke.Volume / 100.0) : 1.0;
                    _targetHeights[i] = _rng.NextDouble() * height * 0.7 * volFactor;
                }
            }
            else
            {
                double time = System.DateTime.Now.TimeOfDay.TotalSeconds;
                _targetHeights[i] = (System.Math.Sin(time * 2.0 + i * 0.3) + 1.0) * 0.5 * height * 0.15;
            }

            _barHeights[i] += (_targetHeights[i] - _barHeights[i]) * (isPlaying ? rise : decay);

            if (VisualizerCanvas.Children[i] is System.Windows.Shapes.Rectangle rect)
            {
                rect.Width = System.Math.Max(1.0, barWidth - 2);
                rect.Height = System.Math.Max(4, _barHeights[i]);
                rect.Opacity = isPlaying ? 0.65 : 0.25;

                Canvas.SetLeft(rect, i * barWidth + 1);
                Canvas.SetBottom(rect, 0);
            }
        }
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
