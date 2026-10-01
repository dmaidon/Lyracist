<!-- Edited on Oct 1, 2026 @ 09:09:00 -> Document Package Version 1.0.1 across solution projects and MAUI display version -->
# Lyracist Pro Suite - System Manual & Architecture Guide
    
Lyracist Pro is a premium, modern Windows WPF Karaoke hosting application designed for professional KJs and home entertainment. Built using WPF and .NET 10, it offers a safe, DJ-friendly Auto-Advance system with grace period timer and fill-in music ducking, advanced multi-monitor projection, rich audio customization, high-speed in-memory library scanning and metadata probing (TagLibSharp), local and streaming library search, an integrated mobile tablet server for performer lyrics, active rotation management with current performer top-floating, smart new singer round insertion, inactive singer filtering, intelligent name and artist proper-casing with mixed-case and apostrophe prefix support, spacious high-DPI singer cards with full button border visibility on 1080p laptops, global auto-highlighting/select-all on focus across all text and numeric inputs, responsive portrait/landscape tablet layouts and Android launch stability in `KSRotation.Maui`, dedicated cross-app landscape tablet kiosk request station (`kiosk.html`) with Attractor/Welcome screen and PWA fullscreen support, remote DJ web control with checkmark-first action buttons and popup modal performer addition (`dj.html`), integrated interactive pub/bar trivia with dedicated separate settings, manual DJ game flow controls with question jumping, timer adjustments, and keyboard shortcuts, multi-monitor auto-casting, randomized answer elimination, non-overlapping score and intermission screens, automated projection pause synchronization, dynamic tiered option value scoring (100% / 70% / 40%), Knockout Trivia standalone game-show module with phone/tablet QR connect, session security, testing module & DJ bot simulator, and automatic internal scoring (`KnockoutTrivia.exe`), a dedicated Trivia Database Creator (`TriviaDbCreator.exe`), and a unified, consolidated directory architecture across all apps (`Settings/`, `Data/`, `Banners/`, `Packs/`, `Logs/`).

---
    
## Key Features

### 🥊 Knockout Trivia Core Fixes & Enhancements (`KnockoutTrivia`, `KnockoutWebServer.cs`, `knockout.html`)
- **Real-Player Seeding & Disconnect Handling**: Removed hardcoded fake player initialization on launch; added a 20-second inactivity disconnect timeout in `IGameStateService.cs` so disconnected players do not block early reveal in Automatic mode, and rejoining players reconnect smoothly without duplicate roster entries.
- **Snapshot Isolation & Socket Stability**: Replaced live collection reads with immutable thread-safe snapshots (`_snapshotLock`, `_cachedSnapshot`) for HTTP socket threads, eliminating 500 internal errors and cross-thread mutation exceptions.
- **Super Streak Phone Screen & Asynchronous Fonts**: Added dedicated `screen-superstreak` UI to `knockout.html`, preloaded Google Fonts asynchronously with noscript fallback for offline venue Wi-Fi, added auto-rejoin recovery, and unlocked submit buttons on failure.
- **Wheel Single-Spin & Token Deduction**: Added single-spin guards to `WheelViewModel.cs` preventing repeat spins, and applied `SuperStreakTokensRemovedPerHit` deduction to targets.
- **Bot Simulator & Config Protection**: Guarded bot simulator loop against stale questions, switched `IConfigService` to atomic JSON writes with `.corrupt.bak` backups, and cached pack question counts by file timestamp.

### 📺 TV Display Window Leak Fix & Engine Rebinding (`Shared/TriviaDisplayViewModel.cs`)
- **Disposable Event Subscriptions**: Implemented `IDisposable` with named delegate unsubscriptions in `TriviaDisplayViewModel.cs`, eliminating 9 anonymous lambda memory leaks on TV window close/reopen.
- **Live Host Engine Rebinding**: Added `RebindEngine(TriviaGameEngine)` allowing open TV displays to instantly re-synchronize when a host resets a trivia session in `Lyracist` or `KSRotation`.

### 🎯 Interactive Pub & Bar Trivia Pro Enhancements (`Lyracist.Trivia`, `Lyracist.Trivia.Core`)
- **Question Revisit Protection (#11)**: Tracked answered question history (`_questionAnswers`) per player in `TriviaGameEngine.cs`. Navigating back via Previous Question, Go To Question, or dropdown preserves answered state, prevents re-answering and duplicate scoring, and protects answer streaks.
- **Standby Timer Preservation on Pause/Resume (#12)**: Tracked `_wasTimerRunningBeforePause` in `PauseGame()` and `ResumeGame()`, preventing standby reading mode timers from auto-starting when DJ banners resume.
- **Timer Drift Guard (#37)**: Added tick processing guard (`if (IsPaused || !_tickTimer.Enabled) return;`) in `ProcessTick()`.
- **Leaderboard Tie-Breakers & Pruning (#45)**: Deterministic player ordering by `TotalScore` -> `TotalCorrect` -> `LastResponseTimeMs` -> `Name`, plus `PruneDisconnectedPlayers()` and `ClearAllPlayers()`.
- **Answer Leak Protection**: Plugged state payload answer leak in `TriviaWebServer.cs` by gating `isCorrect` and `pointsEarned` on `isRevealed`.
- **Atomic File Operations & Database Cleanup**: Added atomic settings write with corrupt backup in `TriviaStorageHelper.cs`, `SearchOption.TopDirectoryOnly` and wraparound de-duplication in `TriviaPackManager.cs`, and `DeleteQuestion`/`DeleteQuestionsByIds` in `TriviaDatabaseService.cs`.
- **TriviaDbCreator Authoring Polish**: Cleaned up old pack files on ID rename, deleted questions from SQLite `trivia.db` on deletion, used collision-free question ID generation, and prompted before overwriting existing packs on import.
- **Safe Pregame Question Validation**: Validated question sets before stopping countdown timers or dismissing screens in `Lyracist.Trivia/ViewModels/MainViewModel.cs`.
- **Leaderboard Auto-Advance Fix (#3)**: Round leaderboard interstitial display automatically advances to the subsequent question (`AdvanceToNextQuestion()`) upon the 8-second countdown expiration when `AutoAdvanceQuestions` is enabled, rather than restarting the previously answered question.
- **Knockout Web Server & Companion Security/Performance**:
  - Request body buffer capped at 8 KB to prevent slow-body memory exhaustion.
  - Registration rate limiter raised from 5 to 60 registrations per 5-minute window with automated expired-IP pruning to support packed venue Wi-Fi NAT environments.
  - Pre-encoded UTF-8 bytes cached for `knockout.html` (`_cachedHtmlBytes`) to avoid redundant encodings on repeated mobile browser hits.
  - Static `JsonSerializerOptions` reused across endpoints to eliminate allocation overhead during answer rushes.
  - Added `QuestionId` validation to both the server endpoint (`/api/knockout/submit`) and mobile buzzer client (`knockout.html`), rejecting stale answers submitted during question transitions.
  - Added in-flight request guard (`isPolling`) to `knockout.html` to eliminate cascading request queues over cellular or high-latency Wi-Fi.
  - Client join handling hardened against HTTP 429 and 500+ server responses with visual feedback.
  - Protected `OnTimerTick`, auto-advance timer, and auto-reveal continuations in `GameStateService.cs` with `try / catch` exception guards.
  - Updated `ConnectViewModel.cs` to accurately track active connected players (`Count(p => p.IsConnected)`).
- **Universal Wi-Fi QR Reserved Character Escaping**:
  - Centralized `WifiHelper.EscapeWifiQrValue()` to escape reserved characters (`\`, `;`, `,`, `"`, `:`) in accordance with the MECARD specification across all apps (`KnockoutTrivia`, `Lyracist.Trivia`, `KSRotation`, and `Lyracist`).
- **Lyracist & KSRotation Trivia Stability & Performance**:
  - Guarded `OnPreGameTimerTick` in `Lyracist/ViewModels/TriviaViewModel.cs` and `OnTriviaPreGameTimerTick` in `KSRotation/ViewModels/MainViewModel.Trivia.cs` with `try / catch` exception handlers to prevent timer tick exceptions from crashing the host UI dispatcher.
  - Offloaded synchronous question pack database seeding in `Lyracist/ViewModels/TriviaViewModel.cs` to a background task so UI startup remains instantaneous.

### 🎯 Interactive Pub & Bar Trivia Pro Enhancements (`Lyracist.Trivia`, `Lyracist.Trivia.Core`)
- **Leaderboard Auto-Advance Fix (#3)**: Round leaderboard interstitial display automatically advances to the subsequent question (`AdvanceToNextQuestion()`) upon the 8-second countdown expiration when `AutoAdvanceQuestions` is enabled, rather than restarting the previously answered question.
- **Games-Played Counter & Multi-Run Support (#4)**: Explicitly resets the games-played counter on fresh manual game launches (`isAutoRestart: false` and `ResetGamesPlayedCount()`), preserving accurate game progression (`Game X of Y`) and preventing subsequent runs from immediately tripping the game cap on game 1.
- **Stale Submission Prevention via Question ID Validation (#5)**: Added `QuestionId` validation to both the mobile client payload (`trivia.html`), the HTTP API server (`TriviaWebServer.cs`), and the game engine core (`TriviaGameEngine.cs`). In-flight answers submitted during question transitions cannot score against subsequent questions.
- **Option Un-fade Restoration on Timer Reset (#6)**: TV display projection windows (`DisplayViewModel.cs`, `TriviaDisplayViewModel.cs`) and phone buzzer screens (`trivia.html`) correctly restore eliminated options back to full opacity and re-enable choices whenever the host triggers a Question Timer Reset.
- **Mobile Client Join and Submission Error Handling (#7)**: Phone client (`trivia.html`) handles HTTP 429 rate limit delays and 500+ server errors with clear user feedback rather than failing silently, and validates submission responses to ensure rejected late answers reset submission states and display warnings.
- **Dynamic Score Ticker Marquee (#8)**: Screen projection windows across all three host applications (`TriviaDisplayWindow.xaml.cs`) dynamically compute container and text widths at a constant 65 px/sec velocity, preventing pop-in and truncation across all display resolutions and patron counts.
- **Venue Registration Rate Limiting & Table Pruning (#9)**: Expanded registration throttling from 5 to 60 registrations per 5-minute window in `TriviaWebServer.cs` to support busy venue Wi-Fi NAT environments, with automatic pruning of expired entries to prevent memory growth.
- **Guarded Timer Callbacks (#11)**: Wrapped `OnPreGameTimerTick` in `MainViewModel.cs` in robust try/catch blocks with diagnostic tracing, preventing unhandled exceptions from terminating .NET Core / .NET 10 processes during unattended pre-game countdowns.
- **Web Server Allocations & Request Limits**: Cached pre-encoded UTF-8 bytes for `trivia.html`, reused static `JsonSerializerOptions` instances to eliminate GC churn during answer bursts, and capped HTTP request bodies at 8 KB to prevent slow client body uploads from exhausting memory.
- **In-Flight Polling Guard**: Added `isPolling` guards to `trivia.html` state requests so high-latency Wi-Fi connections do not pile up concurrent requests.
- **In-Place UI Synchronization & INotifyPropertyChanged**: Implemented `INotifyPropertyChanged` on `TriviaPlayer` and updated `MainViewModel`, `DisplayViewModel`, and `TriviaDisplayViewModel` to update collections in place rather than wiping and rebuilding them on every poll or join.
- **Debounced Settings & Wi-Fi Generation**: Debounced `SaveSettings()` (400ms) and Wi-Fi QR updates (300ms) in `MainViewModel.cs`, preventing UI stalls and repeated disk/QR writes while typing.
- **Asynchronous SQLite Seeding**: Moved initial question pack SQLite database synchronization off the UI thread into a background task in `LoadQuestionPacks()`, ensuring instantaneous window startup.
- **Non-Blocking Netsh Fallback**: Replaced synchronous `ReadToEnd()` in `WifiHelper.cs` with an asynchronous read and 1-second timeout to protect UI threads from network shell hangs.
- **Uniform Viewbox TV Scaling**: Wrapped trivia display canvases in uniform `Viewbox` controls across all host apps, guaranteeing pristine, unclipped layouts at 125%, 150%, and 4K TV scaling.

### 📺 DJ Banner Target Monitor TV Overscan Guidance (`KSRotation`, `Lyracist`, `HelpViewModel.cs`)
- **TV Hardware Overscan Prevention**: When outputting the dedicated DJ Banner window to external TVs (particularly older 720p/1080p hospitality or venue screens), television hardware frequently applies default overscan processing, cropping and zooming into the edges of the banner graphics.
- **In-App Warning Tooltip**: Positioned directly beside the **Target Monitor** / **DJ Banner Screen** settings controls in both KSRotation and Lyracist, a help icon (``) provides DJs with instant reminder guidance on TV picture setup.
- **Recommended TV Picture Settings**: Set TV aspect ratio / picture size to **"Just Scan"**, **"Screen Fit"**, **"1:1 Pixel Mapping"**, **"Full Pixel"**, or disable **"Overscan"** in the television's hardware menu to prevent edge cropping.

### 🔄 Seamless Bidirectional Device Switching & Live Session Handoff (`KSRotation`, `KSRotation.Maui`, Web Portal)
- **Zero-Beat Event Handover**: Seamlessly transfer an active karaoke show between a Windows laptop running `KSRotation` and a mobile tablet running `KSRotation.Maui` (Android or Windows) when the host needs to leave with the laptop or return to the main DJ booth.
- **Architectural Flow: Live Session Handoff & Remote DJ Takeover**:
```text
┌────────────────────────────────────────────────────────────────────────┐
│        ARCHITECTURAL FLOW: LIVE SESSION HANDOFF & REMOTE DJ TAKEOVER   │
└────────────────────────────────────────────────────────────────────────┘

    DJ (Tablet: KSRotation.Maui)             KJ Host (Laptop: KSRotation)
    ┌───────────────────────────┐           ┌────────────────────────────┐
    │ Active rotation on tablet │           │ Arrives at venue & opens   │
    │ Serves requests & portal  │           │ 'Switch Device' on laptop  │
    └─────────────┬─────────────┘           └──────────────┬─────────────┘
                  │                                        │
                  │                                 [1] Wi-Fi Scan or IP
                  │                                     Selects tablet
                  │                                        │
                  │<─────── 1. GET /api/session/handoff ───┤
                  │         (Sends Laptop IP, Port & PIN)  │
                  │                                        │
                  ├─────── 2. 200 OK + Full Session JSON ─>│
                  │        (Queue, Checkmarks, History)    │
                  │                                 [2] Imports session
                  │                                     Takes over host
                  │                                        │
  [3] Auto-switches to in-app                             │
      Remote DJ (dj.html)                                  │
      Pre-authenticated with PIN                           │
                  │                                        │
  [4] DJ manages show from floor via embedded tablet UI    │
      ═════════════════════════════════════════════════════╪═══════════════
      Seamless bi-directional synchronization over venue Wi-Fi!
```
- **Automated In-App Remote DJ Transition**: When the desktop host pulls an active session from the DJ's tablet, `KSRotation.Maui` automatically transitions into a full-screen embedded in-app Remote DJ controller (`dj.html`), pre-authenticated with the host's credentials so the DJ never leaves the native app and can immediately continue queue management from the floor.
- **Complete State Preservation**: Transmits the entire active queue, currently performing singer (`IsCurrent`), on-deck singer (`IsNext`), rotation start anchor (`IsRotationStart`), paused and inactive performers, queued tracks, linked duet pairs, completed round checkmarks (rounds 1–10), tonight's performance history with original timestamps, and pending patron requests.
- **Dedicated Switch Device QR Code & Web Portal (`handoff.html`)**: Generates a dedicated high-resolution QR code (`/handoff`). Scanning with any camera opens a sleek mobile landing page showing live venue stats, current singer, active queue counts, and a 1-tap "Open in KSRotation MAUI" button.
- **Native Deep Linking (`ksrotation://handoff`)**: Android tablets tap-to-launch directly into `KSRotation.Maui` with pre-filled host IP, port, and security PIN, prompting for 1-tap takeover.
- **One-Tap Wi-Fi Auto-Discovery**: Built-in 1-second subnet sweeper detects running host peers on the venue Wi-Fi automatically without requiring manual IP address typing. Selecting a discovered peer automatically focuses and selects the DJ PIN field for instant entry.
- **Full Bidirectional Capability**: Effortlessly push from Laptop to Tablet, and pull from Tablet back to Laptop without dropping a single song, singer, or round placement.
- **Offline & Manual Resilience**: Supports manual IP/PIN entry, manual JSON push, robust socket stream flushing, and downloadable standalone `.ksr` session backup files for isolated venue networks.

### ⏳ Dynamic Estimated Wait Times on Reorder & Move (`KSRotation`, `Lyracist`, `Shared`)
- **Instant Wait Time Recalculation**: Moving any performer up or down in the rotation (via desktop `▲`/`▼` buttons, context menu, tablet controls, or remote DJ web commands) immediately recalculates cumulative wait times based on the new queue position.
- **Display Synchronization**: Immediately pushes updated wait times and position numbers to `SingerDisplayWindow` (Normal list, Vegas Marquee, Vinyl Turntable, and Star Wars Crawl) and audience billboard displays.
- **Desktop Queue Badging**: Singer rows in the desktop console (`MainWindow.xaml`) feature an estimated wait badge (`⏳ {0}m`) with tooltips, allowing the DJ to inspect wait estimates directly in the primary host window.

### ⏱️ Dynamic Round Completion Estimation & Full Round Duration Notice (`dj.html`, `KSRotation`, `KSRotation.Maui`, `Lyracist`, `Shared`)
- **End-of-Night Decision Support**: Instantly informs the host/DJ how much time is remaining in the active round and projects the exact completion clock time (ETA), answering the critical question: *"Is there time for another round before last call or venue closing?"*
- **Remote DJ Status Notice (`dj.html`)**: Pinned color-accented status banner directly beneath the Last Round banner displaying performers left in the round, estimated minutes remaining, projected finish time (e.g. `ends ~11:42 PM`), and full round duration. Auto-refreshes every 15 seconds and updates in real time on any queue modifications.
- **Desktop Karaoke Station (`KSRotation`)**: Pinned notice badge beside active singer counts in the main Rotation header toolbar: `⏱️ Round: 4 singers left • ~20m (ends ~11:42 PM) | Full round: ~25m (5 singers)`.
- **Mobile Tablet App (`KSRotation.Maui`)**: Pinned estimation banner above the singer rotation list on Android and Windows tablets.
- **Lyracist Pro Suite (`Lyracist`)**: Pinned estimation banner above the singer queue on the Rotation management page.
- **Smart Round Progression**: Accurately traces circular rotation from current performer to the rotation anchor (`IsRotationStart`), handles Last Round mode (`!HasSungInLastRound`), and excludes paused, skipped, inactive performers, and filler background music tracks.

### 🎤 Active Singer Count on Vegas Billboard & Vinyl Record Banners (`KSRotation`, `Lyracist`)
- **Live Rotation Singer Count on Audience Projection Screens**: The Broadway / Vegas Marquee and Vinyl Turntable screens on the detached rotation display window (`SingerDisplayWindow.xaml` in `KSRotation` and `RotationWindow.xaml` in `Lyracist`) prominently show the current number of active singers in the rotation.
- **Top Header & On-Deck Placement**: Displayed as a glowing golden badge pill (`🎤 X Singers in Rotation` or `🎤 1 Singer in Rotation`) both in the main header (alongside now-spinning and marquee title headers) and beside the "UP NEXT" / "ON DECK" section banners.
- **Dynamic & Accurate**: Automatically counts eligible non-music performers currently in rotation, handles singular vs plural grammar (`1 Singer in Rotation` vs `{N} Singers in Rotation`), respects Last Round mode, and hides automatically when no active performers remain.

### ⭐ Special Singer (One-Time Performance) (`Shared`, `Lyracist`, `KSRotation`, `KSRotation.Maui`, Web UI)
- **One-Time Guest Performance**: Accommodates guest performers, special dedications, or one-off singers who are called up to sing a single song without joining the permanent nightly rotation.
- **Instant Top of Queue & Current Performer**: When added or designated, the special singer is immediately placed at the top of the queue (index 0) and promoted to current performer, perfectly catering to spur-of-the-moment guest performances.
- **Instant Name Selection on Add**: Whenever a regular or special singer is added, the text in the performer name field is automatically selected so the host can immediately type the new name without clicking or dragging to highlight.
- **Displaced Singer Preservation**: Preserves the previous current or next singer as 'Up Next' so that rotation resumes with them seamlessly once the special performance concludes.
- **"⭐ Special" Badge While Performing**: During their turn on stage, the performer is prominently highlighted with a `⭐ SPECIAL` badge across the DJ consoles, detached rotation display windows, external audience billboard screens, Chromecast TV displays, kiosk stations, and mobile web portals.
- **Automatic Inactive Transition**: The moment their performance concludes (whether through automated track advancement or manual DJ completion), the special singer is marked inactive automatically.
- **Seamless Rotation Resumption**: The rotation queue resumes immediately with the displaced performer or next sequential singer as if the guest had never altered the rotation order.
- **Round Anchor Immunity**: Smart round-anchor heuristics (`EnsureRotationStartFlag` and `HandleSingerRetiredOrRemoved`) ignore special performers so round boundaries are always anchored to regular performers.

### ⏭️ Singer Skip (Round-Scoped Rotation Bypass) (`Shared`, `Lyracist`, `KSRotation`, `dj.html`, `billboard.html`, `kiosk.html`, `PatronPortal.html`)
- **Single-Round Bypass**: Allows the DJ to pass over a performer who needs to temporarily step away (e.g. stepping outside or grabbing a drink) without forfeiting their position in the rotation or dropping them to the back of the queue.
- **Distinct from Inactive & Pause**:
  - `IsInactive`: Marks the performer as inactive/deleted, moves them out of active rotation to the end of the line, and excludes them from active counts.
  - `IsPaused`: Retains queue position, but halts performance turns indefinitely until the DJ manually unpauses them.
  - `IsSkipped`: Retains queue position, passes over them for the current round only, and automatically clears when the round rolls over.
- **Automatic Round Rollover**: When rotation advances and the round completes (crossing or reaching the `⚓ Anchor` singer), all `IsSkipped` flags automatically clear so skipped performers sing normally in the following round without requiring manual DJ intervention.
- **Immediate Promotion When Skipping Current**: If the currently performing singer is skipped, the rotation immediately advances to the next eligible singer.
- **Synchronized Wait-Time Recalculation**: `RecalculateEstimatedWaits` ignores skipped performers so on-deck and waiting singers see true, accurate wait times.
- **Cross-Platform UI & Indicators**: Amber `⏭ SKIP` badges, row styling, and `Skip / Unskip` buttons across Lyracist, KSRotation desktop, remote DJ board (`dj.html`), audience billboard (`billboard.html`), kiosk (`kiosk.html`), and patron mobile portal (`PatronPortal.html`).

### 📍 Hybrid GPS & Wi-Fi Venue Auto-Location (`KSRotation`, `KSRotation.Maui`, `Lyracist`, `Shared`)
- **Hybrid Ground-Truth Detection**: Uses high-precision GPS coordinates (Latitude/Longitude) with a 150-meter spherical Haversine radius (`GeoMath`) as the primary ground truth, with house Wi-Fi SSID as a secondary fallback.
- **Travel Router Immunity**: Addresses the DJ mobile reality where portable travel routers are deployed at venues without Wi-Fi. SSIDs marked as travel routers in `KSRotation.Maui` or registered in `Settings/ksrotation_travel_routers.json` are excluded from Wi-Fi matching, preventing the system from falsely identifying new venues as old gig locations.
- **Tablet-to-Host GPS Bridge**: Devices with real satellite GPS sensors (`KSRotation.Maui` on Android tablets) automatically query hardware coordinates and push them to the host laptop via `POST /api/venue/location`. The desktop host caches these coordinates in `WindowsLocationService`, giving laptop hosts access to satellite precision even without onboard GPS chips.
- **Auto-Retrieval on Startup**: Automatically checks detected coordinates against the venue database (`Settings/ksrotation_venues.json` / `venues.json`) upon launching. If a known venue matches within 150m, it is automatically selected.
- **One-Click Venue Tagging & Auto-Save**: "📍 Tag GPS" buttons across `KSRotation`, `Lyracist`, and `KSRotation.Maui` allow hosts to capture and save the current location to any new or existing venue with one click.

### 📡 KSRotation.Maui Chromecast Web-Casting & TV Billboard Layout (`KSRotation.Maui`, `billboard.html`, `BillboardView.xaml`)
- **Direct Chromecast / Google TV Web-Casting**: Built-in DashCast web-receiver (`appId: 84912283`, `urn:x-cast:com.madmod.dashcast`) integration allows DJs running `KSRotation.Maui` on Android tablets, phones, or Windows laptops to cast the live audience billboard (`billboard.html`) directly to any Google Cast or Chromecast display on the local network.
- **Dedicated Full-Height Rotation Column**: Redesigned the audience billboard so the Upcoming Rotation queue occupies a dedicated, full-height right-hand column. Singer names, song titles, and queue numbers remain completely visible and legible across the room on 720p, 1080p, and 4K TVs.
- **Side-by-Side QR Code Arrangement**: Relocated the Song Requests and Wi-Fi Join QR code cards to the bottom-left area directly below Now Performing and Up Next, arranging them side-by-side with crisp contrast and generous scannable sizing.
- **Persistent DJ Name, Venue Name & Screen Listing Option**: Hosts running `KSRotation.Maui` can configure their DJ/Host Name, Venue Name, and choose whether to display them on the audience billboard and TV cast directly inside the "ⓘ About" dialog. Values are stored locally on the device (`Preferences` and `ksrotation_settings.json`), persisting across app reboots, session resets, and tablet restarts until explicitly changed.
- **Asynchronous Discovery & Android Multicast Lock**: Employs `ChromecastDiscoveryService` mDNS scanning with automatic Android `WifiManager.MulticastLock` handling, ensuring reliable device discovery and one-tap casting across all mobile Wi-Fi chipsets.

### 🖥️ 1080p Resolution Layout Optimization & Internal Search DataGrid Scrolling (`KaraokePage.xaml`)
- **Full HD (1920x1080) Workspace Alignment**: Streamlined page margins (`16,10,16,12`) and TitleBlock padding to prevent UI clipping and vertical scrolling on 1080p laptop displays and monitors running at 100% or 125% Windows DPI scaling.
- **Ultra-Compact Header Toolbar**: Reduced the top TitleBlock from ~102px down to a sleek ~40px toolbar with inline Venue & DJ branding and compact request indicator bulbs, recovering over 60px of vertical space for the song library, queue, and lyrics preview.
- **Interactive QR Code Thumbnail & 1-Click Popout**: Scaled the inline QR code to a clean 30x30 thumbnail with Join URL; clicking the QR thumbnail instantly launches the enlarged, high-resolution QR modal (`KioskQrCodePopoutWindow`) for easy patron scanning or tablet kiosk configuration.
- **Constrained 15–20 Row Search Results DataGrid with Adaptive Fallback**: Column 0 employs an adaptive `ScrollViewer` bound to `ActualHeight` (`MinHeight="500"`), preserving internal DataGrid scrolling, UI recycling virtualization, and 15–20 visible song rows on 1080p displays while providing smooth outer scroll fallback on constrained or non-maximized windows.
- **Permanently Anchored Singer Assignment Card**: Redesigned the Singer Assignment card into a compact two-column layout (Singer Name + Duet Partner, Key Slider + Notes), anchoring it directly beneath the search grid so DJs never have to scroll down to assign selected songs to performers.
- **Compact Playback FooterBar & Anti-Impingement Layout (`MainWindow.xaml`)**: Reduced the bottom playback bar height from ~94px down to ~52px (`Padding="20,6"`). Implemented a strict 3-column `Grid` (`MaxWidth="180"` on performer, `*` on song title, both with `TextTrimming="CharacterEllipsis"` and golden song highlight), placing the seek bar on row 2 with dedicated right-margin breathing room (`Margin="0,0,28,0"`) so text and progress controls never impinge on the center Play/Pause/Stop controls.

### 📱 Android Multi-RID Deployment Pipeline (`KSRotation.Maui.csproj`)
- **Self-Healing Multi-RID Packaging Target**: Integrated `EnsureOuterAssemblyForProcessAssemblies` before Android assembly processing and package signing. Automatically bridges RID-specific inner compiled assemblies (`android-arm64`, `android-x64`) into the outer intermediate directory (`obj\Debug\net10.0-android\`), preventing `XAPRAS7028` missing DLL errors during direct-to-device deployments.
- **Direct Hardware Deployment**: Guarantees seamless one-click Visual Studio and CLI deployment directly to connected tablets (e.g., Lenovo TB373FU).

### 📱 Audience Lyrics Screen QR Code Toggle (`SettingsPage.xaml`, `LyricsPage.xaml`, `LyricsWindow.xaml`)
- **Projection Screen Customization**: KJs can easily show or hide the patron mobile song request QR code badge displayed in the top-right corner of the lyrics projection window.
- **Triple Access Points**:
  - **Settings**: Checkbox under **Monitors & Screen Assignments -> Lyrics Projection Screen**.
  - **Lyrics Page**: Dedicated toggle inside the **Audience Lyrics Screen** configuration card.
  - **Live Right-Click Context Menu**: Right-click anywhere on the running lyrics projection screen and toggle **Show QR Code** on the fly without navigating away from the show.
- **Persistent State**: Synced and persisted automatically via `AppSettings.ShowQrCodeOnLyricsScreen`.

### 🎵 Karaoke & Music Library Separation & Search Isolation (`ScanningService`, `SearchService`, `LibraryService`, `KaraokePage.xaml`)
- **Companion CDG Detection**: `ScanningService.ParseStoreDownload` automatically detects accompanying `.cdg` graphics files (`Path.ChangeExtension(filePath, ".cdg")`), reliably classifying unzipped MP3+G / WAV+G tracks as `IsKaraoke = true, KaraokeType = "MP3G"` and preventing them from mistakenly being routed to the background music library.
- **Expanded Audio Format Enumeration**: The file crawler now catalogs standard music files in `.mp3`, `.mp4`, `.zip`, `.wav`, `.m4a`, `.flac`, `.wma`, `.aac`, and `.ogg` formats.
- **Dedicated Library Tabs & Live Match Badges**: The search results view clearly distinguishes **Karaoke Library** and **Music Library** tabs, each displaying dynamic match count badges (e.g. `Karaoke Library (12)` and `Music Library (3)`).
- **SQLite FTS Search Query Isolation**: SQLite FTS search queries now evaluate the `IsKaraoke` filter directly in SQL (`AND (@isKaraoke IS NULL OR s.IsKaraoke = @isKaraoke)`), preventing voluminous karaoke results from starving out music tracks under the 150-match query limit.

### 🎨 Search Result Track Type Tinting (`KaraokePage.xaml`, `HelpViewModel.cs`)
- **Instant Visual Distinction**: Search result rows feature subtle, semi-transparent row tinting to immediately distinguish track formats at a glance: emerald green (`#1522C55E`) for karaoke tracks (`IsKaraoke = true`) and warm gold (`#15F59E0B`) for standard background music tracks (`IsKaraoke = false`).
- **Help Topic Integration**: Topic 1 in the in-app help system documents search result row tinting alongside direct database scan and compact DJ toolbars.

### 🔘 Special Event Banner Mutual Exclusion, Two-Way Sync & Quiet Updates (`KaraokePage.xaml`, `KaraokeViewModel.DjBanner.cs`, `KSRotation/MainWindow.xaml`, `PatronRequestServer.cs`, `KSRotationSyncService.cs`)
- **Guaranteed Single Selection & Mutual Exclusion**: Added explicit `GroupName="SpecialEventBanner"` to the RadioButton template in Lyracist's KaraokePage and KSRotation, coupled with an active synchronization loop across SpecialEventOptions. Ensures only one special event banner can be selected at a time.
- **Instant 'None' Selection Reset**: Selecting the "None" radio button immediately clears all active banner selections and restores standard DJ branding banners seamlessly.
- **Two-Way Synchronization & Local Grace Window**: Added `POST /api/special-event/active` in `PatronRequestServer.cs` and wired `KSRotationSyncService.NotifyLocalSpecialEventChanged` to push DJ selections from Lyracist to KSRotation immediately, accompanied by a 5-second local selection grace window to prevent polling reversion races.
- **Quiet Sync & Modal Suppression**: Background synchronization utilizes `SetSelectedQuietly` to update radio button UI states without invoking user-action callbacks, preventing automated `ShowPersonalizedBirthdayPrompt` modal dialog popups on unattended host laptops.
- **Dynamic Custom Event Synchronization**: Custom event banners created in either app dynamically appear and select in the other rather than deselecting all radio buttons. All event sync guards enforce case-insensitive comparison (`OrdinalIgnoreCase`).
- **Elimination of Redundant Re-Sync Passes**: Streamlined event setters by relying cleanly on `OnActiveSpecialEventChanged` partial dispatches, eliminating double sync iterations and secondary banner updates.
- **Direct Production Method Verification (`SpecialEventBannerSelectionTests.cs`)**: Unit tests verify production synchronization routines (`SyncSpecialEventOptionSelections`, `UpdateActiveSpecialEventFromSync`) against live `KaraokeViewModel` instances, ensuring dynamic custom event addition and quiet modal-free updates execute correctly.

### 🔄 Rotation Invariants & Shared Logic Consolidation (`Shared/RotationHelpers`, `KSRotation`, `Lyracist`)
- **Unified Song & Singer Matching (`Shared/RotationHelpers.cs`)**: Consolidated `NormalizeForComparison`, `IsSameSingerName`, `IsSameSong`, and `IsSameSongLenient` into shared routines across both `Lyracist` and `KSRotation`, eliminating cross-application logic divergence.
- **Thread-Safe Collection Reads (`ReadWithConcurrentRetry`)**: Replaces hazardous background-thread UI dispatcher calls with lockless, non-deadlocking snapshot retries, preventing `InvalidOperationException` collection enumeration races between web request threads and the UI.
- **Linked Singer Adjacency & Active Boundary Preservation**: Restored `EnforceLinkedAdjacency` post-invocations on singer pause/promote flows so duet partners remain strictly adjacent, and added active/inactive boundary guarding to remote singer moves (`MoveSingerUp`, `MoveSingerDown`).
- **Duplicate Singer Auto-Merge Safety**: Hardened singer merging to properly execute full singer cleanup (reassigning current performer and rotation anchor) rather than raw collection removal.
- **Web Remote & Network Performance**: Cached WiFi SSID queries (30s) to eliminate recurring 1-second `netsh` stalls on patron polling, released singer-login locks before 429 rate-limit responses, and added dynamic byte inspection for patron avatar uploads.

### 🛡️ SQLite Migration Lock Self-Healing (`LyracistDbContext`, `App.xaml.cs`)
- **Proactive Lock Table Cleanup**: Automatically drops abandoned `__EFMigrationsLock` tables on application startup via `LyracistDbContext.ClearAbandonedMigrationLocks()`, preventing SQLite Error 11 (`malformed database schema (__EFMigrationsLock) - table already exists`) caused by interrupted previous sessions or sudden process termination.
- **Automated Migration Recovery & Setup Parity**: Startup automatically recovers from migration lock exceptions, ensuring post-migration setup (`ApplyPostMigrationSetup`: song deduplication, index rebuilds, and `SingerHistory` table creation) executes reliably on both normal and retry branches.

### 🎤 Karaoke Song Search & Compact Auto-Advance Toolbar (`KaraokePage`)
- **Direct Database Search & Scan**: Clicking the "Scan" button right beside the search input or pressing <kbd>Enter</kbd> immediately executes a database query (`SearchDatabaseCommand`) against the local SQLite library for matching songs and artists, eliminating unintentional Windows File Explorer prompts.
- **Dedicated Disk Folder Scan Button**: An independent folder icon button (`ScanFolderCommand`) provides direct access to disk folder scanning with clear tooltips.
- **Ultra-Compact DJ Control Toolbar**: Streamlined Auto-Advance & DJ control strip compressed from ~80px down to a sleek ~32px horizontal toolbar, recovering over 50px of vertical workspace for the song library, singer queue, and player columns.
- **Streamlined DJ Flow Actions**: Compact "▶ Start Song" and "⏭ Skip Singer" buttons provide rapid queue advancement with minimal screen footprint.

### 🛍️ Dedicated "Store" Tab & Licensed Track Import Pipeline (`Lyracist` ONLY)
- **Store Tab Placement**:
  - Exclusively available in the Lyracist desktop application navigation bar. This tab is strictly isolated and does not appear in any other apps (KSRotation, Lyracist Trivia, Knockout Trivia, etc.).
- **Store Notifications & Toast Alerts (`StoreNotificationService`, `ToastNotificationControl`)**:
  - **Non-Intrusive Toast System**: Automatically displays animated slide-in toast notifications in a dedicated bottom-right overlay container inside the Store tab and Bulk Import Wizard without blocking UI interaction or scrolling.
  - **Real-Time Lifecycle Alerts**: Triggers toasts for Track Imported (`Imported: Artist - Title (Provider)`), Audio Normalized (`Audio normalized: Artist - Title`), Silence Trimmed (`Silence trimmed: Artist - Title`), Waveform Generated (`Waveform ready: Artist - Title`), Store Sync Completed, and Bulk Import Completed.
  - **Dynamic Badge Bar**: Displays format tags (`MP3+G`, `MP4`, `ZIP`) and audio pipeline execution badges (`NORM`, `TRIM`, `WAVE`).
  - **Smart Dismissal**: Automatically fades and slides out after 5 seconds or dismisses instantly upon clicking the close button. Maximum 5 toasts stacked vertically.
- **Store Plugin API & Modular Providers (`IStoreProvider`, `BaseStoreProvider`, `ProviderRegistry`)**:
  - **Modular Plugin Architecture**: Each commercial karaoke provider (Karaoke Version, Party Tyme, Sunfly, Karaoke.com) is modeled as a pluggable module implementing `IStoreProvider` and inheriting `BaseStoreProvider`. Future commercial vendors can be added by implementing the interface without modifying core ingestion pipelines.
  - **Multi-Vector Detection Fingerprinting**: Centralizes search URL building, filename regex heuristics, ZIP internal hierarchy inspection, CDG magic binary header matching (KV `0x01 0x0F`, PT `0x02 0x0A`, SF `0x03 0x0C`), ID3 audio tag scans (`TXXX:KV`, `TXXX:PT`, `TXXX:SF`, `TXXX:KCOM`), and MP4 container metadata watermarking.
  - **Central Provider Registry**: Manages registered providers with dynamic registration capabilities (`RegisterProvider`) and thread-safe cross-vector detection lookups (`DetectProviderFromFilename`, `DetectProviderFromZip`, `DetectProviderFromCdg`, `DetectProviderFromId3`, `DetectProviderFromMp4`).
  - **Dynamic ViewModel Actions**: Powers Store search actions, URL routing, and dynamic button generation through `ProviderRegistry`.
- **Provider Settings Panel (`ProviderSettingsControl`, `ProviderSettingsViewModel`)**:
  - **Centralized Host Preferences**: Collapsible configuration panel embedded in Section 3 of the Store tab, empowering KJs to configure preferred commercial providers (KV, PT, Sunfly, Karaoke.com), file packaging types (MP3+G, MP4, Audio-only), target folder categories (Karaoke or Music), and companion lyrics format priority (LRC vs. TXT).
  - **Default Search & Button Highlighting**: Automatically highlights the host's preferred provider button with a gold "★ PREFERRED" badge in the search toolbar, and routes Enter key queries directly to the preferred provider.
  - **Baseline Processing Defaults**: Configures global default toggles for EBU R128 loudness normalization (-16 LUFS), silence trimming (-50dB), and peak waveform rendering across the Store watcher and Bulk Import Wizard.
  - **Smart Import & Sync Hints**: Provides fallback provider attribution for unbranded files during Store Sync and Bulk Import operations, ensuring canonical renaming rules (`Artist - Title (Provider).ext`) apply seamlessly.
- **Track Preview Player (`TrackPreviewControl`, `TrackPreviewViewModel`)**:
  - **Acoustic & Video Inspection Engine**: Built directly into the Recent Imports panel on the Store tab and the candidate review grid in the Bulk Import Wizard, allowing KJs to audition and inspect tracks before or after import without initiating full karaoke sessions.
  - **5-Second Rapid Audio Preview**: Powered by FFmpeg stream extraction (`-ss 0 -t 5`) with built-in WPF `MediaPlayer` playback, millisecond-accurate elapsed tracking, and interactive scrub bar.
  - **Dual-Audio Channel Auditioning**: Automatically detects multi-stream dual-audio tracks (e.g. guide vocals vs. backing instrumental) and provides an interactive toggle between Channel A (Guide Vocals) and Channel B (Instrumental), dynamically re-extracting audio per channel (`pan=mono|c0=c0` vs `pan=mono|c0=c1`).
  - **Waveform & Spectrogram Visualizations**: Generates high-contrast PNG waveform peaks (`showwavespic`) and acoustic frequency spectrograms (`showspectrum=s=800x300:color=intensity`) for visual inspection of dynamics, clipping, and frequency balance.
  - **MP4 Video Preview**: For MPEG-4 karaoke files, extracts the opening 5-second video clip and plays it smoothly inside an embedded WPF `MediaElement` with hardware acceleration.
  - **Technical Metadata Dashboard**: Displays filename, provider badge, container format, track duration, musical key, BPM, fidelity quality rating, performance difficulty, and vocal presence classification.
  - **Trigger Points**: Automatically displays when selecting an item in Recent Imports, clicking the dedicated "Preview" button, or selecting a candidate in the Bulk Import Wizard.
- **1-Click Store Sync (`PurchasedTrackSyncService`, `StoreSyncSummaryWindow`)**:
  - **Toolbar Primary Action**: "Sync Purchased Tracks" button triggers an immediate on-demand scan of the configured purchase directory or Downloads folder.
  - **Multi-Format & Companion File Pairing**: Scans for ZIP (MP3+G), MP3+G pairs, MP4 video, and lyrics (.lrc/.txt) while filtering incomplete browser download temp files (`.crdownload`, `.part`, `.tmp`, `~$*`).
  - **Catalog Verification & Deduplication**: Checks SQLite `Songs` database table to skip previously imported tracks and prevent duplicate reprocessing.
  - **Pipeline Execution**: Automatically invokes Provider Intelligence, Smart Import auto-renaming, FFmpeg loudness normalization/silence trimming, waveform generation, and database ingestion.
  - **Modal Summary Dialog**: Elegant dark acrylic summary dialog displaying total scanned, imported, skipped, errors, providers involved, and average processing time per track.
  - **Auto-Refresh Integration**: Seamlessly refreshes Store Analytics metrics and triggers library update notifications upon completion.
- **Bulk Import Wizard (`BulkImportWindow`, `BulkImportViewModel`, `PurchasedTrackBulkImporter`)**:
  - **Batch Folder Ingestion**: Select any folder containing downloaded karaoke files (MP3, CDG, ZIP MP3+G, MP4, LRC/TXT).
  - **Automatic Media Pairing & Lyrics Linking**: Automatically pairs `.mp3` and `.cdg` companion files into unified candidate entries and binds companion lyrics files without creating duplicates.
  - **Provider Intelligence Preview Grid**: Live candidate inspection displaying detected provider origins (Karaoke Version, Party Tyme, Sunfly, Karaoke.com, Local), technical audio/video specs, duration, musical key, BPM, quality tier, vocal difficulty, and vocal presence.
  - **Global & Granular Audio Processing Toggles**: Global batch toggles for EBU R128 loudness normalization, silence trimming, waveform peak rendering, and target folder routing, with individual per-track override toggles.
  - **Throttled Multi-Threaded Execution**: Parallel FFmpeg batch execution throttled via `SemaphoreSlim(3)` to a maximum of 3 concurrent worker processes to guarantee UI responsiveness and avoid disk thrashing.
  - **Combined Audio Pipeline Pass**: Chains silence trimming and volume normalization into a single FFmpeg pass when both options are enabled for 2x faster batch processing.
  - **Bulk Import Completion Summary**: Comprehensive summary modal reporting total tracks imported, skipped, errors, providers involved, and average processing time per track.
- **Smart Import Rules (Auto-Rename, Tagging, Classification & Enrichment)**:
  - **Auto-Rename Imported Files**: Renames incoming tracks and all companion files (.cdg, .lrc, .txt, waveforms) into canonical naming format: `Artist - Title (Provider).ext` (e.g. `Adele - Hello (KV).mp3`, `Bon Jovi - Wanted Dead or Alive (PT).cdg`, `Queen - Don't Stop Me Now (SF).mp4`). Standardizes publisher abbreviations: Karaoke Version $\rightarrow$ `KV`, Party Tyme $\rightarrow$ `PT`, Sunfly $\rightarrow$ `SF`, Karaoke.com $\rightarrow$ `KCOM`.
  - **Auto-Tag Genres**: Detects and maps provider-specific genres (Pop, Rock, Country, Soul, R&B, Jazz, Hip-Hop, Gospel, Dance, Standards) using FFprobe tags and catalog heuristics.
  - **Auto-Tag Difficulty**: Classifies tracks into `Easy`, `Medium`, and `Hard` based on tempo (BPM), duration, dynamic range, and vocal range/stamina indicators.
  - **Auto-Tag Musical Key**: Identifies and normalizes key signatures (e.g. `Am`, `C#m`, `G`) from ID3 frames (`TKEY`, `initialkey`) and metadata tags.
  - **Auto-Tag BPM**: Detects track tempo and parses numeric BPM values from ID3 tags (`TBPM`, `bpm`, `tempo`) and comment notations.
  - **Auto-Tag Vocal Presence**: Classifies tracks into `guide vocals`, `background vocals`, or `no vocals` using multi-stream container inspection and tags.
  - **Auto-Tag Quality**: Evaluates bitrates, sample rates, channels, codecs, and video resolutions into `High`, `Medium`, and `Low` tiers.
  - **Extended Schema**: Stores smart attributes in SQLite `Songs` database table, `PurchasedTrackItem`, and FTS5 search index tags.
- **Multi-Provider Licensed Store Deep Links**:
  - Direct search buttons for **Karaoke Version** (`https://www.karaoke-version.com/custombackingtrack/search.html?query={query}`), **Party Tyme** (`https://www.partytyme.net/songshop/cat/search.php?search_what=all&search_keyword={query}&submit=GO`), **Karaoke.com** (`https://karaoke.com/search?type=product&q={query}`), and **Sunfly Karaoke** (`https://www.sunflykaraoke.com/?s={query}&post_type=product`).
  - Opens store catalogs directly in the user's default browser, adhering strictly to zero-scraping and zero-audio-proxying architecture.
- **Provider Intelligence ("Advanced Fingerprinting")**:
  - **ZIP Internal Signatures**: Automatically scans archive table of contents for publisher patterns: `/custom_backing_track/` and `track.mp3`+`track.cdg` pairs (Karaoke Version), `/karaoke/` folders and `_PT` suffixes (Party Tyme), `SF` catalog codes (Sunfly), and `KCOM` descriptors (Karaoke.com).
  - **CDG Magic Header Detection**: Inspects initial 24 bytes of CDG streams for publisher magic bytes (`0x01 0x0F` for KV, `0x02 0x0A` for PT, `0x03 0x0C` for SF).
  - **MP3 ID3 Tag Fingerprinting**: Inspects `TXXX:KV`, `TXXX:PT`, `TXXX:SF`, and `TXXX:KCOM` frames via FFprobe.
  - **MP4 Container Watermark Detection**: Probes container title, artist, and comment metadata for publisher watermarks.
  - **Referrer Hints**: In-memory tracking of the last store provider clicked from the search bar as an intelligent fallback.
- **Purchased Tracks Folder Watcher (`PurchasedTrackWatcherService`)**:
  - Background folder watcher on the user's download directory (defaulting to the user's Downloads folder).
  - Automatically captures new `.mp3`, `.cdg`, `.zip`, and `.mp4` downloads, debounces write events, and verifies file lock completion before importing.
  - Asynchronously reconciles separately arriving `.mp3` and `.cdg` files into unified MP3+G entries.
- **FFmpeg-Powered Audio & Video Processing Pipeline**:
  - **Loudness Normalization**: EBU R128 (-16 LUFS) broadcast audio normalization via `loudnorm`.
  - **Silence Trimming**: Automatic removal of leading and trailing dead air below -50dB via `silenceremove`.
  - **Waveform Previews**: Automated high-contrast PNG visual waveform previews via `showwavespic`.
  - **MP4 Dual-Audio Detection**: Inspects container streams via FFprobe and tags multi-channel/dual-stream tracks (guide vocal vs. instrumental backing).
  - **Automated Lyric Pairing**: Automatically discovers matching `.lrc` and `.txt` files in the download folder and associates synchronized lyrics.
- **Target Folder Routing**:
  - Allows designating separate destination directories for **Karaoke tracks** (`.zip`, `.cdg`+`.mp3`, `.mp4`) and **Music / Audio tracks** (standalone `.mp3`).
  - Automatically moves files into their target folders upon download.
- **Library & FTS5 Database Integration**:
  - Parses titles, artists, and formats; tags provider source as `"Karaoke Version"`, `"Party Tyme"`, `"Karaoke.com"`, `"Sunfly"`, or `"Local"`; saves to `LyracistDbContext`; updates the SQLite FTS5 index; and refreshes library search in real time.
- **Manual Import & Activity Tracker**:
  - "Import Purchased Track..." button opens a multi-file picker to ingest downloaded tracks on demand.
  - Live activity table logs recent imports with technical badges: Format, Lyric file indicator, Dual-Audio streams, Normalization status, and Waveform preview indicator.
  - Collapsible **Import Activity Log** showing the last 20 file processing events with status codes and full event details.
- **Store Analytics & Library Insights**:
  - Collapsible **Store Analytics** dashboard displaying provider breakdown (KV, PT, SF, KC, Local), file format distribution (MP3+G, MP4, ZIPCDG, Audio-only, Lyrics), FFmpeg audio benchmarks (normalization, silence trimming, waveforms, average/min/max processing times), quality and difficulty distributions, top musical keys bar chart, BPM tempo histogram, and 14-day timeline & 24-hour activity heatmaps.
- **Web Reference Modules**:
  - Reusable React/TypeScript components (`storeTab.tsx`, `storeSearch.tsx`, `settingsStore.tsx`) and Node.js watcher (`purchasedWatcher.ts`, `importMetadata.ts`, `deepLink.ts`, `ffmpegUtils.ts`, `types.ts`) included under `Lyracist/WebModules/Store`.

### 👤 Dedicated "Users" Management Tab & Account Merging (`Lyracist` & `KSRotation`)
- **Performer Directory & Account Browser**:
  - Dedicated primary "Users" navigation tab in both Lyracist (sidebar) and KSRotation (top navigation bar between Settings and Trivia).
  - 2-pane master-detail interface featuring a searchable list of registered singers, search filter by name, email, or custom title, level badges, XP points, and total song performance tallies.
  - Account lifecycle actions to instantly create new singer accounts or delete obsolete accounts.
- **Performer Profile & Credentials Editor**:
  - Edit Singer Name, 4-digit Patron Portal PIN code, Email, Vocal Range (Soprano, Mezzo-Soprano, Contralto, Countertenor, Tenor, Baritone, Bass), Custom Title / Stage Nickname, Experience Score (XP), and DJ Notes.
  - Synchronized in real-time with the unified SQLite database (`Data/lyracist.db`) shared across both applications.
- **Per-Singer Vocal & Audio Defaults**:
  - Dedicated tab for audio preferences: Microphone Gain (Volume), Key Transposition (-12 to +12 semitones), Playback Speed (0.8x to 1.2x), 3-Band Parametric EQ (Treble, Mid, Bass), Dynamics (Compressor, Limiter), and Sound Check Remarks.
  - Settings automatically preload whenever the performer is queued or takes the stage.
- **Comprehensive Performance History Record**:
  - "Performance History" tab displaying a complete historical record of every song sung by the performer.
  - Columns for Song Title, Artist, Key Transposition, Playback Speed (Tempo), Source (Local, YouTube, etc.), and Date/Time stamp.
  - Direct Actions: "Queue" button instantly re-queues the past performance back into the live rotation preserving the performer's exact key and tempo settings; "Remove" button purges individual entries.
- **Merge Duplicate Singer Accounts**:
  - Dedicated "Merge Duplicate..." consolidation modal tool.
  - Allows the DJ to select a secondary/duplicate singer account to merge into the primary account.
  - Consolidates performance history, past requests, active rotation entries, audio profile defaults, and XP points into the primary account, then permanently deletes the duplicate account.

### 📸 Performer Webcam Capture, Selfie Uploads & Rotation Banner Displays (`Lyracist` & `KSRotation`)
- **Live DJ Webcam Photo Capture**:
  - Dedicated "Take Photo" button on the "Users" tab in both Lyracist and KSRotation allowing DJs to snap a headshot of the performer on the spot using any connected USB or integrated webcam.
  - Powered by `FlashCap` with zero native dependency overhead.
  - Includes real-time camera selection, live video preview with circular headshot framing guide, snapshot freeze/review, retake option, and automatic center-square cropping to 400x400 JPEG saved to `Data/Avatars/{guid}.jpg`.
- **Patron Portal Selfie Uploads**:
  - Patrons can upload a selfie or profile image directly through the smartphone patron portal (`/api/singer/avatar/upload`).
  - Safe validation and storage in `Data/Avatars/` with database record linking.
  - DJs can view, upload, take webcam photos, or clear photos in the desktop "Users" tab.
- **Vegas Billboard ("Vegas Marquee") Display**:
  - On the Broadway/Vegas marquee rotation screen, a high-resolution circular framed photo with gold neon glow effect is prominently rendered right above the singer's name on the big screen.
- **Vinyl Record ("Now Spinning") Turntable Display**:
  - On the turntable rotation screen, the circular framed photo is showcased beside the "♪ NOW SPINNING" banner and singer title while the vinyl record spins.

### 🎨 DJ Banners & Special Event Display System (`Lyracist` & `KSRotation`)
- **Independent DJ Banner Multi-Monitor Projection**:
  - Projects customized DJ promotional graphics and looping MP4 video animations (`Banners/DJBanners`) to any dedicated secondary monitor, stage TV, or patron display screen.
  - Supports automatic looping for animated video banners with soundless background playback (`BannerVideo_MediaEnded` rewind loop).
- **Live In-App Visual Previews**:
  - Live preview cards embedded directly under the DJ Banner selector in both the **Display Settings** tab (`SettingsPage.xaml`) and **Karaoke Control Center** (`KaraokePage.xaml`).
  - Displays instant scaled visual previews for image files (`.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp`), dedicated animated video badges for `.mp4` video files, and fallback indicators when no banner is selected.
- **Intelligent Same-Screen Deconfliction & Seamless Auto-Toggle**:
  - When the DJ Banner and Singer Rotation Billboard share the same physical display output, toggling the DJ Banner ON automatically steps down Rotation, immediately projecting the selected banner to the screen without manual multi-step configuration.
- **Special Event Overrides & Safe Precedence**:
  - Supports quick-switch special event overlays ("Birthday", "Last Song", "Anniversary", "Custom") with instant revert back to the chosen DJ banner when switched to "None".
  - Selecting any standard DJ banner automatically clears prior special event states, ensuring the DJ's selection takes immediate effect on stage displays.

### 🎤 Per-Singer Key/Tempo Recall & Mic Level/EQ Recall (`Lyracist`)
- **Dual-Tier Key & Playback Speed Memory**:
  - **Per-Song Performance Memory**: Whenever a performer sings a song, their exact key transposition semitone offset and playback tempo multiplier (e.g. 1.1x) are saved in the SQLite `SingerHistory` database table. Re-queuing that song in the future automatically recalls the performer's exact key and tempo.
  - **Performer Profile Defaults**: Global performer vocal preferences stored in `SingerAudioSettings` act as the initial baseline when a performer sings a track for the first time without prior history.
  - **Live Pitch & Speed Synchronization**: Recalled key and tempo are passed into `ActivePerformerKey` and `ActivePerformerTempo` on `MediaEngine`, instantly setting the Live Deck pitch and speed sliders as soon as the track begins.
- **Per-Singer Mic Level & Vocal EQ Recall**:
  - Automatically loads the performer's saved vocal gain (`Volume`), 3-band parametric EQ (`Treble`, `Mid`, `Bass`), dynamic compressor threshold, and peak limiter from `SingerAudioSettings` when their turn begins.
  - On-screen Audio Controls sliders update live to match the active performer's profile.
- **Quick-Action Audio Controls [Save to Singer] & [Recall Singer] Buttons**:
  - Located on the Audio Controls card header next to "Reset Audio":
    * **Save to Singer**: Instantly saves current Audio Controls slider values (Volume as Vocal Gain, Treble, Mid, Bass, Compressor, Limiter) as the active performer's defaults in `SingerAudioSettings`, and records active Key and Tempo into `SingerHistory`.
    * **Recall Singer**: Reloads stored profile audio defaults on demand, resetting the DSP chain and slider controls if adjustments were made during the performance.
- **Singer History DataGrid Tracking**:
  - Dedicated "Key" and "Speed" columns in the Singer History tab (`KaraokePage.xaml`) display historical transposition and tempo settings for every completed song.
  - Re-adding songs from Singer History back into the rotation queue retains historical key and tempo settings automatically.

### 🚀 High-Output WASAPI Audio Engine, USB Mixer Output Boost & Tablet Touch Layout (`Lyracist`)
- **Native Windows WASAPI mmdevice Audio Pipeline**:
  - `LibVlcVideoBackend` and `BackgroundMusicPlayer` now route all audio output directly through Windows Core Audio WASAPI (`mmdevice`) for both default and custom audio output devices, completely bypassing legacy DirectSound/WaveOut software mixer attenuation.
  - Expanded software volume headroom up to **200%** (+6 dB clean digital gain boost), ensuring playback loudness matches native media player levels without cranking volume controls to maximum.
  - Recalibrated default integrated loudness normalization to -12 LUFS (matching commercial live performance/DJ standards), delivering punchy, loud audio output.
- **Master Output Boost / Preamp (USB Mixer Mode)**:
  - Adjustable digital preamp slider from 0 dB to +12 dB with dedicated quick presets for `0 dB (Standard)`, `+6 dB (USB Mixer)`, and `+12 dB (Maximum)`.
  - Specifically designed for external sound cards and USB audio interfaces like the **Yamaha MG10XU** (Channel 9/10 USB stereo return), supplying a robust, punchy +4 dBu professional line-level signal directly from Windows without having to crank mixer channel gain knobs to their physical limits.
  - Master preamp boost settings automatically apply across all playback channels (Karaoke performances, Opening Music, Fill-in Music, and End-of-Rotation Music) ensuring seamless, matched volume levels throughout the entire show.
- **Anti-Clipping Peak Limiter**:
  - Embedded a transparent soft-knee peak compressor/limiter into LibVLC's playback pipeline (`--audio-filter=compressor`), protecting against digital clipping, distortion, or harsh speaker spikes when high-gain tracks are played.
- **Instant Search & Query Isolation**:
  - Batched collection updates replace individual item additions, updating search tables instantaneously without visual stuttering or UI thread thrashing.
  - External streaming queries (Spotify, YouTube, Amazon) are isolated exclusively to the "Streaming Links" tab, ensuring local library song lookup is immediate and free of background network overhead.
  - Dedicated one-click clear button (`✕`) integrated directly inside the search box for rapid query resets.
  - Fully enabled UI recycling virtualization on all search DataGrids, ensuring smooth scrolling across thousands of tracks.
- **Responsive Tablet Touch Sizing & Column Protection**:
  - Locked action buttons in search results (Add to Rotation, Singer History, and Singer Queue) to dedicated 52px non-collapsing columns with generous 38x32px touch targets.
  - On 1920x1080 tablets operating at 150% or 175% Windows DPI scaling, action buttons maintain their full proportions and never compress into thin vertical lines.
  - Enforced a 360px minimum width on the search panel to prevent column squishing on compact screens.

### 🛑 Last Round Rotation Management (`Lyracist`, `KSRotation`, `KSRotation.Maui`, `dj.html`)
- **One-Click Host & Remote Activation**: Standout emerald green "Last Round" button positioned directly after the Clear button across host interfaces (`RotationPage.xaml`, `MainWindow.xaml`, `MainPage.xaml`) and on the Remote DJ Board (`dj.html`), immediately synchronizing across the rotation lifecycle, embedded servers, and display services.
- **Audience Screen Announcement**: Bold crimson banner (`★ THE LAST ROUND FOR THE NIGHT IS CURRENTLY UNDERWAY ★`) displayed prominently on the Lyracist `RotationWindow`, KSRotation `SingerDisplayWindow` (across all display projection styles), KSRotation.Maui `BillboardView` (on-device attractor and external HDMI/Presentation displays), Remote DJ Board (`dj.html`), and the real-time web billboard (`billboard.html`).
- **Dynamic Queue Pruning**: Once a performer has sung in the last round, they are automatically hidden from the audience rotation queue (now singing, next, on-deck), while remaining visible on host and remote DJ screens with a distinct `DONE (LAST ROUND)` indicator badge.

### 🔗 Linked Singers (`Lyracist`, `KSRotation`, `KSRotation.Maui`)
- **Two-Click DJ Linking**: Click the 🔗 button on one singer, then on another, to link them - no other singer can ever be inserted between them (a new signup, a drag/drop reorder, or a move up/down all respect the pairing). Clicking 🔗 on an already-linked singer unlinks it; linking a singer that's already linked elsewhere breaks the old link first. **The link stays in effect all night until the DJ unlinks it** - there's no auto-unlink.
- **Perform Back-to-Back, Then Reunite for Next Time**: When the current half of a pair finishes and floats to the bottom (with "Float Current Singer to Top" on) while the other is promoted to sing next, they're *expected* to separate for that moment - adjacency enforcement steps aside while either half is the current singer, so the finished singer isn't dragged back up mid-handoff. Once neither half is current anymore (both have had their turn), they're automatically pulled back together for their next joint turn - the link never needs to be re-applied.
- **Pause-Safe, Not Inactive-Safe**: A linked singer can be Paused without losing its place or its partner - it's simply skipped over, same as any paused singer. Marking one half Inactive ("out for the night") is different: the still-active partner isn't forced to follow it into the retired section.
- **Shared Enforcement**: `RotationHelpers.LinkSingers()`/`UnlinkSinger()`/`EnforceLinkedAdjacency()` (`Shared/RotationHelpers.cs`) drive all three apps identically. Shows as a "🔗 [partner]" badge in Lyracist's `RotationPage`, KSRotation's `MainWindow` (list + right-click menu), and KSRotation.Maui's `MainPage`.

### ⏱ Estimated Wait Time on Rotation Screens (`Lyracist`, `KSRotation`, `KSRotation.Maui`)
- **Per-Singer Wait Badge**: Every waiting singer's name on the audience-facing rotation displays shows an estimated wait in minutes, e.g. `Dennis {5} Only Make Believe` - the cumulative estimated performance length of everyone ahead of them, using each song's actual known duration + 30 seconds where available, or a DJ-configurable default-length fallback otherwise.
- **DJ Can Turn It Off Entirely**: A "Show Estimated Wait Time on Rotation Screens" toggle (Lyracist's Settings page, Display tab; KSRotation's Settings tab) hides the badge everywhere at the DJ's discretion - some DJs don't want the audience seeing wait estimates at all. On by default; takes effect immediately.
- **`{N} = Estimated Wait Time` Legend on Every Display**: So patrons aren't left guessing what the number in braces means, a small explainer appears alongside the badge on every display it can show on - Lyracist's Normal List and Star Wars Crawl, KSRotation's Normal List, Star Wars Crawl, Vegas/Broadway Marquee, and Vinyl Record views, and KSRotation.Maui's Billboard view. On the Marquee/Vinyl/Normal-List views the section label itself ("UP NEXT"/"ON DECK") always stays visible - only the explainer suffix hides with the toggle. Patron-facing web pages (kiosk, DJ remote, billboard, patron portal, mobile tablet) don't currently show the badge at all, so there's nothing to gate there yet.
- **Configurable Default Song Length**: A "Default Song Length" setting (same Settings screens) controls the fallback used when a song's actual duration isn't known - defaults to 4.75 minutes (closer to a typical song's actual runtime than a flat 5), adjustable in quarter-minute steps from 2 to 8 minutes; grayed out while the toggle above is off. KSRotation.Maui shares both settings (no dedicated UI of its own yet) and falls back to the same defaults (shown, 4.75-minute).
- **Shared Calculation**: A single `RotationHelpers.RecalculateEstimatedWaits()` (`Shared/RotationHelpers.cs`) drives all three apps identically, recalculated automatically the moment a singer is added, marked current, reordered, paused/inactive-toggled, removed, or marked finished, so waiting singers' estimates never go stale.
- **Wired Into Every Display Mode**: Lyracist's `RotationWindow` (queue list, Star Wars Crawl, scrolling marquee), KSRotation's `SingerDisplayWindow` (Normal List, Marquee, Vinyl), and KSRotation.Maui's `BillboardView`.

### ⚡ High-Performance In-Memory Library Metadata Probing
- **Microsecond In-Memory Header Extraction**:
  - Embedded `TagLibSharp` in `Lyracist.Data` for direct in-memory audio/video duration and genre extraction (<0.1ms per track), eliminating external process spawning overhead (`ffprobe.exe`).
- **Direct ZIP-CDG Stream Extraction (`StreamFileAbstraction`)**:
  - Probes MP3 streams directly from inside `.zip` archives in RAM without decompressing temporary audio files to `%TEMP%` on disk.
- **Embedded ID3/MP4 Metadata Recovery**:
  - Automatically resolves track artists and titles from embedded ID3v2/MP4 tags when filename conventions omit the artist name.
- **Loop-Free Probed Tracking**:
  - Automatically populates `song.Genre` and marks `Tags = "none"` when tags are absent, ensuring subsequent library scans skip already-probed tracks immediately.
- **Parallel Scaled Concurrency**:
  - Scales background probing workers up to `Math.Max(8, Environment.ProcessorCount * 2)` (16–32 parallel workers) with batched SQLite transactions and FTS5 search index synchronization, reducing 50,000-song follow-up scan times from 3+ hours to under 1–2 minutes.

### 📁 Unified Solution Directory & Asset Consolidation
- **Centralized Application Taxonomy**:
  - `Settings/`: Consolidated application configuration folder with distinct per-app naming (`lyracist_settings.json`, `ksrotation_settings.json`, `knockout_trivia_settings.json`, `lyracist_trivia_settings.json`, `scaryoke_settings.json`, `dbeditor_settings.json`, `keygen_settings.json`, `ksrotation_venues.json`, `ksrotation_djs.json`, `VenueGraphics/`).
  - `Data/`: Centralized database and persistence folder (`lyracist.db`, `trivia.db`, `ksrotation_night_db.json`, `ksrotation_singers.json`, `wifi_passwords.json`, `keygen.db`).
  - `Banners/`: Centralized root banners repository organized into dedicated per-app subdirectories:
    * `Banners/`: shared by every app - `DJBanners/`, `EventBanners/` (including 16:9 standard event banners), `Announcements/`, `CategoryBanners/` (15 themed trivia category graphics) and `CustomBanners/`.
  - `Packs/`: Single shared packs repository containing 16 curated JSON trivia packs used interchangeably across all trivia applications.
  - `Logs/`: Centralized log directory with sanitized app-distinguished logs (`{app}_app_{date}.log` and `{app}_err_{date}.log`).
- **Transparent Legacy Migration**: All services across all applications automatically check for legacy configuration/data files in `%AppData%`, `%LocalAppData%`, or old subfolders on startup, transparently copying them forward to the consolidated directories without user intervention.

### 🥊 Knockout Trivia Standalone Game Module (`KnockoutTrivia`)
- **Fast-Paced Bar-Friendly Elimination Game**:
  - Standalone MVVM WPF desktop app targeting .NET 10 with modern Fluent UI dark styling (`WPF-UI`).
  - Compatible with the shared `Data/trivia.db` SQLite database and `Packs/*.json` trivia packs.
- **📱 Phone & Tablet Connect Screen (`ConnectView.xaml` / `ConnectViewModel.cs`)**:
  - **Dual QR Code Cards**:
    * **📶 Wi-Fi Connect QR**: Automatically generates a high-resolution dark purple QR code (`#4C1D95`, `WIFI:S:...`) for instant venue Wi-Fi connection.
    * **📱 Game Arena QR**: High-resolution dark green QR code (`#064E3B`) linking to `http://<local-ip>:<port>` for zero-install smartphone/tablet browser play.
  - **Live Connected Player Roster**: Shows connected mobile players in real-time with strike dots, shield tokens, and online indicators.
  - **Secondary Screen Projection**: Instant sync with `AudienceWindow` to display large QR codes on venue TVs and projectors.
- **🌐 Embedded Asynchronous Web Server & Mobile Companion App (`knockout.html`)**:
  - Built-in `TcpListener` server hosting REST endpoints (`/api/knockout/join`, `/api/knockout/submit`, `/api/knockout/state`) with GUID session token authentication and payload validation.
  - Mobile web app with touch buttons, haptics, Web Audio synthesized sounds, live strike counters (🟢🟢🟢 -> ❌ -> 💥), shield badges (🛡️), and spectator mode.
- **🧪 Testing Module & In-App DJ Bot Simulator (`KnockoutTrivia.Tests` / `SimulatorView.xaml`)**:
  - **Automated Test Project (`KnockoutTrivia.Tests`)**: xUnit v3 automated test suite for game state transitions, scoring, token defense, streaks, web server endpoints, and wheel/banner services.
  - **In-App DJ Bot Simulator Tab (`MainWindow.xaml`)**: Virtual bot player manager (spawns 1 to 30 bots), automated answer simulation with configurable accuracy and reaction times, auto-play round execution, instant Super Streak wheel rehearsal, and live diagnostics terminal.
- **⚡ Automatic Game Flow & Internal Scoring Engine**:
  - Automated question timer with real-time countdown.
  - Automatic answer evaluation upon timer expiry or DJ reveal (shield tokens absorb strikes; wrong answers without shields accumulate strikes).
  - Autonomous advance cycling in Automatic Mode (`GameAdvanceMode.Automatic`).
- **Horizontal Dynamic Scoreboard**:
  - Dynamic strike-tinted player status bars: Green (0 strikes), Yellow (1 strike), Orange (2 strikes), and dimmed Red (Knocked Out / 3 strikes).
  - **Shield Tokens**: Hold 0 to 3 shield tokens that absorb strikes when an incorrect answer is given.
  - **Streak Meters**: 5-block continuous streak progress meters that award a shield token on every multiple of 5 consecutive correct answers without wiping streak progression.
- **⚡ Super Streak Scaryoke-Style Target Wheel**:
  - Reaching the Super Streak threshold (default: 20 consecutive correct answers) triggers a rotary wheel containing all opponents who currently hold tokens.
- **🔀 Automatic Question Randomization & Inter-Category Shuffling**:
  - Uniform Fisher-Yates deck shuffling across all loaded SQLite databases and category packs on startup and game resets, ensuring categories are thoroughly mixed rather than presented in sequential category chunks. Includes on-demand DJ '🔀 Shuffle' control.
- **❓ Comprehensive DJ Help & Rules Guide**:
  - Built-in split-pane Help system covering game rules, elimination mechanics, shield token defense, 5-block streak meters, Super Streak wheels, live DJ adjudication, 16:9 banner projection, and configuration options.
- **3-Column DJ Settings & Multi-Database Selection ListBox**:
  - 3-column responsive layout preventing vertical scrolling:
    * **Column 1**: Game scoring, question count, auto/manual flow buffers, and audio/visual FX toggles.
    * **Column 2**: Shield caps, streak requirements, and Super Streak wheel settings.
    * **Column 3**: Interactive Database & Pack selection ListBox (with individual checkboxes, 'All' and 'Clear' buttons, and live question count badges) alongside Multi-Monitor & TV Routing controls.
- **🖥️ Multi-Monitor Routing & Dedicated Audience Projection Window (`AudienceWindow`)**:
  - Automatically enumerates all connected physical displays, laptops, secondary TVs, and venue projectors with hardware names and resolutions.
  - Independent monitor routing: DJ Host Screen assignment and Audience Big Screen assignment.
  - Dedicated borderless 16:9 auto-scaling **Audience Window** (`AudienceWindow.xaml`) that displays high-contrast questions, reveals, rotary wheels, and scoreboards to the crowd while keeping DJ controls (scoring, adjudication, settings, help) private to the host.
  - Titlebar quick-action **`📺 Audience Screen`** button for instant one-click projection toggle.
- **16:9 Auto-Scaling Presentation Banners**:
  - Dynamically generated 16:9 game-show screens (Intro, Intermission, Champion Winner, Elimination, Super Streak) with official Knockout Trivia branding (`kotrv_logo.png` / `kotrv_logo.webp`).
- **DJ Control Center & Multi-Monitor Support**:
  - Dual-monitor routing for DJ host console and audience screens.
  - Configurable points per answer, question limits, timers, token caps, super streak thresholds, and audio/visual toggles.

### 🔤 Intelligent Name & Artist Capitalization Engine (`All Applications`)
- **Centralized Smart Proper-Casing (`Shared/NameFormatting.cs`)**:
  - Automatically capitalizes singer names, duet partners, artists, and song titles cleanly across all applications (`Lyracist`, `KSRotation`, `KSRotation.Maui`, `LyracistDbEditor`, `ScaryokeWheel`).
- **Mixed-Case Preservation**:
  - Capitalizes the initial letter of every word while preserving user-typed internal or trailing uppercase letters (e.g. `DeaR` -> `DeaR`, `deaR` -> `DeaR`, `LeBron` -> `LeBron`, `vanBuren` -> `VanBuren`, `MacDonald` -> `MacDonald`).
  - Completely fixes the bug where previous lowercase transformations wiped out custom performer or band stylizations (e.g. turning `DeaR` into `Dear`).
- **Apostrophe Name Prefixes**:
  - Automatically detects single-letter surname prefixes with apostrophes (e.g. `O'`, `D'`, `L'`, `M'`) and capitalizes both the prefix and the root surname (e.g. `o'neal` / `O'neal` / `O'NEAL` -> `O'Neal`, `d'angelo` -> `D'Angelo`, `l'amour` -> `L'Amour`).
- **Scottish & Irish "Mc" Prefixes**:
  - Surnames starting with `Mc` automatically receive root-letter capitalization (e.g. `mcdonald` / `MCDONALD` -> `McDonald`, `mccartney` -> `McCartney`).
- **Hyphenated Compound Names**:
  - Capitalizes words separated by hyphens (e.g. `mary-ann smith` -> `Mary-Ann Smith`, `smith-o'neal` -> `Smith-O'Neal`).
- **Acronyms, Roman Numerals & Contractions**:
  - Intelligently preserves uppercase for standard audio acronyms (`DJ`, `MC`, `TV`, `CD`, `DVD`) and Roman numerals (`II`, `III`, `IV`, `VI`, `VII`, `VIII`, `IX`, `X`, `XI`, `XII`), while keeping song title contractions correctly lowercased (e.g. `Don't Stop Believin'`, `Rock 'N' Roll`).


### 📟 Cross-App Landscape Tablet Kiosk Request Station (`/kiosk`, `kiosk.html`)
- **Split-Screen Venue Kiosk UI Across All Apps (`KSRotation`, `KSRotation.Maui`, `Lyracist`)**:
  - Designed specifically for venue-mounted landscape tablets, giving patrons a self-service kiosk to search songs and join rotation without disturbing the DJ.
  - **Attractor / Welcome Screen**: Fullscreen ambient welcome screen when idle with glowing microphone hero, animated pulsing *"TOUCH SCREEN TO JOIN THE ROTATION"* call-to-action, live stage snapshot (*Now Singing*, *Up Next*, *Queue Count*), and 4 visual step-by-step instructions.
  - **Touch-to-Start & Auto-Return**: Touching anywhere on the screen immediately enters the request station. The station automatically returns to the Attractor screen after 45s of inactivity or upon completing a request submission.
  - **PWA Standalone & Fullscreen Mode**: Includes web app manifest meta tags and an interactive `⛶ Fullscreen` toggle button in the header bar for full screen presentation without browser address bars on iOS/iPadOS and Android tablets.
  - **Left Side (62% width)**: Performer details with auto-fill, request type toggle (Karaoke vs Background Track), instant Apple iTunes catalog search with debounced dropdown, manual song & artist entry, and pitch key adjustment selector ($-2, -1, 0, +1, +2$).
  - **Right Side (38% width)**: Real-time rotation queue with high-visibility **🎤 NOW SINGING** and **⏳ UP NEXT** spotlight cards at the top.
  - **Tap-to-Select Performer Name**: Tapping any singer row on the right automatically populates that name into the request form on the left, preventing spelling variations across rounds.
  - **Queue Slot Prediction**: Shows prospective singers where they will enter in the rotation (e.g. `➕ Spot #6 in rotation`) before submitting.
  - **Auto-Reset & Inactivity Protection**: 5-second post-submission confirmation countdown modal and a 45-second inactivity watchdog that clears abandoned forms.
- **Kiosk Setup & QR Popout in `KSRotation` & `Lyracist`**:
  - One-click `[ Kiosk ]` button on the desktop Patron Portal panel and Settings pages opens `KioskQrCodePopoutWindow.xaml` with high-contrast QR code, direct URL, and clipboard copy.
- **3-Way QR Portal Selector in `KSRotation.Maui`**:
  - `ConnectQrOverlay` provides quick one-tap switching between `📱 Patron`, `📟 Kiosk`, and `🎧 DJ` QR codes and URLs.

### ⚡ Safe, DJ-Friendly Auto-Advance System
- **Grace Period Timer & Fill-In Music**:
  - Automatically initiates a configurable grace period countdown (default 15s) when a song finishes.
  - Automatically spins up ducked fill-in background music between performers.
  - Broadcasts stage announcement: `"Next singer: {name} — please come to the stage"` on the rotation billboard.
- **Large High-Contrast DJ Control Buttons**:
  - **`▶ START SONG`** (`StartSongButton`): Stops grace timer and fill-in music, loads the song, updates projection and tablet lyric displays, and immediately begins playback.
  - **`⏭ SKIP SINGER`** (`SkipSingerButton`): Advances singer queue without scoring the skipped song, keeps fill-in music running, and restarts the grace period for the next performer.
- **State Machine Protection (`AutoAdvanceState`)**:
  - Guards against accidental double-starts via `StartingSong` transition lock.
  - Interlocks with Trivia and Scaryoke modes so auto-advance never triggers during mini-games.
  - Safely falls back to `WaitingForSongSelection` if the upcoming singer has not chosen a track yet.

### 📝 Global Auto-Selection & Fast Input (`All Applications`)
- **Global Select-All on Focus**:
  - Across every application in the suite (`KSRotation`, `Lyracist`, `Lyracist.Trivia`, `TriviaDbCreator`, `LyracistDbEditor`, `LyracistKeyGen`, `ScaryokeWheel`), clicking or tabbing into any `TextBox`, `PasswordBox`, or numeric input box automatically highlights and selects all existing text.
  - Game masters, KJs, and hosts can immediately begin typing new values without having to backspace, clear, or double-click to select existing text.
  - Subsequent clicks within an already-focused text box allow natural caret placement, character editing, and partial text selection without interference.
- **New Singer Addition Auto-Focus & Select-All**:
  - When adding a new performer to the rotation queue, the system automatically scrolls the list directly to the newly inserted row, focuses the singer name field, and highlights the default `"New Singer"` text so the host can instantly type the performer's actual name.

### 📱 Responsive Tablet Layout & Android Stability in `KSRotation.Maui`
- **Vertical Mode (Portrait Orientation)**:
  - When the tablet is held vertically, the layout automatically reflows to place the **Active Rotation Queue at the top** of the screen across full width, and positions the **Patron Request Portal (QR code) and Incoming Requests side-by-side at the bottom** of the screen.
  - This eliminates wasted screen real estate on portrait tablets, maximizing vertical space for the performer list.
- **Horizontal Mode (Landscape Orientation)**:
  - In landscape orientation, the layout retains the two-column view with Rotation on the left (`*`) and the Portal & Requests sidebar on the right (`280px`).
- **Layout Dispatch Safeguarding**:
  - Orientation layout updates are safely dispatched via `Dispatcher.Dispatch` with atomic collection assignment to guarantee smooth orientation transitions without re-entrant layout cycles.
- **Android Self-Contained Deployment**:
  - Embedded assembly packaging (`EmbedAssembliesIntoApk = true`) and platform OS guards ensure reliable, standalone deployment and launch on Android tablets without fast-deployment synchronization failures.

### 🎧 Remote DJ Web Board (`dj.html`)
- **First-Button Finished Checkmark (`✓`)**:
  - Reordered performer card action buttons so the finished song checkmark (`✓`) is positioned as the first button in the actions bar, matching the ergonomics and muscle memory of the desktop and MAUI consoles.
- **Modal Popup Performer Addition**:
  - Converted the static inline "Add Performer to Rotation" form into a dedicated action button and top rotation toolbar button that opens a focused modal overlay dialog (`#add-performer-modal`).
  - Automatically focuses and selects the singer name field upon opening, submits on `Enter`, and dismisses on `Escape` or backdrop click for frictionless singer additions from anywhere in the venue.

### 🛠️ Trivia Database Creator & Pack Studio (`TriviaDbCreator.exe`)
- **Visual Category & Question Authoring**: Standalone WPF MVVM desktop app using Fluent UI (`WPF-UI`) for creating, editing, and expanding trivia question databases.
- **Left Sidebar Pack Explorer**: Auto-discovers and navigates all JSON question packs in `Packs/` with search filtering and question count indicators.
- **Interactive Question Editor**: Real-time editor with colored option cards (▲ Purple, ◆ Cyan, ● Amber, ■ Rose), correct answer radio toggles, difficulty dropdown, and explanation notes.
- **1-Click Option Balancing**: Automatically shuffles option positions across all questions in a pack to guarantee an even ~25% distribution across choices A, B, C, and D.
- **16:9 Banner Studio**: Generates high-resolution 16:9 Category Announcement Banners (`Banners/CategoryBanners/{pack}.png`) with one click.
- **Direct SQLite Seeding**: 1-click database synchronization updating `Data/trivia.db`.

### 🎤 Singer Rotation & Queue Management (`Lyracist`, `KSRotation`, `KSRotation.Maui`)
- **High-DPI Singer Box & Action Button Sizing**:
  - Singer display boxes in `KSRotation` feature expanded vertical padding (`Padding="2,6,2,10"`, `MinHeight="80"`), clean 30px outlined buttons, and bottom margins, ensuring the bottom border of the `"✓ Finish"`, `"▲"`, and `"▼"` buttons is crisp and completely visible on 1080p laptop displays under any DPI scaling.
- **Smart End-of-Round Singer Insertion**:
  - Newly added singers are placed at the end of the *current active rotation round* (right before the 1st singer round anchor `IsRotationStart`), ensuring they perform in the current cycle before already-sung performers repeat in the next round.
- **Accurate Inactive Performer Accounting**:
  - `SingersInRotationCount` strictly excludes inactive singers (`IsInactive = true`) and background music tracks, reflecting the true number of performers waiting in rotation.
- **Float Current Singer to Top Option**:
  - Toggling `"Float Current to Top"` keeps the currently performing singer at index 0 (top of the rotation list).
  - When songs finish, the performer shifts to the end of the queue and the next active singer automatically floats to the top, eliminating scrolling down long rotation queues during busy live shows.
- **Rotation Anchor (Round Start Flag & DJ Tablet Sync)**:
  - Displays a visual red `⚓ ANCHOR` badge next to the performer the rotation round is anchored to.
  - Highlights the Rotation Anchor's card with a subtle red background (`--row-start-bg`) and border across the desktop app, Remote DJ Board (`dj.html`), Mobile Performer Portal (`mobile.html`), and Patron Request Portal (`PatronPortal.html`).
  - Allows the DJ to instantly see when a full rotation cycle/round has completed once that singer returns to the top.
  - Any singer can be designated as the Rotation Anchor via the `"Set as Rotation Anchor"` context action, desktop `⚓` button, or DJ tablet `⚓` action button.

### 🎯 Lyracist Live Trivia (`Lyracist`, `KSRotation`, & `Lyracist.Trivia`)
- **Manual Game Flow Controls for DJ / Game Master (`Lyracist.Trivia`)**:
  - **Interactive Host Pacing (Manual Mode)**: When `⚡ Auto-Run Game` is toggled off, questions load into a ready Reading/Standby state with the timer paused, giving the DJ full control to read the question over the venue microphone before initiating the countdown timer.
  - **Question Navigation & Direct Jump**: Seamlessly move backwards (`⏮ Prev`) or forwards (`Next ⏭`), or select any question index directly from the jump dropdown to review or present questions on demand.
  - **Dynamic Timer Bump & Trim**: On-the-fly adjustment buttons (`[-5s]`, `[+5s]`, `[+10s]`, `[🔄 Reset]`) let the host give players more thinking time or accelerate countdowns dynamically based on live room reactions.
  - **Staged Fade, Instant Reveal & Question Voiding**: Manually eliminate incorrect choices one by one (`✂ Fade Option`), instantly reveal answers (`⚡ Instant Reveal`), or nullify flawed questions (`❌ Void Question`) without penalizing player scores or streaks.
  - **DJ Keyboard Shortcuts**: Complete hands-on-keyboard operation via `Spacebar` (Pause/Resume Timer), `Right Arrow` / `PageDown` (Next Question), and `Left Arrow` / `PageUp` (Previous Question).
- **Live Settings Synchronization & Instant Persistence**:
  - Modifying question countdown duration, elimination fade intervals, post-reveal delay, point values, or auto-run settings immediately syncs to the active game engine and persists to storage without requiring manual saves or game restarts.
- **Randomized Wrong Answer Elimination**:
  - Uses Fisher-Yates randomization so wrong answers fade in unpredictable sequences on every question, eliminating any positional pattern tells.
- **Responsive Game Complete & Intermission Screen**:
  - Side-by-side leaderboard table and intermission sign-up QR cards ensure Wi-Fi and join QR codes never overlap the bottom connection or copyright bar on any display resolution.
- **Tiered Option Value Scoring (100% / 70% / 40%)**:
  - **Dynamic Multiplier Tiers**: Base points awarded scale dynamically based on how many options remain visible when a player buzzes in:
    * **4 Options Visible**: **100% Points** (1,000 pts default). Awards full points to players with instant knowledge before any wrong options fade.
    * **3 Options Visible**: **70% Points** (700 pts default). Automatically drops as the 1st wrong option fades at the 2/3 countdown mark.
    * **2 Options Visible (50/50)**: **40% Points** (400 pts default). Automatically drops as the 2nd wrong option fades at the 1/3 countdown mark.
  - **Live Mobile Multiplier Pill (`trivia.html`)**: Mobile devices display a real-time point multiplier badge (`⚡ 100% VALUE`, `⚡ 70% VALUE`, `⚡ 40% VALUE`) above the buzzer buttons, visually syncing with fading options on the big screen.
  - **Configurable in Trivia Settings**: Enable/disable tiered scoring and customize option percentages across all applications.
- **15 Category Starter Databases (2,250 Questions) with Balanced Option Distribution**:
  - Every single category database contains 150 curated, accurate questions with explanations, and multiple-choice answers evenly distributed across all 4 options (**~25% A, ~25% B, ~25% C, ~25% D**).
  - Dynamic Auto-Discovery: Users can create and drop unlimited custom JSON question packs into `Packs/` with automatic database seeding on startup:
    1. **Famous Lines & Sayings From Movies** (`famous_movie_quotes.json`) - 150 questions
    2. **Bikers & Motorcycles** (`biker_trivia.json`) - 150 questions
    3. **Rock & Roll** (`rock_and_roll.json`) - 150 questions
    4. **Country Music** (`country_music.json`) - 150 questions
    5. **Complete the Lyric** (`complete_the_lyric.json`) - 150 questions
    6. **Music Legends** (`music_legends.json`) - 150 questions
    7. **Blockbuster Movie Soundtracks** (`movie_soundtracks.json`) - 150 questions
    8. **80s & 90s Pop Culture** (`pop_culture_80s_90s.json`) - 150 questions
    9. **TV Shows** (`tv_shows.json`) - 150 questions
    10. **Logos & Slogans** (`logos_and_slogans.json`) - 150 questions
    11. **World Geography** (`geography.json`) - 150 questions
    12. **State & World Capitals** (`state_capitals.json`) - 150 questions
    13. **World History** (`history.json`) - 150 questions
    14. **Sports & Athletes** (`sports.json`) - 150 questions
    15. **Pub Trivia All-Stars** (`pub_general_knowledge.json`) - 150 questions
- **Unified 70:30 Pre-Game Lobby & Category Showcase (`TriviaDisplayWindow`)**:
  - **70% Left Hero Column**: High-resolution 16:9 Category Announcement Banner (`Banners/CategoryBanners/{pack}.png`) with ambient illuminated border, tonight's category theme title, and topic subtitle. Dynamic cross-pack syncing automatically updates the big screen whenever the Game Master selects a category from the dropdown.
  - **30% Right Onboarding Stack**:
    1. **⏱️ Game Start Countdown Clock**: Large digital timer with pulsing amber badge that ticks down to game launch.
    2. **📶 1. Connect to Wi-Fi Card**: Dedicated scan-to-connect Wi-Fi QR code with venue SSID and WPA password.
    3. **📱 2. Join Trivia Game Card**: Dedicated scan-to-join mobile buzzer QR code pointing to `http://<LAN-IP>:8085/trivia` with direct URL.
  - **Bottom Connection & Copyright Bar**: Displays mobile buzzer play address, company copyright information (`© 2026 PAROLE Software - Licensed under GPL-3.0-or-later.`), and app branding.
  - **Full-Width Ticker Bar**: Continuous horizontal marquee scrolling venue announcements, host branding, game rules, and buzzer tips.
- **Single-Click Pre-Game Launch (`🎯 Launch Pre-Game Lobby & Countdown`)**:
  - A single primary action button in the Game Master console that opens/focuses the big screen, locks the category banner, starts the pre-game countdown, and activates the lobby with one click. Accompanied by `"▶ Start Game Now (Skip Countdown)"` for instant kickoff which immediately opens and hydrates the live question projection screen and halts background lobby timers.
- **16:9 Category Announcement Banners (`Banners/CategoryBanners/`)**:
  - Clean 1920x1080 (16:9) graphic banners generated for all 15 categories, focusing on category branding and theme callouts without hardcoded question counts or timers so banners stay accurate regardless of host configuration.
- **Automated Solution Asset Copying**:
  - Configured `Packs\**\*`, `Banners\**\*`, and `Data\**\*` with `CopyToOutputDirectory=PreserveNewest` across all projects, ensuring category packs, announcement banners, databases, and configuration automatically copy to build outputs.
- **Accurate Multi-Monitor Projection**: Select any connected monitor (TV, secondary HDMI, projector) from the host console and cast seamlessly with physical resolution DPI awareness without forcing to Monitor 0.
- **Venue & Game Master Customization**: Customize Venue Name and Game Master / Host Name directly in the **⚙️ Settings & Display** tab with instant two-way live update across projection monitors and mobile buzzer devices.
- **Application Exit & Projection Screen Controls**: 1-click **✕ Exit** button in the main Game Master header bar immediately and cleanly terminates all background socket servers and timers. Press Escape (`Esc`) to close/exit the venue projection window, click the floating `✕` close button in the top-right corner, or right-click anywhere for context menu options.
- **Dedicated Venue Connect Instructions Screen**:
  - **Dual QR Code Architecture**: Projects two enlarged high-contrast QR cards (380×380 px) on venue screens:
    1. **1. Connect to Wi-Fi**: High-resolution scan-to-connect QR code with SSID and password for 1-tap phone connection.
    2. **2. Join Trivia Game**: High-resolution mobile buzzer QR code and URL for mobile buzzer login (`http://<ip>:8085`).
  - **Pre-Game Countdown Clock**: Live digital countdown clock (`"⏱ TRIVIA GAME STARTS IN: 04:59"`) that automatically starts ticking when the screen is cast to the monitor, and auto-starts the game upon reaching 00:00.
  - **Host Game Master Controls**: Rapid timer adjustments (`+1m`, `+5m`, `Reset`), screen toggle button, and one-click Wi-Fi auto-detection.
- **Top Scrolling Player & Score Marquee**: Continuous horizontal ticker across the top of the venue display window displaying registered players, teams, and live scores, with a special gold crown badge highlighting the top scorer.
- **3-Tab Game Master Console & Settings Page**:
  - **🎯 Live Game Master**: Streamlined live command deck with pack picker, auto-run switch, live question visualizer, QR connection code, scoreboard, and round flow controls.
  - **⚙️ Settings & Display**: Dedicated 3-column no-scroll settings tab for Intermission delay (minutes), auto-start toggle, display monitor selector, question timers (15s), wrong answer elimination speed (5s), post-reveal buffer (5s), scoring rules, sound effects, port, and venue branding.
  - **👥 Players & Teams**: Full roster grid with player names, team names, total points, streaks, answer statistics, and player kick/removal tools.
- **Post-Game Intermission Countdown & Automatic Next Game Start**:
  - When a game finishes, if auto-start is enabled, an intermission countdown runs for the configured duration (e.g. 3 minutes).
  - Venue display, GM console, and mobile web app display a live ticking countdown banner (`"🎮 NEXT TRIVIA ROUND STARTS IN: mm:ss"`).
  - Host can click `"▶ Start Now (Skip)"` to bypass the intermission, and once expired, the engine automatically selects the next question pack (or reshuffles) and launches the new round.
- **Connect Instructions Screen & Dedicated Trivia Pages**:
  - **`Lyracist`**: Access `"👁️ Preview Connect Screen"` in `SettingsPage` (Column 4: Connect & Request Instructions), with full Trivia controls and configuration cleanly located in dedicated **Trivia** and **Trivia Settings** pages.
  - **`KSRotation`**: Preview the Wi-Fi and Request Connect screen directly from the `Connect & Request Instructions` display settings, while Trivia rounds and configuration remain organized in dedicated **Trivia** and **Trivia Settings** tabs without cluttering the primary Rotation toolbar.
- **Configurable Questions Per Game**: Set the number of questions per game round (5, 10, 15, 20, 25, 50, 100, or custom). Slices the randomized question pack to the exact number desired.
- **Game Complete Winner Celebration & Team Announcements**:
  - Automatically calculates final rankings upon completing the allotted questions.
  - **Venue Display Screen (`TriviaDisplayWindow`)**: Shows a gold trophy celebration screen with the winning score and announces the champion. If a team wins, prominently shows `"👑 WINNING TEAM: {TeamName}"` and lists all team members.
  - **Mobile Player App (`trivia.html`)**: Mobile devices display the winner announcement, full team member breakdown, personal placement rank, final score, and full top 10 leaderboard.
  - **Host Console (`MainWindow`)**: Displays winning team/player banner with roster and score breakdown.
- **Zero-Config LAN Mobile Web Server (`TcpListener`)**: Direct socket HTTP server listening on `0.0.0.0` (all interfaces) with RFC 1918 LAN IP resolution, allowing smartphones and tablets on local Wi-Fi to load buzzers and buzz in without Windows URL reservation restrictions or admin privileges.
- **Dynamic Question Shuffling**: Questions are automatically shuffled when a category pack is selected and when each game begins, ensuring questions appear in a unique randomized sequence each time a category is played.
- **Dynamic Multi-Monitor Selection**: Host console allows selecting any connected display monitor (Monitor 1, Monitor 2, TV / HDMI) to dynamically project the venue display screen with DPI awareness.
- **Auto-Running Gameplay & 5-Second Progressive Elimination**:
  - Automatically progresses through questions with a live 15-second countdown timer on big screen and mobile buzzers.
  - After 15 seconds, incorrect answers sequentially fade out one every 5 seconds until only the correct answer remains illuminated.
  - After showing the correct answer with full explanation, pauses for 5 seconds before automatically advancing to the next question.
- **Dedicated Solution Folders (`Data/`, `Settings/`, `Packs/`, `Banners/`)**: Centralized repository containing SQLite database `Data/trivia.db`, game settings `Settings/lyracist_trivia_settings.json`, customizable JSON question packs in `Packs/`, and 16:9 high-resolution category announcement banners in `Banners/CategoryBanners/`.
- **14 Curated Category Question Databases (1,450 Questions Total, No Duplicates)**:
  - 🏍️ **Bikers & Motorcycle Culture**: 150 high-octane questions covering Harley-Davidson, Indian, classic choppers, engine mechanics, famous biker movies, historic rallies, MC culture, and legendary rides.
  - 🎸 **Rock & Roll**: 100 questions on classic rock, 70s/80s arena bands, iconic albums, guitar legends, and rock history.
  - 🤠 **Country Music**: 100 questions on outlaw country, 90s anthems, Grand Ole Opry legends, and honky-tonk history.
  - 🌍 **Geography**: 100 questions on world continents, oceans, mountain peaks, rivers, borders, islands, and landmarks.
  - 🏛️ **State Capitals**: 100 questions on all 50 U.S. state capitals, territorial capitals, and major world capitals.
  - 📜 **History**: 100 questions on ancient civilizations, American revolutions, world wars, space race, and historic moments.
  - 🎤 **Complete the Lyric**: 100 questions on karaoke singalong lyrics across rock, pop, 80s/90s classics, and country anthems.
  - 📺 **TV Shows**: 100 questions on classic sitcoms, drama series, sci-fi cult classics, and Emmy-winning television.
  - 🏆 **Sports**: 100 questions on NFL football, MLB baseball, NBA basketball, NHL hockey, soccer, and Olympics.
  - 🏷️ **Logos & Brand Slogans**: 100 questions on famous advertising taglines, company mascots, brand history, and logos.
  - 🌟 **Music & Karaoke Legends**: 100 questions on chart-topping superstars, iconic frontmen/frontwomen, and music history.
  - 🕹️ **80s & 90s Pop Culture**: 100 questions on retro toys, video game classics, 80s/90s movies, fashion fads, and trends.
  - 🎬 **Blockbuster Movie Soundtracks**: 100 questions on iconic film scores, Oscar-winning theme songs, and needle-drops.
  - 🍻 **Pub Trivia All-Stars**: 100 questions on science, literature, food & drink, myths, idioms, and pub favorites.
- **Zero-Install Mobile Player Buzzers (`/trivia`)**: Audience members scan an on-screen QR code on their smartphones to join with their team name and buzz in with 4 large responsive touch buttons with real-time feedback and haptics.
- **16:9 Multi-Monitor Projection Screen (`TriviaDisplayWindow.xaml`)**: TV and projector output featuring category banners, live animated countdown timer with configurable 3-second warnings, 4-color answer options with progressive opacity elimination fading, room distribution stats, and live team leaderboard podium.
- **Game Master Host Console**: Complete control deck with monitor picker, auto-progression switch, question pack selector, manual/auto question triggers, timer controls, live answer graphs, and team score management.

### 🎙️ Performer & Rotation Queue
- **4-in-1 Audience Billboard & Connect Screen System (`KSRotation.Maui` & `KSRotation`)**:
  - **Option 1 (Hardware HDMI & Secondary Display Presentation)**: Cross-platform `SecondaryDisplayService` integrating Android `DisplayManager` and `Android.App.Presentation` to automatically cast an independent audience billboard window to any connected USB-C to HDMI adapter or wireless display, preserving full DJ controls on the tablet. Also supports Windows multi-window secondary projection.
  - **Option 2 (Wi-Fi Web Billboard — `/billboard`)**: Serves an embedded 16:9 audience display over the DJ Travel Router (`http://<ip>:5005/billboard`) compatible with any Smart TV browser, Fire TV Stick Silk browser, Chromecast with Google TV, or mobile device. Displays live Now Performing stage spotlight, Up Next card, upcoming rotation queue, live ticking clock, and dynamic server-rendered QR codes (`/api/qr?text=...`) for both the Song Request Portal and Venue Wi-Fi auto-join.
  - **Option 3 (On-Device Attractor / Intermission Mode)**: Dedicated `📺 Billboard` button in `PortalConnectionCard` launching a full-screen `BillboardOverlay` modal on the DJ tablet touchscreen during breaks, featuring a floating dismiss bar to quickly return to DJ controls.
  - **Option 4 (Chromecast Web-Casting — Built-in Google Cast Support)**: In-app mDNS device discovery (`ChromecastDiscoveryService`) and one-click casting to any Chromecast or Google TV on the Wi-Fi network. Leverages DashCast web-receiver protocol to stream the live responsive audience billboard (`billboard.html`) at native TV resolution with no mobile video compression overhead.
- **Multi-Project Solution Deployment & Standalone Build Workflow (`.slnx`)**: Configured with `<Deploy Solution="Debug|*" />` and `<Deploy Solution="Release|*" />` across solution files, with `KSRotation.Maui` excluded from default rebuilds of the main solution (`Lyracist.slnx`) via `<Build Solution="*|*" Project="false" />` so it is built separately without slowing down desktop WPF application builds.
- **Performer Row Legend & Guide (`KSRotation.Maui`)**: Dedicated "Legend" button on the bottom control card opening an in-app visual modal guide (`LegendOverlay`) explaining all row color indicators (Rotation Anchor [Red], Now Performing [Orange], Next [Blue], Music Track [Green], Paused/Dimmed), action buttons (`✓`, `✏`, `🎙`, `⚓`, `▲`, `▼`, `❚❚` / `▶ Resume`, `🗑` / `↺`), and multi-colored song round checkboxes (1–10) across portrait and landscape orientations.
- **Duet & Backup Partner Support**: Full end-to-end integration of duet/backup singer tracking across Lyracist, KSRotation (WPF), KSRotation.Maui, the mobile Patron Request Portal, and the Remote DJ Web Console. Displays `(with {PartnerName})` across billboard displays, requests queues, and rotation cards.
- **Interactive Singer Queue**: Dynamic list matching performer names with requested song, artist, key changes, and custom notes.
- **Session Performed Songs History**: Dedicated Column 2 on Singer Rotation page tracking all completed performances with 5-color rotating theme cards (Violet, Cyan, Emerald, Amber, Rose) and multi-line copyable text logs.
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
- **Dedicated 4-Column Display Tab**: Structured into 4 equal columns (*Monitors & Screen Assignments*, *DJ & Event Banners*, *Star Wars Crawl & Spaceship*, *Connect & Request Instructions*) matching KsRotation.
- **Screen Activation Checkboxes**: Explicit checkboxes for "Enable Lyrics Projection Screen", "Enable Singer Rotation Billboard Screen", and "Enable DJ Banner Screen".
- **Dual-Window Projection**: Supports launching standalone windows for **Lyrics Projection**, **Singer Rotation Billboard**, and **DJ Banner**.
- **Eleven Dynamic Billboard View Modes**:
  - **Normal List**: A standard listing of the current singer and upcoming rotation queue.
  - **Star Wars Crawl**: High-fidelity 3D projection rendering a starry night sky with cool/warm twinkling star layers, and a 3D-angled text block crawling upward in space.
  - **Vegas Marquee**: Theatrical Broadway stage layout displaying the current performer's name in glowing letters inside a brass frame ringed by lavender/purple "marching ants" chase bulb animations.
  - **Vinyl Turntable**: Classic warm DJ-booth theme with a dynamic rotating vinyl 45 record, tonearm, and clean center label showing the current singer and song metadata.
  - **Disco Ball**: A mirror-ball dance club spectacle featuring rotating mirror-ball facets, floating reflected floor spots, sweeping stage light beams, and a bold neon hero performer card.
  - **Synthwave Grid**: Retro 80s outrun aesthetic with an animated perspective horizon grid in neon cyan and magenta, a glowing retro sun backdrop, and synth-styled typography.
  - **Concert Festival Lineup**: Main-stage music festival poster vibe featuring towering stage trusses, drifting golden concert sparkles, and dynamic sweeping spotlights framing the headlining performer.
  - **Casino Slot Reels**: High-roller casino excitement with spinning slot-machine reels that clunk to a stop on paylines, accompanied by a glittering jackpot coin flourish and golden glow.
  - **Jukebox**: Classic 1950s rock-and-roll diner cabinet with glowing chrome arches, animated rising bubble-tube light columns, prominent illuminated selector pushbuttons, and a split two-line queue layout (singer name with inline estimated wait time on top; song title in cyan italics below).
  - **Stadium Jumbotron**: Massive arena LED scoreboard with sweeping stadium floodlights crisscrossing the venue as the headlining singer is announced like a stadium superstar.
  - **Movie Theater 'Now Showing'**: Vintage Hollywood premiere aesthetic featuring a vintage 35mm film leader countdown (3...2...1) sweep, flickering cinematic projector beam, and marquee coming-attraction lobby poster cards.
- **High-Visibility Queue Typography (Up Next / Right Column)**: All right-side upcoming performer queue sections, headers, position numbers, wait time badges, round anchor tags, and "Up Next" / "On Deck" cards are rendered 2 font sizes larger across all 7 right-column audience projection themes, mobile tablet billboards (`BillboardView.xaml`), and web projection displays (`billboard.html`) for clear legibility across large venue monitors.
- **Automated Random Screen Rotation (`KSRotation`, `Lyracist`)**:
  - **Single Unified Interval Setting**: Replaced per-row time inputs with a single global setting (e.g., `180` seconds) to dictate how often the display rotates.
  - **Intelligent Random Cycling**: Automatically and randomly cycles among enabled screens at each interval, avoiding immediate repetition of the active screen when multiple views are selected.
  - **One-Click "✓ Select All" and "✗ Clear All"**: Quickly toggles all 11 projection themes with a single button click.
  - **Optimized Two-Column Display Layout**: Reorganized `KSRotation` Display tab so Target Monitor, Connect Instructions, and Casting Controls are conveniently grouped in Column 0 on the left, while Screen Rotation occupies Column 1 on the right.
- **Dynamic Chroma-Keying**: Automatically strips standard `.cdg` file backgrounds and borders (pixel index `0,0`) in real time to render lyrics transparent.
- **GPU-Accelerated 4K Backdrops**: Beautiful, responsive vector backdrops layered behind transparent lyrics, wrapped in Viewbox controls to fit HD and 4K displays.
- **Flexible Monitor Assignment & Dropdowns**: Direct dropdown selection in the KJ interface to target specific connected monitors for both the Rotation Display and DJ Banner windows, with real-time dynamic window relocation and automatic fallback to secondary/primary screens if unplugged.
- **DJ Banner Projection Screen**: Upload, select, delete, and project borderless full-screen custom DJ branding/promotional banners (supporting PNG, JPG, GIF, BMP, and looping MP4 video). Includes **Show request QR Code overlay on DJ Banner** checkbox setting.
- **Scan-to-Connect Wi-Fi & Request Instructions Dynamic Graphic**: Auto-detects connected Wi-Fi SSID, request portal URL, and target display resolution (`1080p`, `1440p`, `4K 3840x2160`) to generate custom vector `ConnectInstructions.png` banners with dual high-density QR codes for Wi-Fi join and song requests.
- **Wi-Fi Password Persistence Manager (`wifi_passwords.json`)**: Automatically saves and recalls Wi-Fi passwords per SSID (venue Wi-Fi, travel router, mobile hotspot) so passwords never have to be re-entered at recurring venues.
- **Same-Screen Deconfliction Priority**: Automatically disables and hides the DJ Banner if the Rotation Display is targeted or moved to the same monitor.
- **Wireless Casting Support**: Stream the singer rotation billboard directly to Miracast, Chromecast, AirPlay, Wireless HDMI, or Browser Cast using high-performance off-screen buffer rendering.
- **Browser Cast Server**: Self-hosts a local web server (http://localhost:8080/rotation/) to allow any browser on the local network to view the singer rotation billboard in real-time.
- **"None" Targeting (Deselection)**: Support for selecting *None (Do not show)* in settings or projection controls to immediately close or hide projection windows when not in use.
- **Rear-Projection Mirroring**: Mirror the lyrics screen horizontally for custom projector arrangements.

### 🔊 Audio Configuration & Background Music (BGM)
- **Advanced Audio Engine**: Master volume, latency compensation, and active output device selection.
- **Active Performance Key Transposition**: Real-time pitch transposition from `-6` to `+6` semitones. Automatically hot-reloads the audio track and seeks back to the exact millisecond in under 150ms to apply FFmpeg-based pitch shifting (`asetrate` + `atempo`) during active playback.
- **Audio Processing Controls**: Fully adjustable 3-band EQ (Bass, Mid, Treble) alongside integrated Compressor and Limiter.
- **Per-Singer Key & Tempo Recall**: Automatically tracks each singer's customized pitch shift (key) and tempo (playback speed multiplier) per song in `SingerHistory`. When a singer is added to rotation or re-queues a track from search or singer history, their preferred key and speed are instantly recalled. Global profile defaults in `SingerAudioSettings` act as the initial baseline when a singer performs a new track.
- **Per-Singer Mic Level & Vocal EQ Recall**: Automatically loads and applies the on-stage performer's saved vocal gain (mic level), 3-band EQ (Treble, Mid, Bass), Compressor, and Limiter settings from `SingerAudioSettings` to the audio engine and `KaraokePage` sliders when their performance starts. Includes dedicated **[Save to Singer]** and **[Recall Singer]** action buttons right on the Audio Controls card header for one-click live preset saving and instant recall.
- **Automatic Volume Normalization**: Measures each track's integrated loudness (LUFS) via a single-pass FFmpeg `loudnorm` (EBU R128) analysis the first time it's loaded for playback, then applies a corrective gain so every song lands near the same perceived loudness without the DJ manually riding the volume between tracks. Configurable target loudness and an on/off toggle in Settings; measurement runs in the background and is cached permanently per song.
- **Show Flow Automation**: Automatically manages background music levels between tracks (Opening Music, Fill-in Music, and End of Rotation sets).
- **Configurable Fill-In Music Delay**: Settings slider (0-30s) to set the exact delay before fill-in background music starts.
- **Special Occasion Audio**: Add custom sound bites and clips (Birthdays, Holiday jingles) with individual tone (Bass/Treble) and gain overrides.

### 🎵 Song Library & Integrations
- **Local Scanner & Rescan Maintenance**: Scans folders for `.mp3 + .cdg`, `.mp4` or `.zip` files and builds a local query database. Cleanly deletes obsolete database logs and search indexes for files that were renamed or deleted on the drive during rescans.
- **Catalog Book Exporter**: Export the entire song library (Karaoke or Music) directly into professionally formatted, paginated PDF, plain text (.txt), or Word (.docx) documents with repeating table headers from the Database Manager.
- **Streaming & Search Integration**: Includes search support for YouTube (direct URL stream linking and metadata lookups) and Spotify & Amazon Music links.
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
- **Resizable Index Column**: Integrated a vertical `GridSplitter` allowing users to click and drag the column boundary to manually resize the index list width, preventing text truncation on different screen sizes.
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
2. In **Service Logins & API Keys**, enter credentials for YouTube or Spotify if using streaming search.
3. In **Theme & Appearance**, toggle **Test Mode** on to test queue workflows.
4. Set up monitor assignments under **Display & Projection Monitors** or the main control panel.
5. Manage backups and database restorations in the **Database Maintenance** section under the **Music Library** group box.
6. Curate the DJ Name, active Venues catalog list, Billboard View Mode, and Star Wars Crawl Text Template (Dramatic, Comedic, Over-the-Top, or Custom) on the Settings page, with real-time text previews.

## License

The Lyracist suite is free software, licensed under the **GNU General Public License v3.0 or later**
(`GPL-3.0-or-later`). See [LICENSE](LICENSE). Third-party components remain under their own licenses;
see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
