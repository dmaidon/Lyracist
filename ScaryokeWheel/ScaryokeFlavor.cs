// Created on Oct 28, 2026 @ 09:00:00 -> Halloween flavor data: curse twists, fortune lines, and the built-in Halloween category preset
using System;
using System.Collections.Generic;

namespace ScaryokeWheel;

public static class ScaryokeFlavor
{
    public const string HalloweenPresetName = "🎃 Halloween";

    /// <summary>Second-spin twists. Kept short so they fit on the singer badge.</summary>
    public static readonly string[] Curses =
    [
        "Monster voice",
        "Eyes closed",
        "Pirate accent",
        "Whisper the verses",
        "Dance the chorus",
        "Sing it as a ghost",
        "Zombie walk to the mic",
        "Opera style",
        "Evil laugh before the chorus",
        "Hold the mic like a wand",
        "Duet with a stranger",
        "Haunted-house cackle",
        "No curse - you're spared!"
    ];

    public static readonly string[] HalloweenCategories =
    [
        "Monster Mash", "Ghostbusters", "Thriller", "Addams Family", "Spooky Rock", "Villain Songs",
        "Elvis", "Rock", "Oldies", "80s Hits", "Disney Songs"
    ];

    private static readonly string[] FortuneTemplates =
    [
        "The spirits foretell a {0} triumph for {1}...",
        "{1}, the crystal ball whispers of {0}...",
        "A chill runs down the spine: {1} is destined for {0}!",
        "The cauldron bubbles... {0} awaits {1}.",
        "The ghosts have chosen {0} for {1}. Do not resist.",
        "Beware, {1}... {0} is written in the stars."
    ];

    private static readonly string[] DjFortunes =
    [
        "The DJ's shadow falls upon {0}...",
        "Dark forces hand {0}'s fate to the DJ!",
        "{0}, the DJ has been waiting for you..."
    ];

    private static readonly string[] FreeFortunes =
    [
        "The spirits grant {0} total freedom... for now.",
        "{0}, tonight the song is yours to choose!"
    ];

    public static string Fortune(string category, string? singer)
    {
        string name = string.IsNullOrWhiteSpace(singer) ? "the singer" : singer;
        if (string.Equals(category, "DJ's Choice", StringComparison.OrdinalIgnoreCase))
            return string.Format(DjFortunes[Random.Shared.Next(DjFortunes.Length)], name);
        if (string.Equals(category, "Singer's Choice", StringComparison.OrdinalIgnoreCase))
            return string.Format(FreeFortunes[Random.Shared.Next(FreeFortunes.Length)], name);
        return string.Format(FortuneTemplates[Random.Shared.Next(FortuneTemplates.Length)], category, name);
    }

    public static string RandomCurse() => Curses[Random.Shared.Next(Curses.Length)];

    /// <summary>The "spared" outcome is not a real curse and must not show on a badge.</summary>
    public static bool IsRealCurse(string? curse) =>
        !string.IsNullOrWhiteSpace(curse) && !curse.StartsWith("No curse", StringComparison.OrdinalIgnoreCase);

    /// <summary>Adds the Halloween preset if the user doesn't already have one. Returns true when added.</summary>
    public static bool EnsureHalloweenPreset(ScaryokeSettings settings)
    {
        if (settings.Presets.Exists(p => string.Equals(p.Name, HalloweenPresetName, StringComparison.OrdinalIgnoreCase)))
            return false;

        settings.Presets.Add(new CategoryPreset
        {
            Name = HalloweenPresetName,
            Categories = [.. HalloweenCategories],
            Styles = new Dictionary<string, CategoryStyle>
            {
                ["Monster Mash"] = new() { Weight = 2, Color = "#2E7D32" },
                ["Thriller"] = new() { Weight = 2, Color = "#B71C1C" },
                ["Ghostbusters"] = new() { Color = "#00838F" },
                ["Addams Family"] = new() { Color = "#4527A0" },
                ["Spooky Rock"] = new() { Color = "#FF6D00" },
                ["Villain Songs"] = new() { Color = "#6A0DAD" }
            }
        });
        return true;
    }
}
