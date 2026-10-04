// Created on Oct 3, 2026 @ 12:43:00 -> Code-behind for AdjustSynthDisplayWindow dialog with live preview and instant setting persistence
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Rectangle = System.Windows.Shapes.Rectangle;
using Lyracist.Core.Helpers;
using Lyracist.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Lyracist.Windows;

public partial class AdjustSynthDisplayWindow : Window
{
    private static readonly string[] Styles =
    [
        "Neon Sunset",
        "Cyberpunk",
        "Emerald Pulse",
        "Solar Flare",
        "Electric Blue",
        "Rainbow Spectrum",
        "Monochrome Glow"
    ];

    private static readonly string[] BarWidths =
    [
        "Slim",
        "Normal",
        "Wide",
        "Extra Wide"
    ];

    private bool _initialized;
    private readonly Random _rng = new();
    private readonly double[] _previewHeights = new double[36];

    public AdjustSynthDisplayWindow()
    {
        InitializeComponent();

        StyleComboBox.ItemsSource = Styles;
        BarWidthComboBox.ItemsSource = BarWidths;

        // Load persisted settings
        EnableVisualizerCheckBox.IsChecked = AppSettings.EnableLyricsVisualizer;
        ShowOnRotationCheckBox.IsChecked = AppSettings.ShowVisualizerOnRotation;
        ShowOnDjBannersCheckBox.IsChecked = AppSettings.ShowVisualizerOnDjBanners;
        bool isFft = AppSettings.LyricsVisualizerMode == "Audio Spectrum (Live FFT)";
        ModeFftRadio.IsChecked = isFft;
        ModeSimulatedRadio.IsChecked = !isFft;

        StyleComboBox.SelectedItem = AppSettings.LyricsVisualizerStyle;
        if (StyleComboBox.SelectedIndex < 0) StyleComboBox.SelectedIndex = 0;

        BarWidthComboBox.SelectedItem = AppSettings.LyricsVisualizerBarWidth;
        if (BarWidthComboBox.SelectedIndex < 0) BarWidthComboBox.SelectedIndex = 1;

        OpacitySlider.Value = AppSettings.LyricsVisualizerOpacity;
        OpacityValueText.Text = $"{AppSettings.LyricsVisualizerOpacity:P0}";

        _initialized = true;

        CompositionTarget.Rendering += OnPreviewRendering;
        Closed += (_, _) => CompositionTarget.Rendering -= OnPreviewRendering;

        UpdatePreviewBars();
    }

    private void OnScreenToggleChanged(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        AppSettings.ShowVisualizerOnRotation = ShowOnRotationCheckBox.IsChecked == true;
        AppSettings.ShowVisualizerOnDjBanners = ShowOnDjBannersCheckBox.IsChecked == true;
    }

    private void OnSettingChanged(object? sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool enabled = EnableVisualizerCheckBox.IsChecked == true;
        string mode = ModeFftRadio.IsChecked == true ? "Audio Spectrum (Live FFT)" : "Simulated / Ambient";
        string style = StyleComboBox.SelectedItem as string ?? "Neon Sunset";
        string barWidth = BarWidthComboBox.SelectedItem as string ?? "Normal";
        double opacity = OpacitySlider.Value;

        AppSettings.EnableLyricsVisualizer = enabled;
        AppSettings.LyricsVisualizerMode = mode;
        AppSettings.LyricsVisualizerStyle = style;
        AppSettings.LyricsVisualizerBarWidth = barWidth;
        AppSettings.LyricsVisualizerOpacity = opacity;

        // Notify active lyrics window
        NotifyLyricsWindow();
        UpdatePreviewBars();
    }

    private void OnOpacitySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;

        double val = Math.Round(e.NewValue, 2);
        OpacityValueText.Text = $"{val:P0}";
        AppSettings.LyricsVisualizerOpacity = val;

        NotifyLyricsWindow();
        UpdatePreviewBars();
    }

    private void OnPreset25Click(object sender, RoutedEventArgs e) => SetOpacityPreset(0.25);
    private void OnPreset50Click(object sender, RoutedEventArgs e) => SetOpacityPreset(0.50);
    private void OnPreset80Click(object sender, RoutedEventArgs e) => SetOpacityPreset(0.80);
    private void OnPreset100Click(object sender, RoutedEventArgs e) => SetOpacityPreset(1.00);

    private void SetOpacityPreset(double val)
    {
        OpacitySlider.Value = val;
    }

    private void NotifyLyricsWindow()
    {
        try
        {
            var lyricsVm = App.AppHost.Services.GetService(typeof(LyricsWindowViewModel)) as LyricsWindowViewModel;
            lyricsVm?.NotifyVisualizerChanged();
        }
        catch
        {
            // Best effort
        }
    }

    private void UpdatePreviewBars()
    {
        if (PreviewCanvas == null) return;

        PreviewCanvas.Opacity = OpacitySlider.Value;
        string style = StyleComboBox.SelectedItem as string ?? "Neon Sunset";
        string barWidthStr = BarWidthComboBox.SelectedItem as string ?? "Normal";

        double barWidth = barWidthStr switch
        {
            "Slim" => 6,
            "Wide" => 14,
            "Extra Wide" => 20,
            _ => 10
        };
        double spacing = barWidthStr switch
        {
            "Slim" => 2,
            "Wide" => 4,
            "Extra Wide" => 5,
            _ => 3
        };

        double canvasWidth = PreviewCanvas.ActualWidth > 0 ? PreviewCanvas.ActualWidth : 460;
        int numBars = Math.Clamp((int)(canvasWidth / (barWidth + spacing)), 8, 48);

        if (PreviewCanvas.Children.Count != numBars)
        {
            PreviewCanvas.Children.Clear();
            for (int i = 0; i < numBars; i++)
            {
                var brush = LyricsWindow.GetBarBrush(style, i, numBars);
                brush.Freeze();
                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = 8 + _rng.NextDouble() * 30,
                    RadiusX = 2,
                    RadiusY = 2,
                    Fill = brush
                };
                Canvas.SetLeft(rect, i * (barWidth + spacing));
                Canvas.SetBottom(rect, 0);
                PreviewCanvas.Children.Add(rect);
            }
        }
        else
        {
            for (int i = 0; i < PreviewCanvas.Children.Count; i++)
            {
                if (PreviewCanvas.Children[i] is Rectangle rect)
                {
                    rect.Width = barWidth;
                    var brush = LyricsWindow.GetBarBrush(style, i, numBars);
                    brush.Freeze();
                    rect.Fill = brush;
                    Canvas.SetLeft(rect, i * (barWidth + spacing));
                }
            }
        }
    }

    private void OnPreviewRendering(object? sender, EventArgs e)
    {
        if (!IsVisible || PreviewCanvas == null || PreviewCanvas.Children.Count == 0) return;

        double time = DateTime.Now.TimeOfDay.TotalSeconds;
        int count = PreviewCanvas.Children.Count;

        for (int i = 0; i < count; i++)
        {
            if (PreviewCanvas.Children[i] is Rectangle rect)
            {
                double wave = Math.Sin(time * 3.5 + i * 0.4) * 0.5 + 0.5;
                double wave2 = Math.Cos(time * 2.2 + i * 0.25) * 0.5 + 0.5;
                double combined = (wave * 0.6 + wave2 * 0.4);
                rect.Height = Math.Max(4, combined * 36);
            }
        }
    }

    private void OnDoneClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
