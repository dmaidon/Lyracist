// Edited on Sep 6, 2026 @ 07:29:15 -> Add Tempo property to support per-singer tempo recall
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lyracist.Models;

public partial class Singer : ObservableObject, Lyracist.Shared.IRotationSinger
{
    /// <summary>Stable identity assigned once at construction; never changes even when Name is
    /// edited. Used to reference a linked partner reliably.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLinked))]
    private Guid? _linkedSingerId;

    public bool IsLinked => LinkedSingerId.HasValue;

    /// <summary>Display-only convenience for the linked partner's current name - refreshed by
    /// RotationViewModel.RefreshLinkedPartnerNames() whenever the rotation changes, since Name is
    /// mutable and isn't part of the shared IRotationSinger interface.</summary>
    [ObservableProperty]
    private string _linkedPartnerName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _pinCode = string.Empty;

    [ObservableProperty]
    private string _avatarType = "None";

    [ObservableProperty]
    private string _avatarSource = string.Empty;

    [ObservableProperty]
    private string _vocalRange = string.Empty;

    [ObservableProperty]
    private string _customTitle = string.Empty;
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _duetPartnerName = string.Empty;

    public bool IsDuet => !string.IsNullOrEmpty(DuetPartnerName) && DuetPartnerName != "None";

    [ObservableProperty]
    private string _key = "0"; // e.g., +2, -1, or 0

    [ObservableProperty]
    private double _tempo = 1.0; // e.g. 1.0x, 1.1x

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _songTitle = string.Empty;

    [ObservableProperty]
    private string _artist = string.Empty;

    [ObservableProperty]
    private string _externalLink = string.Empty;

    [ObservableProperty]
    private string _source = "Local"; // Local, PartyTyme, Spotify, YouTube, Amazon

    [ObservableProperty]
    private int _completedCount = 0;

    [ObservableProperty]
    private bool _isPaused = false;

    [ObservableProperty]
    private bool _isInactive = false;

    [ObservableProperty]
    private bool _hasSungInLastRound = false;

    [ObservableProperty]
    private bool _isRotationStart = false;

    [ObservableProperty]
    private bool _isCurrent = false;

    [ObservableProperty]
    private bool _isNext = false;

    [ObservableProperty]
    private bool _isMusic = false;

    [ObservableProperty]
    private double _estimatedPerformanceSeconds = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEstimatedWait))]
    private int _estimatedWaitMinutes = 0;

    public bool HasEstimatedWait => EstimatedWaitMinutes > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(XP))]
    [NotifyPropertyChangedFor(nameof(Level))]
    [NotifyPropertyChangedFor(nameof(XPProgress))]
    [NotifyPropertyChangedFor(nameof(LevelName))]
    [NotifyPropertyChangedFor(nameof(Badges))]
    private int _score = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRatings))]
    [NotifyPropertyChangedFor(nameof(XP))]
    [NotifyPropertyChangedFor(nameof(Level))]
    [NotifyPropertyChangedFor(nameof(XPProgress))]
    [NotifyPropertyChangedFor(nameof(LevelName))]
    [NotifyPropertyChangedFor(nameof(Badges))]
    private int _ratingCount = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(XP))]
    [NotifyPropertyChangedFor(nameof(Level))]
    [NotifyPropertyChangedFor(nameof(XPProgress))]
    [NotifyPropertyChangedFor(nameof(LevelName))]
    [NotifyPropertyChangedFor(nameof(Badges))]
    private double _averageRating = 0.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(XP))]
    [NotifyPropertyChangedFor(nameof(Level))]
    [NotifyPropertyChangedFor(nameof(XPProgress))]
    [NotifyPropertyChangedFor(nameof(LevelName))]
    [NotifyPropertyChangedFor(nameof(Badges))]
    private int _totalSongsSung = 0;

    public bool HasRatings => RatingCount > 0;

    public int XP => Lyracist.Core.Helpers.SingerXpHelper.CalculateXP(TotalSongsSung, Score);
    public int Level => Lyracist.Core.Helpers.SingerXpHelper.CalculateLevel(XP);
    public double XPProgress => Lyracist.Core.Helpers.SingerXpHelper.CalculateXPProgress(XP, Level);
    public string LevelName => Lyracist.Core.Helpers.SingerXpHelper.GetLevelName(Level);
    public List<string> Badges => Lyracist.Core.Helpers.SingerXpHelper.GetBadges(TotalSongsSung, Score, AverageRating, RatingCount);
}
