// Edited on Sep 6, 2026 @ 08:54:50 -> Add Session Schedule and Last Request Cutoff properties
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

        public string ConnectInstructionsScreen { get; init; } = "All Screens / Monitors";

        public string SelectedDjBannerPath { get; init; } = string.Empty;

        public bool IsDjBannerEnabled { get; init; } = false;

        public bool IsDjBannerQrCodeEnabled { get; init; } = true;

        public bool ShowQrCodeOnRotationScreen { get; init; } = true;

        public string WifiPassword { get; init; } = string.Empty;

        public string ActiveSpecialEvent { get; init; } = "None";

        public System.Collections.Generic.List<SpecialEventConfig> SpecialEvents { get; init; } =
        [
            new() { EventName = "Birthday", BannerFileName = "Birthday.png" },
            new() { EventName = "Wedding", BannerFileName = "Wedding.png" },
            new() { EventName = "Engagement", BannerFileName = "Engagement.png" },
            new() { EventName = "Anniversary", BannerFileName = "Anniversary.png" },
            new() { EventName = "Last Song", BannerFileName = "LastSong.png" },
            new() { EventName = "Connect Instructions", BannerFileName = "ConnectInstructions.png" }
        ];

        /// <summary>The display target type (Monitor, Chromecast, Miracast, BrowserCast, etc.)</summary>
        public DisplayTarget RotationTarget { get; init; } = DisplayTarget.Monitor;

        /// <summary>When true, incoming patron requests are added straight to the rotation instead of waiting for DJ approval.</summary>
        public bool AutoAcceptRequests { get; init; } = false;

        /// <summary>When true, the current singer always floats to the top of the rotation list.</summary>
        public bool FloatCurrentSingerToTop { get; init; } = false;

        /// <summary>Default estimated song length in minutes, used by the rotation-screen
        /// "estimated wait time" badge whenever a queued song's actual duration isn't known.</summary>
        public double DefaultSongLengthMinutes { get; init; } = 4.75;

        /// <summary>Whether the rotation-screen "estimated wait time" badge is shown at all. Some
        /// DJs prefer not to display wait estimates to the audience; defaults on.</summary>
        public bool ShowEstimatedWaitTime { get; init; } = true;

        /// <summary>When true, patrons cannot request songs via the portal that have already been performed or queued in the current session.</summary>
        public bool BlockDuplicateSongsInSession { get; init; } = false;

        /// <summary>When true, song requests from patron portal and kiosk are restricted to scheduled session hours.</summary>
        public bool EnableSessionSchedule { get; init; } = false;

        /// <summary>Start time of the scheduled session (e.g. "8:00 PM" or "20:00").</summary>
        public string SessionStartTime { get; init; } = "8:00 PM";

        /// <summary>Stop time of the scheduled session (e.g. "2:00 AM" or "02:00").</summary>
        public string SessionStopTime { get; init; } = "2:00 AM";

        /// <summary>When true, song requests from patron portal and kiosk are cut off at the specified last request time.</summary>
        public bool EnableLastRequestTime { get; init; } = false;

        /// <summary>Cutoff time for receiving new song requests (e.g. "1:30 AM" or "01:30").</summary>
        public string LastRequestTime { get; init; } = "1:30 AM";
    }
}
