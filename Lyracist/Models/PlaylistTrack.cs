// Edited on Oct 7, 2026 @ 19:44:00 -> Add Bpm property to PlaylistTrack
namespace Lyracist.Models;

/// <summary>A single entry in the Opening or Fill-In background music playlist.</summary>
public class PlaylistTrack
{
    public int ItemId { get; set; }
    public int SongId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string AudioPath { get; set; } = string.Empty;
    public int Order { get; set; }
    public double? Bpm { get; set; }
}
