# Implementation Brief: Filler Automix + Sample Pad (for Antigravity)

Hand-off spec. Antigravity implements; Claude reviews, fine-tunes and finishes afterwards.
Written 2026-10-07 against `master` at `aeaa412`+.

## Scope (in order of delivery)

| # | Feature | Size |
|---|---------|------|
| A | Per-track loudness leveling in the filler player | S |
| B | Configurable crossfade length + "fade to next every N minutes" | S |
| C | Sample / jingle pad (Lyracist laptop only) | S-M |
| D | BPM analysis (audio-based) + Smart Shuffle for filler | M |
| E | Mix-in / mix-out cue points | M |
| F | Optional performance recording (device-gated, off by default) | M |

**Explicitly OUT of scope:** key/harmonic detection (BPM only), tablet/`dj.html` triggering of the pad, sharing/download links/QR for recordings (a possible later phase F2), KSRotation / KSRotation.Maui changes.

**Note on F:** the user's own mixer (Yamaha MG10XU) has not been verified to expose its USB return as a Windows recording device, but some DJs will have it, so F must be completely inert unless a recording input device is selected. It must never affect shows for users without one. Do F last.

## Ground rules (repo conventions)

- .NET 10 / WPF, `CommunityToolkit.Mvvm`, WPF-UI (`ui:` controls), NAudio.Wasapi + LibVLCSharp already referenced. Do not add native dependencies.
- Nullable enabled. Match surrounding code style and comment density. Start each edited file with the repo's `// Edited on <date> @ <time> -> <summary>` header comment.
- Shared code lives in `Shared/` (file-linked into projects, not a compiled library) or `Lyracist.Data`. Settings go through `Lyracist/Core/Helpers/AppSettings.cs` (JSON in `Settings/`). Schema changes need an EF migration (`Context.Database.Migrate()` runs at startup).
- Tests: xUnit v3 in `Lyracist.Tests`. Pure logic gets unit tests; DB tests use the real `lyracist.db` with unique markers and clean up in `Dispose` (see `ScanningServiceTests.cs`). Full suite must stay green (currently 343).
- Commit the auto version-bumped `.csproj` files with the work. **Never push.** Never add/commit `Deploy-Lyracist.ps1`.
- Update `CHANGELOG.md` and `README.md` in the existing style.

## Existing code to build on

- `Lyracist/Media/Audio/BackgroundMusicPlayer.cs`: LibVLC dual-player (A/B) filler/opening/end/occasion player. Fixed `CrossfadeDuration = 5s`, `ShufflePlaylist()` random shuffle, `OnMonitorTick` (250 ms) triggers `StartCrossfade()` when `length - time <= crossfade`. `Volume`/`Duck()`/`CurrentTargetVolume` compute output volume. Registered as keyed singletons "Opening", "FillIn", "EndRotation", "Occasion" in `App.xaml.cs`.
- `Lyracist/Services/Media/ShowFlowService.cs`: loads playlists into the players, ducks/resumes fill-in around singer songs.
- `Lyracist/Services/Media/MediaEngine.cs` (~L639-655): reference implementation of loudness normalization: `ILibraryService.GetMeasuredLoudness(path)`, `AppSettings.NormalizeVolumeEnabled`, `AppSettings.TargetLoudnessLufs`, `normFactor = 10^((target - measured)/20)`, clamped 0..200, and `MeasureAndSaveLoudnessAsync` when missing.
- `Lyracist.Data/Models/Song.cs`: has `MeasuredLoudnessLufs`, `Key`, `BPM` (BPM/Key currently come from file **tags only** via `FFprobeRunner.DetectBpm/DetectKey`; the user's filler tracks are untagged, so BPM will be mostly null until D).
- `LyracistDbEditor` Library Health tab already has a readiness/loudness backlog audit (`LibraryHealthViewModel.cs`, `OperationState` single-slot guard) - extend it rather than adding a new tool.
- `FillInPlaylistItem` (SongId, Order) feeds `PlaylistService.GetFillInPlaylist()`.

## A. Loudness leveling for the filler player

- In `BackgroundMusicPlayer.PlayTrack`, scale the player volume per track using the same formula as `MediaEngine` (honour `NormalizeVolumeEnabled` and `IsHardwareMixerMode`). Needs the player to know each track's gain: add an optional `Func<string, double?> LoudnessLookup` (set by `ShowFlowService` from `ILibraryService.GetMeasuredLoudness`) rather than referencing the library service directly.
- Apply the gain inside `CurrentTargetVolume` (per active/inactive player during crossfades) so ducking and fades still work. Missing loudness => no adjustment, and kick off `MeasureAndSaveLoudnessAsync` in the background.
- Tests: extract the gain math into a small static helper in `Shared/` and unit test clamping / missing-value cases.

## B. Crossfade length + elapsed-time automix

- New settings in `AppSettings`: `FillInCrossfadeSeconds` (default 5, range 1-15), `FillInMaxTrackSeconds` (0 = off; e.g. 180 = fade to next after 3 minutes).
- `BackgroundMusicPlayer`: replace the const with a property; in `OnMonitorTick`, if `MaxTrackSeconds > 0 && time >= MaxTrackSeconds*1000` and not crossfading, call `StartCrossfade()`. The "short track" case (track shorter than the crossfade) must not misbehave.
- Settings UI: add to the existing fill-in/background music section of `SettingsPage.xaml` + `SettingsViewModel`.
- Applies to Opening/FillIn/EndRotation players via the same properties; Occasion (non-looping) must ignore elapsed-time automix.

## C. Sample / jingle pad

- `Shared/` or `Lyracist/Services/Media/SamplePadService`: preloads clips into RAM and plays through NAudio (`WasapiOut` + decoded float buffer) for low latency; a small pool for overlapping clips; `StopAll()`. Output device = new setting `SamplePadOutputDevice`, defaulting to the background-music device (same MMDevice id format as `SelectedKaraokeAudioDevice`).
- Ducking: while any clip plays, `ShowFlowService.DuckFillIn()` / `Unduck`. Do **not** duck the karaoke `MediaEngine`.
- Persistence: `Settings/samplepad.json` (label, file path, color, hotkey, volume, order). No DB migration. Handle missing files gracefully (button shown disabled with a tooltip).
- UI: new page/panel (4x4 grid, WPF-UI buttons), drag-and-drop a file onto a pad to assign, right-click to edit/clear, stop-all button. Optional F1-F12 hotkeys at window level; they must NOT fire while a `TextBox` has focus (the app has global select-all-on-focus via `TextBoxSelectionHelper`).
- Tests: JSON store round-trip, missing-file handling, pool/overlap logic where unit-testable.

## D. BPM analysis + Smart Shuffle

1. **BPM analysis** (pure managed, in `Lyracist.Data`, e.g. `Services/BpmAnalyzer.cs`): decode via ffmpeg to mono PCM (reuse the existing ffmpeg plumbing in `FFmpegService`; analyze ~60-90 s from the middle of the track), compute an onset-strength envelope, autocorrelate, fold into 70-180 BPM, return rounded BPM or null when confidence is low. Never overwrite a tag-sourced BPM. Unit test against synthetic click tracks at known tempos.
2. Hook into the DbEditor readiness audit: when `Key/BPM` tag lookup is empty for non-karaoke (filler) songs, run the analyzer. Add a "BPM" backlog count next to the existing counts. Respect `OperationState` and cancellation.
3. **Smart Shuffle** in `BackgroundMusicPlayer`: new setting `FillInSmartShuffle` (default off). Pure function `NextTrackSelector` (unit tested): given current track + candidates (BPM, artist, recent-play history), pick the next with BPM within a tolerance (+-8%, allow half/double), avoid same artist back-to-back and the last N plays; fall back to random when BPM unknown. Replaces `(_currentIndex + 1) % Count` only when enabled.
4. Needs per-track metadata in the player: pass a lightweight `FillInTrack { Path, Artist, Bpm }` list (extend `LoadPlaylist`) instead of bare paths; keep the old overload.

## E. Mix-in / mix-out cue points

- New nullable `Song.MixInMs` / `Song.MixOutMs` (EF migration). Detected in the DbEditor readiness audit with ffmpeg `silencedetect` (leading silence end -> mix-in; trailing silence start / level drop -> mix-out). Provide a manual override later; not required now.
- `BackgroundMusicPlayer`: seek the incoming track to `MixInMs` when starting a crossfade, and start the crossfade at `MixOutMs - crossfade` instead of at `length - crossfade` when set.
- Do this last; it depends on A/B being solid.

## F. Optional performance recording (local only)

**Concept:** with a USB mixer (e.g. Yamaha MG10XU) the mixer's main stereo mix - singer's mic plus the backing track the PC plays into it - can appear to Windows as a *recording* device. Lyracist captures that device while a singer performs and saves it as the singer's recording.

- **Gating:** new `AppSettings`: `RecordPerformancesEnabled` (default **false**), `RecordingInputDevice` (MMDevice id, empty = none). With the feature off or no device chosen, no capture object is created and nothing changes. A missing/unplugged device must degrade to "not recording" with a status message, never an exception during a show.
- **Capture:** NAudio `WasapiCapture` (shared mode) on the chosen capture device, writing WAV to disk as it records (stream to file; do not buffer a whole song in RAM). Capture in the device's native format; do not resample on the audio thread.
- **Start/stop:** hook `MediaEngine.Started` / `Stopped` / `SongEnded` (`Lyracist/Services/Media/MediaEngine.cs` L40-42). Start when a *singer's karaoke song* starts (not filler/opening music, not sample-pad clips). Stop on `SongEnded`/`Stopped`; if the same song restarts, begin a new take. Discard recordings under ~10 seconds.
- **Who:** only record singers with a per-singer opt-in flag (new `Singer.AllowRecording`, default false; exposed in the existing singer profile editor in the Users tab). No flag = no recording. Unknown singer = no recording.
- **Encode:** after stopping, convert WAV -> MP3 in the background with the existing ffmpeg plumbing, delete the WAV on success, keep it on failure. Optionally trim leading/trailing silence with the existing pipeline helper. Never block playback or the next singer.
- **Storage:** `Recordings\<yyyy-MM-dd>\<Singer> - <Title>.mp3` under the app data dir (sanitize names; reuse the filename sanitizing approach from the DbEditor renamer). Setting for retention (`RecordingRetentionDays`, default 30, 0 = keep forever) with a prune on startup, and a free-disk-space check that refuses to start recording below ~500 MB free.
- **Data:** new `PerformanceRecording` table (Id, SingerName or SingerId, SongTitle, Artist, StartedUtc, DurationSeconds, FilePath) + EF migration. Link by singer name/time to `SingerHistoryEntry` (that table stores `SingerName`, not an id).
- **UI:** (1) Settings: enable toggle, input device picker with a live level meter and a "Test (record 5 s)" button, retention days, open-folder button. (2) A "Performances" list (per singer in the profile and a global list): play, open folder, delete. Show a small "REC" indicator in the main window while capturing.
- **Legal/safety:** off by default; the settings text must note that recordings may include copyrighted backing tracks and that the host is responsible for consent and local law. No uploading or sharing in this phase (F2, later: tokenized expiring LAN link + QR via `TabletLyricsServer`).
- **Tests:** filename sanitizing, retention pruning, the start/stop state machine (extract it from the capture so it's unit-testable with a fake recorder), minimum-duration discard, opt-in gating. Real capture is manual-test only.

## Acceptance criteria

- Everything builds with 0 warnings; full test suite green; new logic has unit tests.
- Defaults preserve today's behavior exactly (smart shuffle off, elapsed automix off, crossfade 5 s, normalization follows the existing `NormalizeVolumeEnabled` setting, recording off).
- No regressions to Opening / End-of-Rotation / Occasion players or to duck/unduck around singer songs.
- Docs: CHANGELOG + README updated; version-bumped csproj files committed; nothing pushed.

## Leave for Claude to fine-tune afterwards

Review of threading/dispatcher use in `BackgroundMusicPlayer`, shutdown and cancellation behavior, disposal of NAudio/LibVLC resources, settings-UI polish and edge cases, performance of the BPM batch job on a large library, and test hardening.
