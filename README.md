<!-- Edited on Aug 20, 2026 @ 06:47:00 -> Added Tiered Option Value Scoring (100% / 70% / 40%) details to README.md -->
Last Edit: Aug 20, 2026 - Tiered Option Value Scoring (100% / 70% / 40%) Across All Solution Apps
# Lyracist Pro

Lyracist Pro is a premium, modern Windows WPF Karaoke hosting application designed for professional KJs and home entertainment. Built using WPF and .NET 10, it offers advanced multi-monitor projection, rich audio customization, local and streaming library search, an integrated mobile tablet server for performer lyrics, active rotation management with current performer top-floating and round-cycle indicators, integrated interactive pub/bar trivia with dedicated separate settings, multi-monitor auto-casting, automated projection pause synchronization, full feature parity across standalone and embedded Trivia engines, dynamic tiered option value scoring (100% / 70% / 40%), and a dedicated Trivia Database Creator (`TriviaDbCreator.exe`).

---

## Key Features

### 🛠️ Trivia Database Creator & Pack Studio (`TriviaDbCreator.exe`)
- **Visual Category & Question Authoring**: Standalone WPF MVVM desktop app using Fluent UI (`WPF-UI`) for creating, editing, and expanding trivia question databases.
- **Left Sidebar Pack Explorer**: Auto-discovers and navigates all JSON question packs in `TriviaData/packs/` with search filtering and question count indicators.
- **Interactive Question Editor**: Real-time editor with colored option cards (▲ Purple, ◆ Cyan, ● Amber, ■ Rose), correct answer radio toggles, difficulty dropdown, and explanation notes.
- **1-Click Option Balancing**: Automatically shuffles option positions across all questions in a pack to guarantee an even ~25% distribution across choices A, B, C, and D.
- **16:9 Banner Studio**: Generates high-resolution 16:9 Category Announcement Banners (`TriviaData/Banners/{pack}.png`) with one click.
- **Direct SQLite Seeding**: 1-click database synchronization updating `TriviaData/trivia.db`.

### 🎤 Singer Rotation & Queue Management (`Lyracist`, `KSRotation`, `KSRotation.Maui`)
- **Float Current Singer to Top Option**:
  - Toggling `"Float Current to Top"` keeps the currently performing singer at index 0 (top of the rotation list).
  - When songs finish, the performer shifts to the end of the queue and the next active singer automatically floats to the top, eliminating scrolling down long rotation queues during busy live shows.
- **1st Singer in Rotation (Round Start Anchor Flag)**:
  - Displays a visual red `🚩 1ST` badge next to the anchor performer who started the rotation round.
  - Allows the DJ to instantly see when a full rotation cycle/round has completed once that singer returns to the top.
  - Any singer can be designated as the 1st singer anchor via the `"Set as 1st Singer (Round Start)"` context action or `🚩` button.

### 🎯 Lyracist Live Trivia (`Lyracist`, `KSRotation`, & `Lyracist.Trivia`)
- **Tiered Option Value Scoring (100% / 70% / 40%)**:
  - **Dynamic Multiplier Tiers**: Base points awarded scale dynamically based on how many options remain visible when a player buzzes in:
    * **4 Options Visible**: **100% Points** (1,000 pts default). Awards full points to players with instant knowledge before any wrong options fade.
    * **3 Options Visible**: **70% Points** (700 pts default). Automatically drops as the 1st wrong option fades at the 2/3 countdown mark.
    * **2 Options Visible (50/50)**: **40% Points** (400 pts default). Automatically drops as the 2nd wrong option fades at the 1/3 countdown mark.
  - **Live Mobile Multiplier Pill (`trivia.html`)**: Mobile devices display a real-time point multiplier badge (`⚡ 100% VALUE`, `⚡ 70% VALUE`, `⚡ 40% VALUE`) above the buzzer buttons, visually syncing with fading options on the big screen.
  - **Configurable in Trivia Settings**: Enable/disable tiered scoring and customize option percentages across all applications.
- **15 Category Starter Databases (2,250 Questions) with Balanced Option Distribution**:
  - Every single category database contains 150 curated, accurate questions with explanations, and multiple-choice answers evenly distributed across all 4 options (**~25% A, ~25% B, ~25% C, ~25% D**).
  - Dynamic Auto-Discovery: Users can create and drop unlimited custom JSON question packs into `TriviaData/packs/` with automatic database seeding on startup:
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
  - **70% Left Hero Column**: High-resolution 16:9 Category Announcement Banner (`TriviaData/Banners/{pack}.png`) with ambient illuminated border, tonight's category theme title, and topic subtitle. Dynamic cross-pack syncing automatically updates the big screen whenever the Game Master selects a category from the dropdown.
  - **30% Right Onboarding Stack**:
    1. **⏱️ Game Start Countdown Clock**: Large digital timer with pulsing amber badge that ticks down to game launch.
    2. **📶 1. Connect to Wi-Fi Card**: Dedicated scan-to-connect Wi-Fi QR code with venue SSID and WPA password.
    3. **📱 2. Join Trivia Game Card**: Dedicated scan-to-join mobile buzzer QR code pointing to `http://<LAN-IP>:8085/trivia` with direct URL.
  - **Bottom Connection & Copyright Bar**: Displays mobile buzzer play address, company copyright information (`© 2026 PAROLE Software - All rights reserved.`), and app branding.
  - **Full-Width Ticker Bar**: Continuous horizontal marquee scrolling venue announcements, host branding, game rules, and buzzer tips.
- **Single-Click Pre-Game Launch (`🎯 Launch Pre-Game Lobby & Countdown`)**:
  - A single primary action button in the Game Master console that opens/focuses the big screen, locks the category banner, starts the pre-game countdown, and activates the lobby with one click. Accompanied by `"▶ Start Game Now (Skip Countdown)"` for instant kickoff.
- **16:9 Category Announcement Banners (`TriviaData/Banners/`)**:
  - Clean 1920x1080 (16:9) graphic banners generated for all 14 categories, focusing on category branding and theme callouts without hardcoded question counts or timers so banners stay accurate regardless of host configuration.
- **Automated `TriviaData` Build Output Copying**:
  - Configured `TriviaData\**\*` with `CopyToOutputDirectory=PreserveNewest` across all projects, ensuring category packs, announcement banners, databases, and configuration automatically copy to build outputs.
- **Accurate Multi-Monitor Projection**: Select any connected monitor (TV, secondary HDMI, projector) from the host console and cast seamlessly with physical resolution DPI awareness without forcing to Monitor 0.
- **Venue & Game Master Customization**: Customize Venue Name and Game Master / Host Name directly in the **⚙️ Settings & Display** tab with instant two-way live update across projection monitors and mobile buzzer devices.
- **Projection Screen Escape & Close Controls**: Press Escape (`Esc`) to immediately close/exit the venue display window, click the floating `✕` close button in the top-right corner, or right-click anywhere for context menu options.
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
- **Dedicated Data Folder (`TriviaData/`)**: Centralized repository containing SQLite database `trivia.db`, game settings `trivia_settings.json`, customizable JSON question packs in `TriviaData/packs/`, and 16:9 high-resolution category announcement banners in `TriviaData/Banners/`.
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