// Edited on Oct 2, 2026 @ 10:55:00 -> Add WelcomeScreenDesign to AppSettings
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

        /// <summary>When true, displays the DJ name and Venue on the billboard header and external displays.</summary>
        public bool ListDjAndVenueOnBillboard { get; init; } = true;

        /// <summary>Optional manual IPv4 host override used when composing the patron portal URL and QR code.</summary>
        public string PreferredHostIp { get; init; } = string.Empty;

        /// <summary>Persistent DJ connection login PIN.</summary>
        public string DjPin { get; init; } = string.Empty;

        /// <summary>The DeviceName of the selected monitor for the rotation display.</summary>
        public string SelectedMonitorDevice { get; init; } = string.Empty;

        public string DjBannerMonitorDevice { get; init; } = string.Empty;

        public string ConnectInstructionsScreen { get; init; } = "All Screens / Monitors";

        public string SelectedDjBannerPath { get; init; } = string.Empty;

        /// <summary>DJ banner (image or video) optionally shown in the Stadium Jumbotron
        /// projection view's bottom sponsor box. Empty shows a "tip your bartenders and DJ" notice instead.</summary>
        public string SelectedJumbotronBannerPath { get; init; } = string.Empty;

        public bool IsDjBannerEnabled { get; init; } = false;

        public bool IsDjBannerQrCodeEnabled { get; init; } = true;

        public bool WelcomeScreenEnabled { get; init; } = true;

        public int WelcomeScreenSeconds { get; init; } = 15;

        public string WelcomeScreenMonitor { get; init; } = string.Empty;
        public int WelcomeScreenDesign { get; init; } = -1;

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
        public bool FloatCurrentSingerToTop { get; init; } = true;

        /// <summary>Default estimated song length in minutes, used by the rotation-screen
        /// "estimated wait time" badge whenever a queued song's actual duration isn't known.</summary>
        public double DefaultSongLengthMinutes { get; init; } = 4.75;

        /// <summary>Whether the rotation-screen "estimated wait time" badge is shown at all. Some
        /// DJs prefer not to display wait estimates to the audience; defaults on.</summary>
        public bool ShowEstimatedWaitTime { get; init; } = true;

        /// <summary>"Reduced projection effects" for weaker venue PCs: the projection display's themed
        /// views use fewer particles and skip per-element blur effects.</summary>
        public bool ReducedProjectionEffects { get; init; }

        /// <summary>Audio input (mixer line-in, USB interface or mic) the synth bars listen to. Empty = system default input.</summary>
        public string SpectrumInputDeviceId { get; init; } = string.Empty;

        /// <summary>Color theme of the synth bars (same names as Lyracist: Neon Sunset, Cyberpunk, ...).</summary>
        public string SpectrumStyle { get; init; } = "Neon Sunset";

        /// <summary>How strongly quiet input fills the synth bars (0.25 - 4).</summary>
        public double SpectrumSensitivity { get; init; } = 1.0;

        /// <summary>Show the live synth bars on the rotation (singer display) screen.</summary>
        public bool SpectrumOnRotation { get; init; }

        /// <summary>Show the live synth bars on the DJ banner screen while a regular DJ banner is up.</summary>
        public bool SpectrumOnDjBanners { get; init; }

        /// <summary>Show the live synth bars on the banner screen while a special event banner is active.</summary>
        public bool SpectrumOnSpecialEvents { get; init; }

        /// <summary>When true, patrons cannot request songs via the portal that have already been performed or queued in the current session.</summary>
        public bool BlockDuplicateSongsInSession { get; init; } = true;

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

        /// <summary>When true, this device automatically switches to the in-app Remote DJ view (dj.html) when its session is transferred to or pulled by a peer.</summary>
        public bool AutoSwitchToRemoteDjOnHandoff { get; init; } = true;

        /// <summary>When true, the projection display automatically cycles through the enabled entries
        /// in <see cref="ProjectionRotationSchedule"/> throughout the night instead of staying on one
        /// fixed screen.</summary>
        public bool AutoRotateProjectionViews { get; init; }

        /// <summary>How many seconds each randomly chosen screen stays up during automatic screen rotation.</summary>
        public int AutoRotateDurationSeconds { get; init; } = 180;

        /// <summary>Which projection views participate in the automatic rotation.
        /// Populated with one entry per known view (see MainViewModel.ProjectionViews).</summary>
        public System.Collections.Generic.List<ProjectionRotationEntry> ProjectionRotationSchedule { get; init; } = [];
    }
}
