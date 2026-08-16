// Created on Aug 15, 2026 @ 10:20:00 -> PerformedSong model for session performance history
using System;

namespace Lyracist.Core.Models;

public class PerformedSong
{
    public int OrderNumber { get; set; }
    public string SingerName { get; set; } = string.Empty;
    public string SongTitle { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Key { get; set; } = "0";
    public DateTime PerformedAt { get; set; } = DateTime.Now;

    public string DisplayTime => PerformedAt.ToString("h:mm tt");
    public bool HasKeyChange => Key != "0" && !string.IsNullOrWhiteSpace(Key);
    public string FormattedText => $"#{OrderNumber:D2} | {SingerName} - \"{SongTitle}\"{(string.IsNullOrWhiteSpace(Artist) ? "" : $" ({Artist})")}{(HasKeyChange ? $" [Key: {Key}]" : "")} - {DisplayTime}";

    /// <summary>
    /// Index (0-4) used for 5-color rotation palettes.
    /// </summary>
    public int ColorIndex => (Math.Max(1, OrderNumber) - 1) % 5;
}
