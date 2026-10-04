// Edited on Oct 3, 2026 @ 15:55:00 -> GetBarBrush now delegates to Shared/SpectrumBarStyles so KSRotation uses the same themes
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
        if (!Lyracist.Controls.SpectrumBarsOverlay.AnyActive) _spectrumService?.Stop();
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
            if (!Lyracist.Controls.SpectrumBarsOverlay.AnyActive) _spectrumService?.Stop();
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
            if (!Lyracist.Controls.SpectrumBarsOverlay.AnyActive) _spectrumService?.Stop();
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
        => Lyracist.Shared.SpectrumBarStyles.GetBarBrush(style, barIndex, totalBars);

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
