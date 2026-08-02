// Edited on Aug 1, 2026 @ 12:40:00 -> Add RotationTarget property to AppSettings
// Edited on Aug 1, 2026 @ 09:46:30 -> Add DJ Banner settings properties
// Edited on Jul 27, 2026 @ 22:38:00 -> Add SelectedMonitorDevice property
// Edited on Jul 16, 2026 @ 11:00:00 -> Configuration key mappings
// Last Edit: Jul 02, 2026 14:10 - Added optional PreferredHostIp setting for manual portal URL/QR host override.
using Lyracist.Shared;

namespace KSRotation.Models
{
    public sealed record AppSettings
    {
        public const string DefaultCrawlBannerText = "Tonight in a tavern far, far away known as {venue} we get together to sing...";

        public string VenueName { get; init; } = "Karaoke Night";

        public string Theme { get; init; } = "System";

        public bool IsDisplayEnabled { get; init; }

        public string BannerText { get; init; } = "Welcome to Karaoke Night!";

        /// <summary>Banner text specifically for the Star Wars crawl display.</summary>
        public string CrawlBannerText { get; init; } = DefaultCrawlBannerText;

        // Pixels per second (20–200). Default 60.
        public double MarqueeSpeed { get; init; } = 60;

        /// <summary>When true, 15 sample singers are loaded on startup.</summary>
        public bool IsTestMode { get; init; }

        /// <summary>Email address to receive the end-of-night rotation report.</summary>
        public string EmailRecipient { get; init; } = string.Empty;

        /// <summary>When true, opens the mail client automatically after saving the report.</summary>
        public bool SendEmailOnSave { get; init; }

        /// <summary>Determines which visual panel to show on the display window.</summary>
        public string ProjectionView { get; init; } = "Normal List";

        /// <summary>Controls the opacity of background watermark logos (0.0 to 1.0).</summary>
        public double WatermarkOpacity { get; init; } = 0.06;

        public string DjName { get; init; } = "Guest DJ";

        /// <summary>Optional manual IPv4 host override used when composing the patron portal URL and QR code.</summary>
        public string PreferredHostIp { get; init; } = string.Empty;

        /// <summary>Persistent DJ connection login PIN.</summary>
        public string DjPin { get; init; } = string.Empty;

        /// <summary>The DeviceName of the selected monitor for the rotation display.</summary>
        public string SelectedMonitorDevice { get; init; } = string.Empty;

        public string DjBannerMonitorDevice { get; init; } = string.Empty;

        public string SelectedDjBannerPath { get; init; } = string.Empty;

        public bool IsDjBannerEnabled { get; init; } = false;

        /// <summary>The display target type (Monitor, Chromecast, Miracast, BrowserCast, etc.)</summary>
        public DisplayTarget RotationTarget { get; init; } = DisplayTarget.Monitor;
    }
}