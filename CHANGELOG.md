# Changelog

All notable changes to the Lyracist project are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [26.7.5.85] - 2026-07-05

### Added
- **Display "None" Option**: Users can now select *None (Do not show)* in monitor dropdowns (Settings and the main panel). Selecting this closes/hides the target projection window immediately.
- **Test Mode Setting**: Added a "Test Mode" checkbox under *Settings → Theme & Appearance*. When checked, it seeds the default performer queue (Alice, Bob, etc.) for quick testing. When unchecked, it clears the queue so the system is ready for the night.
- **Diagnostic Configuration**: Added `.editorconfig` to the project root to silence noisy code analysis messages (e.g. `MVVMTK0042`, `CA1805`, `CA1822`, and string localization suggestions) from the Visual Studio Error List.

### Changed
- **Ambiguous ListBox Reference Fixed**: Fully qualified the type `System.Windows.Controls.ListBox` in `KaraokePage.xaml.cs` to resolve compiler warning/error `CS0104` caused by overlapping namespace references.
- **Queue Deselection**: Clicking the active/highlighted current singer in the rotation queue now deselects them immediately. This allows KJs to easily correct accidental selection clicks.
- **Redundant Package Cleanup**: Removed redundant reference to `Microsoft.Extensions.Hosting` inside `Lyracist.csproj` as the ASP.NET Core framework reference (`Microsoft.AspNetCore.App`) already provides these classes, resolving restore warning `NU1510`.
- **Code Style Refactoring**: Ran `dotnet format` to resolve code styles, whitespace discrepancies, and unnecessary syntax constructs across 15 source files.

---

## [26.7.4.3] - 2026-07-04

### Added
- **Singer History Database**: Created a database table to save singer name, song title, artist, key shift, and streaming links.
- **Tablet Lyrics server on Port 5005**: Implemented local WebSocket/HTTP Hub server enabling mobile browser preview for performers.
- **Scaryoke Mode**: Initial integration of a themed "Wheel of Doom" picker for randomized singer challenges.
