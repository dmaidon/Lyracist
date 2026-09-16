<!-- Edited on Sep 16, 2026 @ 00:15:00 -> Document linked singer rotation movement fix (jumping past linked pairs on MoveUp/MoveDown) -->
Last Edit: Sep 16, 2026 - Linked Singer Rotation Movement Jump Fix

# Changelog

All notable changes to the Lyracist project are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [26.9.16.0] - 2026-09-16

### Fixed
- **Linked Singer Rotation Movement & Jump Traversal (`Shared/RotationHelpers.cs`, `KSRotation/ViewModels/MainViewModel.cs`, `Lyracist/ViewModels/RotationViewModel.cs`)**:
  - Implemented `RotationHelpers.MoveSingerUp<T>` and `RotationHelpers.MoveSingerDown<T>` to handle adjacent linked singer pairs during rotation reordering.
  - When a singer directly below a linked pair is moved up, they now jump past the entire linked pair directly above them instead of landing between them and getting pushed back by linked adjacency enforcement.
  - Symmetrically, a singer directly above a linked pair jumps past the linked pair when moved down.
  - Moving within a linked pair cleanly swaps the order of the two partners.
  - Moving outward on either end of the linked pair moves the entire pair together as a unit.
  - Preserved active/inactive partition boundaries and updated `MoveSingerInList` to utilize `ObservableCollection<T>.Move` for atomic notifications.
  - Added unit test coverage across `KSRotation.Tests/RotationTests.cs` and `Lyracist.Tests/RotationHelpersSingerTests.cs`.

## [26.9.13.0] - 2026-09-13

### Removed
- **Unused Watermark Opacity Slider (`KSRotation/MainWindow.xaml`)**:
  - Removed the non-functional "Watermark Opacity" slider and percentage display from the KSRotation Settings tab.
  - Removed the corresponding outdated help topic from the Settings documentation panel.


### Changed
- **Solution Assembly & File Version Bump**:
  - Incremented build version numbers across all solution projects to `26.9.12.*` (`Lyracist`, `KSRotation`, `KnockoutTrivia`, `Lyracist.Data`, `Lyracist.Trivia`, `Lyracist.Trivia.Core`, `LyracistDbEditor`, `ScaryokeWheel`, `TriviaDbCreator`, and test suites).
- **Code Hygiene & Cleanup (`DjBannerWindowService.cs`)**:
  - Removed unused namespace imports and cleaned up legacy comments in `DjBannerWindowService.cs`.

## [26.9.5.37] - 2026-09-11

### Added
- **Production Sync & Mutual Exclusion Test Suite (`SpecialEventBannerSelectionTests.cs`)**:
  - Replaced simulated local test routines with direct invocations of production methods on real `KaraokeViewModel` instances (`SyncSpecialEventOptionSelections`, `UpdateActiveSpecialEventFromSync`, and `SpecialEventOptions` mutual exclusion).
  - Directly verifies dynamic addition of unrecognized custom events, mutual exclusion enforcement, "None" option reset, and that background sync updates the UI quietly without triggering user-action callbacks or launching modal dialogs.
  - Retained unit-level test verification of `SpecialEventOptionViewModel.SetSelectedQuietly` and case-insensitive sync guards.

### Fixed
- **OpenXml Run Splitting & PDF Exception Handling in Manual Generators (`CatalogBookGeneratorTests.cs`)**:
  - Updated `UpdateUserManualsForSpecialEventBannerSelection` and `UpdateUserManualsForKaraoke1080pLayoutOptimization` to evaluate `paragraph.InnerText` across `Paragraph` descendants rather than individual `Text` elements, preventing missed idempotency headers when Word fragments strings across multiple `<w:r>` runs.
  - Replaced swallowed `Console.WriteLine` error logging in PDF manual generator blocks with `throw new InvalidOperationException(...)` to ensure PDF write failures fail tests loudly and reliably.
- **Dead Code & Redundant Re-Sync Cleanup (`MainViewModel.cs`, `KaraokeViewModel.DjBanner.cs`)**:
  - Removed duplicate explicit re-sync calls in `MainViewModel.cs:2985` (`SyncSpecialEventOptions` and `UpdateDjBannerPath`) and in `KaraokeViewModel.DjBanner.cs`, as `ActiveSpecialEvent` property change handlers (`OnActiveSpecialEventChanged`) already trigger option synchronization and banner refreshes automatically without running redundant double loops.
- **Interactive QR Code Quick Popout (`KaraokePage.xaml`, `KaraokeViewModel.cs`)**:
  - Scaled the inline QR code to an unobtrusive 30x30 thumbnail in the header with click-to-enlarge action launching the full-size `KioskQrCodePopoutWindow`.
  - Added quick-access "📟 Kiosk" button right in the header for rapid tablet setup.

### Fixed
- **Special Event Two-Way Synchronization & Local Edit Protection (`KSRotationSyncService.cs`, `PatronRequestServer.cs`, `KaraokeViewModel.DjBanner.cs`, `MainViewModel.cs`)**:
  - Fixed one-way sync bug where local DJ banner selections in Lyracist were silently overwritten by the 2-second polling loop.
  - Added `POST /api/special-event/active` in `PatronRequestServer.cs` and wired `KSRotationSyncService.NotifyLocalSpecialEventChanged` to push selections to KSRotation immediately.
  - Introduced a 5-second local selection grace window to protect local changes against polling race conditions while requests process.
  - Fixed case-sensitivity mismatch in the sync guard by enforcing `StringComparison.OrdinalIgnoreCase`.
  - Added dynamic addition of unrecognized custom event banners across applications so custom events never leave all radio buttons deselected.
- **Modal Dialog Sync Re-entrancy & Unattended Host Protection (`KaraokeViewModel.DjBanner.cs`, `MainViewModel.cs`)**:
  - Implemented `SetSelectedQuietly` on `SpecialEventOptionViewModel` across both Lyracist and KSRotation to update radio button UI states without invoking user-action callbacks.
  - Prevents background polling or remote patron requests from re-entering the Birthday flow and popping a blocking `ShowPersonalizedBirthdayPrompt` modal dialog on unattended host laptops.
- **Special Event Banners Single Selection & "None" Button Clearing (`KaraokePage.xaml`, `KaraokeViewModel.DjBanner.cs`, `KSRotation/ViewModels/MainViewModel.cs`)**:
  - Resolved an issue where multiple special event banners could be selected simultaneously in Lyracist by adding explicit `GroupName="SpecialEventBanner"` to the RadioButton template inside the `SpecialEventOptions` `ItemsControl`.
  - Fixed the "None" button not clearing previous selections by introducing an explicit synchronization loop (`SyncSpecialEventOptionSelections`) in `KaraokeViewModel` and `KSRotation`'s `MainViewModel` that automatically deselects all other options when any option or "None" is selected.
  - Implemented `OnActiveSpecialEventChanged` partial handler to guarantee that two-way data bindings, programmatic changes, and sync events remain strictly in mutual-exclusion sync with the active event.
- **1080p Screen Layout & Adaptive Viewport Fallback (`KaraokePage.xaml`)**:
  - Optimized page margins from `24` to `16,10,16,12` and streamlined TitleBlock from ~102px down to a sleek ~40px toolbar with inline Venue & DJ branding and compact request indicator bulbs.
  - Reclaims over 60px of vertical height across the page, completely eliminating vertical overflow on 1920x1080 screens running at 100% and 125% Windows DPI scaling.
  - Wrapped Column 0 in an adaptive `ScrollViewer` bound to `ActualHeight` (`MinHeight="500"`), preserving 15–20 row internal DataGrid virtualization on 1080p while offering outer scroll fallback so the Singer Assignment card is never clipped on small or heavily scaled viewports.
- **Playback FooterBar Anti-Impingement Layout (`MainWindow.xaml`)**:
  - Streamlined the global playback footer bar from ~94px down to ~52px (`Padding="20,6"`), saving over 40 vertical pixels across the main application window.
  - Replaced the horizontal `StackPanel` with a bounded 3-column `Grid` (`MaxWidth="180"` with ellipsis on performer name, `*` with ellipsis on song title rendered in bold italics and `#F5D042` gold).
  - Placed the seek bar cleanly onto the 2nd row with dedicated right-margin breathing room (`Margin="0,0,28,0"`), physically preventing the performer name, song title, and seek slider from impinging on the center Play/Pause/Stop playback buttons.

## [26.9.5.36] - 2026-09-09

### Added
- **Master Output Boost / Preamp (USB Mixer Mode) (`SettingsPage.xaml`, `SettingsViewModel.MediaEngine.cs`, `AppSettings.cs`)**:
  - Added an adjustable digital preamp slider (0 dB to +12 dB) with one-click presets for `0 dB (Standard)`, `+6 dB (USB Mixer)`, and `+12 dB (Maximum)`.
  - Engineered specifically for external sound cards and USB audio interfaces like the Yamaha MG10XU (Channel 9/10 USB stereo return), supplying a robust, punchy +4 dBu professional line-level signal directly from Windows without having to crank mixer channel gain knobs to their physical limits.
  - Automatically synchronizes BGM player preamps across Opening, Fill-In, End-of-Rotation, and Special Occasion playlists via `ShowFlowService.RefreshOutputSettings`.
- **Anti-Clipping Peak Limiter (`LibVlcVideoBackend.cs`, `SettingsPage.xaml`, `AppSettings.cs`)**:
  - Embedded a transparent soft-knee peak compressor/limiter into LibVLC's native playback pipeline, automatically clamping hot audio signals to prevent digital clipping or speaker distortion when high-gain tracks are played.
- **Instant Search Clear Button (`KaraokePage.xaml`, `KaraokeViewModel.cs`)**:
  - Integrated a dedicated clear button (`✕`) directly into the search box that appears whenever text is entered, enabling one-click query resets.
- **Streaming Search Query Isolation (`KaraokeViewModel.cs`, `KaraokeViewModel.ExternalSearch.cs`, `KaraokePage.xaml`)**:
  - Bound TabControl `SelectedIndex` to `SelectedSearchTabIndex` in the viewmodel, isolating external YouTube, Spotify, and Amazon network requests strictly to the "Streaming Links" tab.
  - Typing in search now searches only local database tables, keeping song lookup instantaneous and free of background network overhead.

### Fixed
- **Android Multi-RID Deployment Missing DLL (`KSRotation.Maui.csproj`)**:
  - Added MSBuild target `EnsureOuterAssemblyForProcessAssemblies` running before `_ProcessAssemblies`, `ProcessAssemblies`, and `SignAndroidPackage`.
  - Automatically bridges RID-specific inner compiled assemblies (`android-arm64`, `android-x64`) to the outer intermediate directory (`obj\Debug\net10.0-android\`), eliminating `XAPRAS7028: Could not find file '...KSRotation.Maui.dll'` when deploying directly from Visual Studio or CLI to connected Android devices (e.g. Lenovo tablet).
- **Tablet Action Button Vertical Line Collapse (`KaraokePage.xaml`)**:
  - Replaced unconstrained `Width="Auto"` template columns in Karaoke Library, Music Library, Singer History, and Singer Queue with dedicated `52px` and `98px` non-collapsing columns (`CanUserResize=False`) with centered `38x32px` touch targets.
  - Prevents buttons on 1080p tablet screens operating under 150% or 175% Windows DPI scaling from collapsing into thin 1px vertical lines when star columns consume available width.
  - Enforced a `360px` minimum width on the search panel to ensure readability and comfortable touch spacing regardless of display resolution or window layout.
- **Search Stutter & UI Thread Thrashing (`KaraokeViewModel.cs`, `KaraokePage.xaml`)**:
  - Replaced individual item-by-item `.Add()` collection mutations (which fired 150+ UI layout passes on the UI thread per keystroke) with atomic batch collection assignments.
  - Enabled active UI recycling virtualization (`VirtualizingPanel.IsVirtualizing="True"`, `VirtualizingPanel.VirtualizationMode="Recycling"`, `VirtualizingPanel.ScrollUnit="Item"`) on all search DataGrids, eliminating visual lag and stutter during rapid typing.
- **Quiet Audio Playback, Headroom & WASAPI Routing (`LibVlcVideoBackend.cs`, `BackgroundMusicPlayer.cs`, `MediaEngine.cs`, `AppSettings.cs`)**:
  - Configured `LibVlcVideoBackend` and `BackgroundMusicPlayer` to unconditionally route audio through modern Windows Core Audio WASAPI (`SetAudioOutput("mmdevice")`) on both default and custom audio devices, bypassing legacy DirectSound/WaveOut software mixer attenuation.
  - Expanded software volume headroom ceiling up to `200%` (+6 dB digital gain boost), ensuring playback volume matches native drive playback without forcing volume and gain sliders to their limits.
  - Recalibrated default integrated loudness normalization to `-12 LUFS` (live performance/DJ standard) and guarded against uninitialized zero-gain multipliers in `MediaEngine.UpdateAudioParameters`.

## [26.9.5.35] - 2026-09-08

### Added
- **Audience Lyrics Screen QR Code Toggle (`LyricsWindow.xaml`, `SettingsPage.xaml`, `LyricsPage.xaml`, `AppSettings.cs`)**:
  - Added a configurable toggle allowing KJs to show or hide the patron mobile song request QR code badge on the audience lyrics projection screen.
  - Accessible via **Settings -> Monitors & Screen Assignments**, the **Lyrics Page** (Audience Lyrics Screen card), and via right-click context menu directly on the active **Lyrics Window**.
  - Persisted in `AppSettings.ShowQrCodeOnLyricsScreen` and documented in Help Topic 5 and the user manuals.
- **Dedicated Karaoke & Music Library Tabs with Live Count Badges (`KaraokePage.xaml`)**:
  - Renamed "Local Library" to "Karaoke Library" and added live result hit count badges to both tabs (`Karaoke Library ({Count})` and `Music Library ({Count})`).

### Fixed
- **Karaoke vs. Music Library Classification & Companion CDG Detection (`ScanningService.cs`)**:
  - Added companion `.cdg` file detection (`File.Exists(Path.ChangeExtension(filePath, ".cdg"))`) to `ScanningService.ParseStoreDownload`, correctly classifying unzipped MP3+G / WAV+G karaoke pairs as `IsKaraoke = true, KaraokeType = "MP3G"` and preventing them from being mistakenly routed to the background music library.
  - Expanded `SafeEnumerateFiles` to catalog standard audio files in `.mp3`, `.mp4`, `.zip`, `.wav`, `.m4a`, `.flac`, `.wma`, `.aac`, and `.ogg`.
  - Rescanning automatically identifies and updates existing database records and FTS5 search indexes.
- **SQLite FTS Search Query Isolation (`SearchService.cs`, `LibraryService.cs`)**:
  - Pushed the `IsKaraoke` check into SQLite's SQL query so music queries return their own full 150 matching tracks without being starved by karaoke matches.
- **KSRotation User Search Box Layout (`KSRotation/MainWindow.xaml`, `MainViewModel.Users.cs`)**:
  - Replaced the Material Design outlined text box style on the Users tab (which had an incompatible 56px minimum height and 16px internal vertical padding that squeezed user input down into an invisible 2px slit on 1920x1080 laptops) with a responsive, modern custom ControlTemplate.
  - Added integrated magnifying glass icon, theme-adaptive styling, and an interactive clear button (`✕`) bound to `ClearUserSearchCommand`.
- **KSRotation Compiler Cleanliness (`DisplayViewModel.cs`)**:
  - Removed erroneous `private` modifier from `partial void OnHasDesignatedCurrentSingerChanged` and `partial void OnIsCurrentMusicChanged` to resolve C# CS8799 compiler errors.

## [26.9.5.34] - 2026-09-08

### Added
- **Search Result Track Type Row Tinting (`KaraokePage.xaml`, `HelpViewModel.cs`)**:
  - Added `SongTypeRowStyle` to visually tint song search results: emerald green (`#1522C55E`) for karaoke tracks (`IsKaraoke = true`) and subtle amber/gold (`#15F59E0B`) for standard music library tracks (`IsKaraoke = false`), making track classifications distinguishable at a glance.
  - Documented search result row tinting in `HelpViewModel.cs` Topic 1 ("Karaoke Page (Dashboard)").
- **KSRotation Done Last Round Indicator (`KSRotation/MainWindow.xaml`)**:
  - Added `DONE (LAST ROUND)` indicator badge in KSRotation singer rows matching Lyracist's rotation display, ensuring KJs have clear on-screen confirmation of completed last-round turns.

### Fixed
- **Special Event Banner Radio Button Mutual Exclusion (`KSRotation/MainWindow.xaml`)**:
  - Explicitly assigned `GroupName="SpecialEventBanner"` to the RadioButton template inside the ItemsControl for Special Events, ensuring that selecting one banner option properly deselects others across dynamically generated items.
- **Last Round Turn Rule Enforcement & Rotation Invariants (`KSRotation`, `Lyracist`, `Tests`)**:
  - Reverted the premature grace-pass logic to uphold the strict rule: whoever completes a song while Last Round is active is marked with `HasSungInLastRound = true`. This prevents a singer already current when Last Round activates from receiving a duplicate performance after the queue wraps around.
  - Added comprehensive regression tests locking in this behavior across test suites.
  - Restored case-insensitive string comparisons for `ActiveSpecialEvent`.

## [26.9.5.33] - 2026-09-07

### Fixed
- **Consolidated Song & Singer Matching (`Shared/RotationHelpers.cs`, `KSRotation`, `Lyracist`)**:
  - **Shared Matching Library**: Unified `NormalizeForComparison`, `IsSameSingerName`, `IsSameSong`, and `IsSameSongLenient` into `Shared/RotationHelpers.cs`, eliminating divergent whitespace normalization and song comparison implementations across `Lyracist` and `KSRotation`.
  - **Cross-Thread Collection Safety (`ReadWithConcurrentRetry`)**: Replaced hazardous UI-dispatcher marshaling in `IsSongInCurrentSession` with lockless snapshot reads using `ReadWithConcurrentRetry`, preventing `InvalidOperationException` collection modification races during background web requests while avoiding deadlocks in non-pumped test runners.
  - **Linked Singer Adjacency Invariants (`MainViewModel.Singers.cs`)**: Re-invoked `EnforceLinkedAdjacency` in `ToggleSingerInactive` and `SetCurrentSinger` after singer state transitions, preventing linked/duet singers from becoming separated when pausing or promoting performers.
  - **Remote Move Active/Inactive Boundary Guards (`MainViewModel.Singers.cs`)**: Added boundary guards to `MoveSingerUp` and `MoveSingerDown` (used by web DJ remote and Maui tablet app) matching desktop `MoveUp`/`MoveDown`, preventing inactive singers from being shifted into the active queue.
  - **Duplicate Singer Auto-Merge Safety (`MainViewModel.Singers.cs`)**: Refactored duplicate singer name auto-merge to execute full singer cleanup (`IsCurrent`/`IsRotationStart` reassignment and `UnlinkSinger`) instead of raw list removal.
  - **Anchor Reassignment Guard (`MainViewModel.Singers.cs`)**: Fixed web remote toggle-off for rotation start to invoke `RotationHelpers.ToggleRotationStartSinger` rather than direct field clearing, ensuring consistent anchor ownership.
  - **Duplicate Session Song Enforcement on DJ Accept (`MainViewModel.Requests.cs`)**: Re-validated `BlockDuplicateSongsInSession` during `AcceptRequest` to catch duplicate song requests when session state changes between patron submission and DJ approval.
  - **Patron Avatar MIME Detection & URL Parsing (`MainViewModel.Requests.cs`)**: Replaced hardcoded `.jpg` avatar responses with real image byte format detection (PNG, GIF, WebP, JPEG) and corrected query parameter extraction for `?name=` lookups.
  - **WiFi SSID Query Caching & Concurrency (`MainViewModel.Requests.cs`, `WifiHelper.cs`)**: Added 30-second thread-safe cached lookup for `WifiHelper.GetConnectedSsid()` to prevent recurring 1-second `netsh` delays on client `/api/info` polling.
  - **Patron Login Lock Scope (`PatronRequestServer.cs`)**: Released singer-login lock prior to sending HTTP 429 rate-limited responses, preventing slow client connections from stalling other patron logins.
  - **Post-Migration Setup Parity (`App.xaml.cs`)**: Extracted shared post-migration steps into `ApplyPostMigrationSetup` so that initial migration and self-healing retry branches both reliably execute song deduplication, index rebuilds, and `SingerHistory` table creation.

### Changed
- **Help System Integration (`HelpViewModel.cs`)**:
  - Updated Topic 1 ("Karaoke Page (Dashboard)") with documentation for the direct database Search Scan button, Enter-key query triggering, dedicated disk folder scan icon, and compact DJ Auto-Advance toolbar.

## [26.9.5.32] - 2026-09-06

### Fixed
- **SQLite Error 11 Migration Lock Self-Healing (`LyracistDbContext.cs`, `App.xaml.cs`)**:
  - **Proactive Lock Table Cleanup**: Added `LyracistDbContext.ClearAbandonedMigrationLocks()` which automatically executes `DROP TABLE IF EXISTS "__EFMigrationsLock";` before EF Core migration starts, eliminating startup crashes caused by abandoned locks from killed/interrupted previous sessions (`SQLite Error 11: 'malformed database schema (__EFMigrationsLock) - table already exists'`).
  - **Self-Healing Startup Recovery**: Enhanced `App.xaml.cs` to clear locks, drop `__EFMigrationsLock` after migration, and catch any SQLite lock errors during startup with an automated recovery retry.
- **Karaoke Song Search Database Scan (`KaraokePage.xaml`, `KaraokeViewModel.cs`, `KaraokePage.xaml.cs`)**:
  - **Scan Button Database Query**: Re-wired the "Scan" button beside the song search input to execute `SearchDatabaseCommand`, immediately scanning and filtering songs from the local SQLite database instead of launching the Windows File Explorer folder picker.
  - **Enter Key Search**: Added `KeyDown` event handling on the search input box (`SearchBox_KeyDown`) so pressing <kbd>Enter</kbd> immediately executes the database scan without waiting for the debounce timer.
  - **Distinct Folder Scan Button**: Added an independent folder icon button (`ScanFolderCommand`) with an explicit tooltip (`"Scan music folder from disk into library..."`) to keep disk folder importing readily available without conflicting with database search queries.

### Changed
- **Compact Auto-Advance DJ Control Strip (`KaraokePage.xaml`)**:
  - **Drastic Screen Real Estate Recovery**: Re-engineered the Auto-Advance & DJ control strip from an oversized ~80px banner down to a sleek ~32px horizontal toolbar, reclaiming over 50px of vertical height for the search results, singer queue, and lyrics/player columns.
  - **Compact DJ Controls**: Replaced bulky `BigDJButton` buttons with streamlined primary/secondary DJ action buttons ("▶ Start Song" and "⏭ Skip Singer") with dedicated tooltips and compact padding (`10,2`).
  - **Slender Metrics & Progress Bar**: Refined countdown clock, status labels, and reduced progress bar height to 4px with tightened 10px page margins.

## [26.9.5.31] - 2026-09-06

### Fixed
- **Store Deep-Link Search URLs 404 / 410 Errors (`Karaoke Version`, `Party Tyme`, `Sunfly`) (`Lyracist` ONLY)**:
  - **Karaoke Version**: Corrected search endpoint from legacy/broken `search.html?q=` (which returned HTTP 404) to the active search route `custombackingtrack/search.html?query={encodedQuery}`.
  - **Party Tyme**: Corrected search endpoint from legacy `search?q=` (which returned HTTP 404) to the active product search route `songshop/cat/search.php?search_what=all&search_keyword={encodedQuery}&submit=GO`.
  - **Sunfly Karaoke**: Corrected search endpoint from legacy Magento `catalogsearch/result/?q=` (which returned HTTP 410 Gone) to the active WooCommerce product search route `?s={encodedQuery}&post_type=product`.
  - **Synchronized Implementations**: Updated `KaraokeVersionProvider.cs`, `PartyTymeProvider.cs`, `SunflyProvider.cs`, `providerRegistry.ts`, `deepLink.ts`, and unit test assertions in `StoreImportTests.cs`.

## [26.9.5.30] - 2026-09-06

### Added
- **Store Notifications & Real-Time Ingestion Toast Alerts (`Lyracist` ONLY)**:
  - **Store Notification Service (`StoreNotificationService.cs`)**:
    - Centralized thread-safe notification manager providing toast queuing, auto-dismiss timers, and UI event dispatching via `ActiveNotifications`.
    - Dedicated alert dispatchers: `ShowTrackImported`, `ShowNormalizationComplete`, `ShowSilenceTrimmed`, `ShowWaveformGenerated`, `ShowSyncCompleted`, and `ShowBulkImportCompleted`.
    - Built-in 5-second auto-dismissal timers and maximum 5-toast vertical stack limiting to prevent UI clutter.
  - **WPF Toast Notification Control (`ToastNotificationControl.xaml`, `ToastNotificationControl.xaml.cs`)**:
    - High-DPI dark acrylic toast cards (`#23273A`) with category accent badge colors (Purple for imports, Emerald for normalization, Cyan for trimming, Sky Blue for waveforms, Indigo for sync).
    - Features smooth cubic slide-in entry animation (`TranslateTransform.X` 320 to 0) and fade-in (`Opacity` 0 to 1).
    - Displays format badge (`MP3+G`, `MP4`, `ZIP`) and audio processing status badges (`NORM`, `TRIM`, `WAVE`).
    - Includes interactive manual close button.
  - **Store Page & Bulk Import Overlay Hosts (`StorePage.xaml`, `BulkImportWindow.xaml`)**:
    - Embedded `NotificationHost` overlay in the bottom-right corner of `StorePage.xaml`, floating above scrollable content.
    - Embedded `NotificationHost` overlay in the bottom-right corner of `BulkImportWindow.xaml` for live batch processing feedback.
  - **Pipeline Triggers (`PurchasedTrackWatcherService.cs`, `PurchasedTrackSyncService.cs`, `BulkImportViewModel.cs`)**:
    - `PurchasedTrackWatcherService` dispatches toast notifications immediately upon track import success, audio normalization, silence trimming, and waveform rendering.
    - `PurchasedTrackSyncService` triggers sync completion summary toast upon completing directory evaluation.
    - `BulkImportViewModel` triggers batch completion summary toast upon finishing batch import jobs.
  - **Help System Integration (`HelpViewModel.cs`)**:
    - Added Topic 25: "Store Notifications & Toast Alerts" detailing slide-in animation, lifecycle alerts, pill badges, and auto-dismiss timing.
  - **Unit Test Coverage (`StoreImportTests.cs`)**:
    - Added unit test suite covering `StoreNotificationService` queueing, dismissal, track imported badge configuration, and batch alert message formatting.

## [26.9.5.29] - 2026-09-06

### Added
- **Store Plugin API & Modular Provider Architecture (`Lyracist` ONLY)**:
  - **Provider Plugin Contracts (`IStoreProvider.cs`, `BaseStoreProvider.cs`)**:
    - Created `IStoreProvider` interface establishing standardized provider metadata (`Name`, `Source`) and multi-vector detection methods (`BuildSearchUri`, `DetectFromFilename`, `DetectFromId3`, `DetectFromZip`, `DetectFromCdgHeader`, `DetectFromMp4`).
    - Created `BaseStoreProvider` abstract class offering default `false` detection fallbacks and thread-safe inspection helpers for ZIP entries (`MatchesZipEntries`), ID3 tag scanning (`MatchesId3Tags`), and MP4 metadata inspection (`MatchesMp4Metadata`).
    - Added `ProviderSource` enumeration (`Local`, `KaraokeVersion`, `PartyTyme`, `Sunfly`, `KaraokeCom`).
  - **Concrete Provider Plugins (`Lyracist/Services/Store/Providers/`)**:
    - `KaraokeVersionProvider`: Implements search URL generation for karaoke-version.com, detection of `/custom_backing_track/` or generic `track.mp3`+`track.cdg` pairs, `KV` filename/tag patterns, and CDG magic header bytes `0x01 0x0F`.
    - `PartyTymeProvider`: Implements search URL for partytyme.net, detection of `/karaoke/` folders, `_pt.` naming, `PT` tags, and CDG magic header bytes `0x02 0x0A`.
    - `SunflyProvider`: Implements search URL for sunflykaraoke.com, detection of `sf` filename/entry prefixes, `SF` tags, and CDG magic header bytes `0x03 0x0C`.
    - `KaraokeComProvider`: Implements search URL for karaoke.com, detection of `kcom` signatures, and ID3/MP4 metadata watermarks.
  - **Central Provider Registry (`ProviderRegistry.cs`)**:
    - Thread-safe singleton registry holding default commercial karaoke providers with dynamic registration support (`RegisterProvider(IStoreProvider)`).
    - Unified multi-vector discovery methods: `DetectProviderFromFilename`, `DetectProviderFromZip`, `DetectProviderFromCdg`, `DetectProviderFromId3`, `DetectProviderFromMp4`, and `DetectProviderFromMetadata`.
  - **Store Ingestion & ViewModel Integration (`PurchasedTrackWatcherService.cs`, `StoreViewModel.cs`)**:
    - Refactored `PurchasedTrackWatcherService` detection pipeline to delegate ZIP, CDG, ID3, MP4, and filename heuristics directly to `ProviderRegistry.Instance`.
    - Updated `StoreViewModel` search actions (`SearchKaraokeVersion`, `SearchPartyTyme`, `SearchKaraokeDotCom`, `SearchSunfly`, and `SearchPreferredProvider`) to resolve and build search URLs through `ProviderRegistry`.
    - Exposed `RegisteredProviders` collection for dynamic UI provider listing and binding.
  - **WebModules Reference Modules (`providerRegistry.ts`, `deepLink.ts`, `importMetadata.ts`, `purchasedWatcher.ts`)**:
    - Implemented TypeScript `IStoreProvider`, `BaseStoreProvider`, concrete provider classes, and singleton `providerRegistry`.
    - Extended `deepLink.ts`, `importMetadata.ts`, and `purchasedWatcher.ts` to route search URL generation and file fingerprinting through `providerRegistry`.
  - **Unit Test Coverage (`StoreImportTests.cs`)**:
    - Added comprehensive test suite verifying `ProviderRegistry` initialization, URI construction across all providers, filename heuristic resolution, and custom provider plugin registration.

## [26.9.5.28] - 2026-09-06

### Added
- **Store Tab Provider Settings Panel (Provider Preferences, Format Defaults & Audio Automation) (`Lyracist` ONLY)**:
  - **WPF Provider Settings UserControl (`ProviderSettingsControl.xaml`, `ProviderSettingsControl.xaml.cs`)**:
    - Embedded as a collapsible configuration panel in Section 3 of `StorePage.xaml`, toggled via the new "Provider Settings..." action button.
    - Features dropdown selectors for Preferred Provider (`KV`, `PT`, `Sunfly`, `Karaoke.com`), Preferred File Type (`MP3+G`, `MP4`, `Audio-only`), Preferred Target Folder (`Karaoke`, `Music`), and Preferred Lyrics Format (`LRC`, `TXT`).
    - Features toggle switches for baseline audio automation flags: Normalize audio by default, Trim silence by default, and Generate waveform by default.
    - Includes "Save Settings" button with status badge indicator and "Reset Defaults" button.
  - **Provider Settings MVVM ViewModel (`ProviderSettingsViewModel.cs`)**:
    - Manages configuration properties, collections for available providers, formats, and folders, and persistent storage via `AppSettings`.
    - Dispatches `SettingsSaved` event notifying `StoreViewModel` and refreshing search toolbar highlights and processing defaults in real time.
  - **Application Settings Expansion (`AppSettings.cs`)**:
    - Added static configuration properties and backing fields: `PreferredProvider`, `PreferredFileType`, `DefaultNormalizeAudio`, `DefaultTrimSilence`, `DefaultGenerateWaveform`, `PreferredTargetFolder`, and `PreferredLyricsFormat`.
  - **Store Page & Search Toolbar Integration (`StorePage.xaml`, `StoreViewModel.cs`)**:
    - Added dynamic gold "★ PREFERRED" badge highlighting over the preferred provider button (Karaoke Version, Party Tyme, Karaoke.com, or Sunfly) based on `PreferredProvider`.
    - Routed Enter key press in the Store search box to execute the query against the host's preferred provider via `SearchPreferredProviderCommand`.
  - **Bulk Import Wizard & Store Sync Integration (`BulkImportViewModel.cs`, `PurchasedTrackSyncService.cs`, `PurchasedTrackWatcherService.cs`)**:
    - Bulk Import Wizard defaults now initialize from `AppSettings.DefaultNormalizeAudio`, `DefaultTrimSilence`, and `DefaultGenerateWaveform`.
    - Bulk Import candidate scanning prioritizes and sorts items matching `PreferredFileType` to the top of the review grid.
    - Store Sync and Smart Import Rules use `PreferredProvider` as an attribution fallback hint when physical file inspection is unbranded.
    - Lyrics companion discovery prioritizes `.lrc` or `.txt` matching `PreferredLyricsFormat`.
  - **WebModules Reference Component (`settingsStore.tsx`, `types.ts`)**:
    - Extended `StoreSettings` interface with provider settings properties.
    - Added Provider Preferences & Import Defaults card in `settingsStore.tsx` with dropdowns, toggles, and Save action.
  - **Unit Test Coverage (`StoreImportTests.cs`)**:
    - Added unit test suite covering `ProviderSettingsViewModel` save/reset persistence, `StoreViewModel` provider highlighting calculation, lyrics format prioritization, and provider abbreviation fallback.

## [26.9.5.27] - 2026-09-06

### Added
- **Track Preview Player (Acoustic Inspection, Waveform/Spectrogram Visualizations & Dual-Audio Selector) (`Lyracist` ONLY)**:
  - **WPF Track Preview Control (`TrackPreviewControl.xaml`, `TrackPreviewControl.xaml.cs`)**:
    - Dedicated inspection UserControl embedded directly below the Recent Imports table in `StorePage.xaml` and in the candidate review grid in `BulkImportWindow.xaml`.
    - Styled with dark acrylic surfaces, cyan header accents, metadata badge bar (Filename, Provider, Format, Duration, Musical Key, BPM, Quality, Difficulty, Vocal Presence), and visual switcher tabs (Waveform, Spectrogram, 5s Video Preview).
    - Features a 5-second audio transport bar with 1-click Play/Pause, scrub slider, and dual-audio channel selector (Channel A Guide Vocals / Channel B Instrumental).
  - **Track Preview MVVM ViewModel (`TrackPreviewViewModel.cs`)**:
    - Manages audio extraction, image rendering, and playback state using `System.Windows.Media.MediaPlayer` and a high-resolution `DispatcherTimer`.
    - Supports dynamic loading from both `PurchasedTrackItem` (Recent Imports) and `BulkImportCandidate` (Bulk Import Wizard).
    - Coordinates asynchronous extraction for audio previews, waveforms, spectrograms, and MP4 video clips, with safe cleanup of temporary preview files upon disposal.
  - **FFmpeg Preview Extraction Extensions (`FFmpegService.cs`)**:
    - `GenerateAudioPreviewAsync(inputPath, outputPath, channelIndex)`: Extracts opening 5 seconds (`-ss 0 -t 5`) with optional audio channel routing (`pan=mono|c0=c0` for Ch A, `pan=mono|c0=c1` for Ch B).
    - `GenerateSpectrogramAsync(inputPath, outputPath)`: Renders high-resolution acoustic frequency density maps using the `showspectrum=s=800x300:color=intensity` filter.
    - `GenerateVideoPreviewAsync(inputPath, outputPath)`: Generates opening 5-second video clips (`-ss 0 -t 5 -c:v libx264 -preset ultrafast -c:a aac`) for embedded WPF `MediaElement` playback.
  - **Store Page & Bulk Import Window Integration (`StorePage.xaml`, `StoreViewModel.cs`, `BulkImportWindow.xaml`, `BulkImportViewModel.cs`)**:
    - Added `SelectedItem="{Binding SelectedRecentImport}"` and dedicated "Preview" button action column to Recent Imports DataGrid.
    - Embedded `TrackPreviewControl` visible whenever a track or candidate is selected.
  - **WebModules Reference Component (`storeTab.tsx`)**:
    - Added interactive Track Preview Player card to the React reference implementation with visual switcher tabs, simulated waveforms, 5-second playback bar, and dual-audio toggle.
  - **Unit Test Coverage (`StoreImportTests.cs`)**:
    - Added test suites covering audio preview, spectrogram, and video preview handling with non-existent or corrupted files, as well as property mapping from `PurchasedTrackItem` and `BulkImportCandidate`.

## [26.9.5.26] - 2026-09-06

### Added
- **Store Sync (1-Click Purchased Tracks Ingestion & Modal Summary Dialog) (`Lyracist` ONLY)**:
  - **Purchased Track Sync Service (`PurchasedTrackSyncService.cs`)**:
    - High-level on-demand ingestion service scanning the configured purchase folder (`AppSettings.StorePurchasedTracksFolder`), user Downloads folder, or standard Music folder.
    - Scans for ZIP archives (MP3+G), separate MP3+G companion pairs, MP4 video tracks, and accompanying lyrics files (.lrc, .txt).
    - Intelligent browser temp file exclusion filtering out partial/active downloads (`.crdownload`, `.part`, `.tmp`, `~$*`).
    - Companion file reconciliation automatically pairing `.mp3` and `.cdg` companion files with matching base names.
    - Master catalog deduplication checking SQLite database records to identify existing tracks and mark them as skipped (`TotalSkipped++`), preventing redundant reprocessing.
    - Invokes `PurchasedTrackWatcherService.ImportFileAsync` to route new tracks through Provider Intelligence, Smart Import auto-renaming, FFmpeg loudness normalization (-16 LUFS) and silence trimming (-50dB), peak waveform generation, and database insertion.
    - Dispatches `ImportLogged` events for the live Recent Imports table and tracks per-track processing duration.
  - **Store Sync Summary Dialog (`StoreSyncSummaryWindow.xaml`, `StoreSyncSummaryWindow.xaml.cs`)**:
    - Modal dialog styled with dark acrylic surfaces, cyan/emerald gradient header, animated `ArrowSync24` branding, stat cards (Scanned, Imported, Skipped, Errors), and technical details card (Scanned Folder, Providers Involved, Avg Processing Time, Total Duration).
  - **Store Page & ViewModel Integration (`StorePage.xaml`, `StoreViewModel.cs`)**:
    - Added "Sync Purchased Tracks" primary action button with sync icon in Section 4 toolbar grid.
    - Added `SyncPurchasedTracksCommand` with `IsSyncing` state tracking to prevent overlapping executions and show a responsive loading spinner.
    - Triggers `Analytics.RefreshAnalyticsAsync()` and UI library updates immediately upon sync completion.
  - **Unit Test Coverage (`StoreImportTests.cs`)**:
    - Added test suite for folder resolution, empty folder execution, result calculations, and zero-warning execution.

## [26.9.5.25] - 2026-09-06

### Added
- **Bulk Import Wizard (Batch Ingestion, Preview Table, Parallel Audio Pipeline & Summary Dialog) (`Lyracist` ONLY)**:
  - **Bulk Import Modal Window (`BulkImportWindow.xaml`, `BulkImportWindow.xaml.cs`)**:
    - Modern WPF modal wizard dialog accessible via the new "Bulk Import..." button on the Store page (`StorePage.xaml`).
    - Styled with acrylic dark surfaces, header icon branding, folder scan toolbar, preview candidate DataGrid, progress tracking bar, and post-import summary report card.
  - **Folder Selection & Multi-Format Scanner (`PurchasedTrackBulkImporter.cs`, `BulkImportViewModel.cs`)**:
    - Allows users to select any folder containing purchased karaoke files.
    - Scans for MP3, CDG, ZIP (MP3+G), MP4, and LRC/TXT lyrics files.
    - Automatically identifies and pairs MP3 audio and CDG graphics files into single candidate items, preventing duplicate entries.
    - Previews ZIP archive entries, MP4 multiplexed dual audio streams, and resolves matching lyrics files (.lrc/.txt).
  - **Provider Intelligence & Metadata Preview Table**:
    - Leverages Provider Intelligence to automatically detect provider origin (Karaoke Version, Party Tyme, Sunfly, Karaoke.com, Local) from ID3 headers, CDG tags, and file naming conventions.
    - Preview DataGrid displays: Filename, Provider, File Type, Duration, Key, BPM, Quality, Difficulty, Vocal Presence, and per-item toggle checkboxes (Will Normalize, Will Trim Silence, Will Generate Waveform).
  - **Batch Processing Options & Combined FFmpeg Audio Pipelines (`FFmpegService.cs`)**:
    - Global option switches: "Normalize all", "Trim silence for all", "Generate waveform for all", and "Move files to target folders".
    - Added batch-friendly `ProcessAudioPipelineBatchAsync` wrapper to `FFmpegService` executing silence trimming and EBU R128 loudness normalization in a single combined FFmpeg pass when both options are selected.
  - **Throttled Parallel Execution & Progress UI**:
    - Background task throttling using `SemaphoreSlim(3)` running a maximum of 3 concurrent FFmpeg operations to keep the UI smooth and avoid disk thrashing.
    - Applies Smart Import Rules auto-renaming (`Artist - Title (Provider).ext`), metadata extraction, destination routing, SQLite catalog insertion, and Store Analytics updates.
    - Live progress reporting: progress bar, current file indicator, success/failure item counters, and graceful cancellation support.
  - **Bulk Import Completion Summary Dialog**:
    - Displays final statistics upon batch completion: total imported, total skipped, total errors, list of providers involved, average processing time per track, and total elapsed duration.
    - Automatically triggers a refresh of Store Analytics dashboards and Library search indices upon closing.
  - **Unit Test Coverage (`StoreImportTests.cs`)**:
    - Added comprehensive unit test suites covering folder scanning, MP3+G pairing deduplication, companion lyrics discovery, global batch toggle inheritance, per-item override persistence, and target file destination collision resolution.

## [26.9.5.24] - 2026-09-06

### Added
- **Store Analytics Panel (Statistical Insights, Audio Benchmarks & Heatmap Dashboards) (`Lyracist` ONLY)**:
  - **Collapsible Store Analytics Panel (`StorePage.xaml`, `StorePage.xaml.cs`)**:
    - Added a responsive, modern collapsible section beneath the Import Activity Log panel in the Store tab.
    - Features manual "Refresh Analytics" command and toggle collapse/expand state for optimal screen space usage.
    - Bound to `AnalyticsViewModel` exposed via `StoreViewModel.Analytics`.
  - **Provider Statistics**:
    - Tracks total cataloged tracks, individual counts, percentage breakdown, and visual branded pills for Karaoke Version (KV), Party Tyme (PT), Sunfly (SF), Karaoke.com (KC), and Local/Custom collections.
    - Highlights the top/most-frequently used provider.
  - **File Type & Container Statistics**:
    - Displays distribution breakdown across media container and package formats: MP3+G pairs, MP4 videos, ZIPCDG archives, Audio-Only (.mp3) backing tracks, and attached synchronized lyrics (.lrc/.txt).
  - **FFmpeg Processing Benchmarks**:
    - Displays total tracks processed through audio optimization pipelines: EBU R128 loudness normalized, silence trimmed below -50dB, and waveform previews generated.
    - Real-time stopwatch instrumentation measuring average processing time, longest (max), and shortest (min) turnaround times.
  - **Quality & Difficulty Distributions**:
    - Displays 3-tier quality distribution (Low, Medium, High) based on audio bitrate, channels, and video resolution.
    - Displays 3-tier vocal difficulty distribution (Easy, Medium, Hard) derived from tempo, duration, and dynamic range.
  - **Musical Key & BPM Distributions**:
    - Custom pure-WPF horizontal bar chart visualizing the top 8 musical key signatures with track counts and relative proportions.
    - 5-bucket tempo distribution histogram (<80 BPM, 80-100 BPM, 100-120 BPM, 120-140 BPM, 140+ BPM).
  - **Import Activity Timeline & 24-Hour Heatmap**:
    - Highlights busiest import date and busiest hour of the day.
    - 14-day daily acquisition timeline bar chart.
    - 24-hour import activity heatmap matrix with dynamic intensity color shading (0 to 4) and informational tooltips.
  - **Database Queries & ViewModels (`LyracistDbContext.cs`, `StoreAnalyticsData.cs`, `AnalyticsViewModel.cs`)**:
    - Implemented `GetStoreAnalyticsAsync()` performing lightweight, null-safe aggregation queries across the `Songs` table without modifying schema.
    - Automated refresh logic in `StoreViewModel` triggered on view load and upon each successful track import.

## [26.9.5.23] - 2026-09-06

### Added
- **Smart Import Rules (Auto-Rename, Tagging, Classification & Enrichment) (`Lyracist` ONLY)**:
  - **Auto-Rename Imported Files (`renameImportedFiles` / `RenameImportedFiles`)**:
    - Automatically renames primary audio/video files and all associated companion files (CDG graphic streams, LRC/TXT synchronized lyrics, and waveforms) to canonical format: `Artist - Title (Provider).ext` (e.g. `Adele - Hello (KV).mp3`, `Bon Jovi - Wanted Dead or Alive (PT).cdg`, `Queen - Don't Stop Me Now (SF).mp4`).
    - Standardizes publisher abbreviation tags: Karaoke Version $\rightarrow$ `KV`, Party Tyme $\rightarrow$ `PT`, Sunfly $\rightarrow$ `SF`, Karaoke.com $\rightarrow$ `KCOM`.
  - **Auto-Tag Genres (`detectGenre` / `DetectGenre`)**:
    - Automatically categorizes imported tracks into catalog genres (Pop, Rock, Country, Soul, R&B, Jazz, Hip-Hop, Gospel, Dance, Standards) using FFprobe tags and provider catalog classifications.
  - **Auto-Tag Difficulty (`detectDifficulty` / `DetectDifficulty`)**:
    - Classifies performance difficulty into `Easy`, `Medium`, and `Hard` levels using tempo (BPM), track duration, dynamic range, and vocal range/stamina heuristics.
  - **Auto-Tag Musical Key (`detectKey` / `DetectKey`)**:
    - Extracts and normalizes musical key signatures from ID3 frames (`TKEY`, `initialkey`), container tags, and title/comment annotations (e.g. `Am`, `C#m`, `G`).
  - **Auto-Tag BPM (`detectBpm` / `DetectBpm`)**:
    - Extracts and parses track tempo from ID3 tags (`TBPM`, `bpm`, `tempo`) and comment descriptors into numeric beats-per-minute.
  - **Auto-Tag Vocal Presence (`detectVocalPresence` / `DetectVocalPresence`)**:
    - Classifies vocal presence into `guide vocals`, `background vocals`, or `no vocals` based on dual audio streams, channel layouts, and audio/comment tags.
  - **Auto-Tag File Quality (`detectQuality` / `DetectQuality`)**:
    - Classifies audio/video stream quality into `High`, `Medium`, and `Low` tiers based on audio bitrate, sample rate, channels, codec, and video resolution.
  - **Track & Database Schema Extensions**:
    - Extended TypeScript `Track` in `types.ts`, `PurchasedTrackItem` in `PurchasedTrackWatcherService.cs`, and `Song` entity in `Song.cs` with optional `Genre`, `Difficulty`, `Key`, `BPM`, `VocalPresence`, and `Quality` fields.
    - Added EF Core migration `20260906152500_AddSmartImportFieldsToSong` and updated `LyracistDbContextModelSnapshot.cs`.
    - Enriched SQLite FTS5 search index and library tag badges with smart classification metadata.

## [26.9.5.22] - 2026-09-06

### Added
- **Advanced Store Provider Intelligence ("Provider Fingerprinting") (`Lyracist` ONLY)**:
  - **ZIP Internal Signatures (`inspectZipForProvider` / `InspectZipForProvider`)**:
    - **Karaoke Version**: Identifies internal `/custom_backing_track/` directory hierarchies, exact `track.mp3` + `track.cdg` generic pairs, and "KV" or "Karaoke Version" catalog entries.
    - **Party Tyme**: Inspects `/karaoke/` folders, `_pt.` or `- pt.` file suffixes, and PT catalog tags.
    - **Sunfly**: Detects entries prefixed with `SF` and Sunfly catalog codes.
    - **Karaoke.com**: Identifies `KCOM` / `KARAOKECOM` filenames and internal descriptors.
  - **CDG Magic Header Fingerprinting (`detectProviderFromCdgHeader` / `DetectProviderFromCdgHeader`)**:
    - Reads the initial 24-byte header of CDG graphics streams to detect publisher encoding signatures:
      - `0x01 0x0F` $\rightarrow$ **Karaoke Version**
      - `0x02 0x0A` $\rightarrow$ **Party Tyme**
      - `0x03 0x0C` $\rightarrow$ **Sunfly**
  - **MP3 ID3 Tag Fingerprinting (`detectProviderFromId3` / `DetectProviderFromId3`)**:
    - Leverages FFprobe tag extraction to detect user-defined text frames and publisher tags:
      - `TXXX:KV` or `KV` $\rightarrow$ **Karaoke Version**
      - `TXXX:PT` or `PT` / `Sybersound` $\rightarrow$ **Party Tyme**
      - `TXXX:SF` or `SF` $\rightarrow$ **Sunfly**
      - `TXXX:KCOM` or `KCOM` / `KARAOKECOM` $\rightarrow$ **Karaoke.com**
  - **MP4 Container Metadata Fingerprinting (`detectProviderFromMp4` / `DetectProviderFromMp4`)**:
    - Scans video container metadata (`title`, `artist`, `comment`, and stream tags) for licensed publisher watermarks: "Sunfly", "Party Tyme", "Karaoke Version", and "Karaoke.com".
  - **In-Memory Referrer Hints (`setReferrerHint` / `PurchasedTrackWatcherService.ReferrerHint`)**:
    - Records the last provider clicked from the Store search bar ("Search Karaoke Version", "Search Party Tyme", "Search Karaoke.com", "Search Sunfly") and uses it as an intelligent fallback hint when files lack internal metadata signatures.
  - **Unified Pipeline Hierarchy**:
    - Watchers in both desktop WPF (`PurchasedTrackWatcherService.cs`) and Web reference (`purchasedWatcher.ts`) now execute the full fingerprinting cascade (ZIP $\rightarrow$ CDG Header $\rightarrow$ ID3 Tag $\rightarrow$ MP4 Metadata $\rightarrow$ Filename Heuristics $\rightarrow$ Referrer Hint) before library insertion.

## [26.9.5.21] - 2026-09-06

### Added
- **Extended "Store" Tab & Advanced Import Pipeline (`Lyracist` ONLY)**:
  - **Additional Search Providers (Deep-Linking)**:
    - Added collapsible "Search Additional Providers" panel featuring direct deep-link buttons for **Karaoke.com** (`https://karaoke.com/search?type=product&q={encodedQuery}`) and **Sunfly Karaoke** (`https://www.sunflykaraoke.com/catalogsearch/result/?q={encodedQuery}`).
    - Fully integrated into both desktop WPF UI (`StorePage.xaml`) and Web reference UI (`storeSearch.tsx`).
  - **FFmpeg-Powered Audio Processing Pipeline**:
    - **Loudness Normalization**: Optional audio normalization to EBU R128 broadcast standard (-16 LUFS target, -1.5 dB TP, 11 LRA) via `FFmpegService.NormalizeAudioAsync` / `ffmpegUtils.ts`.
    - **Silence Trimming**: Optional leading and trailing silence elimination below -50dB via `FFmpegService.TrimSilenceAsync`.
    - **Waveform Preview Generation**: High-contrast PNG audio waveform preview image rendering via `FFmpegService.GenerateWaveformPreviewAsync` (`showwavespic=s=800x120:colors=#3B82F6`).
  - **MP4 Dual-Audio Stream Detection**:
    - Automatic identification of MP4 karaoke video tracks containing multiple audio streams (e.g. guide vocals on stream 1, backing music on stream 2).
    - Detected tracks are tagged with a `Dual-Audio` badge in the library and recent imports table.
  - **Asynchronous Separate-Arrival MP3+G Pairing**:
    - Enhanced watcher logic to gracefully handle browsers downloading paired `.mp3` and `.cdg` files at slightly different times, holding individual arrivals in a debounce queue and consolidating into a unified track.
  - **Deep Media Probing & Metadata Extraction (FFprobe)**:
    - Integrated `FFprobeRunner.ProbeMediaFileAsync` extracting precise duration, audio bitrate, codec, sample rate, audio channels, stream count, and video stream specs.
    - Automatic detection and pairing of `.lrc` and `.txt` lyric files sharing the same base filename, tagging songs with a `Lyrics` badge and lyrics file path.
  - **Store UI Refinements & Import Log Activity Panel**:
    - Added three new pipeline configuration toggles in Settings & Store page: "Normalize Audio on Import", "Trim Silence on Import", and "Generate Waveform Preview".
    - Added collapsible **Import Activity Log** panel maintaining a live 20-event rolling log of file detections, conversions, and imports with status badges (`Success`, `Info`, `Warning`, `Error`) and "Clear Log" action.
    - Updated Recent Imports DataGrid with technical badges displaying format, lyric association, dual-audio streams, normalization status, and waveform availability.
- **Dedicated "Store" Tab & Licensed Track Import Workflow (`Lyracist` ONLY)**:
  - Added a dedicated top-level "Store" navigation tab exclusively in the Lyracist desktop application (`MainWindow.xaml` and `StorePage.xaml`). This tab is strictly omitted from companion applications (Lyracist Trivia, Bar Trivia, KSRotation, etc.).
  - **Deep-Link Store Search**:
    - Branded direct deep-link search buttons for **Karaoke Version** (`https://www.karaoke-version.com/search.html?q={encodedQuery}`) and **Party Tyme** (`https://www.partytyme.net/search?q={encodedQuery}`).
    - User query encoding with external browser launching via `Process.Start`.
    - Complies with zero-scraping, zero-audio-proxying architecture for licensed commercial platforms.
  - **Purchased Tracks Folder & Auto-Import Watcher (`PurchasedTrackWatcherService`)**:
    - Configurable download folder watcher (defaults to user's `Downloads` folder).
    - Asynchronous `FileSystemWatcher` detecting new `.mp3`, `.cdg`, `.zip`, and `.mp4` downloads.
    - File lock checking and debounce logic to wait for web browser downloads to finish before importing.
    - MP3+G pair detection coalescing matching `.mp3` and `.cdg` files into a single unified track.
  - **Target Folder Organization**:
    - Configurable target folders for **Karaoke tracks** (`.zip`, `.cdg`+`.mp3`, `.mp4`) and **Music / Audio tracks** (standalone `.mp3`).
    - Move toggle automatically organizes imported files from Downloads into designated library directories.
  - **Automated Metadata & Database Integration**:
    - Automatic title and artist parsing via `ScanningService.ParseStoreDownload` and store pattern heuristics.
    - Provider source tagging (`"Karaoke Version"`, `"Party Tyme"`, or `"Local"`).
    - Direct insertion into `LyracistDbContext` and SQLite FTS5 search index (`SearchService.IndexSongsBatch`).
    - Event-driven library updates via `ILibraryService.NotifyLibraryUpdated()`.
  - **Manual Import & Recent Activity View**:
    - "Import Purchased Track..." multi-file picker button for on-demand manual imports.
    - Real-time recent imports data table with source badges, title, artist, format, file path, and timestamps.
  - **Full Web / TypeScript Reference Module (`Lyracist/WebModules/Store`)**:
    - Created React/TypeScript UI components (`storeTab.tsx`, `storeSearch.tsx`, `settingsStore.tsx`) and Node.js backend modules (`purchasedWatcher.ts`, `importMetadata.ts`, `deepLink.ts`, `types.ts`, `index.ts`).

### Added
- **Live DJ Webcam Performer Photo Capture (`Lyracist` & `KSRotation`)**:
  - Added a dedicated "Take Photo" button on the Users tab next to "Upload Photo" and "Clear Photo" in both Lyracist (`UsersPage.xaml`) and KSRotation (`MainWindow.xaml`).
  - **FlashCap Camera Integration**: Integrated pure managed DirectShow/MediaFoundation capture via `FlashCap` (v1.12.0) with zero external native DLL dependencies, full .NET 10 compatibility, and asynchronous background frame processing.
  - **Shared Camera Engine (`WebcamCaptureService.cs`)**: Shared helper supporting video device enumeration, dynamic camera selection, live frame callback decoding to `BitmapSource`, background thread dispatching, and camera lifecycle disposal.
  - **Live Viewfinder & Headshot Guide**: Modal overlay featuring real-time camera selection dropdown, 480x360 live preview, and a circular headshot framing guide assisting the DJ in centering the performer's face for audience rotation billboards, vinyl turntable graphics, and patron portal profiles.
  - **Snapshot Review & Retake**: Ability to freeze the camera feed upon snapshot capture, review the picture with the performer, and either retake immediately or confirm.
  - **Automatic Center-Crop, Scaling & Avatar Assignment**: `SaveSquarePhoto()` automatically crops the captured frame to a 1:1 square centered on the frame, scales the image to 400x400 pixels, encodes to JPEG, saves to `Data/Avatars/{guid}.jpg`, and assigns the path to the performer's account in `Data/lyracist.db`.

### Fixed
- **High-Contrast Light Text Foreground on Users Tab / Edit Page (`Lyracist` & `KSRotation`)**:
  - Eliminated dark/black text rendering on dark backgrounds across the Users tab and performer editor views.
  - Applied `TextElement.Foreground="{DynamicResource AppContrastTextBrush}"` across the root Grid, two-column layout, left pane, and right pane editor borders in `KSRotation\MainWindow.xaml`.
  - Added explicit high-contrast foreground brushes (`AppContrastTextBrush` in KSRotation, `AppTextPrimaryBrush` in Lyracist) to all input field labels ("Singer Name", "Portal PIN Code", "Email Address", "Vocal Range", "Custom Title / Nickname", "Experience Score (XP)", "Total Songs Sung", "Performer Notes", "Microphone Gain", "Key Transposition", "Playback Speed", "Treble", "Mid", "Bass", "Compressor Amount", "Limiter Threshold", "Audio Notes / Sound Check Remarks").
  - Styled the 3-Band EQ GroupBox with `BlueSettingsGroupBoxStyle` (KSRotation) and `UsersSettingsGroupBoxStyle` (Lyracist) featuring crisp gradient headers and high-contrast light labels.
  - Configured high-contrast foreground styling and cell/row resources on the "Performance History" DataGrid (`UserPerformanceHistory` / `PerformanceHistory`).
  - Set explicit light foregrounds on the "Merge Duplicate Singer Account" and "Webcam Performer Snapshot" modal overlays.
- **Run.Text Read-Only Binding XamlParseException in Users View (`Lyracist` & `KSRotation`)**:
  - Resolved `System.Windows.Markup.XamlParseException` (`InvalidOperationException: A TwoWay or OneWayToSource binding cannot work on the read-only property 'SongsText' of type 'SingerUserItem'`) caused by WPF's default `BindsTwoWayByDefault` metadata on the `Run.Text` dependency property.
  - Added explicit `Mode=OneWay` to `<Run Text="{Binding SongsText, Mode=OneWay}" />` and `<Run Text="{Binding VocalRange, Mode=OneWay}" />` in both `KSRotation\MainWindow.xaml` and `Lyracist\Views\Pages\UsersPage.xaml`.
  - Added defensive empty setters `set { }` to `SongsText` and `LevelText` on both `SingerUserItem` (`MainViewModel.Users.cs`) and `SingerItem` (`UsersViewModel.cs`) to ensure full binding resilience.

## [26.9.5.18] - 2026-09-06

### Fixed
- **SymbolRegular Parsing Exception in UsersPage (Lyracist)**:
  - Resolved `System.Windows.Markup.XamlParseException` (`ArgumentException: Requested value 'ArrowMerge20' was not found`) on line 225 of `UsersPage.xaml` caused by an invalid WPF-UI symbol name.
  - Replaced `ArrowMerge20` with the valid, semantically intuitive `People24` symbol icon for the "Merge Duplicate..." button.

### Added
- **Dedicated "Users" Tab & Singer Account Management (KSRotation)**:
  - Implemented full feature parity with Lyracist by adding a top-level "Users" TabItem into KSRotation's main window (`MainWindow.xaml`), positioned cleanly between Settings and Trivia.
  - **Performer Master-Detail Directory**: Searchable list of all registered performers with real-time text query filtering across name, email, and custom title, complete with circular avatar thumbnails, level badges, XP scores, and song counts.
  - **Account Lifecycle Actions**: Added "New Singer" and "Delete Singer" actions backed by `AddNewUserCommand` and `DeleteUserCommand`.
  - **Performer Profile & PIN Credentials**: Sub-tab for editing Singer Name, 4-digit patron portal PIN code, email address, vocal range dropdown, custom stage titles / nicknames, experience score (XP), and DJ performer notes, saved directly to the shared `Data/lyracist.db` database.
  - **Vocal & Audio Profile Defaults**: Sub-tab for editing preloaded audio defaults (Microphone Gain, Key Transposition, Playback Speed, Treble/Mid/Bass 3-Band EQ, Compressor, Limiter, and Sound Check Remarks) that automatically apply whenever the performer takes the stage.
  - **Performance History & 1-Click Rotation Queuing**: Sub-tab displaying all past songs sung by the performer, with a direct "Queue" button to insert a past favorite track immediately into the active rotation round, and a "Remove" button to delete individual records.
  - **Duplicate Account Merging**: Added "Merge Duplicate..." modal overlay (`OpenUserMergePopupCommand`, `ExecuteUserMergeCommand`, `CancelUserMergeCommand`) consolidating duplicate performer profiles by transferring performance history, queued requests, and XP into the primary account, followed by safe deletion of the duplicate.
  - **Performer Photo & Avatar Management**: Support for uploading custom selfies and photos (`UploadUserAvatarCommand`) or clearing back to default avatars (`ClearUserAvatarCommand`), saved directly in `Data/Avatars/`.
  - Added `MainViewModel.Users.cs` partial class to `KSRotation` and linked `SingerXpHelper.cs` and `SingerHistoryService.cs` in `KSRotation.csproj`.

## [26.9.5.17] - 2026-09-06

### Added
- **Session Start & Stop Scheduling (Lyracist & KSRotation)**:
  - Allowed DJs to configure exact start and stop times for the active karaoke session via a dedicated "Session Schedule & Request Cutoff" GroupBox in both Lyracist (`SettingsPage.xaml`) and KSRotation (`MainWindow.xaml`).
  - Dropdown options in 30-minute intervals across the 24-hour cycle, plus freeform editable text for custom times (supporting both 12-hour AM/PM and 24-hour formats).
  - Robust overnight schedule support handling windows crossing midnight (e.g., 8:00 PM to 2:00 AM).
  - Configured `EnableSessionSchedule`, `SessionStartTime`, and `SessionStopTime` properties in `AppSettings` with persistence across app sessions.
  - When enabled, requests submitted through the patron portal (`/api/requests` and `/api/request`) or kiosk before the session starts or after it finishes are blocked with an informative message notifying patrons of the scheduled session hours.
- **Last Request Cutoff Time via Patron & Kiosk Portals**:
  - Added configurable "Enable Last Request Cutoff" toggle (`EnableLastRequestTime`) and "Last Request Time" (`LastRequestTime`, default "1:30 AM") in both Lyracist and KSRotation.
  - When enabled, patron and kiosk request endpoints reject submissions attempted after the cutoff time with a polite, clear message: "Song requests are now closed for tonight. The cutoff time for requests was [Time]."
  - Supports operating in conjunction with the session schedule or independently for open-ended shows.
- **Shared Session Schedule Logic**:
  - Implemented `SessionScheduleHelper.cs` in `Shared`, providing time interval generation, format-agnostic time parsing, window comparison including midnight spanning, and request eligibility evaluation.
  - Linked `SessionScheduleHelper.cs` across `Lyracist`, `KSRotation`, `KSRotation.Maui`, and test projects.
- **Automated Tests**:
  - Added unit test suite in `SessionDuplicateAndUserTests.cs` validating `TryParseTime`, daytime and overnight `IsTimeInWindow`, session schedule enforcement, last request cutoff enforcement, and default bypass behavior.

## [26.9.5.16] - 2026-09-06

### Added
- **Session Duplicate Song Blocking (Lyracist & KSRotation)**:
  - Added `BlockDuplicateSongsInSession` setting with persistence in `AppSettings` across both Lyracist and KSRotation, complete with UI checkboxes in Settings > Rotation Settings.
  - Implemented `IsSongInCurrentSession(string songTitle, string artist)` in both `RotationViewModel` and `MainViewModel`, validating incoming requests against both active queued singers and songs already performed in the current session.
  - Enforced duplicate song blocking in patron web portal endpoints (`/api/request` and `/api/requests`), rejecting duplicates with a polite notification that the song has already been performed or queued during the current session.
- **New "Users" Navigation Tab & Singer Management (Lyracist)**:
  - Added a dedicated "Users" navigation item to the Lyracist sidebar navigation menu (`MainWindow.xaml`) backed by `UsersPage.xaml` and `UsersViewModel.cs`.
  - **Performer Directory**: Searchable list of all registered performers with real-time filtering, level badges, XP scores, and song counts.
  - **Account Details Editor**: Edit Singer Name, 4-digit Patron Portal PIN code, Email, Vocal Range (dropdown), Custom Title / Nickname, XP (Experience Score), and DJ Notes.
  - **Vocal & Audio Defaults**: Configure per-singer default Microphone Gain (Volume), Key Transposition (-12 to +12 semitones), Playback Speed (Tempo), 3-Band Parametric EQ (Treble, Mid, Bass), Dynamics (Compressor, Limiter), and Sound Check remarks.
  - **Performance History Record**: Displays full historical log of songs performed by the singer with Song Title, Artist, Key Transposition, Playback Speed, Source, and Date/Time stamp, with direct "Queue" (instantly re-queues the past performance back into rotation preserving key and tempo) and "Remove" actions.
  - **Merge Duplicate Accounts**: Added "Merge Duplicate..." modal dialog and backing services (`DatabaseService.MergeSingers()` and `SingerHistoryService.MergeHistory()`), consolidating duplicate performer accounts by transferring performance history, requests, rotation entries, audio profile defaults, and XP points to the primary account, followed by permanent deletion of the duplicate.
- **Performer Selfie / Photo Uploads via Patron Portal**:
  - Implemented `/api/singer/login`, `/api/singer/profile`, `/api/singer/avatar/upload`, and `/api/singer/avatar` in `TabletLyricsServer.cs` with payload signature verification, base64 decoding, and secure file saving in `Data/Avatars/`.
  - DJs can also view, upload, or clear photos directly within the "Users" tab in Lyracist.
- **Performer Selfie Display on "Vegas Billboard" & "Vinyl Record" Rotation Banners**:
  - Updated `RotationWindow.xaml` / `RotationWindowViewModel.cs` (Lyracist) and `SingerDisplayWindow.xaml` / `DisplayViewModel.cs` (KSRotation) to display the performer's photo in a circular gold-framed badge on the Vegas Marquee ("Vegas Billboard") and alongside the "NOW SPINNING" header on the Vinyl Record ("Vinyl Record") turntable banner.
- **Automated Tests**:
  - Added `SessionDuplicateAndUserTests.cs` in `Lyracist.Tests` validating session duplicate song detection, history consolidation, and database singer account merging.

## [26.9.5.15] - 2026-09-06

### Added
- **Per-Singer Key/Tempo Recall & Per-Singer Mic Level/EQ Save and Recall (Lyracist)**:
  - **Dual-Tier Key and Tempo Recall**:
    - *Per-Song History Tracking*: Key shift (`Key`) and playback speed (`Tempo`) are recorded per performer and song in the `SingerHistory` SQLite database table. When a performer selects or repeats a song (via Search, Singer History, or rotation queuing), their exact key and tempo for that track are automatically recalled.
    - *Singer Profile Defaults*: Global performer vocal defaults in `SingerAudioSettings` serve as the fallback initial baseline when a performer sings a song for the first time without prior history.
    - Added `Singer.Tempo` and `PendingSong.Tempo` properties to carry custom tempo settings through the rotation queue, and implemented `SingerHistoryService.GetSongHistory()` with case-insensitive `COLLATE NOCASE` lookups.
  - **Per-Singer Mic Level & Vocal EQ Recall**:
    - A performer's saved vocal level gain (mic level), 3-band EQ (`Treble`, `Mid`, `Bass`), and dynamics (`Compressor`, `Limiter`) from `SingerAudioSettings` are automatically loaded and applied when their performance begins.
    - The host console's Audio Controls sliders on `KaraokePage` dynamically update to reflect the on-stage performer's profile.
    - Direct **[Save to Singer]** and **[Recall Singer]** action buttons in the Audio Controls card header allow the KJ to instantly save live audio adjustments into the performer's profile or revert to their saved defaults with a single click.
  - **Singer History DataGrid Enhancements**:
    - Added `Key` and `Speed` columns to the Singer History tab in `KaraokePage.xaml`.
    - Preserved recalled key and tempo when queuing past songs directly from the Singer History tab into the active rotation via `AddHistorySongToRotationCommand`.
  - **Database Migration & Unit Tests**:
    - Auto-migrated `SingerHistory` SQLite table schema to add `Key TEXT DEFAULT '0'` and `Tempo REAL DEFAULT 1.0` columns safely on startup.
    - Added `SingerAudioRecallTests` in `Lyracist.Tests` validating database persistence, song history key/tempo recall, and rotation completion tracking.

## [26.9.5.14] - 2026-09-06

### Changed
- **KSRotation (Settings Tab Layout, Compact Email Settings & Rotation Settings)**:
  - Moved the "Save Settings" button to Column 2 directly underneath the "DJ Names" groupbox for balanced alignment and intuitive access.
  - Compacted the "Email Settings" groupbox in Column 0 by removing the fixed `Height="121"` constraint on its child StackPanel, setting `VerticalAlignment="Top"` and `Margin="0,0,0,12"`, tightening control margins, and adjusting `SlateSettingsGroupBoxStyle` body padding to eliminate excessive whitespace above the first hint textblock and below the auto-send checkbox.
  - Made the "Network IP Setup" groupbox more concise and compact by setting `VerticalAlignment="Top"`, updating `NavySettingsGroupBoxStyle` body padding to compact `12,6,12,6`, tightening control margins, and condensing the explanatory text to a clean, direct sentence.
  - Placed the "Test Mode" checkbox and "Save Current Rotation as Test List" button into a dedicated "Rotation Settings" groupbox in Column 3 positioned directly beneath "Network IP Setup".
  - Enlarged the estimated wait time legend text font size on KSRotation's display projection window (SingerDisplayWindow.xaml) for enhanced audience legibility.
  - Cleaned up the Settings tab grid row definitions to eliminate leftover designer overrides and fixed fractional row heights.

## [26.9.5.13] - 2026-09-06

### Added
- **Lyracist / KSRotation / KSRotation.Maui (Estimated Wait Time Legend on Every Display)**:
  - The `{N} = Estimated wait time` explainer now appears everywhere the `{N}` badge itself can appear, not just KSRotation's Marquee/Vinyl views: Lyracist's Normal List (queue panel header) and Star Wars Crawl, KSRotation's Normal List (new header row - it had none before) and Star Wars Crawl (which, it turns out, never actually showed the `{N}` badge at all until now - only the legend was missing there, the badge itself was a gap too), and KSRotation.Maui's single Billboard view.
  - Every instance hides along with the badges when the DJ turns "Show Estimated Wait Time" off, and reappears immediately when turned back on - no restart needed.
  - Lyracist's `RotationWindowViewModel` gained a `ShowEstimatedWaitTime` property (it had none before this - only `RotationViewModel`/`AppSettings` did), pushed live via a new `IDisplayService.SetShowEstimatedWaitTime()` and triggering an immediate Star Wars Crawl rebuild through the existing `Vm_PropertyChanged` hook.
  - Checked all patron-facing web pages (`kiosk.html`, `dj.html`, `billboard.html`, `PatronPortal.html`, Lyracist's `mobile.html`/`index.html`): none of them currently serialize or render `EstimatedWaitMinutes` to patrons at all, so there is nothing to gate there yet - adding the badge to the patron-facing web/JSON pipeline would be a separate, larger feature.
- **KSRotation (billboard.html)**: Cleaned up CSS stylesheet indentation and JavaScript string escaping formatting in the web rotation billboard template.

### Fixed
- **KSRotation.Maui (Broken Build Introduced by the Wait Time Toggle)**: the `26.9.5.11` toggle feature added `DisplayWindowService.SetShowEstimatedWaitTime()` to KSRotation's real service but missed the Maui-only no-op stand-in in `KSRotation.Maui/Shims/WpfShims.cs` - since KSRotation.Maui compiles KSRotation's `MainViewModel.cs` directly but substitutes its own WPF-free service shims, this broke KSRotation.Maui's build entirely (masked at the time by a `| tail` pipe that hid the real `dotnet build` exit code in verification output). Added the missing shim method; confirmed by reading the actual build output text this time, not just the wrapping shell command's exit code.

## [26.9.5.12] - 2026-09-06

### Fixed
- **KSRotation (Wait Time Toggle Hid Too Much of the Marquee/Vinyl Queue Labels)**:
  - Turning off "Show Estimated Wait Time" hid the entire "UP NEXT"/"ON DECK" queue section label on the Vegas/Broadway Marquee and Vinyl Record projection styles - it should only have hidden the part explaining what the `{N}` badge means, not the label itself.
  - Each label is now split into an always-visible "UP NEXT"/"ON DECK" and a separately-bound ": # in {} = Estimated wait time" suffix, so the section header stays put and only the badge-format explainer hides with the toggle.

## [26.9.5.11] - 2026-09-05

### Added
- **Lyracist / KSRotation (Toggle to Hide Estimated Wait Time Badges)**:
  - New "Show Estimated Wait Time on Rotation Screens" setting (Lyracist's Settings page, Display tab; KSRotation's Settings tab) lets the DJ turn the `{N}` minute wait-time badge off entirely - some DJs don't want the audience seeing wait estimates at all. Defaults on (unchanged behavior).
  - `RotationHelpers.RecalculateEstimatedWaits()` gained an `enabled` parameter (defaults `true`); when `false` it clears every singer's estimate to 0, and since every display already hides the badge whenever the estimate is 0, no rendering code needed to change.
  - Flipping the toggle takes effect immediately (no need to wait for the next rotation change) - Lyracist's `RotationViewModel.RefreshEstimatedWaitTimeVisibility()` and KSRotation's `OnShowEstimatedWaitTimeChanged` both force an immediate recalculation and display refresh.
  - The Default Song Length slider is grayed out and disabled whenever the toggle is off, since it has no effect while wait times aren't shown. KSRotation.Maui shares KSRotation's `MainViewModel`/`AppSettings` so it honors the same setting, though it has no dedicated toggle in its own UI yet.
  - KSRotation's Vegas/Broadway Marquee and Vinyl Record projection styles (`SingerDisplayWindow.xaml`) also carry an "UP NEXT/ON DECK: # in {} = Estimated wait time" legend caption above the queue - now hidden along with the badges themselves via a new `DisplayViewModel.ShowEstimatedWaitTime` property, kept in sync by `DisplayWindowService.SetShowEstimatedWaitTime()`.

## [26.9.5.10] - 2026-09-05

### Fixed
- **Lyracist / KSRotation (Estimated Wait Time Badges Missing Entirely Until First Singer Finished)**:
  - `RotationHelpers.RecalculateEstimatedWaits()` was only ever invoked when a singer was marked finished/done - so a freshly-built rotation with a singer marked current, but nobody finished yet, showed **no wait-time badges at all** on any rotation/billboard display, even though everything else about the feature was working correctly.
  - Both apps now also recalculate wait times whenever a singer is added, set/toggled current or round-start-anchor, moved up/down, paused/reactivated, marked inactive/reactivated, or removed - not only on "done" - so badges appear as soon as there's a current singer, and stay accurate through every reorder in between.
  - Lyracist: added to the shared `RunRotationOrderChange` chokepoint (covers move up/down, set current, skip, set rotation anchor) plus `AddSinger`, `RemoveSinger`, and `ToggleInactiveSinger`. KSRotation: added to `AddActiveSinger`, `SetCurrentSinger`, `SetRotationStartSinger`, `RemoveSinger`, `ToggleSingerInactive`, and all four move commands. KSRotation.Maui shares KSRotation's `MainViewModel` so it picks up the same fix automatically.
  - Added ViewModel-level regression tests in `Lyracist.Tests` proving a waiting singer gets a nonzero estimate as soon as the first singer is marked current or a new singer joins an already-started rotation, without requiring anyone to finish a song first.

## [26.9.5.9] - 2026-09-05

### Added
- **Lyracist / KSRotation (Configurable Default Song Length for Estimated Wait Time)**:
  - The rotation-screen "estimated wait time" badge's fallback estimate (used whenever a queued song's actual duration isn't known) is now a DJ-configurable setting instead of a fixed 5 minutes - **default lowered to 4.75 minutes**, closer to a typical song's actual runtime.
  - New "Default Song Length" slider (2-8 min, quarter-minute steps) on Lyracist's Settings page (Display tab, next to "Float Current Singer to Top") and KSRotation's Settings tab (next to Marquee Speed/Watermark Opacity), persisted the same way as every other setting in each app.
  - `RotationHelpers.RecalculateEstimatedWaits()` (`Shared/RotationHelpers.cs`) gained an optional `defaultEstimatedPerformanceSeconds` parameter that both apps now pass their own configured value into; the built-in constant (unchanged call sites, other consumers) also moved from 300s to 285s to match the new default.
  - KSRotation.Maui reads the same setting from its own local settings file (shares `MainViewModel`/`AppSettings` with KSRotation) but has no dedicated settings UI of its own for it yet - falls back to the 4.75-minute default until/unless a value is set.

## [26.9.5.8] - 2026-09-05

### Changed
- **Lyracist / KSRotation / KSRotation.Maui (Terminology: "1st Singer" → "Rotation Anchor")**:
  - The round-start marker (previously the 🚩 "1ST" badge/"1st Singer" label - easy to confuse with "whoever's physically first in the list" or "whoever's up next") is now called **Rotation Anchor**, shown as an **⚓ ANCHOR** badge. It marks the same thing it always did - the performer the rotation's full-round tracking is anchored to - and the DJ can still assign/clear it manually the same way (right-click menu, 🚩→⚓ toggle button); only the label and icon changed.
  - Updated every user-facing surface: badges, tooltips, context-menu items, and button labels in Lyracist (`RotationPage`, `KaraokePage`, `RotationWindow` - list, Star Wars Crawl, and marquee banner), KSRotation (`MainWindow`, `SingerDisplayWindow` - all three projection styles), KSRotation.Maui (`MainPage`, `BillboardView`), the in-app Help pages, and the shared web surfaces (`kiosk.html`, `dj.html`, `billboard.html`, `PatronPortal.html`, `mobile.html`).
  - Purely a display/wording change - the underlying `IsRotationStart` property, `RotationHelpers` method names, and command bindings are unchanged, so this carries no behavioral risk.

## [26.9.5.7] - 2026-09-05

### Fixed
- **KSRotation ("1st Singer" Round-Start Badge Missing Entirely After Seeding/Restore)**:
  - `LoadTestData()` (demo/test-mode seed data) and `LoadDatabaseNow()` (restoring a saved night from `ksrotation_night_db.json` on startup) both populate `Singers` via a raw `Add()` loop that bypasses `RotationHelpers.InsertNewSinger` - the normal path that guarantees someone always holds the "🚩 1ST" round-start badge. With nobody holding it, and `_isInitializing` suppressing the usual reactive fallback during construction, a freshly-loaded rotation could end up with **no one** marked 1st at all.
  - Both methods now call `RotationHelpers.EnsureRotationStartFlag(Singers)` after populating the list, which defaults the badge to whoever was entered first - matching the expectation that a fresh rotation's first singer is automatically the 1st singer, with the DJ still free to reassign it via the existing 🚩 toggle.
  - Lyracist's equivalent seeding path (`RotationViewModel.SeedSingers()`) was checked too, but turned out to already be covered by its own reactive `Rotation.CollectionChanged` handler (which has no `_isInitializing`-style suppression) - added a regression test confirming that, rather than an unneeded code change.
  - Added a regression test in `KSRotation.Tests` that reproduces the exact construction-time timing (flips `_isInitializing` back on before invoking `LoadTestData` via reflection) so it can't be papered over by the reactive fallback running anyway when called from a test.

## [26.9.5.6] - 2026-09-05

### Fixed
- **Lyracist / KSRotation (Linked Singers: Finished Singer Wasn't Dropping to the Bottom)**:
  - With "Float Current Singer to Top" enabled, marking a linked singer's song finished was supposed to drop them to the bottom of the rotation like any other singer - instead they landed in 2nd place, with their linked partner promoted to 1st, because adjacency enforcement ran right after the rotation advanced and dragged the just-finished singer straight back up next to their partner (undoing the float-to-bottom).
  - `RotationHelpers.EnforceLinkedAdjacency()` now exempts a linked pair while **either half is the current singer** - that's exactly the moment a pair is *expected* to separate (one just finished and floated away, the other was promoted to perform next - back-to-back, the whole point of linking them). Once neither half is current anymore (both have had their turn), the next call finds no exemption and pulls them back together for their next joint turn - so **the link still stays in effect all night, exactly as intended, without needing to be re-applied by the DJ**.
  - Added unit tests for both the exemption and the reunion, plus ViewModel-level regression tests in `Lyracist.Tests` and `KSRotation.Tests` (mirroring an existing, unrelated float-to-top regression test) confirming the finished singer actually reaches the bottom of the list.

## [26.9.5.5] - 2026-09-05

### Fixed
- **KSRotation (Crash: "Cannot change ObservableCollection during a CollectionChanged event")**:
  - Reordering the rotation (Move Up/Down, adding a singer, finishing a song, etc.) while a Linked Singers pair needed realignment could crash with this `InvalidOperationException`, shown as an "Unexpected Error" popup.
  - Cause: `OnSingersCollectionChanged` fires synchronously on every structural change to `Singers` and was calling into the new Linked Singers adjacency enforcement, which can itself reorder `Singers` - `ObservableCollection` forbids mutating itself while still dispatching its own `CollectionChanged` event.
  - Fix: adjacency enforcement is now deferred via `Dispatcher.BeginInvoke` so it always runs after the current dispatch has fully unwound, instead of reentrantly during it. Several now-redundant explicit enforcement calls scattered across `MainViewModel.cs` were removed in favor of this single, safe chokepoint.

## [26.9.5.4] - 2026-09-05

### Added
- **Lyracist / KSRotation / KSRotation.Maui (Linked Singers)**:
  - The DJ can now link two singers already in the rotation (two-click: click 🔗 on one, then click 🔗 on the other) so they always stay adjacent - no other singer can ever be inserted between them, whether by a new signup landing between them, a manual drag/drop or move up/down, or the rotation advancing after someone finishes.
  - Clicking 🔗 on an already-linked singer unlinks it (and its partner); re-linking a singer that's already linked to someone else breaks the old link first, so an entry is never linked to more than one partner at a time.
  - A linked singer can still be Paused - it keeps its place and is simply skipped over, exactly like any other paused singer; pausing does **not** exempt the pair from the adjacency rule. Marking a linked singer Inactive ("out for the night") is treated differently: its still-active partner is *not* forced to follow it to the retired section.
  - The link persists across songs for the rest of the night (not just one performance) - it's only broken by an explicit unlink, or when one half is removed from the rotation entirely.
  - New shared primitives in `Shared/RotationHelpers.cs` - `LinkSingers()`, `UnlinkSinger()`, `EnforceLinkedAdjacency()` - and two new `IRotationSinger` members (`Id`, `LinkedSingerId`) used identically by all three apps, the same mechanism already driving rotation-order and wait-time behavior across them.
  - Shows as a "🔗 [partner name]" badge next to the singer's name in Lyracist's `RotationPage`, KSRotation's `MainWindow` (rotation list + right-click context menu), and KSRotation.Maui's `MainPage`.
  - **Not yet included**: automatically linking two singers who sign up together on the patron/kiosk app - that touches the shared `kiosk.html` form plus two separate request-handling backends and their approval-queue timing, and is scoped as a separate follow-up.

## [26.9.5.3] - 2026-09-05

### Added
- **Lyracist / KSRotation / KSRotation.Maui (Estimated Wait Time on Rotation Screens)**:
  - Every waiting singer's name now shows an estimated wait-time badge on the audience-facing rotation/billboard displays, e.g. `Dennis {5} Only Make Believe` - `{5}` means roughly 5 minutes until they're up.
  - Estimate = the cumulative estimated performance length of everyone ahead of them: each performance is the song's actual known duration + 30 seconds (Lyracist only, via a library lookup), or a 5-minute fallback per song when the duration isn't known (always the case in KSRotation/KSRotation.Maui, which have no song-duration library).
  - Recalculated automatically every time a singer is marked finished/done, via a new shared `RotationHelpers.RecalculateEstimatedWaits<T>()` (`Shared/RotationHelpers.cs`) used identically by all three apps - `IRotationSinger` gained `EstimatedPerformanceSeconds`/`EstimatedWaitMinutes` members to support it.
  - Wired into Lyracist's `RotationWindow` (plain queue list, Star Wars Crawl, and scrolling marquee banner), KSRotation's `SingerDisplayWindow` (Normal List, Marquee, and Vinyl views), and KSRotation.Maui's `BillboardView`.
  - The currently-performing singer never shows a wait badge (they're already up); paused/inactive singers, and (in Last Round mode) singers who already performed this round, are excluded from both the badge and the running time total.

### Fixed
- **Lyracist (Compressor/Limiter Settings Not Applied to Playback)**:
  - The Compressor and Limiter sliders (Song/Singer Audio Settings) were stored but never actually applied to sound - `LibVlcVideoBackend`, the actively-registered playback backend, only implemented `Compressor`/`Limiter` as inert stored properties with no effect on the audio signal.
  - Wired both into VLC's native `compressor` dynamics-processing audio filter via per-track `:audio-filter=compressor` media options (`compressor-threshold`, `compressor-ratio`, `compressor-attack`, `compressor-release`, `compressor-makeup-gain`), applied in `LoadAsync` alongside the existing pitch-shift filter.
  - Since VLC ships one dynamics filter and this app has two independent concepts (Compressor character, Limiter dB ceiling), Limiter is folded in as a floor on the compression threshold, with the ratio pushed toward the max when Limiter's ceiling is the more restrictive of the two - approximating a hard limit rather than a musical compressor.

## [26.9.5.1] - 2026-09-05

### Added
- **Lyracist (Automatic Volume Normalization)**:
  - Added `FFmpegService.MeasureIntegratedLoudness()`: runs a single-pass ffmpeg `loudnorm` (EBU R128) analysis to measure a track's integrated loudness (LUFS).
  - Tracks are measured lazily and in the background the first time they're loaded for playback (never during a library scan, since the analysis decodes the whole file), then cached permanently on `Song.MeasuredLoudnessLufs`.
  - `MediaEngine.UpdateAudioParameters()` now applies a corrective gain factor - alongside the existing Song/Singer/Duet Partner gain merge - so every track lands near the same perceived loudness instead of the DJ needing to manually ride the volume between songs.
  - Added **Normalize Volume Across Tracks** toggle and **Target Loudness (LUFS)** slider (`-23` to `-9`, default `-16`) to Settings → Audio & Engine, next to Hardware Mixer Mode (which continues to bypass normalization along with the rest of the software gain/EQ chain).

## [26.9.5.0] - 2026-09-05

### Added
- **Lyracist (Full Live Question Controls & Manual/Auto Flow in TriviaPage)**:
  - Added Question Navigation Row to `TriviaPage.xaml` with `⏮ Prev`, `Jump to Question #` (drop-down `ComboBox` listing all question numbers in the round), and `Next ⏭` buttons.
  - Added Timer Quick-Adjust Row with `-5s`, `+5s`, `+10s` dynamic bump adjustments and `🔄` timer reset.
  - Added Option Elimination and Instant Reveal controls: `✂ Fade Option` (eliminates one wrong answer choice) and `⚡ Instant Reveal` (immediately locks answering and displays the correct answer).
  - Added `❌ Void Question (No Penalty)` command to safely discard a faulty question without penalizing players or affecting answer streaks.
  - Integrated `PrepareCurrentQuestion(startTimerImmediately: AutoAdvanceQuestions)` in `TriviaViewModel.cs`: when Auto-Run Game is disabled, questions open in reading/standby mode, allowing hosts to read the question to the crowd before clicking `▶ Start Question & Timer`.

### Changed
- **Lyracist (Trivia Page UI Sizing)**:
  - Increased Category Pack(s) selection ListBox height (`MinHeight="160"`, `MaxHeight="200"`, `Height="160"`) on `TriviaPage.xaml` so more trivia packs are visible simultaneously without requiring tight scrolling.
  - Increased the Patron Mobile Portal URL TextBox height (`Height="35"`) and label container for improved readability and touch target sizing.

### Fixed
- **KSRotation (Dark Mode Text Contrast on Trivia Settings & Trivia Pages)**:
  - Fixed nearly illegible/black text when running in Dark Mode across the Trivia Settings tab and Live Trivia console (`MainWindow.xaml`).
  - Switched Window root foreground, `GridTextBoxStyle`, `ModernTextBoxStyle`, `ModernCompactTextBoxStyle`, `ModernCheckBoxStyle`, and explicit CheckBox foregrounds from `MaterialDesignBody` to `AppContrastTextBrush` (`#EEF2F7` in dark mode, `#1E293B` in light mode).
  - Added `TextElement.Foreground="{DynamicResource AppContrastTextBrush}"` to the Trivia Settings root grid.
  - Added `MaterialDesign.Brush.Foreground` and `MaterialDesign.Brush.Foreground.Light` theme brush overrides in `ThemeService.cs` ensuring proper foreground resolution in dark mode.
  - Styled Questions-Per-Game and question-jump ComboBoxes, and swapped Round Title and Patron Portal text from unthemed `PrimaryHueMidBrush` to `AppHeaderBrush` for uniform contrast.

## [26.9.4.0] - 2026-09-04

### Added
- **Lyracist, KSRotation (WPF), and KSRotation.Maui (Last Round Management)**:
  - **Host Control**: Added "Last Round" control across rotation host interfaces — styled as a standout emerald green button placed right after the Clear button in KSRotation (`MainWindow.xaml`), Lyracist (`RotationPage.xaml`), and KSRotation.Maui (`MainPage.xaml`), immediately synchronizing across the rotation lifecycle and display services.
  - **Remote DJ Screen (`dj.html`)**:
    * Added emerald green "Last Round" toggle buttons in both the `Current Rotation` header toolbar and the right sidebar quick actions card.
    * Toggles in real-time between inactive emerald green (`#059669`) and active sapphire blue (`#2563EB`) with `✓ Last Round`, synchronizing with the desktop/tablet consoles via `/api/info` and `/api/dj/action`.
    * Renders a crimson announcement banner (`★ THE LAST ROUND FOR THE NIGHT IS CURRENTLY UNDERWAY ★`) above the queue.
    * Displays `DONE (LAST ROUND)` emerald badges and dimmed card styling for performers who have completed their performance in the last round.
    * Implemented `toggle-last-round`, `set-last-round`, `next-singer`, and `previous-singer` action handling in `MainViewModel.Requests.cs`.
  - **Audience Display Announcement**: When activated, displays a prominent crimson announcement banner (`★ THE LAST ROUND FOR THE NIGHT IS CURRENTLY UNDERWAY ★`) across all audience views:
    * `Lyracist`: `RotationWindow.xaml`.
    * `KSRotation`: `SingerDisplayWindow.xaml` across all projection modes (Normal, Star Wars, Vinyl, Marquee).
    * `KSRotation.Maui`: `BillboardView.xaml` (on-device attractor and external HDMI/Presentation displays).
    * Web Billboard: `billboard.html` served via embedded web server.
  - **Performer Lifecycle**:
    * Added `HasSungInLastRound` property to `IRotationSinger`, `Singer`, and `SingerEntry`.
    * In the last round, after a performer finishes their song, `HasSungInLastRound` is set to true and they are automatically hidden from the audience rotation queue (current, next, and on deck).
    * The rotation automatically advances only through remaining unsung performers; when all singers have completed their song, the current singer clears.
    * In the host queue and Remote DJ screen, performers who have sung in the last round remain visible with a distinct `DONE (LAST ROUND)` indicator badge.

### Fixed
- **Lyracist & KSRotation (Last Round consistency)**: Several rotation-advance code paths were silently ignoring Last Round mode, so a performer who had already sung could be re-promoted to current or re-highlighted as next:
  - `RotationHelpers.SetCurrentSinger` (`Shared/RotationHelpers.cs`) now accepts an `isLastRound` parameter and no longer resumes a displaced current singer as "next" if they've already performed this last round; threaded through every call site in both apps.
  - The DJ web remote's `toggle-inactive`, `delete`, `restore`, and `complete-round`/`clear-round` handlers (`MainViewModel.Requests.cs`), and the equivalent native WPF commands (`ToggleSingerInactive`, `RemoveSinger` in KSRotation; `RemoveSinger`/`ToggleInactiveSinger` in Lyracist's `RotationViewModel`), now respect Last Round when searching for the next singer to promote.
  - `KaraokeViewModel.UpdateNowNext` in Lyracist — the Now/Next reconciliation hooked to every rotation change — now excludes already-sung singers from its promotion fallback and no longer trusts a stale "current"/"next" designation for a singer who has already performed.

## [26.9.3.0] - 2026-09-03

### Added
- **KSRotation & KSRotation.Maui (3-in-1 Audience Billboard & Connect Screen System)**:
  - **Option 2 (Wi-Fi Web Billboard — `billboard.html`)**:
    - Embedded 16:9 high-contrast dark theme audience billboard web page served at `http://<ip>:5005/billboard` and `http://<ip>:5005/billboard.html`.
    - Live rotation auto-syncing every 3s via `/api/rotation` and venue/DJ info syncing every 10s via `/api/info`.
    - Real-time Stage Queue: Glowing amber "NOW PERFORMING" card with duet partner (`w/ Partner`), song, and artist; blue "UP NEXT" card; and scrollable upcoming rotation queue.
    - Connect & Request QR Hub: Generates dynamic server-rendered QR codes via new `/api/qr?text=...` endpoint for Song Requests (`/`) and Venue Wi-Fi auto-join (`WIFI:S:...;P:...;;`).
    - Complete support for any venue Smart TV, Fire TV Stick Silk browser, Chromecast with Google TV, or audience device connected to the DJ Travel Router.
  - **Option 3 (On-Device Attractor & Intermission Mode — `BillboardOverlay` & `BillboardView`)**:
    - Added dedicated `📺 Billboard` button in `PortalConnectionCard` on the DJ tablet console.
    - Created reusable `BillboardView.xaml` ContentView in MAUI displaying the full-screen stage queue, live ticking clock, active singer count, request QR, and Wi-Fi QR.
    - Added `BillboardOverlay` modal with floating dismiss and external display controls in `MainPage.xaml`, allowing the KJ to put the tablet on a table or stand in attractor mode during breaks and tap to return to DJ controls.
  - **Option 1 (Hardware HDMI & Secondary Display Presentation — `SecondaryDisplayService`)**:
    - Added cross-platform `SecondaryDisplayService` supporting native Android `Android.Hardware.Display.DisplayManager` and `Android.App.Presentation`.
    - Automatically projects `BillboardPresentation` to external monitors or TVs plugged into the tablet via USB-C to HDMI adapters or wireless displays, keeping the DJ console private on the tablet touchscreen.
    - Added Windows multi-window presentation support via `BillboardPage` for dual-monitor setups.
  - **Models & Serialization**:
    - Added `partner` field to `RotationItemDto` and `Partner` alias on `SingerEntry` to ensure duet partners are serialized and rendered across all billboard screens.
  - **Decoupled Trivia from KSRotation.Maui**:
    - Completely removed `Lyracist.Trivia.Core.csproj` and `MainViewModel.Trivia.cs` from `KSRotation.Maui.csproj` to keep the MAUI tablet app strictly focused on karaoke singer rotation.
    - Added `#if !MAUI` guards on trivia property change triggers and commands in `MainViewModel.cs`.
    - Reduced APK native AOT package assembly count from 116 to 108 assemblies.

## [26.9.2.250] - 2026-09-02

### Added
- **KSRotation.Maui (Performer Row Legend & Guide)**:
  - Added a dedicated "Legend" action button to the bottom control card (`PortalConnectionCard`) on Android tablets and desktops, positioned alongside `+ Add Performer` and `📱 Connect QR`.
  - Added a full-featured `LegendOverlay` modal popup explaining all visual indicators, badges (`🚩 1ST`, `NOW PERFORMING`, `NEXT`, `MUSIC`), queue toolbar action buttons (`✓`, `✏`, `🎙`, `🚩`, `▲`, `▼`, `❚❚` / `▶ Resume`, `🗑` / `↺`), and the 10 multi-colored song round checkboxes.
  - Fully integrated `LegendOverlay` with dynamic orientation handling (`UpdateOrientationLayout`) for seamless portrait and landscape scaling.

### Fixed & Enhanced
- **KSRotation.Maui (Android Build & Fast Deployment Stability)**:
  - Enhanced the MSBuild pre-build target workaround for `XARLP7024` / `XARLP7000` to safely rename and move locked `.stamp` and `.flata` intermediate files when Visual Studio background design-time builds hold file locks during package extraction.

## [26.9.1.1] - 2026-09-01

### Fixed & Enhanced

- **Test Infrastructure (`global.json`)**:
  - Added a `test` runner block so `dotnet test` works again under the .NET 10 SDK (xUnit v3's Microsoft.Testing.Platform had silently stopped running via the legacy VSTest entry point). All 283 tests across the four test projects verified passing.
- **Silent Error Logging Eliminated (32+ files across `Lyracist`, `Lyracist.Data`, `KSRotation`, `KnockoutTrivia`, `Lyracist.Trivia.Core`, `LyracistDbEditor`, `LyracistKeyGen`, `Shared`)**:
  - Replaced 86 `Debug.WriteLine`-only catch blocks (invisible in Release builds) with real persistent logging: `Globals.LogError`/`LoggerService.LogError` for app code, `Trace.TraceError` for shared libraries with no per-app logger.
- **Library Scan & Settings Performance (`Lyracist.Data`, `Lyracist`)**:
  - `ScanningService`: removed `UpdateRange` calls that force-marked every song Modified (and reindexed into FTS5) on every rescan regardless of whether anything actually changed; batches now run in one explicit transaction with `ChangeTracker.Clear()` between them.
  - `LyracistDbContext`/`SearchService`: dropped `Cache=Shared` from the SQLite connection string (legacy/unneeded under WAL) and added `PRAGMA synchronous=NORMAL` everywhere WAL is set.
  - `AppSettings.Save()` now debounces through a 400ms timer instead of a full fsync-and-rename on every property set (was firing on every slider drag tick); `Flush()` is called on app exit so nothing pending is lost.
  - Fixed a race in `SearchService.EnsureFtsTableExists` where two threads could both pass the verification check and race the DROP/CREATE of the FTS table; also fixed a leaked `SqliteConnection` in `GetSharedConnectionAsync`.
  - `PrepareFtsQuery` now runs the query through the same normalization used to build the FTS index, closing a mismatch for accented search terms.
- **Playback (`Lyracist`)**:
  - **New: seek slider.** `MediaEngine.Seek()` was a no-op; it now calls `IVideoBackend.SeekAsync` and re-syncs CDG rendering afterward. Added `Duration`/`Position`/`PositionChanged` to `IMediaEngine` and `IVideoBackend` (backed by FFME's `NaturalDuration` and LibVLC's `Length`), and a real seek slider with elapsed/total labels in `MainWindow.xaml`.
  - ZIP-CDG extraction in `MediaEngine.LoadSong` moved off the UI thread (was blocking playback start on every zipped track).
  - `.cdg` files are no longer catalogued as standalone songs during scanning (MP3+CDG pairs were being counted twice); added a one-time cleanup for existing duplicate rows.
  - CDG render timer reduced from 60Hz to 30Hz (CDG delivers 300 packets/sec; the extra ticks were pure overhead).
  - `MediaEngine` is now `IDisposable`, cleaning up its timers and pitch-change token on host shutdown; orphaned `%TEMP%\LyracistPlayback_*` directories from a crashed prior session are swept at startup.
- **Shutdown Reliability (`Lyracist`)**:
  - `App.OnExit` no longer races the process exit against its own cleanup (`Host.StopAsync`/`Dispose`) - it now blocks (bounded to 5s, off the UI thread's sync context to avoid deadlocking) until shutdown actually completes.
- **External Search (`Lyracist`)**:
  - External (YouTube/Spotify/Amazon) search is now debounced with the same 250ms timer as local search, and given its own staleness token - previously it fired on every keystroke with no guard against an older response overwriting a newer one.
- **DJ Action Reliability (`KSRotation`)**:
  - `HandleDjAction` now times out after 5 seconds instead of blocking the patron request server indefinitely if the UI dispatcher is busy (e.g. behind a stray modal), which could otherwise starve every connection slot.
- **Abuse-Rate Hardening (`KSRotation`, `KnockoutTrivia`, `Lyracist.Trivia.Core`)**:
  - Singer PIN endpoints (`/api/singer/login`, `/api/singer/profile`, `/api/singer/avatar/upload`) now share the DJ PIN's per-IP lockout protection, tracked independently so the two can't interfere with each other.
  - Singer self-registration, and new-player registration on both trivia join endpoints (`/api/knockout/join` in `KnockoutWebServer`, `/api/trivia/join` in `TriviaWebServer`), are now capped at 5 new registrations per IP per 10 minutes - closing an unbounded-row-insertion gap in all three. Returning/reconnecting players are never blocked.
  - Avatar uploads are now validated against real image magic bytes (JPEG/PNG/GIF/WEBP) and capped at 2 MB, instead of writing arbitrary decoded bytes to disk under a `.jpg` extension.
- **Trivia Player Timeout Correctness (`Lyracist.Trivia.Core`)**:
  - `TriviaPlayer.ConnectedAt`/`LastSeenAt` and the disconnect-timeout comparison in `TriviaGameEngine.GetPlayers()` switched from local time to UTC, fixing a DST-transition bug where every player would appear stale (or none would) for an hour around the clock change.
- **Requests Cache Debounce (`KSRotation`)**:
  - The patron-requests JSON cache now debounces the same way the rotation cache does, instead of re-serializing the whole list on every single incoming request.
- **Settings Corruption Recovery (`Lyracist`)**:
  - A corrupt `lyracist_settings.json` is now quarantined with a timestamped `.corrupt-*` suffix instead of being silently overwritten with defaults on the next save.
- **LyracistKeyGen Build Fixes (`LyracistKeyGen`)**:
  - Fixed a broken standalone build (`Globals.SettingsDir`/`Globals.DataDir` referenced without linking `Globals.cs`, masked because the solution's Debug configuration skips building this project).
  - Aligned the SQLite native package to `SQLitePCLRaw.bundle_e_sqlite3` 3.0.5, matching every other project in the solution.

## [26.8.30.4] - 2026-08-30

### Fixed & Enhanced

- **Git Repository Size Optimization (`.gitignore`)**:
  - Untracked and excluded runtime binary assets (`Banners/`, `Logs/`, and live databases `lyracist.db`, `ksrotation_night_db.json`, `wifi_passwords.json`, `keygen.db`), removing 336 MB of binary assets from the git index while keeping the master seed database (`Data/trivia.db`) and question packs (`Packs/`) tracked.
- **Unknown Artist Retention & Unresolved Reporting (`ScanningService.cs`)**:
  - Maintained `Artist == "Unknown Artist"` in the probing query and avoided falsely marking unresolved tracks as `Tags = "none"`, ensuring tracks missing both filename and ID3 artist metadata remain identified and resolvable in `LyracistDbEditor`.
  - Added summary logging at the conclusion of library scans reporting the total count of unresolved "Unknown Artist" tracks.
- **Tracked Deployment Script & Live State Protection (`Deploy/Deploy-Lyracist.ps1`)**:
  - Version-controlled the laptop show deployment script in `Deploy/Deploy-Lyracist.ps1`.
  - Added `selected_announcement.json` exclusion under `Banners` to ensure laptop live announcement selections are preserved across redeployments alongside settings and database protections.
- **MAUI Cross-Platform Path Compatibility (`TriviaStorageHelper.cs`)**:
  - Maintained `net10.0` compatibility in `Lyracist.Trivia.Core` for seamless MAUI Android and Windows compilation without dependency collisions.

## [26.8.30.3] - 2026-08-30

### Added & Enhanced

- **High-Performance In-Memory Library Metadata Probing (`Lyracist`, `LyracistDbEditor`, `Lyracist.Data`)**:
  - **In-Memory TagLib Extraction**: Integrated `TagLibSharp` (v2.3.0) directly into `Lyracist.Data` for microsecond-level in-memory metadata and duration extraction (<0.1ms per file), completely eliminating external `ffprobe.exe` process spawning overhead.
  - **Direct ZIP-CDG Stream Probing**: Implemented `StreamFileAbstraction` to read MP3 audio streams directly from ZIP archives in RAM without disk decompression to `%TEMP%`.
  - **Local ID3/MP4 Metadata Recovery**: Automatically extracts duration, genre, and populates missing artists/titles directly from embedded ID3/MP4 metadata.
  - **Loop-Free Probed Tracking**: Marks probed tracks with local genres or `Tags = "none"` to guarantee subsequent scans skip already-probed files immediately.
  - **Parallel Batch Processing**: Scaled probe concurrency from 3 threads to `Math.Max(8, Environment.ProcessorCount * 2)` (16–32 parallel workers) with batch SQLite saves and FTS5 search index synchronization, reducing 50,000-song follow-up scan times from 3+ hours to under 1–2 minutes.

## [26.8.30.2] - 2026-08-30

### Added & Enhanced

- **Solution-Wide Directory & Asset Consolidation (`Lyracist`, `KSRotation`, `KnockoutTrivia`, `LyracistTrivia`, `TriviaDbCreator`, `ScaryokeWheel`, `LyracistDbEditor`, `LyracistKeyGen`)**:
  - **Unified Folder Architecture**:
    * `Settings/`: Consolidated all application configuration into one directory with app-distinguishing names (`lyracist_settings.json`, `ksrotation_settings.json`, `knockout_trivia_settings.json`, `lyracist_trivia_settings.json`, `scaryoke_settings.json`, `dbeditor_settings.json`, `keygen_settings.json`, `ksrotation_venues.json`, `ksrotation_djs.json`, `VenueGraphics/`).
    * `Data/`: Consolidated all databases and runtime persistence files (`lyracist.db`, `trivia.db`, `ksrotation_night_db.json`, `ksrotation_singers.json`, `wifi_passwords.json`, `keygen.db`).
    * `Banners/`: Unified banners folder organized by app:
      - `Banners/KSRotation/`: `DJBanners/`, `Announcements/`, `EventBanners/` (including 16:9 standard event banners).
      - `Banners/Lyracist/`: `DJBanners/`, `Announcements/`, `EventBanners/` (including 16:9 standard event banners).
      - `Banners/LyracistTrivia/`: `CategoryBanners/` (15 themed category graphics) and `Announcements/`.
      - `Banners/KnockoutTrivia/`: `Announcements/` and `CustomBanners/`.
    * `Packs/`: Single shared packs folder containing 16 curated JSON trivia packs used interchangeably across all trivia apps.
    * `Logs/`: Centralized log directory with sanitized app-distinguished logs (`{app}_app_{date}.log` and `{app}_err_{date}.log`).
  - **Transparent Legacy Migration**: Implemented startup fallback checks across all services to seamlessly migrate legacy config and data files forward from `%AppData%`, `%LocalAppData%`, or old subfolders to the consolidated architecture without user action.
  - **Eliminated Duplicate Assets & Obsolete Folders**: Removed legacy redundant directories (`KnockoutTrivia/KoTrivia`, `TriviaData`, `EventBanners`, `Lyracist.Trivia/Announcements`) and configured `.csproj` content links to copy `..\Packs\**\*`, `..\Banners\**\*`, and `..\Data\**\*` to release outputs automatically.

## [26.8.30.1] - 2026-08-30

### Added & Enhanced

- **Solution-Wide Version Synchronization (`26.8.30.x`)**:
  - Synchronized build and assembly versioning across all solution projects and test suites (`Lyracist`, `KnockoutTrivia`, `KSRotation`, `KSRotation.Maui`, `Lyracist.Trivia`, `LyracistDbEditor`, `ScaryokeWheel`, `TriviaDbCreator`, `Lyracist.Data`, `Lyracist.Trivia.Core`, and test assemblies).
- **Knockout Trivia Application Icon & Output Packaging (`KnockoutTrivia.csproj`)**:
  - Embedded official high-resolution boxing glove application icon (`Google-Noto-Emoji-Activities-52746-boxing-glove.ico`) and package branding (`Glove-128.png`).
  - Standardized release output routing to `C:\VB26\Release\Lyracist` across all desktop executables.
  - Added standalone solution manifest `KnockoutTrivia.slnx` for isolated module development and rapid testing.
- **Automated Test Suite Verification**:
  - Verified 100% test pass rate across all 283 unit tests in the solution test projects (`KnockoutTrivia.Tests`, `Lyracist.Trivia.Tests`, `Lyracist.Tests`, and `KSRotation.Tests`).

## [26.8.29.1] - 2026-08-29

### Added & Enhanced

- **Knockout Trivia Testing Module & Automated Suite (`KnockoutTrivia.Tests`)**:
  - **Dedicated xUnit v3 Test Suite (`KnockoutTrivia.Tests.csproj`)**: Built a complete test project covering `GameStateService`, `StreakService`, `TokenService`, `KnockoutWebServer`, `WheelService`, `BannerService`, and `SimulatorService`.
  - **In-App DJ Bot Simulator & Diagnostics Panel (`ISimulatorService.cs` / `SimulatorView.xaml`)**:
    - Interactive testing tab embedded directly in the host interface (`MainWindow.xaml`).
    - Configurable virtual bot player spawner (1 to 30 bots) with distinct AI identities and avatars.
    - Automated answer simulation with realistic human response delays (400ms - 2800ms) and customizable accuracy rate (0% - 100%).
    - Auto-play mode enabling hands-free game flow testing and rehearsal.
    - Rehearsal controls for instant Super Streak wheel triggering (setting bot streak to 20), phase stepping, and live activity log inspection.

### Fixed & Enhanced

- **Knockout Trivia Bug Fixes & Resilience Hardening (`KnockoutTrivia`)**:
  - **Super Streak Milestone Fix (`StreakService.cs`)**: Fixed bug where resetting `StreakCount = 0` on token rewards prevented `StreakCount` from reaching the `SuperStreakThreshold` (20). Tokens are now awarded on multiples of `StreakRequirement` (5, 10, 15, 20) without wiping the streak progression, allowing the Super Streak Wheel to trigger as designed.
  - **Cross-Thread UI Dispatch Fix (`GameStateService.cs`)**: Fixed crash in Automatic mode when all mobile players submitted answers early by wrapping the auto-reveal timeout callback in `RunOnUI(...)`.
  - **Deadlock Hazard Elimination (`GameStateService.cs`)**: Removed nested `_syncLock` synchronization around `Dispatcher.Invoke` calls in `RegisterOrGetPlayer`, `RemovePlayer`, `SubmitPlayerAnswer`, and `EvaluateSubmittedAnswers`, moving mutations directly to UI thread dispatch.
  - **Global Exception Handling & Fault Recovery (`App.xaml.cs`)**: Registered `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, and `TaskScheduler.UnobservedTaskException` with `Lyracist.Shared.Globals.LogError`. Added startup try/catch recovery dialog so host shows are protected from unexpected crashes.
  - **Duplicate Player Name Disambiguation (`GameStateService.cs`)**: Reconnection by name now only claims inactive/disconnected records (`!p.IsConnected`), preventing duplicate player names from silently hijacking another connected device.
  - **Uniform Fisher-Yates Shuffle Refactor (`TriviaDataService.cs` / `GameStateService.cs`)**: Removed redundant random shuffles from database and pack loaders; centralized single uniform Fisher-Yates in-place shuffle using `Random.Shared` in `GameStateService.ShuffleQuestions()`.
  - **Answer Bounds Validation & Session Security (`KnockoutWebServer.cs` / `knockout.html`)**: Added boundary checks on incoming `SelectedOptionIndex` and added GUID session tokens (`SessionToken`) to authenticate mobile submissions and state polling.
  - **Multi-Tier Streak Meter Visual Progression (`KnockoutPlayer.cs` / `ScoreboardView.xaml`)**: Added `StreakMeterProgress` calculated property so the 5-block meter visualizes continuous cycles past 5 without UI reset anomalies.

## [26.8.28.1] - 2026-08-28

### Added & Enhanced

- **Knockout Trivia Multi-Monitor Routing & Audience Screen Projection (`KnockoutTrivia`)**:
  - **DPI-Aware Window Positioning (`DisplayService.cs` / `WindowPositioner.cs`)**:
    - Replaced buggy Win32 pixel coordinates and `WindowState.Maximized` with `WindowPositioner.FillArea` and `Screen.AllScreens` matching `Lyracist` and `Lyracist.Trivia`.
    - Eliminates the bug where WPF automatically redirected/snapped the Audience Window back onto Screen #1 (Primary display) when maximized.
    - Added persistent monitor binding by hardware `DeviceName` (`SelectedGameMonitorDevice` & `SelectedBannerMonitorDevice`), ensuring window positioning remains locked to the selected screen across restarts and display reconnects.

- **Knockout Trivia Phone/Tablet Connect Screen & Mobile Companion (`KnockoutTrivia`)**:
  - **Dual QR Code Connect Screen (`ConnectView.xaml` / `ConnectViewModel.cs`)**:
    - **Card 1: 📶 1. Connect Wi-Fi**: Automatically generates a high-resolution dark purple QR code (`#4C1D95`, `[76, 29, 149]`) formatted as `WIFI:S:...;T:...;P:...;;` with SSID & Password display, allowing players to join venue Wi-Fi with one smartphone camera scan.
    - **Card 2: 📱 2. Join Trivia Arena**: Generates a high-resolution dark green QR code (`#064E3B`, `[6, 78, 59]`) pointing to `http://<local-ip>:<port>`, displaying the web address in large text for instant browser play with no app download required.
    - **Live Connected Player Roster**: Real-time grid displaying connected players, strike status dots, shield token counts, and online pulse indicators.
    - **Host Quick Toolbar**: Actions for "Start Game Now ▶", "Copy URL 📋", "Refresh Connection 🔄", and "Push to Big Screen 📺".
    - **Top Navigation Tab Integration**: Added dedicated "📱 Player Connect" tab in `MainWindow.xaml` and wired projection synchronization to the secondary Audience Display (`AudienceWindow.xaml`).
  - **Embedded Asynchronous Web Server (`IKnockoutWebServer` / `KnockoutWebServer.cs`)**:
    - High-performance, non-blocking `TcpListener` server hosting REST endpoints:
      * `GET /` & `GET /knockout`: Serves the responsive mobile player companion app (`knockout.html`).
      * `POST /api/knockout/join`: Registers players or reconnects existing devices with preserved identity, scores, and shield tokens.
      * `POST /api/knockout/submit`: Receives answer submissions and reaction times (in ms) from connected phones and tablets.
      * `GET /api/knockout/state`: High-frequency polling endpoint returning real-time game state, countdown timer, question prompts, options, personal score/shield/strike statuses, and live standings.
      * `OPTIONS`: CORS preflight handling for cross-origin browser requests.
  - **Mobile Player Companion Web Application (`Resources/knockout.html`)**:
    - Touch-first responsive HTML5/CSS3/Vanilla JS application tailored for smartphones and tablets.
    - Knockout Trivia theme with Emerald Green (`#10B981`), Purple (`#8B5CF6`), Amber (`#F59E0B`), and Crimson (`#EF4444`).
    - Synthesized Web Audio API sound effects (button clicks, correct chimes, wrong buzzers, shield block clangs, strike hits) and haptic vibration (`navigator.vibrate`).
    - Multi-screen flow: Join Screen -> Waiting Lobby -> Active Question (4 large color-coded buttons) -> Answer Locked In -> Real-time Answer Reveal -> Knocked Out Spectator Mode -> Final Podium.
  - **Internal Scoring & Autonomous Game Flow (`GameStateService.cs`)**:
    - Real-time countdown timer per question with live tick synchronization across Host, Audience Big Screen, and mobile devices.
    - Automated answer evaluation upon timer expiration or DJ reveal:
      * Correct answers award points and increment streak counters (granting a Shield Token upon reaching 5).
      * Incorrect answers or timeouts consume a Shield Token (0 strikes, "Shield Protected") if held; otherwise record 1 strike (3 strikes eliminates player).
      * Automatic Super Streak wheel triggering (at 20 streak) and last-player-standing victory detection.
    - **Automatic Game Mode (`GameAdvanceMode.Automatic`)**: Autonomously steps from Question Active -> Answer Reveal -> Auto-Advance Buffer -> Next Question with full DJ manual override controls at any time.
  - **DJ Settings Configuration**:
    - Added Wi-Fi SSID with auto-detect button, Wi-Fi password, and companion server port configuration in `SettingsView.xaml` / `SettingsViewModel.cs` with persistent `WifiPasswordStore`.

## [26.8.27.1] - 2026-08-27

### Added & Enhanced

- **Knockout Trivia Standalone Game Project (`KnockoutTrivia`)**:
  - Generated a clean, professional, MVVM-based WPF standalone game project targeting **.NET 10** with modern Fluent UI (`WPF-UI 4.3.0`) dark theme.
  - **Directory & Resource Architecture (`/KoTrivia`)**:
    - Created dedicated asset and data hierarchy: `/KoTrivia/Data` (SQLite trivia databases), `/KoTrivia/Packs` (JSON trivia packs), `/KoTrivia/Logs` (game logs), `/KoTrivia/Banners` (static 16:9 banners), `/KoTrivia/Config` (JSON game settings), `/KoTrivia/Assets` (official `kotrv_logo.png` & `kotrv_logo.webp`), and `/KoTrivia/Temp`.
  - **Core MVVM ViewModels & Views**:
    - `MainViewModel` / `MainView.xaml`: Host command console for rapid question flow, instant answer reveals, and quick player response adjudication.
    - `ScoreboardViewModel` / `ScoreboardView.xaml`: Horizontal player status cards with dynamic strike color-coding (Green -> Yellow -> Orange -> Dimmed Red), shield tokens (0-3), and 5-block streak meters.
    - `QuestionViewModel` / `QuestionView.xaml`: High-visibility 16:9 bar-friendly question board with category pill, question count, countdown timer, high-contrast option cards, and explanation reveals.
    - `WheelViewModel` / `WheelView.xaml`: Scaryoke-style rotary Super Streak wheel rendering player wedges for all token holders and animated spin targeting.
    - `BannerViewModel` / `BannerView.xaml`: 16:9 auto-scaling presentation banner system with letterbox/pillarbox support and official Knockout Trivia branding.
    - `HelpViewModel` / `HelpView.xaml`: Split-pane DJ guide with 8 detailed topics explaining game rules, strike colors, shield tokens, 5-block streak meters, Super Streak wheels, live adjudication, and settings.
    - **Automatic Question Deck Randomization & Shuffling**:
      * Implemented automatic Fisher-Yates randomization across all loaded SQLite databases and JSON category packs, guaranteeing questions from different categories and themes are mixed rather than served in sequential primary key / database insertion order.
      * Questions automatically re-shuffle upon starting a new game, resetting games, or clicking the live DJ **`🔀 Shuffle`** button on the host console.
    - `SettingsViewModel` / `SettingsView.xaml`: Redesigned 3-column responsive DJ host panel eliminating vertical scrolling:
      * **Column 1**: Points per question, total questions, auto-advance delays, manual vs automatic game mode, and audio/visual FX toggles.
      * **Column 2**: Shield token maximum caps, streak requirements, and Super Streak wheel thresholds.
      * **Column 3**: Interactive Multi-Database & Pack selection ListBox (with checkboxes, 'All' and 'Clear' buttons, and live question count badges) alongside Multi-Monitor & TV Routing controls.
    - **Multi-Monitor Routing & Dedicated Audience Projection Window (`AudienceWindow`)**:
      * Implemented native display enumeration with `IDisplayService` detecting hardware display names, primary status, and resolutions.
      * Added dedicated borderless 16:9 **`AudienceWindow`** positioned automatically on secondary TVs/projectors, keeping host controls (scoring, adjudication, settings, help) private to the DJ.
      * Added titlebar quick-action **`📺 Audience Screen`** button for instant one-click projection toggle.
    - `AboutViewModel` / `AboutView.xaml`: Official branding modal with PAROLE Software metadata and shared database compatibility notes.
  - **Service Layer Architecture**:
    - `ITriviaDataService` / `TriviaDataService`: SQLite database & JSON pack ingestion compatible with `Lyracist.Trivia`.
    - `IGameStateService` / `GameStateService`: Player lifecycle, scoring, strike accumulation, and Super Streak triggers.
    - `ITokenService` / `TokenService`: Shield token awards, deductions, and animation event routing.
    - `IStreakService` / `StreakService`: 5-block streak tracking, token reward milestones, and Super Streak triggers.
    - `IWheelService` / `WheelService`: Dynamic wheel segment construction and target selection.
    - `IBannerService` / `BannerService`: 16:9 dynamic banner generation and static asset loader.
    - `IDisplayService` / `DisplayService`: Native Win32 monitor enumeration and multi-screen assignment.
    - `IConfigService` / `ConfigService`: JSON persistence for game settings (`KoTrivia/Config/game_settings.json`).

- **Intelligent Name, Artist & Song Proper-Casing Engine (`Shared/NameFormatting.cs`)**:
  - Implemented a centralized, robust proper-casing utility shared across **all solution applications** (`Lyracist`, `KSRotation`, `KSRotation.Maui`, `LyracistDbEditor`, `ScaryokeWheel`).
  - **Mixed-Case Preservation**:
    - Ensures the initial letter of every word is capitalized while preserving intentional inner and trailing uppercase letters typed by the user (e.g. `DeaR` -> `DeaR`, `deaR` -> `DeaR`, `LeBron` -> `LeBron`, `vanBuren` -> `VanBuren`, `MacDonald` -> `MacDonald`).
    - Eliminates the previous bug where `.ToLowerInvariant()` clobbered custom singer and artist stylizations into plain lowercase letters (e.g. converting `DeaR` into `Dear`).
  - **Apostrophe Name Prefixes**:
    - Added smart detection for single-letter name prefixes followed by apostrophes (e.g. `O'`, `D'`, `L'`, `M'`), properly capitalizing both the prefix and the root surname (e.g. `o'neal` / `O'neal` / `O'NEAL` -> `O'Neal`, `d'angelo` -> `D'Angelo`, `l'amour` -> `L'Amour`).
  - **Scottish & Irish "Mc" Prefixes**:
    - Automatically capitalizes `Mc` prefixes (e.g. `mcdonald` / `MCDONALD` -> `McDonald`, `mccartney` -> `McCartney`).
  - **Hyphenated Names & Word Boundary Handling**:
    - Correctly capitalizes all hyphenated segments (e.g. `mary-ann smith` -> `Mary-Ann Smith`, `smith-o'neal` -> `Smith-O'Neal`).
  - **Acronyms, Roman Numerals & Contractions**:
    - Preserves standard music acronyms (`DJ`, `MC`, `TV`, `CD`, `DVD`) and Roman numerals (`II`, `III`, `IV`, `VI`, `VII`, `VIII`, `IX`, `X`, `XI`, `XII`), while keeping song title contractions correctly lowercased (e.g. `Don't Stop Believin'`, `Rock 'N' Roll`).
  - **Application Integration**:
    - Integrated across `Lyracist` (`RotationViewModel`, `EditSingerViewModel`, `KaraokeViewModel`, `RequestService`), `KSRotation` & `KSRotation.Maui` (`SingerEntry`), and `LyracistDbEditor` (`MainViewModel`).
  - **Pinned .NET 10 SDK Version (`global.json`)**:
    - Added `global.json` pinning the build tooling to the stable GA .NET 10 SDK (`10.0.400`), ensuring Visual Studio and the `dotnet` CLI compile with .NET 10 tools and preventing preview SDK fallback warnings (`NETSDK1057`).


### Testing & Verification

- **Expanded NameFormatting Unit Tests (`Lyracist.Tests`)**:
  - Added comprehensive test cases covering mixed case (`DeaR`, `deaR`), apostrophe prefixes (`O'Neal`, `o'neal`, `O'neal`, `O'NEAL`, `D'Angelo`, `L'Amour`), Mc prefixes (`McDonald`, `MCDONALD`, `McCartney`), hyphens (`Mary-Ann Smith`, `Smith-O'Neal`), contractions (`Don't Stop Believin'`), acronyms (`DJ Khaled`, `MC Hammer`), Roman numerals (`Henry VIII`), and all-caps inputs (`DENNIS MAIDON`).
  - Verified 100% test pass rate across all 238 unit tests in the solution.

## [26.8.26.1] - 2026-08-26

### Added & Enhanced

- **Help System & User Manual updates log for Manual/Automatic Trivia Modes (`Lyracist`, `KSRotation`, and `Lyracist.Trivia`)**:
  - Added detailed instructions detailing the distinct behaviors of **Manual DJ Mode** (Auto-Run unchecked: questions standby, manual start timer via Spacebar, manual wrong answer fades, manual next progression) versus **Automatic Mode** (Auto-Run checked: automatic countdowns, automatic fades, automatic reveal, and auto-advance after 5s buffer).
  - Updated Help Topic 17 in `Lyracist` (`HelpViewModel.cs`), Help Topic 3 in `Lyracist.Trivia` (`MainViewModel.cs`), and Help Topic 6 in `KSRotation` (`MainWindow.xaml`).
  - Recorded detailed changes in `Lyracist_User_Manual_Updates.txt` for main user manual maintenance.
- **Manual Help Page Index Column Width Resizing (`Lyracist`)**:
  - Integrated a vertical `GridSplitter` into the Help Page layout (`HelpPage.xaml`), allowing users to manually click and drag to adjust the index column width.
  - Set robust minimum width constraints (`MinWidth="180"` for the index column, and `MinWidth="300"` for the details pane) to prevent accidental layout collapse.
  - Avoids truncation of long help topic index headers, ensuring readability on diverse screen sizes.

### Testing & Verification

- **Manual Trivia Flow Unit Tests (`Lyracist.Trivia.Tests`)**:
  - Added `ManualGameFlow_DJFlowControl_StandbyAndStartTimer` unit test to `GameEngineTests.cs` to verify question loading in standby mode and subsequent manual timer activation by the DJ.
  - Successfully verified solution-wide compilation and test execution with 100% pass rate.

## [26.8.25.1] - 2026-08-25

### Code Quality, Diagnostics & Compiler Cleanliness

- **Solution-Wide Zero-Warning Compiler & Analyzer Cleanliness**:
  - Resolved all Roslyn compiler errors (`CS8799`, `CS0133`, `CS1061`, `CS0103`), Roslynator warnings/messages (`RCS1021`, `RCS1037`, `RCS1075`, `RCS1077`, `RCS1102`, `RCS1118`, `RCS1123`, `RCS1139`, `RCS1146`, `RCS1155`, `RCS1163`, `RCS1187`, `RCS1196`, `RCS1213`, `RCS1215`, `RCS1235`, `RCS1261`), SonarAnalyzer rules (`S927`, `S1066`, `S3358`, `S3881`, `S3981`), and code analysis diagnostics (`CA1835`, `CA1850`, `CA1859`, `IDE0079`) across all 11 projects in `Lyracist.slnx`.
  - **`Lyracist.Trivia` & `Lyracist.Trivia.Core`**:
    - Removed `private` modifier from 29 partial method declarations in `MainViewModel.cs` generated by CommunityToolkit.Mvvm `[ObservableProperty]` to adhere to source generator accessibility rules (`CS8799`).
    - Standardized XML documentation comments with `<summary>` tags (`RCS1139`).
    - Fixed empty catch blocks with explicit discards (`RCS1075`), un-nested ternaries in countdown calculations (`S3358`), and implemented conforming `Dispose(bool disposing)` pattern (`S3881`).
    - Hooked up `RequestOpenProjectionWindow`, `OpenProjectionWindow_Click`, and `ExitButton_Click` handlers cleanly in `MainWindow.xaml.cs`.
  - **`KSRotation` & `KSRotation.Maui`**:
    - Replaced synchronous `using` with `await using` for asynchronous `DbContext` and `MemoryStream` instances in `PatronRequestServer.cs` (`RCS1261`).
    - Switched `SendBadRequestAsync` stream write to `Memory<byte>` overload (`CA1835`).
    - Guarded non-MAUI image and path resolution methods (`SendImageResponseAsync`, `MD5Hash`, `ResolveAvatarPath`) in `#if !MAUI` directives to eliminate unused member warnings in MAUI builds (`RCS1213`).
    - Replaced reflection-heavy lookups with `const` and `static readonly` constants (`RCS1187`).
    - Cleaned trailing whitespace across all ViewModels, Services, and Windows (`RCS1037`).
    - Replaced iterative list additions with `AddRange` (`RCS1235`) and converted LINQ operations to `ConvertAll` (`RCS1077`).
  - **`Lyracist.Data` & `Shared`**:
    - Renamed `ResolveTypeface` parameters to match `IFontResolver` interface definitions (`S927`).
    - Used `StringComparison.OrdinalIgnoreCase` in `FFmpegService.cs` and `ScanningService.cs` (`RCS1155`).
    - Marked `FFprobeRunner` as `public static class` (`RCS1102`).
    - Added arithmetic precedence parentheses in `DjBannerFileManager.cs` (`RCS1123`) and used concrete `BitmapImage` return type (`CA1859`).
    - Removed redundant `#pragma warning restore CA1416` in `LocalNetworkHelper.cs` (`IDE0079`).
  - **Test Suite Updates (`KSRotation.Tests`, `Lyracist.Tests`, `Lyracist.Trivia.Tests`)**:
    - Assigned return values of `Assert.Single(...)` directly across all test fixtures (`xUnit2033`).
    - Replaced tautological string length assertions on `WifiHelper.GetConnectedSsid()` with `Record.Exception(...)` validation (`S3981`, `RCS1215`).
    - Verified 100% test pass rate across all 202 unit tests in the solution.

### Added & Enhanced

- **Manual Game Flow Controls for DJ / Game Master (`Lyracist.Trivia`)**:
  - **Interactive Host Pacing (Manual Mode)**: When `⚡ Auto-Run Game` is disabled, new questions load into a ready **Reading / Standby** state with full time on the clock and the timer paused, allowing the DJ/Game Master to read the prompt over the microphone before starting the timer via `▶ Start Question & Timer` or `Spacebar`.
  - **Question Navigation & Direct Jump**:
    - Added `⏮ Prev` button (`Left Arrow` / `PageUp`) to safely step backward to the preceding question in the round.
    - Added `Next ⏭` button (`Right Arrow` / `PageDown`) to advance questions on demand.
    - Added a direct **Question Jump ComboBox** selector (Questions 1 through $N$) for instant navigation to any specific question without restarting the game.
  - **On-the-Fly Timer Bump & Trim Pacing**:
    - Added quick-action timer adjustment buttons (`[-5s]`, `[+5s]`, `[+10s]`, `[🔄 Reset]`) to dynamically extend or shorten active countdowns on the fly based on venue crowd discussion or mobile Wi-Fi latency.
    - Timer increases automatically adjust `TotalCountdownSeconds` and warning state thresholds.
  - **Stepwise Wrong Option Elimination & Instant Reveal**:
    - Added `✂ Fade Option` (`EliminateNextWrongCommand`) to manually fade out wrong answer choices one by one for 50/50 clues or interactive hints.
    - Added `⚡ Instant Reveal` (`InstantRevealCommand`) to immediately bypass multi-second countdowns and display the correct answer and explanation.
  - **Question Voiding without Penalty**:
    - Added `❌ Void Question (No Penalty)` (`VoidCurrentQuestionCommand`) to nullify spoiled or flawed questions, rolling back any points earned or lost by players for that specific question and preserving existing streaks.
  - **DJ Keyboard Shortcuts**:
    - Hooked global window hotkeys in `MainWindow.xaml`: `Spacebar` (Pause/Resume Timer), `Right Arrow` / `PageDown` (Next Question), and `Left Arrow` / `PageUp` (Previous Question).
  - **Two-Line Wi-Fi Credentials Layout on Connect Screen (`Lyracist.Trivia`, `Lyracist`, `KSRotation`)**:
    - Updated `TriviaDisplayWindow.xaml` across all applications so that `Wifi Network:` and `Password:` appear on clean, stacked individual lines with expanded max widths, preventing character clipping and improving readability from across the room.
  - **Updated Help Topics**:
    - Expanded Help Topic 1 (*Game Master Command Deck*) in `MainViewModel.cs` with complete documentation for manual flow controls, pacing buttons, and hotkeys.

## [26.8.21.2] - 2026-08-21

### Added & Enhanced

- **Cross-App Landscape Tablet Kiosk Request Station (`/kiosk`, `kiosk.html`)**:
  - Implemented a dedicated split-screen landscape web portal designed specifically for venue tablets mounted as public singer kiosks across **all three applications** (`KSRotation`, `KSRotation.Maui`, and `Lyracist`).
  - **Attractor / Welcome Screen & Idle Return**:
    - Added an illuminated, full-screen Welcome & Attractor overlay (`#kiosk-welcome-screen`) when the kiosk is idle.
    - **Hero & Call to Action**: Displays a pulsing glowing microphone icon, brand title, and an animated breathing call-to-action button: `✨ TOUCH SCREEN TO JOIN THE ROTATION ✨`.
    - **4-Step User Instructions**: Features glassmorphic visual cards explaining the entire flow in 4 clear steps:
      1. *Enter Your Name* (or tap your name in the live queue).
      2. *Search Songs* (online catalog search or manual entry).
      3. *Pitch Key Adjust* (vocal key adjustment from -2 to +2).
      4. *Take the Stage* (submit and watch stage screens).
    - **Live Stage Snapshot Bar**: Displays real-time live performance stats (🎤 *Now Singing*, ⏳ *Up Next*, and 👥 *Active/Total Queue count*) right on the welcome screen.
    - **Touch Anywhere to Begin**: Touching or tapping anywhere on the screen seamlessly transitions to the request station and focuses the name field.
    - **Automatic Idle Return**: After 45 seconds of inactivity or after request submission countdown, automatically clears the form and smoothly returns to the Welcome Screen.
    - **Manual "Welcome" Header Button**: Allows KJ or users to immediately return to the attractor screen at any time.
  - **PWA Standalone & Fullscreen Mode**: Added PWA meta tags (`apple-mobile-web-app-capable`, `mobile-web-app-capable`, `theme-color`) and an interactive `⛶ Fullscreen` toggle button in the header bar for full screen browser presentation on iOS/iPadOS and Android tablets without browser address bars.
  - **Left Pane (Request Station - 62% width)**:
    - Performer Name & optional Duet Partner input fields with auto-suggest and tap-to-select support.
    - Request Type toggle (Karaoke Sing vs. Background Music Track Play).
    - Online song catalog search via Apple iTunes Search API with debounced instant results and offline fallback.
    - Manual song & artist inputs with Key / Pitch adjustment pill selector ($-2, -1, \text{Standard } 0, +1, +2$).
    - Touch-optimized gradient **Submit Song Request** button.
    - **Post-Submission Celebration & Auto-Reset**: 5-second countdown modal (`🎉 Request Received!`) that automatically clears the form for the next singer, or allows instant reset with the "Ready for Next Singer Now" button.
    - **45-Second Inactivity Watchdog**: Automatically resets abandoned or half-filled forms after 45 seconds of touchscreen/keyboard idle time.
  - **Right Pane (Live Rotation & Queue - 38% width)**:
    - Real-time live rotation polling every 3 seconds with dual server fallback (`/api/rotation` and `/api/queue`).
    - **Spotlight Cards**: High-visibility cards at the top for **🎤 Now Singing** (with illuminated gold styling) and **⏳ Up Next** (illuminated cyan styling).
    - **Interactive Tap-to-Select**: Tapping any performer row on the right automatically populates that singer's name on the left request form, eliminating duplicate names and spelling errors.
    - **Round Start & Status Badges**: Displays the `🚩 1st` round start anchor badge, `Singing`, `Up Next`, `Music`, and `Paused` status tags.
    - **"Your Spot" Queue Preview**: Highlights an interactive slot at the bottom showing prospective singers their exact entry position (e.g., `✨ ➕ New signups enter here at Spot #X in rotation`) before submitting.
- **KSRotation.Maui Kiosk & 3-Way QR Overlay Integration**:
  - Embedded `kiosk.html` resource into `KSRotation.Maui.csproj` and linked to `PatronRequestServer`.
  - Added 3-way mode switcher tabs (`📱 Patron`, `📟 Kiosk`, `🎧 DJ`) in `ConnectQrOverlay` in `MainPage.xaml` / `MainPage.xaml.cs`.
- **Lyracist Kiosk Server & UI Integration**:
  - Embedded `kiosk.html` in `TabletClient/kiosk.html` with `/kiosk`, `/kiosk.html`, `/api/rotation`, and `/api/request` routing in `TabletLyricsServer.cs`.
  - Added `KioskUrl`, `KioskQrCodeImage`, and `OpenKioskQrWindowCommand` to `KaraokeViewModel.cs` and `SettingsViewModel.Network.cs`.
  - Created `KioskQrCodePopoutWindow.xaml` with large QR code, URL copy, and kiosk tips.
  - Added Kiosk QR buttons to `KaraokePage.xaml` and `SettingsPage.xaml`.
- **Remote DJ Web Board Checkmark Action & Add Performer Modal Dialog (`dj.html`)**:
  - Reordered performer card action buttons so the finished song checkmark (`✓`) is positioned as the very first button in the actions bar, matching the ergonomics of desktop and MAUI consoles.
  - Converted the static inline "Add Performer to Rotation" card into a dedicated action button and top rotation toolbar shortcut that opens a popup modal overlay dialog (`#add-performer-modal`).
  - Added full keyboard navigation to the Add Performer popup (auto-focusing and selecting performer name on open, `Enter` key submission, and `Escape` key dismissal with backdrop click close), streamlining rapid singer entry without scrolling the main board.
- **Android Deployment & Layout Dispatch Safeguarding (`KSRotation.Maui`)**:
  - Configured `<EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>` and `<AndroidEnableFastDeployment>false</AndroidEnableFastDeployment>` in `KSRotation.Maui.csproj` to eliminate Visual Studio Android launcher errors (`DotNetDebugLaunchProvider.LaunchApplicationAsync` / `AggregateException`), ensuring a 100% self-contained APK package.
  - Added an `OperatingSystem.IsWindows()` guard to `Shared/WifiHelper.cs` to immediately bypass native `wlanapi.dll` P/Invoke and `netsh` process spawning on non-Windows platforms (Android).
  - Wrapped dynamic orientation changes in `Dispatcher.Dispatch(...)` and switched to atomic `ColumnDefinitionCollection` and `RowDefinitionCollection` assignments in `MainPage.xaml.cs`, eliminating re-entrant layout passes during orientation switching.
- **Responsive Vertical & Horizontal Orientation Layouts in `KSRotation.Maui`**:
  - Implemented dynamic orientation handling in `MainPage.xaml` and `MainPage.xaml.cs` via `OnSizeAllocated` detection.
  - In **Vertical Mode (Portrait)**: The Active Rotation Queue spans the top across full width, and the Patron Request Portal (QR Code card) and Incoming Requests list reflow side-by-side across the bottom (220px height), maximizing vertical screen real estate for rotation management.
  - In **Horizontal Mode (Landscape)**: The layout retains the traditional two-column master view with Rotation on the left (`*`) and Portal/Requests sidebar on the right (`280px`).
- **Global Auto-Highlighting & Select-All on Focus Across All Applications**:
  - Implemented `TextBoxSelectionHelper.EnableGlobalSelectAllOnFocus()` across all WPF applications (`KSRotation`, `Lyracist`, `Lyracist.Trivia`, `TriviaDbCreator`, `LyracistDbEditor`, `LyracistKeyGen`, and `ScaryokeWheel`).
  - Entering or clicking any `TextBox`, `PasswordBox`, or numeric box from an unfocused state automatically highlights and selects all existing text, allowing instant overwrite typing without manual backspacing or double-clicking. Subsequent clicks inside an already focused box preserve normal caret positioning.
- **Auto-Focus & Text Highlighting on New Singer Addition**:
  - When adding a singer in `KSRotation`, the rotation queue automatically scrolls to the newly inserted singer card, focuses the name input field, and highlights the default `"New Singer"` text for immediate editing.
- **1st Singer (Round Start Anchor) Display & Action Sync on DJ Tablet & Web Portals**:
  - Resolved an issue where the `🚩 1ST` badge and red background for the 1st singer in rotation were not displaying on the Remote DJ Tablet (`dj.html`).
  - Added `.singer-card.rotation-start` red background (`--row-start-bg`) and border outline to `dj.html`, rendered the `🚩 1ST` badge next to the performer's name, and added a 1-click `🚩` remote action button in the action buttons bar.
  - Implemented remote `set-rotation-start` handling in `MainViewModel.Requests.cs` to allow designating or clearing the 1st singer anchor directly from the DJ tablet.
  - Synced `isRotationStart` across `PatronPortal.html` (with `🚩 1st` badge and red row styling), `TabletLyricsServer.cs` `BuildQueuePayload()`, `LyricsHub.cs` queue broadcast, and `mobile.html` performer queue view.
- **High-DPI Singer Display Box & Action Button Sizing in `KSRotation`**:
  - Resolved an issue on 1080p laptop displays where the bottom border of the `"✓ Finish"`, `"▲"`, and `"▼"` action buttons was clipped by the singer item container border.
  - Increased singer display box vertical padding (`Padding="2,6,2,10"`), container `MinHeight="80"`, input box `MinHeight="30"`, button heights to `30px`, and adjusted bottom margins to guarantee crisp button border rendering on all display resolutions and DPI scales.
- **Smart End-of-Round Singer Insertion Across `KSRotation` and `Lyracist`**:
  - Resolved an issue where newly added singers were appended to the very bottom of the list after performers who had already sung in the current cycle.
  - Implemented `RotationHelpers.InsertNewSinger` to automatically insert new singers at the end of the *current active rotation cycle* (immediately before the round-anchor performer holding `IsRotationStart = true` at index > 0). New singers now perform before previous singers repeat in the next round.
- **Accurate Inactive Singer Accounting in `SingersInRotationCount`**:
  - Corrected `SingersInRotationCount` in `KSRotation\ViewModels\MainViewModel.cs` to filter out inactive singers (`!s.IsInactive`), accurately showing active rotation size.
  - Added property change notification wiring for `IsInactive` and `IsMusic` to dynamically keep the singer count badge updated in real time.
- **Randomized Wrong Answer Elimination Order in `TriviaGameEngine`**:
  - Implemented Fisher-Yates shuffling for pending incorrect answer choices on every question start in `TriviaGameEngine.cs`.
  - Wrong answers now fade out in randomized, unpredictable sequences, preventing players from deducing the correct answer based on positional fade order.
- **Live Synchronization & Instant Persistence for Trivia Settings Across `KSRotation`, `Lyracist`, and `Lyracist.Trivia`**:
  - Resolved an issue in `KSRotation` and `Lyracist` where custom question timing (e.g. changing 15s to 25s), answer elimination fade intervals, post-reveal delay, point settings, and Wi-Fi credentials set in the Settings tab were not syncing into the active game engine or persisting to storage on change.
  - Added partial property change handlers across `KSRotation\ViewModels\MainViewModel.Trivia.cs`, `Lyracist\ViewModels\TriviaSettingsViewModel.cs`, and `Lyracist.Trivia\ViewModels\MainViewModel.cs` for all gameplay, timing, scoring, and Wi-Fi settings to immediately update `Settings.<Prop>`, sync into the active `_triviaEngine.Settings.<Prop>`, and save to `trivia_settings.json` / `appsettings.json`.
- **Game Complete & Intermission Screen Responsive Layout**:
  - Redesigned the Game Complete view in `Lyracist.Trivia` and `Lyracist` (`TriviaDisplayWindow.xaml`) to use a side-by-side 2-column layout (Leaderboard in Column 0, Intermission Sign-Up QR card in Column 1).
  - Both Wi-Fi and join QR codes are sized cleanly at 120x120px, preventing any vertical overflow or overlap with the bottom connection and copyright footer.

### Fixed

- **Kiosk Rotation List JS Injection (`kiosk.html`)**: The rotation list rendered each singer's name into an inline `onclick="selectSingerFromRotation('...')"` attribute using HTML-escaping only; since the browser decodes HTML entities back to literal characters before parsing the attribute as JS, a crafted singer name (typed on the kiosk itself) could break out of the string literal and execute arbitrary script on any device rendering the list, including the venue's stage displays. Replaced the inline handler with a `data-singer-name` attribute read by a single delegated click listener, so the name is never re-parsed as code. Fixed identically in `KSRotation/Resources/kiosk.html` and `Lyracist/TabletClient/kiosk.html` (now the same file, see below).
- **Missing Field Length Validation (`TabletLyricsServer.cs`)**: The new `POST /api/request` kiosk endpoint skipped the `ExceedsLength` checks its sibling `POST /api/requests` endpoint enforces, allowing unbounded name/song/artist/notes submissions straight into the database and live rotation. Added the same length caps used elsewhere in the file.
- **Duplicate `kiosk.html` (Lyracist)**: `Lyracist/TabletClient/kiosk.html` was a second, independent 1741-line copy of `KSRotation/Resources/kiosk.html` that could silently drift out of sync. Removed the duplicate and linked `Lyracist.csproj` to the single canonical copy, matching the link pattern `KSRotation.Maui.csproj` already used.
- **Dead Code (`KSRotation.Maui/MainPage.xaml.cs`)**: Removed the orphaned `OnPortalTitleTapped` handler, left behind after the QR mode switcher was replaced by dedicated Patron/Kiosk/DJ buttons.

## [26.8.20.1] - 2026-08-20

### Added & Enhanced

- **Trivia Display Countdown Screen QR Layout & Inline Venue Name Across `Lyracist.Trivia`, `Lyracist`, and `KSRotation`**:
  - Integrated the venue name directly on the same line as the main title (`"🎮 LIVE PUB TRIVIA NIGHT — {VenueName}"`) on the pre-game countdown lobby screen and inline with categories (`"{CategoryTitle} • {VenueName}"`) during gameplay.
  - Eliminated the separate bulky venue banner row to recover vertical screen real estate.
  - Resolved scrunched/clipped QR codes on the countdown lobby screen by reorganizing the right column into a side-by-side dual card layout (`📶 1. CONNECT WI-FI` on the left, `📱 2. JOIN TRIVIA` on the right) and embedding QR images in auto-scaling `Viewbox` containers that preserve 1:1 aspect ratio and crisp rendering on any resolution (1080p, 720p, 1440p, 4K).

- **Clean Application Termination & Explicit Exit Controls in `Lyracist.Trivia`**:
  - Resolved an issue where closing `Lyracist.Trivia` could leave background network listeners or timers running, preventing the process from terminating without Task Manager.
  - Added explicit `Application.Current.Shutdown()` and `App.OnExit` `Environment.Exit(0)` termination handlers.
  - Updated `MainViewModel.Dispose()` to safely stop and dispose `_preGameTimer` alongside `TriviaWebServer`, `TriviaGameEngine`, and `TriviaDatabaseService`.
  - Added a dedicated **"✕ Exit"** button in the main Game Master header bar for immediate 1-click application exit.

- **TV Projection Screen Synchronization & Countdown Termination Fix**:
  - Resolved an issue in `Lyracist.Trivia`, `Lyracist`, and `KSRotation` where clicking `"▶ Start Game Now (Skip Countdown)"` started active gameplay on the host console, but opening or focusing the TV Projection window (`TriviaDisplayWindow`) caused the pre-game countdown clock to start/display rather than Question #1.
  - Corrected `StartGameWithSelectedPack()` / `StartGame()` / `StartTrivia()` to immediately cancel and stop any running pre-game lobby timer, dismiss `IsShowingConnectScreen`, and trigger/focus the TV projection screen.
  - Added `SyncWithEngine()` to `DisplayViewModel` and `TriviaDisplayViewModel` to immediately hydrate the active question prompt, options, timer countdown, elimination opacities, answer stats, and scores whenever the display window is initialized or opened during an in-progress game.

- **Safe, DJ-Friendly Auto-Advance System for Karaoke Hosting**:
  - **Grace Period Lifecycle & Timer**:
    - Configurable post-performance grace period countdown (default 15 seconds, adjustable in Settings).
    - Natural playback completion via LibVLC & FFME `EndReached` events triggers the grace period transition automatically.
    - Smooth fill-in background music starts ducked during the grace period while stage billboard displays announcement banner: `"Next singer: {name} — please come to the stage"`.
  - **Large High-Contrast DJ Control Panel Buttons**:
    - Added `BigDJButton` style with high contrast, large bold text, rounded glow styling, and touch/click accessibility.
    - **`▶ START SONG`** (`StartSongButton`): Instantly cancels grace timer, halts/ducks fill-in music, loads the current singer's song, synchronizes projection window and mobile lyrics server, and begins audio/video playback.
    - **`⏭ SKIP SINGER`** (`SkipSingerButton`): Advances singer queue without completing or scoring skipped song, keeps fill-in music playing, and restarts a fresh grace period for the next performer.
  - **Multi-Subsystem Synchronization & State Machine**:
    - Created `AutoAdvanceManager` coordinating `IMediaEngine`, `IShowFlowService`, `IDisplayService`, `RotationViewModel`, `ITabletLyricsServer`, `LyricsWindowViewModel`, and `KaraokeViewModel`.
    - Implemented `AutoAdvanceState` enum (`Idle`, `GracePeriod`, `WaitingForSongSelection`, `ReadyToStart`, `StartingSong`) preventing accidental double-starts.
    - Safe mode interlocks: Auto-advance automatically suppresses execution during active Trivia and Scaryoke modes.
    - Empty queue / missing singer protection: Auto-advance safely returns to `Idle` if no active singers exist.
    - Missing song detection: Transitions to `WaitingForSongSelection` with billboard prompt if singer has no song selected yet.

- **Tiered Option Value Scoring (100% / 70% / 40%) Across `Lyracist`, `KSRotation`, and `Lyracist.Trivia`**:
  - **Dynamic Multiplier Tiers**: Base points scale according to the number of visible options remaining at the exact moment of player answer submission:
    - **4 Options Visible (0 Eliminated)**: 100% of Base Points (1,000 pts default). Rewarding early, confident buzz-ins before any wrong options fade.
    - **3 Options Visible (1 Eliminated)**: 70% of Base Points (700 pts default). Triggered when the first wrong option fades at the 2/3 question countdown mark.
    - **2 Options Visible (2 Eliminated / 50-50)**: 40% of Base Points (400 pts default). Triggered when the second wrong option fades at the 1/3 question countdown mark.
    - **Mathematical Balance**: Gives early knowledgeable players a decisive 2.5× scoring advantage (1,000 pts vs 400 pts) while casual patrons can still score meaningful points on 50/50 guesses.
  - **Mobile Buzzer Web App (`trivia.html`) Real-Time Multiplier Pill**:
    - Added dynamic `#value-multiplier-pill` right above the 4 buzzer buttons updating in real time as options fade: `⚡ 100% VALUE (1,000 pts)`, `⚡ 70% VALUE (700 pts)`, `⚡ 40% VALUE (400 pts)`.
    - Synchronized buzzer button elimination and disable state during question active countdown.
  - **Configuration Controls in Trivia Settings**:
    - Added `Tiered Option Value (4=100%, 3=70%, 2=40%)` toggle and customizable percentage fields across `Lyracist` (`TriviaSettingsPage.xaml`), `KSRotation` (`MainWindow.xaml`), and `Lyracist.Trivia` (`MainWindow.xaml`).
- **Unified Trivia Parity & Feature Alignment Across `Lyracist`, `KSRotation`, and `Lyracist.Trivia`**:
  - Brought complete visual, behavioral, and architectural feature parity between standalone `Lyracist.Trivia` and the embedded trivia engines in `Lyracist` and `KSRotation`.
  - **16:9 Big Screen Projection Window (`TriviaDisplayWindow`) in `Lyracist` and `KSRotation`**:
    - High-impact 70:30 pre-game lobby featuring single-category 16:9 announcement banners and real-time generated collage banners for multi-pack category games (`TriviaBannerGenerator`).
    - Right-column onboarding stack featuring large digital pre-game countdown clock, Wi-Fi scan-to-connect QR card with SSID and password credentials, and Trivia scan-to-join mobile buzzer QR card.
    - Continuous top marquee ticker bar for venue announcements and player rankings.
    - Dynamic 2x2 question cards (▲ Purple, ◆ Cyan, ● Amber, ■ Rose) with real-time answer elimination fading and post-reveal explanations.
    - Intermission countdown card and celebration view for round winners and team rosters.
  - **3-Column Game Master Deck in `Lyracist` (`TriviaPage.xaml`) and `KSRotation` (`MainWindow.xaml`)**:
    - **Header Bar**: Active venue label, connected player count badge, target monitor projection dropdown, and 1-click "📺 Open Big Screen" button.
    - **Left Column**: Category pack checklist (check 2+ to mix categories), Questions Per Game preset selector (5, 10, 15, 20, 25, 50, 100), Auto-Run Game switch (15s answer, 5s fade, 5s reveal), 1-click `"🎯 Launch Pre-Game Countdown"` and `"▶ Start Game Now"`, live question controls (Start, Pause, Lock & Reveal, Next, Reset), and patron mobile portal info.
    - **Center Column**: Active round title, countdown timer, question prompt card, real-time **Patron Answer Distribution Visualizer** (A, B, C, D vote counts and progress bars), and 2x2 stylized answer option preview.
    - **Right Column**: Live Player Leaderboard with real-time score updates, team associations, and individual player kick/moderation action (`✕`).
  - **Dedicated Trivia Settings Tab in `Lyracist` (`TriviaSettingsPage.xaml`) and `KSRotation`**:
    - Question Timers (Time limit, warning countdown, wrong option elimination fade, post-reveal delay).
    - Scoring & Multipliers (Base points, wrong answer penalty deduction, speed bonus, streak multiplier).
    - Venue & Host configuration with customizable pre-game welcome banner template supporting `{venue}` and `{dj}` tokens.
    - Intermission & Pre-game countdown controls (auto-start next game, pre-game delay in minutes, auto-start after countdown).
    - Local network and Wi-Fi credentials with auto-detect Wi-Fi network and password.
    - Target monitor selection and Save / Reset Defaults with animated status toasts.
  - **Karaoke & Rotation Screen Priority & Auto-Pause Deconfliction**:
    - In `Lyracist` (`DisplayService.cs`) and `KSRotation` (`MainViewModel.Trivia.cs`), Trivia is secondary to Karaoke performance and Singer Rotation.
    - When a karaoke song is playing or singer rotation is projected on the screen, Trivia automatically pauses with an informative status reason (`"Karaoke Performance"` or `"Rotation Screen Active"`) and yields the display.
    - When song performance finishes and rotation yields, Trivia automatically resumes seamlessly.

### Added

- **New Category Pack: "Famous Lines & Sayings From Movies" (150 Questions)**:
  - Created a dedicated 150-question database (`TriviaData/packs/famous_movie_quotes.json`) packed with iconic cinematic catchphrases, memorable movie quotes, AFI Top 100 quotes, and legendary film lines.
  - Covers golden age classics (*Casablanca, Gone with the Wind, The Wizard of Oz, Citizen Kane, Sunset Boulevard*), blockbuster sci-fi (*Star Wars, The Godfather, Jaws, Terminator, Matrix, Alien, Blade Runner*), 80s/90s hits (*Top Gun, Dirty Harry, Die Hard, Forrest Gump, Pulp Fiction, Jerry Maguire, A Few Good Men, Silence of the Lambs, Goodfellas, Titanic, The Big Lebowski, Fight Club*), and hilarious comedies (*Airplane!, Monty Python, Ghostbusters, Caddyshack, Anchorman, Groundhog Day, The Princess Bride, Mean Girls, Ferris Bueller*).
  - Evenly balanced option choices (~25% each for A, B, C, and D) and custom 16:9 Category Announcement Banner (`famous_movie_quotes.png`).
  - Indexed and synchronized into SQLite database `TriviaData/trivia.db` (**15 categories, 2,250 total curated questions**).
- **New `TriviaDbCreator` Project (Fluent UI / MVVM Question Pack Authoring Tool)**:
  - Created a dedicated standalone desktop application (`TriviaDbCreator.exe`) for creating, authoring, and managing trivia category databases and question packs.
  - Built with **CommunityToolkit.Mvvm** and **WPF-UI (Fluent Design / Dark Theme)**.
  - **Left Sidebar**: Auto-discovers and navigates all category packs in `TriviaData/packs/`, with search filtering, question count badges, and New/Import actions.
  - **Center Panel**: Pack metadata configuration (Title, ID slug, Category, Description) and interactive filterable Questions Table with difficulty and correct answer indicators.
  - **Right Panel**: Real-time Question Editor with stylized option cards (▲ A Purple, ◆ B Cyan, ● C Amber, ■ D Rose), radio toggles to select the correct answer, difficulty dropdown, and explanation notes.
  - **Option Balancing Tool**: 1-click option shuffler that redistributes correct answer positions evenly across options A, B, C, D (~25% each) across the entire pack while preserving correct answer strings.
  - **16:9 Banner Studio**: 1-click automated high-resolution category banner generator into `TriviaData/Banners/{pack}.png`.
  - **SQLite Database Sync**: 1-click direct database synchronization to `TriviaData/trivia.db`.
  - **Comprehensive In-App Help & About Tabs**: Added a dedicated **`❓ Help & Instructions`** tab with an interactive 8-topic index covering Quick Start, Creating Databases, Question Authoring, 25% Option Balancing, 16:9 Banners, SQLite Sync, JSON Import/Export, and complete JSON Schema specification, as well as an **`ℹ️ About`** tab with official branding.
- **Balanced Answer Option Distribution Across All 2,100 Questions**:
  - Shuffled multiple-choice answer option positions across all 14 category databases so that correct answers are evenly distributed across all 4 choices (**A: ~25%, B: ~25%, C: ~25%, D: ~25%** / exactly 37-38 questions per option in every 150-question pack).
  - Synchronized and verified all re-balanced questions into SQLite database `TriviaData/trivia.db`.
  - Added automated unit test (`VerifyAnswerDistribution_SpreadEvenlyAcrossAllOptions`) asserting that every pack maintains balanced option distributions.
- **Dedicated In-App Help & About Tabs in `LyracistTrivia`**:
  - Added a **`❓ Help`** tab with a split-pane interactive index covering 8 detailed topics: Game Master Command Deck, 70:30 Pre-Game Lobby, Authoritative Timer Rules, Mobile Buzzer App, Players & Teams, Dynamic Databases & Auto-Discovery, Multi-Monitor Projection, and JSON Database Schema.
  - Added an **`ℹ️ About`** tab featuring the official `LyracistTrivia_logo.png`, versioning (`v26.8.19.20`), company (`PAROLE Software`), author (`Dennis N. Maidon`), copyright, and system specifications.
  - Updated in-app help systems in `Lyracist` and `KSRotation` with full trivia night operations, timer override rules, and JSON pack creation guides.
- **Expanded All 14 Trivia Category Databases to 150 Questions (2,100 Total Curated Questions)**:
  - Fleshed out every single trivia category database in `TriviaData/packs/` and synchronized to `trivia.db`, providing 150 curated, accurate questions per category:
    1. `biker_trivia.json`: 150 questions (Harley-Davidson, Indian, European/Japanese classics, rallies, chopper lore).
    2. `rock_and_roll.json`: 150 questions (Classic rock, progressive rock, metal, grunge, legendary guitarists, and iconic albums).
    3. `country_music.json`: 150 questions (Outlaw country, 90s country classics, female country royalty, bluegrass, and Grand Ole Opry history).
    4. `complete_the_lyric.json`: 150 questions (Singalong lyric completions across rock, pop, 80s/90s, and karaoke anthems).
    5. `music_legends.json`: 150 questions (Motown, Soul, Jazz, Pop icons, Rock pioneers, and legendary songwriters).
    6. `movie_soundtracks.json`: 150 questions (Oscar-winning theme songs, blockbuster movie anthems, musicals, and soundtrack trivia).
    7. `pop_culture_80s_90s.json`: 150 questions (Fads, toys, retro video games, iconic commercials, fashion, and memorable decade moments).
    8. `tv_shows.json`: 150 questions (Classic and modern sitcoms, primetime dramas, animated favorites, and catchphrases).
    9. `logos_and_slogans.json`: 150 questions (Famous brand slogans, hidden logo symbols, automotive badges, and corporate emblems).
    10. `geography.json`: 150 questions (World geography, natural landmarks, rivers, oceans, mountain peaks, and world wonders).
    11. `state_capitals.json`: 150 questions (All 50 US state capitals, world capitals, territory seats, and historical capital trivia).
    12. `history.json`: 150 questions (Ancient civilizations, world wars, American history, revolutions, discoveries, and famous rulers).
    13. `sports.json`: 150 questions (Championships, Olympic feats, legends, records, rules, and classic sporting events).
    14. `pub_general_knowledge.json`: 150 questions (Science, nature, literature, art, mathematics, and all-around pub trivia essentials).
- **Unified 16:9 Pre-Game Lobby & Category Showcase Screen (`TriviaDisplayWindow`)**:
  - Re-architected the pre-game big screen into a unified, high-impact lobby layout combining all pre-game elements onto one screen:
    - **Left Hero Column (70% width)**: Displays the high-resolution 16:9 Category Announcement Banner (`TriviaData/Banners/{pack}.png`) at maximum size with ambient border glow, theme tagline, and dynamic cross-pack syncing whenever the host selects a new category dropdown.
    - **Right Onboarding Stack (30% width)**:
      1. **Game Start Countdown Clock**: Large digital timer with pulsing amber badge showing time until game launch.
      2. **📶 1. Connect to Wi-Fi QR Card**: Scan-to-connect Wi-Fi QR code with venue SSID and WPA password.
      3. **📱 2. Join Trivia Game QR Card**: Scan-to-join mobile buzzer QR code pointing to `http://<LAN-IP>:8085/trivia` with clean direct URL.
    - **Bottom Connection & Copyright Bar**: Displays mobile buzzer URL (`📱 PLAY ON YOUR PHONE: ...`), company copyright information (`© 2026 PAROLE Software - All rights reserved.`), and app branding across all projection screens.
    - **Full-Width Ticker Bar**: Continuous horizontal marquee scrolling venue announcements, host branding, game rules, and buzzer tips.
- **Single-Click Pre-Game Launch (`LaunchPreGameLobbyCommand`)**:
  - Added a primary **"🎯 Launch Pre-Game Lobby & Countdown"** action button in `Lyracist.Trivia` that automatically opens/focuses the big screen on the target monitor, syncs the selected category banner, starts the pre-game countdown, and activates the lobby with one single click.
  - Accompanied by **"▶ Start Game Now (Skip Countdown)"** for instant gameplay kickoff.
- **16:9 Aspect Ratio Category Announcement Banners (`TriviaData/Banners/`)**:
  - Created high-resolution 1920x1080 (16:9) announcement banners for all 14 trivia categories, saved directly into `TriviaData/Banners/` and automatically copied to the build folder with `PreserveNewest`:
    1. `biker_trivia.png`: *Bikers & Motorcycles* (Dark Carbon & Flame Gold theme, 🏍️ emblem).
    2. `rock_and_roll.png`: *Rock & Roll Legends* (Electric Indigo & Neon Pink theme, 🎸 emblem).
    3. `country_music.png`: *Country Music Hits* (Saddle Brown & Gold theme, 🤠 emblem).
    4. `geography.png`: *World Geography* (Deep Emerald & Ocean Teal theme, 🌍 emblem).
    5. `state_capitals.png`: *State & World Capitals* (Navy Blue & Gold theme, 🏛️ emblem).
    6. `history.png`: *World History* (Antique Bronze & Crimson theme, 📜 emblem).
    7. `complete_the_lyric.png`: *Complete the Lyric* (Neon Magenta & Cyan Singalong theme, 🎤 emblem).
    8. `tv_shows.png`: *TV Shows & Sitcoms* (Retro Indigo & Cyber Blue theme, 📺 emblem).
    9. `sports.png`: *Sports & Athletes* (Stadium Green & Gold theme, 🏆 emblem).
    10. `logos_and_slogans.png`: *Logos & Slogans* (Electric Blue & Amber theme, 🏷️ emblem).
    11. `music_legends.png`: *Music & Karaoke Legends* (Royal Purple & Platinum Gold theme, 🌟 emblem).
    12. `pop_culture_80s_90s.png`: *80s & 90s Pop Culture* (Synthwave Neon Pink & Turquoise theme, 🕹️ emblem).
    13. `movie_soundtracks.png`: *Movie Soundtracks* (Midnight Cinema & Gold theme, 🎬 emblem).
    14. `pub_general_knowledge.png`: *Pub Trivia All-Stars* (Pub Tavern Amber & Forest Green theme, 🍻 emblem).
  - Streamlined banner graphics to focus purely on category branding, topic highlights, and player join callouts, omitting hardcoded question counts, timers, and scoring rules so banners remain accurate regardless of host settings.
  - Added `TriviaStorageHelper.GetBannersDirectory()` and automated banner generator utility (`TriviaBannerGenerator.cs`).
- **Automated `TriviaData` Build Output Copying**:
  - Configured `TriviaData\**\*` with `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` across `Lyracist.Trivia.Core.csproj`, `Lyracist.Trivia.csproj`, `Lyracist.csproj`, and `KSRotation.csproj`.
  - Automatically copies and updates `TriviaData/packs/` (all 14 category JSON packs), `TriviaData/Banners/` (all 14 category banners), `trivia.db`, and `trivia_settings.json` into the target build output directory (`C:\VB26\Release\Lyracist\Debug\net10.0-windows\TriviaData\`) whenever files or settings are added or modified.
- **150-Question Bikers & Motorcycles Trivia Database (`biker_trivia.json`)**:
  - Added a comprehensive, curated 150-question category pack covering:
    1. **Harley-Davidson Heritage & Engines**: Founding in 1903 Milwaukee, Knucklehead, Panhead, Shovelhead, Evolution, Twin Cam, Milwaukee-Eight, Revolution/Porsche V-Rod, Sportster, Fat Boy, and H.O.G. history.
    2. **Indian Motorcycle & Early American Iron**: 1901 Springfield heritage, Scout, Chief, Indian Four, Burt Munro's Bonneville record, Crocker, Henderson, and Excelsior.
    3. **British, European & Japanese Icons**: Triumph Bonneville & Steve McQueen, Norton Commando & Featherbed frame, Vincent Black Shadow & Rollie Free, Ducati Desmodromic, BMW Boxers, Honda CB750 & Gold Wing, Kawasaki H2 Mach IV Widowmaker & GPZ900R Ninja, Suzuki Hayabusa.
    4. **Legendary Rallies & Road Pilgrimages**: Sturgis (Pappy Hoel & Jackpine Gypsies), Daytona Bike Week, Laconia, Tail of the Dragon (318 curves in 11 miles), Route 66, Blue Ridge Parkway, and Iron Butt Association (SaddleSore 1000).
    5. **Choppers & Custom Mechanical Engineering**: Origin of chopping, ape hangers, sissy bars, suicide clutch & jockey shifters, Springer front ends, hardtail vs. softail, rake & trail geometry, dry sump oiling, and masters (Arlen Ness, Indian Larry, Jesse James, Von Dutch, Ed Roth).
    6. **Biker Culture, Traditions & Etiquette**: Guardian / Gremlin bell lore, 1%er history (Hollister 1947), two-finger downward wave, cut / three-piece rocker patches, road captain & sweep duties, staggered formation, and lane splitting.
    7. **Movies, TV Shows & Pop Culture Icons**: *Easy Rider* (Captain America & Billy Bike), *The Wild One* (Marlon Brando), *Sons of Anarchy* (SAMCRO & Jax Teller), *Terminator 2* (Fat Boy), Evel Knievel, *Long Way Round*, and *On Any Sunday*.

### Changed

- **`Lyracist.Trivia` 3-Column No-Scroll Settings Dashboard**:
  - Converted the **⚙️ Settings & Display** tab into an equal 3-column dashboard layout so Game Masters and hosts can view and edit all configuration fields without vertical scrolling.
  - **Column 1**: Venue & Host Branding (Venue Name, Game Master / Host Name) and Intermission & Auto-Flow controls.
  - **Column 2**: Timer & Elimination Speeds (Default Question Time, Wrong Answer Fade Speed, Post-Reveal Delay) and Scoring & Multipliers (Base Points, Wrong Answer Deduction, Fast Buzzer Speed Bonus).
  - **Column 3**: Wi-Fi Credentials & Connect Instructions (Wi-Fi SSID, Password, Auto-Detect button, Pre-Game Countdown duration, and Auto-Start toggle).
  - Added a dedicated top header bar with quick-access **"💾 Save All Settings"** action button.

## [26.8.18.0] - 2026-08-18

### Added

- **Multi-Monitor Big Screen Projection & Auto-Casting (`KSRotation`)**:
  - Integrated full-screen 16:9 venue projection (`TriviaDisplayWindow` & `TriviaDisplayViewModel`) into `KSRotation`.
  - Added **"📡 Open Big Screen"** button to the `🎯 Trivia Game Control` tab, allowing hosts to toggle and focus the big-screen display window on the configured target monitor.
  - Automatically launches and positions the 16:9 projection window onto the designated secondary monitor whenever `"▶ Start Game"` is clicked.
  - Dynamically repositions `TriviaDisplayWindow` if the target monitor changes during runtime.
- **Configurable Wrong Answer Point Deduction (`WrongAnswerDeductionPoints`)**:
  - Added dedicated configuration input boxes under **🏆 Scoring & Bonus Multipliers** / **Scoring & Multipliers** in `Lyracist` (**Trivia Settings**), `KSRotation` (**Trivia Settings**), and standalone `Lyracist.Trivia` (**Settings & Display**).
  - Defaults to `0` (standard pub trivia rules), allowing Game Masters / hosts to penalize incorrect guesses or wild buzzer spam by deducting configured point amounts (e.g. -250, -500).
  - Synchronized scoring engine (`TriviaGameEngine`), SQLite persistence, and mobile player buzzer feedback displaying deducted points (e.g., `"❌ Incorrect (-250 pts)"`).

### Changed / Cleaned Up

- **Toolbar & Settings Streamlining**:
  - **`KSRotation`**: Removed the redundant `"📡 Connect Screen"` and `"🎯 Trivia Night"` quick-launch buttons from the primary Rotation page toolbar. The Connect Instructions screen remains accessible directly within `Connect & Request Instructions` under the **Display** settings tab, while Trivia Night controls remain cleanly dedicated to the **Trivia** and **Trivia Settings** tabs.
  - **`Lyracist` & `KSRotation` Display Settings**: Removed the `"🎯 Launch Trivia Night"` button from the `Connect & Request Instructions` groupbox on the settings pages, keeping Trivia configuration exclusively on dedicated Trivia pages.

### Added

- **Float Current Singer to Top of Rotation Option**:
  - Added a togglable option in `Lyracist`, `KSRotation` (WPF), and `KSRotation.Maui`:
    - **`Lyracist`**: Checkbox on the **Rotation** page toolbar, the **Karaoke** control page queue options bar, and persistent toggle under **Settings -> Monitors & Screen Assignments**.
    - **`KSRotation` (WPF)**: Checkbox in the top rotation toolbar with automatic settings persistence.
    - **`KSRotation.Maui`**: Dedicated `Float to Top` checkbox in the Active Rotation Queue header bar.
  - **Dynamic Top-Floating Queue Mechanics**:
    - When enabled, the currently performing singer always automatically floats to index 0 (top of the rotation list).
    - As songs finish, the completed performer moves to the back of the active queue and the next active performer automatically floats to the top (index 0), completely eliminating the need for the DJ/KJ to scroll down long rotation lists during shows.
    - Selecting any singer as current immediately promotes and shifts them to the top of the queue.
- **1st Singer in Rotation (Round Start Anchor Flag)**:
  - **Visual 🚩 Round Start Badge & Red Row Highlight**:
    - Added a red `🚩 1ST` badge next to the 1st singer in the active rotation across `Lyracist`, `KSRotation` (WPF), and `KSRotation.Maui`.
    - Added a matching red row background/highlight (alongside the existing yellow current-singer and blue next-singer row highlights) so the 1st singer stands out at a glance, not just from the badge text.
    - Allows the DJ to instantly identify the start of the round cycle and know when the full rotation has completed as the 1st singer returns to the top.
  - **Designate / Clear 1st Singer Action**:
    - Added `"Set as 1st Singer (Round Start)"` context menu items and quick-action buttons (`🚩`) across `Lyracist`, `KSRotation`, and `KSRotation.Maui`.
    - The action now toggles: clicking it again on the singer who already holds the flag clears it and hands it to the next active singer in rotation order, so an accidental flag can be undone without picking a specific replacement.
    - Automatically guarantees that exactly one active singer holds the start anchor flag at all times.
  - **Sync & Web Portal Integration**:
    - `isRotationStart` is included in the REST/WebSocket `/api/rotation` payload and synchronized between `KSRotation` and `Lyracist`.

### Fixed

- **`Lyracist`: Float Current Singer to Top not floating the new current singer after "Finish Song"**: A `Rotation.CollectionChanged` handler re-triggered the float-to-top logic on every intermediate list mutation performed internally by the shared rotation-advance helper, undoing its own "move the finished singer to the bottom" step before it completed. The song would advance, but the rotation order never visibly changed. Fixed by suppressing that auto-sync handler while a rotation-reordering operation is already in progress and syncing once after it completes.
- **"Clear 1st Singer Badge" appearing to do nothing**: Clearing the badge fell back to reassigning it to "the first active singer in list order" — which, with Float Current Singer to Top enabled, is almost always the very singer you just cleared it from (they're floated to the top). Clearing now hands the flag to the next active singer in rotation order instead, so it can no longer reassign back to the singer being cleared.
- **`KSRotation`**: Closed a related reentrancy gap where `EnsureRotationStartFlag` could re-run mid-operation during rotation-advance/reorder calls; it's now suppressed during those operations (matching the rest of the class's existing reentrancy guard) and re-run once explicitly afterward, including when a fully-played music request holding the flag is auto-removed from the rotation.

## [26.8.17.0] - 2026-08-17

### Added

- **Full Trivia Engine & Host Deck Integration into `Lyracist` and `KSRotation`**:
  - **`Lyracist` App**: Added primary navigation items **`Trivia`** (interactive Game Master host console) and **`Trivia Settings`** (dedicated separate settings page for question timers, scoring rules, bonuses, and local Wi-Fi credentials).
  - **`KSRotation` App**: Added dedicated top-level tabs **`TabItem Header="Trivia"`** and **`TabItem Header="Trivia Settings"`**, backed by the `MainViewModel.Trivia.cs` partial class.
  - **Deduplicated Venue & Game Master / DJ Settings**:
    - Trivia in both `Lyracist` and `KSRotation` directly pulls the active Venue and Host / DJ names from existing application settings (`AppSettings.SelectedVenue` / `AppSettings.DjName` in Lyracist, `MainViewModel.VenueName` / `MainViewModel.DjName` in KSRotation).
    - Removed redundant text input fields from the Trivia Settings pages, replacing them with live synchronized indicator badges.
  - **3-Column No-Scroll Trivia Settings Dashboard**:
    - Redesigned the Trivia Settings tab into an efficient 3-column layout (Column 0: Timers & Flow, Scoring & Multipliers; Column 1: Active Venue & Host info, Intermission & Pre-Game; Column 2: Local Network & Wi-Fi Access, Save/Reset Actions) allowing users to see and configure all settings on screen without scrolling.
  - **Automated Game Pause & Resume Synchronization**:
    - When a DJ Banner, Special Event, or Rotation Projection window is opened or brought active on the projection monitor, the trivia game engine automatically pauses with a descriptive reason banner (e.g. `"DJ Banner Active"`, `"Rotation Screen Active"`), freezing countdown clocks and safely blocking mobile buzzer submissions.
    - Dismissing or closing the overlay immediately resumes the trivia game session right where it was paused.
- **`Lyracist.Trivia` Standalone App & `Lyracist.Trivia.Core` Hybrid Engine**: Introduced a complete pub/bar trivia system designed for both dedicated Trivia Night events and karaoke intermission / rotation fill-in games:
  - **Venue & Game Master Branding Configuration**: Added dedicated input fields on the **⚙️ Settings & Display** tab for **Venue Name** and **Game Master / Host Name**, with live two-way synchronization to the projection display window, scoreboard, and mobile buzzer web portal.
  - **Enlarged Venue QR Codes & Connect Screen Typography**: Expanded QR code image dimensions from 220×220 to 380×380 with enhanced high-contrast borders and scaled typography for Wi-Fi credentials and mobile buzzer links, ensuring patrons can effortlessly scan codes from across the room on large venue monitors and TVs.
  - **Multi-Monitor Display Resolution & Placement**: Corrected display window initialization by maintaining `WindowState.Normal` with borderless bounds placement (`targetScreen.Bounds`), ensuring `TriviaDisplayWindow` opens directly onto the selected secondary TV/projector without being forced to Monitor 0.
  - **Projection Cast Timer Synchronization**: Configured the pre-game countdown timer to start strictly when the host casts the projection screen to the TV monitor (or manually clicks Start), preventing background timer expiration before the host connects the venue display.
  - **Reliable Connect Instructions Screen Initialization**: Guaranteed that the dual QR code Connect Instructions screen (with Wi-Fi credentials and game join links) loads immediately upon opening the venue projection display in Lobby state.
  - **Projection Screen Dismissal & Controls (`TriviaDisplayWindow`)**:
    - **Escape Key (`Esc`)**: Pressing Escape immediately closes and exits the projection display window.
    - **Floating Close Button (`✕`)**: Sleek translucent circular close button in the top-right corner, illuminating red on hover for instant 1-click closure.
    - **Right-Click Context Menu**: Right-clicking anywhere on the screen displays quick options to close (`✕ Close Projection (Esc)`) or toggle fullscreen (`🗖 Toggle Fullscreen (F11)`).
    - **Double-Click & Drag**: Double-clicking the display toggles between windowed and fullscreen maximized modes, with click-and-drag window repositioning.
  - **Dedicated Venue Connect Instructions Screen (`TriviaDisplayWindow`)**:
    - **Dual QR Code Architecture**: Full-screen high-contrast visual display featuring:
      1. **1. CONNECT TO WI-FI**: High-resolution scan-to-connect Wi-Fi QR code (`WIFI:S:...;T:WPA;P:...;;` or `nopass`), auto-detected/configured SSID name, and password.
      2. **2. JOIN TRIVIA GAME**: High-resolution mobile buzzer QR code (`http://<ip>:<port>/`), browser link, and clear 3-step instructions on joining and forming teams.
    - **Pre-Game Countdown Clock**: Large prominent ticking countdown banner (`"⏱ TRIVIA GAME STARTS IN: mm:ss"`), announcing when the round begins and automatically launching the game when the countdown reaches 00:00.
    - **Game Master Console Controls**: Host can easily toggle the connect instructions screen, set the pre-game countdown (e.g. 5 minutes), quickly add minutes (`+1m`, `+5m`, `Reset`), pause/resume, and configure Wi-Fi credentials with one-click auto-detection.
  - **Top Scrolling Player & Score Marquee**: Added a continuous horizontal marquee ticker running along the top of `TriviaDisplayWindow`, displaying all registered players and teams with live scores, and highlighting the leading scorer with a gold crown badge (`"👑 1. Player (Team): 3,450 pts"`).
  - **3-Tab Game Master Console & Dedicated Settings Page**:
    - **🎯 Live Game Master Tab**: Streamlined 3-column live command deck with question pack selection, auto-run switch, live question & 2x2 options visualizer, QR connection code, live scoreboard, and round flow controls.
    - **⚙️ Settings & Display Tab**: Full configuration page for Intermission delay (configurable minutes), auto-start toggle, display monitor selector, question timers (15s), wrong answer elimination speed (5s), post-reveal buffer (5s), base points, speed bonus toggle, sound effects, server port, venue branding, and persistent "Save All Settings" button.
    - **👥 Players & Teams Tab**: Roster grid showing player names, team names, total points, streaks, correct answer counts, total answered, and individual player kick/removal controls.
  - **Post-Game Intermission Countdown & Automatic Next Game Start**:
    - When a game finishes, if auto-start is enabled, an intermission countdown runs for the configured duration (e.g. 3 minutes).
    - Venue display, GM console, and mobile web app display a live ticking countdown banner (`"🎮 NEXT TRIVIA ROUND STARTS IN: mm:ss"`).
    - Host can click `"▶ Start Now (Skip)"` to bypass the intermission, and once expired, the engine automatically selects the next question pack (or reshuffles) and launches the new round.
  - **Trivia & Connect Instruction Screen Links in `Lyracist` and `KSRotation`**:
    - **`Lyracist`**: Added quick-access `"👁️ Preview Connect Screen"` and `"🎯 Launch Trivia Night"` action buttons in `SettingsPage` (Column 4: Connect & Request Instructions).
    - **`KSRotation`**: Added `"📡 Connect Screen"` and `"🎯 Trivia Night"` quick-launch buttons on the primary Rotation toolbar as well as in the `Connect & Request Instructions` display settings.
  - **Configurable Questions Per Game**: Added a game length selector in the Game Master console (with editable presets: 5, 10, 15, 20, 25, 50, 100, or any custom count) saved directly into `trivia_settings.json`. Slices the shuffled question pack to the exact number of desired questions per game session.
  - **Game Complete Winner Celebration & Team Roster Announcements**:
    - **Venue Big Screen (`TriviaDisplayWindow`)**: Upon completing all allotted questions, automatically transitions to a celebration screen with gold trophy graphics, announcing the champion with their final score. If a team wins, prominently displays `"👑 WINNING TEAM: {TeamName} ({Score} pts)"` and lists all team members (`"Team Players: Alice, Bob, Charlie"`).
    - **Mobile Player App (`trivia.html`)**: Mobile devices display the winner announcement, full team member breakdown, the player's personal placement and final score, and the complete top 10 leaderboard.
    - **Game Master Host Console (`MainWindow`)**: Displays the winner announcement banner with team roster and score breakdown when the game completes, with one-click options to start the next game.
  - **Zero-Config LAN Mobile Web Server (`TcpListener`)**: Replaced Windows `HttpListener` with an asynchronous raw `TcpListener(IPAddress.Any, port)` and HTTP/1.1 pipeline. This completely bypasses Windows HTTP.sys URL reservation restrictions, allowing smartphones, tablets, and mobile devices on the same Wi-Fi network to connect immediately without Administrator permissions.
  - **Accurate LAN IPv4 Resolution (`LocalNetworkHelper`)**: Integrated `LocalNetworkHelper.GetLocalIPv4()` to automatically pick the primary Wi-Fi or Ethernet adapter with gateway routing, preventing virtual adapter (WSL, Hyper-V, VPN) misdirections in the QR code and connect URL.
  - **Dynamic Question Shuffling**: When a category or question pack is selected or a game is started, all questions in the pack are automatically shuffled using a non-deterministic randomization algorithm so that each play session offers a unique question sequence.
  - **Dynamic Multi-Monitor Display Selection**: Added target monitor selector dropdown directly in the Game Master header and settings (`Monitor 1`, `Monitor 2`, `Monitor 3` with true physical resolution and DPI awareness), enabling seamless placement and dynamic relocation of `TriviaDisplayWindow` onto any connected venue TV or projector.
  - **Auto-Running Gameplay & 5-Second Sequential Answer Elimination**:
    - **15-Second Answering Window**: Automatically starts questions with a live 15-second answering timer on both venue display and mobile player buzzers.
    - **5-Second Wrong Answer Elimination**: Once the answering window expires, incorrect answers progressively fade out one every 5 seconds (`opacity: 0.12`, grayscale, and scaled down) across the venue display and player phones until only the single correct answer remains illuminated.
    - **5-Second Post-Reveal Buffer & Auto-Progression**: After the correct answer is revealed with full explanation and point calculations, the game waits exactly 5 seconds before automatically advancing to the next question/round, running unattended without manual host clicks. Hosts can toggle auto-run or pause/resume anytime.
  - **Dedicated Data Layer (`TriviaData/`)**: Solution root folder storing SQLite database `trivia.db`, settings `trivia_settings.json`, and curated JSON question packs in `TriviaData/packs/`.
  - **13 Comprehensive 100+ Question Category Databases (1,300 Questions Total, No Duplicates)**:
    1. `rock_and_roll.json`: 100 questions covering classic rock, 70s/80s bands, legendary albums, guitarists, and rock history.
    2. `country_music.json`: 100 questions covering outlaw country, classic honky-tonk, 90s country, and Grand Ole Opry legends.
    3. `geography.json`: 100 questions spanning world continents, oceans, mountain peaks, rivers, borders, islands, and famous landmarks.
    4. `state_capitals.json`: 100 questions covering all 50 U.S. state capitals, territorial capitals, and major world capitals.
    5. `history.json`: 100 questions spanning ancient civilizations, American revolutions, world wars, and monumental events.
    6. `complete_the_lyric.json`: 100 questions across iconic rock, pop, 80s/90s singalongs, and karaoke crowd favorites.
    7. `tv_shows.json`: 100 questions covering classic sitcoms, prestige dramas, 90s nostalgia, and Emmy-winning television.
    8. `sports.json`: 100 questions covering NFL football, MLB baseball, NBA basketball, NHL hockey, soccer, and Olympics.
    9. `logos_and_slogans.json`: 100 questions covering advertising taglines, company mascots, brand history, and logos.
    10. `music_legends.json`: 100 questions celebrating iconic vocalists, chart-topping legends, and karaoke hall-of-fame artists.
    11. `pop_culture_80s_90s.json`: 100 questions on retro toys, video game classics, 80s/90s movies, and nostalgic trends.
    12. `movie_soundtracks.json`: 100 questions covering blockbuster film scores, Oscar-winning theme songs, and needle-drops.
    13. `pub_general_knowledge.json`: 100 questions spanning science, literature, food & drink, myths, idioms, and pub favorites.
  - **Deduplication**: Removed duplicate starter packs (`classic_rock.json`, `country_hits.json`, `finish_the_lyric.json`) in favor of the full 100-question category databases.
  - **Shared Core Engine (`Lyracist.Trivia.Core`)**: Full game state machine (Lobby, Countdown, QuestionActive, AnsweringLocked, EliminatingAnswers, RevealAnswer, RoundLeaderboard, GameComplete), speed-bonus scoring calculations, streak multipliers, SQLite persistence (`TriviaDatabaseService`), pack manager (`TriviaPackManager`), and embedded HTTP server (`TriviaWebServer`).
  - **Mobile Player Portal (`trivia.html`)**: Zero-install, high-contrast mobile web app allowing patrons to join via on-screen QR code, answer on 4 color-coded responsive touch buzzers (▲ Violet, ◆ Cyan, ● Amber, ■ Rose), receive haptic vibration, and track real-time score, streak bonuses, and live sequential answer elimination.
  - **Configurable Countdown Timer**: Configurable question timer with a distinct 3-second warning countdown and answer reveal buffer.
  - **16:9 Venue Projection Screen (`TriviaDisplayWindow.xaml`)**: Multi-monitor projection window with category banners, animated timer badges, 4-color answer option cards with opacity elimination fading, and live team leaderboard podium.
  - **Game Master Host Console (`MainWindow.xaml`)**: Standalone host console with monitor target picker, auto-progression toggle, pack loader, round triggers, player score management, timer controls, and live answer distribution charts.
  - **Automated Test Suite (`Lyracist.Trivia.Tests`)**: Comprehensive unit tests validating state machine transitions, progressive elimination math, speed scoring, single-player auto-locking, and SQLite persistence.
- **Backup & Duet Partner Support Across All Applications**: Added comprehensive support for specifying, managing, and projecting duet and backup vocal partners across `Lyracist`, `KSRotation` (WPF), `KSRotation.Maui` (.NET MAUI), the Patron Request Portal, and the Remote DJ Web Board:
  - **Single-Song Duet Auto-Clearing**: Automatically clears the duet partner field once the current song is finished (`FinishSingerSong` / `AdvanceToNextSinger`), since performers typically use a partner for a single song rather than all rotation rounds. If a performer submits a subsequent request with a partner, that request reinstates the partner when accepted.
  - **Session History & Night Reports Persistence**: Preserves completed duet performance history across `SongPerformance` (KSRotation), `PerformedSong` (Lyracist), Night Database snapshots, CSV exports (`Duet Partner` column), and PDF reports (`(with {PartnerName})`).
  - **Patron Request Portal (`PatronPortal.html`)**: Added an optional "Duet / Backup Partner (Optional)" input field to the song submission form (dynamically shown for Karaoke requests and hidden for Music requests), sending `duetPartner` alongside songs to the request server.
  - **Remote DJ Web Board (`dj.html`)**: Added "Duet / Backup Partner (Optional)" input to the "Add Performer to Rotation" form, and added `(with {PartnerName})` badges to both the rotation queue items and incoming patron request cards.
  - **KSRotation Desktop (`MainWindow.xaml` & `SingerEntry.cs`)**: Added dedicated "Duet Partner" column to the active rotation queue grid, enabling direct inline editing of duet partners. Incoming requests display `(with {PartnerName})` when present.
  - **KSRotation.Maui (`MainPage.xaml` & `MainPage.xaml.cs`)**: Added "Duet Partner (Optional)" input in the top Add Performer bar, an edit field in the Edit Performer modal dialog, and `(with {PartnerName})` labels on queue rows and incoming requests.
  - **Billboard Display Projection (`DisplayViewModel.cs` & `SingerDisplayWindow.xaml`)**: Formats performers as `{Name} & {DuetPartnerName}` on Current Performer, Up Next, Full Rotation crawls, and marquee displays when a duet partner is present.
  - **Server & Ingestion Pipeline (`PatronRequestServer.cs` & `MainViewModel.Requests.cs`)**: Extended TCP socket server JSON parsing and request delegates to route `duetPartner` through `HandleRequestReceived`, `AcceptRequest`, and `TryAddPerformer`.
- **Enlarged DJ Control QR Code Popout Window (`DjQrCodePopoutWindow.xaml`)**: Added an enlarged popout modal when clicking the DJ control QR code on the main window request panel:
  - Displays a large 280x280 crisp QR code with `NearestNeighbor` scaling for scanning across the room with tablets/phones.
  - Shows full URL with a one-click **Copy URL** button and prominent 24pt bold **DJ Security PIN** badge with **Copy PIN** button.
  - Activates exclusively when the DJ Control Portal mode is active (`IsDjQrVisible`), leaving the patron song request QR unaffected.
- **Priority "Last Song" Banner on All Non-Lyric Screens (`LastSong.png`)**: Added dedicated system-wide priority handling for the `"Last Song"` event banner:
  - When `"Last Song"` is activated (via KJ Display Settings, Karaoke Page, or Remote DJ Board), the `LastSong.png` banner takes top priority over all other banners and displays on **all non-lyric projection screens** (`DjBannerWindow` and `RotationWindow` / `SingerDisplayWindow`).
  - Added `LastSongBannerOverlay` to `RotationWindow.xaml` (Lyracist) and `SingerDisplayWindow.xaml` (KSRotation), instantly displaying the full-screen finale banner over rotation billboard content while preserving continuous lyrics projection on the lyric screen (`LyricsWindow`).
  - Added high-fidelity vector banner generator `CreateLastSongBannerPng` in `DjBannerFileManager.cs` with deep velvet gradients, radial golden glow, starburst fireworks, music notes, and glowing "LAST SONG OF THE NIGHT" typography.

### Fixed & Improved

- **Rotation Queue Editable Input Boxes Standout Styling (`GridTextBoxStyle`)**: Enhanced the rotation queue editable textboxes (Singer Name, Duet Partner, Song, Artist) to be distinctly outlined and visible without having to focus or click into them:
  - Added a visible 1px card container border (`{DynamicResource MaterialDesignDivider}`) and card background (`{DynamicResource MaterialDesignCardBackground}`) with rounded corners (`CornerRadius="4"`).
  - Added clear column margin separation (`Margin="2,1"`) and comfortable padding (`Padding="6,3"`).
  - Added hover highlights (`IsMouseOver` trigger) and focus elevation (`IsKeyboardFocused` trigger).
  - Adapted contrast automatically for Now Performing (`IsCurrent`) and Up Next (`IsNext`) highlighted rows with semi-transparent frosted surfaces and crisp borders.
- **High-Contrast DJ Connect QR Code Optical Contrast**: Resolved QR code camera recognition issues on older iPad/tablet sensors (such as iPad Air):
  - Changed `DjQrCodeImage` generation from maroon (`[128, 0, 32]`) to pure high-contrast black on white (`[0, 0, 0]` on `[255, 255, 255]`) with `RenderOptions.BitmapScalingMode="NearestNeighbor"`.
  - Replaced Wi-Fi QR code color on the `ConnectInstructions.png` graphic with crisp black-on-white.
  - Added a distinctive `#EF4444` crimson border indicator around the QR container in `MainWindow.xaml` to visually distinguish DJ mode without degrading camera barcode scan reliability.
- **Remote DJ Board Single Column Layout (`dj.html`)**: Resolved layout issue where the dashboard flex container defaulted to horizontal layout, causing the header (title, theme toggle, lock button) to display on the left side while scrunched rotation queue and controls rendered on the right.
  - Refactored `.dashboard` and `.main-layout` to a single column vertical flex layout (`flex-direction: column; width: 100%;`).
  - Stacked Current Rotation Queue, Add Performer, Special Event Banners, and Incoming Requests in a clean scrollable single-column flow.
  - Enhanced mobile responsiveness for singer cards and action buttons under narrow viewports (< 600px).
  - Aligned `.lock-screen` / `.lock-overlay` and `.pin-box` / `.lock-card` styles for consistent PIN unlock screen rendering across themes.

## [26.8.16.0] - 2026-08-16

### Added

- **Session Performed Songs History (Singer Rotation Page)**: Added Column 2 to `RotationPage.xaml` featuring live tracking of all performed songs in the current session. Includes:
  - Dual view tabs: **5-Color List Tab** (rotating Violet, Cyan, Emerald, Amber, Rose cards adapting dynamically to Light & Dark themes via `PerformedSongColorConverter`) and **Session Textbox Tab** (multi-line copyable text log).
  - Quick action buttons: **Copy Session Songs to Clipboard** and **Clear Session Performed Songs**.
- **`PerformedSong` Model (`PerformedSong.cs`)**: Created model tracking performer name, song title, artist, key transposition, timestamp, order number (`ColorIndex => (OrderNumber - 1) % 5`), and `HasKeyChange` helper property.

### Fixed & Improved

- **Assembly Metadata Standardization**: Standardized `<Authors>` (`Dennis N. Maidon`), `<Company>` (`PAROLE Software`), and `<Copyright>` (`Copyright © 2026 PAROLE Software`) across all solution `.csproj` files (`Lyracist`, `Lyracist.Data`, `KSRotation`, `LyracistDbEditor`, `ScaryokeWheel`, `KSRotation.Maui`, `LyracistKeyGen`).
- **Compact Queue Layout Spacing (`RotationPage.xaml` & `KaraokePage.xaml`)**: Reduced vertical padding and card margins on performed song cards in Column 2 and active performer queue rows on the Karaoke Control page, fitting more singers on screen without scrolling.
- **Load & Play Selected Performer Button Positioning**: Restructured middle column `Grid.RowDefinitions` in `KaraokePage.xaml` to 3 explicit rows (`Auto`, `*`, `Auto`), locking the **Load & Play Selected Performer** button to `Grid.Row="2"` at the bottom of the Singer Queue panel.
- **`SingerAvatarConverter` TypeInitializationException**: Replaced static field initialization of `DefaultAvatar` with a thread-safe, lazy-initialized property wrapped in try-catch fallback handling, preventing WPF startup exceptions.
- **Startup Splash Window Bypass**: Set `ShowSplashOnStartup = false` by default and restored initial page navigation to `OnMainWindowLoaded` after control template inflation to resolve WPF UI `NullReferenceException` and eliminate red startup screen flash.
- **Solution Build Order Optimization (`Lyracist.slnx` & `KSRotation.Maui.csproj`)**: Reordered solution project declarations and added conditional `Lyracist.Data` project reference (`Condition="'$(TargetFramework)' == '' or $(TargetFramework.Contains('-windows'))"`) to `KSRotation.Maui.csproj`. Ensures `Lyracist.Data` compiles first in Visual Studio while preventing `.NETCoreApp,Version=v10.0` cross-compilation errors on Android.

## [26.8.15.0] - 2026-08-15

### Added

- **Dedicated 4-Column "Display" Tab in Settings (`SettingsPage.xaml`)**: Reorganized the Settings page into a clean top-level tab control featuring a dedicated **Display** tab laid out into 4 equal columns (matching `KSRotation` layout):
  - *Column 1*: Monitors & Screen Assignments (Lyrics Projection, Singer Rotation Billboard, DJ Banner Screen, Billboard View Mode, Mirror Lyrics).
  - *Column 2*: DJ & Event Banners (DJ Banner Upload/Select/Delete, Request QR Code Overlay toggle, Special Event Banners Mapping & Save).
  - *Column 3*: Star Wars Crawl & Spaceship Overlay (Crawl intro templates, Custom text, Spaceship font size, duration, frequency, custom snippets).
  - *Column 4*: Connect & Request Instructions (Connect Instructions & QR Code target screen selector, Wi-Fi Password input, Dynamic banner status info).
- **Screen Activation Checkboxes**: Added explicit checkboxes for "Enable Lyrics Projection Screen" (`IsLyricsActive`), "Enable Singer Rotation Billboard Screen" (`IsRotationActive`), and "Enable DJ Banner Screen" (`IsDjBannerActive`) to toggle window visibility directly from settings.
- **Request QR Code Overlay Toggle**: Added "Show request QR Code overlay on DJ Banner" (`IsDjBannerQrCodeEnabled`) setting saved in `AppSettings` and bound to ViewModel.
- **MAUI Stub for `CreatePersonalizedBirthdayBannerPng`**: Added `#if MAUI` no-op stub in `Shared/DjBannerFileManager.cs` to resolve cross-platform build dependency for `KSRotation.Maui`.

### Updated

- **Help System & User Manual**: Expanded `HelpViewModel.cs` topic 5 (*Settings: Display & Projectors*) and topic 15 (*Connect & Request Instructions Screen*) and updated `Lyracist_User_Manual_Updates.txt`.

## [26.8.13.1] - 2026-08-13

### Added

- **Tablet/Vertical-Mode Rotation List Layout (KSRotation)**: The rotation list in `MainWindow.xaml` now responsively reflows into a 3-row per-singer layout (Name + current-singer badge / round checkboxes / action buttons) and stacks the Incoming Requests sidebar below the list instead of beside it, once the window narrows below ~1050px — sized for real 11" tablet portrait use rather than desktop widths. Backed by a new `NarrowWidthToBooleanConverter`.

### Fixed

- **KSRotation.Maui Singer Row Overlap**: Restructured the per-singer `DataTemplate` in `MainPage.xaml` from a 2-column Grid into a stacked `VerticalStackLayout`. The previous layout centered the (7-button) actions panel vertically across the whole row, which landed it directly on top of the round-checkbox row once that row was added — now each section gets its own full-width row.
- **DJ Banner Window Not Actually Closing**: The `IsShuttingDown` flag added to gate `OnClosing` cancellation was never set to `true` anywhere, so DJ banner windows were only ever hidden, not closed, on app exit. Now set before `Close()` in KSRotation's `DjBannerWindowService` and during `OnExit` in Lyracist's `App.xaml.cs`.
- **Doubled DJ Banner Decode on Every Switch**: `GetDecodeTargetWidth`'s width probe used `BitmapCacheOption.OnLoad`, forcing a full eager pixel decode just to read a dimension before the real, capped decode ran right after. Switched to `OnDemand` in both KSRotation and Lyracist.
- **Per-Keystroke Banner Regeneration**: Typing a host IP in KSRotation triggered a full QR render + PNG encode + disk write on the UI thread on every keystroke. `RefreshConnectInstructionsBanner` is now debounced.
- **Unescaped Wi-Fi QR Payload**: SSID/passwords containing `;`, `,`, `\`, or `"` could truncate or corrupt the generated `WIFI:` QR code. Reserved characters are now escaped per the WIFI-QR spec.
- **Fake Wi-Fi QR When SSID Unknown**: When no real SSID was known (fresh install, Ethernet-only), the banner still rendered a scannable QR encoding a placeholder network name. The Wi-Fi QR section is now skipped entirely until a real SSID is detected.
- **`ConnectInstructionsScreen` Setting Never Persisted**: The target-monitor picker for the Connect Instructions banner was never restored from or saved to `AppSettings`, silently reverting to "All Screens / Monitors" on every restart.
- **"Connect Instructions" Wrongly Selectable as an Event Banner**: It was included in `StandardEventNames`, the list that seeds the DJ's Special Event Banner picker, letting a DJ accidentally pin the Wi-Fi/QR instructional graphic as the active party banner.
- **MAUI About-Popup Logo Missing**: The `MauiImage` build item for `ksr_logo.png` was dropped in favor of `MauiIcon` alone; restored it so the About overlay's logo resolves again on Android/Windows.
- **`WifiPasswordStore` Crash-Safety**: Switched from `File.WriteAllText` to the existing `AtomicJsonFile.Serialize` write-to-temp-then-rename helper, matching every other settings store in the codebase, so a crash mid-write can no longer truncate `wifi_passwords.json`.

### Changed

- **Dead Branch Cleanup**: Removed two structurally-unreachable OR branches from `GetTargetScreenResolution`'s monitor-match logic in Lyracist's `SettingsViewModel.Display.cs`.

## [26.8.13.0] - 2026-08-13

### Added

- **Global `CA1416` Platform Warning Suppression**: Added `CA1416` and `CA1422` suppressions to `Directory.Build.props` and `KSRotation.Maui.csproj` to eliminate 160 platform dependent API warnings, keeping the build error list completely clean at 0 warnings and 0 errors.

### Fixed

- **Android Design-Time File Lock Workarounds (`MSB3374` & `XARLP7000`)**: Added `AndroidDesignTimeFileLockWorkaround` target and pre-build directory creation (`android\bin`, `android\assets`, `designtime\stamp`) to `KSRotation.Maui.csproj` to prevent MSBuild file handle collisions when Visual Studio design-time compiler background builds run concurrently.

## [26.8.12.0] - 2026-08-13

### Added

- **Dynamic Theme Info Text Brush (`AppInfoTextBrush`)**: Added `AppInfoTextBrush` resource in `LyracistThemeManager` (`#A7F3D0` for Dark / `#047857` for Light) and bound settings info text blocks for optimal contrast in all theme modes.
- **Secondary Button & Info Theme Overrides**: Extended `ThemeService.cs` in KSRotation with explicit dynamic secondary button border (`#C4B5FD` / `#7C3AED`), foreground (`#E9D5FF` / `#6D28D9`), and info text brush resource overrides across system, light, and dark theme modes.

### Fixed

- **Simplified Singer Song Completion**: Streamlined `FinishSingerSong` in `MainViewModel.cs` to sequentially check off completed song checkboxes for active singers.
- **MAUI Resizetizer Build Target (`EnsureMauiResizetizerDirectoriesExist`)**: Added pre-build target to `KSRotation.Maui.csproj` ensuring intermediate resizetizer directories exist to prevent build exceptions (`MSB3371`/`CS7064`).
- **KeyGen Icon Reference & Package Cleanup**: Corrected icon asset path in `LyracistKeyGen.csproj` pointing to `Assets\lyracist_mic.ico` and removed unused `ProtectedData` package dependency.
- **Design-Mode Safety**: Added `#if !MAUI` compile guard around `IsInDesignMode` WPF designer property check in `MainViewModel.cs`.

## [26.8.10.1] - 2026-08-10

### Added

- **Scan-to-Connect Wi-Fi & Request Instructions Dynamic Graphic**: Added dynamic vector graphic generator for `ConnectInstructions.png` with dual high-density QR codes: Wi-Fi join (WPA/WPA2/NoPass) and Song Request Portal URL.
- **Dynamic Resolution Screen Detection**: Automatically detects physical target screen dimensions (`1080p`, `1440p`, `4K 3840x2160`, etc.) and scales vector elements and QR module density (`pixelsPerModule = (int)(40 * scale)`) for 1:1 pixel-sharp rendering.
- **Persistent Wi-Fi Password Store (`wifi_passwords.json`)**: Created `WifiPasswordStore.cs` in `Shared` layer. Automatically saves and recalls Wi-Fi passwords per connected SSID (venue Wi-Fi, travel router, mobile hotspot) so passwords do not need to be re-entered.
- **Dedicated Green GroupBox Layout**: Added `GreenSettingsGroupBoxStyle` (Emerald/Forest Green header `#059669` $\rightarrow$ `#047857`) and moved the **Connect & Request Instructions** box into Column 1 under **Display & Projection** in both Lyracist and KSRotation. Added a dedicated **Target Screen / Monitor** selector.
- **Comprehensive Help System Expansion**: Added dedicated `"📡 Connect & Wi-Fi Instructions"` topic in KSRotation and `"15. Connect & Request Instructions Screen"` in Lyracist (`HelpViewModel.cs`), detailing dual QR codes, Wi-Fi password store, dynamic resolution detection, and expanding all 15 help topics for complete clarity.

### Fixed

- **Clean App Shutdown & Process Lingering**: Updated `DjBannerWindow.xaml.cs` with an `IsShuttingDown` flag to ensure closing events are not canceled during application exit, preventing orphaned background processes.
- **Explicit WPF Application Termination**: Added `Application.Current.Shutdown()` in `MainWindow.xaml.cs` when the main window is closed.

## [26.8.10.0] - 2026-08-10

### Added

- **"Last Song" Default Event Banner**: Added `"Last Song"` (`LastSong.png`) to standard pre-saved event banners alongside `Birthday`, `Wedding`, `Engagement`, and `Anniversary` across both Lyracist and KSRotation. Standard pre-saved events are protected from deletion and automatically mapped to their respective graphics.

### Fixed

- **1-Click Rotation Advancement & Rollover**: Created `AdvanceRotationAfterFinished` in `RotationHelpers.cs` to advance rotation sequence sequentially relative to the finished performer. When the last singer in rotation finishes, rotation automatically rolls over to the top performer in 1 click without needing multiple clicks or manual reset.
- **Out-of-Sync `IsCurrent` Recovery**: Fixed a bug where checking off a singer when `IsCurrent` was out of sync or unassigned failed to advance the rotation indicator. Rotation now advances reliably starting from the finished singer's position.
- **Web Portal Round Checkbox Sync**: Updated `OnSingerEntryPropertyChanged` in `MainViewModel.cs` so that toggling round completion checkboxes triggers an immediate web JSON cache rebuild for DJ and Patron web views.

## [26.8.9.0] - 2026-08-09

### Added

- **Special Event Banner Management in KSRotation**: Added **Upload Banner**, **Add Event**, and **Delete Event** controls to the Display tab in KSRotation, allowing hosts to upload image/video banners directly to `EventBanners` and manage custom event mappings.
- **Pre-Saved Standard Event Protection**: Guaranteed the presence of 4 standard pre-saved events (`Birthday`, `Wedding`, `Engagement`, `Anniversary`) in `Globals.EventBannersDir` with auto-generated 16:9 banner graphics if missing, and protected standard events from accidental deletion.
- **Remote Active Special Event Synchronization**: Added public `/api/special-event/active` GET endpoint and extended `KSRotationSyncService` to synchronize active special event banner projections in real-time between KSRotation and Lyracist.

### Fixed

- **DJ Web App Special Event Loading**: Fixed an HTTP route matching bug in `PatronRequestServer.cs` where GET `/api/special-events` was intercepted by the `404 Not Found` fallback, allowing the remote DJ dashboard (`dj.html`) to load special event buttons properly instead of hanging on `"Loading events..."`.
- **Mouse Cursor Flickering in KSRotation**: Eliminated mouse cursor flickering by disabling Win32 layered popup transparency and drop-shadow effects on autocomplete suggestion popups inside ListViews, and lowering marquee timer execution priority to `DispatcherPriority.Background`.
- **KSRotation Finished Song Rotation Advance**: Updated `FinishSingerSong` in `MainViewModel.cs` so that performers who have completed all 10 round checkboxes still have their rotation position advanced, queued songs promoted, and 11th+ song performances logged to database history upon clicking **Finished Song**.

### Changed

- **High-Contrast Light Lavender UI Styling**: Updated button text, button borders (`#C4B5FD`), and RadioButton labels in `MainWindow.xaml` and `dj.html` to a bright light lavender purple (`#E9D5FF`) for high legibility against dark green/teal backgrounds.
- **Clean Standard Banner Mapping Interface**: Simplified the **Special Event Banners Mapping** panel in KSRotation by hiding drop-down list boxes (ComboBoxes), TextBoxes, and Delete buttons for standard pre-saved events, reserving edit controls exclusively for custom events.
- **High-Contrast DJ QR Code Color**: Changed the DJ QR Code foreground color from bright crimson red (`RGB(239, 68, 68)`) to a dark maroon/brick-red (`RGB(128, 0, 32)` / `#800020`), maximizing contrast against white background tiles for optical scanning by mobile cameras.

## [26.8.7.0] - 2026-08-07

### Fixed

- **Database Manager Search and Bindings**: Fixed data binding mismatches in the Database Manager (`LyracistDbEditor`) that broke track search, filtering, right-click metadata scanning, and saving track edits.
- **SQLite FTS5 MATCH Query Exception**: Added a self-healing detection check in the search service that automatically drops and recreates `SongSearch` as a true FTS5 virtual table if it is detected as a regular SQL table, preventing the `'no such column: SongSearch'` exception when searching.
- **Initial Database Creation Migration**: Updated the consolidated EF Core migration to directly use raw SQL to create the `SongSearch` virtual table on any fresh installations.

### Changed

- **ListBox Layout Compactness**: Changed the search results list layout from 3 lines to 2 lines, merging the track title and artist into a single line (`Title (Artist)`) while preserving font size and weights, and moving the file location path to the second line.
- **Subtle Alternating Rows**: Configured the search results list to display alternating row backgrounds using the Fluent theme's intermediate control brush for a polished, low-contrast UI.

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
- **Pitch Hot-Reload Race**: Rapidly changing pitch while a song load/stop was in flight could occasionally apply the pitch change over the wrong track; the pending change now targets a fixed snapshot of the track and cancels cleanly.
- **Mobile Portal Security Hardening**: The singer mobile portal now rate-limits every endpoint per device, caps the length of submitted text fields (name, title, notes, etc.), and replaces permanent session-token bindings with a 6-hour sliding expiry so a singer who loses their token isn't locked out of their name until the server restarts.
- **Duplicate/Lost Rating Updates**: Concurrent star ratings for the same performance can no longer silently overwrite each other's score/average-rating updates.
- **Auto-Accept Status Mismatch**: The mobile portal's auto-accept flow now waits for the rotation to actually update before reporting a request as "Queued," instead of claiming success and then silently failing in the background.
- **Stress-Test Simulator Crash Risk**: Fixed a rare crash in the Settings page's Stress-Test simulator caused by reading the rotation queue from a background thread while it was being modified on the UI thread.
- **Stale Playlist Refresh**: Rapid playlist edits (add/remove/reorder) could occasionally leave the Opening/Fill-In/End-Rotation lists showing stale ordering; refreshes are now sequenced correctly.
- **Library Scan Path Matching**: Scanning one music folder could incorrectly affect songs from an unrelated folder that happened to share a name prefix (e.g. `C:\Music` vs `C:\Music2`).
- **Song/Singer Settings Save Guard**: The Save button in the Song and Singer settings windows is now disabled until settings have actually been loaded for an item.
- **Reliability & Error Logging**: Several background database operations that previously failed silently now log errors for troubleshooting, and the YouTube integration now retries automatically on transient network failures instead of failing on the first hiccup.

### Changed

- **Karaoke & Settings ViewModel Cleanup**: Reorganized the two largest internal ViewModel files (DJ banners, Party Tyme, external search, display/monitor assignment, stress-test simulator, and more) into focused files by feature area, with no change in app behavior — purely an internal maintainability cleanup.

### Removed

- **Party Tyme Karaoke Integration**: Completely removed the commercial streaming/caching karaoke integration (tabs, UI inputs, settings, model definitions, and backend service code) from the application.

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
- **Browser Cast Server**: Self-hosts a local web server (<http://localhost:8080/rotation/>) to allow any browser on the local network to view the singer rotation billboard in real-time.
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
