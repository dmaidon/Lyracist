// Edited on Aug 6, 2026 @ 07:01:27 -> Add RequestStatuses constants to replace repeated magic status strings
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
    public string RequestType { get; set; } = "Karaoke";
    public string Key { get; set; } = "0";
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = RequestStatuses.Pending;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// The MusicRequest.Status values, stored as plain strings in the DB (not a real enum, to avoid
/// touching the EF model/migrations or any XAML bindings that display Status as text). Statuses
/// flow Pending -> Approved/Rejected, and Approved -> Played; auto-accept can also send a
/// Karaoke request straight to Queued.
/// </summary>
public static class RequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Played = "Played";
    public const string Queued = "Queued";
}
