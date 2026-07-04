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
        public string Status { get; set; } = "Pending"; // Pending, Approved, Played
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public Singer? Singer { get; set; }
    }
}
