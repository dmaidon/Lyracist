// Edited on Aug 19, 2026 @ 11:15:30 -> Respect GameMaster DefaultQuestionSeconds over question JSON default
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

    public List<TriviaPlayer> GetPlayers()
    {
        return _players.Values.OrderByDescending(p => p.TotalScore).ToList();
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
            }

            EliminatedAnswerIndices.Clear();
            _pendingWrongIndices.Clear();

            SetState(TriviaGameState.Lobby);
        }
    }

    public void StartCurrentQuestion()
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
            }

            EliminatedAnswerIndices.Clear();
            _pendingWrongIndices.Clear();

            TotalCountdownSeconds = Settings.DefaultQuestionSeconds > 0 ? Settings.DefaultQuestionSeconds : (q.TimeLimitSeconds > 0 ? q.TimeLimitSeconds : 15);
            RemainingSeconds = TotalCountdownSeconds;

            SetState(TriviaGameState.QuestionActive);
            QuestionStarted?.Invoke(this, q);

            _tickTimer.Stop();
            _tickTimer.Start();
        }
    }

    public bool SubmitAnswer(string playerName, int optionIndex, double responseTimeMs = 0)
    {
        lock (_stateLock)
        {
            if (IsPaused || State != TriviaGameState.QuestionActive)
            {
                return false; // Answers locked while paused or inactive
            }

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
            player.LastSeenAt = DateTime.Now;

            // Check if all connected players have answered
            var activePlayers = _players.Values.Where(p => p.IsConnected).ToList();
            if (activePlayers.Count > 0 && activePlayers.All(p => p.HasAnsweredCurrentQuestion))
            {
                // All players answered early! Lock and begin elimination
                StartAnswerElimination();
            }

            return true;
        }
    }

    public void LockAndRevealAnswer()
    {
        lock (_stateLock)
        {
            StartAnswerElimination();
        }
    }

    public void StartAnswerElimination()
    {
        lock (_stateLock)
        {
            _tickTimer.Stop();
            SetState(TriviaGameState.AnsweringLocked);

            var q = CurrentSession.CurrentQuestion;
            if (q == null) return;

            // Calculate scores right away so they are locked in
            double multiplier = CurrentSession.CurrentRound?.PointMultiplier ?? 1.0;
            foreach (var p in _players.Values)
            {
                if (p.HasAnsweredCurrentQuestion)
                {
                    p.TotalAnswered++;
                    if (p.LastAnswerIndex == q.CorrectAnswerIndex)
                    {
                        p.TotalCorrect++;
                        p.CurrentStreak++;
                        if (p.CurrentStreak > p.MaxStreak) p.MaxStreak = p.CurrentStreak;

                        int speedBonus = 0;
                        if (Settings.SpeedBonusEnabled && TotalCountdownSeconds > 0)
                        {
                            double remainingRatio = Math.Clamp((double)RemainingSeconds / TotalCountdownSeconds, 0.0, 1.0);
                            speedBonus = (int)(Settings.MaxSpeedBonus * remainingRatio);
                        }

                        double streakMultiplier = 1.0 + Math.Min(p.CurrentStreak * Settings.StreakBonusMultiplier, 0.5);
                        int points = (int)((Settings.BasePointsPerQuestion + speedBonus) * multiplier * streakMultiplier);
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
                else
                {
                    p.CurrentStreak = 0;
                    p.LastPointsEarned = 0;
                }
            }

            // Identify wrong answer indices
            var wrongIndices = Enumerable.Range(0, q.Options.Count).Where(i => i != q.CorrectAnswerIndex).ToList();
            if (wrongIndices.Count > 0)
            {
                // Eliminate the first wrong answer immediately
                EliminatedAnswerIndices.Clear();
                EliminatedAnswerIndices.Add(wrongIndices[0]);

                _pendingWrongIndices.Clear();
                _pendingWrongIndices.AddRange(wrongIndices.Skip(1));

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

    public bool AdvanceToNextQuestion()
    {
        lock (_stateLock)
        {
            var round = CurrentSession.CurrentRound;
            if (round == null) return false;

            if (CurrentSession.CurrentQuestionIndex + 1 < round.Questions.Count)
            {
                CurrentSession.CurrentQuestionIndex++;
                StartCurrentQuestion();
                return true;
            }
            else if (CurrentSession.CurrentRoundIndex + 1 < CurrentSession.Rounds.Count)
            {
                CurrentSession.CurrentRoundIndex++;
                CurrentSession.CurrentQuestionIndex = 0;
                ShowLeaderboard();
                return true;
            }
            else
            {
                // Game Complete
                SetState(TriviaGameState.GameComplete);
                var result = GetGameResult();
                LeaderboardUpdated?.Invoke(this, GetPlayers());
                GameCompleted?.Invoke(this, result);

                if (Settings.AutoStartNextGameEnabled && Settings.NextGameDelayMinutes > 0)
                {
                    IntermissionSecondsRemaining = Settings.NextGameDelayMinutes * 60;
                    _tickTimer.Start();
                    IntermissionTick?.Invoke(this, IntermissionSecondsRemaining);
                }
                else
                {
                    IntermissionSecondsRemaining = 0;
                    _tickTimer.Stop();
                }
                return false;
            }
        }
    }

    public TriviaGameResult GetGameResult()
    {
        var rankedPlayers = GetPlayers();
        var result = new TriviaGameResult
        {
            RankedPlayers = rankedPlayers
        };

        // Group players who provided a team name
        var teamGroups = rankedPlayers
            .Where(p => !string.IsNullOrWhiteSpace(p.TeamName))
            .GroupBy(p => p.TeamName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new TriviaTeamSummary
            {
                TeamName = g.Key,
                TotalScore = g.Sum(p => p.TotalScore),
                Players = g.Select(p => p.Name).Distinct().ToList()
            })
            .OrderByDescending(t => t.TotalScore)
            .ToList();

        result.Teams = teamGroups;

        if (teamGroups.Count > 0)
        {
            var winningTeam = teamGroups[0];
            result.HasTeamWinner = true;
            result.WinningName = winningTeam.TeamName;
            result.WinningScore = winningTeam.TotalScore;
            result.WinningTeamMembers = winningTeam.Players;
            result.WinnerTitle = $"🏆 TEAM CHAMPIONS: {winningTeam.TeamName}";
        }
        else if (rankedPlayers.Count > 0)
        {
            var winningPlayer = rankedPlayers[0];
            result.HasTeamWinner = false;
            result.WinningName = winningPlayer.Name;
            result.WinningScore = winningPlayer.TotalScore;
            result.WinningTeamMembers = [winningPlayer.Name];
            result.WinnerTitle = $"🏆 TRIVIA CHAMPION: {winningPlayer.Name}";
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
        if ((State == TriviaGameState.QuestionActive || State == TriviaGameState.EliminatingAnswers || State == TriviaGameState.RevealAnswer || State == TriviaGameState.RoundLeaderboard))
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
                                // All wrong answers have faded out! Only correct answer remains!
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

    public void StartIntermissionCountdown(int seconds)
    {
        lock (_stateLock)
        {
            IntermissionSecondsRemaining = seconds;
            if (IntermissionSecondsRemaining > 0)
            {
                _tickTimer.Start();
                IntermissionTick?.Invoke(this, IntermissionSecondsRemaining);
            }
        }
    }

    public void SkipIntermission()
    {
        lock (_stateLock)
        {
            IntermissionSecondsRemaining = 0;
            _tickTimer.Stop();
            IntermissionCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetState(TriviaGameState newState)
    {
        CurrentSession.State = newState;
        StateChanged?.Invoke(this, newState);
    }

    public Dictionary<int, int> GetAnswerDistribution()
    {
        var dist = new Dictionary<int, int> { [0] = 0, [1] = 0, [2] = 0, [3] = 0 };
        foreach (var p in _players.Values)
        {
            if (p.HasAnsweredCurrentQuestion && p.LastAnswerIndex >= 0 && p.LastAnswerIndex < 4)
            {
                dist[p.LastAnswerIndex]++;
            }
        }
        return dist;
    }

    public void PauseGame(string? reason = null)
    {
        lock (_stateLock)
        {
            if (IsPaused) return;
            IsPaused = true;
            PauseReason = string.IsNullOrWhiteSpace(reason) ? "Event In Progress" : reason;
            _tickTimer.Stop();
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
            if (State == TriviaGameState.QuestionActive || State == TriviaGameState.EliminatingAnswers || State == TriviaGameState.RevealAnswer || State == TriviaGameState.RoundLeaderboard || (State == TriviaGameState.GameComplete && IntermissionSecondsRemaining > 0))
            {
                _tickTimer.Start();
            }
            GameResumed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void TogglePause(string? reason = null)
    {
        if (IsPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame(reason);
        }
    }

    public void Dispose()
    {
        _tickTimer.Stop();
        _tickTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
