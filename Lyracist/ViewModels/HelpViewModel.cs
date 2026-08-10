// Edited on Aug 10, 2026 @ 13:05:00 -> Add Topic 15 (Connect & Request Instructions Screen) and expand help topics
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
                DescriptionContent = "• Search Box & Library: Type artist or title keywords to query the local SQLite database and active streaming providers (YouTube) in real-time. Use filters to narrow results.\n\n• Direct Queueing: Click the '+' (Add) button next to any track in search results to instantly queue it, avoiding copy-paste operations.\n\n• Singer Selection: Click on a singer in the rotation list to select them as the current singer (highlighted in red). Click the singer again to deselect, allowing you to easily correct misclicks.\n\n• Manual Performer Override: Click the star toggle on any singer row to manually designate who is currently performing, overriding default sequence order.\n\n• Pending Request Bulbs: Glowing 'K' (yellow) and 'M' (neon green) indicator bulbs light up on the Karaoke header the instant a karaoke or music request arrives from a patron's device.\n\n• Theme Mode Selector: Three-way Light/Dark/System theme dropdown on the header and Settings with live system theme detection.\n\n• Playback Controls: Play, Pause, Stop, Seek Slider, and Volume. The active lyrics projection window displays synchronized CDG frames or MP4 video streams."
            },
            new() {
                Title = "2. Rotation Page",
                Icon = "People24",
                AccentColor = "#0078D4",
                DescriptionHeader = "Singer Rotation Management & Scaryoke Mode",
                DescriptionContent = "• Add to Rotation: Enter Performer Name, select a song, specify key transposition semitones (e.g. +2 for higher, -3 for lower), type custom notes, and click Add. Supports up to 4 singers in Rotation.\n\n• Queue Ordering & Rollover: Use Up/Down arrow buttons next to a performer's entry to adjust priority. 1-Click rotation advancement automatically rolls over to the top performer when the final singer finishes.\n\n• Paused & Inactive (Deleted) Singer Controls: Pause a singer to skip them in rotation while retaining their position, or mark them inactive ('delete' them) to move them to the end of the queue. Click 'Restore' to reactivate them.\n\n• Performer XP & Milestone Badges: Tracks singer history, total songs sung, and average feedback ratings to calculate XP (`XP = TotalSongsSung * 100 + Score`) and unlock visual milestone badges (e.g. Shower Singer, Karaoke Legend).\n\n• Performer Editing: Double-click any singer row or select Edit to open the Singer Edit dialog to adjust name, requested track, transposition key, or notes dynamically.\n\n• Scaryoke Selection Wheel: Toggle Scaryoke to show the spin wheel. When spun, it randomly picks a category/genre and forces a random database song onto the performer. Wheel crosses trigger synchronized pointer clicking sounds."
            },
            new() {
                Title = "3. Playlists Page",
                Icon = "MusicNote2Play20",
                AccentColor = "#10893E",
                DescriptionHeader = "Background Music (BGM) Channels",
                DescriptionContent = "• Playlist Channels: Manage Opening Music (plays before the show), Fill-In Music (auto-plays in gaps between performer tracks), and End-of-Rotation Music (plays when the singer list is completed).\n\n• Adding Tracks: Browse indexed files in the Library column and click Add next to the target playlist. Use the playlist grid control to manage files.\n\n• Configurable Fill-In Music Delay: Adjust the delay slider (0-30 seconds) in settings to control exactly when fill-in music begins after a singer track stops.\n\n• Show Flow Automation: BGM players interact automatically with the karaoke player: BGM pauses/ducks when a performer's track starts, and resumes immediately when it stops."
            },
            new() {
                Title = "4. Requests Page",
                Icon = "MailInboxArrowDown20",
                AccentColor = "#E81123",
                DescriptionHeader = "Mobile Performer Request Approvals",
                DescriptionContent = "• Incoming Request Grid: Displays real-time song submissions sent by performers using their mobile portal.\n\n• Karaoke vs. Background Music: Requests are labeled as standard Karaoke (purple badge) or Background Music (green [MUSIC] badge). Singer rotation allows one active karaoke request and one background music request slot concurrently.\n\n• Music Request Automation: Background music requests show up in bold green in the rotation list. Completed music tracks are automatically removed from rotation upon completion and are excluded from standard performer stats.\n\n• Approval Workflow: Review details (Performer, Song, Key offset, notes). Click Approve to instantly queue the singer and song in the main rotation list, or click Decline to reject the submission.\n\n• Auto-Accept Requests: Toggle in the page header to skip manual approval entirely — Karaoke requests go straight into the rotation and Music requests go straight to Approved the moment they arrive."
            },
            new() {
                Title = "5. Settings: Display & Projectors",
                Icon = "WindowAd24",
                AccentColor = "#DFB900",
                DescriptionHeader = "Dual-Screen Monitor Target Assignments & View Modes",
                DescriptionContent = "• Lyrics Projection Screen: Choose the target monitor index for the borderless singer lyrics window.\n\n• Rotation Billboard Screen: Choose the monitor index for the audience rotation list.\n\n• Billboard View Modes: Choose between 'Normal List', 'Star Wars Crawl', 'Vegas Marquee', or 'Vinyl Turntable' view modes. Switchable from Settings or Karaoke page.\n\n• Crawl Intro Text Template: Choose from 3 canned templates (Dramatic, Comedic, Over-the-Top) or input a custom template for Star Wars Crawl 3D projection.\n\n• DJ & Venue Variables: Customize DJ / Host Name and manage the Venue listbox. DJ and Venue names display dynamically in the main title bar and projection screens.\n\n• Mirror Lyrics: Enable to horizontally flip the CDG graphics pixels for rear-projection screen setups."
            },
            new() {
                Title = "6. Settings: Audio & DSP Engine",
                Icon = "Settings24",
                AccentColor = "#A700EC",
                DescriptionHeader = "Global Equalizer, Pitch Transposition, Compressor, and Peak Limiter",
                DescriptionContent = "• Active Key Transposition: Pitch shift performances from -6 to +6 semitones with sub-150ms instant seek reload applying FFmpeg pitch shifting (`asetrate` + `atempo`).\n\n• Global EQ: 3-band tone balancing. Adjust Bass (low frequencies), Midrange (vocal clarity), and Treble (vocal presence) sliders from -20dB to +20dB. Click Reset to revert.\n\n• Dynamic Compressor: Evens out voice fluctuations by boosting quiet sections and ducking loud parts.\n\n• Peak Limiter: Clamps sound levels to prevent digital clipping, microphone feedback, or hardware speaker damage."
            },
            new() {
                Title = "7. Settings: Service API Keys",
                Icon = "Globe24",
                AccentColor = "#00B7C3",
                DescriptionHeader = "Logins and API Integration Configurations",
                DescriptionContent = "• YouTube API Key: Enter your Google developer key to query the YouTube karaoke catalog directly.\n\n• Spotify Client ID & Secret: Enter client credentials to fetch album artwork and song recommendations.\n\n• Amazon Access & Secret Keys: Enables lookups on the Amazon Music platform."
            },
            new() {
                Title = "8. Settings: Tablet Web Server",
                Icon = "Server24",
                AccentColor = "#8764B8",
                DescriptionHeader = "SignalR Performer Portal Host Port Settings",
                DescriptionContent = "• SignalR Server Port: Sets the hosting socket port (default: 5005). Ensure the port is open in Windows Defender Firewall.\n\n• Local Wi-Fi & Travel Routers: Host a private offline network by connecting your laptop and performers' devices to a local Wi-Fi travel router. Performers can connect to http://[Your-Laptop-IP]:5005 without needing any internet connection.\n\n• Live Performer Portal & Online Search: Once logged in, performers can search the local catalog or search online song catalogs (querying the iTunes search index in real-time via their mobile browser) to submit song requests, view rotation queues, and view/spin the synchronized Scaryoke wheel.\n\n• Remote DJ Console Lock: The web-based remote DJ console features a secure screen lock, togglable QR code visibility, and a secure DJ PIN to prevent unauthorized modifications by patrons.\n\n• Start/Stop Broadcast: Toggles hosting of the performer web portal."
            },
            new() {
                Title = "9. Settings: Library & Decoders",
                Icon = "FolderAdd24",
                AccentColor = "#F7630C",
                DescriptionHeader = "Media Scanner and Playback Rendering Decoders",
                DescriptionContent = "• CDG Canvas Scaling: Select pixel scaling interpolation (Point, Bilinear, Bicubic). Point provides sharp pixels; Bicubic provides smooth edges.\n\n• MP4 Video Decoder: Configures default video hardware acceleration backends (VLC / Native).\n\n• Target Frame Rate: Set graphics rendering updates (15 FPS to 60 FPS). Higher settings result in smoother lyrics scrolling.\n\n• Library Scanning Directories: Add local drives or folders. Click 'Rescan All Directories' to parse and index files (.zip, .mp3, .cdg, .mp4) into the FTS5 search engine. Obsolete search entries are purged automatically.\n\n• Catalog Book Generator: Run Database Manager (LyracistDbEditor) or Export PDF/Text/Docx to export your entire indexed song library into printable, formatted catalogs sorted alphabetically by artist."
            },
            new() {
                Title = "10. Settings: Database Maintenance",
                Icon = "Database24",
                AccentColor = "#0078D4",
                DescriptionHeader = "Database Live Backups, Restores, and Optimizations",
                DescriptionContent = "• SQLite Optimization: Runs database engines under Write-Ahead Logging (WAL mode) and shared caching to speed up concurrent file access.\n\n• Backup DB: Launches a Save File dialog and runs the native SQLite 'VACUUM INTO' operation. This safely exports a compressed copy of all playlists, history, and configuration data to an external file.\n\n• Restore DB: Launches an Open File dialog. When a backup is selected, Lyracist closes active DB connections, overwrites the active DB, purges WAL/SHM cache logs, and shuts down the application safely for reboot."
            },
            new() {
                Title = "11. Settings: BGM & Special Occasions",
                Icon = "Calendar24",
                AccentColor = "#00B294",
                DescriptionHeader = "Background Music Channels & Occasion Jingle Settings",
                DescriptionContent = "• BGM Volumes: Adjust default audio volumes specifically for Opening, Fill-In, and End-of-Rotation music players.\n\n• Special Occasion Audio & Banners: Add custom sound bites and pre-saved event banners ('Birthday', 'Wedding', 'Engagement', 'Anniversary', 'Last Song'). Assign local audio files and set individual Bass, Treble, and Gain overrides."
            },
            new() {
                Title = "12. Settings: Scaryoke Customization",
                Icon = "PlayCircle24",
                AccentColor = "#FF8E53",
                DescriptionHeader = "Customizing Wheel Segments & Genres",
                DescriptionContent = "• Wheel Categories: View and edit the list of segments displayed on the Scaryoke wheel (2 to 12 total categories). Customize category names and colors.\n\n• Performer Portal Synchronization: The Scaryoke wheel is completely synchronized over Wi-Fi with connected tablets and mobile phones. When spun, the canvas-based wheel on the performer's device spins and lands on the exact same category as the DJ's screen.\n\n• Remote Spin Request: Performers can trigger the Scaryoke wheel directly from their mobile portal when enabled by the host."
            },
            new() {
                Title = "13. External Mixer Setup (e.g. Pyle PMXU88BT)",
                Icon = "Settings24",
                AccentColor = "#DFB900",
                DescriptionHeader = "Integrating Hardware Audio Mixers",
                DescriptionContent = "• Hardware Mixing Philosophy: When using a hardware mixer like the Pyle PMXU88BT, singers' microphones connect directly to mixer physical inputs (XLR Channels 1-4). Sound levels, microphone EQ, and effects are adjusted physically on the mixer.\n\n• Hardware Mixer Mode: Toggle 'Enable Hardware Mixer Mode' in settings to flatten software equalizer, limiter, and compressor, and output at 100% volume for clean unprocessed audio output.\n\n• Separate Karaoke & BGM Buses: Route Karaoke output and BGM (Background Music) to separate channels on your mixer to crossfade and EQ them independently using physical mixer faders."
            },
            new() {
                Title = "14. Wireless Casting & DJ Banners",
                Icon = "Cast24",
                AccentColor = "#0078D4",
                DescriptionHeader = "Casting the Rotation and Custom DJ Banners",
                DescriptionContent = "• Wireless Casting: Navigate to Casting. Choose a casting target (Monitor, Miracast, Chromecast, Browser Cast, AirPlay, or Wireless HDMI). Click 'Cast Rotation' to initialize casting using off-screen buffer rendering.\n\n• Browser Cast Server: Self-hosts a real-time rotation page on http://[Your-Laptop-IP]:8080/rotation/ readable by any browser on local Wi-Fi.\n\n• Custom DJ Banners: Set up borderless, full-screen DJ branding banners (PNG, JPG, GIF, BMP, MP4 video) to display on a selected monitor.\n\n• Same-Screen Deconfliction: If DJ Banner and Rotation Display target the same screen, Rotation Display takes priority and DJ Banner hides automatically."
            },
            new() {
                Title = "15. Connect & Request Instructions Screen",
                Icon = "QrCode24",
                AccentColor = "#059669",
                DescriptionHeader = "Scan-to-Connect Wi-Fi & Patron Request Portal Dual QR Code Banner",
                DescriptionContent = "• Dual Scan-to-Connect QR Code Banner: Displays a full-screen or custom projected banner (`ConnectInstructions.png`) featuring two distinct QR codes: Wi-Fi Scan-to-Connect (WPA/WPA2/NoPass) and Song Request Portal URL.\n\n• High-Contrast Color Palette: The Wi-Fi QR code uses forest green (`#064E3B`) and the Song Request Portal QR code uses pure black (`#000000`) for high contrast scan reliability.\n\n• Persistent Wi-Fi Password Store (`wifi_passwords.json`): Wi-Fi passwords entered for detected Wi-Fi SSIDs are automatically saved and recalled whenever the application connects to that network (venue Wi-Fi, travel router, mobile hotspot).\n\n• 1:1 Pixel-Sharp Screen Resolution Detection: Automatically detects the target monitor's physical dimensions (1080p, 1440p, 4K 3840x2160, etc.) and scales vector graphics, text cards, and QR module sizes (`pixelsPerModule`) to guarantee razor-sharp 1:1 pixel rendering on any screen.\n\n• Green Settings GroupBox & Target Monitor Selection: Located in Column 1 under Display & Projection in a dedicated Green GroupBox (`GreenSettingsGroupBoxStyle`). Select target monitor options: 'None', 'All Screens / Monitors', or individual connected display screens."
            }
        ];

        _selectedTopic = _helpTopics[0];
    }
}

