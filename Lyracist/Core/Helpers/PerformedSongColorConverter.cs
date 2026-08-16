// Created on Aug 15, 2026 @ 10:20:00 -> PerformedSongColorConverter supporting 5-color rotation for Dark and Light schemes
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Lyracist.Core.Models;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace Lyracist.Core.Helpers;

public class PerformedSongColorConverter : IValueConverter
{
    // 5 Dark Mode Palettes (Background, Border, Foreground)
    private static readonly SolidColorBrush[] DarkBackgrounds =
    [
        new((MediaColor)MediaColorConverter.ConvertFromString("#231834")), // Violet
        new((MediaColor)MediaColorConverter.ConvertFromString("#122A38")), // Cyan
        new((MediaColor)MediaColorConverter.ConvertFromString("#123322")), // Emerald
        new((MediaColor)MediaColorConverter.ConvertFromString("#362414")), // Amber
        new((MediaColor)MediaColorConverter.ConvertFromString("#361426"))  // Rose
    ];

    private static readonly SolidColorBrush[] DarkBorders =
    [
        new((MediaColor)MediaColorConverter.ConvertFromString("#9333EA")), // Violet
        new((MediaColor)MediaColorConverter.ConvertFromString("#0EA5E9")), // Cyan
        new((MediaColor)MediaColorConverter.ConvertFromString("#10B981")), // Emerald
        new((MediaColor)MediaColorConverter.ConvertFromString("#F59E0B")), // Amber
        new((MediaColor)MediaColorConverter.ConvertFromString("#EC4899"))  // Rose
    ];

    private static readonly SolidColorBrush[] DarkForegrounds =
    [
        new((MediaColor)MediaColorConverter.ConvertFromString("#E9D5FF")), // Violet
        new((MediaColor)MediaColorConverter.ConvertFromString("#BAE6FD")), // Cyan
        new((MediaColor)MediaColorConverter.ConvertFromString("#A7F3D0")), // Emerald
        new((MediaColor)MediaColorConverter.ConvertFromString("#FDE68A")), // Amber
        new((MediaColor)MediaColorConverter.ConvertFromString("#FBCFE8"))  // Rose
    ];

    // 5 Light Mode Palettes (Background, Border, Foreground)
    private static readonly SolidColorBrush[] LightBackgrounds =
    [
        new((MediaColor)MediaColorConverter.ConvertFromString("#F3E8FF")), // Violet
        new((MediaColor)MediaColorConverter.ConvertFromString("#E0F2FE")), // Cyan
        new((MediaColor)MediaColorConverter.ConvertFromString("#DCFCE7")), // Emerald
        new((MediaColor)MediaColorConverter.ConvertFromString("#FEF3C7")), // Amber
        new((MediaColor)MediaColorConverter.ConvertFromString("#FCE7F3"))  // Rose
    ];

    private static readonly SolidColorBrush[] LightBorders =
    [
        new((MediaColor)MediaColorConverter.ConvertFromString("#C084FC")), // Violet
        new((MediaColor)MediaColorConverter.ConvertFromString("#38BDF8")), // Cyan
        new((MediaColor)MediaColorConverter.ConvertFromString("#4ADE80")), // Emerald
        new((MediaColor)MediaColorConverter.ConvertFromString("#FBBF24")), // Amber
        new((MediaColor)MediaColorConverter.ConvertFromString("#F472B6"))  // Rose
    ];

    private static readonly SolidColorBrush[] LightForegrounds =
    [
        new((MediaColor)MediaColorConverter.ConvertFromString("#581C87")), // Violet
        new((MediaColor)MediaColorConverter.ConvertFromString("#0369A1")), // Cyan
        new((MediaColor)MediaColorConverter.ConvertFromString("#15803D")), // Emerald
        new((MediaColor)MediaColorConverter.ConvertFromString("#B45309")), // Amber
        new((MediaColor)MediaColorConverter.ConvertFromString("#BE185D"))  // Rose
    ];

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int index = 0;
        if (value is int intVal)
        {
            index = Math.Abs(intVal) % 5;
        }
        else if (value is PerformedSong song)
        {
            index = Math.Abs(song.ColorIndex) % 5;
        }

        bool isDark = true;
        try
        {
            isDark = Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme() == Wpf.Ui.Appearance.ApplicationTheme.Dark;
        }
        catch
        {
            // Default to dark theme if theme manager is unavailable
        }

        string targetTypeParam = parameter as string ?? "Background";

        if (isDark)
        {
            return targetTypeParam.ToLowerInvariant() switch
            {
                "border" => DarkBorders[index],
                "foreground" or "text" => DarkForegrounds[index],
                _ => DarkBackgrounds[index]
            };
        }
        else
        {
            return targetTypeParam.ToLowerInvariant() switch
            {
                "border" => LightBorders[index],
                "foreground" or "text" => LightForegrounds[index],
                _ => LightBackgrounds[index]
            };
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
