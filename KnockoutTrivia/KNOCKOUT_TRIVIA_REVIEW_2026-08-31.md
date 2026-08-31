# Review of KnockoutTrivia & Repair Plan

Full read-through of `KnockoutTrivia/` (all 40 source files, ~4,400 lines) looking for bugs, memory leaks, and enhancements, verified against the actual code (not just the file headers' change-log comments). Ordered by priority. File links are repo-relative from `KnockoutTrivia/`.

---

## P0 — Fix before the next show

### 1. Live countdown display updates from a background thread and can silently fail every second
**Files:** [Services/IGameStateService.cs:386-398](Services/IGameStateService.cs:386) (`OnTimerTick`), consumed at [Views/MainView.xaml:54](Views/MainView.xaml:54)

`GameStateService` uses a `RunOnUI(...)` helper everywhere it mutates state that the UI binds to — **except** in `OnTimerTick`, which runs on the raw `System.Threading.Timer` callback thread (a ThreadPool thread, not the UI thread):

```csharp
private void OnTimerTick(object? state)
{
    if (!IsTimerRunning) return;
    SecondsRemaining--;                          // raises PropertyChanged off the UI thread
    TimerTicked?.Invoke(this, SecondsRemaining);  // fires off the UI thread
    if (SecondsRemaining <= 0)
    {
        StopTimer();
        RunOnUI(RevealAnswer);                    // <- only this last call is marshaled
    }
}
```

The top-of-file comment says threading was already "fixed... eliminated deadlock hazard" — that fix covered the `RevealAnswer()` call but missed the tick itself. `GameViewModel` exposes the raw singleton as `GameState` (`ViewModels/GameViewModel.cs:20`), and the Game Host screen binds directly to it:

```xml
<TextBlock Text="{Binding GameState.SecondsRemaining, StringFormat='⏱️ {0}s'}" .../>
```

WPF does not auto-marshal `PropertyChanged` raised from a non-UI thread for a plain binding like this — it normally throws `InvalidOperationException` ("calling thread cannot access this object"). That exception is caught by the global handler in `App.xaml.cs:107-111` (`e.Handled = true`), so the app won't crash, but the practical effect is the DJ's own on-screen countdown can silently fail to update once a second during every single active question, while the log fills with the same swallowed exception all night.

**Fix:** Wrap the whole body of `OnTimerTick` in the existing `RunOnUI(...)` helper, same as every other mutation in this class, instead of only wrapping the final `RevealAnswer()` call.

### 2. `Players` is read from background threads while it's mutated on the UI thread
**Files:** [Services/KnockoutWebServer.cs:295](Services/KnockoutWebServer.cs:295), [Services/ISimulatorService.cs:114](Services/ISimulatorService.cs:114) and [:210-217](Services/ISimulatorService.cs:210)

Every mutation of `GameStateService.Players` (an `ObservableCollection`) is correctly wrapped in `RunOnUI(...)` (dispatcher thread) inside `GameStateService`. But two other call sites **read** it directly from background threads with no synchronization at all:

- `KnockoutWebServer.RouteRequestAsync`'s `/api/knockout/state` handler runs inside a `Task.Run`-spawned socket handler and does `_gameStateService.Players.ToList()` directly — this is the endpoint every connected phone polls roughly once a second.
- `SimulatorService.OnQuestionChanged` kicks off a `Task.Run` for auto-play bots that calls `SimulateQuestionAnswersAsync`, which does `_gameStateService.Players.Where(...).ToList()` on that same background task.

`ObservableCollection<T>` is not thread-safe for concurrent enumerate-while-mutate. With a full room of phones joining/polling while the UI thread adds/removes players (`RegisterOrGetPlayer`, `RemovePlayer`), this is a real, load-dependent race that can throw `InvalidOperationException: Collection was modified` mid-request, or hand back an inconsistent/torn leaderboard.

**Fix:** Pick one consistent approach — either marshal these reads through the dispatcher too, or replace ad-hoc dispatcher marshaling with a real lock (or a `lock`-guarded snapshot list) shared by every mutation and every read of `Players`, so background threads never touch the live `ObservableCollection` directly.

### 3. Saving Settings mid-game silently wipes the current game's progress
**File:** [ViewModels/SettingsViewModel.cs:190-217](ViewModels/SettingsViewModel.cs:190)

`SaveSettingsAsync` unconditionally reloads questions on every save:

```csharp
Settings.SelectedSourcePaths = AvailableSources.Where(s => s.IsSelected).Select(s => s.FilePath).ToList();
_configService.SaveSettings(Settings);
await _gameStateService.LoadQuestionsFromSourcesAsync(Settings.SelectedSourcePaths);
```

`LoadQuestionsFromSourcesAsync` (`Services/IGameStateService.cs:201-213`) reloads the full question list, reshuffles it, and resets `CurrentQuestionIndex` back to `0` — with no check of whether the source selection actually changed, and no check of `IsGameActive`. If the DJ opens Settings mid-game just to switch the audience monitor or update Wi-Fi info and clicks Save, the live game's question order and progress are silently thrown away.

**Fix:** Only call `LoadQuestionsFromSourcesAsync` when `Settings.SelectedSourcePaths` actually differs from what's currently loaded, and/or prompt for confirmation when `_gameStateService.IsGameActive` is true before reloading.

---

## P1 — High-value fixes

### 4. Web server start failure is invisible to the DJ
**File:** [Services/KnockoutWebServer.cs:69-83](Services/KnockoutWebServer.cs:69)

If the configured port (default `8088`) is already bound — another instance still shutting down, another app, a previous crashed run holding the socket — `Start()` catches the exception, writes to `Debug.WriteLine` only (invisible outside a debugger), and leaves `IsRunning = false`. The Connect screen still renders a QR code and URL as if nothing's wrong; players just can't reach it, with no on-screen explanation of why.

**Fix:** Surface the failure back up (return a `bool`/result, or raise an event `ConnectViewModel`/`MainViewModel` can subscribe to) so the UI can show "Port 8088 is already in use — try a different port" instead of failing silently.

### 5. `Players` never gets pruned — unbounded growth if the app stays running across multiple events
**File:** [Services/IGameStateService.cs:560-592](Services/IGameStateService.cs:560) (`ResetGame`)

`ResetGame()` resets every player's stats (`Score`, `StrikeCount`, `Tokens`, etc.) but never removes anyone from `Players`. `RegisterOrGetPlayer` only reuses an existing record when the name matches **and** the player is currently disconnected — it never actively deletes stale entries. For a venue that leaves the app running across multiple trivia nights instead of restarting between events (a very plausible deployment for a standing PC), every distinct player name from every past night accumulates in memory indefinitely, and gets iterated on every `/api/knockout/state` poll, every `EvaluateSubmittedAnswers` pass, and every `AnsweredCount`/`ActivePlayerCount` computation, forever.

**Fix:** Add an explicit "New Event / Clear All Players" action (distinct from the in-round `ResetGame()`) that clears `Players` entirely, and/or prune players whose `LastSeenAt` is older than a reasonable idle threshold as part of `ResetGame()`.

### 6. Settings save failures are swallowed and reported as success
**Files:** [Services/IConfigService.cs:68-80](Services/IConfigService.cs:68), [ViewModels/SettingsViewModel.cs:216](ViewModels/SettingsViewModel.cs:216)

`ConfigService.SaveSettings` catches and discards every exception ("`// Ignore write errors in stub`"), and `SettingsViewModel.SaveSettingsAsync` unconditionally sets `StatusMessage = "Settings saved!..."` regardless of whether the write actually succeeded. A locked file, a full disk, or a permissions issue means the DJ's Wi-Fi password, monitor picks, and source selection silently fail to persist while the UI claims success.

**Fix:** Have `SaveSettings` return a `bool` (or throw and let the caller catch), and reflect real success/failure in `StatusMessage`.

---

## P2 — Consistency / cleanup

### 7. Repeated answer submissions stack redundant auto-reveal timers
**File:** [Services/IGameStateService.cs:287-339](Services/IGameStateService.cs:287) (`SubmitPlayerAnswer`)

There's no guard against a player re-submitting after `HasAnsweredCurrentQuestion` is already `true`. In `Automatic` game mode, once the "everyone's answered" threshold is crossed, **every** subsequent duplicate submission (a network retry, a double-tap) re-enters the `Task.Delay(1000).ContinueWith(...)` branch and schedules another delayed reveal. It's harmless today only because the reveal itself is idempotent-guarded (`if (Phase == QuestionActive && !IsAnswerRevealed)`), but it's wasted timers/allocations that scale with however many duplicate taps happen right at the end of a question.

**Fix:** Add `if (player.HasAnsweredCurrentQuestion) return false;` near the top of the method (or explicitly decide answer-changing before reveal should be allowed, and only evaluate the auto-reveal threshold once instead of on every resubmission).

### 8. `StrikeColorBrush` allocates a new brush on every read
**File:** [Models/KnockoutPlayer.cs:83-89](Models/KnockoutPlayer.cs:83)

```csharp
public Brush StrikeColorBrush => StrikeCount switch
{
    0 => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
    ...
};
```

This is a computed property re-evaluated on every `StrikeCount` change (`[NotifyPropertyChangedFor(nameof(StrikeColorBrush))]`) for every player row in the scoreboard/roster — each read allocates a brand-new unfrozen `SolidColorBrush`. Minor GC pressure, and unfrozen brushes are also the wrong choice when the same visuals get shared with the audience/projector window.

**Fix:** Four `static readonly` brushes, `Freeze()`d once, selected by the same switch — or bind `StrikeColorHex` through a converter instead of exposing a `Brush` property at all.

### 9. `App.OnExit` bypasses the `Dispose()` methods it already has
**File:** [App.xaml.cs:101-105](App.xaml.cs:101)

```csharp
protected override void OnExit(ExitEventArgs e)
{
    base.OnExit(e);
    Environment.Exit(0);
}
```

`GameStateService` and `KnockoutWebServer` both implement `IDisposable` (timers, `CancellationTokenSource`, `TcpListener`) and both are correctly cleaned up in their own `Dispose()` — but nothing ever calls it, because `OnExit` terminates the process immediately, and both services only exist as `OnStartup` locals rather than app-level fields. Not a cross-run leak (the OS reclaims the handles on process exit either way), but it means graceful shutdown never runs, and makes the existing `Dispose()` implementations dead code.

**Fix:** Promote `gameStateService` and `webServer` to fields on `App`, and call `Dispose()` on both at the top of `OnExit` before `Environment.Exit(0)`.

---

## Verified correct, no action needed

- **`KnockoutWebServer` connection handling** — semaphore-limited concurrent connections (64), 15s read timeout via linked `CancellationTokenSource`, 2 MB body cap, proper `using`/`await using` disposal of sockets and streams, and a real `Dispose()`. Solid.
- **`TriviaDataService`** — every SQLite connection/reader uses `await using`; no leaked connections.
- **`TokenService` / `StreakService`** — token award/deduct and streak/Super-Streak math are straightforward and correctly bounded (`Math.Clamp`, `maxTokens` check).
- **`SimulatorService`** — the one service in this project that actually unsubscribes its event handlers and disposes its `CancellationTokenSource` in `Dispose()` (it's just never called — see item 9, but the implementation itself is correct).
- **`AudienceWindow` lifecycle** (`ViewModels/MainViewModel.cs:109-144`) — correctly cached as a singleton and reused via `Show()`/`Hide()` rather than recreated, with `_audienceWindow = null` only on the (effectively never-hit, since the window is borderless and only closes via `Hide()`) `Closed` event. No leak from repeated open/close.
- **`DisplayService`/multi-monitor positioning** — delegates to the same shared `WindowPositioner`/`MonitorEnumerator` the main Lyracist app uses; nothing new here to break.

---

## Suggested execution order

1. **Item 1 (countdown cross-thread update)** first — smallest possible fix (wrap one method body), and it's the one most likely to be an actual visible symptom the DJ has already noticed (a frozen/stuttering countdown on the host screen).
2. **Item 2 (thread-unsafe `Players` access)** next — same root cause category as item 1; worth fixing both threading issues in one pass through `GameStateService`.
3. **Item 3 (Settings save wipes game progress)** — isolated to `SettingsViewModel`, high user-visible impact, low risk to fix.
4. **Items 4 & 6 (silent failures)** together — both are "surface the error instead of swallowing it," same shape of fix in two places.
5. **Item 5 (`Players` growth)** — needs a product decision (what should "New Event" mean vs. "Reset Game"), so worth a quick confirmation of intended behavior before implementing.
6. **Items 7–9** as a small cleanup pass, low urgency, safe to batch together.
