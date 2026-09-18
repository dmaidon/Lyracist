// Edited on Sep 18, 2026 @ 08:46:00 -> Add CurrentSingerIsSpecial property for audience rotation display
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
    private bool _isLastRound;

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
    private System.Windows.Media.ImageSource? _currentSingerAvatar;

    [ObservableProperty]
    private bool _hasCurrentSingerAvatar;

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

    /// <summary>Whether the "{N} = Estimated wait time" legend and per-singer wait badges are
    /// shown - mirrors AppSettings.ShowEstimatedWaitTime, pushed live by DisplayService whenever
    /// the DJ flips the Settings toggle.</summary>
    [ObservableProperty]
    private bool _showEstimatedWaitTime = Lyracist.Core.Helpers.AppSettings.ShowEstimatedWaitTime;

    public ObservableCollection<SingerRank> Leaderboard { get; } = [];

    public record SingerRank(string Name, int Score, double AverageRating, int Rank, int Level, string LevelName, string Badges);

    private readonly System.Timers.Timer? _toggleTimer;

    public string RatingIconSymbol => Lyracist.Core.Helpers.AppSettings.ActiveRatingIconSymbol;
    public bool IsRatingSystemEnabled => Lyracist.Core.Helpers.AppSettings.IsRatingSystemEnabled;
    public bool ShowQrCodeOnRotationScreen => Lyracist.Core.Helpers.AppSettings.ShowQrCodeOnRotationScreen;

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
                OnPropertyChanged(nameof(ShowQrCodeOnRotationScreen));
            });
        };
        _toggleTimer.Start();

        // AutoAdvanceManager.CountdownTick is the real, running countdown - IShowFlowService's own
        // AutoAdvanceCountdownTick event can never fire (nothing calls
        // ShowFlowService.StartAutoAdvanceCountdown()), so this window's progress bar never
        // updated even though the actual auto-advance countdown was genuinely running the whole
        // time. Prefer AutoAdvanceManager; fall back to the ShowFlowService event only if for some
        // reason AutoAdvanceManager isn't registered.
        var autoAdvance = App.AppHost?.Services?.GetService(typeof(Lyracist.Services.Media.AutoAdvanceManager)) as Lyracist.Services.Media.AutoAdvanceManager;
        if (autoAdvance != null)
        {
            autoAdvance.CountdownTick += (seconds, active) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AutoAdvanceRemainingSeconds = seconds;
                    IsAutoAdvanceActive = active;
                    OnPropertyChanged(nameof(AutoAdvanceMaxSeconds));
                });
            };
        }
        else
        {
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to generate QR Code", ex);
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
    private bool _currentSingerIsRotationStart;

    [ObservableProperty]
    private bool _currentSingerIsSpecial;

    [ObservableProperty]
    private bool _nextSingerIsRotationStart;

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

    public record NextSingerDisplay(string Text, bool IsRotationStart);

    public ObservableCollection<NextSingerDisplay> NextSingers { get; } = [];

    [ObservableProperty]
    private bool _hasDesignatedCurrentSinger;

    public ObservableCollection<Singer> FullRotation { get; } = [];

    public ObservableCollection<Singer> Rotation { get; } = [];

    public void UpdateRotation(List<Singer> singers)
    {
        var visibleSingers = IsLastRound ? singers.Where(s => !s.HasSungInLastRound).ToList() : singers;

        Rotation.Clear();
        foreach (var s in visibleSingers)
        {
            Rotation.Add(s);
        }

        // Find current singer from the passed list (already computed by main VM)
        var now = visibleSingers.FirstOrDefault(s => s.IsCurrent);
        if (now != null)
        {
            CurrentSinger = now.Name;
            CurrentSongTitle = now.SongTitle ?? string.Empty;
            CurrentSingerSong = string.IsNullOrEmpty(now.Artist) ? (now.SongTitle ?? string.Empty) : $"{now.SongTitle} - {now.Artist}";
            CurrentSingerScore = now.Score;
            CurrentSingerAverageRating = now.AverageRating;
            CurrentSingerRatingCount = now.RatingCount;
            CurrentSingerHasRatings = now.RatingCount > 0;
            CurrentSingerIsRotationStart = now.IsRotationStart;
            CurrentSingerIsSpecial = now.IsSpecial;

            var avatar = SingerAvatarConverter.ResolveSingerAvatar(now);
            if (avatar == null && !string.IsNullOrEmpty(now.Name))
            {
                try
                {
                    using var db = new Lyracist.Data.LyracistDbContext();
                    var dbSinger = db.Singers.FirstOrDefault(s => s.Name == now.Name);
                    if (dbSinger != null)
                    {
                        avatar = SingerAvatarConverter.ResolveAvatarImage(dbSinger.AvatarType, dbSinger.AvatarSource);
                    }
                }
                catch
                {
                    // Ignored
                }
            }
            CurrentSingerAvatar = avatar;
            HasCurrentSingerAvatar = avatar != null;
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
            CurrentSingerIsRotationStart = false;
            CurrentSingerIsSpecial = false;
            CurrentSingerAvatar = null;
            HasCurrentSingerAvatar = false;
        }

        var next = visibleSingers.FirstOrDefault(s => s.IsNext);
        if (next != null)
        {
            NextSinger = next.Name;
            NextSingerIsRotationStart = next.IsRotationStart;
        }
        else
        {
            NextSinger = "None";
            NextSingerIsRotationStart = false;
        }

        bool hasDesignated = now != null;
        PerformerHeaderText = hasDesignated ? "NOW SINGING" : "FIRST PERFORMER";

        // Build NextSingers queue starting with designated next singer if available,
        // followed sequentially starting after them (wrapping around).
        NextSingers.Clear();
        if (now != null)
        {
            var nextActiveSingers = Lyracist.Shared.RotationHelpers.GetNextActiveSingers(visibleSingers, now, 5, isLastRound: IsLastRound);
            foreach (var candidate in nextActiveSingers)
            {
                string display = string.IsNullOrEmpty(candidate.SongTitle) ? candidate.Name : $"{candidate.Name} (\"{candidate.SongTitle}\")";
                NextSingers.Add(new NextSingerDisplay(display, candidate.IsRotationStart));
            }
        }
        else
        {
            var activeSingers = visibleSingers.Where(s => !s.IsPaused && !s.IsInactive && !s.IsSkipped).Take(5).ToList();
            foreach (var singer in activeSingers)
            {
                string display = string.IsNullOrEmpty(singer.SongTitle) ? singer.Name : $"{singer.Name} (\"{singer.SongTitle}\")";
                NextSingers.Add(new NextSingerDisplay(display, singer.IsRotationStart));
            }
        }

        // POPULATE FullRotation exactly like KSRotation does!
        FullRotation.Clear();
        var activeRotation = visibleSingers.Where(s => !s.IsInactive && !s.IsPaused && !s.IsSkipped).ToList();
        HasDesignatedCurrentSinger = Lyracist.Shared.RotationHelpers.HasActiveCurrentSinger(visibleSingers);

        var currentSingerForCrawl = Lyracist.Shared.RotationHelpers.GetCurrentSinger(visibleSingers)
                                  ?? visibleSingers.FirstOrDefault(s => !s.IsInactive && !s.IsPaused && !s.IsSkipped);

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
        OnPropertyChanged(nameof(ShowQrCodeOnRotationScreen));
    }

    public void HighlightSinger(Singer singer)
    {
        var currentMatch = Rotation.FirstOrDefault(s => s.Name == singer.Name);
        if (currentMatch != null)
        {
            Lyracist.Shared.RotationHelpers.SetCurrentSinger(Rotation, currentMatch, isLastRound: IsLastRound);
            CurrentSinger = currentMatch.Name;
            CurrentSongTitle = currentMatch.SongTitle ?? string.Empty;
            CurrentSingerSong = string.IsNullOrEmpty(currentMatch.Artist) ? (currentMatch.SongTitle ?? string.Empty) : $"{currentMatch.SongTitle} - {currentMatch.Artist}";
            CurrentSingerScore = currentMatch.Score;
            CurrentSingerAverageRating = currentMatch.AverageRating;
            CurrentSingerRatingCount = currentMatch.RatingCount;
            CurrentSingerHasRatings = currentMatch.RatingCount > 0;
            CurrentSingerIsRotationStart = currentMatch.IsRotationStart;
            CurrentSingerIsSpecial = currentMatch.IsSpecial;
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
            CurrentSingerIsRotationStart = singer.IsRotationStart;
            CurrentSingerIsSpecial = singer.IsSpecial;
        }

        var next = Rotation.FirstOrDefault(s => s.IsNext);
        if (next != null)
        {
            NextSinger = next.Name;
            NextSingerIsRotationStart = next.IsRotationStart;
        }
        else
        {
            NextSinger = "None";
            NextSingerIsRotationStart = false;
        }

        PerformerHeaderText = "NOW SINGING";

        // Build NextSingers queue sequentially starting after currentMatch
        NextSingers.Clear();
        if (currentMatch != null)
        {
            var nextActiveSingers = Lyracist.Shared.RotationHelpers.GetNextActiveSingers(Rotation.ToList(), currentMatch, 5, isLastRound: IsLastRound);
            foreach (var candidate in nextActiveSingers)
            {
                string display = string.IsNullOrEmpty(candidate.SongTitle) ? candidate.Name : $"{candidate.Name} (\"{candidate.SongTitle}\")";
                NextSingers.Add(new NextSingerDisplay(display, candidate.IsRotationStart));
            }
        }
        else
        {
            var activeSingers = Rotation.Where(s => !s.IsPaused && !s.IsInactive && !s.IsSkipped && (!IsLastRound || !s.HasSungInLastRound)).Take(5).ToList();
            foreach (var s in activeSingers)
            {
                string display = string.IsNullOrEmpty(s.SongTitle) ? s.Name : $"{s.Name} (\"{s.SongTitle}\")";
                NextSingers.Add(new NextSingerDisplay(display, s.IsRotationStart));
            }
        }
        // POPULATE FullRotation exactly like KSRotation does!
        FullRotation.Clear();
        var activeRotation = Rotation.Where(s => !s.IsInactive && !s.IsPaused && !s.IsSkipped && (!IsLastRound || !s.HasSungInLastRound)).ToList();
        HasDesignatedCurrentSinger = Lyracist.Shared.RotationHelpers.HasActiveCurrentSinger(Rotation);

        var currentSingerForCrawl = Lyracist.Shared.RotationHelpers.GetCurrentSinger(Rotation)
                                  ?? Rotation.FirstOrDefault(s => !s.IsInactive && !s.IsPaused && !s.IsSkipped);

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
        OnPropertyChanged(nameof(ShowQrCodeOnRotationScreen));
    }
}
