<!-- Created on Sep 4, 2026 @ 23:05:00 -> Log Trivia and Trivia Settings text-contrast fix in Dark mode -->

# Changelog

## 2026-09-04

- Fixed low-contrast (dark-on-dark) label text on the **Trivia** and **Trivia Settings** pages when running in Dark mode. Labels such as "Questions Per Game", the question timer fields (Time/Warn/Fade/Reveal), scoring fields (Base Points, Wrong Answer Deduction, tiered option percentages), venue/host display, Pre-Game Welcome Banner, and the Wi-Fi/network fields (Port, SSID, Password) were bound to `MaterialDesignBody`, which was not resolving to the Dark-mode override for plain `TextBlock` elements (confirmed live in the running app — the same labels rendered correctly once switched).
  - Switched 21 `TextBlock` `Foreground` bindings in `MainWindow.xaml` from `{DynamicResource MaterialDesignBody}` to `{DynamicResource MaterialDesignBodyLight}`, which was already rendering correctly in Dark mode elsewhere in the app (e.g. "GAME MASTER CONTROLS", "LIVE QUESTION CONTROLS" headers).
  - Left `CheckBox` content bindings using `MaterialDesignBody` untouched — those were already rendering correctly.
  - This is theme-aware (keeps following Light/Dark/System selection) rather than a hardcoded color.
  - Note: this same `MaterialDesignBody`-on-`TextBlock` pattern likely exists on other tabs (Rotation, Settings, Display) too, since it's the same underlying app resource — not changed here since this pass was scoped to the Trivia pages.
