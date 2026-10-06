// Edited on Oct 6, 2026 @ 08:52:00 -> Add duetPartnerName to RotationItemDto for DJ tablet and web portal compatibility
using System.Collections.Generic;

namespace KSRotation.Models
{
    /// <summary>Flat, serialization-friendly view of a queued song for the patron web portal's <c>/api/rotation</c> feed.</summary>
    public sealed class QueuedSongDto
    {
        public string song { get; set; } = string.Empty;
        public string artist { get; set; } = string.Empty;
    }

    /// <summary>Flat, serialization-friendly view of a singer for the patron web portal's <c>/api/rotation</c> feed.</summary>
    public sealed class RotationItemDto
    {
        public string id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string partner { get; set; } = string.Empty;
        public string duetPartnerName { get; set; } = string.Empty;
        public string song { get; set; } = string.Empty;
        public string artist { get; set; } = string.Empty;
        public bool isCurrent { get; set; }
        public bool isNext { get; set; }
        public bool isInactive { get; set; }
        public bool isPaused { get; set; }
        public bool isSkipped { get; set; }
        public bool isSpecial { get; set; }
        public bool isMusic { get; set; }
        public bool isRotationStart { get; set; }
        public bool hasSungInLastRound { get; set; }
        public string vocalRange { get; set; } = string.Empty;
        public string customTitle { get; set; } = string.Empty;
        public List<QueuedSongDto> queuedSongs { get; set; } = [];

        public bool song1Completed { get; set; }
        public bool song2Completed { get; set; }
        public bool song3Completed { get; set; }
        public bool song4Completed { get; set; }
        public bool song5Completed { get; set; }
        public bool song6Completed { get; set; }
        public bool song7Completed { get; set; }
        public bool song8Completed { get; set; }
        public bool song9Completed { get; set; }
        public bool song10Completed { get; set; }
    }
}

