// Last Edit: Jun 30, 2026 08:40 - Named DTO for the cached /api/rotation payload (replaces an anonymous type so it can be source-generated).
namespace KSRotation.Models
{
    /// <summary>Flat, serialization-friendly view of a singer for the patron web portal's <c>/api/rotation</c> feed.</summary>
    public sealed class RotationItemDto
    {
        public string id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string song { get; set; } = string.Empty;
        public string artist { get; set; } = string.Empty;
        public bool isCurrent { get; set; }
        public bool isNext { get; set; }
        public bool isInactive { get; set; }

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
