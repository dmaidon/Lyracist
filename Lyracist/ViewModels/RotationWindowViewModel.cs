// Edited on Aug 6, 2026 @ 07:01:27 -> Log swallowed exception in RefreshLeaderboard; document singleton-lifetime timer/event subscriptions
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Models;
using Lyracist.Core.Helpers;

namespace Lyracist.ViewModels;

public partial class RotationWindowViewModel : BaseViewModel
{
    [ObservableProperty]
    private string _joinUrl = string.Empty;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _qrCodeImage;

    [ObservableProperty]
    private string _currentSinger = string.Empty;

    [ObservableProperty]
    private int _currentSingerScore = 0;

    [ObservableProperty]
    private double _currentSingerAverageRating = 0.0;

    [ObservableProperty]
    private int _currentSingerRatingCount = 0;

    [ObservableProperty]
    private bool _currentSingerHasRatings = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowQueue))]
    private bool _showLeaderboard = false;

    public bool ShowQueue => !ShowLeaderboard;

    [ObservableProperty]
    private int _autoAdvanceRemainingSeconds;

    [ObservableProperty]
    private bool _isAutoAdvanceActive;

    public int AutoAdvanceMaxSeconds => Lyracist.Core.Helpers.AppSettings.AutoAdvanceCountdownSeconds;

    public ObservableCollection<SingerRank> Leaderboard { get; } = [];

    public record SingerRank(string Name, int Score, double AverageRating, int Rank, int Level, string LevelName, string Badges);

    private readonly System.Timers.Timer? _toggleTimer;

    public string RatingIconSymbol => Lyracist.Core.Helpers.AppSettings.ActiveRatingIconSymbol;
    public bool IsRatingSystemEnabled => Lyracist.Core.Helpers.AppSettings.IsRatingSystemEnabled;

    public RotationWindowViewModel()
    {
        RefreshQrCode();

        // The timer and event subscription below are never stopped/unsubscribed:
        // RotationWindowViewModel is registered AddSingleton in App.xaml.cs, so exactly one
        // instance exists for the app's lifetime and both are meant to run until the process exits.
        _toggleTimer = new System.Timers.Timer(10000); // Toggle between queue and leaderboard every 10s
        _toggleTimer.Elapsed += (s, e) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ShowLeaderboard = !ShowLeaderboard;
                if (ShowLeaderboard)
                {
                    RefreshLeaderboard();
                }
                OnPropertyChanged(nameof(RatingIconSymbol));
                OnPropertyChanged(nameof(IsRatingSystemEnabled));
            });
        };
        _toggleTimer.Start();

        var showFlow = App.AppHost?.Services?.GetService(typeof(Lyracist.Core.Interfaces.IShowFlowService)) as Lyracist.Core.Interfaces.IShowFlowService;
        if (showFlow != null)
        {
            showFlow.AutoAdvanceCountdownTick += (seconds, active) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AutoAdvanceRemainingSeconds = seconds;
                    IsAutoAdvanceActive = active;
                    OnPropertyChanged(nameof(AutoAdvanceMaxSeconds));
                });
            };
        }
    }

    public void RefreshLeaderboard()
    {
        try
        {
            using var context = new Lyracist.Data.LyracistDbContext();
            var topSingers = context.Singers
                .Where(s => s.Score > 0)
                .OrderByDescending(s => s.Score)
                .Take(5)
                .ToList();

            Leaderboard.Clear();
            for (int i = 0; i < topSingers.Count; i++)
            {
                var s = topSingers[i];
                int xp = Lyracist.Core.Helpers.SingerXpHelper.CalculateXP(s.TotalSongsSung, s.Score);
                int level = Lyracist.Core.Helpers.SingerXpHelper.CalculateLevel(xp);
                string levelName = Lyracist.Core.Helpers.SingerXpHelper.GetLevelName(level);
                var badgesList = Lyracist.Core.Helpers.SingerXpHelper.GetBadges(s.TotalSongsSung, s.Score, s.AverageRating, s.RatingCount);
                string badges = string.Join(" ", badgesList);

                Leaderboard.Add(new SingerRank(s.Name, s.Score, s.AverageRating, i + 1, level, levelName, badges));
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "RefreshLeaderboard: failed to load leaderboard from database");
        }
    }

    public void RefreshQrCode()
    {
        try
        {
            string ip = AppSettings.GetActiveIPAddress();
            JoinUrl = $"http://{ip}:{AppSettings.TabletPort}";

            using var qrGenerator = new QRCoder.QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(JoinUrl, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);
            QrCodeImage = LoadImage(qrCodeAsPngByteArr);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to generate QR Code: {ex.Message}");
        }
    }

    private static System.Windows.Media.Imaging.BitmapImage? LoadImage(byte[] imageData)
    {
        if (imageData == null || imageData.Length == 0) return null;
        var image = new System.Windows.Media.Imaging.BitmapImage();
        using (var mem = new System.IO.MemoryStream(imageData))
        {
            mem.Position = 0;
            image.BeginInit();
            image.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat;
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.UriSource = null;
            image.StreamSource = mem;
            image.EndInit();
        }
        image.Freeze();
        return image;
    }

    [ObservableProperty]
    private string _nextSinger = string.Empty;

    [ObservableProperty]
    private string _announcementBanner = string.Empty;

    [ObservableProperty]
    private bool _isAnnouncementVisible;

    [ObservableProperty]
    private string _selectedProjectionView = "Normal List";

    [ObservableProperty]
    private string _crawlBannerText = string.Empty;

    [ObservableProperty]
    private string _currentSingerSong = string.Empty;

    [ObservableProperty]
    private string _currentSongTitle = string.Empty;

    [ObservableProperty]
    private string _performerHeaderText = "NOW SINGING";

    public ObservableCollection<string> NextSingers { get; } = [];

    [ObservableProperty]
    private bool _hasDesignatedCurrentSinger;

    public ObservableCollection<Singer> FullRotation { get; } = [];

    public ObservableCollection<Singer> Rotation { get; } = [];

    public void UpdateRotation(List<Singer> singers)
    {
        Rotation.Clear();
        foreach (var s in singers)
        {
            Rotation.Add(s);
        }

        // Find current singer from the passed list (already computed by main VM)
        var now = singers.FirstOrDefault(s => s.IsCurrent);
        if (now != null)
        {
            CurrentSinger = now.Name;
            CurrentSongTitle = now.SongTitle ?? string.Empty;
            CurrentSingerSong = string.IsNullOrEmpty(now.Artist) ? (now.SongTitle ?? string.Empty) : $"{now.SongTitle} - {now.Artist}";
            CurrentSingerScore = now.Score;
            CurrentSingerAverageRating = now.AverageRating;
            CurrentSingerRatingCount = now.RatingCount;
            CurrentSingerHasRatings = now.RatingCount > 0;
        }
        else
        {
            CurrentSinger = "No Singer";
            CurrentSongTitle = string.Empty;
            CurrentSingerSong = string.Empty;
            CurrentSingerScore = 0;
            CurrentSingerAverageRating = 0.0;
            CurrentSingerRatingCount = 0;
            CurrentSingerHasRatings = false;
        }

        var next = singers.FirstOrDefault(s => s.IsNext);
        if (next != null)
        {
            NextSinger = next.Name;
        }
        else
        {
            NextSinger = "None";
        }

        bool hasDesignated = now != null;
        PerformerHeaderText = hasDesignated ? "NOW SINGING" : "FIRST PERFORMER";

        // Build NextSingers queue starting with designated next singer if available,
        // followed sequentially starting after them (wrapping around).
        NextSingers.Clear();
        if (now != null)
        {
            var nextActiveSingers = Lyracist.Shared.RotationHelpers.GetNextActiveSingers(singers, now, 5);
            foreach (var candidate in nextActiveSingers)
            {
                string display = string.IsNullOrEmpty(candidate.SongTitle) ? candidate.Name : $"{candidate.Name} (\"{candidate.SongTitle}\")";
                NextSingers.Add(display);
            }
        }
        else
        {
            var activeSingers = singers.Where(s => !s.IsPaused && !s.IsInactive).Take(5).ToList();
            foreach (var singer in activeSingers)
            {
                string display = string.IsNullOrEmpty(singer.SongTitle) ? singer.Name : $"{singer.Name} (\"{singer.SongTitle}\")";
                NextSingers.Add(display);
            }
        }

        // POPULATE FullRotation exactly like KSRotation does!
        FullRotation.Clear();
        var activeRotation = singers.Where(s => !s.IsInactive && !s.IsPaused).ToList();
        HasDesignatedCurrentSinger = singers.Any(s => s.IsCurrent && !s.IsInactive && !s.IsPaused);

        var currentSingerForCrawl = singers.FirstOrDefault(s => s.IsCurrent && !s.IsInactive && !s.IsPaused)
                                  ?? singers.FirstOrDefault(s => !s.IsInactive && !s.IsPaused);

        if (currentSingerForCrawl != null && activeRotation.Count > 0)
        {
            int currentIndex = activeRotation.IndexOf(currentSingerForCrawl);
            int count = activeRotation.Count;
            for (int offset = 0; offset < count; offset++)
            {
                FullRotation.Add(activeRotation[(currentIndex + offset) % count]);
            }
        }

        OnPropertyChanged(nameof(RatingIconSymbol));
        OnPropertyChanged(nameof(IsRatingSystemEnabled));
    }

    public void HighlightSinger(Singer singer)
    {
        var currentMatch = Rotation.FirstOrDefault(s => s.Name == singer.Name);
        if (currentMatch != null)
        {
            Lyracist.Shared.RotationHelpers.SetCurrentSinger(Rotation, currentMatch);
            CurrentSinger = currentMatch.Name;
            CurrentSongTitle = currentMatch.SongTitle ?? string.Empty;
            CurrentSingerSong = string.IsNullOrEmpty(currentMatch.Artist) ? (currentMatch.SongTitle ?? string.Empty) : $"{currentMatch.SongTitle} - {currentMatch.Artist}";
            CurrentSingerScore = currentMatch.Score;
            CurrentSingerAverageRating = currentMatch.AverageRating;
            CurrentSingerRatingCount = currentMatch.RatingCount;
            CurrentSingerHasRatings = currentMatch.RatingCount > 0;
        }
        else
        {
            foreach (var s in Rotation)
            {
                s.IsCurrent = false;
                s.IsNext = false;
            }
            CurrentSinger = singer.Name;
            CurrentSongTitle = singer.SongTitle ?? string.Empty;
            CurrentSingerSong = string.IsNullOrEmpty(singer.Artist) ? (singer.SongTitle ?? string.Empty) : $"{singer.SongTitle} - {singer.Artist}";
            CurrentSingerScore = singer.Score;
            CurrentSingerAverageRating = singer.AverageRating;
            CurrentSingerRatingCount = singer.RatingCount;
            CurrentSingerHasRatings = singer.RatingCount > 0;
        }

        var next = Rotation.FirstOrDefault(s => s.IsNext);
        if (next != null)
        {
            NextSinger = next.Name;
        }
        else
        {
            NextSinger = "None";
        }

        PerformerHeaderText = "NOW SINGING";

        // Build NextSingers queue sequentially starting after currentMatch
        NextSingers.Clear();
        if (currentMatch != null)
        {
            var nextActiveSingers = Lyracist.Shared.RotationHelpers.GetNextActiveSingers(Rotation.ToList(), currentMatch, 5);
            foreach (var candidate in nextActiveSingers)
            {
                string display = string.IsNullOrEmpty(candidate.SongTitle) ? candidate.Name : $"{candidate.Name} (\"{candidate.SongTitle}\")";
                NextSingers.Add(display);
            }
        }
        else
        {
            var activeSingers = Rotation.Where(s => !s.IsPaused && !s.IsInactive).Take(5).ToList();
            foreach (var s in activeSingers)
            {
                string display = string.IsNullOrEmpty(s.SongTitle) ? s.Name : $"{s.Name} (\"{s.SongTitle}\")";
                NextSingers.Add(display);
            }
        }
        // POPULATE FullRotation exactly like KSRotation does!
        FullRotation.Clear();
        var activeRotation = Rotation.Where(s => !s.IsInactive && !s.IsPaused).ToList();
        HasDesignatedCurrentSinger = Rotation.Any(s => s.IsCurrent && !s.IsInactive && !s.IsPaused);

        var currentSingerForCrawl = Rotation.FirstOrDefault(s => s.IsCurrent && !s.IsInactive && !s.IsPaused)
                                  ?? Rotation.FirstOrDefault(s => !s.IsInactive && !s.IsPaused);

        if (currentSingerForCrawl != null && activeRotation.Count > 0)
        {
            int currentIndex = activeRotation.IndexOf(currentSingerForCrawl);
            int count = activeRotation.Count;
            for (int offset = 0; offset < count; offset++)
            {
                FullRotation.Add(activeRotation[(currentIndex + offset) % count]);
            }
        }

        OnPropertyChanged(nameof(RatingIconSymbol));
        OnPropertyChanged(nameof(IsRatingSystemEnabled));
    }
}
