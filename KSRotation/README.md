<!-- Edited on Sep 8, 2026 @ 13:48:00 -> Add Users tab description and modern search input controls -->

# KSRotation

KSRotation ("Karaoke Singer Rotation") is a WPF (.NET, C#, Material Design in XAML) desktop app for running karaoke nights, built and used by Dennis for Maidon Woodworking-adjacent DJ/karaoke hosting.

## Tabs

- **Rotation** — Manages the singer rotation queue for the night.
- **Users** — Centralized user, performer, and staff management with high-DPI quick-search, profile management, and history.
- **Settings** — Venue, DJ, banner, and general app settings (theme, etc.).
- **Display** — Controls the audience-facing display window(s)/monitor output.
- **Trivia** — Live "Game Master" controls for running a trivia round (question packs, questions-per-game, live question navigation, timer controls, scoring reveal, patron mobile portal status).
- **Trivia Settings** — Configuration for trivia gameplay: question timers & flow, scoring & bonus multipliers, active venue/host (inherited from app settings), intermission/pre-game timing, and local network/Wi-Fi access for the mobile buzzer/patron portal.
- **Help** / **About** — In-app help and version/about information.

## Theming

The app supports Light/Dark/System theme selection (`Services/ThemeService.cs`), built on MaterialDesignInXaml's `BundledTheme`, with additional app-specific brush overrides (card backgrounds, header colors, split-flap display colors, etc.) applied at runtime based on the selected theme.

See `CHANGELOG.md` for recent fixes and changes.
