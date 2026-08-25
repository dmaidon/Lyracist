// Edited on Aug 22, 2026 @ 11:20:00 -> Added manual game flow controls, question navigation, timer adjustments, and voiding support
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Lyracist.Trivia.Core.Models;

namespace Lyracist.Trivia.Core.Services;

public class TriviaGameEngine : IDisposable
{
    private readonly System.Timers.Timer _tickTimer;
    private readonly ConcurrentDictionary<string, TriviaPlayer> _players = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _stateLock = new();

    private readonly List<int> _pendingWrongIndices = [];
    private int _eliminationCountdownSeconds;
    private int _postRevealCountdownSeconds;
    private int _leaderboardCountdownSeconds;

    public TriviaSettings Settings { get; set; }
    public TriviaGameSession CurrentSession { get; private set; }
    public TriviaGameState State => CurrentSession.State;

    public int RemainingSeconds { get; private set; }
    public int TotalCountdownSeconds { get; private set; }
    public bool IsInWarningCountdown => State == TriviaGameState.QuestionActive && RemainingSeconds <= Settings.WarningCountdownSeconds && RemainingSeconds > 0;

    public List<int> EliminatedAnswerIndices { get; } = [];

    public int IntermissionSecondsRemaining { get; private set; }
    public bool IsInIntermission => State == TriviaGameState.GameComplete && IntermissionSecondsRemaining > 0;

    /// Counts every StartGame() call this session (each auto-restart calls it again), so
    /// CompleteGame can stop auto-restarting once Settings.TotalGamesToPlay is reached.
    public int GamesPlayedCount { get; private set; }

    public bool IsPaused { get; private set; }
    public string? PauseReason { get; private set; }

    public event EventHandler<TriviaGameState>? StateChanged;
    public event EventHandler<int>? TimerTick;
    public event EventHandler<TriviaQuestion>? QuestionStarted;
    public event EventHandler<List<int>>? AnswersEliminated;
    public event EventHandler<TriviaQuestion>? AnswerRevealed;
    public event EventHandler<List<TriviaPlayer>>? LeaderboardUpdated;
    public event EventHandler<TriviaGameResult>? GameCompleted;
    public event EventHandler<int>? IntermissionTick;
    public event EventHandler? IntermissionCompleted;
    public event EventHandler<string?>? GamePaused;
    public event EventHandler? GameResumed;

    public TriviaGameEngine(TriviaSettings? settings = null)
    {
        Settings = settings ?? new TriviaSettings();
        CurrentSession = new TriviaGameSession { VenueName = Settings.VenueName };

        _tickTimer = new System.Timers.Timer(1000);
        _tickTimer.Elapsed += OnTimerTick;
        _tickTimer.AutoReset = true;
    }

    private static readonly TimeSpan PlayerDisconnectTimeout = TimeSpan.FromSeconds(20);

    public List<TriviaPlayer> GetPlayers()
    {
        // Phones poll /api/trivia/state roughly once a second while connected, refreshing
        // LastSeenAt. A player with no fresh poll in PlayerDisconnectTimeout has dropped off
        // WiFi/closed the tab - mark them disconnected so they no longer block early-reveal.
        var cutoff = DateTime.Now - PlayerDisconnectTimeout;
        foreach (var p in _players.Values)
        {
            if (p.IsConnected && p.LastSeenAt < cutoff)
            {
                p.IsConnected = false;
            }
        }

        return _players.Values.OrderByDescending(p => p.TotalScore).ToList();
    }

    public bool RemovePlayer(string name)
    {
        if (!_players.TryRemove(name.Trim(), out _)) return false;
        LeaderboardUpdated?.Invoke(this, GetPlayers());
        return true;
    }

    public TriviaPlayer RegisterPlayer(string name, string teamName = "")
    {
        string trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            trimmed = $"Player_{_players.Count + 1}";
        }

        var player = _players.GetOrAdd(trimmed, key => new TriviaPlayer
        {
            Name = key,
            TeamName = string.IsNullOrWhiteSpace(teamName) ? string.Empty : teamName.Trim(),
            IsConnected = true,
            ConnectedAt = DateTime.Now,
            LastSeenAt = DateTime.Now
        });

        player.IsConnected = true;
        player.LastSeenAt = DateTime.Now;
        LeaderboardUpdated?.Invoke(this, GetPlayers());
        return player;
    }

    public void StartGame(List<TriviaRound> rounds, string? title = null)
    {
        lock (_stateLock)
        {
            _tickTimer.Stop();
            GamesPlayedCount++;
            CurrentSession = new TriviaGameSession
            {
                Title = title ?? "Pub Trivia Night",
                VenueName = Settings.VenueName,
                Rounds = rounds,
                CurrentRoundIndex = 0,
                CurrentQuestionIndex = 0,
                State = TriviaGameState.Lobby
            };

            foreach (var p in _players.Values)
            {
                p.TotalScore = 0;
                p.CurrentStreak = 0;
                p.MaxStreak = 0;
                p.TotalCorrect = 0;
                p.TotalAnswered = 0;
                p.LastAnswerIndex = -1;
                p.HasAnsweredCurrentQuestion = false;
                p.VisibleOptionsAtSubmission = 4;
                p.RemainingSecondsAtSubmission = 0;
            }

            EliminatedAnswerIndices.Clear();
            _pendingWrongIndices.Clear();

            SetState(TriviaGameState.Lobby);
        }
    }

    public void StartCurrentQuestion()
    {
        PrepareCurrentQuestion(startTimerImmediately: true);
    }

    public void PrepareCurrentQuestion(bool startTimerImmediately)
    {
        lock (_stateLock)
        {
            var q = CurrentSession.CurrentQuestion;
            if (q == null) return;

            // Reset player per-question states
            foreach (var p in _players.Values)
            {
                p.LastAnswerIndex = -1;
                p.HasAnsweredCurrentQuestion = false;
                p.LastPointsEarned = 0;
                p.VisibleOptionsAtSubmission = 4;
                p.RemainingSecondsAtSubmission = 0;
            }

            EliminatedAnswerIndices.Clear();
            _pendingWrongIndices.Clear();
            var wrongIndices = Enumerable.Range(0, q.Options.Count).Where(i => i != q.CorrectAnswerIndex).ToList();
            for (int i = wrongIndices.Count - 1; i > 0; i--)
            {
                int k = Random.Shared.Next(i + 1);
                (wrongIndices[i], wrongIndices[k]) = (wrongIndices[k], wrongIndices[i]);
            }
            _pendingWrongIndices.AddRange(wrongIndices);

            TotalCountdownSeconds = Settings.DefaultQuestionSeconds > 0 ? Settings.DefaultQuestionSeconds : (q.TimeLimitSeconds > 0 ? q.TimeLimitSeconds : 15);
            RemainingSeconds = TotalCountdownSeconds;

            SetState(TriviaGameState.QuestionActive);
            QuestionStarted?.Invoke(this, q);

            _tickTimer.Stop();
            if (startTimerImmediately)
            {
                _tickTimer.Start();
            }
        }
    }

    public bool SubmitAnswer(string playerName, int optionIndex, double responseTimeMs = 0)
    {
        lock (_stateLock)
        {
            // Answers are open for the full question countdown AND the post-countdown fade
            // (EliminatingAnswers) - a late answer submitted once options have started fading
            // is scored against however many options are still visible at that moment, via
            // ScoreAnswer/GetTierPercent below. This is what makes the 70%/40% tiers reachable
            // at all, since fading no longer happens until the game master's full question time
            // has elapsed.
            if (IsPaused || (State != TriviaGameState.QuestionActive && State != TriviaGameState.EliminatingAnswers))
            {
                return false;
            }

            var q = CurrentSession.CurrentQuestion;
            if (q == null) return false;

            if (!_players.TryGetValue(playerName, out var player))
            {
                player = RegisterPlayer(playerName);
            }

            if (player.HasAnsweredCurrentQuestion)
            {
                return false; // Already submitted
            }

            player.LastAnswerIndex = optionIndex;
            player.LastResponseTimeMs = responseTimeMs;
            player.HasAnsweredCurrentQuestion = true;
            player.VisibleOptionsAtSubmission = Math.Max(1, q.Options.Count - EliminatedAnswerIndices.Count);
            player.RemainingSecondsAtSubmission = RemainingSeconds;
            player.LastSeenAt = DateTime.Now;

            ScoreAnswer(player, q);

            // If everyone connected has now answered, skip straight ahead instead of waiting
            // out the remaining clock/fade.
            var activePlayers = _players.Values.Where(p => p.IsConnected).ToList();
            if (activePlayers.Count > 0 && activePlayers.All(p => p.HasAnsweredCurrentQuestion))
            {
                if (State == TriviaGameState.QuestionActive)
                {
                    StartAnswerElimination();
                }
                else
                {
                    CompleteRevealAnswer();
                }
            }

            return true;
        }
    }

    /// Scores one player's answer immediately at submission time, using however many options
    /// were visible for them right then (see VisibleOptionsAtSubmission/GetTierPercent). Called
    /// from SubmitAnswer only - unanswered players are scored (zeroed) later, once the answer
    /// window fully closes in CompleteRevealAnswer.
    private void ScoreAnswer(TriviaPlayer p, TriviaQuestion q)
    {
        double roundMultiplier = CurrentSession.CurrentRound?.PointMultiplier ?? 1.0;
        p.StreakBeforeAnswer = p.CurrentStreak;
        p.MaxStreakBeforeAnswer = p.MaxStreak;
        p.TotalAnswered++;

        if (p.LastAnswerIndex == q.CorrectAnswerIndex)
        {
            p.TotalCorrect++;
            p.CurrentStreak++;
            if (p.CurrentStreak > p.MaxStreak) p.MaxStreak = p.CurrentStreak;

            double tierMultiplier = Math.Clamp(GetTierPercent(p.VisibleOptionsAtSubmission) / 100.0, 0.0, 5.0);

            int basePoints = (int)(Settings.BasePointsPerQuestion * tierMultiplier);

            int speedBonus = 0;
            if (Settings.SpeedBonusEnabled && TotalCountdownSeconds > 0)
            {
                double remainingRatio = Math.Clamp((double)p.RemainingSecondsAtSubmission / TotalCountdownSeconds, 0.0, 1.0);
                speedBonus = (int)(Settings.MaxSpeedBonus * remainingRatio * tierMultiplier);
            }

            double streakMultiplier = 1.0 + Math.Min(p.CurrentStreak * Settings.StreakBonusMultiplier, 0.5);
            int points = (int)((basePoints + speedBonus) * roundMultiplier * streakMultiplier);
            p.LastPointsEarned = points;
            p.TotalScore += points;
        }
        else
        {
            p.CurrentStreak = 0;
            int deduction = Math.Max(0, Settings.WrongAnswerDeductionPoints);
            p.LastPointsEarned = -deduction;
            p.TotalScore -= deduction;
        }
    }

    public void LockAndRevealAnswer()
    {
        lock (_stateLock)
        {
            StartAnswerElimination();
        }
    }

    public void InstantRevealAnswer()
    {
        lock (_stateLock)
        {
            if (State != TriviaGameState.QuestionActive && State != TriviaGameState.EliminatingAnswers && State != TriviaGameState.AnsweringLocked)
            {
                return;
            }

            _tickTimer.Stop();
            var q = CurrentSession.CurrentQuestion;
            if (q == null) return;

            // Eliminate all wrong answers immediately
            EliminatedAnswerIndices.Clear();
            _pendingWrongIndices.Clear();
            for (int i = 0; i < q.Options.Count; i++)
            {
                if (i != q.CorrectAnswerIndex)
                {
                    EliminatedAnswerIndices.Add(i);
                }
            }
            AnswersEliminated?.Invoke(this, [.. EliminatedAnswerIndices]);
            CompleteRevealAnswer();
        }
    }

    public void EliminateNextWrongAnswer()
    {
        lock (_stateLock)
        {
            if (State != TriviaGameState.QuestionActive && State != TriviaGameState.EliminatingAnswers)
            {
                return;
            }

            var q = CurrentSession.CurrentQuestion;
            if (q == null) return;

            var uneliminatedWrong = _pendingWrongIndices
                .Where(i => !EliminatedAnswerIndices.Contains(i))
                .ToList();

            if (uneliminatedWrong.Count > 0)
            {
                EliminatedAnswerIndices.Add(uneliminatedWrong[0]);
                _pendingWrongIndices.Clear();
                _pendingWrongIndices.AddRange(uneliminatedWrong.Skip(1));

                SetState(TriviaGameState.EliminatingAnswers);
                AnswersEliminated?.Invoke(this, [.. EliminatedAnswerIndices]);

                if (_pendingWrongIndices.Count == 0)
                {
                    CompleteRevealAnswer();
                }
            }
            else
            {
                CompleteRevealAnswer();
            }
        }
    }

    public void StartAnswerElimination()
    {
        lock (_stateLock)
        {
            if (State != TriviaGameState.QuestionActive) return; // already locked/revealed - ignore re-entrant calls

            _tickTimer.Stop();
            SetState(TriviaGameState.AnsweringLocked);

            var q = CurrentSession.CurrentQuestion;
            if (q == null) return;

            // Scoring for players who already answered happened immediately in ScoreAnswer at
            // submission time. Players who haven't answered yet still can - the window stays
            // open through the fade below - so they're only finalized (zeroed) once the answer
            // window fully closes in CompleteRevealAnswer.

            // Identify wrong answer indices that have not yet been eliminated, preserving our shuffled order
            var uneliminatedWrong = _pendingWrongIndices
                .Where(i => !EliminatedAnswerIndices.Contains(i))
                .ToList();

            if (uneliminatedWrong.Count > 0)
            {
                // Eliminate the next wrong answer immediately and enter EliminatingAnswers state
                EliminatedAnswerIndices.Add(uneliminatedWrong[0]);
                _pendingWrongIndices.Clear();
                _pendingWrongIndices.AddRange(uneliminatedWrong.Skip(1));

                _eliminationCountdownSeconds = Settings.AnswerEliminationIntervalSeconds > 0 ? Settings.AnswerEliminationIntervalSeconds : 5;
                SetState(TriviaGameState.EliminatingAnswers);
                AnswersEliminated?.Invoke(this, [.. EliminatedAnswerIndices]);

                _tickTimer.Start();
            }
            else
            {
                CompleteRevealAnswer();
            }
        }
    }

    private void CompleteRevealAnswer()
    {
        var q = CurrentSession.CurrentQuestion;
        if (q == null) return;

        // The answer window is fully closed now - anyone who never answered forfeits their
        // streak and scores nothing for this question.
        foreach (var p in _players.Values)
        {
            if (!p.HasAnsweredCurrentQuestion)
            {
                p.CurrentStreak = 0;
                p.LastPointsEarned = 0;
            }
        }

        SetState(TriviaGameState.RevealAnswer);
        _postRevealCountdownSeconds = Settings.PostRevealDelaySeconds > 0 ? Settings.PostRevealDelaySeconds : 5;

        AnswerRevealed?.Invoke(this, q);
        LeaderboardUpdated?.Invoke(this, GetPlayers());

        _tickTimer.Start();
    }

    public void ShowLeaderboard()
    {
        lock (_stateLock)
        {
            _tickTimer.Stop();
            _leaderboardCountdownSeconds = 8;
            SetState(TriviaGameState.RoundLeaderboard);
            LeaderboardUpdated?.Invoke(this, GetPlayers());
            _tickTimer.Start();
        }
    }

    public bool AdvanceToNextQuestion(bool startTimerImmediately = true)
    {
        lock (_stateLock)
        {
            var round = CurrentSession.CurrentRound;
            if (round == null) return false;

            if (CurrentSession.CurrentQuestionIndex + 1 < round.Questions.Count)
            {
                CurrentSession.CurrentQuestionIndex++;
                PrepareCurrentQuestion(startTimerImmediately);
                return true;
            }

            // End of round
            if (CurrentSession.CurrentRoundIndex + 1 < CurrentSession.Rounds.Count)
            {
                CurrentSession.CurrentRoundIndex++;
                CurrentSession.CurrentQuestionIndex = 0;
                PrepareCurrentQuestion(startTimerImmediately);
                return true;
            }

            // End of game!
            CompleteGame();
            return false;
        }
    }

    public bool PreviousQuestion(bool startTimerImmediately = true)
    {
        lock (_stateLock)
        {
            var round = CurrentSession.CurrentRound;
            if (round == null) return false;

            if (CurrentSession.CurrentQuestionIndex > 0)
            {
                CurrentSession.CurrentQuestionIndex--;
                PrepareCurrentQuestion(startTimerImmediately);
                return true;
            }

            // Previous round if applicable
            if (CurrentSession.CurrentRoundIndex > 0)
            {
                CurrentSession.CurrentRoundIndex--;
                var prevRound = CurrentSession.Rounds[CurrentSession.CurrentRoundIndex];
                CurrentSession.CurrentQuestionIndex = Math.Max(0, prevRound.Questions.Count - 1);
                PrepareCurrentQuestion(startTimerImmediately);
                return true;
            }

            return false;
        }
    }

    public bool GoToQuestion(int questionIndex, int roundIndex = -1, bool startTimerImmediately = true)
    {
        lock (_stateLock)
        {
            if (roundIndex >= 0 && roundIndex < CurrentSession.Rounds.Count)
            {
                CurrentSession.CurrentRoundIndex = roundIndex;
            }

            var round = CurrentSession.CurrentRound;
            if (round == null) return false;

            if (questionIndex >= 0 && questionIndex < round.Questions.Count)
            {
                CurrentSession.CurrentQuestionIndex = questionIndex;
                PrepareCurrentQuestion(startTimerImmediately);
                return true;
            }

            return false;
        }
    }

    public void VoidCurrentQuestion()
    {
        lock (_stateLock)
        {
            var q = CurrentSession.CurrentQuestion;
            if (q == null) return;

            _tickTimer.Stop();

            // Reverse score adjustments and stats for players who answered this question
            foreach (var p in _players.Values)
            {
                if (p.HasAnsweredCurrentQuestion)
                {
                    p.TotalScore -= p.LastPointsEarned;
                    p.TotalAnswered = Math.Max(0, p.TotalAnswered - 1);
                    if (p.LastPointsEarned > 0)
                    {
                        p.TotalCorrect = Math.Max(0, p.TotalCorrect - 1);
                    }
                    p.CurrentStreak = p.StreakBeforeAnswer;
                    p.MaxStreak = p.MaxStreakBeforeAnswer;
                    p.LastPointsEarned = 0;
                    p.LastAnswerIndex = -1;
                    p.HasAnsweredCurrentQuestion = false;
                }
            }

            LeaderboardUpdated?.Invoke(this, GetPlayers());
            AdvanceToNextQuestion(Settings.AutoAdvanceQuestions);
        }
    }

    public void AdjustRemainingSeconds(int deltaSeconds)
    {
        lock (_stateLock)
        {
            if (State != TriviaGameState.QuestionActive && State != TriviaGameState.EliminatingAnswers)
            {
                return;
            }

            int newSeconds = RemainingSeconds + deltaSeconds;
            if (newSeconds <= 0)
            {
                RemainingSeconds = 0;
                TimerTick?.Invoke(this, 0);
                StartAnswerElimination();
                return;
            }

            RemainingSeconds = newSeconds;
            if (RemainingSeconds > TotalCountdownSeconds)
            {
                TotalCountdownSeconds = RemainingSeconds;
            }

            TimerTick?.Invoke(this, RemainingSeconds);
        }
    }

    public void ResetQuestionTimer()
    {
        lock (_stateLock)
        {
            if (State != TriviaGameState.QuestionActive && State != TriviaGameState.EliminatingAnswers)
            {
                return;
            }

            var q = CurrentSession.CurrentQuestion;
            TotalCountdownSeconds = Settings.DefaultQuestionSeconds > 0 ? Settings.DefaultQuestionSeconds : (q?.TimeLimitSeconds > 0 ? q.TimeLimitSeconds : 15);
            RemainingSeconds = TotalCountdownSeconds;
            EliminatedAnswerIndices.Clear();

            if (q != null)
            {
                var wrongIndices = Enumerable.Range(0, q.Options.Count).Where(i => i != q.CorrectAnswerIndex).ToList();
                for (int i = wrongIndices.Count - 1; i > 0; i--)
                {
                    int k = Random.Shared.Next(i + 1);
                    (wrongIndices[i], wrongIndices[k]) = (wrongIndices[k], wrongIndices[i]);
                }
                _pendingWrongIndices.Clear();
                _pendingWrongIndices.AddRange(wrongIndices);
            }

            SetState(TriviaGameState.QuestionActive);
            TimerTick?.Invoke(this, RemainingSeconds);
            AnswersEliminated?.Invoke(this, []);
            _tickTimer.Start();
        }
    }

    /// True once Settings.TotalGamesToPlay (if set) has been reached - CompleteGame stops
    /// auto-restarting at that point instead of looping forever.
    public bool HasReachedGamesCap => Settings.TotalGamesToPlay > 0 && GamesPlayedCount >= Settings.TotalGamesToPlay;

    private void CompleteGame()
    {
        _tickTimer.Stop();
        SetState(TriviaGameState.GameComplete);

        var result = GetGameResult();
        GameCompleted?.Invoke(this, result);

        if (Settings.AutoStartNextGameEnabled && Settings.NextGameDelayMinutes > 0 && !HasReachedGamesCap)
        {
            IntermissionSecondsRemaining = Settings.NextGameDelayMinutes * 60;
            IntermissionTick?.Invoke(this, IntermissionSecondsRemaining);
            _tickTimer.Start();
        }
        else
        {
            IntermissionSecondsRemaining = 0;
        }
    }

    public void SkipIntermission()
    {
        lock (_stateLock)
        {
            _tickTimer.Stop();
            IntermissionSecondsRemaining = 0;
            IntermissionCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    public TriviaGameResult GetGameResult()
    {
        var ranked = GetPlayers();
        var result = new TriviaGameResult
        {
            RankedPlayers = ranked
        };

        // Group players by TeamName (excluding individual / blank team names)
        var teamGroups = ranked
            .Where(p => !string.IsNullOrWhiteSpace(p.TeamName) && !p.TeamName.Equals(p.Name, StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.TeamName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TriviaTeamSummary
            {
                TeamName = g.Key,
                TotalScore = g.Sum(p => p.TotalScore),
                Players = g.Select(p => p.Name).ToList()
            })
            .OrderByDescending(t => t.TotalScore)
            .ToList();

        result.Teams = teamGroups;

        if (teamGroups.Count > 0)
        {
            var topTeam = teamGroups[0];
            result.HasTeamWinner = true;
            result.WinnerTitle = $"🏆 WINNING TEAM: {topTeam.TeamName}";
            result.WinningName = topTeam.TeamName;
            result.WinningScore = topTeam.TotalScore;
            result.WinningTeamMembers = topTeam.Players;
        }
        else if (ranked.Count > 0)
        {
            var topPlayer = ranked[0];
            result.HasTeamWinner = false;
            result.WinnerTitle = $"👑 CHAMPION: {topPlayer.Name}";
            result.WinningName = topPlayer.Name;
            result.WinningScore = topPlayer.TotalScore;
            result.WinningTeamMembers = [topPlayer.Name];
        }
        else
        {
            result.WinnerTitle = "🏆 GAME COMPLETE";
            result.WinningName = "No players";
            result.WinningScore = 0;
        }

        return result;
    }

    public void PauseTimer()
    {
        _tickTimer.Stop();
    }

    public void ResumeTimer()
    {
        if ((State == TriviaGameState.QuestionActive || State == TriviaGameState.EliminatingAnswers || State == TriviaGameState.RevealAnswer || State == TriviaGameState.RoundLeaderboard || State == TriviaGameState.GameComplete))
        {
            _tickTimer.Start();
        }
    }

    private void OnTimerTick(object? sender, ElapsedEventArgs e)
    {
        lock (_stateLock)
        {
            switch (State)
            {
                case TriviaGameState.QuestionActive:
                    // All 4 options stay visible for the full question time - no fading until
                    // the game master's configured time is fully up. Progressive elimination
                    // (and the tiered 70%/40% scoring that comes with it) only starts once we
                    // move into EliminatingAnswers below.
                    RemainingSeconds--;
                    TimerTick?.Invoke(this, RemainingSeconds);

                    if (RemainingSeconds <= 0)
                    {
                        StartAnswerElimination();
                    }
                    break;

                case TriviaGameState.EliminatingAnswers:
                    _eliminationCountdownSeconds--;
                    TimerTick?.Invoke(this, _eliminationCountdownSeconds);

                    if (_eliminationCountdownSeconds <= 0)
                    {
                        if (_pendingWrongIndices.Count > 0)
                        {
                            EliminatedAnswerIndices.Add(_pendingWrongIndices[0]);
                            _pendingWrongIndices.RemoveAt(0);
                            AnswersEliminated?.Invoke(this, [.. EliminatedAnswerIndices]);

                            if (_pendingWrongIndices.Count > 0)
                            {
                                _eliminationCountdownSeconds = Settings.AnswerEliminationIntervalSeconds > 0 ? Settings.AnswerEliminationIntervalSeconds : 5;
                            }
                            else
                            {
                                CompleteRevealAnswer();
                            }
                        }
                        else
                        {
                            CompleteRevealAnswer();
                        }
                    }
                    break;

                case TriviaGameState.RevealAnswer:
                    if (Settings.AutoAdvanceQuestions)
                    {
                        _postRevealCountdownSeconds--;
                        if (_postRevealCountdownSeconds <= 0)
                        {
                            AdvanceToNextQuestion();
                        }
                    }
                    break;

                case TriviaGameState.RoundLeaderboard:
                    if (Settings.AutoAdvanceQuestions)
                    {
                        _leaderboardCountdownSeconds--;
                        if (_leaderboardCountdownSeconds <= 0)
                        {
                            if (CurrentSession.CurrentQuestion != null)
                            {
                                StartCurrentQuestion();
                            }
                            else
                            {
                                AdvanceToNextQuestion();
                            }
                        }
                    }
                    break;

                case TriviaGameState.GameComplete:
                    if (IntermissionSecondsRemaining > 0)
                    {
                        IntermissionSecondsRemaining--;
                        IntermissionTick?.Invoke(this, IntermissionSecondsRemaining);

                        if (IntermissionSecondsRemaining <= 0)
                        {
                            _tickTimer.Stop();
                            IntermissionCompleted?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    break;
            }
        }
    }

    public void PauseGame(string? reason = null)
    {
        lock (_stateLock)
        {
            if (IsPaused) return;
            IsPaused = true;
            PauseReason = reason ?? "Paused by Host";
            PauseTimer();
            GamePaused?.Invoke(this, PauseReason);
        }
    }

    public void ResumeGame()
    {
        lock (_stateLock)
        {
            if (!IsPaused) return;
            IsPaused = false;
            PauseReason = null;
            ResumeTimer();
            GameResumed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void TogglePause(string? reason = null)
    {
        if (IsPaused) ResumeGame();
        else PauseGame(reason);
    }

    /// Percent of base points awarded for a correct answer submitted while
    /// <paramref name="visibleOptionsCount"/> options are still visible (100/70/40 tiers).
    /// Single source of truth for the tier lookup - used for actual scoring here and by
    /// TriviaWebServer for the live "potential points" badge shown to players.
    public int GetTierPercent(int visibleOptionsCount)
    {
        if (!Settings.TieredScoringEnabled) return 100;
        if (visibleOptionsCount >= 4) return Settings.Points4OptionsPercent;
        if (visibleOptionsCount == 3) return Settings.Points3OptionsPercent;
        return Settings.Points2OptionsPercent;
    }

    public Dictionary<int, int> GetAnswerDistribution()
    {
        lock (_stateLock)
        {
            var dist = new Dictionary<int, int> { [0] = 0, [1] = 0, [2] = 0, [3] = 0 };
            foreach (var p in _players.Values)
            {
                if (p.HasAnsweredCurrentQuestion && p.LastAnswerIndex >= 0 && p.LastAnswerIndex <= 3)
                {
                    dist[p.LastAnswerIndex]++;
                }
            }
            return dist;
        }
    }

    private void SetState(TriviaGameState newState)
    {
        CurrentSession.State = newState;
        StateChanged?.Invoke(this, newState);
    }

    public void Dispose()
    {
        _tickTimer.Stop();
        _tickTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
