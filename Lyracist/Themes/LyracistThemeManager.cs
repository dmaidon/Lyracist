// Edited on Aug 12, 2026 @ 06:22:00 -> Add AppInfoTextBrush for dynamic dark/light info text contrast
using MediaColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfApplication = System.Windows.Application;

namespace Lyracist.Themes;

public static class LyracistThemeManager
{
    public static void Apply(bool isDarkMode)
    {
        var palette = isDarkMode ? Dark : Light;
        var resources = WpfApplication.Current.Resources;

        foreach (var (key, color) in palette)
        {
            resources[key] = new SolidColorBrush(color);
        }
    }

    private static readonly (string Key, MediaColor Color)[] Dark =
    [
        ("AppBackgroundBrush", MediaColor.FromRgb(0x1E, 0x1E, 0x1E)),
        ("AppSurfaceBrush", MediaColor.FromRgb(0x2C, 0x2C, 0x2C)),
        ("AppBorderBrush", MediaColor.FromRgb(0x3A, 0x3A, 0x3A)),
        ("AppTextPrimaryBrush", MediaColor.FromRgb(0xFF, 0xFF, 0xFF)),
        ("AppTextSecondaryBrush", MediaColor.FromRgb(0xCC, 0xCC, 0xCC)),
        ("AppInfoTextBrush", MediaColor.FromRgb(0xA7, 0xF3, 0xD0)),
    ];

    private static readonly (string Key, MediaColor Color)[] Light =
    [
        ("AppBackgroundBrush", MediaColor.FromRgb(0xF3, 0xF3, 0xF3)),
        ("AppSurfaceBrush", MediaColor.FromRgb(0xFF, 0xFF, 0xFF)),
        ("AppBorderBrush", MediaColor.FromRgb(0xD8, 0xD8, 0xD8)),
        ("AppTextPrimaryBrush", MediaColor.FromRgb(0x1A, 0x1A, 0x1A)),
        ("AppTextSecondaryBrush", MediaColor.FromRgb(0x4A, 0x4A, 0x4A)),
        ("AppInfoTextBrush", MediaColor.FromRgb(0x04, 0x78, 0x57)),
    ];
}