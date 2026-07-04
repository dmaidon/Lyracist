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
- [ ] Add CDG frame display
- [ ] Add MP4 frame display
- [ ] Add fallback text mode
- [ ] Add monitor assignment
- [ ] Add mirror toggle

#### 8. DisplayService
- [ ] Detect monitors
- [ ] Assign windows
- [ ] Restore assignments
- [ ] Move windows
- [ ] Mirror lyrics
- [ ] Save monitor preferences

---

### STAGE 3 — Show Flow & Automation (KJ Workflow Core)

#### 9. Opening Music Playlist
- [ ] Add playlist editor
- [ ] Add playlist playback
- [ ] Add auto‑start before show
- [ ] Add crossfade
- [ ] Add FFmpeg filters
- [ ] Add persistence

#### 10. Fill‑In Music Playlist
- [ ] Add playlist editor
- [ ] Add auto‑pause when singer starts
- [ ] Add auto‑resume when singer finishes
- [ ] Add crossfade
- [ ] Add ducking
- [ ] Add FFmpeg filters
- [ ] Add persistence

#### 11. End‑of‑Rotation Music
- [ ] Add playlist editor
- [ ] Add “rotation complete” hook
- [ ] Add playback logic
- [ ] Add resume‑fill‑in logic
- [ ] Add persistence

#### 12. Music Requests
- [ ] Add request queue
- [ ] Add request approval
- [ ] Add request playback
- [ ] Add request history
- [ ] Add mobile portal request support

#### 13. Special Occasion Music (Dropdown + Nested Menus)
- [ ] Add “Special Occasion ▼” dropdown
- [ ] Add nested Holiday submenu
- [ ] Add customizable categories
- [ ] Add customizable items
- [ ] Add per‑occasion audio settings
- [ ] Add ducking/crossfade logic
- [ ] Add resume‑fill‑in logic
- [ ] Add RotationWindow banner
- [ ] Add mobile portal request support
- [ ] Add persistence

#### 13b. Scaryoke Selection Wheel (Party Mode)
- [ ] Add Scaryoke mode toggle on control panel
- [ ] Add interactive spinning wheel UI component (genres/challenges)
- [ ] Add as a popup window on main screen
- [ ] Add spin command with deceleration and sound hooks
- [ ] Add random genre selector matching song database categories
- [ ] Add forced song assignment logic for selected performer
- [ ] Add special visual effect overlay on main lyrics display
- [ ] Categories: Gender Bender, Elvis, Country, Rock & Roll, Pop, 80s Music, 70s Music, 60s Oldies, Sad Songs, Romantic Duet, DJ's Choice, Motown

---

### STAGE 4 — Audio Processing (Core Feature, Not Future)

#### 14. Slider‑Only Audio Controls
- [ ] Add treble slider
- [ ] Add midrange slider
- [ ] Add bass slider
- [ ] Add gain slider
- [ ] Add key slider
- [ ] Add tempo slider
- [ ] Add compressor slider
- [ ] Add limiter slider
- [ ] Add reset buttons

#### 15. Per‑Song Audio Settings
- [ ] Add SongAudioSettings table
- [ ] Add song editor UI
- [ ] Add slider panel
- [ ] Add FFmpeg filter integration
- [ ] Add auto‑apply on song load
- [ ] Add persistence

#### 16. Per‑Singer Audio Settings
- [ ] Add SingerAudioSettings table
- [ ] Add singer editor UI
- [ ] Add slider panel
- [ ] Add FFmpeg filter integration
- [ ] Add auto‑apply on singer change
- [ ] Add persistence

#### 17. Audio Priority Logic
- [ ] Apply Song settings → Singer settings → Global defaults
- [ ] Merge filters
- [ ] Update MediaEngine in real time

---

### STAGE 5 — Karaoke/Music Service Integration

#### 18. Karaoke Services
- [ ] Add Party Tyme API integration
- [ ] Add authentication
- [ ] Add catalog search
- [ ] Add track playback
- [ ] Add caching
- [ ] Add rotation integration

#### 19. Download‑Based Karaoke Stores
- [ ] Add Sunfly download scanning
- [ ] Add Karaoke Version scanning
- [ ] Add metadata extraction
- [ ] Add karaoke detection

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