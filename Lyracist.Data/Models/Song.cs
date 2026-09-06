// Edited on Sep 6, 2026 @ 11:22:00 -> Add Difficulty, Key, BPM, VocalPresence, and Quality properties
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
        public string Genre { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public double Duration { get; set; }
        public int KeyDefault { get; set; }
        public double TempoDefault { get; set; }
        public double? MeasuredLoudnessLufs { get; set; }
        public string? Difficulty { get; set; }
        public string? Key { get; set; }
        public double? BPM { get; set; }
        public string? VocalPresence { get; set; }
        public string? Quality { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
        public DateTime? LastPlayed { get; set; }
        public int PlayCount { get; set; }

        // Navigation Properties
        public SongAudioSettings? AudioSettings { get; set; }
        public ICollection<RotationEntry> RotationEntries { get; set; } = [];
    }
}
