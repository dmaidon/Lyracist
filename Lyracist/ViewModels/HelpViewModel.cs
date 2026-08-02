// Edited on Aug 2, 2026 @ 07:50:00 -> Add Casting & DJ Banners topic, update online search, background music requests, singer edit, screen lock details, and book exporter
// Edited on Jul 19, 2026 @ 09:40:00 -> Add external mixer setup guide to HelpPage
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lyracist.ViewModels;

public class HelpTopic
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#38ef7d";
    public string DescriptionHeader { get; set; } = string.Empty;
    public string DescriptionContent { get; set; } = string.Empty;
}

public partial class HelpViewModel : BaseViewModel
{
    [ObservableProperty]
    private ObservableCollection<HelpTopic> _helpTopics;

    [ObservableProperty]
    private HelpTopic? _selectedTopic;

    public HelpViewModel()
    {
        _helpTopics =
        [
            new() {
                Title = "1. Karaoke Page (Dashboard)",
                Icon = "PlayCircle24",
                AccentColor = "#107C41",
                DescriptionHeader = "Media Playback Controls & Search Library",
                DescriptionContent = "• Search Box & Library: Type artist or title keywords to query the local SQLite database and active streaming providers (YouTube, Party Tyme) in real-time. Use filters to narrow results.\n\n• Direct Queueing: Click the '+' (Add) button next to any track in search results to instantly queue it, avoiding copy-paste operations.\n\n• Singer Selection: Click on a singer in the rotation list to select them as the current singer (highlighted in red). Click the singer again to deselect, allowing you to easily correct misclicks.\n\n• Playback controls: Play, Pause, Stop, Seek Slider, and Volume. The active lyrics projection window displays synchronized CDG frames or MP4 video streams."
            },
            new() {
                Title = "2. Rotation Page",
                Icon = "People24",
                AccentColor = "#0078D4",
                DescriptionHeader = "Singer Rotation Management & Scaryoke Mode",
                DescriptionContent = "• Add to Rotation: Enter the Performer Name, select a song, specify key transposition semitones (e.g. +2 for higher, -3 for lower), type custom notes, and click Add. Supports up to 4 singers in Rotation.\n\n• Queue Ordering: Use the Up/Down arrow buttons next to a performer's entry to adjust execution priority, or click Remove to dismiss them from the queue.\n\n• Paused & Inactive (Deleted) Singer Controls: Pause a singer to skip them in rotation while retaining their position, or mark them inactive ('delete' them) to move them to the end of the queue. Click 'Restore' to reactivate them and return them to the end of the active section.\n\n• Performer Editing: Double-click any singer row or select Edit to open the Singer Edit dialog to adjust the performer's name, requested track, transposition key, or custom notes dynamically.\n\n• Scaryoke Selection Wheel: Toggle Scaryoke to show the spin wheel. When spun, it randomly picks a genre/challenge (Elvis, Rock, Country, Pop, Gender Bender, Oldies) and forces a random database song from that category onto the singer."
            },
            new() {
                Title = "3. Playlists Page",
                Icon = "MusicNote2Play20",
                AccentColor = "#10893E",
                DescriptionHeader = "Background Music (BGM) Channels",
                DescriptionContent = "• Playlist Channels: Manage Opening Music (plays before the show), Fill-In Music (auto-plays in gaps between performer tracks), and End-of-Rotation Music (plays when the singer list is completed).\n\n• Adding Tracks: Browse indexed files in the Library column and click Add next to the target playlist. Use the playlist grid control to manage files.\n\n• Show Flow Automation: BGM players interact automatically with the karaoke player: BGM pauses/ducks when a performer's track starts, and resumes immediately when it stops."
            },
            new() {
                Title = "4. Requests Page",
                Icon = "MailInboxArrowDown20",
                AccentColor = "#E81123",
                DescriptionHeader = "Mobile Performer Request Approvals",
                DescriptionContent = "• Incoming Request Grid: Displays real-time song submissions sent by performers using their mobile portal.\n\n• Karaoke vs. Background Music: Requests are labeled as standard Karaoke (purple badge) or Background Music (green [MUSIC] badge). Singer rotation allows one active karaoke request and one background music request slot concurrently.\n\n• Music Request Automation: Background music requests show up in bold green in the rotation list. Completed music tracks are automatically removed from rotation upon completion and are excluded from standard performer stats. Round checkboxes are replaced by a 'Background Music' label for these tracks.\n\n• Approval Workflow: Review details (Performer, Song, Key offset, notes). Click Approve to instantly queue the singer and song in the main rotation list, or click Decline to reject the submission."
            },
            new() {
                Title = "5. Settings: Display & Projectors",
                Icon = "WindowAd24",
                AccentColor = "#DFB900",
                DescriptionHeader = "Dual-Screen Monitor Target Assignments",
                DescriptionContent = "• Lyrics Projection Screen: Choose the target monitor index for the borderless singer lyrics window.\n\n• Rotation Billboard Screen: Choose the monitor index for the audience rotation list.\n\n• Billboard View Mode: Choose between 'Normal List', 'Star Wars Crawl', 'Vegas Marquee', or 'Vinyl Turntable' view modes. This control is available on both the Settings page and the main Karaoke control page for quick switching during a show.\n\n• Crawl Intro Text Template: When Star Wars Crawl is active, choose from 3 canned templates (Dramatic, Comedic, Over-the-Top) or input a custom template.\n\n• DJ & Venue Variables: Customize DJ / Host Name and manage the Venue listbox. In both the marquee and crawl screens, any occurrences of '{dj}' and '{venue}' are replaced dynamically.\n\n• Show Banner Control: Use the 'Show Banner' checkbox on the Karaoke dashboard page to dynamically show or hide the audience scrolling queue marquee on the projection window.\n\n• Mirror Lyrics: Enable to horizontally flip the CDG graphics pixels for rear-projection screen setups."
            },
            new() {
                Title = "6. Settings: Audio & DSP Engine",
                Icon = "Settings24",
                AccentColor = "#A700EC",
                DescriptionHeader = "Global Equalizer, Compressor, and Peak Limiter",
                DescriptionContent = "• Global EQ defaults: 3-band tone balancing. Adjust Bass (low frequencies), Midrange (vocal clarity), and Treble (vocal presence) sliders from -20dB to +20dB. Click Reset to revert to default settings.\n\n• Global Key/Tempo: Sets default transpositions (semitone adjustments) and playback speeds.\n\n• Dynamic Compressor: Evens out voice fluctuations by boosting quiet sections and ducking loud parts.\n\n• Peak Limiter: Clamps sound levels to prevent digital clipping, microphone feedback, or hardware speaker damage."
            },
            new() {
                Title = "7. Settings: Service API Keys",
                Icon = "Globe24",
                AccentColor = "#00B7C3",
                DescriptionHeader = "Logins and API Integration Configurations",
                DescriptionContent = "• YouTube API Key: Enter your Google developer key to query the YouTube karaoke catalog directly.\n\n• Spotify Client ID & Secret: Enter client credentials to fetch album artwork and song recommendations.\n\n• Amazon Access & Secret Keys: Enables lookups on the Amazon Music platform.\n\n• Party Tyme Client ID & Secret: Input subscription credentials to authenticate the Party Tyme premium karaoke streaming catalog search and playback."
            },
            new() {
                Title = "8. Settings: Tablet Web Server",
                Icon = "Server24",
                AccentColor = "#8764B8",
                DescriptionHeader = "SignalR Performer Portal Host Port Settings",
                DescriptionContent = "• SignalR Server Port: Sets the hosting socket port (default: 5005). Ensure the port is open in Windows Defender Firewall.\n\n• Local Wi-Fi & Travel Routers: Host a private offline network by connecting your laptop and performers' devices to a local Wi-Fi travel router. Performers can connect to http://[Your-Laptop-IP]:5005 without needing any internet connection.\n\n• Live Performer Portal & Online Search: Once logged in, performers can search the local catalog or search online song catalogs (querying the iTunes search index in real-time via their mobile browser) to submit song requests, view rotation queues, and view/spin the synchronized Scaryoke wheel. A graceful offline fallback message is shown if the phone has no internet connection.\n\n• Remote DJ Console Lock: The web-based remote DJ console features a secure screen lock, togglable QR code visibility, and a secure DJ PIN to prevent unauthorized modifications by patrons.\n\n• Start/Stop Broadcast: Toggles hosting of the performer web portal."
            },
            new() {
                Title = "9. Settings: Library & Decoders",
                Icon = "FolderAdd24",
                AccentColor = "#F7630C",
                DescriptionHeader = "Media Scanner and Playback Rendering Decoders",
                DescriptionContent = "• CDG Canvas Scaling: Select pixel scaling interpolation (Point, Bilinear, Bicubic). Point provides sharp pixels; Bicubic provides smooth edges.\n\n• MP4 Video Decoder: Configures default video hardware acceleration backends (VLC / Native).\n\n• Target Frame Rate: Set graphics rendering updates (15 FPS to 60 FPS). Higher settings result in smoother lyrics scrolling at the expense of CPU usage.\n\n• Library Scanning Directories: Add local drives or folders. Click 'Rescan All Directories' to parse and index files (.zip, .mp3, .cdg, .mp4) into the FTS5 search engine.\n\n• Catalog Book Generator: Run the Database Manager (LyracistDbEditor) to export your entire indexed song library into printable, formatted Word (.docx) or PDF catalogs sorted alphabetically by artist."
            },
            new() {
                Title = "10. Settings: Database Maintenance",
                Icon = "Database24",
                AccentColor = "#0078D4",
                DescriptionHeader = "Database Live Backups, Restores, and Optimizations",
                DescriptionContent = "• SQLite Optimization: Runs database engines under Write-Ahead Logging (WAL mode) and shared caching to speed up concurrent file access.\n\n• Backup DB: Launches a Save File dialog and runs the native SQLite 'VACUUM INTO' operation. This safely exports a compressed copy of all playlists, history, and configuration data to an external file.\n\n• Restore DB: Launches an Open File dialog. When a backup is selected, Lyracist closes active DB connections, overwrites the active DB, purges WAL/SHM cache logs to prevent corruption, and shuts down the application safely for a manual reboot."
            },
            new() {
                Title = "11. Settings: BGM & Special Occasions",
                Icon = "Calendar24",
                AccentColor = "#00B294",
                DescriptionHeader = "Background Music Channels & Occasion Jingle Settings",
                DescriptionContent = "• BGM Volumes: Adjust default audio volumes specifically for Opening, Fill-In, and End-of-Rotation music players.\n\n• Special Occasions Category: Manage occasions (Holidays, Weddings, Birthdays) and assign local audio files. Set individual Bass, Treble, and Gain overrides for each occasion track and click Save."
            },
            new() {
                Title = "12. Settings: Scaryoke Customization",
                Icon = "PlayCircle24",
                AccentColor = "#FF8E53",
                DescriptionHeader = "Customizing Wheel Segments & Genres",
                DescriptionContent = "• Wheel Categories: View and edit the list of segments displayed on the Scaryoke wheel. You can customize the name of each category (e.g., '90s Pop', 'Metallica', 'Dolly Parton').\n\n• Category Bounds: Enforces a maximum of 8 categories (for wheel design formatting) and a minimum of 2 categories (to ensure a valid choice is selectable).\n\n• Performer Portal Synchronization: The Scaryoke wheel is completely synchronized over Wi-Fi with connected tablets and mobile phones. When spun, the canvas-based wheel on the performer's device spins and lands on the exact same category as the DJ's screen.\n\n• Remote Spin Request: Performers can trigger the Scaryoke wheel directly from their mobile portal by clicking 'SPIN WHEEL', launching the wheel animation on the DJ's monitor in real time."
            },
            new() {
                Title = "13. External Mixer Setup (e.g. Pyle PMXU88BT)",
                Icon = "Settings24",
                AccentColor = "#DFB900",
                DescriptionHeader = "Integrating Hardware Audio Mixers",
                DescriptionContent = "• Hardware Mixing Philosophy: When using a hardware mixer like the Pyle PMXU88BT, the singers' microphones are connected directly to the mixer's physical inputs (XLR Channels 1-4). Sound levels, microphone EQ (High/Mid/Low knobs), and microphone effects (Delay/Repeat) must be adjusted physically on the mixer, not in Lyracist software.\n\n• Hardware Mixer Mode: Toggle 'Enable Hardware Mixer Mode' in settings to flatten Lyracist's software equalizer, limiter, and compressor, and output at 100% volume. This prevents 'double-equalizing' or 'double-compressing' your music, giving you a clean, unprocessed output from your PC to mix physically.\n\n• Separate Karaoke & BGM Buses: Route Karaoke output and BGM (Background Music) to separate channels on your mixer (e.g. Karaoke to USB/Line-In 5/6 and BGM to Bluetooth 7/8). This allows you to crossfade and EQ them independently using physical mixer faders.\n\n• Live Recording: The Pyle's USB connection is primarily for flash drives. To record performances, route the mixer's 'Main Out' or 'Phones Out' back into your PC's Line-In or a USB audio capture adapter, and capture using a local recording device."
            },
            new() {
                Title = "14. Wireless Casting & DJ Banners",
                Icon = "Cast24",
                AccentColor = "#0078D4",
                DescriptionHeader = "Casting the Rotation and Custom DJ Banners",
                DescriptionContent = "• Wireless Casting: Navigate to the Casting page in the sidebar menu. Choose a casting target (Monitor, Miracast, Chromecast, Browser Cast, AirPlay, or Wireless HDMI). Click 'Cast Rotation' to initialize casting using high-performance off-screen buffer rendering, leaving your host desktop clutter-free.\n\n• Chromecast Selection: When casting to Chromecast, you can select from scanned local devices and click 'Rescan' to query again. Make sure the Chromecast device is on the same local network.\n\n• Browser Cast Server: Self-hosts a real-time rotation page on http://[Your-Laptop-IP]:8080/rotation/ that can be opened in any browser on the local Wi-Fi network.\n\n• Custom DJ Banners: Set up borderless, full-screen DJ branding/promotional banners (PNG, JPG, JPEG, GIF, BMP, and looping MP4 video) to display on a selected monitor. Manage banners (Upload, Select, Delete) on the Settings page or the main Karaoke control page.\n\n• Same-Screen Deconfliction: If the DJ Banner and the Rotation Display are configured to target the same screen, the active Rotation Display takes priority, and the DJ Banner is automatically disabled/hidden to prevent visual collision."
            }
        ];

        _selectedTopic = _helpTopics[0];
    }
}
