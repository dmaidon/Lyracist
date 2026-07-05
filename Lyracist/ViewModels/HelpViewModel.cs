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
                Title = "1. Karaoke Playback",
                Icon = "PlayCircle24",
                AccentColor = "#107C41",
                DescriptionHeader = "Loading and Controlling Media",
                DescriptionContent = "Use the 'Playback Controls' tab. You can click 'Load Song...' to open standard files, or browse the 'Search Library' tab to select matching tracks. Controls support Play, Pause, and Stop buttons, and a draggable Seek Slider to change playback positions."
            },
            new HelpTopic
            {
                Title = "2. Media Library Scanning",
                Icon = "Folder24",
                AccentColor = "#F7630C",
                DescriptionHeader = "Folder Indexing and CDG/MP3 Matching",
                DescriptionContent = "Go to the 'Search Library' tab and click 'Scan Folder...'. Select a directory containing your audio files. The engine automatically indexes MP3 files and matches them with sibling CDG graphics files sharing the same name. Orphans (CDG files without MP3s) are skipped, and matched pairs are marked with a green 'CDG' badge."
            },
            new HelpTopic
            {
                Title = "3. Dual-Screen Projection",
                Icon = "WindowAd24",
                AccentColor = "#DFB900",
                DescriptionHeader = "Setting up Secondary Monitors",
                DescriptionContent = "Click the 'Open Window' buttons on the Dashboard. The application queries system monitors using native Win32 APIs and automatically moves the lyrics or singer rotation window to your secondary display. If no secondary display is detected, the windows default full-screen on the primary display for previewing."
            },
            new HelpTopic
            {
                Title = "4. Singer Rotation",
                Icon = "People24",
                AccentColor = "#0078D4",
                DescriptionHeader = "Queue Ordering and Transpositions",
                DescriptionContent = "Open the 'Rotation' page via navigation tabs. Type a singer's name, key transposition semitones (e.g. +2, -1, 0), and optional request notes, then click 'Add to Rotation'. Use the Up/Down buttons to prioritize singers in the queue or click 'Remove' to delete them when their song finishes."
            },
            new HelpTopic
            {
                Title = "5. Background Music Playlists",
                Icon = "MusicNote2Play20",
                AccentColor = "#10893E",
                DescriptionHeader = "Managing Background Audio Tracks",
                DescriptionContent = "First, populate your library by going to the 'Settings' tab, locating the 'Music Library' group, and clicking 'Add Folder...' followed by 'Rescan All Directories'. Once indexed, navigate to the 'Playlists' page. Select a song from the leftmost 'Music Library' column and click the 'Add' button under your chosen background playlist (Opening, Fill-In, or End of Rotation). Adjust volumes and tone parameters on the 'Settings' page under 'Background Music Channels'."
            },
            new HelpTopic
            {
                Title = "6. Mobile Performer Portal",
                Icon = "MailInboxArrowDown20",
                AccentColor = "#E81123",
                DescriptionHeader = "Starting Broadcast & SignalR Sync",
                DescriptionContent = "To start the performer server, navigate to the 'Settings' tab, locate the 'Self-Hosted Tablet Server' section, and click 'Start Broadcast'. Ensure your computer and your performers' devices are connected to the same local WiFi router (LAN). Performers can connect by scanning the mDNS address or visiting http://[Your-KJ-Computer-IP]:5005 in their browser. They can search catalog songs, submit request transpositions and notes, and view the live singer queue synced in real-time."
            },
            new HelpTopic
            {
                Title = "7. Database Maintenance",
                Icon = "History24",
                AccentColor = "#00B7C3",
                DescriptionHeader = "Backing Up and Restoring Show Data",
                DescriptionContent = "Navigate to the 'Settings' page, locate the 'Music Library' group box, and find the 'Database Maintenance' panel. Click 'Backup DB' to save your settings, playlists, performer records, and song logs as a clean, compacted SQLite file. To restore, click 'Restore DB' and select a valid database backup. Overwriting your database will prompt the application to shutdown safely to complete the restore."
            }
        };

        _selectedTopic = _helpTopics[0];
    }
}
