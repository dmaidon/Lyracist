<!-- Edited on Sep 18, 2026 @ 09:26:00 -> Update Special Singer top of list placement and current singer promotion in KSRotation Changelog -->

# Changelog

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
  - Added Skip/Unskip action button, amber `SKIPPED` badge, and row opacity triggers in `KSRotation.Maui` (`MainPage.xaml`, `MainPage.xaml.cs`, `BillboardView.xaml`).
  - Added `toggle-skip` action to web request handler in `MainViewModel.Requests.cs`.
  - Added visual amber `⏭ SKIPPED` badges, row styling, and controls to Remote DJ board (`dj.html`), Billboard (`billboard.html`), Kiosk (`kiosk.html`), and Patron Request Portal (`PatronPortal.html`).
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
