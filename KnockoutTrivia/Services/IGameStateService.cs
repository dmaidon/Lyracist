// Edited on Aug 27, 2026 @ 15:20:10 -> Added ShuffleQuestions and guaranteed randomization on load and reset
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using KnockoutTrivia.Models;

namespace KnockoutTrivia.Services;

public interface IGameStateService
{
    ObservableCollection<KnockoutPlayer> Players { get; }
    KnockoutQuestion? CurrentQuestion { get; }
    int CurrentQuestionIndex { get; }
    int TotalQuestions { get; }
    bool IsAnswerRevealed { get; }
    bool IsGameActive { get; }
    KnockoutSettings Settings { get; }

    event EventHandler<KnockoutQuestion?>? QuestionChanged;
    event EventHandler<KnockoutPlayer>? SuperStreakTriggered;
    event EventHandler<KnockoutPlayer>? PlayerEliminated;
    event EventHandler<KnockoutPlayer>? GameWon;

    void InitializeGame(KnockoutSettings settings);
    Task LoadGameQuestionsAsync(string? packOrDbPath = null);
    Task LoadQuestionsFromSourcesAsync(IEnumerable<string> sourcePaths);
    void ShuffleQuestions();
    void AddPlayer(string name);
    void RemovePlayer(string playerId);
    void NextQuestion();
    void RevealAnswer();
    void RecordPlayerAnswer(string playerId, bool isCorrect);
    void ResetGame();
}

public class GameStateService : ObservableObject, IGameStateService
{
    private readonly ITriviaDataService _triviaDataService;
    private readonly ITokenService _tokenService;
    private readonly IStreakService _streakService;

    private List<KnockoutQuestion> _questions = [];
    private int _currentQuestionIndex;
    private KnockoutQuestion? _currentQuestion;
    private bool _isAnswerRevealed;
    private bool _isGameActive;

    public ObservableCollection<KnockoutPlayer> Players { get; } = [];

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

    public KnockoutSettings Settings { get; private set; } = new();

    public event EventHandler<KnockoutQuestion?>? QuestionChanged;
    public event EventHandler<KnockoutPlayer>? SuperStreakTriggered;
    public event EventHandler<KnockoutPlayer>? PlayerEliminated;
    public event EventHandler<KnockoutPlayer>? GameWon;

    public GameStateService(
        ITriviaDataService triviaDataService,
        ITokenService tokenService,
        IStreakService streakService)
    {
        _triviaDataService = triviaDataService;
        _tokenService = tokenService;
        _streakService = streakService;

        _streakService.SuperStreakReached += (s, p) => SuperStreakTriggered?.Invoke(this, p);
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
            var random = new Random();
            _questions = [.. _questions.OrderBy(_ => random.Next())];
        }
    }

    public void AddPlayer(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var p = new KnockoutPlayer
        {
            Name = name.Trim(),
            Tokens = 1 // Start with 1 default shield token
        };
        Players.Add(p);
    }

    public void RemovePlayer(string playerId)
    {
        var p = Players.FirstOrDefault(x => x.Id == playerId);
        if (p != null) Players.Remove(p);
    }

    public void NextQuestion()
    {
        if (_questions.Count == 0) return;

        IsAnswerRevealed = false;
        CurrentQuestionIndex++;

        if (CurrentQuestionIndex < _questions.Count)
        {
            CurrentQuestion = _questions[CurrentQuestionIndex];
            QuestionChanged?.Invoke(this, CurrentQuestion);
        }
        else
        {
            // Game concluded
            CheckGameWinner();
        }
    }

    public void RevealAnswer()
    {
        IsAnswerRevealed = true;
    }

    public void RecordPlayerAnswer(string playerId, bool isCorrect)
    {
        var player = Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null || player.IsEliminated) return;

        if (isCorrect)
        {
            player.Score += Settings.PointsPerCorrectAnswer;
            _streakService.RecordAnswer(player, true, Settings.StreakRequirement, Settings.SuperStreakThreshold, Settings.MaxTokens);
        }
        else
        {
            // Check if player has a shield token to absorb the strike
            if (player.Tokens > 0)
            {
                _tokenService.DeductToken(player);
            }
            else
            {
                player.StrikeCount++;
                if (player.StrikeCount >= 3)
                {
                    PlayerEliminated?.Invoke(this, player);
                }
            }
            _streakService.RecordAnswer(player, false, Settings.StreakRequirement, Settings.SuperStreakThreshold, Settings.MaxTokens);
        }

        CheckGameWinner();
    }

    private void CheckGameWinner()
    {
        var activePlayers = Players.Where(p => !p.IsEliminated).ToList();
        if (activePlayers.Count == 1 && Players.Count > 1)
        {
            GameWon?.Invoke(this, activePlayers[0]);
        }
    }

    public void ResetGame()
    {
        ShuffleQuestions();
        CurrentQuestionIndex = 0;
        IsAnswerRevealed = false;
        IsGameActive = true;

        foreach (var p in Players)
        {
            p.Score = 0;
            p.StrikeCount = 0;
            p.Tokens = 1;
            p.StreakCount = 0;
        }

        if (_questions.Count > 0)
        {
            CurrentQuestion = _questions[0];
            QuestionChanged?.Invoke(this, CurrentQuestion);
        }
    }
}
