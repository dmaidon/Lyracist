// Edited on Aug 29, 2026 @ 10:35:00 -> Fixed UI dispatch for auto-reveal, eliminated deadlock hazard, hardened player registration and answers, and added Fisher-Yates shuffle
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using KnockoutTrivia.Models;

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

    event EventHandler<KnockoutQuestion?>? QuestionChanged;
    event EventHandler<GameStatePhase>? PhaseChanged;
    event EventHandler<int>? TimerTicked;
    event EventHandler<KnockoutPlayer>? SuperStreakTriggered;
    event EventHandler<KnockoutPlayer>? PlayerEliminated;
    event EventHandler<KnockoutPlayer>? GameWon;
    event EventHandler? AnswersEvaluated;

    void InitializeGame(KnockoutSettings settings);
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
    void ResetGame();
}

public class GameStateService : ObservableObject, IGameStateService, IDisposable
{
    private readonly ITriviaDataService _triviaDataService;
    private readonly ITokenService _tokenService;
    private readonly IStreakService _streakService;

    private List<KnockoutQuestion> _questions = [];
    private int _currentQuestionIndex;
    private KnockoutQuestion? _currentQuestion;
    private bool _isAnswerRevealed;
    private bool _isGameActive;
    private GameStatePhase _phase = GameStatePhase.Lobby;
    private int _secondsRemaining = 15;
    private int _totalCountdownSeconds = 15;
    private bool _isTimerRunning;

    private System.Threading.Timer? _countdownTimer;
    private System.Threading.Timer? _autoAdvanceTimer;
    private bool _disposed;

    public ObservableCollection<KnockoutPlayer> Players { get; } = [];

    // Players is mutated on the UI thread (via RunOnUI) but is also polled by the web server's
    // socket-handler threads and the bot simulator's background tasks. Enumerating an
    // ObservableCollection while another thread mutates it is undefined behavior, so any
    // non-UI-thread reader must go through this snapshot rather than touching Players directly.
    public List<KnockoutPlayer> GetPlayersSnapshot()
    {
        List<KnockoutPlayer> snapshot = [];
        RunOnUI(() => snapshot = [.. Players]);
        return snapshot;
    }

    public KnockoutQuestion? CurrentQuestion
    {
        get => _currentQuestion;
        private set => SetProperty(ref _currentQuestion, value);
    }

    public int CurrentQuestionIndex
    {
        get => _currentQuestionIndex;
        private set => SetProperty(ref _currentQuestionIndex, value);
    }

    public int TotalQuestions => _questions.Count;

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

    public int AnsweredCount => Players.Count(p => !p.IsEliminated && p.HasAnsweredCurrentQuestion);
    public int ActivePlayerCount => Players.Count(p => !p.IsEliminated);

    public KnockoutSettings Settings { get; private set; } = new();

    public event EventHandler<KnockoutQuestion?>? QuestionChanged;
    public event EventHandler<GameStatePhase>? PhaseChanged;
    public event EventHandler<int>? TimerTicked;
    public event EventHandler<KnockoutPlayer>? SuperStreakTriggered;
    public event EventHandler<KnockoutPlayer>? PlayerEliminated;
    public event EventHandler<KnockoutPlayer>? GameWon;
    public event EventHandler? AnswersEvaluated;

    public GameStateService(
        ITriviaDataService triviaDataService,
        ITokenService tokenService,
        IStreakService streakService)
    {
        _triviaDataService = triviaDataService;
        _tokenService = tokenService;
        _streakService = streakService;

        _streakService.SuperStreakReached += (s, p) =>
        {
            Phase = GameStatePhase.SuperStreak;
            SuperStreakTriggered?.Invoke(this, p);
        };
    }

    public void InitializeGame(KnockoutSettings settings)
    {
        Settings = settings;
        ResetGame();
    }

    public async Task LoadGameQuestionsAsync(string? packOrDbPath = null)
    {
        if (packOrDbPath != null && packOrDbPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            _questions = await _triviaDataService.LoadQuestionsFromPackAsync(packOrDbPath);
        }
        else
        {
            _questions = await _triviaDataService.LoadQuestionsFromDatabaseAsync(packOrDbPath);
        }

        ShuffleQuestions();

        if (_questions.Count > 0)
        {
            CurrentQuestionIndex = 0;
            CurrentQuestion = _questions[0];
            QuestionChanged?.Invoke(this, CurrentQuestion);
        }
    }

    public async Task LoadQuestionsFromSourcesAsync(IEnumerable<string> sourcePaths)
    {
        _questions = await _triviaDataService.LoadQuestionsFromMultipleSourcesAsync(sourcePaths);

        ShuffleQuestions();

        if (_questions.Count > 0)
        {
            CurrentQuestionIndex = 0;
            CurrentQuestion = _questions[0];
            QuestionChanged?.Invoke(this, CurrentQuestion);
        }
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
            }
        });
    }

    public void ClearAllPlayers()
    {
        // Distinct from ResetGame(), which only resets stats: without an explicit "new event"
        // action, Players only ever grows (RegisterOrGetPlayer reuses disconnected records by
        // name but never deletes them), so a venue that leaves the app running across multiple
        // trivia nights would otherwise accumulate every past player forever.
        RunOnUI(() => Players.Clear());
    }

    public bool SubmitPlayerAnswer(string playerId, int answerIndex, int responseTimeMs, string? sessionToken = null)
    {
        bool accepted = false;
        RunOnUI(() =>
        {
            var player = Players.FirstOrDefault(p => string.Equals(p.Id, playerId, StringComparison.OrdinalIgnoreCase));
            if (player == null || player.IsEliminated || player.HasAnsweredCurrentQuestion) return;

            // If session token is provided, verify match
            if (!string.IsNullOrEmpty(sessionToken) && !string.Equals(player.SessionToken, sessionToken, StringComparison.Ordinal))
            {
                return;
            }

            if (Phase != GameStatePhase.QuestionActive && (!IsGameActive || IsAnswerRevealed))
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
            player.ResponseTimeMs = responseTimeMs;
            player.LastSeenAt = DateTime.Now;
            accepted = true;

            OnPropertyChanged(nameof(AnsweredCount));

            // If all active players have answered, can optionally advance
            int active = ActivePlayerCount;
            if (active > 0 && AnsweredCount >= active && Settings.GameMode == GameAdvanceMode.Automatic)
            {
                // Give a short 1s grace before auto-revealing on UI thread
                Task.Delay(1000).ContinueWith(_ =>
                {
                    RunOnUI(() =>
                    {
                        if (Phase == GameStatePhase.QuestionActive && !IsAnswerRevealed)
                        {
                            RevealAnswer();
                        }
                    });
                });
            }
        });

        return accepted;
    }

    public void StartGame()
    {
        IsGameActive = true;
        if (_questions.Count > 0)
        {
            CurrentQuestionIndex = 0;
            CurrentQuestion = _questions[0];
            QuestionChanged?.Invoke(this, CurrentQuestion);
            StartQuestionTimer();
        }
        else
        {
            Phase = GameStatePhase.QuestionActive;
        }
    }

    public void StartQuestionTimer(int? customSeconds = null)
    {
        StopTimer();

        int seconds = customSeconds ?? CurrentQuestion?.TimeLimitSeconds ?? Settings.QuestionTimerSeconds;
        if (seconds <= 0) seconds = 15;

        TotalCountdownSeconds = seconds;
        SecondsRemaining = seconds;
        IsTimerRunning = true;
        IsAnswerRevealed = false;
        Phase = GameStatePhase.QuestionActive;

        RunOnUI(() =>
        {
            foreach (var p in Players)
            {
                p.HasAnsweredCurrentQuestion = false;
                p.LastAnswerIndex = -1;
                p.WasShieldProtected = false;
                p.LastPointsEarned = 0;
            }
        });

        OnPropertyChanged(nameof(AnsweredCount));

        _countdownTimer = new System.Threading.Timer(OnTimerTick, null, 1000, 1000);
    }

    private void OnTimerTick(object? state)
    {
        // Runs on the Timer's own ThreadPool thread. SecondsRemaining is bound directly in XAML
        // (e.g. MainView's countdown), so mutating it and raising TimerTicked must happen on the
        // UI thread like every other state change in this class - not just the RevealAnswer call.
        RunOnUI(() =>
        {
            if (!IsTimerRunning) return;

            SecondsRemaining--;
            TimerTicked?.Invoke(this, SecondsRemaining);

            if (SecondsRemaining <= 0)
            {
                StopTimer();
                RevealAnswer();
            }
        });
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

        if (_questions.Count == 0) return;

        IsAnswerRevealed = false;
        CurrentQuestionIndex++;

        int maxQuestions = Settings.UnlimitedQuestions ? _questions.Count : Math.Min(_questions.Count, Settings.NumberOfQuestionsPerGame > 0 ? Settings.NumberOfQuestionsPerGame : _questions.Count);

        if (CurrentQuestionIndex < maxQuestions)
        {
            CurrentQuestion = _questions[CurrentQuestionIndex];
            QuestionChanged?.Invoke(this, CurrentQuestion);
            StartQuestionTimer();
        }
        else
        {
            StopTimer();
            Phase = GameStatePhase.GameOver;
            CheckGameWinner();
        }
    }

    public void RevealAnswer()
    {
        StopTimer();
        IsAnswerRevealed = true;
        Phase = GameStatePhase.AnswerRevealed;

        EvaluateSubmittedAnswers();

        // If Automatic mode is enabled, schedule auto-advance
        if (Settings.GameMode == GameAdvanceMode.Automatic && Phase != GameStatePhase.GameOver && Phase != GameStatePhase.SuperStreak)
        {
            int delayMs = Math.Max(2, Settings.AutoAdvanceDelaySeconds) * 1000;
            _autoAdvanceTimer?.Dispose();
            _autoAdvanceTimer = new System.Threading.Timer(_ =>
            {
                RunOnUI(NextQuestion);
            }, null, delayMs, Timeout.Infinite);
        }
    }

    public void EvaluateSubmittedAnswers()
    {
        if (CurrentQuestion == null) return;
        int correctIndex = CurrentQuestion.CorrectAnswerIndex;

        RunOnUI(() =>
        {
            foreach (var player in Players)
            {
                if (player.IsEliminated) continue;

                bool isCorrect = player.HasAnsweredCurrentQuestion && player.LastAnswerIndex == correctIndex;

                if (isCorrect)
                {
                    player.Score += Settings.PointsPerCorrectAnswer;
                    player.LastPointsEarned = Settings.PointsPerCorrectAnswer;
                    player.WasShieldProtected = false;
                    _streakService.RecordAnswer(player, true, Settings.StreakRequirement, Settings.SuperStreakThreshold, Settings.MaxTokens);
                }
                else
                {
                    player.LastPointsEarned = 0;
                    if (player.Tokens > 0)
                    {
                        _tokenService.DeductToken(player);
                        player.WasShieldProtected = true;
                    }
                    else
                    {
                        player.StrikeCount++;
                        player.WasShieldProtected = false;
                        if (player.StrikeCount >= 3)
                        {
                            PlayerEliminated?.Invoke(this, player);
                        }
                    }
                    _streakService.RecordAnswer(player, false, Settings.StreakRequirement, Settings.SuperStreakThreshold, Settings.MaxTokens);
                }
            }
        });

        AnswersEvaluated?.Invoke(this, EventArgs.Empty);
        CheckGameWinner();
    }

    public void RecordPlayerAnswer(string playerId, bool isCorrect)
    {
        var player = Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null || player.IsEliminated) return;

        RunOnUI(() =>
        {
            if (isCorrect)
            {
                player.Score += Settings.PointsPerCorrectAnswer;
                player.LastPointsEarned = Settings.PointsPerCorrectAnswer;
                player.WasShieldProtected = false;
                _streakService.RecordAnswer(player, true, Settings.StreakRequirement, Settings.SuperStreakThreshold, Settings.MaxTokens);
            }
            else
            {
                player.LastPointsEarned = 0;
                if (player.Tokens > 0)
                {
                    _tokenService.DeductToken(player);
                    player.WasShieldProtected = true;
                }
                else
                {
                    player.StrikeCount++;
                    player.WasShieldProtected = false;
                    if (player.StrikeCount >= 3)
                    {
                        PlayerEliminated?.Invoke(this, player);
                    }
                }
                _streakService.RecordAnswer(player, false, Settings.StreakRequirement, Settings.SuperStreakThreshold, Settings.MaxTokens);
            }
        });

        CheckGameWinner();
    }

    private void CheckGameWinner()
    {
        var activePlayers = Players.Where(p => !p.IsEliminated).ToList();
        if (activePlayers.Count == 1 && Players.Count > 1)
        {
            Phase = GameStatePhase.GameOver;
            GameWon?.Invoke(this, activePlayers[0]);
        }
    }

    public void ResetGame()
    {
        StopTimer();
        _autoAdvanceTimer?.Dispose();
        _autoAdvanceTimer = null;

        ShuffleQuestions();
        CurrentQuestionIndex = 0;
        IsAnswerRevealed = false;
        IsGameActive = true;
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
                p.LastAnswerIndex = -1;
                p.LastPointsEarned = 0;
                p.WasShieldProtected = false;
            }
        });

        if (_questions.Count > 0)
        {
            CurrentQuestion = _questions[0];
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopTimer();
        _autoAdvanceTimer?.Dispose();
    }
}

