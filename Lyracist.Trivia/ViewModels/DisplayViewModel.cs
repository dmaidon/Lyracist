// Edited on Aug 20, 2026 @ 12:10:30 -> Added SyncWithEngine and engine-state hydration to DisplayViewModel
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Trivia.Core.Models;
using Lyracist.Trivia.Core.Services;

using Application = System.Windows.Application;

namespace Lyracist.Trivia.ViewModels;

public partial class DisplayViewModel : ObservableObject
{
    private readonly TriviaGameEngine _engine;

    public string Copyright => Lyracist.Shared.Globals.Copyright;

    [ObservableProperty]
    private string _venueName = "The Main Stage Lounge";

    [ObservableProperty]
    private string _connectUrl = "http://localhost:8085";

    [ObservableProperty]
    private BitmapSource? _qrCodeImage;

    [ObservableProperty]
    private TriviaGameState _gameState = TriviaGameState.Lobby;

    [ObservableProperty]
    private string _categoryTitle = "Music & Pop Culture";

    [ObservableProperty]
    private string? _categoryBannerPath;

    [ObservableProperty]
    private string _categorySubtitle = string.Empty;

    [ObservableProperty]
    private string _featuredCategoryHeaderText = "⭐ TONIGHT'S FEATURED CATEGORY";

    [ObservableProperty]
    private string _welcomeBannerText = TriviaSettings.DefaultInstructionBannerText;

    private string _instructionBannerTemplate = TriviaSettings.DefaultInstructionBannerText;

    [ObservableProperty]
    private bool _isMixedCategoryGame;

    [ObservableProperty]
    private BitmapSource? _mixedCategoryBannerImage;

    public bool HasCategoryBanner => IsMixedCategoryGame
        ? MixedCategoryBannerImage != null
        : (!string.IsNullOrEmpty(CategoryBannerPath) && File.Exists(CategoryBannerPath));

    [ObservableProperty]
    private int _connectedPlayersCount;

    [ObservableProperty]
    private string _questionPrompt = "Question will appear here...";

    [ObservableProperty]
    private int _remainingSeconds = 15;

    [ObservableProperty]
    private int _totalCountdownSeconds = 15;

    [ObservableProperty]
    private double _countdownProgress = 1.0; // 0.0 to 1.0

    [ObservableProperty]
    private bool _isWarningActive;

    [ObservableProperty]
    private string _optionA = "Option A";

    [ObservableProperty]
    private string _optionB = "Option B";

    [ObservableProperty]
    private string _optionC = "Option C";

    [ObservableProperty]
    private string _optionD = "Option D";

    [ObservableProperty]
    private double _optionAOpacity = 1.0;

    [ObservableProperty]
    private double _optionBOpacity = 1.0;

    [ObservableProperty]
    private double _optionCOpacity = 1.0;

    [ObservableProperty]
    private double _optionDOpacity = 1.0;

    [ObservableProperty]
    private int _correctAnswerIndex = -1;

    [ObservableProperty]
    private string _explanationText = string.Empty;

    [ObservableProperty]
    private bool _isGameComplete;

    [ObservableProperty]
    private bool _hasTeamWinner;

    [ObservableProperty]
    private string _winnerTitle = string.Empty;

    [ObservableProperty]
    private string _winningName = string.Empty;

    [ObservableProperty]
    private int _winningScore;

    [ObservableProperty]
    private string _winningTeamMembersRoster = string.Empty;

    [ObservableProperty]
    private string _marqueeSummaryText = "🎯 LYRACIST LIVE TRIVIA • Scan the QR code on your phone to join now!";

    [ObservableProperty]
    private bool _isConnectInstructionsActive = true;

    [ObservableProperty]
    private bool _isAnnouncementActive;

    [ObservableProperty]
    private BitmapSource? _announcementImage;

    [ObservableProperty]
    private string _announcementDisplayName = string.Empty;

    [ObservableProperty]
    private BitmapSource? _wifiQrCodeImage;

    [ObservableProperty]
    private string _wifiSsid = "Venue Wi-Fi";

    [ObservableProperty]
    private string _wifiPassword = string.Empty;

    [ObservableProperty]
    private string _wifiPasswordDisplay = "No Password Required";

    [ObservableProperty]
    private string _preGameCountdownText = "05:00";

    [ObservableProperty]
    private int _preGameSecondsRemaining = 300;

    [ObservableProperty]
    private bool _isPreGameCountdownRunning;

    [ObservableProperty]
    private string _hostName = "Trivia Master";

    [ObservableProperty]
    private bool _isIntermissionActive;

    [ObservableProperty]
    private int _intermissionSecondsRemaining;

    [ObservableProperty]
    private string _intermissionCountdownText = "03:00";

    public ObservableCollection<TriviaPlayer> TopPlayers { get; } = [];
    public ObservableCollection<TriviaTeamSummary> TopTeams { get; } = [];
    public ObservableCollection<MarqueeScoreItem> MarqueeScores { get; } = [];
    public ObservableCollection<AnswerDistributionItem> AnswerStats { get; } = [];

    public DisplayViewModel(TriviaGameEngine engine, string venueName, string connectUrl, BitmapSource? qrCode, string? wifiSsid = null, string? wifiPassword = null, int preGameSecondsRemaining = 300)
    {
        _engine = engine;
        _venueName = venueName;
        _connectUrl = connectUrl;
        _qrCodeImage = qrCode ?? GenerateQrBitmap(connectUrl);
        _isConnectInstructionsActive = (_engine.State == TriviaGameState.Lobby);

        UpdateWifiCredentials(wifiSsid ?? string.Empty, wifiPassword ?? string.Empty);
        UpdatePreGameCountdown(preGameSecondsRemaining);
        RefreshWelcomeBannerText();

        _engine.StateChanged += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleStateChanged(e));
        _engine.TimerTick += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleTimerTick(e));
        _engine.QuestionStarted += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleQuestionStarted(e));
        _engine.AnswersEliminated += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleAnswersEliminated(e));
        _engine.AnswerRevealed += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleAnswerRevealed(e));
        _engine.LeaderboardUpdated += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => RefreshTopPlayers(e));
        _engine.GameCompleted += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleGameCompleted(e));
        _engine.IntermissionTick += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleIntermissionTick(e));
        _engine.IntermissionCompleted += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleIntermissionCompleted());

        SyncWithEngine();
    }

    public static BitmapSource? GenerateQrBitmap(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try
        {
            using var qrGenerator = new QRCoder.QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            byte[] qrBytes = qrCode.GetGraphic(20);

            using var stream = new System.IO.MemoryStream(qrBytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sets the game master's raw instruction banner template (may contain {venue}/{dj} tokens)
    /// and re-resolves it immediately against the current venue/host names.
    /// </summary>
    public void UpdateInstructionBannerTemplate(string template)
    {
        _instructionBannerTemplate = template;
        RefreshWelcomeBannerText();
    }

    private void RefreshWelcomeBannerText()
    {
        WelcomeBannerText = TriviaSettings.ResolveInstructionBanner(_instructionBannerTemplate, VenueName, HostName);
    }

    partial void OnVenueNameChanged(string value) => RefreshWelcomeBannerText();

    partial void OnHostNameChanged(string value) => RefreshWelcomeBannerText();

    public void UpdateWifiCredentials(string ssid, string password)
    {
        WifiSsid = string.IsNullOrWhiteSpace(ssid) ? (Lyracist.Shared.WifiHelper.GetConnectedSsid() ?? "Ask Host for Wi-Fi") : ssid;
        WifiPassword = password;
        WifiPasswordDisplay = string.IsNullOrWhiteSpace(password) ? "No Password Required" : password;

        try
        {
            string payload = string.IsNullOrWhiteSpace(password)
                ? $"WIFI:S:{WifiSsid};T:nopass;;;"
                : $"WIFI:S:{WifiSsid};T:WPA;P:{password};;";

            using var qrGenerator = new QRCoder.QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            byte[] qrBytes = qrCode.GetGraphic(20);

            using var stream = new System.IO.MemoryStream(qrBytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            WifiQrCodeImage = image;
        }
        catch
        {
            WifiQrCodeImage = null;
        }
    }

    public void UpdateCategory(string category, string? subtitle = null)
    {
        IsMixedCategoryGame = false;
        MixedCategoryBannerImage = null;

        CategoryTitle = category;
        if (!string.IsNullOrEmpty(subtitle))
        {
            CategorySubtitle = subtitle;
        }
        CategoryBannerPath = TriviaStorageHelper.GetBannerPathForPack(category);
        OnPropertyChanged(nameof(HasCategoryBanner));
    }

    /// <summary>
    /// Renders a fresh lobby banner listing every checked pack's category instead of the plain
    /// "Mixed Trivia" text fallback. Called every time the game master's pack checklist changes,
    /// so the banner always reflects the current mix.
    /// </summary>
    public void UpdateMixedCategory(List<TriviaQuestionPack> packs)
    {
        IsMixedCategoryGame = true;
        CategoryBannerPath = null;
        CategoryTitle = "Mixed Trivia";
        CategorySubtitle = $"A randomized mix of {packs.Count} categories: {string.Join(", ", packs.Select(p => p.Title))}";

        try
        {
            MixedCategoryBannerImage = Lyracist.Trivia.Services.TriviaBannerGenerator.RenderMixedBanner(packs);
        }
        catch
        {
            MixedCategoryBannerImage = null;
        }

        OnPropertyChanged(nameof(HasCategoryBanner));
    }

    /// <summary>
    /// Refreshes the lobby's "TONIGHT'S GAME FEATURES N QUESTIONS FROM THE FOLLOWING
    /// CATEGORY/CATEGORIES:" header. Called alongside UpdateCategory/UpdateMixedCategory whenever
    /// the game master's pack checklist or question-count setting changes.
    /// </summary>
    public void UpdateFeaturedCategoryHeader(int questionCount, int categoryCount)
    {
        FeaturedCategoryHeaderText = TriviaPackManager.BuildFeaturedCategoryHeader(questionCount, categoryCount);
    }

    public void UpdatePreGameCountdown(int secondsRemaining)
    {
        PreGameSecondsRemaining = secondsRemaining;
        IsPreGameCountdownRunning = secondsRemaining > 0;
        int mins = secondsRemaining / 60;
        int secs = secondsRemaining % 60;
        PreGameCountdownText = $"{mins:D2}:{secs:D2}";
    }

    /// Shows a pre-game advertisement banner full-screen, ahead of the connect instructions
    /// screen. Loaded fresh from disk each call rather than cached, since these banners are
    /// large (1-2 MB) and only shown occasionally.
    public void ShowAnnouncement(string imagePath, string displayName)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(imagePath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            AnnouncementImage = image;
            AnnouncementDisplayName = displayName;
            IsAnnouncementActive = true;
        }
        catch
        {
            AnnouncementImage = null;
            IsAnnouncementActive = false;
        }
    }

    public void DismissAnnouncement()
    {
        IsAnnouncementActive = false;
    }

    public void SyncWithEngine()
    {
        GameState = _engine.State;
        if (_engine.State == TriviaGameState.Lobby)
        {
            IsConnectInstructionsActive = true;
            IsGameComplete = false;
        }
        else
        {
            IsConnectInstructionsActive = false;
            IsPreGameCountdownRunning = false;
            IsGameComplete = (_engine.State == TriviaGameState.GameComplete);

            var currentQ = _engine.CurrentSession?.CurrentQuestion;
            if (currentQ != null)
            {
                CategoryTitle = currentQ.Category;
                QuestionPrompt = currentQ.Prompt;
                OptionA = currentQ.Options.Count > 0 ? currentQ.Options[0] : "";
                OptionB = currentQ.Options.Count > 1 ? currentQ.Options[1] : "";
                OptionC = currentQ.Options.Count > 2 ? currentQ.Options[2] : "";
                OptionD = currentQ.Options.Count > 3 ? currentQ.Options[3] : "";

                OptionAOpacity = _engine.EliminatedAnswerIndices.Contains(0) ? 0.12 : 1.0;
                OptionBOpacity = _engine.EliminatedAnswerIndices.Contains(1) ? 0.12 : 1.0;
                OptionCOpacity = _engine.EliminatedAnswerIndices.Contains(2) ? 0.12 : 1.0;
                OptionDOpacity = _engine.EliminatedAnswerIndices.Contains(3) ? 0.12 : 1.0;

                RemainingSeconds = _engine.RemainingSeconds;
                TotalCountdownSeconds = _engine.TotalCountdownSeconds;
                CountdownProgress = TotalCountdownSeconds > 0 ? Math.Clamp((double)RemainingSeconds / TotalCountdownSeconds, 0.0, 1.0) : 0;
                IsWarningActive = _engine.IsInWarningCountdown;

                if (_engine.State == TriviaGameState.RevealAnswer)
                {
                    CorrectAnswerIndex = currentQ.CorrectAnswerIndex;
                    ExplanationText = currentQ.Explanation;
                    OptionAOpacity = (currentQ.CorrectAnswerIndex == 0) ? 1.0 : 0.12;
                    OptionBOpacity = (currentQ.CorrectAnswerIndex == 1) ? 1.0 : 0.12;
                    OptionCOpacity = (currentQ.CorrectAnswerIndex == 2) ? 1.0 : 0.12;
                    OptionDOpacity = (currentQ.CorrectAnswerIndex == 3) ? 1.0 : 0.12;

                    AnswerStats.Clear();
                    var dist = _engine.GetAnswerDistribution();
                    string[] labels = ["A", "B", "C", "D"];
                    for (int i = 0; i < 4; i++)
                    {
                        int count = dist.GetValueOrDefault(i, 0);
                        string text = (currentQ.Options.Count > i) ? currentQ.Options[i] : $"Option {labels[i]}";
                        bool isCorrect = (i == currentQ.CorrectAnswerIndex);
                        AnswerStats.Add(new AnswerDistributionItem(labels[i], text, count, isCorrect));
                    }
                }
            }

            if (_engine.State == TriviaGameState.GameComplete)
            {
                IsIntermissionActive = _engine.IsInIntermission;
                IntermissionSecondsRemaining = _engine.IntermissionSecondsRemaining;
                int mins = IntermissionSecondsRemaining / 60;
                int secs = IntermissionSecondsRemaining % 60;
                IntermissionCountdownText = $"{mins:D2}:{secs:D2}";
            }
        }

        RefreshTopPlayers(_engine.GetPlayers());
    }

    private void HandleStateChanged(TriviaGameState state)
    {
        GameState = state;
        if (state == TriviaGameState.Lobby)
        {
            IsConnectInstructionsActive = true;
            IsGameComplete = false;
        }
        else if (state != TriviaGameState.GameComplete)
        {
            IsConnectInstructionsActive = false;
        }
    }

    private void HandleTimerTick(int secondsRemaining)
    {
        RemainingSeconds = secondsRemaining;
        TotalCountdownSeconds = _engine.TotalCountdownSeconds;
        CountdownProgress = TotalCountdownSeconds > 0 ? Math.Clamp((double)secondsRemaining / TotalCountdownSeconds, 0.0, 1.0) : 0;
        IsWarningActive = _engine.IsInWarningCountdown;
    }

    private void HandleQuestionStarted(TriviaQuestion q)
    {
        CategoryTitle = q.Category;
        QuestionPrompt = q.Prompt;
        OptionA = q.Options.Count > 0 ? q.Options[0] : "";
        OptionB = q.Options.Count > 1 ? q.Options[1] : "";
        OptionC = q.Options.Count > 2 ? q.Options[2] : "";
        OptionD = q.Options.Count > 3 ? q.Options[3] : "";

        OptionAOpacity = 1.0;
        OptionBOpacity = 1.0;
        OptionCOpacity = 1.0;
        OptionDOpacity = 1.0;

        CorrectAnswerIndex = -1;
        ExplanationText = string.Empty;
        IsGameComplete = false;
        IsConnectInstructionsActive = false;
        RemainingSeconds = _engine.RemainingSeconds;
        TotalCountdownSeconds = _engine.TotalCountdownSeconds;
        CountdownProgress = 1.0;
        IsWarningActive = false;
        AnswerStats.Clear();
    }

    private void HandleAnswersEliminated(List<int> eliminatedIndices)
    {
        if (eliminatedIndices.Contains(0)) OptionAOpacity = 0.12;
        if (eliminatedIndices.Contains(1)) OptionBOpacity = 0.12;
        if (eliminatedIndices.Contains(2)) OptionCOpacity = 0.12;
        if (eliminatedIndices.Contains(3)) OptionDOpacity = 0.12;
    }

    private void HandleAnswerRevealed(TriviaQuestion q)
    {
        CorrectAnswerIndex = q.CorrectAnswerIndex;
        ExplanationText = q.Explanation;
        IsWarningActive = false;

        // Ensure all wrong options are faded
        OptionAOpacity = (q.CorrectAnswerIndex == 0) ? 1.0 : 0.12;
        OptionBOpacity = (q.CorrectAnswerIndex == 1) ? 1.0 : 0.12;
        OptionCOpacity = (q.CorrectAnswerIndex == 2) ? 1.0 : 0.12;
        OptionDOpacity = (q.CorrectAnswerIndex == 3) ? 1.0 : 0.12;

        AnswerStats.Clear();
        var dist = _engine.GetAnswerDistribution();
        string[] labels = ["A", "B", "C", "D"];
        for (int i = 0; i < 4; i++)
        {
            int count = dist.GetValueOrDefault(i, 0);
            string text = (q.Options.Count > i) ? q.Options[i] : $"Option {labels[i]}";
            bool isCorrect = (i == q.CorrectAnswerIndex);
            AnswerStats.Add(new AnswerDistributionItem(labels[i], text, count, isCorrect));
        }
    }

    private void RefreshTopPlayers(List<TriviaPlayer> list)
    {
        ConnectedPlayersCount = list.Count;
        TopPlayers.Clear();
        MarqueeScores.Clear();
        var ranked = list.OrderByDescending(p => p.TotalScore).ToList();
        for (int i = 0; i < ranked.Count; i++)
        {
            var p = ranked[i];
            if (i < 10)
            {
                TopPlayers.Add(p);
            }

            MarqueeScores.Add(new MarqueeScoreItem
            {
                Rank = i + 1,
                Name = p.Name,
                TeamName = p.TeamName,
                Score = p.TotalScore,
                IsTopScorer = (i == 0 && p.TotalScore > 0)
            });
        }

        if (MarqueeScores.Count > 0)
        {
            MarqueeSummaryText = string.Join("      ★      ", MarqueeScores.Select(m => m.FormattedText));
        }
        else
        {
            MarqueeSummaryText = "🎯 LYRACIST LIVE TRIVIA • Scan the QR code on your phone to join the show!";
        }
    }

    private void HandleGameCompleted(TriviaGameResult result)
    {
        GameState = TriviaGameState.GameComplete;
        IsGameComplete = true;
        HasTeamWinner = result.HasTeamWinner;
        WinnerTitle = result.WinnerTitle;
        WinningName = result.WinningName;
        WinningScore = result.WinningScore;
        WinningTeamMembersRoster = result.WinningTeamMembersRoster;

        TopTeams.Clear();
        foreach (var t in result.Teams)
        {
            TopTeams.Add(t);
        }

        RefreshTopPlayers(result.RankedPlayers);
    }

    private void HandleIntermissionTick(int secondsRemaining)
    {
        IsIntermissionActive = secondsRemaining > 0;
        IntermissionSecondsRemaining = secondsRemaining;
        int mins = secondsRemaining / 60;
        int secs = secondsRemaining % 60;
        IntermissionCountdownText = $"{mins:D2}:{secs:D2}";
    }

    private void HandleIntermissionCompleted()
    {
        IsIntermissionActive = false;
        IntermissionSecondsRemaining = 0;
    }
}

public class MarqueeScoreItem
{
    public int Rank { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int Score { get; set; }
    public bool IsTopScorer { get; set; }

    public string FormattedText => IsTopScorer
        ? $"👑 1. {Name}{(!string.IsNullOrWhiteSpace(TeamName) && TeamName != Name ? $" ({TeamName})" : "")}: {Score:N0} pts"
        : $"{Rank}. {Name}{(!string.IsNullOrWhiteSpace(TeamName) && TeamName != Name ? $" ({TeamName})" : "")}: {Score:N0} pts";
}
