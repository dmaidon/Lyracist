# Lyracist

Lyracist is a premium, modern Windows WPF Karaoke hosting application designed for professional KJs and home entertainment. Built using WPF and .NET 10, it offers advanced multi-monitor projection, rich audio customization, local and streaming library search, and an integrated mobile tablet server for performer lyrics.

---

## Key Features

### 🎙️ Performer & Rotation Queue
- **Interactive Singer Queue**: Dynamic list matching performer names with requested song, artist, key changes, and custom notes.
- **Click-to-Deselect**: Easily toggle current singer selection on/off (highlighted in red) to allow correcting misclicks.
- **Test Mode**: Accessible under Settings → Theme & Appearance to instantly seed default performers (Alice, Bob, Charlie, Diana) for audio/video checks, or clear them when ready for the show.
- **Customizable Scaryoke Categories**: Add, edit, or remove categories (2 to 12 total) from the settings page. The Scaryoke wheel will dynamically rebuild its structure, sector colors, play a mechanical pointer clicking sound synchronized to sector crossings, and automatically search the library for the landed category.

### 🖥️ Display & Projection Management
- **Dual-Window Projection**: Supports launching standalone windows for **Lyrics Projection** and the **Rotation Billboard**.
- **Flexible Monitor Assignment**: Direct dropdown selection in the KJ interface to project to any secondary monitor (with borderless, topmost, maximized sizing).
- **"None" Targeting (Deselection)**: Support for selecting *None (Do not show)* in settings or projection controls to immediately close or hide projection windows when not in use.
- **Rear-Projection Mirroring**: Mirror the lyrics screen horizontally for custom projector arrangements.

### 🔊 Audio Configuration & Background Music (BGM)
- **Advanced Audio Engine**: Master volume, latency compensation, and active output device selection.
- **Audio Processing Controls**: Fully adjustable 3-band EQ (Bass, Mid, Treble) alongside integrated Compressor and Limiter.
- **Show Flow Automation**: Automatically manages background music levels between tracks (Opening Music, Fill-in Music, and End of Rotation sets).
- **Special Occasion Audio**: Add custom sound bites and clips (Birthdays, Holiday jingles) with individual tone (Bass/Treble) and gain overrides.

### 🎵 Song Library & Integrations
- **Local Scanner**: Scans specified folders for `.mp3 + .cdg` or `.mp4` files and builds a fast local query database.
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