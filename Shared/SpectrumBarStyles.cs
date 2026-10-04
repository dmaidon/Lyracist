// Created on Oct 3, 2026 @ 15:50:00 -> Shared synth bar color themes (moved from Lyracist LyricsWindow so KSRotation offers the same styles)
using System;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace Lyracist.Shared;

/// <summary>The synth bar color themes shared by Lyracist's lyrics screen and KSRotation's screens.</summary>
public static class SpectrumBarStyles
{
    public const string Default = "Neon Sunset";

    public static readonly string[] Names =
    [
        "Neon Sunset",
        "Cyberpunk",
        "Emerald Pulse",
        "Solar Flare",
        "Electric Blue",
        "Rainbow Spectrum",
        "Monochrome Glow"
    ];

    /// <summary>Normalises a saved style name: unknown or empty values fall back to <see cref="Default"/>.</summary>
    public static string Normalize(string? style) =>
        Array.Find(Names, n => n.Equals(style, StringComparison.OrdinalIgnoreCase)) ?? Default;

    /// <summary>Builds the (unfrozen) bottom-to-top gradient for one bar. Only "Rainbow Spectrum" varies per bar.</summary>
    public static Brush GetBarBrush(string style, int barIndex, int totalBars)
    {
        return Normalize(style) switch
        {
            "Cyberpunk" => Gradient(Rgb(0, 240, 255), Rgb(121, 40, 202), Rgb(255, 0, 127)),          // Cyan, Violet, Fuchsia
            "Emerald Pulse" => Gradient(Rgb(6, 95, 70), Rgb(16, 185, 129), Rgb(132, 204, 22)),       // Dark Emerald, Jade, Neon Lime
            "Solar Flare" => Gradient(Rgb(220, 38, 38), Rgb(245, 158, 11), Rgb(253, 224, 71)),       // Crimson, Amber, Gold
            "Electric Blue" => Gradient(Rgb(30, 58, 138), Rgb(2, 132, 199), Rgb(103, 232, 249)),     // Deep Navy, Azure, Ice Cyan
            "Monochrome Glow" => Gradient(Rgb(51, 65, 85), Rgb(148, 163, 184), Rgb(255, 255, 255)),  // Slate, Silver, White
            "Rainbow Spectrum" => RainbowBar(barIndex, totalBars),
            _ => Gradient(Rgb(37, 99, 235), Rgb(168, 85, 247), Rgb(236, 72, 153)),                   // Neon Sunset: Blue, Purple, Pink
        };
    }

    private static Brush RainbowBar(int barIndex, int totalBars)
    {
        double hue = totalBars > 1 ? barIndex / (double)(totalBars - 1) * 300.0 : 0;
        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 1),
            EndPoint = new Point(0, 0),
            GradientStops =
            {
                new GradientStop(HsvToRgb(hue, 0.9, 0.9), 0.0),
                new GradientStop(HsvToRgb(hue, 0.4, 1.0), 1.0)
            }
        };
    }

    private static LinearGradientBrush Gradient(Color bottom, Color middle, Color top) => new()
    {
        StartPoint = new Point(0, 1),
        EndPoint = new Point(0, 0),
        GradientStops =
        {
            new GradientStop(bottom, 0.0),
            new GradientStop(middle, 0.5),
            new GradientStop(top, 1.0)
        }
    };

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    private static Color HsvToRgb(double h, double s, double v)
    {
        int hi = (int)Math.Floor(h / 60.0) % 6;
        double f = h / 60.0 - Math.Floor(h / 60.0);
        byte vByte = (byte)(v * 255);
        byte p = (byte)(v * (1 - s) * 255);
        byte q = (byte)(v * (1 - f * s) * 255);
        byte t = (byte)(v * (1 - (1 - f) * s) * 255);

        return hi switch
        {
            0 => Color.FromRgb(vByte, t, p),
            1 => Color.FromRgb(q, vByte, p),
            2 => Color.FromRgb(p, vByte, t),
            3 => Color.FromRgb(p, q, vByte),
            4 => Color.FromRgb(t, p, vByte),
            _ => Color.FromRgb(vByte, p, q)
        };
    }
}
