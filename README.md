Last Edit: Aug 3, 2026 - Added Auto-Accept Requests toggle, consolidated shared KSRotation/Lyracist code, fixed multi-monitor DPI positioning and unwanted auto-casting
# Lyracist Pro

Lyracist Pro is a premium, modern Windows WPF Karaoke hosting application designed for professional KJs and home entertainment. Built using WPF and .NET 10, it offers advanced multi-monitor projection, rich audio customization, local and streaming library search, and an integrated mobile tablet server for performer lyrics.

---

## Key Features

### 🎙️ Performer & Rotation Queue
- **Interactive Singer Queue**: Dynamic list matching performer names with requested song, artist, key changes, and custom notes.
- **Performer XP & Levels**: Automatic XP and Leveling system (`XP = TotalSongsSung * 100 + Score`) that tracks history, displays custom titles (e.g. *Shower Singer*, *Karaoke Legend*), and unlocks visual milestone badges directly in the queue and leaderboard.
- **Customizable Feedback Ratings**: Symmetrical DJ tab controls allowing hosts to customize positive symbol choices (⭐, ❤️, 🔥, 🏆, 👑) with strict safety guidelines (preventing negative feedback) and regular emoji preset support.
- **Click-to-Deselect**: Easily toggle current singer selection on/off (highlighted in red) to allow correcting misclicks.
- **Manual "Set as Current Performer" Override**: Star-toggle on any singer row to manually designate who's currently singing, taking priority over automatic sequencing on both the Karaoke and Rotation pages.
- **Paused & Inactive (Deleted) Singer Controls**: Pause singers to skip them in rotation while retaining their position, or mark them inactive ("delete" them) to move them to the end of the queue with one-click restoration to the active section.
- **Add From Singer History**: Re-queue a returning performer directly from the Singer History tab via row selection or double-click, without re-searching the catalog.
- **Pending Request Indicator Bulbs**: Two glowing "K" (yellow) and "M" (neon green) bulbs on the Karaoke page light up and pulse the instant a karaoke or music request comes in from a singer's phone, so hosts never miss one.
- **Test Mode**: Accessible under Settings → Theme & Appearance to instantly seed default performers for audio/video checks, or clear them when ready for the show.
- **Theme Mode Selector**: Three-way Light/Dark/System theme dropdown on the Karaoke page header and Settings, with live system-theme watching and instant application.
- **Active Venue & DJ Title Bar Integration**: Displays the active Venue and DJ name dynamically in the main window's title bar, maximizing vertical screen real estate on smaller screens.
- **Customizable Scaryoke Categories**: Add, edit, or remove categories (2 to 12 total) from the settings page. The Scaryoke wheel will dynamically rebuild its structure, sector colors, play a mechanical pointer clicking sound synchronized to sector crossings, and project the active wheel and category announcement onto the rotation billboard window.

### 🖥️ Display & Projection Management
- **Dual-Window Projection**: Supports launching standalone windows for **Lyrics Projection** and the **Rotation Billboard**.
- **Four Dynamic Billboard View Modes**:
  - **Normal List**: A standard listing of the current singer and upcoming rotation queue.
  - **Star Wars Crawl**: High-fidelity 3D projection rendering a starry night sky with cool/warm twinkling star layers, and a 3D-angled text block crawling upward in space.
  - **Vegas Marquee**: Theatrical Broadway stage layout displaying the current performer's name in glowing letters inside a brass frame ringed by lavender/purple "marching ants" chase bulb animations.
  - **Vinyl Turntable**: Classic warm DJ-booth theme with a dynamic rotating vinyl 45 record, tonearm, and center label showing the current singer and song metadata.
- **Dynamic Chroma-Keying**: Automatically strips standard `.cdg` file backgrounds and borders (pixel index `0,0`) in real time to render lyrics transparent.
- **GPU-Accelerated 4K Backdrops**: Beautiful, responsive vector backdrops layered behind transparent lyrics, wrapped in Viewbox controls to fit HD and 4K displays.
- **Flexible Monitor Assignment & Dropdowns**: Direct dropdown selection in the KJ interface to target specific connected monitors for both the Rotation Display and DJ Banner windows, with real-time dynamic window relocation and automatic fallback to secondary/primary screens if unplugged.
- **DJ Banner Projection Screen**: Upload, select, delete, and project borderless full-screen custom DJ branding/promotional banners (supporting PNG, JPG, GIF, BMP, and looping MP4 video).
- **Same-Screen Deconfliction Priority**: Automatically disables and hides the DJ Banner if the Rotation Display is targeted or moved to the same monitor.
- **Wireless Casting Support**: Stream the singer rotation billboard directly to Miracast, Chromecast, AirPlay, Wireless HDMI, or Browser Cast using high-performance off-screen buffer rendering.
- **Browser Cast Server**: Self-hosts a local web server (http://localhost:8080/rotation/) to allow any browser on the local network to view the singer rotation billboard in real-time.
- **"None" Targeting (Deselection)**: Support for selecting *None (Do not show)* in settings or projection controls to immediately close or hide projection windows when not in use.
- **Rear-Projection Mirroring**: Mirror the lyrics screen horizontally for custom projector arrangements.

### 🔊 Audio Configuration & Background Music (BGM)
- **Advanced Audio Engine**: Master volume, latency compensation, and active output device selection.
- **Active Performance Key Transposition**: Real-time pitch transposition from `-6` to `+6` semitones. Automatically hot-reloads the audio track and seeks back to the exact millisecond in under 150ms to apply FFmpeg-based pitch shifting (`asetrate` + `atempo`) during active playback.
- **Audio Processing Controls**: Fully adjustable 3-band EQ (Bass, Mid, Treble) alongside integrated Compressor and Limiter.
- **Show Flow Automation**: Automatically manages background music levels between tracks (Opening Music, Fill-in Music, and End of Rotation sets).
- **Configurable Fill-In Music Delay**: Settings slider (0-30s) to set the exact delay before fill-in background music starts.
- **Special Occasion Audio**: Add custom sound bites and clips (Birthdays, Holiday jingles) with individual tone (Bass/Treble) and gain overrides.

### 🎵 Song Library & Integrations
- **Local Scanner & Rescan Maintenance**: Scans folders for `.mp3 + .cdg`, `.mp4` or `.zip` files and builds a local query database. Cleanly deletes obsolete database logs and search indexes for files that were renamed or deleted on the drive during rescans.
- **Catalog Book Exporter**: Export the entire song library (Karaoke or Music) directly into professionally formatted, paginated PDF or Word (.docx) documents with repeating table headers from the Database Manager.
- **Streaming & Search Integration**: Includes search support for:
  - **Party Tyme Karaoke** (built-in streaming provider)
  - **YouTube** (direct URL stream linking and metadata lookups)
  - **Spotify & Amazon Music** links
- **Singer History DB**: Automatically saves matching song, artist, singer, and links for instant lookup in future sessions.

### 💾 Database Maintenance & Stability
- **Live Online Backups**: Instantly back up settings, singer logs, playlists, and performer queue to an external SQLite `.db` file using live `VACUUM INTO` operations.
- **Graceful Restore**: Restore data from backup files; the application closes database connections safely and shuts down for reboot validation automatically.

### 📱 Tablet Lyrics Web Server
- **Local Web Server**: Serves a mobile-friendly web page (defaulting to port `5005`) over the local network.
- **Singer Preview Monitor**: Allows singers to scan a local QR code and follow the live scrolling lyrics directly from their phone or tablet.
- **"Scan to Join" QR Badges**: Auto-generated QR codes linking to the tablet portal appear as a badge on the Karaoke page header and as a floating overlay on the Lyrics projection window.
- **Online Song & Artist Lookup**: Real-time lookup querying the iTunes search index from the patron's mobile browser with automatic form populating and offline fallback.
- **Emoji Crowd Reactions**: Singers can send 👏 🔥 ❤️ 🙌 🎉 👑 reactions from their device that render in full color and float in from a random edge of the screen, drifting and fading across both the Lyrics and Rotation projection screens in real time.
- **Live Rating Sync**: The tablet dashboard shows the current performer's live average rating and labels the rating card with the host's chosen feedback icon, updating instantly after each new submission.
- **Live Server Log Viewer**: A "Logs" tab in the tablet portal surfaces the most recent app and error log entries for on-the-fly troubleshooting.
- **Karaoke & Music Song Requests**: Singers can search the karaoke catalog to request a song to perform, or search a separate background-music library to request a track just be played — both flow into the KJ's Requests queue, and approving a Karaoke request adds the singer straight into the active rotation.
- **Auto-Accept Requests**: Optional toggle (Requests page in Lyracist, next to the Incoming Requests list in KSRotation) that skips manual approval entirely — Karaoke requests go straight into the rotation and Music requests go straight to Approved the instant they arrive.
- **Scaryoke Access Control**: The Scaryoke tab and wheel spin are only available on singers' phones once the host enables Scaryoke Mode, and only the currently-performing singer can trigger a spin — everyone else watches it live.

### ❓ Split-Pane Help System
- **Interactive Index Menu**: Left-pane sidebar allowing direct navigation between 12 detailed help categories covering every user-facing page and setting.
- **Rich Detail Panel**: Right-pane scroll-contained viewer displaying page guides, setting definitions, tone balancing parameters, decoders, and API settings, preventing excessive page scrolling.

---

## Tech Stack
- **Framework**: WPF (.NET 10.0-windows)
- **Design System**: `Wpf.Ui` (harmonious dark/light theme management)
- **Database**: SQLite backed by Entity Framework Core (EF Core)
- **MVVM Pattern**: CommunityToolkit.Mvvm (source generators, observable properties, relay commands)
- **Media Playback**: LibVLC / LibVLCSharp & FFmpeg interop for high-performance `.cdg` and video rendering

---

## Installation & Setup

### Requirements
- **OS**: Windows 10 / 11
- **Runtime**: .NET 10.0 SDK or Desktop Runtime
- **External Dependencies**: FFmpeg (expected at `C:\ffmpeg\bin\ffmpeg.exe` and `ffprobe.exe`)

### Configuration
1. Open **Settings** on the left menu.
2. In **Service Logins & API Keys**, enter credentials for YouTube, Spotify, or Party Tyme if using streaming search.
3. In **Theme & Appearance**, toggle **Test Mode** on to test queue workflows.
4. Set up monitor assignments under **Display & Projection Monitors** or the main control panel.
5. Manage backups and database restorations in the **Database Maintenance** section under the **Music Library** group box.
6. Curate the DJ Name, active Venues catalog list, Billboard View Mode, and Star Wars Crawl Text Template (Dramatic, Comedic, Over-the-Top, or Custom) on the Settings page, with real-time text previews.