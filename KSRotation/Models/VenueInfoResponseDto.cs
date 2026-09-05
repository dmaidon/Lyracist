// Edited on Sep 3, 2026 @ 23:49:25 -> Add isLastRound property to VenueInfoResponseDto

namespace KSRotation.Models
{
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
    }
}
