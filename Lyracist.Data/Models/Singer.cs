// Edited on Oct 7, 2026 @ 19:44:00 -> Add AllowRecording property for optional performance recording
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

        // Navigation Properties
        public SingerAudioSettings? AudioSettings { get; set; }
        public ICollection<RotationEntry> RotationEntries { get; set; } = [];
        public ICollection<MusicRequest> MusicRequests { get; set; } = [];
    }
}
