# Review of Today's Changes (2026-08-30) & Repair Plan

Verified against the actual repo state (commits `8101ec6`, `ccd1f3c`, plus the untracked deploy script) rather than the summary alone. Ordered by priority.

---

## P0 — Fix before the next show

### 1. `Banners/**` (336 MB) is committed straight into git with no exclusion
**Evidence:** `git ls-files Banners Packs Data` → 103 tracked files; `du -sh Banners` → 336 MB; no entry for `Banners/`, `Data/*.db`, or `Packs/` in `.gitignore`. Commit `ccd1f3c` alone added ~50 binary PNGs/MP4s.

This is a live-show asset folder that will keep growing every time a DJ adds a new banner — and git never shrinks: even a later `git rm` leaves the blobs in history forever. Left as-is, every clone/fetch of this repo gets slower and heavier permanently, and GitHub's soft repo-size warnings become a real problem within a few more show seasons.

**Fix:** Decide what actually belongs in git:
- Practically none of `Banners/**` needs version control — it's per-venue deployment content, not source. Add it to `.gitignore`, `git rm -r --cached Banners`, and let the existing Robocopy `Deploy-Lyracist.ps1` (or a similar publish step) be the only distribution mechanism.
- If some "starter" banners genuinely need to ship with a fresh checkout, keep only those in git and gitignore everything else, or move the whole folder to Git LFS / a release asset instead of the main tree.
- `Data/*.db` (`trivia.db`) and `Packs/*.json` are much smaller (1.4 MB / 760 KB) and are legitimate seed content — fine to keep tracked, but confirm `Data/lyracist.db`, `ksrotation_night_db.json`, etc. (the *live* per-venue databases) are never accidentally added; they should stay gitignored since the deploy script explicitly excludes them from redeploys as "live data."

### 2. Bulk metadata scan permanently gives up on "Unknown Artist" tracks
**File:** [Lyracist.Data/Services/ScanningService.cs:409-411](Lyracist.Data/Services/ScanningService.cs:409), [Lyracist.Data/Services/ScanningService.cs:583-586](Lyracist.Data/Services/ScanningService.cs:583)

Two changes compound into a regression:
- The "needs probing" query dropped `|| s.Artist == "Unknown Artist" || s.Artist == null` — a song is now considered "done" purely based on `Duration`/`Tags`.
- `ProbeSongMetadataAsync`'s step 4 unconditionally sets `song.Tags = "none"` whenever `Tags` is empty, **even if the artist could not be resolved and even if TagLib/FFprobe both failed** — the only thing that keeps a song out of future scans.
- The online MusicBrainz lookup (`MetadataFetchService.FetchMetadataAsync`) that used to resolve karaoke tracks whose filename *and* ID3 tags both lacked an artist has been removed from the bulk path entirely (it now only runs from `LyracistDbEditor`'s manual per-song action).

Net effect: the very first bulk scan after this change permanently marks every track — including ones that stay stuck on "Unknown Artist" because neither the filename nor the embedded tags had an artist — as fully probed. There is no automatic way left for those tracks to ever get resolved; a full rescan will report 0 files needing work even though some are still "Unknown Artist."

**Fix — pick one:**
- (a) Keep `Artist == "Unknown Artist"` in the WHERE filter so a song without a resolved artist keeps showing up for local (fast) re-probing attempts, and add a lightweight periodic "resolve remaining Unknown Artists via MusicBrainz" pass (opt-in, rate-limited, run once after the fast bulk scan finishes) instead of folding it into the hot path.
- (b) If dropping online resolution from bulk scans was intentional for speed, that's a reasonable tradeoff — but surface it: after a scan, report a count of remaining "Unknown Artist" tracks so the DJ knows to fix them manually in `LyracistDbEditor`, and document the behavior change in the CHANGELOG (currently not mentioned).

---

## P1 — High-value fixes

### 3. Deploy script's live-Banners protection has a gap
**File:** `C:\Temp\Deploy-Lyracist.ps1:71-81`

The script explicitly protects `Settings/*_settings.json` and several `Data/*.db`/`*.json` files from being clobbered by a redeploy ("Never clobber live DJ / venue configurations"), and includes an exclusion for `lyracist_trivia_selected_announcement.json`. But:
- That exclusion is only wired into the `Settings` folder's exclusion list, and it protects `Settings/lyracist_trivia_selected_announcement.json` (Lyracist.Trivia's own announcement-selection file, confirmed at [Lyracist.Trivia/Services/AnnouncementHelper.cs:65](Lyracist.Trivia/Services/AnnouncementHelper.cs:65)).
- There are two *other*, differently-located selection files — `Banners/KSRotation/Announcements/selected_announcement.json` and `Banners/Lyracist/Announcements/selected_announcement.json` — each holding a live `{"SelectedFileName": "..."}` value. The `Banners` folder branch of the deploy loop has **no exclusion list at all** (`$Exclusions = @()` falls through untouched), so these two files get overwritten by whatever the dev machine's copy contains on every redeploy.

**Fix:** Confirm whether KSRotation/Lyracist actually write to `Banners/*/Announcements/selected_announcement.json` at runtime (worth a quick check — the reference wasn't found via `AppPaths.AnnouncementsDirectoryPath` usage, so verify it isn't already dead data before spending time protecting it). If it is live state, add it to the `Banners` folder's Robocopy exclusion the same way `Settings` and `Data` are already excluded:
```powershell
if ($Folder -eq "Banners") {
    $Exclusions = @("selected_announcement.json")
}
```

### 4. Deploy script and its log live untracked in `C:\Temp`
**File:** `C:\Temp\Deploy-Lyracist.ps1`

This is the only mechanism that gets tonight's build onto the show laptop, and it lives in a temp-cleanup-prone folder with no version history and no backup. A disk cleanup tool, a Windows temp-purge policy, or a simple typo'd `rm` wipes the one script that knows how to safely deploy without clobbering live venue data — and there'd be no way to reconstruct the current, working `/XF` exclusion list from memory.

**Fix:** Move it into the repo (e.g. `Deploy/Deploy-Lyracist.ps1`) and commit it. It's a small text file — no repo-bloat concern like item 1 — and it directly benefits from version history given how easy it is to introduce exactly the kind of silent exclusion gap in item 3.

---

## P2 — Consistency / cleanup

### 5. `Lyracist.Trivia.Core/Services/TriviaStorageHelper.cs` duplicates path logic instead of using the new shared `Globals`
**File:** [Lyracist.Trivia.Core/Services/TriviaStorageHelper.cs:18,36,46,56](Lyracist.Trivia.Core/Services/TriviaStorageHelper.cs:18)

The commit message for `Lyracist.Trivia.Core.csproj` says "link Globals.cs," but no `<Compile Include="..\Shared\Globals.cs">` was actually added to the csproj — `TriviaStorageHelper` still hardcodes `AppDomain.CurrentDomain.BaseDirectory` + `"Data"`/`"Packs"`/`"Settings"`/`"Banners"` independently rather than calling `Lyracist.Shared.Globals.DataDir` / `.PacksDir` / `.SettingsDir` / `.GetBannersDir("LyracistTrivia")` like every other project in this consolidation. It happens to resolve to the same paths today only because `Globals.StartupPath == AppDomain.CurrentDomain.BaseDirectory` for non-MAUI builds — but it's now the one project maintaining a second, hand-copied source of truth for the shared directory layout, and it will silently drift the next time `Globals` changes (e.g., any MAUI-specific path branch).

**Fix:** Link `Shared/Globals.cs` into `Lyracist.Trivia.Core.csproj` and rewrite `TriviaStorageHelper` to delegate to `Globals` like `KSRotation/Services/AppPaths.cs` does.

### 6. Verified correct, no action needed
Spot-checked the other "legacy settings migration" claims against each app's actual pre-consolidation path (via `git show HEAD~2:...`) — `KSRotation/Services/SettingsService.cs`, `Lyracist/Core/Helpers/AppSettings.cs`, `KnockoutTrivia/Services/IConfigService.cs`, `ScaryokeWheel/ScaryokeSettings.cs`, and `LyracistDbEditor/LibraryDirectoryStore.cs` all correctly point their one-time fallback `File.Copy` at the real prior location (`%AppData%\Lyracist\settings.json`, `KoTrivia\Config\game_settings.json`, `%LocalAppData%\ParoleSoftware\ScaryokeWheel\settings.json`, etc.) — this part of the consolidation is solid and doesn't need changes.

---

## Suggested execution order

1. **Item 1 (repo bloat)** first and alone — it's a one-time git history decision (`git rm --cached` + `.gitignore`), best done before more binary assets pile on top of the 336 MB already committed.
2. **Item 2 (Unknown Artist regression)** — small, isolated change to `ScanningService.cs`; re-run a scan on a library with a few tagless karaoke files afterward to confirm they still show up as needing work.
3. **Items 3–4 (deploy script)** together — pull the script into the repo, then add the missing exclusion once its Banners write-path is confirmed.
4. **Item 5** as a small cleanup pass, low urgency.

### Verification checklist
- After item 1: `git ls-files Banners | wc -l` should be 0 (or a small, deliberate curated set), and `git clone` size should drop noticeably.
- After item 2: point `ProbeMissingMetadataAsync` at a test library containing a karaoke file with no filename artist and no ID3 tags — confirm it's still reported as needing a probe on a second run, or confirm the new "remaining Unknown Artist" report (whichever fix is chosen) surfaces it.
- After item 3: change `Banners/KSRotation/Announcements/selected_announcement.json` on the "laptop" copy, redeploy from a dev build with a different selection, confirm the laptop's selection survives.
