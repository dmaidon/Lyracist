Last Edit: Aug 6, 2026 - Add plain-text catalog export and failed-artist scan report; fix XP progress bar math and harden FFmpeg/CDG/LibVLC playback against deadlocks, race conditions, and memory leaks
# Changelog

All notable changes to the Lyracist project are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [26.8.6.0] - 2026-08-06

### Added
- **Plain-Text Catalog Export**: The Database Manager's Catalog Book Exporter now supports exporting the Karaoke or Music catalog as a plain text (.txt) file, in addition to PDF and Word. Format dropdown reordered to PDF, Text, Word.
- **Failed Artist Update Report**: The Database Manager's Slow Metadata Scan now tracks any songs whose artist could not be resolved and, on completion or cancellation, exports the unresolved file paths to a timestamped report in the app's `Reports` folder.

### Fixed
- **Singer XP Progress Bar**: Corrected the level-progress calculation, which was off by a factor of two and caused the progress bar to sit near 0% for almost an entire level before jumping.
- **FFmpeg/FFprobe Conversion Hangs**: Stdout and stderr are now read concurrently from ffmpeg/ffprobe child processes instead of sequentially, eliminating a potential deadlock when a conversion or metadata probe produces enough stderr output to fill the OS pipe buffer.
- **CDG Playback Race Condition**: Synchronized the CDG frame scheduler's internal state so a song stop/reload can no longer race an in-flight background frame decode and hand back a stale or torn packet index.
- **LibVLC Media Leaks**: The video and background-music playback backends now dispose the outgoing LibVLC `Media` instance on every track change instead of leaking the native handle, preventing memory growth over long shows with frequent song/crossfade changes.
- **LibVLC Frame Buffer Use-After-Free**: The video backend now copies each decoded frame into a managed buffer before handing it to the UI thread, instead of deferring a copy from a native pointer that a concurrent format change could free first.

## [26.8.4.0] - 2026-08-04

### Added
- **DJ Banner QR Code Overlay**: Toggles a floating patron web portal QR code overlay in the bottom-right corner of the full-screen DJ Banner projection window.
- **DJ Banner QR Code Settings Toggle**: Added a check box under the Select DJ Banner dropdown to enable or disable showing the request QR code overlay.

### Changed
- **Display Tab Rename**: Renamed the "Casting" tab to "Display" across all UI tabs, help documentation topics, and code comments to better describe its display projection capabilities.
- **Skipped Performer Round Tracking**: Corrected sequential round-checking logic to check the active show round rather than the first incomplete round when a performer skips their turn.
- **Immediate Auto-Accept Processing**: Checking the "Auto-accept incoming requests" box now immediately processes and approves all currently pending requests in the queue.

### Fixed
- **Settings tab height and scrolling**: Shortened the Appearance GroupBox's RowSpan from 6 to 3 to align with Email Settings and prevent Settings tab scrollbars.

## [26.8.3.0] - 2026-08-03

### Added
- **Auto-Accept Requests**: New toggle in both Lyracist (Requests page) and KSRotation (next to the Incoming Requests list, including the KSRotation.Maui tablet app) that skips manual DJ approval — Karaoke requests go straight into the rotation and Music requests go straight to Approved the instant they arrive.
- **Shared DJ Banners Folder**: KSRotation and Lyracist now read and write DJ banners from a single shared `DJBanners` folder next to the app installation, so a banner uploaded from either app (or dropped in by hand) is immediately available to both.

### Fixed
- **Multi-Monitor DPI Positioning**: The rotation display and DJ banner windows now report the correct physical resolution and position/size correctly on mixed-DPI multi-monitor setups (e.g. a 100% laptop panel plus a 125% external monitor), instead of using a stale DPI reading from whichever monitor the window happened to start on.
- **Unwanted Auto-Casting**: Enabling the local rotation display, changing the Casting tab's target, or simply launching KSRotation no longer automatically starts (or resumes) casting to Miracast/Chromecast/BrowserCast/AirPlay. Casting now only ever starts from an explicit "Cast Rotation" action.
- **KSRotation Monitor Selector**: Restored the Target Monitor dropdown and Refresh button to the Settings page (removing an accidental duplicate on the Rotation page) and fixed it being incorrectly greyed out / unable to select a second monitor.
- **DJ Banner / Rotation Display Conflict**: Simplified so enabling the rotation display always disables the DJ Banner (and vice versa) regardless of which monitor each is targeting, instead of only when they happened to target the exact same monitor.

### Changed
- **Shared Code Consolidation**: Moved DJ banner file management, monitor enumeration, DPI-aware window positioning, and the crash-safe `AtomicJsonFile` writer out of per-app duplicates and into `Shared/`, used by both KSRotation and Lyracist (and KSRotation.Maui where applicable) so future fixes only need to happen once.
- **KSRotation Settings Layout**: Removed the internal debug-only "Form Size" panel and moved Email Settings into its place.

## [26.8.2.0] - 2026-08-02

### Added
- **Looping MP4 DJ Banners**: Added full support for displaying looping `.mp4` video files as full-screen borderless DJ promotional and branding banners. Includes same-screen collision priority with the rotation billboard display window.
- **Settings Category Visual Styling**: Color-coded the Settings category group boxes in KSRotation (Purple, Blue, Navy, Slate) to visually differentiate settings categories.
- **Wireless Casting Support**: Introduced target options for casting the singer rotation billboard directly to Miracast, Chromecast, AirPlay, Wireless HDMI, or Browser Cast. Used high-performance off-screen buffer rendering to run without cluttered windows on the host desktop.
- **Browser Cast Server**: Self-hosts a local web server (http://localhost:8080/rotation/) to allow any browser on the local network to view the singer rotation billboard in real-time.
- **Display Monitor Selection**: Added a Target Monitor dropdown to allow operators to select a specific monitor for projecting both the Singer Display Window and DJ Banner Window, with dynamic redirection and automatic fallback to secondary/primary screens if unplugged.
- **DJ Banner Projection Screen**: Added support for configuring and projecting borderless, full-screen custom DJ branding/promotional banners (PNG, JPG, JPEG, GIF, BMP, etc.). Includes uploading banners, selecting the active banner, and deleting custom banners.
- **DJ Banner Same-Screen Collision Priority**: Added same-screen deconfliction logic that automatically disables and hides the DJ Banner when the Rotation Display is active on the same monitor.
- **Catalog Book Exporter**: Added the ability to export the entire song database (Karaoke or Music) directly into Word (.docx) or PDF format from the Database Manager, featuring professionally formatted tables and paginated footer layouts.
- **Online Song & Artist Lookup**: Added real-time lookup querying the iTunes search index from the patron's mobile browser, with automatic form population and offline fallback.
- **Background Music Requests**: Patrons can request background music tracks from the mobile portal. These are distinguished in the KJ console and DJ portal queues with a green [MUSIC] badge.
- **Paused and Inactive Singer Controls**: Added the ability to pause singers (retaining their index but skipping them in sequence) and mark deleted singers as inactive (moving them to the end of the queue with one-click restoration to the active section).
- **Active Venue & DJ Title Bar Integration**: Integrated the active Venue and DJ name into the standard main window title bar to prevent layout wrapping issues on 1080p laptop screens.
- **Help System Updates**: Updated both Lyracist and KSRotation integrated help panels to document wireless casting options, custom DJ banner configurations, iTunes online lookup, background music requests, remote DJ console locking/PIN protection, and deleted split-flap FlipTile references.

### Removed
- **FlipTile Board View Mode**: Deprecated and completely removed the obsolete Split-Flap FlipTile view mode from the projection options and code.

## [26.7.31.0] - 2026-07-31

### Fixed
- **Nullability Warnings**: Resolved possible null reference return (CS8603) and dereference warnings (CS8602) in `CatalogBookGenerator.cs` and `CatalogBookGeneratorTests.cs`.
- **Android SDK Build Issue**: Cleaned locked `bin`/`obj` folders under `KSRotation.Maui` to resolve clean/rebuild directory deletion errors.

## [26.7.25.0] - 2026-07-25

### Fixed
- **Pending Model Changes EF Exception**: Generated the missing `MakeSongFilePathIndexUnique` migration to resolve the `PendingModelChangesWarning` exception that blocked new database schema migrations on fresh installations.

### Changed
- **Build Output Cleanup**: Configured the build system to target English resources exclusively (`<SatelliteResourceLanguages>en</SatelliteResourceLanguages>` in `Directory.Build.props`), completely removing foreign language satellite folders (`cs`, `de`, `es`, etc.) from the build output directory.

## [26.7.10.1] - 2026-07-10

### Added
- **Karaoke/Music Request Indicator Bulbs**: Two glowing "K" (yellow) and "M" (neon green) bulb indicators on the Karaoke page header light up and gently pulse whenever a pending karaoke or music request is waiting for review, and go dim again automatically once it's approved or rejected.
- **Separate Karaoke vs. Music Requests**: The mobile portal and the KJ's request queue now distinguish "Karaoke" requests (a singer performing) from "Music" requests (just play the track), end-to-end. Added a "Search Music" tab on the tablet portal for browsing the background-music library separately from the karaoke catalog, and a Karaoke/Music toggle on the Custom Link tab.
- **Current-Performer-Only Scaryoke Spin**: Once Scaryoke Mode is enabled, only the singer currently marked as performing can spin the wheel from their phone. Everyone else still watches it spin live, but the Spin button is hidden for them, and a spin attempt from anyone else is rejected server-side.
- **Scaryoke Wheel Gated Behind DJ Toggle**: The mobile portal's Scaryoke tab and its underlying API endpoints are now hidden/blocked until the host enables Scaryoke Mode on the Karaoke page, instead of always being reachable to anyone connected.

### Fixed
- **Approving a Karaoke Request Didn't Add the Singer to the Rotation**: Approving a pending request from the mobile portal only flipped its database status; it never added the singer to the show. Approving a Karaoke-type request now adds the singer and song straight into the active rotation, matching what KJs expect from the mobile "request" feature.
- **Next Singer Didn't Follow the Current Singer**: The previous fix that preserved a manually-designated "Next" singer interacted badly with the "Set as Current Performer" star toggle — marking a new singer as Current could leave a stale Next flag pointing at whoever used to be next, since there's no actual UI to pick a Next singer independently of Current. Next Up now always recalculates sequentially from whoever is Current, on both the Karaoke page and the rotation billboard.
- **Fill-In / Opening / End-Rotation "Play" Ignored the Highlighted Track**: Clicking Play on any of the three background-music playlists always started from the first track in the internal (possibly shuffled) playback order, regardless of which song was highlighted in the list. Play now starts at the highlighted track.
- **Floating Emoji Reactions Rendered in Black & White**: WPF's built-in text renderer can't display color emoji glyphs; reactions sent from the mobile portal now render in full color on both projection screens.
- **Emoji Reactions Always Floated Bottom-to-Top**: Reactions now spawn from a random screen edge (top, bottom, left, or right) and drift across the screen instead of always rising from the bottom, and render about 20% larger.
- **Karaoke Page Header Fixed-Width Hack**: The header banner had picked up a hardcoded pixel width; it now stretches to fill the page width like every other header, and the "Scan to Join" QR code badge was enlarged for easier scanning.

---

## [26.7.10.0] - 2026-07-10

### Added
- **Emoji Crowd Reactions**: Singers can tap 👏 🔥 ❤️ 🙌 🎉 👑 buttons on the tablet portal to fire floating, animated emoji reactions that drift and fade across both the Lyrics and Rotation projection screens in real time over SignalR.
- **Live Server Log Viewer**: Added a "Logs" tab to the tablet web portal exposing the most recent app and error log entries via a new `/api/logs` endpoint, with a manual Refresh button.
- **Rating Symbol & Score Sync to Tablet**: The tablet dashboard now shows the current performer's live average rating next to their name and labels the rating card with the host's chosen feedback icon instead of a hardcoded star, refreshing immediately after each new rating submission.
- **QR Code "Scan to Join" Badges**: Auto-generated QR codes linking to the tablet portal now appear as a badge on the Karaoke page header and as a floating overlay on the Lyrics projection window.
- **System/Light/Dark Theme Selector**: Replaced the dark-mode-only checkbox with a three-way Theme Mode dropdown (Light/Dark/System) on the Karaoke page header and Settings, with live system-theme watching.
- **Manual "Set as Current Performer" Override**: Added a star-toggle on each singer row (Karaoke and Rotation pages) letting hosts manually designate who's currently singing, taking priority over automatic sequencing.
- **Configurable Fill-In Music Delay**: New Settings slider (0-30s) lets hosts set the exact delay before fill-in background music starts, replacing the previous fixed random 5-7 second delay.
- **Add From Singer History**: Singers can be re-queued directly from the Singer History tab via row selection or double-click, without re-searching the catalog.
- **Queue Singer Without a Song Selected**: Adding a performer with no song/track chosen now creates a placeholder queue entry instead of silently doing nothing.

### Fixed
- **Star Wars Crawl Resetting Every 10 Seconds**: The rotation billboard's crawl view restarted itself on every view-model property change, including an unrelated leaderboard-toggle timer that fires every 10 seconds — so the crawl never scrolled past its header before resetting. Narrowed the restart trigger to only the properties the crawl actually depends on.
- **Tablet Rating & Reaction Buttons Unreachable**: The tablet portal's `submitRating` function and the rest of the client script (including the Scaryoke Wheel logic) had been accidentally nested inside another function's scope, making them unreachable from `onclick` handlers.
- **Manually Designated Next Singer Ignored**: `KaraokeViewModel` and the rotation display previously always recalculated the next singer sequentially, ignoring a manually designated next singer; both now respect the manual designation.

---

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
