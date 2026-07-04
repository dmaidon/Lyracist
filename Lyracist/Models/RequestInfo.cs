using System;

namespace Lyracist.Models;

/// <summary>App-side view of a MusicRequest row, flattened for binding.</summary>
public class RequestInfo
{
    public int Id { get; set; }
    public string SingerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Source { get; set; } = "Local";
    public string Status { get; set; } = "Pending";
    public DateTime Timestamp { get; set; }
}
