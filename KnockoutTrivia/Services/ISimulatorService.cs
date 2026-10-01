// Edited on Oct 1, 2026 @ 08:45:00 -> Guard bot simulation loop against carrying over into subsequent questions
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using KnockoutTrivia.Models;

namespace KnockoutTrivia.Services;

public interface ISimulatorService
{
    bool IsAutoPlayEnabled { get; set; }
    int BotCount { get; }
    int AccuracyRate { get; set; }
    ObservableCollection<string> ActivityLogs { get; }

    void SpawnBots(int count = 10);
    void ClearBots();
    Task SimulateQuestionAnswersAsync(int? accuracyPercentage = null);
    void TriggerSimulatedSuperStreak();
    void FastForwardRound();
    void ClearLogs();
}

public class SimulatorService : ISimulatorService, IDisposable
{
    private static readonly string[] BotNames =
    [
        "🤖 TriviaTitan", "🤖 QuizWhiz", "🤖 BuzzerBeater", "🤖 Brainiac",
        "🤖 LuckyStrike", "🤖 SpeedyAnswer", "🤖 NeonQuizzer", "🤖 CyberHost",
        "🤖 PocketGenius", "🤖 RetroPlayer", "🤖 QuantumBuzzer", "🤖 PixelPro",
        "🤖 StrikeDodger", "🤖 ShieldMaster", "🤖 TurboTrivia", "🤖 StarPlayer"
    ];

    private readonly IGameStateService _gameStateService;
    private readonly Random _random = new();
    private bool _isAutoPlayEnabled;
    private int _accuracyRate = 75;
    private bool _disposed;
    private CancellationTokenSource? _autoPlayCts;

    public bool IsAutoPlayEnabled
    {
        get => _isAutoPlayEnabled;
        set
        {
            if (_isAutoPlayEnabled != value)
            {
                _isAutoPlayEnabled = value;
                Log(value ? "Auto-play enabled: bots will automatically answer each question." : "Auto-play disabled.");
            }
        }
    }

    public int BotCount => _gameStateService.Players.Count(p => p.IsBot);

    public int AccuracyRate
    {
        get => _accuracyRate;
        set => _accuracyRate = Math.Clamp(value, 0, 100);
    }

    public ObservableCollection<string> ActivityLogs { get; } = [];

    public SimulatorService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
        _gameStateService.QuestionChanged += OnQuestionChanged;
        _gameStateService.PhaseChanged += OnPhaseChanged;
    }

    public void SpawnBots(int count = 10)
    {
        int existingCount = _gameStateService.Players.Count;
        for (int i = 0; i < count; i++)
        {
            string name = (i < BotNames.Length) ? BotNames[i] : $"🤖 Bot #{existingCount + i + 1}";
            var player = _gameStateService.RegisterOrGetPlayer(name);
            player.IsBot = true;
            player.Tokens = 1;
            player.IsConnected = true;
        }

        Log($"Spawned {count} bot players. Total bots: {BotCount}, Total room players: {_gameStateService.Players.Count}");
    }

    public void ClearBots()
    {
        var bots = _gameStateService.Players.Where(p => p.IsBot).ToList();
        foreach (var bot in bots)
        {
            _gameStateService.RemovePlayer(bot.Id);
        }

        Log($"Cleared {bots.Count} bot players. Remaining players: {_gameStateService.Players.Count}");
    }

    public async Task SimulateQuestionAnswersAsync(int? accuracyPercentage = null)
    {
        if (_gameStateService.Phase != GameStatePhase.QuestionActive || _gameStateService.CurrentQuestion == null)
        {
            Log("Cannot simulate answers: Game is not in QuestionActive phase or question is null.");
            return;
        }

        int targetAccuracy = accuracyPercentage ?? AccuracyRate;
        var q = _gameStateService.CurrentQuestion;
        int correctIndex = q.CorrectAnswerIndex;
        int optCount = q.Options.Count > 0 ? q.Options.Count : 4;

        var activeBots = _gameStateService.GetPlayersSnapshot()
            .Where(p => p.IsBot && !p.IsEliminated && !p.HasAnsweredCurrentQuestion)
            .ToList();

        if (activeBots.Count == 0)
        {
            Log("No active bots available to answer.");
            return;
        }

        Log($"Simulating answers for {activeBots.Count} bots (Accuracy: {targetAccuracy}%)...");

        // Randomize bot response sequence
        var randomizedBots = activeBots.OrderBy(_ => _random.Next()).ToList();

        string? targetQuestionId = q.Id;
        foreach (var bot in randomizedBots)
        {
            if (_gameStateService.Phase != GameStatePhase.QuestionActive || _gameStateService.IsAnswerRevealed || _gameStateService.CurrentQuestion?.Id != targetQuestionId)
            {
                break;
            }

            int responseDelayMs = _random.Next(400, 2800);
            await Task.Delay(responseDelayMs);

            if (_gameStateService.Phase != GameStatePhase.QuestionActive || _gameStateService.IsAnswerRevealed || _gameStateService.CurrentQuestion?.Id != targetQuestionId)
            {
                break;
            }

            bool pickCorrect = _random.Next(100) < targetAccuracy;
            int chosenOption;

            if (pickCorrect && correctIndex >= 0 && correctIndex < optCount)
            {
                chosenOption = correctIndex;
            }
            else
            {
                var wrongOptions = Enumerable.Range(0, optCount).Where(idx => idx != correctIndex).ToList();
                chosenOption = wrongOptions.Count > 0 ? wrongOptions[_random.Next(wrongOptions.Count)] : 0;
            }

            _gameStateService.SubmitPlayerAnswer(bot.Id, chosenOption, responseDelayMs, bot.SessionToken);
            string resultEmoji = (chosenOption == correctIndex) ? "✅ Correct" : "❌ Incorrect";
            Log($"{bot.Name} picked Opt {chosenOption + 1} ({resultEmoji}) in {responseDelayMs}ms");
        }
    }

    public void TriggerSimulatedSuperStreak()
    {
        var bot = _gameStateService.Players.FirstOrDefault(p => p.IsBot && !p.IsEliminated) ?? _gameStateService.Players.FirstOrDefault();
        if (bot == null)
        {
            SpawnBots(5);
            bot = _gameStateService.Players.First();
        }

        bot.StreakCount = 19;
        _gameStateService.RecordPlayerAnswer(bot.Id, true);
        Log($"Set {bot.Name} streak to 20! Super Streak Triggered.");
    }

    public void FastForwardRound()
    {
        if (_gameStateService.Phase == GameStatePhase.QuestionActive)
        {
            _gameStateService.RevealAnswer();
            Log("Fast-forwarded round: Answer Revealed.");
        }
        else if (_gameStateService.Phase == GameStatePhase.AnswerRevealed)
        {
            _gameStateService.NextQuestion();
            Log("Fast-forwarded round: Next Question Loaded.");
        }
        else if (_gameStateService.Phase == GameStatePhase.Lobby)
        {
            _gameStateService.StartGame();
            Log("Fast-forwarded round: Game Started.");
        }
    }

    public void ClearLogs()
    {
        RunOnUI(() => ActivityLogs.Clear());
    }

    private void OnQuestionChanged(object? sender, KnockoutQuestion? e)
    {
        if (e != null)
        {
            Log($"Question #{_gameStateService.CurrentQuestionIndex + 1} Loaded: {e.Category} - \"{e.Prompt}\"");
            if (IsAutoPlayEnabled)
            {
                _autoPlayCts?.Cancel();
                _autoPlayCts = new CancellationTokenSource();
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000, _autoPlayCts.Token);
                    if (!_autoPlayCts.Token.IsCancellationRequested)
                    {
                        await SimulateQuestionAnswersAsync();
                    }
                }, _autoPlayCts.Token);
            }
        }
    }

    private void OnPhaseChanged(object? sender, GameStatePhase e)
    {
        Log($"Game Phase Changed -> {e}");
    }

    private void Log(string message)
    {
        string timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        RunOnUI(() =>
        {
            ActivityLogs.Insert(0, timestamped);
            while (ActivityLogs.Count > 100)
            {
                ActivityLogs.RemoveAt(ActivityLogs.Count - 1);
            }
        });
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
        _autoPlayCts?.Cancel();
        _autoPlayCts?.Dispose();
        _gameStateService.QuestionChanged -= OnQuestionChanged;
        _gameStateService.PhaseChanged -= OnPhaseChanged;
    }
}
