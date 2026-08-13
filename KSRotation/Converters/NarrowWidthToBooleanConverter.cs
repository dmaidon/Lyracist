// Created on Aug 13, 2026 @ 13:46:21 -> Add NarrowWidthToBooleanConverter to drive the rotation list's tablet/vertical-mode layout
using System.Globalization;
using System.Windows.Data;

namespace KSRotation.Converters;

/// <summary>
/// Returns true when the bound width is below the threshold given via ConverterParameter (default
/// 1050 — comfortably above a typical 11" tablet's portrait width of ~900-1000 logical pixels, and
/// below any normal desktop window width). Used to switch the whole "Rotation" tab — the sidebar
/// stacking below the list, and the per-row item layout — into a tablet-friendly vertical mode.
/// </summary>
public class NarrowWidthToBooleanConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width)
        {
            return false;
        }

        double threshold = 1050;
        if (parameter is string paramText && double.TryParse(paramText, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedThreshold))
        {
            threshold = parsedThreshold;
        }

        return width > 0 && width < threshold;
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
    {
        throw new System.NotSupportedException();
    }
}
