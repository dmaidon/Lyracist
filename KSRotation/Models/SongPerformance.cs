// Edited on Jul 31, 2026 @ 12:08:52 -> Added IsMusic property to track background music track performances
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
        public bool IsMusic { get; set; }
    }
}
