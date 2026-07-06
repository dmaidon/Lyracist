using System;
using System.Globalization;
using System.Windows.Data;

namespace Lyracist.Core.Helpers;

public class CompletedToCheckedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int completedCount && parameter != null && int.TryParse(parameter.ToString(), out int index))
        {
            return completedCount >= index;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && parameter != null && int.TryParse(parameter.ToString(), out int index))
        {
            return isChecked ? index : index - 1;
        }
        return 0;
    }
}
