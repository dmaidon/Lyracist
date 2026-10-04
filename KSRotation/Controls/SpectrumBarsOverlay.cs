// Created on Oct 3, 2026 @ 14:58:00 -> Reusable bottom-of-screen synth bar overlay driven by LineInSpectrumService
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using KSRotation.Services;
using Lyracist.Shared;

namespace KSRotation.Controls
{
    /// <summary>
    /// Draws live frequency bars across the bottom of a screen. Costs nothing while collapsed: the
    /// per-frame render hook is attached only while the overlay is visible.
    /// </summary>
    public sealed class SpectrumBarsOverlay : Canvas
    {
        private const int BarCount = 48;

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
            nameof(IsActive), typeof(bool), typeof(SpectrumBarsOverlay),
            new PropertyMetadata(false, (d, _) => ((SpectrumBarsOverlay)d).Refresh()));

        /// <summary>Whether this screen should currently show the bars (per-screen setting).</summary>
        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        private readonly Rectangle[] _bars = new Rectangle[BarCount];
        private readonly double[] _heights = new double[BarCount];
        private bool _hooked;

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
            ApplyStyle();

            Loaded += (_, _) => Refresh();
            Unloaded += (_, _) => Unhook();
        }

        private void Refresh()
        {
            if (IsActive)
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
            LineInSpectrumService.Instance.StyleChanged += ApplyStyle;
            ApplyStyle();
            CompositionTarget.Rendering += OnRendering;
            _hooked = true;
        }

        private void Unhook()
        {
            if (!_hooked) return;
            LineInSpectrumService.Instance.StyleChanged -= ApplyStyle;
            CompositionTarget.Rendering -= OnRendering;
            _hooked = false;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

            float[] bands = LineInSpectrumService.Instance.GetBands(BarCount);
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

        // Re-colours every bar from the shared theme (changed live from Settings).
        private void ApplyStyle()
        {
            string style = LineInSpectrumService.Instance.Style;
            for (int i = 0; i < BarCount; i++)
            {
                var brush = SpectrumBarStyles.GetBarBrush(style, i, BarCount);
                brush.Freeze();
                _bars[i].Fill = brush;
            }
        }

    }
}
