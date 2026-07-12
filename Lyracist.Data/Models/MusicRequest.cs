using System;

namespace Lyracist.Data.Models
{
    public class MusicRequest
    {
        public int MusicRequestId { get; set; }
        public int SingerId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Source { get; set; } = "Local"; // Local, Spotify, YouTube, etc.
        public string RequestType { get; set; } = "Karaoke"; // Karaoke (performed by the requester) or Music (just play the track)
        public string Key { get; set; } = "0";
        public string Notes { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Approved, Queued, Played, Rejected
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public Singer? Singer { get; set; }
    }
}
