<!-- Edited on Sep 5, 2026 @ 09:28:00 -> Port Live Question Controls and manual/auto flow controls to Lyracist TriviaPage -->
Last Edit: Sep 5, 2026 - Lyracist Live Question Controls (Prev/Next/Jump/Timer/Void) & KSRotation contrast fixes
# Lyracist Pro
    
Lyracist Pro is a premium, modern Windows WPF Karaoke hosting application designed for professional KJs and home entertainment. Built using WPF and .NET 10, it offers a safe, DJ-friendly Auto-Advance system with grace period timer and fill-in music ducking, advanced multi-monitor projection, rich audio customization, high-speed in-memory library scanning and metadata probing (TagLibSharp), local and streaming library search, an integrated mobile tablet server for performer lyrics, active rotation management with current performer top-floating, smart new singer round insertion, inactive singer filtering, intelligent name and artist proper-casing with mixed-case and apostrophe prefix support, spacious high-DPI singer cards with full button border visibility on 1080p laptops, global auto-highlighting/select-all on focus across all text and numeric inputs, responsive portrait/landscape tablet layouts and Android launch stability in `KSRotation.Maui`, dedicated cross-app landscape tablet kiosk request station (`kiosk.html`) with Attractor/Welcome screen and PWA fullscreen support, remote DJ web control with checkmark-first action buttons and popup modal performer addition (`dj.html`), integrated interactive pub/bar trivia with dedicated separate settings, manual DJ game flow controls with question jumping, timer adjustments, and keyboard shortcuts, multi-monitor auto-casting, randomized answer elimination, non-overlapping score and intermission screens, automated projection pause synchronization, dynamic tiered option value scoring (100% / 70% / 40%), Knockout Trivia standalone game-show module with phone/tablet QR connect, session security, testing module & DJ bot simulator, and automatic internal scoring (`KnockoutTrivia.exe`), a dedicated Trivia Database Creator (`TriviaDbCreator.exe`), and a unified, consolidated directory architecture across all apps (`Settings/`, `Data/`, `Banners/`, `Packs/`, `Logs/`).

---
    
## Key Features

### 🛑 Last Round Rotation Management (`Lyracist`, `KSRotation`, `KSRotation.Maui`, `dj.html`)
- **One-Click Host & Remote Activation**: Standout emerald green "Last Round" button positioned directly after the Clear button across host interfaces (`RotationPage.xaml`, `MainWindow.xaml`, `MainPage.xaml`) and on the Remote DJ Board (`dj.html`), immediately synchronizing across the rotation lifecycle, embedded servers, and display services.
- **Audience Screen Announcement**: Bold crimson banner (`★ THE LAST ROUND FOR THE NIGHT IS CURRENTLY UNDERWAY ★`) displayed prominently on the Lyracist `RotationWindow`, KSRotation `SingerDisplayWindow` (across all display projection styles), KSRotation.Maui `BillboardView` (on-device attractor and external HDMI/Presentation displays), Remote DJ Board (`dj.html`), and the real-time web billboard (`billboard.html`).
- **Dynamic Queue Pruning**: Once a performer has sung in the last round, they are automatically hidden from the audience rotation queue (now singing, next, on-deck), while remaining visible on host and remote DJ screens with a distinct `DONE (LAST ROUND)` indicator badge.

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
    * `Banners/KSRotation/`: Contains `DJBanners/`, `Announcements/`, and `EventBanners/` (including 16:9 standard event banners).
    * `Banners/Lyracist/`: Contains `DJBanners/`, `Announcements/`, and `EventBanners/` (including 16:9 standard event banners).
    * `Banners/LyracistTrivia/`: Contains `CategoryBanners/` (15 themed category graphics) and `Announcements/`.
    * `Banners/KnockoutTrivia/`: Contains `Announcements/` and `CustomBanners/`.
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
- **16:9 Banner Studio**: Generates high-resolution 16:9 Category Announcement Banners (`Banners/LyracistTrivia/CategoryBanners/{pack}.png`) with one click.
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
- **1st Singer in Rotation (Round Start Anchor Flag & DJ Tablet Sync)**:
  - Displays a visual red `🚩 1ST` badge next to the anchor performer who started the rotation round.
  - Highlights the 1st singer card with a subtle red background (`--row-start-bg`) and border across the desktop app, Remote DJ Board (`dj.html`), Mobile Performer Portal (`mobile.html`), and Patron Request Portal (`PatronPortal.html`).
  - Allows the DJ to instantly see when a full rotation cycle/round has completed once that singer returns to the top.
  - Any singer can be designated as the 1st singer anchor via the `"Set as 1st Singer (Round Start)"` context action, desktop `🚩` button, or DJ tablet `🚩` action button.

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
  - **70% Left Hero Column**: High-resolution 16:9 Category Announcement Banner (`Banners/LyracistTrivia/CategoryBanners/{pack}.png`) with ambient illuminated border, tonight's category theme title, and topic subtitle. Dynamic cross-pack syncing automatically updates the big screen whenever the Game Master selects a category from the dropdown.
  - **30% Right Onboarding Stack**:
    1. **⏱️ Game Start Countdown Clock**: Large digital timer with pulsing amber badge that ticks down to game launch.
    2. **📶 1. Connect to Wi-Fi Card**: Dedicated scan-to-connect Wi-Fi QR code with venue SSID and WPA password.
    3. **📱 2. Join Trivia Game Card**: Dedicated scan-to-join mobile buzzer QR code pointing to `http://<LAN-IP>:8085/trivia` with direct URL.
  - **Bottom Connection & Copyright Bar**: Displays mobile buzzer play address, company copyright information (`© 2026 PAROLE Software - All rights reserved.`), and app branding.
  - **Full-Width Ticker Bar**: Continuous horizontal marquee scrolling venue announcements, host branding, game rules, and buzzer tips.
- **Single-Click Pre-Game Launch (`🎯 Launch Pre-Game Lobby & Countdown`)**:
  - A single primary action button in the Game Master console that opens/focuses the big screen, locks the category banner, starts the pre-game countdown, and activates the lobby with one click. Accompanied by `"▶ Start Game Now (Skip Countdown)"` for instant kickoff which immediately opens and hydrates the live question projection screen and halts background lobby timers.
- **16:9 Category Announcement Banners (`Banners/LyracistTrivia/CategoryBanners/`)**:
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
- **Dedicated Solution Folders (`Data/`, `Settings/`, `Packs/`, `Banners/`)**: Centralized repository containing SQLite database `Data/trivia.db`, game settings `Settings/lyracist_trivia_settings.json`, customizable JSON question packs in `Packs/`, and 16:9 high-resolution category announcement banners in `Banners/LyracistTrivia/CategoryBanners/`.
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
- **3-in-1 Audience Billboard & Connect Screen System (`KSRotation.Maui` & `KSRotation`)**:
  - **Option 2 (Wi-Fi Web Billboard — `/billboard`)**: Serves an embedded 16:9 audience display over the DJ Travel Router (`http://<ip>:5005/billboard`) compatible with any Smart TV browser, Fire TV Stick Silk browser, Chromecast with Google TV, or mobile device. Displays live Now Performing stage spotlight, Up Next card, upcoming rotation queue, live ticking clock, and dynamic server-rendered QR codes (`/api/qr?text=...`) for both the Song Request Portal and Venue Wi-Fi auto-join.
  - **Option 3 (On-Device Attractor / Intermission Mode)**: Dedicated `📺 Billboard` button in `PortalConnectionCard` launching a full-screen `BillboardOverlay` modal on the DJ tablet touchscreen during breaks, featuring a floating dismiss bar to quickly return to DJ controls.
  - **Option 1 (Hardware HDMI & Secondary Display Presentation)**: Cross-platform `SecondaryDisplayService` integrating Android `DisplayManager` and `Android.App.Presentation` to automatically cast an independent audience billboard window to any connected USB-C to HDMI adapter or wireless display, preserving full DJ controls on the tablet. Also supports Windows multi-window secondary projection.
- **Performer Row Legend & Guide (`KSRotation.Maui`)**: Dedicated "Legend" button on the bottom control card opening an in-app visual modal guide (`LegendOverlay`) explaining all row color indicators (1st Singer [Red], Now Performing [Orange], Next [Blue], Music Track [Green], Paused/Dimmed), action buttons (`✓`, `✏`, `🎙`, `🚩`, `▲`, `▼`, `❚❚` / `▶ Resume`, `🗑` / `↺`), and multi-colored song round checkboxes (1–10) across portrait and landscape orientations.
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
- **Four Dynamic Billboard View Modes**:
  - **Normal List**: A standard listing of the current singer and upcoming rotation queue.
  - **Star Wars Crawl**: High-fidelity 3D projection rendering a starry night sky with cool/warm twinkling star layers, and a 3D-angled text block crawling upward in space.
  - **Vegas Marquee**: Theatrical Broadway stage layout displaying the current performer's name in glowing letters inside a brass frame ringed by lavender/purple "marching ants" chase bulb animations.
  - **Vinyl Turntable**: Classic warm DJ-booth theme with a dynamic rotating vinyl 45 record, tonearm, and center label showing the current singer and song metadata.
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