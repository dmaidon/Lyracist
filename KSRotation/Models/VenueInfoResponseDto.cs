// Edited on Oct 30, 2026 @ 06:10:00 -> Add scaryokeCategories to VenueInfoResponseDto for manual Scaryoke category selection in dj.html
namespace KSRotation.Models
{
    /// <summary>
    /// Serialized holiday theme configuration for <c>billboard.html</c> and remote displays.
    /// </summary>
    public sealed class HolidayThemeDto
    {
        public string key { get; set; } = string.Empty;
        public string display { get; set; } = string.Empty;
        public string vignetteColor { get; set; } = string.Empty;
        public string fogColor { get; set; } = string.Empty;
        public double fogOpacity { get; set; }
        public bool flicker { get; set; }
        public string[] crossers { get; set; } = [];
        public string[] ambient { get; set; } = [];
        public string ambientMotion { get; set; } = "Fall";
        public double crossSeconds { get; set; } = 7;
        public double ambientSeconds { get; set; } = 1.0;
        public string[] palette { get; set; } = [];
    }

    /// <summary>
    /// Metadata payload for the <c>/api/info</c> endpoint consumed by <c>billboard.html</c> and patron web clients.
    /// </summary>
    public sealed class VenueInfoResponseDto
    {
        public string venue { get; set; } = string.Empty;
        public string dj { get; set; } = string.Empty;
        public string portalUrl { get; set; } = string.Empty;
        public string billboardUrl { get; set; } = string.Empty;
        public string wifiSsid { get; set; } = string.Empty;
        public string wifiPassword { get; set; } = string.Empty;
        public bool isLastRound { get; set; }
        public bool showQrCode { get; set; } = true;
        public bool listDjAndVenue { get; set; } = true;
        public double defaultSongLengthMinutes { get; set; } = 4.75;
        public string projectionView { get; set; } = "Normal List";
        public string holidayTheme { get; set; } = "None";
        public HolidayThemeDto? activeHoliday { get; set; }
        public bool isScaryokeActive { get; set; }
        public ScaryokeStateDto? scaryokeState { get; set; }
        public string[] scaryokeCategories { get; set; } = [];
    }
}

