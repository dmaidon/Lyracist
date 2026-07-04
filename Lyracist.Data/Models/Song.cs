using System;
using System.Collections.Generic;

namespace Lyracist.Data.Models
{
    public class Song
    {
        public int SongId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public bool IsKaraoke { get; set; }
        public string KaraokeType { get; set; } = string.Empty; // MP3G, MP4, ZIPCDG
        public double Duration { get; set; }
        public int KeyDefault { get; set; }
        public double TempoDefault { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
        public DateTime? LastPlayed { get; set; }
        public int PlayCount { get; set; }

        // Navigation Properties
        public SongAudioSettings? AudioSettings { get; set; }
        public ICollection<RotationEntry> RotationEntries { get; set; } = new List<RotationEntry>();
    }
}
