using System;
using System.Collections.Generic;

namespace Lyracist.Data.Models
{
    public class Singer
    {
        public int SingerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastSang { get; set; }
        public int TotalSongsSung { get; set; }
        public string Notes { get; set; } = string.Empty;

        // Navigation Properties
        public SingerAudioSettings? AudioSettings { get; set; }
        public ICollection<RotationEntry> RotationEntries { get; set; } = new List<RotationEntry>();
        public ICollection<MusicRequest> MusicRequests { get; set; } = new List<MusicRequest>();
    }
}
