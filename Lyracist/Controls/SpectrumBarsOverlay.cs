// Created on Oct 4, 2026 @ 10:00:00 -> Bottom-of-screen synth bars for the Rotation and DJ Banner screens, driven by IAudioSpectrumService
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lyracist.Core.Helpers;
using Lyracist.Services.Media;
using Lyracist.Shared;
using Microsoft.Extensions.DependencyInjection;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace Lyracist.Controls;

/// <summary>
/// Draws live frequency bars across the bottom of a screen. <see cref="Screen"/> picks which per-screen
/// toggle ("Rotation" or "DjBanners") controls it. The per-frame hook is attached only while shown.
/// </summary>
public sealed class SpectrumBarsOverlay : Canvas
{
    private const int BarCount = 48;
    private static int _activeCount;

    /// <summary>True while any overlay is capturing, so the Lyrics window doesn't stop a capture it doesn't own.</summary>
    public static bool AnyActive => _activeCount > 0;

    public static readonly DependencyProperty ScreenProperty = DependencyProperty.Register(
        nameof(Screen), typeof(string), typeof(SpectrumBarsOverlay),
        new PropertyMetadata("Rotation", (d, _) => ((SpectrumBarsOverlay)d).Refresh()));

    public string Screen
    {
        get => (string)GetValue(ScreenProperty);
        set => SetValue(ScreenProperty, value);
    }

    private readonly Rectangle[] _bars = new Rectangle[BarCount];
    private readonly double[] _heights = new double[BarCount];
    private IAudioSpectrumService? _service;
    private bool _hooked;
    private string _appliedStyle = string.Empty;

    public SpectrumBarsOverlay()
    {
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
        ClipToBounds = true;

        for (int i = 0; i < BarCount; i++)
        {
            var rect = new Rectangle { RadiusX = 3, RadiusY = 3 };
            _bars[i] = rect;
            Children.Add(rect);
        }

        Loaded += (_, _) =>
        {
            AppSettings.VisualizerScreensChanged += Refresh;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            AppSettings.VisualizerScreensChanged -= Refresh;
            Unhook();
        };
    }

    private bool WantsBars => Screen == "DjBanners" ? AppSettings.ShowVisualizerOnDjBanners : AppSettings.ShowVisualizerOnRotation;

    private void Refresh()
    {
        if (IsLoaded && WantsBars)
        {
            Visibility = Visibility.Visible;
            Hook();
        }
        else
        {
            Unhook();
            Visibility = Visibility.Collapsed;
        }
    }

    private void Hook()
    {
        if (_hooked) return;
        _service ??= App.AppHost.Services.GetService<IAudioSpectrumService>();
        _service?.Start();
        _activeCount++;
        CompositionTarget.Rendering += OnRendering;
        _hooked = true;
    }

    private void Unhook()
    {
        if (!_hooked) return;
        CompositionTarget.Rendering -= OnRendering;
        _hooked = false;
        _activeCount--;
        if (_activeCount == 0 && !AppSettings.EnableLyricsVisualizer) _service?.Stop();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0 || _service == null) return;

        if (!_service.IsCapturing) _service.Start();

        string style = AppSettings.LyricsVisualizerStyle;
        if (style != _appliedStyle)
        {
            for (int i = 0; i < BarCount; i++)
            {
                var brush = SpectrumBarStyles.GetBarBrush(style, i, BarCount);
                brush.Freeze();
                _bars[i].Fill = brush;
            }
            _appliedStyle = style;
        }
        Opacity = AppSettings.LyricsVisualizerOpacity;

        float[] bands = _service.GetFrequencyBands(BarCount);
        double slot = width / BarCount;
        double barWidth = Math.Max(2, slot * 0.72);

        for (int i = 0; i < BarCount; i++)
        {
            double target = i < bands.Length ? bands[i] * height : 0;
            _heights[i] += (target - _heights[i]) * 0.85;
            var rect = _bars[i];
            rect.Width = barWidth;
            rect.Height = Math.Max(2, _heights[i]);
            SetLeft(rect, i * slot + (slot - barWidth) / 2);
            SetBottom(rect, 0);
        }
    }
}
