<!-- Edited on Sep 8, 2026 @ 13:48:00 -> Log Users page search textbox visibility fix on 1080p screens and DisplayViewModel accessibility fix -->

# Changelog

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
