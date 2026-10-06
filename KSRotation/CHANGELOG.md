<!-- Edited on Oct 6, 2026 @ 13:06:00 -> Document new singer welcome screen trigger on name box lost focus in KSRotation CHANGELOG -->

# Changelog

## 2026-10-06

- **New Singer Welcome Screen Trigger on Name Entry Lost Focus (`MainWindow.xaml.cs`, `ViewModels/MainViewModel.Welcome.cs`, `ViewModels/MainViewModel.cs`)**:
  - Replaced the previous 2.5-second settle timer after typing with immediate welcome screen triggering when the singer name entry box loses focus (`LostFocus`).
  - Ensures the "Welcome to our new performer" screen is displayed only after the DJ has finished entering the performer's name and moved away from the input box, eliminating untimely interruptions while typing.
  - Added unit test coverage in `KSRotation.Tests/RotationTests.cs`.

- **Mouse Drag-and-Drop Rotation Queue Reordering (`MainWindow.xaml`, `MainWindow.xaml.cs`, `ViewModels/MainViewModel.cs`)**:
  - Full Mouse Drag-and-Drop Reordering: Hosts can grab and move any performer row directly within the rotation queue using the mouse to reposition singers instantly.
  - Dedicated Drag Grip Handle (⋮⋮): Added a dedicated drag handle column on the left edge with a 4-way move cursor (`SizeAll`) and tooltip hint ("Drag to reorder singer in rotation").
  - Protected Interactive Controls: Textboxes (Name, Duet Partner, Song, Artist) and buttons/checkboxes remain protected for standard editing and typing, while dragging from the grip handle or row background smoothly initiates reordering.
  - Midpoint Precision: Dropping on the top half of a target row inserts the singer above that row; dropping on the bottom half inserts them below. Dropping into empty list space below the queue moves the singer to the end of the rotation.
  - Partition & Partner Safety: Enforces active/inactive boundaries so inactive performers cannot enter the active queue, and keeps linked duet partners strictly adjacent via `EnforceLinkedAdjacency`.
  - Instant System Sync: Automatically recalculates estimated wait times, updates next-singer highlights, triggers TV display window updates, rebuilds the rotation JSON cache for mobile devices, and marks the database dirty for saving.
  - Unit Tests: Added comprehensive test coverage in `KSRotation.Tests/RotationTests.cs`.

- **Test Mode Settings GroupBox & Performer Count Options (3, 5, 10, 15) (`MainWindow.xaml`, `ViewModels/MainViewModel.cs`, `Models/AppSettings.cs`, `Shared/RotationHelpers.cs`)**:
  - Added dedicated "Test Mode Settings" GroupBox (GBX) in the Settings tab, separating test controls from general session rotation settings.
  - Added performer count dropdown selector supporting 3, 5, 10, and 15 sample singers.
  - Replaced sample singers with 18 realistic performer names (Brenda Bumps, James Smith, Raymond Carter, Sharon Roberts, Ami Anderson, Ali Davis, Randy Davis, Larry Strickland, Robert Roberts, Cynthis Nix, David Wayne, Danny Hinnant, Cerrina Culbert, Julia Stanton, Carol Henderson, Joe Bob Briggs, Craven Counts, and Dennis Starling) randomly selected and paired with popular songs.
  - Added 1-click "Load Test Singers Now" button to instantly populate the rotation queue on demand.
  - Added unit test suite in `KSRotation.Tests/RotationTests.cs`.

- **New Singer Placeholder Cleanup & Automatic Orphan Row Removal (`MainWindow.xaml.cs`, `Models/SingerEntry.cs`, `ViewModels/MainViewModel.cs`, `Shared/NameFormatting.cs`)**:
  - Automatically deletes a newly added performer row from the rotation queue if focus leaves the name field while the placeholder text remains unchanged ("New Singer") or empty/whitespace, avoiding clutter without prompting for confirmation.
  - Automatically cleans and strips "New Singer" if entered with additional text (e.g., transforming "New Singertom" into "Tom") and applies ProperCase formatting.
  - Pressing Enter in the singer name field now cleanly commits the name and shifts focus to the next input field.
  - Added unit test coverage in `KSRotation.Tests/RotationTests.cs`.

- **Single-Instance Application Enforcement (`App.xaml.cs`, `KSRotation.csproj`, `Shared/SingleInstanceHelper.cs`)**:
  - Enforced single-instance application execution for KSRotation.
  - Automatically brings the existing running KSRotation window to the front and restores it from minimized state if the user launches a second instance, then closes the duplicate instance immediately.
  - Added unit test coverage in `KSRotation.Tests/SingleInstanceHelperTests.cs`.

- **Duet Partner Remote Sync & DJ Tablet Display (`Models/RotationItemDto.cs`, `ViewModels/MainViewModel.Requests.cs`, `ViewModels/MainViewModel.cs`, `Resources/dj.html`, `Resources/kiosk.html`, `Resources/PatronPortal.html`, `Resources/billboard.html`)**:
  - Fixed an issue where duet partners added or edited on performers showed up on the desktop grid and TV screens (`billboard.html`), but did not appear on the DJ tablet (`dj.html`).
  - Added `duetPartnerName` alongside `partner` in `RotationItemDto` and `RebuildRotationJsonCacheNow()`.
  - Added `DuetPartnerName` and `Partner` handling to `Singer_PropertyChanged` in `MainViewModel.cs` so desktop edits immediately trigger dirty state, TV window display refresh, and JSON cache invalidation.
  - Updated `dj.html`, `kiosk.html`, `PatronPortal.html`, and `billboard.html` with dual-property fallback (`duetPartnerName || partner`).
  - Added unit test in `RotationTests.cs` verifying serialization of both duet partner properties.

## 2026-10-05

- **"Move to Top of Rotation" Performer Priority & Anchor Preservation (`MainWindow.xaml`, `MainViewModel.cs`, `MainViewModel.Requests.cs`, `KSRotation.Maui/MainPage.xaml`, `Shared/RotationHelpers.cs`)**:
  - Added "Move to Top of Rotation" option to each performer across KSRotation desktop and KSRotation.Maui.
  - Re-queues the performer at slot 1 when an active singer is currently singing, or at index 0 when no one is singing.
  - Strict Anchor Preservation: Moving a performer to the top does not in any way change, reassign, or clear the rotation anchor (`IsRotationStart`). The anchor singer marks round progression and remains strictly preserved; re-anchoring still requires manual assignment if desired.
  - Automatically clears skipped (`IsSkipped`), paused (`IsPaused`), inactive (`IsInactive`), and last-round completed (`HasSungInLastRound`) flags upon moving so the performer is restored to active eligibility. Duet partners remain adjacent.
  - Added "Move to Top of Rotation" to performer ContextMenu in `MainWindow.xaml`, added a dedicated `⤒` button on each performer row, and added a dedicated `⤒` button on Maui performer cards.
  - Added `move-to-top` and `move-top` action handling in `MainViewModel.Requests.cs` for remote DJ requests.
  - Added unit test coverage in `RotationTests.cs`.

- **DJ & Special Event Banner Monitor Selection Persistence (`MainWindow.xaml`, `MainViewModel.cs`)**:
  - Resolved an issue where the selected monitor device in the "DJ & Special Event Banners" settings ComboBox did not stay visible.
  - Added explicit two-way binding (`Mode=TwoWay`) on `DjBannerMonitorDevice` in `MainWindow.xaml`.
  - Updated `RefreshAvailableMonitors()` in `MainViewModel.cs` to capture, preserve, and restore `DjBannerMonitorDevice` (as well as `ConnectInstructionsScreen`) across monitor re-scans, preventing collection clears from resetting the selection to blank.
  - Implemented intelligent fallback defaulting to `SelectedMonitorDevice` or the primary/secondary monitor when unassigned or unavailable.
  - Added monitor refresh guard (`_isRefreshingMonitors`) across both WPF and MAUI compilation paths to suppress transient `null` values and unwanted settings overrides while monitor devices are enumerated.

- **Clear "Last Round Done" Flag & Restore Accidental Finished Singers (`MainWindow.xaml`, `MainViewModel.cs`, `MainViewModel.Requests.cs`, `KSRotation.Maui`, `dj.html`)**:
  - Added the ability to clear the `HasSungInLastRound` completion flag when a singer was marked finished on the last round by accident.
  - Interactive Singer Badge: Replaced the static "DONE (LAST ROUND)" badge with a clickable button badge (`DONE (LAST ROUND) ✕`) in the rotation queues of KSRotation desktop, KSRotation.Maui, and the Remote DJ portal (`dj.html`). Clicking or tapping this badge immediately removes the last-round done flag, restores the singer to active eligibility, recalculates wait times and round estimations, updates Next Singer highlights, and syncs to audience billboard and remote devices.
  - Singer Context Menu: Added "↩ Clear 'Last Round Done' Status" to singer row context menu in KSRotation desktop.
  - Remote DJ Board Action: Added `clear-last-round-done` request action in KSRotation's remote DJ web handler (`PatronRequestServer`) and bound it to the interactive badge in `dj.html`.
  - Edit Performer Dialog (Maui): Added "Done in Last Round (uncheck to restore)" checkbox in KSRotation.Maui's `EditSingerOverlay`.

## 2026-10-04

- **Dynamic Special Event Announcement Banner Studio (`MainWindow.xaml`, `MainViewModel.cs`, `MainViewModel.Requests.cs`, `dj.html`, `Shared/DjBannerFileManager.cs`)**:
  - Added 'Announcement' as a standard special event banner option in KSRotation, allowing DJs to author custom, high-definition full-screen celebration graphics dynamically on demand.
  - Interactive Host Modal: Selecting 'Announcement' immediately opens an intuitive dialog (`ShowDynamicAnnouncementPrompt`) on the host laptop where the DJ can type any custom greeting (e.g. *"Welcome to Jill & Robert, 1st timers tonight"*). Clicking 'Launch Banner' dynamically renders a 16:9 graphic into `EventBanners/Announcement.png` with midnight/deep purple gradient, ambient radial glow, corner fireworks, floating music notes, and auto-scaled typography.
  - Remote DJ Board Modal (`dj.html`): Mobile DJs operating from phones or tablets can tap 'Announcement' to bring up a responsive modal overlay and launch custom announcements remotely without physical access to the host laptop.
  - Cancellation Safety: Cancelling or closing the announcement prompt cleanly preserves the previously selected banner without switching displays prematurely.

- **Pre-Show Screen Mode & Stacked Wi-Fi / Sign-Up QR Codes (`MainWindow.xaml`, `MainViewModel.cs`, `MainViewModel.Requests.cs`, `DisplayWindowService.cs`, `SingerDisplayWindow.xaml.cs`, `DisplayViewModel.cs`, `Shared/WelcomeScreen.cs`)**:
  - Added dedicated **Pre-Show Screen** toggle ("📺 Pre-Show Screen") to the main rotation toolbar and projection screen settings.
  - When active, keeps the audience welcome and sign-up screen held on the singer display window even when singers are queued up before showtime.
  - Right ~20% of display width stacks cards for venue Wi-Fi ("📶 1. CONNECT WI-FI" with SSID and password) and song sign-up ("📱 2. SCAN TO SIGN UP" with URL).
  - If no venue Wi-Fi is detected or configured, automatically falls back to showing only the single sign-up QR code card.
  - While Pre-Show Screen is active, individual new singer welcome screens are held in queue and remain hidden; when Pre-Show mode is closed, all queued welcome screens display one by one in exact sequence.
  - Resolved an issue where typing or editing a Wi-Fi password did not update the pre-show screen: `OnWifiPasswordChanged` regenerates `WifiQrCodeImage`, refreshes `WifiPasswordDisplay`, and pushes the updated credentials to `DisplayWindowService.SetWifiInfo(...)` live as the host types (`UpdateSourceTrigger=PropertyChanged`).

- **Line-In Audio Spectrum Visualizer (`LineInSpectrumService.cs`, `Controls/`, `MainWindow.xaml`, `AppSettings.cs`)**:
  - Added real-time line-in audio spectrum analysis powered by shared FFT analyzer for the singer display and DJ banner windows.

## 2026-10-02

- **Dedicated "New Performer Welcome Screen" GroupBox (`MainWindow.xaml`)**:
  - Separated the Welcome Screen configuration controls out of the "DJ & Special Event Banners" groupbox into their own dedicated `New Performer Welcome Screen` groupbox card.
  - Positioned in Column 1 below Screen Rotation, balancing layout density and making welcome screen settings much easier to find.

- **Welcome Screen Style / Model Selector (`MainWindow.xaml`, `MainViewModel.Welcome.cs`, `AppSettings.cs`, `WelcomeScreen.cs`)**:
  - Added a "Welcome Screen Style" dropdown selection to the Display tab.
  - Allows DJs to choose a specific welcome screen design to always display (`Spotlight`, `Neon Night`, `Sunset Stage`, `Confetti Party`, `Disco Rays`, or `Red Velvet Curtain`), or choose `All (Random)` to automatically rotate through all models.
  - Preview button instantly shows the selected style on the audience display.
  - Settings are persisted across sessions.

- **Purple Velvet Curtain Projection View Mode (`SingerDisplayWindow.xaml(.cs)`, `MainViewModel.cs`, `MainWindow.xaml`)**:
  - Added a 12th projection view mode inspired by the "Welcome to our new performer" `RedCurtain` design, featuring a full-screen royal purple velvet theater curtain with procedural drapery folds and soft specular highlights (`VelvetCurtainEffect`).
  - Screen perimeter lined with warm champagne-gold bulbs (`#FFE08A`) and amber halo (`#FFC400`) pulsing in a 700ms 2-phase theatrical alternation (`CurtainMarqueeBulbChase`).
  - Floating theatrical stage typography with deep drop shadows directly on the velvet without framing boxes.
  - Integrated into settings view mode dropdowns, help documentation, and auto-rotate projection sequence.

## 2026-09-29

- **Consolidated Banners Folder (`AppPaths.cs`, `KSRotation.csproj`)**:
  - Banners now live directly under `Banners/` (`DJBanners`, `EventBanners`, `Announcements`) instead of `Banners/KSRotation/`. Move existing DJ banners and announcements into the new folders on installed copies.

## 2026-09-27

- **DJ Banner Target Monitor Overscan Warning Tooltip (`MainWindow.xaml`)**:
  - Added a help icon (``) and warning tooltip next to the Target Monitor combo box in the DJ Banner settings card.
  - Warns DJs that older TVs may crop and zoom into the banner due to hardware overscan, guiding them to disable overscan or choose *Just Scan*, *Screen Fit*, or *1:1 Pixel Mapping* in TV picture settings.

## 2026-09-25

- **Concurrent Banner File Generation (`Shared/DjBannerFileManager.cs`)**:
  - Added synchronization lock (`_bannerInitLock`) around `EnsureStandardEventBanners` with fallback handling to prevent file contention when multiple components initialize event banners concurrently.
- **Reference Cleanup (`KSRotation.csproj`)**:
  - Cleaned up duplicate project references.

## 2026-09-24

- **Shared Projection Effects (`Shared/ProjectionEffects.cs`)**: All themed-view canvas effects now live in one file shared with Lyracist; `SingerDisplayWindow.xaml.cs` went from ~2,600 to ~1,100 lines. The Star Wars crawl starfield switched to the optimized version (no per-star/per-galaxy blur, 100-300 stars).
- **Display Window Leak Fix**: Closing the projection display now stops every view's animations; the film strip and slot reels previously kept the closed window alive and rendering every frame.
- **Reduced Projection Effects Setting** (Screen Rotation settings): fewer particles and no per-element glow effects for slower PCs; saved in settings.
- **View Crossfade**: switching projection views fades out the old view and fades in the new one; view-change requests collapse into a single rebuild.
- **Auto-Rotation Empty Queue**: skips the Star Wars Crawl while no one is queued and moves on immediately if the queue empties while it's showing.
- **Casino Slot Reels Lucky 7s**: the anchor singer lands 7️⃣ with a "LUCKY 7s! ⚓ NEW ROUND ⚓" banner; others land 💎 with "JACKPOT!".

- **Projection Screen Performance (`SingerDisplayWindow.xaml(.cs)`, `DisplayViewModel.cs`, `MainViewModel.cs`)**:
  - A rotation update used to rebuild the ticker, Star Wars crawl, and film strip 20+ times (once per view-model property change and `NextSingers` event); these now coalesce into one refresh after the update, and the ticker/crawl only restart when their text actually changes (no more ticker jumping back to the start).
  - `NextSingers` is diffed by value instead of cleared and refilled, so unchanged queues no longer regenerate item templates in all eight themed panels.
  - Display updates are skipped mid-reorder (`_isFinishingSong`); each such operation pushes one final update itself.
  - Vegas Marquee bulbs use one frozen glow gradient instead of ~100 `DropShadowEffect` shaders.
  - Film strip and slot reel strips use `CacheMode="BitmapCache"`.
  - Synthwave grid lines animate via render transforms (no per-frame layout) with shared frozen brushes/glows.
  - Current-singer avatar: SQLite lookup cached for one minute; decoded/downloaded images cached (no more re-decoding or re-downloading Gravatars on every update).

## 2026-09-23

- **Casino Slot Reels Rework (`SingerDisplayWindow.xaml(.cs)`)**:
  - Fixed reel symbols rendering as slivers (reel strips switched from `StackPanel` to `Canvas`).
  - Reels spin together at a readable speed, stop left-to-right, and land all three 💎 on the payline; symbols are individually tinted.
  - Added a sparkle/shockwave jackpot celebration, clipped to the reel row.
  - Resized the Next Spins ticket chips and added a DJ sponsor banner box that reuses the Jumbotron banner selection.

## 2026-09-21

- **Active Singer Count on Vegas Billboard & Vinyl Record Banners (`SingerDisplayWindow.xaml`, `DisplayViewModel.cs`)**:
  - Added glowing gold badge pills (`🎤 {ActiveSingerCountText}`) displaying the count of active singers in the current rotation on the Broadway / Vegas Marquee and Vinyl Turntable projection screens.
  - Positioned in both the top header (alongside now-spinning and venue marquee headers) and beside the "UP NEXT" / "ON DECK" section banners.
  - Dynamically calculates active non-music singers (`!s.IsMusic`), handles singular/plural phrasing (`1 Singer in Rotation` vs `{N} Singers in Rotation`), and respects Last Round mode.
- **Dynamic Round Completion Estimation & Full Round Duration Notice (`dj.html`, `MainWindow.xaml`, `MainViewModel.cs`, `KSRotation.Maui`)**:
  - Added live estimation of remaining round time, performer count, projected completion clock time, and full round duration.
  - **Remote DJ Portal (`dj.html`)**: Integrated a styled, color-accented status banner directly below the Last Round banner displaying performers left in round, remaining minutes, projected finish time (e.g. `ends ~11:42 PM`), and full round duration. Automatically refreshes every 15 seconds and recalculates immediately upon queue additions, reorders, or status changes.
  - **Desktop App (`MainWindow.xaml`)**: Added a styled badge (`⏱️ {Binding RoundEstimateNoticeText}`) directly beside the active singer count in the Rotation tab header toolbar.
  - **Mobile Tablet App (`KSRotation.Maui/MainPage.xaml`)**: Added a pinned estimation banner above the singer rotation list on Android and Windows tablets.
  - **End-of-Night Planning**: Enables the DJ to immediately determine if there is sufficient time remaining for another full rotation before venue closing or last call.
  - **Smart Calculation**: Respects circular rotation to the round anchor (`IsRotationStart`), handles Last Round mode (`!HasSungInLastRound`), and excludes paused, skipped, inactive performers, and filler music tracks.
  - **Venue Info API**: Added `defaultSongLengthMinutes` to `/api/info` response.

## 2026-09-18

- **Special Singer (One-Time Performance) Lifecycle (`MainWindow.xaml`, `MainViewModel.cs`, `KSRotation.Maui`, Web Portals)**:
  - Added ability to add one-time guest/special singers who perform once and are automatically marked inactive upon song conclusion.
  - **Instant Top of Queue & Current Performer**: When added or toggled as Special, the singer is immediately placed at the top of the queue (index 0) and promoted to Current singer, accommodating spur-of-the-moment guest performances.
  - **Displaced Singer Preservation**: Preserves the previous current/next singer as 'Up Next' so that rotation resumes with them seamlessly after the special singer finishes.
  - While singing, the performer displays a `⭐ SPECIAL` badge on the DJ screen, detached display, MAUI app, and web portals.
  - Added "Add Special" button, context menu item `⭐ Mark as Special (One-Time)`, and violet `⭐ SPECIAL` badge in `MainWindow.xaml`.
  - Added Special checkbox in `AddSingerOverlay` and `EditSingerOverlay`, `⭐` toggle button in row actions, and badges in `MainPage.xaml` and `BillboardView.xaml` (KSRotation.Maui).
  - Updated remote DJ portal (`dj.html`) with special checkbox in Add modal and `⭐ Special` toggle button in performer cards.
  - Rotation immediately resumes with the displaced on-deck performer or next sequential singer once the special singer finishes.
  - Anchor selection ignores special performers to prevent transitory singers from anchoring rounds.

- **Singer Skip Round-Scoped Rotation Bypass (`MainWindow.xaml`, `MainViewModel.cs`, `MainViewModel.Requests.cs`, `KSRotation.Maui`, Web Portals)**:
  - Added single-round Singer Skip capability to bypass performers temporarily stepping away without losing rotation order.
  - Added `ToggleSkipSingerCommand` to `MainViewModel` and wired context menu and list item buttons in `MainWindow.xaml`.
  - Added Skip/Unskip action button, amber `SKIP` badge, and row opacity triggers in `KSRotation.Maui` (`MainPage.xaml`, `MainPage.xaml.cs`, `BillboardView.xaml`).
  - Added `toggle-skip` action to web request handler in `MainViewModel.Requests.cs`.
  - Added visual amber `⏭ SKIP` badges, row styling, and controls to Remote DJ board (`dj.html`), Billboard (`billboard.html`), Kiosk (`kiosk.html`), and Patron Request Portal (`PatronPortal.html`).
  - Implemented automatic clearing of `IsSkipped` upon round completion when reaching/crossing the rotation anchor (`IsRotationStart`).

## 2026-09-08

- **User Search Input Sizing & Usability Fix (`MainWindow.xaml`, `MainViewModel.Users.cs`)**:
  - Replaced the Material Design outlined text box style on the **Users** tab (which had an incompatible 56px minimum height and 16px internal vertical padding that squeezed user input down into an unusable thin slit when placed inside constrained containers on 1920x1080 laptops) with a responsive, modern custom ControlTemplate.
  - Implemented 36px height, vertically centered text alignment, theme-adaptive stroke and background brushes, 6px corner radii, an integrated magnifying glass icon, and an interactive clear button (`✕`) bound to `ClearUserSearchCommand` that activates whenever text is entered.
- **Compiler Cleanliness (`DisplayViewModel.cs`)**:
  - Removed erroneous `private` modifiers from `partial void OnHasDesignatedCurrentSingerChanged` and `partial void OnIsCurrentMusicChanged` to resolve `CS8799` partial method accessibility mismatches.

## 2026-09-04

- Fixed low-contrast (dark-on-dark) label text on the **Trivia** and **Trivia Settings** pages when running in Dark mode. Labels such as "Questions Per Game", the question timer fields (Time/Warn/Fade/Reveal), scoring fields (Base Points, Wrong Answer Deduction, tiered option percentages), venue/host display, Pre-Game Welcome Banner, and the Wi-Fi/network fields (Port, SSID, Password) were bound to `MaterialDesignBody`, which was not resolving to the Dark-mode override for plain `TextBlock` elements (confirmed live in the running app — the same labels rendered correctly once switched).
  - Switched 21 `TextBlock` `Foreground` bindings in `MainWindow.xaml` from `{DynamicResource MaterialDesignBody}` to `{DynamicResource MaterialDesignBodyLight}`, which was already rendering correctly in Dark mode elsewhere in the app (e.g. "GAME MASTER CONTROLS", "LIVE QUESTION CONTROLS" headers).
  - Left `CheckBox` content bindings using `MaterialDesignBody` untouched — those were already rendering correctly.
  - This is theme-aware (keeps following Light/Dark/System selection) rather than a hardcoded color.
  - Note: this same `MaterialDesignBody`-on-`TextBlock` pattern likely exists on other tabs (Rotation, Settings, Display) too, since it's the same underlying app resource — not changed here since this pass was scoped to the Trivia pages.
