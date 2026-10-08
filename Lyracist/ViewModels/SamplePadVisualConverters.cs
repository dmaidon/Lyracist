// Created on Oct 7, 2026 @ 19:46:00 -> Visual value converters for the SamplePadPage
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Lyracist.ViewModels;

public static class SamplePadVisualConverters
{
    public static readonly IValueConverter PlayingBorderBrushConverter = new PlayingBorderBrush();
    public static readonly IValueConverter InverseBoolConverter = new InverseBool();
    public static readonly IValueConverter StringNotEmptyToVisConverter = new StringNotEmptyToVis();

    private class PlayingBorderBrush : IValueConverter
    {
        private static readonly System.Windows.Media.Brush ActiveBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)); // Emerald Green glow
        private static readonly System.Windows.Media.Brush IdleBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is true) ? ActiveBrush : IdleBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    private class InverseBool : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is true);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(value is true);
        }
    }

    private class StringNotEmptyToVis : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
