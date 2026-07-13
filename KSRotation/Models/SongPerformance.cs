// Last Edit: Jun 29, 2026 13:22 - Added SingerId (Guid) for stable singer identity; keying history on name alone was fragile.
using System;

namespace KSRotation.Models
{
    public class SongPerformance
    {
        /// <summary>Stable singer identity matching <see cref="SingerEntry.Id"/>; keyed here so renames do not lose history.</summary>
        public Guid SingerId { get; set; }
        public string SingerName { get; set; } = string.Empty;
        public string SongTitle { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public int Round { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
