using System;

namespace Lyracist.Data.Models
{
    public class RotationEntry
    {
        public int RotationEntryId { get; set; }
        public int SingerId { get; set; }
        public int SongId { get; set; }
        public string Status { get; set; } = "Queued"; // Queued, Singing, Finished
        public int Position { get; set; }
        public int RequestedKey { get; set; }
        public double RequestedTempo { get; set; }
        public DateTime TimestampAdded { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public Singer? Singer { get; set; }
        public Song? Song { get; set; }
    }
}
