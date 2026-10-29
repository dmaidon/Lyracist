// Created on Oct 28, 2026 @ 12:00:00 -> Holiday rotation-screen themes (Halloween, Christmas, Thanksgiving, New Year's Day, Valentine's Day, St. Patrick's Day, Easter, Mother's Day, Father's Day, Independence Day, Memorial Day)
using System;
using System.Collections.Generic;
using System.Linq;

namespace KSRotation.Models
{
    /// <summary>How an ambient particle travels across the 1920x1080 display canvas.</summary>
    public enum HolidayMotion
    {
        /// <summary>Drifts horizontally across the screen with a gentle wobble.</summary>
        Cross,
        /// <summary>Drops from above the screen to below it, swaying side to side.</summary>
        Fall,
        /// <summary>Floats from below the screen to above it, swaying side to side.</summary>
        Rise
    }

    /// <param name="Key">Stable id persisted in settings.</param>
    /// <param name="VignetteColor">Edge tint as #AARRGGBB.</param>
    /// <param name="FogColor">Ground-fog tint as #RRGGBB (empty for no fog).</param>
    /// <param name="Palette">Holiday colors (#RRGGBB) randomly applied to each floating character as its tint and glow.</param>
    /// <param name="Crossers">Glyphs that periodically cross the screen.</param>
    /// <param name="Ambient">Glyphs that continuously fall or rise.</param>
    /// <param name="CrossSeconds">Average gap between crossers.</param>
    /// <param name="AmbientSeconds">Gap between ambient particles.</param>
    public sealed record HolidayTheme(
        string Key,
        string Display,
        string VignetteColor,
        string FogColor,
        double FogOpacity,
        bool Flicker,
        string[] Crossers,
        string[] Ambient,
        HolidayMotion AmbientMotion,
        double CrossSeconds,
        double AmbientSeconds,
        string[] Palette);

    public static class HolidayThemes
    {
        public const string None = "None";
        public const string Auto = "Auto";

        public static readonly IReadOnlyList<HolidayTheme> All =
        [
            new("Halloween", "🎃 Halloween", "#CC1A0800", "B9C0D6", 0.35, true,
                ["🦇", "👻", "🕷️", "🎃", "💀", "🧛", "🧟", "🦉", "🐈", "🧙", "⚰️", "🕸️"],
                ["🍂", "🍁", "🍂"], HolidayMotion.Fall, 5, 5, ["#FF6D00", "#9C27B0", "#76FF03", "#F5F5F5", "#FF1744"]),
            new("Christmas", "🎄 Christmas", "#CC0B1A0B", "FFFFFF", 0.22, false,
                ["🎅", "🦌", "🛷", "🎄", "⭐", "🎁", "🔔", "⛄", "🤶"],
                ["❄️", "❄️", "✨"], HolidayMotion.Fall, 9, 0.6, ["#E53935", "#43A047", "#FFFFFF", "#FFD54F", "#00ACC1"]),
            new("Thanksgiving", "🦃 Thanksgiving", "#CC241203", "FFB74D", 0.15, false,
                ["🦃", "🌽", "🥧", "🍁", "🐿️", "🍂", "🥔"],
                ["🍂", "🍁", "🍂"], HolidayMotion.Fall, 8, 0.9, ["#E65100", "#BF360C", "#FFB300", "#8D6E63", "#F9A825"]),
            new("NewYears", "🎆 New Year's Day", "#CC050A24", "FFD54F", 0.12, false,
                ["🥂", "🍾", "🎆", "🎇", "🎉", "⏰", "🕛", "🎊"],
                ["🎊", "✨", "🎉"], HolidayMotion.Fall, 8, 0.7, ["#FFD54F", "#FFFFFF", "#C0C0C0", "#7C4DFF", "#40C4FF"]),
            new("Valentines", "💘 Valentine's Day", "#CC2A0612", "F8BBD0", 0.18, false,
                ["💌", "🌹", "🦋", "💝", "🧸", "😍", "🍫", "💞"],
                ["💕", "❤️", "💖", "💗"], HolidayMotion.Rise, 8, 0.8, ["#FF4081", "#E91E63", "#FF1744", "#F8BBD0", "#FFFFFF"]),
            new("StPatricks", "☘️ St. Patrick's Day", "#CC06210B", "A5D6A7", 0.15, false,
                ["🍀", "☘️", "🌈", "🍺", "🪙", "🧝", "🎩", "🐍"],
                ["🍀", "☘️", "🪙"], HolidayMotion.Fall, 7, 0.9, ["#00C853", "#69F0AE", "#FFD600", "#FFFFFF", "#FF9100"]),
            new("Easter", "🐣 Easter", "#CC2A1030", "F8BBD0", 0.18, false,
                ["🐰", "🐣", "🥚", "🐇", "🌷", "🐑", "🧺", "🐥"],
                ["🌷", "🥚", "🌸"], HolidayMotion.Fall, 7, 1.0, ["#F48FB1", "#CE93D8", "#81D4FA", "#FFF59D", "#A5D6A7"]),
            new("Mothers", "💐 Mother's Day", "#CC2A0A1A", "F8BBD0", 0.18, false,
                ["💐", "🌷", "🌹", "🦋", "💝", "🐝", "🌸", "🌺"],
                ["🌸", "💐", "💗"], HolidayMotion.Rise, 7, 1.0, ["#F48FB1", "#EC407A", "#CE93D8", "#FFFFFF", "#FFCC80"]),
            new("Fathers", "👔 Father's Day", "#CC0A1424", "90CAF9", 0.12, false,
                ["👔", "🎣", "⛳", "🍔", "🔧", "🏆", "🚗", "🍺"],
                ["⭐", "🔧", "⛳"], HolidayMotion.Fall, 7, 1.4, ["#1E88E5", "#0D47A1", "#8D6E63", "#B0BEC5", "#FFFFFF"]),
            new("Independence", "🎇 Independence Day", "#CC0A1230", "FFFFFF", 0.10, false,
                ["🎆", "🎇", "🗽", "🦅", "🇺🇸", "🧨", "⭐", "🍔"],
                ["✨", "🎇", "⭐"], HolidayMotion.Rise, 6, 1.1, ["#E53935", "#FFFFFF", "#1E88E5", "#FFD54F"]),
            new("Memorial", "🇺🇸 Memorial Day", "#CC0A1230", "FFFFFF", 0.08, false,
                ["🇺🇸", "🦅", "🎖️", "🕊️", "🌺", "⭐"],
                ["🌺", "⭐"], HolidayMotion.Fall, 10, 2.2, ["#E53935", "#FFFFFF", "#1E88E5", "#B0BEC5"]),
        ];

        /// <summary>File name (no extension) of the bundled Twemoji image for a glyph: lowercase hex code points joined by '-', variation selectors dropped.</summary>
        public static string EmojiFileKey(string glyph) =>
            string.Join('-', glyph.EnumerateRunes().Where(r => r.Value != 0xFE0F).Select(r => r.Value.ToString("x")));

        /// <summary>Choices for the settings dropdown: Off, Auto, then each holiday.</summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Options { get; } =
        [
            new(None, "Off"),
            new(Auto, "Auto (by date)"),
            .. All.Select(t => new KeyValuePair<string, string>(t.Key, t.Display)),
        ];

        public static HolidayTheme? Find(string? key) =>
            All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>Resolves a stored choice ("None", "Auto" or a holiday key) to the theme to show today, or null for none.</summary>
        public static HolidayTheme? Resolve(string? choice, DateTime today) =>
            string.Equals(choice, Auto, StringComparison.OrdinalIgnoreCase) ? ForDate(today) : Find(choice);

        /// <summary>The holiday whose window covers <paramref name="date"/>, or null.</summary>
        public static HolidayTheme? ForDate(DateTime date)
        {
            date = date.Date;
            int y = date.Year;

            bool Within(DateTime start, DateTime end) => date >= start.Date && date <= end.Date;

            if (Within(new DateTime(y, 10, 24), new DateTime(y, 10, 31))) return Find("Halloween");

            var thanksgiving = NthWeekday(y, 11, DayOfWeek.Thursday, 4);
            if (Within(thanksgiving.AddDays(-3), thanksgiving.AddDays(1))) return Find("Thanksgiving");

            if (Within(new DateTime(y, 12, 12), new DateTime(y, 12, 26))) return Find("Christmas");

            if (date >= new DateTime(y, 12, 30) || date <= new DateTime(y, 1, 2)) return Find("NewYears");

            if (Within(new DateTime(y, 2, 10), new DateTime(y, 2, 14))) return Find("Valentines");

            if (Within(new DateTime(y, 3, 14), new DateTime(y, 3, 17))) return Find("StPatricks");

            var easter = EasterSunday(y);
            if (Within(easter.AddDays(-3), easter.AddDays(1))) return Find("Easter");

            var mothers = NthWeekday(y, 5, DayOfWeek.Sunday, 2);
            if (Within(mothers.AddDays(-2), mothers)) return Find("Mothers");

            var fathers = NthWeekday(y, 6, DayOfWeek.Sunday, 3);
            if (Within(fathers.AddDays(-2), fathers)) return Find("Fathers");

            if (Within(new DateTime(y, 6, 29), new DateTime(y, 7, 5))) return Find("Independence");

            var memorial = LastWeekday(y, 5, DayOfWeek.Monday);
            if (Within(memorial.AddDays(-3), memorial)) return Find("Memorial");

            return null;
        }

        /// <summary>Gregorian Easter Sunday (anonymous Gregorian algorithm).</summary>
        private static DateTime EasterSunday(int year)
        {
            int a = year % 19, b = year / 100, c = year % 100;
            int d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
            int h = ((19 * a) + b - d - g + 15) % 30;
            int i = c / 4, k = c % 4;
            int l = (32 + (2 * e) + (2 * i) - h - k) % 7;
            int m = (a + (11 * h) + (22 * l)) / 451;
            int month = (h + l - (7 * m) + 114) / 31;
            int day = ((h + l - (7 * m) + 114) % 31) + 1;
            return new DateTime(year, month, day);
        }

        private static DateTime NthWeekday(int year, int month, DayOfWeek day, int n)
        {
            var first = new DateTime(year, month, 1);
            int offset = ((int)day - (int)first.DayOfWeek + 7) % 7;
            return first.AddDays(offset + ((n - 1) * 7));
        }

        private static DateTime LastWeekday(int year, int month, DayOfWeek day)
        {
            var last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            int back = ((int)last.DayOfWeek - (int)day + 7) % 7;
            return last.AddDays(-back);
        }
    }
}
