// Edited on Oct 31, 2026 @ 10:38:00 -> Defer SuperStreak until batch evaluation finishes, guard RecordPlayerAnswer, add speed bonus/tie-break, category filter, RevivePlayer, and sound service hooks
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using KnockoutTrivia.Models;
using Lyracist.Shared;

namespace KnockoutTrivia.Services;

public enum GameStatePhase
{
    Lobby = 0,
    QuestionActive = 1,
    AnswerRevealed = 2,
    SuperStreak = 3,
    GameOver = 4
}

public interface IGameStateService
{
    ObservableCollection<KnockoutPlayer> Players { get; }
    List<KnockoutPlayer> GetPlayersSnapshot();
    KnockoutQuestion? CurrentQuestion { get; }
    int CurrentQuestionIndex { get; }
    int CurrentQuestionNumber { get; }
    int TotalQuestions { get; }
    bool IsAnswerRevealed { get; }
    bool IsGameActive { get; }
    GameStatePhase Phase { get; }
    int SecondsRemaining { get; }
    int TotalCountdownSeconds { get; }
    bool IsTimerRunning { get; }
    int AnsweredCount { get; }
    int ActivePlayerCount { get; }
    KnockoutSettings Settings { get; }
    IReadOnlyList<string> AvailableCategories { get; }

    event EventHandler<KnockoutQuestion?>? QuestionChanged;
    event EventHandler<GameStatePhase>? PhaseChanged;
    event EventHandler<int>? TimerTicked;
    event EventHandler<KnockoutPlayer>? SuperStreakTriggered;
    event EventHandler<KnockoutPlayer>? PlayerEliminated;
    event EventHandler<KnockoutPlayer?>? GameWon;
    event EventHandler? AnswersEvaluated;

    void InitializeGame(KnockoutSettings settings);
    void UpdateSettings(KnockoutSettings settings);
    Task LoadGameQuestionsAsync(string? packOrDbPath = null);
    Task LoadQuestionsFromSourcesAsync(IEnumerable<string> sourcePaths);
    void ShuffleQuestions();
    KnockoutPlayer RegisterOrGetPlayer(string name, string? playerId = null);
    void AddPlayer(string name);
    void RemovePlayer(string playerId);
    bool SubmitPlayerAnswer(string playerId, int answerIndex, int responseTimeMs, string? sessionToken = null);
    void ClearAllPlayers();
    void StartGame();
    void StartQuestionTimer(int? customSeconds = null);
    void PauseTimer();
    void ResumeTimer();
    void StopTimer();
    void NextQuestion();
    void RevealAnswer();
    void EvaluateSubmittedAnswers();
    void RecordPlayerAnswer(string playerId, bool isCorrect);
    void AddPlayerStrike(KnockoutPlayer player);
    void ResetPlayerStrikes(KnockoutPlayer player);
    void RevivePlayer(KnockoutPlayer player);
    void ResumeFromSuperStreak();
    void ResetGame();
}

public class GameStateService : ObservableObject, IGameStateService, IDisposable
{
    private readonly ITriviaDataService _triviaDataService;
    private readonly ITokenService _tokenService;
    private readonly IStreakService _streakService;
    private readonly ISoundService? _soundService;

    private List<KnockoutQuestion> _allLoadedQuestions = [];
    private List<KnockoutQuestion> _questions = [];
    private List<string> _availableCategories = ["All Categories"];
    private int _currentQuestionIndex;
    private KnockoutQuestion? _currentQuestion;
    private bool _isAnswerRevealed;
    private bool _isGameActive;
    private GameStatePhase _phase = GameStatePhase.Lobby;
    private int _secondsRemaining = 15;
    private int _totalCountdownSeconds = 15;
    private bool _isTimerRunning;

    private bool _isEvaluatingBatch;
    private readonly Queue<KnockoutPlayer> _pendingSuperStreakPlayers = new();

    private System.Threading.Timer? _countdownTimer;
    private System.Threading.Timer? _autoAdvanceTimer;
    private bool _disposed;

    private readonly object _snapshotLock = new();
    private List<KnockoutPlayer> _cachedSnapshot = [];
    private static readonly TimeSpan PlayerDisconnectTimeout = TimeSpan.FromSeconds(20);
    public const int MaxPlayerNameLength = 24;

    public ObservableCollection<KnockoutPlayer> Players { get; } = [];
    public IReadOnlyList<string> AvailableCategories => _availableCategories;

    // Thread-safe snapshot cached and updated on player changes so polling socket threads never
    // cause collection modification errors or block the UI thread on high-frequency polls.
    public List<KnockoutPlayer> GetPlayersSnapshot()
    {
        var cutoff = DateTime.Now - PlayerDisconnectTimeout;
        List<KnockoutPlayer> snapshot;
        lock (_snapshotLock)
        {
            snapshot = _cachedSnapshot;
        }

        foreach (var p in snapshot)
        {
            if (p.IsConnected && p.LastSeenAt < cutoff)
            {
                var target = p;
                RunOnUIAsync(() =>
                {
                    if (target.IsConnected && target.LastSeenAt < cutoff)
                    {
                        target.IsConnected = false;
                        NotifyPlayerCountsChanged();
                    }
                });
            }
        }
        return snapshot;
    }

    private void UpdateSnapshotInternal()
    {
        lock (_snapshotLock)
        {
            _cachedSnapshot = [.. Players];
        }
        NotifyPlayerCountsChanged();
    }

    private void NotifyPlayerCountsChanged()
    {
        OnPropertyChanged(nameof(AnsweredCount));
        OnPropertyChanged(nameof(ActivePlayerCount));
    }

    public KnockoutQuestion? CurrentQuestion
    {
        get => _currentQuestion;
        private set => SetProperty(ref _currentQuestion, value);
    }

    public int CurrentQuestionIndex
    {
        get => _currentQuestionIndex;
        private set
        {
            if (SetProperty(ref _currentQuestionIndex, value))
            {
                OnPropertyChanged(nameof(CurrentQuestionNumber));
            }
        }
    }

    public int CurrentQuestionNumber => _currentQuestionIndex + 1;

    public int TotalQuestions => Settings.UnlimitedQuestions
        ? _questions.Count
        : Math.Min(_questions.Count, Settings.NumberOfQuestionsPerGame > 0 ? Settings.NumberOfQuestionsPerGame : _questions.Count);

    public bool IsAnswerRevealed
    {
        get => _isAnswerRevealed;
        private set => SetProperty(ref _isAnswerRevealed, value);
    }

    public bool IsGameActive
    {
        get => _isGameActive;
        private set => SetProperty(ref _isGameActive, value);
    }

    public GameStatePhase Phase
    {
        get => _phase;
        private set
        {
            if (SetProperty(ref _phase, value))
            {
                PhaseChanged?.Invoke(this, value);
            }
        }
    }

    public int SecondsRemaining
    {
        get => _secondsRemaining;
        private set => SetProperty(ref _secondsRemaining, value);
    }

    public int TotalCountdownSeconds
    {
        get => _totalCountdownSeconds;
        private set => SetProperty(ref _totalCountdownSeconds, value);
    }

    public bool IsTimerRunning
    {
        get => _isTimerRunning;
        private set => SetProperty(ref _isTimerRunning, value);
    }

    public int AnsweredCount => Players.Count(p => !p.IsEliminated && p.IsConnected && p.HasAnsweredCurrentQuestion);
    public int ActivePlayerCount => Players.Count(p => !p.IsEliminated && p.IsConnected);

    public KnockoutSettings Settings { get; private set; } = new();

    public event EventHandler<KnockoutQuestion?>? QuestionChanged;
    public event EventHandler<GameStatePhase>? PhaseChanged;
    public event EventHandler<int>? TimerTicked;
    public event EventHandler<KnockoutPlayer>? SuperStreakTriggered;
    public event EventHandler<KnockoutPlayer>? PlayerEliminated;
    public event EventHandler<KnockoutPlayer?>? GameWon;
    public event EventHandler? AnswersEvaluated;

    public GameStateService(
        ITriviaDataService triviaDataService,
        ITokenService tokenService,
        IStreakService streakService,
        ISoundService? soundService = null)
    {
        _triviaDataService = triviaDataService;
        _tokenService = tokenService;
        _streakService = streakService;
        _soundService = soundService;

        Players.CollectionChanged += (_, _) => UpdateSnapshotInternal();

        _streakService.SuperStreakReached += (s, p) =>
        {
            if (_isEvaluatingBatch)
            {
                if (!_pendingSuperStreakPlayers.Contains(p))
                {
                    _pendingSuperStreakPlayers.Enqueue(p);
                }
                return;
            }

            TriggerSuperStreakForPlayer(p);
        };
    }

    private void TriggerSuperStreakForPlayer(KnockoutPlayer player)
    {
        StopTimer();
        _autoAdvanceTimer?.Dispose();
        _autoAdvanceTimer = null;
        Phase = GameStatePhase.SuperStreak;
        _soundService?.PlaySuperStreakFanfare();
        SuperStreakTriggered?.Invoke(this, player);
    }

    public void InitializeGame(KnockoutSettings settings)
    {
        Settings = settings;
        if (_soundService != null)
        {
            _soundService.IsEnabled = settings.EnableSoundEffects;
        }
        ResetGame();
    }

    public void UpdateSettings(KnockoutSettings settings)
    {
        string previousCategory = Settings.SelectedCategory;
        Settings = settings;
        if (_soundService != null)
        {
            _soundService.IsEnabled = settings.EnableSoundEffects;
        }

        if (!string.Equals(previousCategory, settings.SelectedCategory, StringComparison.OrdinalIgnoreCase) && _allLoadedQuestions.Count > 0)
        {
            ApplyCategoryFilterAndResetDeck();
        }

        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(TotalQuestions));
    }

    public async Task LoadGameQuestionsAsync(string? packOrDbPath = null)
    {
        if (packOrDbPath != null && packOrDbPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            _allLoadedQuestions = await _triviaDataService.LoadQuestionsFromPackAsync(packOrDbPath);
        }
        else
        {
            _allLoadedQuestions = await _triviaDataService.LoadQuestionsFromDatabaseAsync(packOrDbPath);
        }

        RebuildCategoriesAndQuestions();
    }

    public async Task LoadQuestionsFromSourcesAsync(IEnumerable<string> sourcePaths)
    {
        _allLoadedQuestions = await _triviaDataService.LoadQuestionsFromMultipleSourcesAsync(sourcePaths);
        RebuildCategoriesAndQuestions();
    }

    private void RebuildCategoriesAndQuestions()
    {
        var cats = new List<string> { "All Categories" };
        cats.AddRange(_allLoadedQuestions
            .Select(q => q.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase));
        _availableCategories = cats;
        OnPropertyChanged(nameof(AvailableCategories));

        ApplyCategoryFilterAndResetDeck();
    }

    private void ApplyCategoryFilterAndResetDeck()
    {
        string selectedCat = Settings.SelectedCategory;
        if (!string.IsNullOrWhiteSpace(selectedCat) &&
            !string.Equals(selectedCat, "All Categories", StringComparison.OrdinalIgnoreCase))
        {
            var filtered = _allLoadedQuestions
                .Where(q => string.Equals(q.Category, selectedCat, StringComparison.OrdinalIgnoreCase))
                .ToList();
            _questions = filtered.Count > 0 ? filtered : [.. _allLoadedQuestions];
        }
        else
        {
            _questions = [.. _allLoadedQuestions];
        }

        ShuffleQuestions();

        if (_questions.Count > 0)
        {
            CurrentQuestionIndex = 0;
            CurrentQuestion = _questions[0];
            ResetQuestionPlayerStates();
            QuestionChanged?.Invoke(this, CurrentQuestion);
        }
        OnPropertyChanged(nameof(TotalQuestions));
    }

    private void ResetQuestionPlayerStates()
    {
        RunOnUI(() =>
        {
            foreach (var p in Players)
            {
                p.HasAnsweredCurrentQuestion = false;
                p.HasBeenEvaluatedForCurrentQuestion = false;
                p.LastAnswerIndex = -1;
                p.WasShieldProtected = false;
                p.LastPointsEarned = 0;
                p.ResponseTimeMs = 0;
            }
            NotifyPlayerCountsChanged();
        });
    }

    public void ShuffleQuestions()
    {
        if (_questions.Count > 1)
        {
            var rng = Random.Shared;
            for (int i = _questions.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_questions[i], _questions[j]) = (_questions[j], _questions[i]);
            }
        }
    }

    public KnockoutPlayer RegisterOrGetPlayer(string name, string? playerId = null)
    {
        string trimmed = name.Trim();
        if (trimmed.Length > MaxPlayerNameLength) trimmed = trimmed[..MaxPlayerNameLength];
        KnockoutPlayer? result = null;

        RunOnUI(() =>
        {
            KnockoutPlayer? existing = null;
            if (!string.IsNullOrEmpty(playerId))
            {
                existing = Players.FirstOrDefault(p => string.Equals(p.Id, playerId, StringComparison.OrdinalIgnoreCase));
            }

            // Only reuse disconnected player records by name to avoid phone takeovers during live shows
            if (existing == null)
            {
                existing = Players.FirstOrDefault(p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase) && !p.IsConnected);
            }

            if (existing != null)
            {
                existing.IsConnected = true;
                existing.LastSeenAt = DateTime.Now;
                result = existing;
                UpdateSnapshotInternal();
                return;
            }

            var newPlayer = new KnockoutPlayer
            {
                Name = trimmed,
                Tokens = 1,
                IsConnected = true,
                LastSeenAt = DateTime.Now
            };

            Players.Add(newPlayer);
            result = newPlayer;
            UpdateSnapshotInternal();
        });

        return result!;
    }

    public void AddPlayer(string name)
    {
        RegisterOrGetPlayer(name);
    }

    public void RemovePlayer(string playerId)
    {
        RunOnUI(() =>
        {
            var p = Players.FirstOrDefault(x => x.Id == playerId);
            if (p != null)
            {
                Players.Remove(p);
                UpdateSnapshotInternal();
            }
        });
    }

    public void ClearAllPlayers()
    {
        RunOnUI(() =>
        {
            Players.Clear();
            UpdateSnapshotInternal();
        });
    }

    public bool SubmitPlayerAnswer(string playerId, int answerIndex, int responseTimeMs, string? sessionToken = null)
    {
        bool accepted = false;
        RunOnUI(() =>
        {
            var player = Players.FirstOrDefault(p => string.Equals(p.Id, playerId, StringComparison.OrdinalIgnoreCase));
            if (player == null || player.IsEliminated || player.HasAnsweredCurrentQuestion) return;

            // Session token must match if player has one
            if (!string.IsNullOrEmpty(player.SessionToken) &&
                (string.IsNullOrEmpty(sessionToken) || !string.Equals(player.SessionToken, sessionToken, StringComparison.Ordinal)))
            {
                return;
            }

            if (!IsGameActive || Phase != GameStatePhase.QuestionActive || IsAnswerRevealed)
            {
                return;
            }

            // Validate answer index against current question options count
            if (answerIndex < -1 || answerIndex >= (CurrentQuestion?.Options.Count ?? 0))
            {
                return;
            }

            player.LastAnswerIndex = answerIndex;
            player.HasAnsweredCurrentQuestion = true;
            player.ResponseTimeMs = Math.Max(0, responseTimeMs);
            player.LastSeenAt = DateTime.Now;
            if (!player.IsConnected)
            {
                player.IsConnected = true;
            }
            accepted = true;

            NotifyPlayerCountsChanged();
            CheckAutoRevealThreshold();
        });

        return accepted;
    }

    private void CheckAutoRevealThreshold()
    {
        int active = ActivePlayerCount;
        if (active > 0 && AnsweredCount >= active && Settings.GameMode == GameAdvanceMode.Automatic)
        {
            int targetIndex = CurrentQuestionIndex;
            string? targetQuestionId = CurrentQuestion?.Id;

            // Give a short 1s grace before auto-revealing on UI thread
            Task.Delay(1000).ContinueWith(_ =>
            {
                try
                {
                    RunOnUI(() =>
                    {
                        if (Phase == GameStatePhase.QuestionActive &&
                            !IsAnswerRevealed &&
                            CurrentQuestionIndex == targetIndex &&
                            CurrentQuestion?.Id == targetQuestionId)
                        {
                            RevealAnswer();
                        }
                    });
                }
                catch (Exception ex)
                {
                    Globals.LogError("KnockoutTrivia", "GameStateService.AutoRevealGrace", ex);
                }
            });
        }
    }

    public void StartGame()
    {
        _pendingSuperStreakPlayers.Clear();
        IsGameActive = true;
        if (_questions.Count > 0)
        {
            CurrentQuestionIndex = 0;
            CurrentQuestion = _questions[0];
            StartQuestionTimer();
            QuestionChanged?.Invoke(this, CurrentQuestion);
            _soundService?.PlayAudioSnippet(CurrentQuestion.AudioSnippetPath);
        }
        else
        {
            Phase = GameStatePhase.QuestionActive;
        }
    }

    public void StartQuestionTimer(int? customSeconds = null)
    {
        StopTimer();

        int seconds = customSeconds ?? (Settings.QuestionTimerSeconds > 0 ? Settings.QuestionTimerSeconds : (CurrentQuestion?.TimeLimitSeconds ?? 15));
        if (seconds <= 0) seconds = 15;

        ResetQuestionPlayerStates();

        TotalCountdownSeconds = seconds;
        SecondsRemaining = seconds;
        IsTimerRunning = true;
        IsAnswerRevealed = false;
        Phase = GameStatePhase.QuestionActive;

        NotifyPlayerCountsChanged();

        _countdownTimer = new System.Threading.Timer(OnTimerTick, null, 1000, 1000);
    }

    private void OnTimerTick(object? state)
    {
        try
        {
            RunOnUI(() =>
            {
                if (!IsTimerRunning) return;

                SecondsRemaining--;
                TimerTicked?.Invoke(this, SecondsRemaining);

                if (SecondsRemaining > 0 && SecondsRemaining <= 5)
                {
                    _soundService?.PlayCountdownTick();
                }

                if (SecondsRemaining <= 0)
                {
                    StopTimer();
                    RevealAnswer();
                }
            });
        }
        catch (Exception ex)
        {
            Globals.LogError("KnockoutTrivia", "GameStateService.OnTimerTick", ex);
        }
    }

    public void PauseTimer()
    {
        IsTimerRunning = false;
    }

    public void ResumeTimer()
    {
        if (SecondsRemaining > 0 && Phase == GameStatePhase.QuestionActive)
        {
            IsTimerRunning = true;
        }
    }

    public void StopTimer()
    {
        IsTimerRunning = false;
        _countdownTimer?.Dispose();
        _countdownTimer = null;
    }

    public void NextQuestion()
    {
        _autoAdvanceTimer?.Dispose();
        _autoAdvanceTimer = null;
        _pendingSuperStreakPlayers.Clear();
        _soundService?.StopAudioSnippet();

        if (_questions.Count == 0) return;

        IsAnswerRevealed = false;
        CurrentQuestionIndex++;

        int maxQuestions = Settings.UnlimitedQuestions
            ? _questions.Count
            : Math.Min(_questions.Count, Settings.NumberOfQuestionsPerGame > 0 ? Settings.NumberOfQuestionsPerGame : _questions.Count);

        if (CurrentQuestionIndex < maxQuestions)
        {
            CurrentQuestion = _questions[CurrentQuestionIndex];
            StartQuestionTimer();
            QuestionChanged?.Invoke(this, CurrentQuestion);
            _soundService?.PlayAudioSnippet(CurrentQuestion.AudioSnippetPath);
        }
        else
        {
            StopTimer();
            IsAnswerRevealed = true;
            IsGameActive = false;
            Phase = GameStatePhase.GameOver;
            CheckGameWinner();
        }
    }

    public void RevealAnswer()
    {
        if (IsAnswerRevealed) return;
        StopTimer();
        _soundService?.StopAudioSnippet();
        _soundService?.PlayRevealChime();

        IsAnswerRevealed = true;
        Phase = GameStatePhase.AnswerRevealed;

        EvaluateSubmittedAnswers();

        // If Automatic mode is enabled and we did not enter GameOver or SuperStreak, schedule auto-advance
        ScheduleAutoAdvanceIfEligible();
    }

    private void ScheduleAutoAdvanceIfEligible()
    {
        if (Settings.GameMode == GameAdvanceMode.Automatic &&
            Phase != GameStatePhase.GameOver &&
            Phase != GameStatePhase.SuperStreak)
        {
            int delayMs = Math.Max(2, Settings.AutoAdvanceDelaySeconds) * 1000;
            _autoAdvanceTimer?.Dispose();
            _autoAdvanceTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    RunOnUI(NextQuestion);
                }
                catch (Exception ex)
                {
                    Globals.LogError("KnockoutTrivia", "GameStateService.AutoAdvance", ex);
                }
            }, null, delayMs, Timeout.Infinite);
        }
    }

    private int CalculatePointsForCorrectAnswer(KnockoutPlayer player)
    {
        int basePoints = Settings.PointsPerCorrectAnswer;
        if (Settings.EnableSpeedBonus && TotalCountdownSeconds > 0 && player.ResponseTimeMs > 0)
        {
            double maxMs = TotalCountdownSeconds * 1000.0;
            double remainingRatio = Math.Clamp(1.0 - (player.ResponseTimeMs / maxMs), 0.0, 1.0);
            int speedBonus = (int)Math.Ceiling(remainingRatio * 5.0);
            return basePoints + speedBonus;
        }
        return basePoints;
    }

    public void EvaluateSubmittedAnswers()
    {
        if (CurrentQuestion == null) return;
        int correctIndex = CurrentQuestion.CorrectAnswerIndex;

        bool anyShieldUsed = false;
        bool anyStrikeAdded = false;

        RunOnUI(() =>
        {
            _isEvaluatingBatch = true;
            try
            {
                foreach (var player in Players)
                {
                    // Skip eliminated players, already manually evaluated players, or disconnected players who haven't answered
                    if (player.IsEliminated || player.HasBeenEvaluatedForCurrentQuestion || (!player.IsConnected && !player.HasAnsweredCurrentQuestion)) continue;

                    player.HasBeenEvaluatedForCurrentQuestion = true;
                    bool isCorrect = player.HasAnsweredCurrentQuestion && player.LastAnswerIndex == correctIndex;

                    if (isCorrect)
                    {
                        int points = CalculatePointsForCorrectAnswer(player);
                        player.Score += points;
                        player.LastPointsEarned = points;
                        player.CorrectAnswersCount++;
                        if (player.ResponseTimeMs > 0)
                        {
                            player.TotalResponseTimeMs += player.ResponseTimeMs;
                        }
                        player.WasShieldProtected = false;
                        _streakService.RecordAnswer(player, true, Settings.StreakRequirement, Settings.SuperStreakThreshold, Settings.MaxTokens, Settings.SuperStreakEnabled);
                    }
                    else
                    {
                        player.LastPointsEarned = 0;
                        if (player.Tokens > 0)
                        {
                            _tokenService.DeductToken(player);
                            player.WasShieldProtected = true;
                            anyShieldUsed = true;
                        }
                        else
                        {
                            player.StrikeCount++;
                            player.WasShieldProtected = false;
                            anyStrikeAdded = true;
                            if (player.StrikeCount >= 3)
                            {
                                PlayerEliminated?.Invoke(this, player);
                            }
                        }
                        _streakService.RecordAnswer(player, false, Settings.StreakRequirement, Settings.SuperStreakThreshold, Settings.MaxTokens, Settings.SuperStreakEnabled);
                    }
                }
            }
            finally
            {
                _isEvaluatingBatch = false;
            }
            NotifyPlayerCountsChanged();
        });

        if (anyStrikeAdded)
        {
            _soundService?.PlayStrikeBuzzer();
        }
        else if (anyShieldUsed)
        {
            _soundService?.PlayShieldAbsorb();
        }

        AnswersEvaluated?.Invoke(this, EventArgs.Empty);
        CheckGameWinner();

        // Only trigger queued Super Streak if the game hasn't ended
        if (Phase != GameStatePhase.GameOver && _pendingSuperStreakPlayers.Count > 0)
        {
            var nextStreaker = _pendingSuperStreakPlayers.Dequeue();
            TriggerSuperStreakForPlayer(nextStreaker);
        }
    }

    public void RecordPlayerAnswer(string playerId, bool isCorrect)
    {
        bool evaluated = false;
        RunOnUI(() =>
        {
            var player = Players.FirstOrDefault(p => p.Id == playerId);
            if (player == null || player.IsEliminated) return;
            if (IsGameActive && CurrentQuestion != null && player.HasBeenEvaluatedForCurrentQuestion) return;

            player.HasBeenEvaluatedForCurrentQuestion = true;
            player.HasAnsweredCurrentQuestion = true;
            player.LastAnswerIndex = isCorrect ? (CurrentQuestion?.CorrectAnswerIndex ?? 0) : -1;
            evaluated = true;

            if (isCorrect)
            {
                int points = CalculatePointsForCorrectAnswer(player);
                player.Score += points;
                player.LastPointsEarned = points;
                player.CorrectAnswersCount++;
                player.WasShieldProtected = false;
                _streakService.RecordAnswer(player, true, Settings.StreakRequirement, Settings.SuperStreakThreshold, Settings.MaxTokens, Settings.SuperStreakEnabled);
            }
            else
            {
                player.LastPointsEarned = 0;
                if (player.Tokens > 0)
                {
                    _tokenService.DeductToken(player);
                    player.WasShieldProtected = true;
                    _soundService?.PlayShieldAbsorb();
                }
                else
                {
                    player.StrikeCount++;
                    player.WasShieldProtected = false;
                    _soundService?.PlayStrikeBuzzer();
                    if (player.StrikeCount >= 3)
                    {
                        PlayerEliminated?.Invoke(this, player);
                    }
                }
                _streakService.RecordAnswer(player, false, Settings.StreakRequirement, Settings.SuperStreakThreshold, Settings.MaxTokens, Settings.SuperStreakEnabled);
            }

            NotifyPlayerCountsChanged();
        });

        if (!evaluated) return;

        CheckGameWinner();

        if (Phase == GameStatePhase.QuestionActive && !IsAnswerRevealed)
        {
            CheckAutoRevealThreshold();
        }
    }

    public void AddPlayerStrike(KnockoutPlayer player)
    {
        if (player == null || player.StrikeCount >= 3) return;

        RunOnUI(() =>
        {
            player.StrikeCount++;
            _soundService?.PlayStrikeBuzzer();
            if (player.StrikeCount >= 3)
            {
                PlayerEliminated?.Invoke(this, player);
            }
            NotifyPlayerCountsChanged();
        });

        CheckGameWinner();
    }

    public void ResetPlayerStrikes(KnockoutPlayer player)
    {
        if (player == null) return;

        RunOnUI(() =>
        {
            player.StrikeCount = 0;
            UpdateSnapshotInternal();
        });
    }

    public void RevivePlayer(KnockoutPlayer player)
    {
        if (player == null) return;

        RunOnUI(() =>
        {
            if (player.IsEliminated)
            {
                player.StrikeCount = 2;
                if (player.Tokens == 0)
                {
                    player.Tokens = 1;
                }
            }
            else
            {
                player.StrikeCount = Math.Max(0, player.StrikeCount - 1);
            }

            if (Phase == GameStatePhase.GameOver && CurrentQuestionIndex < TotalQuestions)
            {
                Phase = IsAnswerRevealed ? GameStatePhase.AnswerRevealed : GameStatePhase.Lobby;
                IsGameActive = true;
            }

            UpdateSnapshotInternal();
        });
    }

    public void ResumeFromSuperStreak()
    {
        if (Phase != GameStatePhase.SuperStreak) return;

        // If another player also reached Super Streak on the same question, spin for them next
        if (_pendingSuperStreakPlayers.Count > 0)
        {
            var nextPlayer = _pendingSuperStreakPlayers.Dequeue();
            TriggerSuperStreakForPlayer(nextPlayer);
            return;
        }

        if (IsAnswerRevealed)
        {
            Phase = GameStatePhase.AnswerRevealed;
            ScheduleAutoAdvanceIfEligible();
        }
        else
        {
            Phase = GameStatePhase.QuestionActive;
            if (SecondsRemaining > 0 && IsGameActive)
            {
                IsTimerRunning = true;
                _countdownTimer?.Dispose();
                _countdownTimer = new System.Threading.Timer(OnTimerTick, null, 1000, 1000);
            }
        }
    }

    private void CheckGameWinner()
    {
        var activePlayers = Players.Where(p => !p.IsEliminated).ToList();
        if (activePlayers.Count == 1 && Players.Count > 1)
        {
            _pendingSuperStreakPlayers.Clear();
            Phase = GameStatePhase.GameOver;
            IsGameActive = false;
            _soundService?.PlayWinnerFanfare();
            GameWon?.Invoke(this, activePlayers[0]);
        }
        else if (activePlayers.Count == 0 && Players.Count > 0)
        {
            _pendingSuperStreakPlayers.Clear();
            Phase = GameStatePhase.GameOver;
            IsGameActive = false;
            GameWon?.Invoke(this, null);
        }
        else if (Phase == GameStatePhase.GameOver && activePlayers.Count > 0)
        {
            _pendingSuperStreakPlayers.Clear();
            IsGameActive = false;
            var winner = activePlayers
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.StrikeCount)
                .ThenBy(p => p.AverageResponseTimeMs)
                .First();
            _soundService?.PlayWinnerFanfare();
            GameWon?.Invoke(this, winner);
        }
    }

    public void ResetGame()
    {
        StopTimer();
        _autoAdvanceTimer?.Dispose();
        _autoAdvanceTimer = null;
        _pendingSuperStreakPlayers.Clear();
        _soundService?.StopAudioSnippet();

        ShuffleQuestions();
        CurrentQuestionIndex = 0;
        IsAnswerRevealed = false;
        IsGameActive = false;
        Phase = GameStatePhase.Lobby;

        RunOnUI(() =>
        {
            foreach (var p in Players)
            {
                p.Score = 0;
                p.StrikeCount = 0;
                p.Tokens = 1;
                p.StreakCount = 0;
                p.HasAnsweredCurrentQuestion = false;
                p.HasBeenEvaluatedForCurrentQuestion = false;
                p.LastAnswerIndex = -1;
                p.LastPointsEarned = 0;
                p.WasShieldProtected = false;
                p.ResponseTimeMs = 0;
                p.TotalResponseTimeMs = 0;
                p.CorrectAnswersCount = 0;
            }
            UpdateSnapshotInternal();
        });

        if (_questions.Count > 0)
        {
            CurrentQuestion = _questions[0];
            SecondsRemaining = Settings.QuestionTimerSeconds > 0 ? Settings.QuestionTimerSeconds : CurrentQuestion.TimeLimitSeconds;
            TotalCountdownSeconds = SecondsRemaining;
            QuestionChanged?.Invoke(this, CurrentQuestion);
        }
    }

    private static void RunOnUI(Action action)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    private static void RunOnUIAsync(Action action)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            _ = Application.Current.Dispatcher.InvokeAsync(action);
        }
        else
        {
            action();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopTimer();
        _autoAdvanceTimer?.Dispose();
    }
}
