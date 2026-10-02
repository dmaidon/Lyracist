// Edited on Oct 2, 2026 @ 11:30:00 -> Update Topic 5 help for New Performer Welcome Screen dedicated group box
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

public partial class HelpViewModel : ObservableObject
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
                DescriptionHeader = "Media Playback Controls, Audio Controls & Search Library",
                DescriptionContent = "• Search Box & Direct Database Scan: Type artist or title keywords to filter songs in real-time. A dedicated clear button ('✕') clears text in one click. Click the 'Scan' button beside the search box or press Enter to immediately query the local SQLite database. External streaming searches (YouTube, Spotify, Amazon) are isolated to the Streaming Links tab to ensure local library lookup remains instantaneous.\n\n• 1080p Resolution Layout Optimization: Streamlined header toolbar and adaptive column scrolling eliminate vertical clipping on 1920x1080 displays at 100% and 125% Windows DPI scaling. The Singer Assignment card is permanently anchored beneath the virtualized search results table without requiring scrolling.\n\n• Playback FooterBar Text Protection: Performer name, song title, and seek slider are bounded within a dedicated anti-impingement grid layout on the bottom playback bar, physically protecting center Play/Pause/Stop transport controls from long text overflow.\n\n• Tablet Touch Sizing & Non-Collapsing Buttons: Action buttons across search result tables and rotation queues are optimized with fixed 52px non-collapsing columns and generous 38x32px touch targets, ensuring controls remain comfortable and never compress into vertical lines on 1080p tablet screens.\n\n• High-Output WASAPI Audio Engine & USB Mixer Boost: Audio playback routes through the modern Windows Core Audio WASAPI mmdevice pipeline on all endpoints with up to 200% volume headroom. A dedicated Master Output Boost setting (0 dB to +12 dB) delivers full line-level signal drive for external USB mixers like the Yamaha MG10XU, paired with an integrated anti-clipping limiter to protect against digital distortion.\n\n• Karaoke & Music Library Tabs: Search results are organized into distinct 'Karaoke Library' and 'Music Library' tabs displaying live match count badges (e.g. 'Karaoke Library (12)' and 'Music Library (3)'). The library scanner automatically routes companion MP3+G karaoke pairs (.mp3 + .cdg) into the Karaoke Library, and catalogs standard audio formats (.mp3, .m4a, .flac, .wav, .wma, .aac, .ogg) into the Music Library with search isolation.\n\n• Search Result Track Type Tinting: Track search results feature subtle color tinting to distinguish track types at a glance—green for karaoke tracks (IsKaraoke=True) and gold for standard music library tracks.\n\n• Compact DJ Auto-Advance Toolbar: A streamlined ~32px horizontal toolbar provides quick Auto-Advance toggle, countdown timer, and compact '▶ Start Song' and '⏭ Skip Singer' buttons for rapid queue control with maximum screen real estate.\n\n• Direct Queueing: Click the '+' (Add) button next to any track in search results to instantly queue it, avoiding copy-paste operations.\n\n• Singer Selection: Click on a singer in the rotation list to select them as the current singer (highlighted in red). Click the singer again to deselect, allowing you to easily correct misclicks.\n\n• Manual Performer Override: Click the star toggle on any singer row to manually designate who is currently performing, overriding default sequence order.\n\n• Pending Request Bulbs: Glowing 'K' (yellow) and 'M' (neon green) indicator bulbs light up on the Karaoke header the instant a karaoke or music request arrives from a patron's device.\n\n• Theme Mode Selector: Three-way Light/Dark/System theme dropdown on the header and Settings with live system theme detection.\n\n• Playback Controls: Play, Pause, Stop, Seek Slider, and Volume. The active lyrics projection window displays synchronized CDG frames or MP4 video streams.\n\n• Per-Singer Key, Tempo & Mic Recall: Key transposition and playback speed are automatically remembered per singer per song in history. Vocal gain (mic level), 3-band EQ, compressor, and limiter recall automatically when the singer performs.\n\n• Audio Controls Save & Recall: Click 'Save to Singer' in Audio Controls to save current slider levels to the singer's profile defaults, or 'Recall Singer' to restore saved defaults on demand.\n\n• Singer History Tracking: Singer History displays past Key and Speed for each song sung, and re-queuing a song preserves its historical transposition and tempo."
            },
            new() {
                Title = "2. Rotation Page",
                Icon = "People24",
                AccentColor = "#0078D4",
                DescriptionHeader = "Singer Rotation Management & Scaryoke Mode",
                DescriptionContent = "• Add to Rotation: Enter Performer Name, select a song, specify key transposition semitones (e.g. +2 for higher, -3 for lower), type custom notes, and click Add. Supports up to 4 singers in Rotation.\n\n• Smart End-of-Round Insertion: Newly added singers are placed at the end of the active rotation round (right before whoever holds the '⚓ ANCHOR' badge), ensuring new singers sing in the current cycle before previously finished performers repeat in the next round.\n\n• Dynamic Round Completion Estimation & Duration Notice: Displays real-time estimated round duration and completion ETA (e.g. '⏱️ Round: 4 singers left • ~20m (ends ~11:45 PM) | Full round: ~25m (5 singers)'). Automatically updates every 15 seconds and recalculates when singers are added, reordered, paused, skipped, or finish performing. In Last Round mode, counts only singers who have yet to perform. Provides essential end-of-night planning across Lyracist, KSRotation desktop, KSRotation.Maui tablet, and the Remote DJ portal (dj.html) to determine if there is time for another round before venue closing.\n\n• Special Singer (One-Time Performance): Check '⭐ Special' when adding or editing a singer to designate them as a guest or spur-of-the-moment performer. Special singers are immediately placed at the top of the queue as the current singer. Once their performance concludes, they are automatically transitioned to inactive and regular rotation resumes seamlessly.\n\n• Smart Name & Artist Proper-Casing: Automatically capitalizes performer names, duet partners, artists, and song titles while preserving intentional mixed-case capitalizations (such as 'DeaR' or 'LeBron') and correctly capitalizing prefix names (like 'O'Neal', 'D'Angelo', 'L'Amour') and Scottish/Irish 'Mc' surnames ('McDonald').\n\n• Queue Ordering & Dynamic Wait Times: Use Up/Down arrow buttons next to a performer's entry to adjust priority. Estimated wait times and audience displays recalculate immediately when queue order changes. 1-Click rotation advancement automatically rolls over to the top performer when the final singer finishes.\n\n• Paused & Inactive Controls: Pause a singer to suspend them indefinitely while retaining position until unpaused, or mark them inactive ('delete') to move them out of rotation to the end of the queue. Inactive singers are excluded from the rotation count. Click 'Restore' to reactivate them.\n\n• Singer Skip (Single-Round Bypass): Click '⏭ Skip' on any performer in the rotation list or context menu to bypass them for the current round while preserving their exact placement in the queue. When the rotation advances and reaches the round anchor ('⚓ Anchor'), the skipped flag automatically resets so the singer performs normally in the subsequent round. Unlike Inactive or Pause, Skip is strictly single-round and hassle-free.\n\n• Performer XP & Milestone Badges: Tracks singer history, total songs sung, and average feedback ratings to calculate XP (`XP = TotalSongsSung * 100 + Score`) and unlock visual milestone badges (e.g. Shower Singer, Karaoke Legend).\n\n• Performer Editing: Double-click any singer row or select Edit to open the Singer Edit dialog to adjust name, requested track, transposition key, or notes dynamically.\n\n• Scaryoke Selection Wheel: Toggle Scaryoke to show the spin wheel. When spun, it randomly picks a category/genre and forces a random database song onto the performer. Wheel crosses trigger synchronized pointer clicking sounds."
            },
            new() {
                Title = "3. Playlists Page",
                Icon = "MusicNote2Play20",
                AccentColor = "#10893E",

                DescriptionHeader = "Background Music (BGM) Channels",
                DescriptionContent = "• Playlist Channels: Manage Opening Music (plays before the show), Fill-In Music (auto-plays in gaps between performer tracks), and End-of-Rotation Music (plays when the singer list is completed).\n\n• Adding Tracks: Browse indexed files in the Library column and click Add next to the target playlist. Use the playlist grid control to manage files.\n\n• Configurable Fill-In Music Delay: Adjust the delay slider (0-30 seconds) in settings to control exactly when fill-in music begins after a singer track stops.\n\n• Show Flow Automation: BGM players interact automatically with the karaoke player: BGM pauses/ducks when a performer's track starts, and resumes immediately when it stops."
            },
            new() {
                Title = "4. Patron Song Request Portal & DJ Approval Queue",
                Icon = "Phone24",
                AccentColor = "#8E8CD8",
                DescriptionHeader = "Self-Hosted Web Portal & Live Request Ingestion",
                DescriptionContent = "• Incoming Request Grid: Displays real-time song submissions sent by performers using their mobile portal.\n\n• Karaoke vs. Background Music: Requests are labeled as standard Karaoke (purple badge) or Background Music (green [MUSIC] badge). Singer rotation allows one active karaoke request and one background music request slot concurrently.\n\n• Music Request Automation: Background music requests show up in bold green in the rotation list. Completed music tracks are automatically removed from rotation upon completion and are excluded from standard performer stats.\n\n• Approval Workflow: Review details (Performer, Song, Key offset, notes). Click Approve to instantly queue the singer and song in the main rotation list, or click Decline to reject the submission.\n\n• Auto-Accept Requests: Toggle in the page header to skip manual approval entirely — Karaoke requests go straight into the rotation and Music requests go straight to Approved the moment they arrive."
            },
            new() {
                Title = "5. Settings: Display & Projectors",
                Icon = "WindowAd24",
                AccentColor = "#DFB900",
                DescriptionHeader = "Dedicated 4-Column Display Tab & Screen Activation Toggles",
                DescriptionContent = "• 4-Column Display Screen Layout: Consolidates all screen assignments, DJ banners, Star Wars crawl, and QR code instructions into 4 equal columns (matching KsRotation layout).\n\n• Screen Activation Checkboxes: Explicitly toggle 'Enable Lyrics Projection Screen', 'Enable Singer Rotation Billboard Screen', and 'Enable DJ Banner Screen' on or off.\n\n• QR Code Display Toggles: Independently toggle song-request QR code badges on or off for both the Lyrics Projection Screen and the Singer Rotation Billboard Screen via Settings, Lyrics Live Center, or the Lyrics Screen right-click context menu.\n\n• Monitors & Screen Assignments: Assign independent display monitors for borderless Lyrics, Singer Rotation Billboard, and DJ Banner windows. When broadcasting the DJ Banner to an older TV, check its picture settings for 'Overscan', 'Just Scan', 'Screen Fit', or '1:1 Pixel Mapping' and disable overscan to prevent edge cropping.\n\n• Billboard View Modes: Choose between 12 visual projection themes: Normal List, Star Wars Crawl, Vegas Marquee, Vinyl Turntable, Disco Ball, Synthwave Grid, Concert Festival Lineup, Casino Slot Reels, Jukebox, Stadium Jumbotron, Movie Theater 'Now Showing', and Purple Velvet Curtain.\n\n• New Performer Welcome Screen: Dedicated settings group box on the Display tab that automatically displays a full-screen welcome for new singers joining the rotation. Customize enable state, display duration (3-120 seconds), target monitor, and welcome style model (choose between Spotlight, Neon Night, Sunset Stage, Confetti Party, Disco Rays, Red Velvet Curtain, or 'All (Random)'). Test immediately with the Preview button.\n\n• Crawl & Spaceship Overlay: Select Star Wars crawl intro text templates, custom crawl text, and adjust spaceship overlay font size, duration, frequency, and custom text snippets.\n\n• Mirror Lyrics: Enable to horizontally flip CDG graphics for rear-projection setups."
            },
            new() {
                Title = "6. Settings: Audio & DSP Engine",
                Icon = "Settings24",
                AccentColor = "#A700EC",
                DescriptionHeader = "Master Output Boost (USB Mixer Mode), Equalizers, and Dynamics",
                DescriptionContent = "• Master Output Boost / Preamp (USB Mixer Mode): Adjust digital preamplification from 0 dB to +12 dB (with quick presets for 0 dB Standard, +6 dB USB Mixer, and +12 dB Maximum). This delivers hot, professional +4 dBu line-level signal drive into USB mixers (such as the Yamaha MG10XU) without requiring external preamps or cranking mixer channel gains to their limits.\n\n• Anti-Clipping Peak Limiter: A transparent soft-knee peak limiter prevents digital clipping and speaker distortion when output boost or hot audio tracks are played.\n\n• Active Key Transposition: Pitch shift performances from -6 to +6 semitones with sub-150ms instant seek reload applying FFmpeg pitch shifting (`asetrate` + `atempo`).\n\n• Global EQ: 3-band tone balancing. Adjust Bass (low frequencies), Midrange (vocal clarity), and Treble (vocal presence) sliders from -20dB to +20dB. Click Reset to revert.\n\n• Dynamic Compressor: Evens out voice fluctuations by boosting quiet sections and ducking loud parts.\n\n• Peak Limiter: Clamps sound levels to prevent digital clipping, microphone feedback, or hardware speaker damage."
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
                DescriptionContent = "• BGM Volumes: Adjust default audio volumes specifically for Opening, Fill-In, and End-of-Rotation music players.\n\n• Special Occasion Audio & Banners: Add custom sound bites and pre-saved event banners ('Birthday', 'Wedding', 'Engagement', 'Anniversary', 'Last Song'). Assign local audio files and set individual Bass, Treble, and Gain overrides.\n\n• Special Event Banner Mutual Exclusion & Two-Way Sync: Special Event Banners enforce strict single-selection mutual exclusion across the radio button group. Selecting 'None' cleanly clears and restores standard DJ branding banners. Selections synchronize seamlessly between Lyracist and KSRotation with local edit protection and quiet updates, avoiding blocking popups on unattended host laptops.\n\n• Priority 'Last Song' Banner: Activating 'Last Song' displays `LastSong.png` on all non-lyric screens (DJ banner and rotation billboard) with top priority while continuing uninterrupted lyrics projection on the lyric screen."
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
                Title = "14. Wireless Casting, Chromecast TV Billboard & DJ Banners",
                Icon = "Cast24",
                AccentColor = "#0078D4",
                DescriptionHeader = "Casting the Rotation, Chromecast Web Displays, and Custom DJ Banners",
                DescriptionContent = "• Wireless Casting & DashCast TV Display: Navigate to Casting or use the 1-tap 'Cast to TV' button in KSRotation.Maui. Cast the live rotation queue, currently singing performer, upcoming queue, and dual QR codes directly to Google Cast / Chromecast displays via DashCast web streaming.\n\n• Live Initial Paint & Dynamic Rendering: Billboard template dynamically injects the active venue name, DJ branding, and server IP at serve time for instant TV display with zero wait.\n\n• Browser Cast Server: Self-hosts a real-time rotation page on http://[Your-Laptop-IP]:8080/rotation/ or http://[Your-Laptop-IP]:5005/billboard readable by any smart TV or browser on local Wi-Fi.\n\n• Custom DJ Banners: Set up borderless, full-screen DJ branding banners (PNG, JPG, GIF, BMP, MP4 video) to display on a selected monitor.\n\n• Same-Screen Deconfliction: If DJ Banner and Rotation Display target the same screen, Rotation Display takes priority and DJ Banner hides automatically."
            },
            new() {
                Title = "15. Connect & Request Instructions Screen",
                Icon = "QrCode24",
                AccentColor = "#059669",
                DescriptionHeader = "Scan-to-Connect Wi-Fi & Patron Request Portal Dual QR Code Banner",
                DescriptionContent = "• Dual Scan-to-Connect QR Code Banner: Displays a full-screen or custom projected banner (`ConnectInstructions.png`) featuring two distinct QR codes: Wi-Fi Scan-to-Connect (WPA/WPA2/NoPass) and Song Request Portal URL.\n\n• QR Code Overlay on DJ Banner: Toggle 'Show request QR Code overlay on DJ Banner' in Settings to project the QR code directly onto active DJ Banners.\n\n• Target Monitor Selection: Select the target display screen/monitor using the 'Connect Instructions & QR Code Screen' dropdown in Column 4 of the Display tab.\n\n• High-Contrast Color Palette: High-contrast pure black on white QR codes with NearestNeighbor pixel scaling for maximum scanning readability across all tablet and phone camera sensors.\n\n• DJ Control QR Code Enlarged Popout: Clicking the DJ Control QR code opens a dedicated enlarged popout dialog with 280x280 QR display, large bold security PIN badge, and one-click URL & PIN copy.\n\n• Persistent Wi-Fi Password Store (`wifi_passwords.json`): Remembers Wi-Fi passwords entered for detected SSIDs (venue Wi-Fi, travel router, mobile hotspot)."
            },
            new() {
                Title = "16. Lyracist Live Trivia (Interactive Pub & Bar Trivia)",
                Icon = "BrainCircuit24",
                AccentColor = "#38BDF8",
                DescriptionHeader = "Real-Time Multi-Player Trivia with Mobile Buzzers & 70:30 Pre-Game Lobby",
                DescriptionContent = "• Zero-Install Mobile Buzzers: Patrons connect to local Wi-Fi and open http://<LAN-IP>:8085/trivia on their smartphones to join with an instant 4-button color buzzer (A, B, C, D), real-time answer elimination, and haptic feedback.\n\n• 70:30 Pre-Game Lobby Showcase: 1-click launch projects a dual-purpose lobby screen featuring 16:9 high-resolution Category Announcement Banners (70% width) alongside a live countdown timer and stacked Wi-Fi & Join QR codes (30% width).\n\n• Dynamic Category Databases & Starter Library: Includes 15 starter curated categories (150 questions each / 2,250 questions total). Users can add unlimited custom category packs simply by placing new JSON files into `TriviaData/packs/` (auto-discovered on launch without code changes).\n\n• Auto-Advance & Live Visualizer: Automatic countdown timers, progressive wrong answer elimination, answer reveals, and real-time response distribution graphs for the Game Master."
            },
            new() {
                Title = "17. Trivia Settings, Timers & Scoring Rules",
                Icon = "Clock24",
                AccentColor = "#F59E0B",
                DescriptionHeader = "Authoritative Timer Control, Tiered Option Value Scoring & Deconfliction",
                DescriptionContent = "• Manual vs. Automatic Gameplay Modes:\n  - 🎮 Manual Mode (Auto-Run disabled): Questions load in a 'standby/reading' state with the timer paused, allowing the DJ to read the question over the microphone. The DJ starts the countdown clock on demand (Spacebar or 'Start Question & Timer' button), can adjust pacing using timer bumps, and manually triggers answer reveals and question progression (Next ⏭).\n  - ⚡ Automatic Mode (Auto-Run enabled): The entire game runs unattended. Question timers start counting down immediately upon loading, wrong answers are eliminated automatically, correct answers are revealed after countdown, and the engine automatically advances to the next question after a 5-second reveal buffer.\n\n• Tiered Option Value Scoring (100% / 70% / 40%): Correct answers awarded while all 4 options are visible earn 100% of base points (1,000 pts default). When 1 wrong option fades at 2/3 countdown, points drop to 70% (700 pts). When 2 options remain (50/50), points drop to 40% (400 pts). Confident, early answers are awarded a decisive 2.5× scoring advantage.\n\n• Real-Time Settings Synchronization: Changing question duration (e.g. 25s), answer elimination fade interval, post-reveal delay, point percentages, or Wi-Fi credentials immediately propagates to the active game engine and persists to storage on change.\n\n• Randomized Wrong Answer Elimination: Wrong options fade in randomized, unpredictable sequences on each question, preventing players from guessing based on fade position patterns.\n\n• Question Answer Window (Game Master Override): The 'Question Answer Window' in Trivia Settings (default: 15s) is authoritative. If the host sets it to 10s, 20s, 30s, etc., all questions count down with the host's configured duration, overriding the 15s default in the question JSON databases.\n\n• Dynamic Scoring & Speed Bonuses: Base score of 1,000 pts per correct answer, with up to +500 speed bonus for fast submissions, and a +10% streak multiplier for consecutive correct answers.\n\n• Wrong Answer Deduction Points: Optional penalty (0 to -500 pts) for incorrect submissions to discourage blind guessing.\n\n• Screen Priority & Deconfliction: Trivia operates secondary to Karaoke performance and Singer Rotation. It automatically pauses with a clear notice and yields the display when songs play or rotation is cast."
            },
            new() {
                Title = "18. Trivia JSON Database Schema & Custom Pack Guide",
                Icon = "Code24",
                AccentColor = "#10B981",
                DescriptionHeader = "JSON Structure Reference for Building & Importing Custom Category Packs",
                DescriptionContent = "• Custom Question Pack JSON File Schema:\nSave as UTF-8 `.json` files inside `TriviaData/packs/`:\n\n{\n  \"PackId\": \"unique-pack-id\",\n  \"Title\": \"Display Category Title\",\n  \"Category\": \"Category Name\",\n  \"Description\": \"Description of pack content.\",\n  \"Questions\": [\n    {\n      \"Id\": \"Q-001\",\n      \"Category\": \"Category Name\",\n      \"Difficulty\": \"Easy\",\n      \"QuestionType\": \"MultipleChoice\",\n      \"Prompt\": \"Question text here?\",\n      \"Options\": [ \"Choice A\", \"Choice B\", \"Choice C\", \"Choice D\" ],\n      \"CorrectAnswerIndex\": 0,\n      \"Explanation\": \"Explanation snippet for answer reveal.\",\n      \"TimeLimitSeconds\": 15\n    }\n  ]\n}\n\n• Custom Banners: Place a matching image file (e.g. `unique-pack-id.png` or `.jpg`) in `TriviaData/Banners/` for 16:9 lobby projection.\n• Auto-Discovery: All files in `TriviaData/packs/` load automatically on startup and sync to SQLite `trivia.db`."
            },
            new() {
                Title = "19. Global Auto-Selection & Fast Input",
                Icon = "SelectAllOn24",
                AccentColor = "#06B6D4",
                DescriptionHeader = "Instant Textbox & Numeric Input Highlighting Across All Suite Applications",
                DescriptionContent = "• Global Select-All on Focus: Clicking or tabbing into any TextBox, PasswordBox, or numeric input control across any application (Lyracist, KsRotation, Trivia, TriviaDbCreator, LyracistDbEditor, KeyGen, ScaryokeWheel) automatically highlights and selects all existing text.\n\n• Instant Overwrite Typing: Hosts and Game Masters can immediately type new values without manually clearing or double-clicking text boxes first.\n\n• Natural Sub-Selection: Subsequent clicks inside an already focused box preserve normal caret placement for precision editing.\n\n• New Singer Auto-Focus: Adding a new singer automatically scrolls the rotation queue, focuses the performer name field, and highlights 'New Singer' for immediate overwrite."
            },
            new() {
                Title = "20. Rotation Anchor & Remote DJ Tablet Controls",
                Icon = "Flag24",
                AccentColor = "#EF4444",
                DescriptionHeader = "Rotation Cycle Visual Highlighting & Remote DJ Tablet Actions",
                DescriptionContent = "• Visual Red Badge & Outline: The performer who marks where the rotation round begins is highlighted with a '⚓ ANCHOR' badge, red background outline, and distinct card styling across desktop consoles, Remote DJ Tablet (dj.html), and Patron Portals.\n\n• 1-Click Remote Anchor Control: The DJ Tablet features a dedicated '⚓' action button to assign or clear the Rotation Anchor with one tap from anywhere in the venue.\n\n• First-Button Checkmark (✓): In the Remote DJ Board (dj.html), the finished song checkmark ('✓') is positioned first in the action buttons bar for fast, immediate 1-tap song completion.\n\n• Popup Modal Performer Entry: Adding a performer from the DJ tablet opens a focused popup modal dialog with auto-focused name input, keyboard shortcuts (Enter to add, Escape to cancel), and instant queue synchronization."
            },
            new() {
                Title = "21. Performer Directory & Users Management",
                Icon = "Person24",
                AccentColor = "#8B5CF6",
                DescriptionHeader = "Singer Directory, Audio Profiles, Performance History & Account Merging",
                DescriptionContent = "• Performer Directory: Master-detail interface in the primary 'Users' tab displaying all registered performers, level badges, XP scores, and song counts.\n\n• Profile & Credentials Editor: Edit Singer Name, 4-digit Patron Portal PIN code, Email, Vocal Range (Soprano to Bass), Custom Stage Title, and DJ Notes.\n\n• Audio Preferences: Save per-singer defaults for Microphone Gain (Volume), Key Transposition (-12 to +12 semitones), Playback Speed (0.8x to 1.2x), 3-Band Parametric EQ (Treble, Mid, Bass), Compressor, and Limiter.\n\n• Performance History: Review complete historical logs of songs sung by each performer with instant 'Queue' and 'Remove' actions.\n\n• Merge Duplicate Accounts: Consolidate duplicate singer profiles into a single primary account, merging all performances, requests, audio profiles, and XP points seamlessly."
            },
            new() {
                Title = "22. Licensed Karaoke Store & Provider Intelligence",
                Icon = "ShoppingBag24",
                AccentColor = "#EC4899",
                DescriptionHeader = "Multi-Store Deep Links, Downloads Folder Watcher, Store Sync & Fingerprinting",
                DescriptionContent = "• Licensed Store Deep Links: Direct search integration for Karaoke Version, Party Tyme, Sunfly Karaoke, and Karaoke.com with zero audio proxying.\n\n• 1-Click Store Sync: Dedicated 'Sync Purchased Tracks' button triggers an immediate on-demand scan of your Downloads or configured purchase folder, importing newly purchased tracks and displaying a comprehensive modal summary.\n\n• Background Downloads Watcher: Automatically detects new .mp3, .cdg, .zip, and .mp4 purchases in your download folder, pairs companion files, and imports them seamlessly.\n\n• Provider Intelligence: Fingerprints track origins from ZIP internal layouts, CDG magic header bytes, ID3 tags, and MP4 watermarks (KV, PT, SF, KC, Local).\n\n• Smart Import Rules: Automatically renames files to canonical format 'Artist - Title (Provider).ext' and classifies genre, difficulty, musical key, BPM, and vocal presence.\n\n• FFmpeg Audio Processing: Applies EBU R128 loudness normalization (-16 LUFS), silence trimming below -50dB, and peak waveform preview generation."
            },
            new() {
                Title = "23. Store Analytics & Benchmarks",
                Icon = "DataTrending24",
                AccentColor = "#14B8A6",
                DescriptionHeader = "Library Distribution, Audio Benchmarks, Musical Keys & Activity Heatmaps",
                DescriptionContent = "• Provider Breakdown: Statistical track counts, percentage shares, and top provider highlights across all cataloged stores.\n\n• File Packaging Stats: Tracks distribution across MP3+G pairs, MP4 video, ZIPCDG archives, standalone MP3s, and attached lyrics (.lrc/.txt).\n\n• Processing Benchmarks: Tracks normalized count, silence-trimmed count, waveform previews, and live stopwatch timing (average, min, max processing speed).\n\n• Musical Keys & BPM Charts: Top 8 musical keys horizontal bar chart for transposition cueing and 5-bucket BPM tempo distribution histogram.\n\n• Activity Timeline & 24-Hour Heatmap: 14-day daily acquisition bar chart and 24-hour import intensity heatmap matrix."
            },
            new() {
                Title = "24. Bulk Import Wizard",
                Icon = "FolderZip24",
                AccentColor = "#3B82F6",
                DescriptionHeader = "Batch Folder Ingestion, MP3+G Pairing, Parallel FFmpeg & Completion Report",
                DescriptionContent = "• Batch Folder Scanning: Select any folder to scan MP3, CDG, ZIP, MP4, and LRC/TXT files with automated MP3+G pairing and companion lyrics binding.\n\n• Candidate Preview Table: Inspects file type, provider source, duration, musical key, BPM, quality tier, difficulty, and vocal presence.\n\n• Global & Granular Audio Toggles: Global options to Normalize all, Trim silence for all, Generate waveforms for all, and Move to target folders, with individual per-track override toggles.\n\n• Throttled Parallel Execution: Runs batch operations concurrently throttled to max 3 simultaneous FFmpeg tasks via SemaphoreSlim(3) with combined single-pass audio filter execution.\n\n• Progress UI & Completion Summary: Live progress bar, current file indicator, success/error counters, cancellation support, and post-import summary report."
            },
            new() {
                Title = "25. Store Notifications & Toast Alerts",
                Icon = "AlertBadge24",
                AccentColor = "#F59E0B",
                DescriptionHeader = "Real-Time Slide-In Toast Alerts for Ingestion, DSP Pipelines & Batch Sync",
                DescriptionContent = "• Automated Slide-In Toasts: Real-time non-intrusive notification cards animate smoothly in the bottom-right corner of the Store Page and Bulk Import Wizard.\n\n• Track Imported Alerts: Displays 'Imported: Artist - Title (Provider)' accompanied by visual pill badges for file packaging (MP3+G, MP4, ZIPCDG) and applied enhancements (Normalized, Trimmed, Waveform).\n\n• Audio DSP Feedback: Instant toasts alert the host when EBU R128 audio normalization completes, when lead/tail silence is trimmed below -50dB, or when visual peak waveform data is ready.\n\n• Sync & Batch Completion: Clear summary toasts pop up upon Store Sync ('Store Sync Complete — X tracks imported') and Bulk Import completion ('Bulk Import Complete — X tracks processed').\n\n• Auto-Dismiss & Manual Dismissal: Toasts automatically fade out after 5 seconds or can be dismissed immediately via the top-right close button. Up to 5 toasts stack cleanly without obstructing background navigation."
            },
            new() {
                Title = "26. Settings: Venue Management & Hybrid GPS Auto-Location",
                Icon = "Location24",
                AccentColor = "#107C41",
                DescriptionHeader = "Hybrid GPS & Wi-Fi Geotagging, Travel Router Immunity & Tablet GPS Sync",
                DescriptionContent = "• Hybrid GPS & Wi-Fi Matching: Automatically identifies known performance venues using a 150-meter GPS proximity circle (calculated via Haversine spherical distance). Known venues in Settings/venues.json or ksrotation_venues.json are selected automatically on launch.\n\n• Travel Router Immunity: When using a portable travel router with a static SSID across different venues, flag the network as a 'Travel Router'. Flagged SSIDs are registered in ksrotation_travel_routers.json and excluded from Wi-Fi matching, preventing false venue detection.\n\n• Tablet-to-Laptop GPS Bridging: Laptops lacking dedicated satellite GPS hardware receive real-time peer GPS coordinates from companion Android tablets running KSRotation.Maui via POST /api/venue/location, enabling precision venue auto-location on desktop PCs.\n\n• 1-Click '📍 Tag GPS' Geotagging: Instantly bind current satellite coordinates and Wi-Fi SSID to the active venue name across Lyracist, KSRotation, and KSRotation.Maui with automatic database persistence."
            },
            new() {
                Title = "27. Device Switching & Live Session Handoff",
                Icon = "ArrowSync24",
                AccentColor = "#3B82F6",
                DescriptionHeader = "Zero-Beat Show Migration Between Laptop & Tablet with Automated Remote DJ Mode",
                DescriptionContent = "• Live Show Migration: Transfer an ongoing karaoke session between a Windows laptop (KSRotation) and a mobile tablet (KSRotation.Maui on Android/Windows) without losing singer positions, completed rounds, or performance history.\n\n" +
                                     "• Architectural Flow Diagram:\n" +
                                     "┌────────────────────────────────────────────────────────────────────────┐\n" +
                                     "│        ARCHITECTURAL FLOW: LIVE SESSION HANDOFF & REMOTE DJ TAKEOVER   │\n" +
                                     "└────────────────────────────────────────────────────────────────────────┘\n\n" +
                                     "    DJ (Tablet: KSRotation.Maui)             KJ Host (Laptop: KSRotation)\n" +
                                     "    ┌───────────────────────────┐           ┌────────────────────────────┐\n" +
                                     "    │ Active rotation on tablet │           │ Arrives at venue & opens   │\n" +
                                     "    │ Serves requests & portal  │           │ 'Switch Device' on laptop  │\n" +
                                     "    └─────────────┬─────────────┘           └──────────────┬─────────────┘\n" +
                                     "                  │                                        │\n" +
                                     "                  │                                 [1] Wi-Fi Scan or IP\n" +
                                     "                  │                                     Selects tablet\n" +
                                     "                  │                                        │\n" +
                                     "                  │<─────── 1. GET /api/session/handoff ───┤\n" +
                                     "                  │         (Sends Laptop IP, Port & PIN)  │\n" +
                                     "                  │                                        │\n" +
                                     "                  ├─────── 2. 200 OK + Full Session JSON ─>│\n" +
                                     "                  │        (Queue, Checkmarks, History)    │\n" +
                                     "                  │                                 [2] Imports session\n" +
                                     "                  │                                     Takes over host\n" +
                                     "                  │                                        │\n" +
                                     "  [3] Auto-switches to in-app                             │\n" +
                                     "      Remote DJ (dj.html)                                  │\n" +
                                     "      Pre-authenticated with PIN                           │\n" +
                                     "                  │                                        │\n" +
                                     "  [4] DJ manages show from floor via embedded tablet UI    │\n" +
                                     "      ═════════════════════════════════════════════════════╪═══════════════\n" +
                                     "      Seamless bi-directional synchronization over venue Wi-Fi!\n\n" +
                                     "• Automated In-App Remote DJ Transition: When the laptop pulls the session, the tablet automatically flips into an embedded full-screen Remote DJ controller (dj.html). The tablet securely receives the laptop's IP, port, and DJ PIN, auto-authenticating so the DJ never leaves the app.\n\n" +
                                     "• 1-Click Wi-Fi Discovery: The 'Scan Wi-Fi' feature sweeps the local venue subnet in 1 second to find running instances on port 5000. Selecting a discovered peer auto-populates the host address and focuses the DJ PIN field for instant entry.\n\n" +
                                     "• Complete State Preservation: Serializes active singers, current performer, on-deck singer, rotation anchor, completed checkmarks (rounds 1-10), tonight's performance history with original timestamps, pending requests, and venue/DJ branding.\n\n" +
                                     "• Deep Linking & QR Portal: A dedicated Switch Device QR code points to http://<ip>:5000/handoff. Scanning with a mobile camera opens a themed portal with a 1-tap 'Open in KSRotation MAUI' deep link (ksrotation://handoff)."
            },
            new() {
                Title = "28. Kiosk Request Station & Landscape Attractor",
                Icon = "Tablet24",
                AccentColor = "#10B981",
                DescriptionHeader = "Dedicated Patron Self-Service Request Station with PWA Fullscreen Support",
                DescriptionContent = "• Kiosk Request Portal (kiosk.html): A dedicated, landscape-optimized tablet station designed for venues where patrons walk up to a stationary tablet (e.g. at the bar or near the stage) to search the song library and submit requests.\n\n" +
                                     "• Welcome & Attractor Screen: When idle, the kiosk displays an eye-catching animated Attractor screen inviting patrons to touch the screen to start searching.\n\n" +
                                     "• High-Touch Search & Filters: Large, accessible on-screen touch targets, alphabetical filtering, instant keyword search, and separate Karaoke vs. Music tabs.\n\n" +
                                     "• Direct KJ Queue Ingestion: Submitted requests stream directly into the host's Incoming Requests queue with glowing visual indicator bulbs and real-time badge counters.\n\n" +
                                     "• PWA Fullscreen Support: Can be installed as a Progressive Web App (PWA) on Android and iOS tablets for a distraction-free, browser-chrome-free kiosk experience."
            },
            new() {
                Title = "29. Audience Billboard & Chromecast Web-Casting",
                Icon = "Tv24",
                AccentColor = "#8B5CF6",
                DescriptionHeader = "Full-Height Stage Rotation Column & Built-in Google Cast Streaming",
                DescriptionContent = "• Built-in Chromecast / Google TV Casting: Directly stream the live audience billboard (billboard.html) from KSRotation.Maui on Android tablets or Windows PCs to any Chromecast or Google TV device on the network.\n\n" +
                                     "• Full-Height Rotation Column: The upcoming singer queue occupies the entire full-height right column, ensuring performer names, song titles, and queue position numbers remain completely legible from across the room on 720p, 1080p, and 4K venue displays.\n\n" +
                                     "• Side-by-Side QR Cards: The 'Song Requests' QR code and 'Wi-Fi Join' QR code are positioned side-by-side beneath Now Performing and Up Next in the left column for easy scanning.\n\n" +
                                     "• Persistent Host & Venue Branding: Configure Host DJ Name and Venue Name in the About dialog with a persistent toggle to display or hide branding on the billboard screen."
            }
        ];

        _selectedTopic = _helpTopics[0];
    }
}
