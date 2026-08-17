// Edited on Aug 17, 2026 @ 12:29:30 -> Add DuetPartnerName and IsDuet to PerformedSong
using System;

namespace Lyracist.Core.Models;

public class PerformedSong
{
    public int OrderNumber { get; set; }
    public string SingerName { get; set; } = string.Empty;
    public string DuetPartnerName { get; set; } = string.Empty;
    public string SongTitle { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Key { get; set; } = "0";
    public DateTime PerformedAt { get; set; } = DateTime.Now;

    public bool IsDuet => !string.IsNullOrEmpty(DuetPartnerName) && DuetPartnerName != "None";
    public string DisplaySinger => IsDuet ? $"{SingerName} & {DuetPartnerName}" : SingerName;
    public string DisplayTime => PerformedAt.ToString("h:mm tt");
    public bool HasKeyChange => Key != "0" && !string.IsNullOrWhiteSpace(Key);
    public string FormattedText => $"#{OrderNumber:D2} | {DisplaySinger} - \"{SongTitle}\"{(string.IsNullOrWhiteSpace(Artist) ? "" : $" ({Artist})")}{(HasKeyChange ? $" [Key: {Key}]" : "")} - {DisplayTime}";

    /// <summary>
    /// Index (0-4) used for 5-color rotation palettes.
    /// </summary>
    public int ColorIndex => (Math.Max(1, OrderNumber) - 1) % 5;
}
