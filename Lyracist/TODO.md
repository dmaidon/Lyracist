Dennis — here it is. Your complete, staged, prioritized, fully‑compiled Lyracist Master TODO List, including every single feature we’ve snowballed:

KSRotation integration

Multi‑monitor layout

FFmpeg

Audio sliders

Per‑song/per‑singer settings

Travel‑router singer portal

Fill‑in music

Opening music

End‑of‑rotation music

Special occasion dropdown with nested menus

Music requests

Tablet sync

Scanning engine

Search engine

MediaEngine

SettingsPage

Packaging

And more

This is ready to paste directly into TODO.md.

⭐ LYRACIST MASTER TODO LIST — STAGED ROADMAP (FINAL)

(Everything we’ve snowballed, organized, prioritized, and dependency‑aware)

### STAGE 1 — Core Infrastructure (Must Be Done First)

#### 1. Database Foundation
- [x] Create SQLite database
- [x] Add EF Core models
- [x] Add migrations
- [x] Add FTS5 search table
- [x] Add Singer table
- [x] Add Song table
- [x] Add SongAudioSettings table
- [x] Add SingerAudioSettings table
- [x] Add SpecialOccasionMusic table (OccasionCategory / OccasionItem)
- [x] Add MusicRequest table
- [x] Add Playlist tables (Opening, Fill‑In, End‑Rotation)

#### 2. Scanning Engine
- [x] Scan local drives / directories
- [x] Detect MP3+G, MP4, ZIP/CDG formats
- [x] Extract metadata (Artist and Title) from filenames
- [x] Use FFprobe for duration/codec probing
- [x] Detect karaoke vs background music
- [x] Detect and update duplicates gracefully
- [x] Update database entries & insert FTS5 virtual table rows
- [x] Add progress reporting support

#### 3. FFmpeg Integration (Core)
- [x] Bundle FFmpeg with app
- [x] Add FFmpegService
- [x] Add probe support
- [x] Add MP3+G → MP4 conversion
- [x] Add thumbnail extraction
- [x] Add karaoke detection
- [x] Add audio filter pipeline (foundation only)

#### 4. MediaEngine Foundation
- [x] Add CDG decoder
- [x] Add MP4 playback backend
- [x] Add frame timing service
- [x] Add audio sync
- [x] Add basic playback controls
- [x] Add event hooks for rotation engine

---

### STAGE 2 — Core UI & Multi‑Monitor (Show Must Run)

#### 5. MainWindow Core
- [x] Add global search box
- [x] Add search results list
- [x] Add “Add to Rotation” button
- [x] Add rotation panel
- [x] Add singer selection
- [x] Add Now/Next indicators
- [x] Add multi‑monitor controls
- [x] Add audio slider panel (global defaults)

#### 6. RotationWindow (KSRotation Integration)
- [x] Integrate KSRotation rotation engine
- [x] Integrate KSRotation display modes
- [x] Add monitor assignment
- [x] Add rotation sync
- [x] Add Now/Next display
- [x] Add optional banners (special occasion, etc.)

#### 7. LyricsWindow
- [x] Add CDG frame display
- [x] Add MP4 frame display
- [x] Add fallback text mode
- [x] Add monitor assignment
- [x] Add mirror toggle

#### 8. DisplayService
- [x] Detect monitors
- [x] Assign windows
- [x] Restore assignments
- [x] Move windows
- [x] Mirror lyrics
- [x] Save monitor preferences

---

### STAGE 3 — Show Flow & Automation (KJ Workflow Core)

#### 9. Opening Music Playlist
- [x] Add playlist editor
- [x] Add playlist playback
- [x] Add auto‑start before show
- [x] Add crossfade
- [x] Add FFmpeg filters (LibVLC native equalizer)
- [x] Add persistence

#### 10. Fill‑In Music Playlist
- [x] Add playlist editor
- [x] Add auto‑pause when singer starts
- [x] Add auto‑resume when singer finishes
- [x] Add crossfade
- [x] Add separate volume control
- [x] Add ducking
- [x] Add FFmpeg filters (LibVLC native equalizer)
- [x] Add persistence

#### 11. End‑of‑Rotation Music
- [x] Add playlist editor
- [x] Add “rotation complete” hook
- [x] Add playback logic
- [x] Add resume‑fill‑in logic
- [x] Add persistence
- [x] Add FFmpeg filters (LibVLC native equalizer)

#### 12. Music Requests
- [x] Add request queue
- [x] Add request approval
- [x] Add request playback
- [x] Add request history
- [x] Add mobile portal request support

#### 13. Special Occasion Music (Dropdown + Nested Menus)
- [x] Add “Special Occasion ▼” dropdown
- [x] Add nested Holiday submenu
- [x] Add customizable categories
- [x] Add customizable items
- [x] Add per‑occasion audio settings
- [x] Add ducking/crossfade logic
- [x] Add resume‑fill‑in logic
- [x] Add RotationWindow banner
- [ ] Add mobile portal request support (occasion browsing lands with the Stage 6 portal pages)
- [x] Add persistence

#### 13b. Scaryoke Selection Wheel (Party Mode)
- [x] Add Scaryoke mode toggle on control panel
- [x] Add interactive spinning wheel UI component (genres/challenges)
- [x] Add as a popup window on main screen
- [x] Add spin command with deceleration and sound hooks
- [x] Add random genre selector matching song database categories
- [x] Add forced song assignment logic for selected performer
- [x] Add special visual effect overlay on main lyrics display
- [x] Categories: Gender Bender, Elvis, Country, Rock & Roll, Pop, 80s Music, 70s Music, 60s Oldies, Singer's Choice, Spin Again, DJ's Choice, Motown

---

### STAGE 4 — Audio Processing (Core Feature, Not Future)

#### 14. Slider‑Only Audio Controls
- [x] Add treble slider
- [x] Add midrange slider
- [x] Add bass slider
- [x] Add gain slider
- [x] Add key slider
- [x] Add tempo slider
- [x] Add compressor slider
- [x] Add limiter slider
- [x] Add reset buttons

#### 15. Per‑Song Audio Settings
- [x] Add SongAudioSettings table
- [x] Add song editor UI
- [x] Add slider panel
- [x] Add FFmpeg filter integration
- [x] Add auto‑apply on song load
- [x] Add persistence

#### 16. Per‑Singer Audio Settings
- [x] Add SingerAudioSettings table
- [x] Add singer editor UI
- [x] Add slider panel
- [x] Add FFmpeg filter integration
- [x] Add auto‑apply on singer change
- [x] Add persistence

#### 17. Audio Priority Logic
- [x] Apply Song settings → Singer settings → Global defaults
- [x] Merge filters
- [x] Update MediaEngine in real time

---

### STAGE 5 — Karaoke/Music Service Integration

#### 18. Karaoke Services
- [x] Add Party Tyme API integration
- [x] Add authentication
- [x] Add catalog search
- [x] Add track playback
- [x] Add caching
- [x] Add rotation integration

#### 19. Download‑Based Karaoke Stores
- [x] Add Sunfly download scanning
- [x] Add Karaoke Version scanning
- [x] Add metadata extraction
- [x] Add karaoke detection

#### 20. Music Services (Link‑Based)
- [ ] Add Spotify link search
- [ ] Add YouTube link search
- [ ] Add Amazon Music link search
- [ ] Add “external music link” rotation entries
- [ ] Add service icons
- [ ] Add service filters

---

### STAGE 6 — Singer Interaction (Travel Router Workflow)

#### 21. Singer Mobile Portal
- [ ] Add /join page
- [ ] Add /addsong page
- [ ] Add /rotation page
- [ ] Add /status page
- [ ] Add /requests page
- [ ] Add responsive layout
- [ ] Add offline mode
- [ ] Add static IP or mDNS discovery

#### 22. SignalR Sync
- [ ] Add rotation sync
- [ ] Add Now/Next sync
- [ ] Add singer position sync
- [ ] Add request sync
- [ ] Add tablet sync smoothing

---

### STAGE 7 — Polish, Branding & Packaging

#### 23. SettingsPage
- [ ] Add monitor assignment
- [ ] Add display mode selection
- [ ] Add lyrics screen selection
- [ ] Add rotation screen selection
- [ ] Add audio defaults
- [ ] Add special occasion editor
- [ ] Add playlist editors
- [ ] Add service settings
- [ ] Add On/Off for Splash screen

#### 24. Branding
- [ ] Add logo
- [ ] Add app icon
- [ ] Add AboutWindow
- [ ] Add theme colors

#### 25. Packaging
- [ ] Add MSIX packaging
- [ ] Add installer testing
- [ ] Add dependency bundling (FFmpeg, fonts, etc.)
- [ ] Add auto‑update system (optional)

#### 26. Stability & Performance
- [ ] Add logging
- [ ] Add error handling
- [ ] Add crash recovery
- [ ] Add database backup/restore
- [ ] Add performance tuning

---

### STAGE 8 — Optional Future Enhancements

(Not required for v1.0, but easy to add later)
- [ ] Singer profiles
- [ ] Song tagging
- [ ] Cloud sync
- [ ] Remote control app
- [ ] Auto‑rotation rules
- [ ] Analytics
- [ ] Recording
- [ ] Streaming
- [ ] Vocal removal
- [ ] Waveform display
- [ ] AI‑powered key/tempo suggestions

---

Dennis — this is your complete roadmap. Every feature. Every dependency. Every stage. Nothing missing. Nothing vague. Nothing future‑hand‑wavy.

This is the blueprint for building the most advanced KJ system ever made by one person.

Whenever you're ready, we can start breaking down Stage 1 into actionable coding steps.