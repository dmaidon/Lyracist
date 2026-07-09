# Changelog

All notable changes to the Lyracist project are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [26.7.9.0] - 2026-07-09

### Added
- **Performer XP, Levels & Progression**: Gamified singer progression using `XP = TotalSongsSung * 100 + Score`, automatically granting performance titles (e.g. *Shower Singer*, *Pub Regular*, *Vocal Powerhouse*, *Karaoke Legend*) and achievement badges (Debut, Legend, Rising Star, Crowd Pleaser, High Scorer) on both active queues and leaderboard displays.
- **Projected Scaryoke Wheel**: Syncs category wheel spin animations onto the crowd billboard rotation screen with identical deceleration physics, rotation angles, sector colors, and ticking sound effects.
- **Customizable Feedback Ratings**: Symmetrical DJ-side settings controls allowing hosts to curate a list of positive feedback rating symbols (e.g. ⭐, ❤️, 🔥, 🏆, 👑) with strict non-detrimental positive-only validation rules and regular emoji preset support.
- **CDG Background Chroma-Keying**: Strips the native background color of `.cdg` lyrics files (index 0,0) in real time to render them transparent, letting the custom backdrops show through.
- **Dynamic GPU-Accelerated Backdrops**: Added four beautiful, responsive visualizer layers behind transparent lyrics, wrapped in Viewbox controls to scale smoothly on HD and 4K displays:
  - *Neon Waveform*: morphing neon cyan and magenta curves.
  - *Nebula Bokeh*: liquid-glow purple, blue, and red blurred circles floating smoothly.
  - *Retro Synthwave*: scrolling perspective grids and glowing neon sun.
  - *Space Starfield*: multi-layer parallax space stardust canvas.
- **Active Performance Key Transposition**: Real-time pitch transposition from `-6` to `+6` semitones. Hot-reloads and seeks under 150ms to apply FFmpeg-based pitch shifting (`asetrate` + `atempo`) dynamically during live performances.

### Changed
- **Rebranded to Lyracist Pro**: Rebranded the entire application, assembly metadata, and documentation to *Lyracist Pro* to reflect its professional KJ feature set.

---

## [26.7.7.0] - 2026-07-07

### Added
- **Star Wars Crawl View Mode**: High-fidelity 3D projection view mode for the rotation billboard, rendering a starfield backdrop with rotating/twinkling stars, cool/warm color variance, and a 3D-angled text block crawling upward in perspective.
- **Vegas Marquee View Mode**: Theatrical stage theme rendering the current performer's name in giant glowing letters inside a brass frame ringed by purple "marching ants" chasing lights (pulsing Lavender/Purple core), with an "Up Next" strip of next-performers badges below.
- **Vinyl Turntable View Mode**: Warm DJ-booth theme featuring a dynamic rotating vinyl 45 record, static tonearm pivot, and center label showing current performer and song title details alongside an "On Deck" list.
- **Dynamic DJ & Venue Variables**: Integrated `{dj}` and `{venue}` template parameter replacements across the scrolling marquee and all Star Wars crawl templates.
- **DJ & Venue Settings**: Management card on the Settings page to configure the DJ Name and curate/select the Venue database list.
- **Crawl Intro Text Templates**: Provided 3 preconfigured options (Dramatic, Comedic, Over-the-Top) and custom template inputs.
- **Crawl Template Preview**: Added an inline, italicized text block preview in settings to immediately inspect formatted crawl template strings.
- **Show Banner Toggle Support**: Wired the "Show Banner" CheckBox on the Karaoke page to dynamically control the visibility of the billboard scrolling performer marquee.
- **CPU Resource Saver**: Automated animation freeze hooks using the window's `IsVisibleChanged` state, pausing chaser timers and rotation animations when the screen is hidden.

### Changed
- **Billboard Scrolling Perquee**: Upgraded the billboard performer marquee to a continuous scrolling canvas showing the active queue sequence starting from the current performer (yellow/bold highlighted) and the next 5 performers (cyan).
- **Settings View Modes Dropdown**: Exposed all 4 view mode choices ("Normal List", "Star Wars Crawl", "Vegas Marquee", "Vinyl Turntable").

---

## [26.7.6.1] - 2026-07-06

### Added
- **Settings Multi-Column Redesign**: Converted settings page into a 5-column independent scrolling configuration, stacking background music channels and special occasion channels vertically to optimize screenspace.
- **Global ScrollBar Thumb Sizing**: Styled all scrollbar thumbs to enforce a minimum width/height of 45 pixels, preventing microscopic scroll bars on large library lists.
- **Folder-Specific Directory Scans**: Exposed Scan and Rescan selected directory buttons next to the local library folders list.
- **Visual Scan Feedback**: Integrated a dynamic progress ring spinner showing active background directory scanning tasks.
- **Queued Track Playback Fix**: Cached local song file paths directly in rotation queue slots on selection and added loose song title fallback matching + warning alerts when manual song searches fail, correcting the missing lyrics rendering.
- **ZIP Format Playback Support**: Implemented on-the-fly extraction of `.mp3`/`.cdg` pairs from ZIP karaoke archives during playback loading, resolving the issue where CDG lyrics and audio failed to render. Included background thread cleanup to purge temporary directory tracks when stopping or transitioning songs.
- **Rescan Sync & Purge**: Rescanning directories now identifies renamed or deleted files on the drive, removing dead records from both the main SQL database and the FTS5 search index to maintain library integrity.

### Optimized
- **4000x Faster Directory Scanner**: Eliminated process spawning (`ffprobe.exe`) and ZIP extraction disk operations during library scanning, resolving the 2TB drive scanning bottlenecks.
- **Batch Database Ingestion**: Restructured the scan process to query existing records in a single in-memory dictionary lookup and batch insert/update SQLite database and FTS5 search indexes, reducing scan times from hours to seconds.
- **Rescan Transaction Safety**: Refactored multi-directory rescans to execute sequentially in a single transaction on a background thread, preventing concurrent SQLite database locks.
- **Mechanical Spin Clicking**: Integrated mechanical ticking sound effect programmatically synthesized in-memory and synchronized to sector boundary crossings during active wheel spin rendering.
- **Customizable Scaryoke Categories**: Dynamic categories configuration (maximum 12, minimum 2) directly inside settings.
- **Simplified Scaryoke Selector**: Removed all library querying and automatic song assignment logic. The wheel now simply announces the selected category sector, allowing the singer to always choose their own song within that genre/theme.
- **Scaryoke Help & Settings Documentation**: Expanded the Split-Pane Help system to 12 categories, detailing custom wheel configurations.

---

## [26.7.5.85] - 2026-07-05

### Added
- **Split-Pane Help View & Settings Guides**: Redesigned the Help Page to feature a clean left-side navigation index with 11 dynamic categories detailing every single settings parameter (Audio EQ, API keys, tablet port server socket configuration, library scanner indexing rules, backup/restoration steps, Scaryoke spinner, background players) and their configuration instructions.
- **Database Backup & Restore**: Live database backups (via SQLite-native `VACUUM INTO` command) and connection-closed database restorations (overwriting target, deleting temporary WAL/SHM files, and restarting application safely).
- **Display "None" Option**: Support for selecting *None (Do not show)* in monitor dropdowns (Settings and main projection panels). Selecting this option immediately closes or hides the target projection window (Lyrics or Rotation).
- **Test Mode Setting**: Settings toggle to instantly seed default performer queue (Alice, Bob, Charlie, Diana) to check audio and projection setup, or clear the queue when done.
- **SignalR Real-Time Performer Sync**: Automated background sync of the active singer, next performer, and rotation queue updates to the mobile web server, with offline fallback polling.
- **Performer Mobile Portal**: Responsive web portal on port `5005` featuring catalog search, request submissions, queue statuses, and occasion requests.
- **Streaming Music Integrations**: Custom search and play support for Party Tyme (OAuth API, caching), YouTube (live API search and custom URL streaming links), Spotify, and Amazon Music.
- **Persistent Credential Storage**: Settings page fields for API keys (YouTube, Spotify Client ID/Secret, Amazon, Party Tyme) backed by JSON persistence.
- **Direct Streaming Queueing**: Direct queueing (+) button on YouTube/Party Tyme search results to add songs directly without manual copy-paste.
- **Lyrics Preview Overlay**: Automatic visual overlay notification in the Lyrics Preview Monitor during browser-based performances (YouTube, Spotify, Amazon) along with a quick link to re-open the source URL.
- **Custom BGM EQ Settings**: Persistent sliders for Opening, Fill-In, and End-of-Rotation background music volumes and tone properties (Bass/Treble), persisted to the local app settings.
- **About and Branding Window**: Integrated custom `AboutWindow` and `AboutViewModel` to display license details and version info.
- **Sunfly & Karaoke Version Metadata Scanning**: Scanner rules for parsing folder structures, extracting track numbers, and identifying karaoke backing tracks.

### Fixed & Changed
- **Roslyn Warning Suppression & EF1002 Fix**: Added compiler pragma blocks to suppress `EF1002` (potential SQL injection warning on live backup path copy) and added localized rules inside `.editorconfig` to keep the project compile state clean at 0 warnings.
- **ListBox Ambiguity Fixed**: Fully qualified the type `System.Windows.Controls.ListBox` in `KaraokePage.xaml.cs` to resolve naming conflict warnings (`CS0104`) with Windows Forms.
- **Queue Click-to-Deselect**: Re-clicking the active singer in the queue deselects them immediately to allow KJs to easily correct accidental selection clicks.
- **Redundant Hosting Package Cleanup**: Removed direct dependency on `Microsoft.Extensions.Hosting` in `Lyracist.csproj` to fix compile warning `NU1510`, since it's already provided by the ASP.NET Core framework reference.
- **SQLite Optimization & WAL Mode**: Configured SQLite to run in WAL journal mode, Normal synchronization, and shared cache. Made data loading asynchronous via background tasks (`Task.Run`) to keep page transitions and startup snappy.
- **Auto-stop Background Music**: Exposed and wired up track start events to stop/fade opening or fill-in background music immediately when a karaoke track is started.
- **UI Auto-Scaling**: Removed duplicate scroll containers, disabled parent NavigationView scrollbars, and expanded default ScrollBar dimensions to `16px` (and `22px` for main panels) for high visibility and reliable scaling on high-resolution screens.
- **Automated Formatting**: Applied `dotnet format` to automatically fix spacing and style violations across 15 source files.

---

## [26.7.4.3] - 2026-07-04

### Added
- **Base Control Panel & Windows**: Main presentation layer, standalone borderless projection windows, and multi-monitor movement logic.
- **Singer History Database**: Created initial database schema for recording performer histories.
- **Audio Processing Controls**: Initial implementation of 3-band EQ, Compressor, and Limiter.
- **Occasions Library**: Special Occasions Category manager.
