// Edited on Aug 29, 2026 @ 10:44:00 -> Added IsBot property for simulated player diagnostic testing
using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KnockoutTrivia.Models;

public partial class KnockoutPlayer : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [ObservableProperty]
    private string _sessionToken = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isBot;

    [ObservableProperty]
    private int _score;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEliminated))]
    [NotifyPropertyChangedFor(nameof(StrikeColorHex))]
    [NotifyPropertyChangedFor(nameof(StrikeColorBrush))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int _strikeCount;

    [ObservableProperty]
    private int _tokens;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StreakMeterProgress))]
    private int _streakCount;

    [ObservableProperty]
    private bool _isCurrentTurn;

    [ObservableProperty]
    private bool _isConnected = true;

    [ObservableProperty]
    private DateTime _lastSeenAt = DateTime.Now;

    [ObservableProperty]
    private int _lastAnswerIndex = -1;

    [ObservableProperty]
    private bool _hasAnsweredCurrentQuestion;

    [ObservableProperty]
    private int _lastPointsEarned;

    [ObservableProperty]
    private bool _wasShieldProtected;

    [ObservableProperty]
    private int _responseTimeMs;

    public bool IsEliminated => StrikeCount >= 3;

    public int StreakMeterProgress => StreakCount == 0 ? 0 : ((StreakCount - 1) % 5) + 1;

    public string StatusText => StrikeCount switch
    {
        0 => "ACTIVE",
        1 => "1 STRIKE",
        2 => "DANGER",
        _ => "KNOCKED OUT"
    };

    public string StrikeColorHex => StrikeCount switch
    {
        0 => "#10B981", // Emerald Green
        1 => "#EAB308", // Warning Yellow
        2 => "#F97316", // Danger Orange
        _ => "#EF4444"  // Knockout Red
    };

    private static readonly Brush ActiveBrush = CreateFrozenBrush(16, 185, 129);
    private static readonly Brush WarningBrush = CreateFrozenBrush(234, 179, 8);
    private static readonly Brush DangerBrush = CreateFrozenBrush(249, 115, 22);
    private static readonly Brush EliminatedBrush = CreateFrozenBrush(239, 68, 68);

    private static Brush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public Brush StrikeColorBrush => StrikeCount switch
    {
        0 => ActiveBrush,
        1 => WarningBrush,
        2 => DangerBrush,
        _ => EliminatedBrush
    };
}
