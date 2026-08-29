# Knockout Trivia — Repair Plan

Prepared from a manual code review of `KnockoutTrivia/` (Services, ViewModels, Models, App/Web server). Ordered by priority. Each item lists root cause, evidence, and the concrete fix to apply.

---

## P0 — Game-breaking bugs

### 1. Super Streak Wheel can never trigger with default settings
**File:** [Services/IStreakService.cs:28-52](Services/IStreakService.cs:28)

`RecordAnswer` awards a token and resets `StreakCount = 0` whenever `StreakCount >= StreakRequirement` (default 5), **before** checking `StreakCount == SuperStreakThreshold` (default 20). Since 20 is a multiple of 5, the counter is always reset to 0 at 5/10/15 and never reaches 20 — the Super Streak Wheel feature (a headline mechanic per recent commits) is dead code under default config, and under any config where `SuperStreakThreshold` is reachable only by passing a `StreakRequirement` multiple first.

**Fix:** Check the Super Streak milestone against the pre-reset value, or track lifetime consecutive-correct count separately from the token-reward streak:
```csharp
public void RecordAnswer(KnockoutPlayer player, bool isCorrect, int streakRequirement = 5, int superStreakThreshold = 20, int maxTokens = 3)
{
    if (isCorrect)
    {
        player.StreakCount++;

        if (player.StreakCount == superStreakThreshold)
        {
            SuperStreakReached?.Invoke(this, player);
        }
        else if (player.StreakCount >= streakRequirement && player.StreakCount % streakRequirement == 0)
        {
            _tokenService.AwardToken(player, maxTokens);
            TokenRewardEarned?.Invoke(this, player);
        }
    }
    else
    {
        ResetStreak(player);
    }
}
```
Also drop the `player.StreakCount = 0` reset on token award — resetting the streak on a token reward is what created the collision. Verify against `SettingsViewModel` that `SuperStreakThreshold` can never be configured as non-multiple-safe (or just make the check order-independent as above regardless of settings).

### 2. Cross-thread crash when players finish answering before the timer expires
**File:** [Services/IGameStateService.cs:305-315](Services/IGameStateService.cs:305)

```csharp
if (active > 0 && AnsweredCount >= active && Settings.GameMode == GameAdvanceMode.Automatic)
{
    Task.Delay(1000).ContinueWith(_ =>
    {
        if (Phase == GameStatePhase.QuestionActive && !IsAnswerRevealed)
        {
            RevealAnswer();
        }
    });
}
```
`ContinueWith` runs on a ThreadPool thread. `RevealAnswer()` sets `IsAnswerRevealed`/`Phase` (bound `ObservableObject` properties) directly — no dispatch to the UI thread. Compare with `OnTimerTick` ([IGameStateService.cs:373-377](Services/IGameStateService.cs:373)), which correctly calls `RunOnUI(RevealAnswer)`. This path is reachable any time every mobile player answers before the countdown ends in Automatic mode, and throws `InvalidOperationException: The calling thread cannot access this object because a different thread owns it`, crashing the app mid-show (see item 4 — there's no handler to catch it).

**Fix:**
```csharp
Task.Delay(1000).ContinueWith(_ =>
{
    if (Phase == GameStatePhase.QuestionActive && !IsAnswerRevealed)
    {
        RunOnUI(RevealAnswer);
    }
});
```

### 3. Global exception handling is missing — any unhandled exception kills the app during a live event
**File:** [App.xaml.cs:10](App.xaml.cs:10)

`OnStartup` is `async void` with no try/catch, and there is no `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, or `TaskScheduler.UnobservedTaskException` handler registered anywhere. Item 2's crash (and any future one) takes down the whole process with no recovery, no log, and no message to the DJ running a live show.

**Fix:** In `App.xaml.cs`, add:
```csharp
public App()
{
    DispatcherUnhandledException += (s, e) =>
    {
        LogFatal(e.Exception);
        MessageBox.Show($"Knockout Trivia hit an unexpected error and needs to continue:\n{e.Exception.Message}",
            "Knockout Trivia", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true; // keep the show running
    };
    AppDomain.CurrentDomain.UnhandledException += (s, e) => LogFatal(e.ExceptionObject as Exception);
    TaskScheduler.UnobservedTaskException += (s, e) => { LogFatal(e.Exception); e.SetObserved(); };
}
```
Wrap the body of `OnStartup` in try/catch and show a startup-failure dialog instead of a silent crash if question/database loading throws.

---

## P1 — High-impact bugs

### 4. Duplicate player names silently merge two different phones into one identity
**File:** [Services/IGameStateService.cs:225-262](Services/IGameStateService.cs:225) (`RegisterOrGetPlayer`)

When a join request's `playerId` doesn't match an existing player, the code falls back to matching by `Name` (case-insensitive). Two different guests who both type "Alex" become the *same* `KnockoutPlayer` — the second phone silently takes over the first person's score, strikes, and connection state. With double-digit crowds this is a likely, not edge-case, collision.

**Fix:** Only reuse an existing record by name if it is not currently connected (a genuine "same person reconnecting on a new device" case):
```csharp
existing = Players.FirstOrDefault(p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase) && !p.IsConnected);
```
If no match, always create a new player — even on a name collision — and have the mobile page disambiguate visually (e.g., append last-4-of-id or a color badge to duplicate display names in the leaderboard).

### 5. Lock + `Dispatcher.Invoke` combination is a deadlock hazard
**File:** [Services/IGameStateService.cs:281-319](Services/IGameStateService.cs:281) (`SubmitPlayerAnswer`) vs [Services/IGameStateService.cs:446-493](Services/IGameStateService.cs:446) (`EvaluateSubmittedAnswers`)

`SubmitPlayerAnswer` (called from a background web-server thread) takes `lock (_syncLock)` and then calls `RunOnUI(...)`, which blocks on `Dispatcher.Invoke` while still holding the lock. `EvaluateSubmittedAnswers` does the reverse: `RunOnUI(() => { lock (_syncLock) { ... } })`. If the DJ triggers "Reveal Answer" on the UI thread at the same instant a phone submits an answer, the UI thread can block acquiring `_syncLock` while the background thread is blocked in `Dispatcher.Invoke` waiting for that same UI thread to service its queued call — a deadlock that freezes the app.

**Fix:** Never call `Dispatcher.Invoke` while holding `_syncLock`. Prefer dispatching the *entire* mutation to the UI thread and dropping the lock — `Players` is an `ObservableCollection` that should only be touched from the UI thread anyway, so the lock is largely redundant once every mutation already goes through `RunOnUI`. If cross-thread reads of `Players`/state must stay safe from the web server thread, use a lock that is held only around plain data reads/writes, never around a `Dispatcher.Invoke` call.

---

## P2 — Correctness / robustness

### 6. Questions get shuffled redundantly, with a weak shuffle algorithm
**Files:** [Services/TriviaDataService.cs:144-145,197-198,215-216](Services/TriviaDataService.cs:144), [Services/IGameStateService.cs:216-223](Services/IGameStateService.cs:216)

Every loader (`LoadQuestionsFromDatabaseAsync`, `LoadQuestionsFromPackAsync`, `LoadQuestionsFromMultipleSourcesAsync`) shuffles its own list with a fresh `new Random()` and `.OrderBy(_ => random.Next())`, and then `GameStateService.LoadQuestionsFromSourcesAsync`/`LoadGameQuestionsAsync` calls `ShuffleQuestions()` again on top. That's up to 3 redundant O(n log n) shuffles per load, using a biased shuffle (`OrderBy` on a random key is not a uniform permutation).

**Fix:** Shuffle exactly once, at the point questions are finalized in `GameStateService`, using `Random.Shared` and an in-place Fisher-Yates:
```csharp
public void ShuffleQuestions()
{
    var rng = Random.Shared;
    for (int i = _questions.Count - 1; i > 0; i--)
    {
        int j = rng.Next(i + 1);
        (_questions[i], _questions[j]) = (_questions[j], _questions[i]);
    }
}
```
Remove the shuffle calls from `TriviaDataService`'s loader methods — they should return questions in natural order and let `GameStateService` own randomization.

### 7. Submitted answer index isn't validated against the question's option count
**File:** [Services/KnockoutWebServer.cs:260-281](Services/KnockoutWebServer.cs:260), [Services/IGameStateService.cs:281-319](Services/IGameStateService.cs:281)

`SelectedOptionIndex` from the mobile client is accepted with no range check. Harmless today (an out-of-range index just never equals `CorrectAnswerIndex`), but it means a malformed/malicious client can never be distinguished from a normal miss, and any future feature that indexes `Options[selectedIndex]` will throw. Add a bounds check in `SubmitPlayerAnswer` and reject (return `false`) if `answerIndex < -1 || answerIndex >= (CurrentQuestion?.Options.Count ?? 0)`.

### 8. No session token — any client can act as any player by guessing/observing an 8-char ID
**File:** [Services/KnockoutWebServer.cs:229-281](Services/KnockoutWebServer.cs:229)

`playerId` returned at `/api/knockout/join` is the only credential needed to submit answers as that player on `/api/knockout/submit`. On a private venue LAN this is low risk, but worth hardening: issue a random session token at join (separate from the short display `Id`) and require it on `submit`/`state` calls.

---

## Suggested execution order for Antigravity

1. Apply fixes 1–3 (P0) together — they are small, isolated, and each independently testable.
2. Apply fix 4 and 5 (P1) together since both touch `RegisterOrGetPlayer`/`SubmitPlayerAnswer` in the same file.
3. Apply fix 6 (P2) as a standalone refactor; re-verify question order still randomizes across a full game.
4. Fixes 7–8 are additive hardening — safe to defer or bundle into a "companion API hardening" pass.

### Manual verification checklist after fixes 1–3
- Set `GameMode = Automatic`, `StreakRequirement = 5`, `SuperStreakThreshold = 20` (defaults) and answer 20 questions correctly in a row from a phone client — confirm the Super Streak Wheel triggers.
- In Automatic mode, connect 2+ phones and have all of them submit an answer before the countdown reaches 0 — confirm the app reveals the answer without crashing.
- Force an exception (e.g., temporarily throw inside a command) and confirm the app shows a dialog and keeps running instead of exiting.
