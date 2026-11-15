// Edited on Nov 14, 2026 @ 09:14:20 -> Add Venues and IsDistinctName properties to Singer model
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
        public int Score { get; set; } = 0;
        public int RatingPoints { get; set; } = 0;
        public int RatingCount { get; set; } = 0;
        public double AverageRating { get; set; } = 0.0;

        public string Email { get; set; } = string.Empty;
        public string PinCode { get; set; } = string.Empty;
        public string AvatarType { get; set; } = "None";
        public string AvatarSource { get; set; } = string.Empty;
        public string VocalRange { get; set; } = string.Empty;
        public string CustomTitle { get; set; } = string.Empty;
        public bool AllowRecording { get; set; } = false;
        public string Venues { get; set; } = string.Empty;
        public bool IsDistinctName { get; set; } = false;

        // Navigation Properties
        public SingerAudioSettings? AudioSettings { get; set; }
        public ICollection<RotationEntry> RotationEntries { get; set; } = [];
        public ICollection<MusicRequest> MusicRequests { get; set; } = [];
    }
}
