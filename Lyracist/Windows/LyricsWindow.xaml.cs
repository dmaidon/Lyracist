// Edited on Oct 3, 2026 @ 12:44:00 -> Make GetBarBrush internal static for AdjustSynthDisplayWindow dialog preview
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.Services.Media;
using Lyracist.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Lyracist.Windows;

public partial class LyricsWindow : Window
{
    private readonly LyricsWindowViewModel _vm;
    private readonly IAudioSpectrumService? _spectrumService;
    private readonly System.Random _rng = new();
    private double[] _barHeights = [];
    private double[] _targetHeights = [];
    private string _currentStyle = string.Empty;
    private string _currentBarWidth = string.Empty;

    public LyricsWindow(LyricsWindowViewModel vm, IAudioSpectrumService? spectrumService = null)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _spectrumService = spectrumService ?? App.AppHost.Services.GetService<IAudioSpectrumService>();

        Lyracist.Services.Tablet.LyricsHub.ReactionReceived += OnReactionReceived;
        System.Windows.Media.CompositionTarget.Rendering += OnCompositionTargetRendering;
        Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        System.Windows.Media.CompositionTarget.Rendering -= OnCompositionTargetRendering;
        Lyracist.Services.Tablet.LyricsHub.ReactionReceived -= OnReactionReceived;
        _spectrumService?.Stop();
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

        bool isVisualizerEnabled = Lyracist.Core.Helpers.AppSettings.EnableLyricsVisualizer;
        if (!isVisualizerEnabled)
        {
            if (VisualizerCanvas.Visibility != Visibility.Collapsed)
            {
                VisualizerCanvas.Visibility = Visibility.Collapsed;
            }
            _spectrumService?.Stop();
            return;
        }

        if (VisualizerCanvas.Visibility != Visibility.Visible)
        {
            VisualizerCanvas.Visibility = Visibility.Visible;
        }

        double width = VisualizerCanvas.ActualWidth;
        double height = VisualizerCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var karaoke = App.AppHost.Services.GetService<KaraokeViewModel>();
        bool isPlaying = karaoke != null && karaoke.IsPlaying;

        string currentStyle = Lyracist.Core.Helpers.AppSettings.LyricsVisualizerStyle;
        string currentBarWidth = Lyracist.Core.Helpers.AppSettings.LyricsVisualizerBarWidth;
        string currentMode = Lyracist.Core.Helpers.AppSettings.LyricsVisualizerMode;

        // Determine bar spacing and target width
        double barWidthPx = currentBarWidth switch
        {
            "Slim" => 10,
            "Wide" => 28,
            "Extra Wide" => 42,
            _ => 18 // "Normal"
        };
        double barSpacing = currentBarWidth switch
        {
            "Slim" => 2,
            "Wide" => 4,
            "Extra Wide" => 6,
            _ => 3
        };

        int numBars = Math.Clamp((int)(width / (barWidthPx + barSpacing)), 8, 80);
        double slotWidth = width / numBars;
        double actualBarWidth = Math.Max(2.0, slotWidth - barSpacing);

        // Recreate rectangles if count, style, or width mode changed
        if (VisualizerCanvas.Children.Count != numBars || _currentStyle != currentStyle || _currentBarWidth != currentBarWidth)
        {
            VisualizerCanvas.Children.Clear();
            _currentStyle = currentStyle;
            _currentBarWidth = currentBarWidth;
            _barHeights = new double[numBars];
            _targetHeights = new double[numBars];

            for (int i = 0; i < numBars; i++)
            {
                var brush = GetBarBrush(currentStyle, i, numBars);
                brush.Freeze();
                var rect = new System.Windows.Shapes.Rectangle
                {
                    RadiusX = 3,
                    RadiusY = 3,
                    Fill = brush
                };
                VisualizerCanvas.Children.Add(rect);
            }
        }

        bool isLiveFft = currentMode == "Audio Spectrum (Live FFT)";
        float[]? fftBands = null;

        if (isLiveFft && _spectrumService != null)
        {
            if (!_spectrumService.IsCapturing)
            {
                _spectrumService.Start();
            }
            fftBands = _spectrumService.GetFrequencyBands(numBars);
        }
        else
        {
            _spectrumService?.Stop();
        }

        double decay = 0.16;
        double rise = isLiveFft ? 0.85 : 0.55; // live FFT is already smoothed in the service
        double time = System.DateTime.Now.TimeOfDay.TotalSeconds;

        for (int i = 0; i < numBars; i++)
        {
            if (isLiveFft && fftBands != null && fftBands.Length > i)
            {
                float bandEnergy = fftBands[i];
                if (bandEnergy > 0.005f)
                {
                    _targetHeights[i] = bandEnergy * height * 0.92;
                }
                else if (isPlaying)
                {
                    // Gentle baseline when song is playing during quiet passages
                    _targetHeights[i] = (Math.Sin(time * 2.5 + i * 0.4) + 1.0) * 0.5 * height * 0.08;
                }
                else
                {
                    // Ambient ripple when idle / paused
                    _targetHeights[i] = (Math.Sin(time * 1.5 + i * 0.25) + 1.0) * 0.5 * height * 0.10;
                }
            }
            else if (isPlaying)
            {
                // Simulated mode when playing
                if (_rng.Next(10) > 7)
                {
                    double volFactor = karaoke != null ? (karaoke.Volume / 100.0) : 1.0;
                    _targetHeights[i] = _rng.NextDouble() * height * 0.7 * volFactor;
                }
            }
            else
            {
                // Simulated mode when paused
                _targetHeights[i] = (Math.Sin(time * 2.0 + i * 0.3) + 1.0) * 0.5 * height * 0.15;
            }

            _barHeights[i] += (_targetHeights[i] - _barHeights[i]) * (isPlaying ? rise : decay);

            if (VisualizerCanvas.Children[i] is System.Windows.Shapes.Rectangle rect)
            {
                rect.Width = actualBarWidth;
                rect.Height = Math.Max(3, _barHeights[i]);
                rect.Opacity = isPlaying ? 0.95 : 0.40;

                Canvas.SetLeft(rect, i * slotWidth + barSpacing / 2);
                Canvas.SetBottom(rect, 0);
            }
        }
    }

    internal static System.Windows.Media.Brush GetBarBrush(string style, int barIndex, int totalBars)
    {
        switch (style)
        {
            case "Cyberpunk":
                return new System.Windows.Media.LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 1),
                    EndPoint = new System.Windows.Point(0, 0),
                    GradientStops =
                    {
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0, 240, 255), 0.0), // Cyan
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(121, 40, 202), 0.5), // Violet
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(255, 0, 127), 1.0)  // Fuchsia
                    }
                };
            case "Emerald Pulse":
                return new System.Windows.Media.LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 1),
                    EndPoint = new System.Windows.Point(0, 0),
                    GradientStops =
                    {
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(6, 95, 70), 0.0),    // Dark Emerald
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(16, 185, 129), 0.5), // Vibrant Jade
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(132, 204, 22), 1.0)  // Neon Lime
                    }
                };
            case "Solar Flare":
                return new System.Windows.Media.LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 1),
                    EndPoint = new System.Windows.Point(0, 0),
                    GradientStops =
                    {
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(220, 38, 38), 0.0),  // Crimson
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(245, 158, 11), 0.5), // Vivid Amber
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(253, 224, 71), 1.0)  // Electric Gold
                    }
                };
            case "Electric Blue":
                return new System.Windows.Media.LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 1),
                    EndPoint = new System.Windows.Point(0, 0),
                    GradientStops =
                    {
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(30, 58, 138), 0.0),  // Deep Navy
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(2, 132, 199), 0.5),  // Azure
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(103, 232, 249), 1.0) // Ice Cyan
                    }
                };
            case "Rainbow Spectrum":
                double hue = totalBars > 1 ? (barIndex / (double)(totalBars - 1)) * 300.0 : 0;
                var baseColor = HsvToRgb(hue, 0.9, 0.9);
                var topColor = HsvToRgb(hue, 0.4, 1.0);
                return new System.Windows.Media.LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 1),
                    EndPoint = new System.Windows.Point(0, 0),
                    GradientStops =
                    {
                        new System.Windows.Media.GradientStop(baseColor, 0.0),
                        new System.Windows.Media.GradientStop(topColor, 1.0)
                    }
                };
            case "Monochrome Glow":
                return new System.Windows.Media.LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 1),
                    EndPoint = new System.Windows.Point(0, 0),
                    GradientStops =
                    {
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(51, 65, 85), 0.0),    // Slate
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(148, 163, 184), 0.5), // Silver
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(255, 255, 255), 1.0)  // Pure White
                    }
                };
            case "Neon Sunset":
            default:
                return new System.Windows.Media.LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 1),
                    EndPoint = new System.Windows.Point(0, 0),
                    GradientStops =
                    {
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(37, 99, 235), 0.0),  // Blue
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(168, 85, 247), 0.5), // Purple
                        new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(236, 72, 153), 1.0)  // Pink
                    }
                };
        }
    }

    private static System.Windows.Media.Color HsvToRgb(double h, double s, double v)
    {
        int hi = (int)(Math.Floor(h / 60.0)) % 6;
        double f = h / 60.0 - Math.Floor(h / 60.0);
        byte vByte = (byte)(v * 255);
        byte p = (byte)(v * (1 - s) * 255);
        byte q = (byte)(v * (1 - f * s) * 255);
        byte t = (byte)(v * (1 - (1 - f) * s) * 255);

        return hi switch
        {
            0 => System.Windows.Media.Color.FromRgb(vByte, t, p),
            1 => System.Windows.Media.Color.FromRgb(q, vByte, p),
            2 => System.Windows.Media.Color.FromRgb(p, vByte, t),
            3 => System.Windows.Media.Color.FromRgb(p, q, vByte),
            4 => System.Windows.Media.Color.FromRgb(t, p, vByte),
            _ => System.Windows.Media.Color.FromRgb(vByte, p, q)
        };
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
