// Created on Nov 9, 2026 @ 09:02:30 -> Add EmojiReactionThemes model for 7 themed crowd emoji reaction sets and DJ Show Theme override
using System;
using System.Collections.Generic;
using System.Linq;

namespace KSRotation.Models
{
    /// <summary>
    /// Represents a themed set of 6 crowd reaction emojis that patrons can send from their phones.
    /// </summary>
    /// <param name="Key">Stable identifier persisted in settings and sent over the wire.</param>
    /// <param name="Display">Full display name with leading icon.</param>
    /// <param name="ShortLabel">Compact label for mobile category pills.</param>
    /// <param name="Emojis">The 6 reaction emojis in this set.</param>
    public sealed record EmojiReactionSet(
        string Key,
        string Display,
        string ShortLabel,
        string[] Emojis);

    /// <summary>
    /// Defines the 7 themed crowd reaction sets and DJ Show Emoji Theme override options
    /// shared across KSRotation, KSRotation.Maui, and Lyracist.
    /// </summary>
    public static class EmojiReactionThemes
    {
        /// <summary>Default mode: patrons can freely switch between all 7 emoji sets on their phone for each singer.</summary>
        public const string PatronChoice = "PatronChoice";

        /// <summary>Disables crowd emoji reactions across patron phones and displays.</summary>
        public const string Off = "Off";

        public static readonly IReadOnlyList<EmojiReactionSet> All =
        [
            new("Classic",   "🌟 Classic",            "🌟 Classic", ["❤️", "🔥", "👏", "🙌", "🎉", "👑"]),
            new("Country",   "🤠 Country",            "🤠 Country", ["🤠", "🍻", "❤️", "🎸", "🎶", "🌟"]),
            new("Rock",      "🎸 Rock & Metal",       "🎸 Rock",    ["🤘", "🎸", "🔥", "⚡", "💥", "🎤"]),
            new("HipHop",    "🎤 Hip-Hop / Rap",      "🎤 Rap",     ["🎤", "💯", "🔥", "💰", "👑", "🚀"]),
            new("Ballad",    "🕯️ Ballad / Slow Jam",  "🕯️ Ballad",  ["🕯️", "💖", "🌹", "✨", "🎶", "👏"]),
            new("Party",     "🕺 Party / Dance",      "🕺 Party",   ["🕺", "💃", "🎉", "🥂", "🤩", "⚡"]),
            new("Superstar", "🏆 Superstar / Encore", "🏆 Star",    ["🏆", "👑", "💎", "⭐", "💯", "🙌"])
        ];

        private static readonly HashSet<string> AllKnownEmojis =
        [
            .. All.SelectMany(s => s.Emojis),
            // Legacy mobile.html reaction emojis kept valid in PatronChoice mode
            "🥳", "🤩", "💯", "🙌", "❤️", "🔥", "👏", "🎉", "👑", "🎸", "🎤"
        ];

        /// <summary>Choices for the DJ Show Emoji Theme dropdown in Settings and Remote DJ.</summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Options { get; } =
        [
            new(PatronChoice, "🎭 Patron's Choice (All 7 Sets)"),
            new("Classic",    "🌟 Classic Only (❤️ 🔥 👏 🙌 🎉 👑)"),
            new("Country",    "🤠 Country Night (🤠 🍻 ❤️ 🎸 🎶 🌟)"),
            new("Rock",       "🎸 Rock & Metal Night (🤘 🎸 🔥 ⚡ 💥 🎤)"),
            new("HipHop",     "🎤 Hip-Hop / Rap Night (🎤 💯 🔥 💰 👑 🚀)"),
            new("Ballad",     "🕯️ Ballad / Slow Jam Night (🕯️ 💖 🌹 ✨ 🎶 👏)"),
            new("Party",      "🕺 Party & Dance Night (🕺 💃 🎉 🥂 🤩 ⚡)"),
            new("Superstar",  "🏆 Superstar / Encore Night (🏆 👑 💎 ⭐ 💯 🙌)"),
            new(Off,          "🚫 Off (Disable Reactions)")
        ];

        /// <summary>Normalizes a persisted or incoming theme key to a known option key.</summary>
        public static string NormalizeThemeKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return PatronChoice;
            }

            if (string.Equals(key, Off, StringComparison.OrdinalIgnoreCase))
            {
                return Off;
            }

            if (string.Equals(key, PatronChoice, StringComparison.OrdinalIgnoreCase))
            {
                return PatronChoice;
            }

            var match = All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
            return match?.Key ?? PatronChoice;
        }

        /// <summary>Finds a specific emoji reaction set by key, or null if not a locked set.</summary>
        public static EmojiReactionSet? GetSet(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Validates whether an incoming emoji from a patron phone is permitted under the active DJ Show Theme.
        /// When the DJ locks the show to a specific theme (Option A), only that theme's 6 emojis are allowed.
        /// </summary>
        public static bool IsAllowedEmoji(string? emoji, string? activeThemeKey)
        {
            if (string.IsNullOrWhiteSpace(emoji))
            {
                return false;
            }

            string normalizedTheme = NormalizeThemeKey(activeThemeKey);
            if (normalizedTheme == Off)
            {
                return false;
            }

            string trimmed = emoji.Trim();
            if (normalizedTheme == PatronChoice)
            {
                return AllKnownEmojis.Contains(trimmed);
            }

            var lockedSet = GetSet(normalizedTheme);
            return lockedSet != null && lockedSet.Emojis.Contains(trimmed, StringComparer.Ordinal);
        }

        /// <summary>Accent glow color (#RRGGBB) used when rendering a floating reaction glyph on the projection canvas.</summary>
        public static string GetGlowHex(string? emoji) => emoji?.Trim() switch
        {
            "❤️" or "💖" or "🌹" => "#FF1744",
            "🔥" or "💥" or "🕯️" => "#FF6D00",
            "👑" or "🏆" or "⭐" or "🌟" or "✨" or "🥂" or "🍻" or "⚡" => "#FFD600",
            "💎" or "🚀" or "🎶" => "#00E5FF",
            "💰" or "💯" => "#00E676",
            "🎸" or "🤘" or "🎤" or "🕺" or "💃" or "🎉" => "#D500F9",
            _ => "#FF4081"
        };
    }
}

