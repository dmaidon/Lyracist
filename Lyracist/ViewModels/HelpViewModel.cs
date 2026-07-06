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
        _helpTopics = new ObservableCollection<HelpTopic>
        {
            new HelpTopic
            {
                Title = "1. Karaoke Page (Dashboard)",
                Icon = "PlayCircle24",
                AccentColor = "#107C41",
                DescriptionHeader = "Media Playback Controls & Search Library",
                DescriptionContent = "• Search Box & Library: Type artist or title keywords to query the local SQLite database and active streaming providers (YouTube, Party Tyme) in real-time. Use filters to narrow results.\n\n• Direct Queueing: Click the '+' (Add) button next to any track in search results to instantly queue it, avoiding copy-paste operations.\n\n• Singer Selection: Click on a singer in the rotation list to select them as the current singer (highlighted in red). Click the singer again to deselect, allowing you to easily correct misclicks.\n\n• Playback controls: Play, Pause, Stop, Seek Slider, and Volume. The active lyrics projection window displays synchronized CDG frames or MP4 video streams."
            },
            new HelpTopic
            {
                Title = "2. Rotation Page",
                Icon = "People24",
                AccentColor = "#0078D4",
                DescriptionHeader = "Singer Rotation Management & Scaryoke Mode",
                DescriptionContent = "• Add to Rotation: Enter the Performer Name, select a song, specify key transposition semitones (e.g. +2 for higher, -3 for lower), type custom notes, and click Add. Supports up to 4 singers in Rotation.\n\n• Queue Ordering: Use the Up/Down arrow buttons next to a performer's entry to adjust execution priority, or click Remove to dismiss them from the queue.\n\n• Scaryoke Selection Wheel: Toggle Scaryoke to show the spin wheel. When spun, it randomly picks a genre/challenge (Elvis, Rock, Country, Pop, Gender Bender, Oldies) and forces a random database song from that category onto the singer."
            },
            new HelpTopic
            {
                Title = "3. Playlists Page",
                Icon = "MusicNote2Play20",
                AccentColor = "#10893E",
                DescriptionHeader = "Background Music (BGM) Channels",
                DescriptionContent = "• Playlist Channels: Manage Opening Music (plays before the show), Fill-In Music (auto-plays in gaps between performer tracks), and End-of-Rotation Music (plays when the singer list is completed).\n\n• Adding Tracks: Browse indexed files in the Library column and click Add next to the target playlist. Use the playlist grid control to manage files.\n\n• Show Flow Automation: BGM players interact automatically with the karaoke player: BGM pauses/ducks when a performer's track starts, and resumes immediately when it stops."
            },
            new HelpTopic
            {
                Title = "4. Requests Page",
                Icon = "MailInboxArrowDown20",
                AccentColor = "#E81123",
                DescriptionHeader = "Mobile Performer Request Approvals",
                DescriptionContent = "• Incoming Request Grid: Displays real-time song submissions sent by performers using their mobile portal.\n\n• Approval Workflow: Review details (Performer, Song, Key offset, notes). Click Approve to instantly queue the singer and song in the main rotation list, or click Decline to reject the submission."
            },
            new HelpTopic
            {
                Title = "5. Settings: Display & Projectors",
                Icon = "WindowAd24",
                AccentColor = "#DFB900",
                DescriptionHeader = "Dual-Screen Monitor Target Assignments",
                DescriptionContent = "• Lyrics Projection Screen: Choose the target monitor index for the borderless singer lyrics window. Select 'None (Do not show)' to disable this projection or close the window.\n\n• Rotation Billboard Screen: Choose the monitor index for the audience rotation list. Select 'None (Do not show)' to keep the rotation billboard closed.\n\n• Mirror Lyrics: Enable to horizontally flip the CDG graphics pixels. This is required for rear-projection setups so text reads correctly from the front."
            },
            new HelpTopic
            {
                Title = "6. Settings: Audio & DSP Engine",
                Icon = "Equalizer24",
                AccentColor = "#A700EC",
                DescriptionHeader = "Global Equalizer, Compressor, and Peak Limiter",
                DescriptionContent = "• Global EQ defaults: 3-band tone balancing. Adjust Bass (low frequencies), Midrange (vocal clarity), and Treble (vocal presence) sliders from -20dB to +20dB. Click Reset to revert to default settings.\n\n• Global Key/Tempo: Sets default transpositions (semitone adjustments) and playback speeds.\n\n• Dynamic Compressor: Evens out voice fluctuations by boosting quiet sections and ducking loud parts.\n\n• Peak Limiter: Clamps sound levels to prevent digital clipping, microphone feedback, or hardware speaker damage."
            },
            new HelpTopic
            {
                Title = "7. Settings: Service API Keys",
                Icon = "Globe24",
                AccentColor = "#00B7C3",
                DescriptionHeader = "Logins and API Integration Configurations",
                DescriptionContent = "• YouTube API Key: Enter your Google developer key to query the YouTube karaoke catalog directly.\n\n• Spotify Client ID & Secret: Enter client credentials to fetch album artwork and song recommendations.\n\n• Amazon Access & Secret Keys: Enables lookups on the Amazon Music platform.\n\n• Party Tyme Client ID & Secret: Input subscription credentials to authenticate the Party Tyme premium karaoke streaming catalog search and playback."
            },
            new HelpTopic
            {
                Title = "8. Settings: Tablet Web Server",
                Icon = "Server24",
                AccentColor = "#8764B8",
                DescriptionHeader = "SignalR Performer Portal Host Port Settings",
                DescriptionContent = "• SignalR Server Port: Sets the hosting socket port (default: 5005). Ensure the port is open in Windows Defender Firewall.\n\n• Status indicators: Displays active connection state and device sockets.\n\n• Start/Stop Broadcast: Toggles hosting of the performer web portal. Performers on the same local network can access the portal at http://[Your-KJ-Computer-IP]:5005."
            },
            new HelpTopic
            {
                Title = "9. Settings: Library & Decoders",
                Icon = "FolderAdd24",
                AccentColor = "#F7630C",
                DescriptionHeader = "Media Scanner and Playback Rendering Decoders",
                DescriptionContent = "• CDG Canvas Scaling: Select pixel scaling interpolation (Point, Bilinear, Bicubic). Point provides sharp pixels; Bicubic provides smooth edges.\n\n• MP4 Video Decoder: Configures default video hardware acceleration backends (VLC / Native).\n\n• Target Frame Rate: Set graphics rendering updates (15 FPS to 60 FPS). Higher settings result in smoother lyrics scrolling at the expense of CPU usage.\n\n• Library Scanning Directories: Add local drives or folders. Click 'Rescan All Directories' to parse and index files (.zip, .mp3, .cdg, .mp4) into the FTS5 search engine."
            },
            new HelpTopic
            {
                Title = "10. Settings: Database Maintenance",
                Icon = "Database24",
                AccentColor = "#0078D4",
                DescriptionHeader = "Database Live Backups, Restores, and Optimizations",
                DescriptionContent = "• SQLite Optimization: Runs database engines under Write-Ahead Logging (WAL mode) and shared caching to speed up concurrent file access.\n\n• Backup DB: Launches a Save File dialog and runs the native SQLite 'VACUUM INTO' operation. This safely exports a compressed copy of all playlists, history, and configuration data to an external file.\n\n• Restore DB: Launches an Open File dialog. When a backup is selected, Lyracist closes active DB connections, overwrites the active DB, purges WAL/SHM cache logs to prevent corruption, and shuts down the application safely for a manual reboot."
            },
            new HelpTopic
            {
                Title = "11. Settings: BGM & Special Occasions",
                Icon = "Calendar24",
                AccentColor = "#00B294",
                DescriptionHeader = "Background Music Channels & Occasion Jingle Settings",
                DescriptionContent = "• BGM Volumes: Adjust default audio volumes specifically for Opening, Fill-In, and End-of-Rotation music players.\n\n• Special Occasions Category: Manage occasions (Holidays, Weddings, Birthdays) and assign local audio files. Set individual Bass, Treble, and Gain overrides for each occasion track and click Save."
            },
            new HelpTopic
            {
                Title = "12. Settings: Scaryoke Customization",
                Icon = "PlayCircle24",
                AccentColor = "#FF8E53",
                DescriptionHeader = "Customizing Wheel Segments & Genres",
                DescriptionContent = "• Wheel Categories: View and edit the list of segments displayed on the Scaryoke wheel. You can customize the name of each category (e.g., '90s Pop', 'Metallica', 'Dolly Parton').\n\n• Category Bounds: Enforces a maximum of 12 categories (for wheel design formatting) and a minimum of 2 categories (to ensure a valid choice is selectable).\n\n• Automatic Track Lookup: When the wheel lands on a custom category, the media engine automatically searches the library for songs matching that name. If no tracks match, the singer picks any song from that category."
            }
        };

        _selectedTopic = _helpTopics[0];
    }
}
