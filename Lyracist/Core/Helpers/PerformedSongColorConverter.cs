// Created on Aug 15, 2026 @ 10:20:00 -> PerformedSongColorConverter supporting 5-color rotation for Dark and Light schemes
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Lyracist.Core.Models;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace Lyracist.Core.Helpers;

public class PerformedSongColorConverter : IValueConverter
{
    private readonly record struct RotationPalette(MediaBrush Background, MediaBrush Border, MediaBrush Foreground);

    private static MediaBrush FrozenBrush(string hex)
    {
        var brush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    // Indexed 0..4: Violet, Cyan, Emerald, Amber, Rose
    private static readonly RotationPalette[] DarkPalettes =
    [
        new(FrozenBrush("#231834"), FrozenBrush("#9333EA"), FrozenBrush("#E9D5FF")), // Violet
        new(FrozenBrush("#122A38"), FrozenBrush("#0EA5E9"), FrozenBrush("#BAE6FD")), // Cyan
        new(FrozenBrush("#123322"), FrozenBrush("#10B981"), FrozenBrush("#A7F3D0")), // Emerald
        new(FrozenBrush("#362414"), FrozenBrush("#F59E0B"), FrozenBrush("#FDE68A")), // Amber
        new(FrozenBrush("#361426"), FrozenBrush("#EC4899"), FrozenBrush("#FBCFE8"))  // Rose
    ];

    private static readonly RotationPalette[] LightPalettes =
    [
        new(FrozenBrush("#F3E8FF"), FrozenBrush("#C084FC"), FrozenBrush("#581C87")), // Violet
        new(FrozenBrush("#E0F2FE"), FrozenBrush("#38BDF8"), FrozenBrush("#0369A1")), // Cyan
        new(FrozenBrush("#DCFCE7"), FrozenBrush("#4ADE80"), FrozenBrush("#15803D")), // Emerald
        new(FrozenBrush("#FEF3C7"), FrozenBrush("#FBBF24"), FrozenBrush("#B45309")), // Amber
        new(FrozenBrush("#FCE7F3"), FrozenBrush("#F472B6"), FrozenBrush("#BE185D"))  // Rose
    ];

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int index = value switch
        {
            int intVal => Math.Abs(intVal) % 5,
            PerformedSong song => song.ColorIndex,
            _ => 0
        };

        bool isDark = true;
        try
        {
            isDark = Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme() == Wpf.Ui.Appearance.ApplicationTheme.Dark;
        }
        catch
        {
            // Default to dark theme if theme manager is unavailable
        }

        var palette = (isDark ? DarkPalettes : LightPalettes)[index];
        string targetTypeParam = parameter as string ?? "Background";

        return targetTypeParam.ToLowerInvariant() switch
        {
            "border" => palette.Border,
            "foreground" or "text" => palette.Foreground,
            _ => palette.Background
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
