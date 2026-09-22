// Created on Sep 22, 2026 @ 10:00:00 -> Add FestivalTierFontSizeConverter for the Concert Festival Lineup rotation screen
using System.Globalization;
using System.Windows.Data;

namespace Lyracist.Core.Helpers;

/// <summary>
/// Maps an ItemsControl's AlternationIndex to a decreasing font size, giving the Concert Festival
/// Lineup screen's support-act list its poster-style "headliner down to opener" typography tier.
/// The floor (18) matches the minimum readable size settled on for the other rotation screens'
/// "Up Next" lists, so even the smallest-billed act stays legible.
/// </summary>
public class FestivalTierFontSizeConverter : IValueConverter
{
    private static readonly double[] Tiers = [34, 28, 24, 20];
    private const double Floor = 18;

    public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int index || index < 0)
        {
            return Floor;
        }

        return index < Tiers.Length ? Tiers[index] : Floor;
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
    {
        throw new System.NotSupportedException();
    }
}
