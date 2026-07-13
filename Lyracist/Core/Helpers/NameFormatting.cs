using System;
using System.Globalization;

namespace Lyracist.Core.Helpers;

public static class NameFormatting
{
    public static string ProperCase(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        if (input.Equals("None", StringComparison.OrdinalIgnoreCase)) return "None";
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
    }
}
