// Edited on Aug 28, 2026 @ 09:14:00 -> Added companion connection tracking, answer submissions, and shield protection state
using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KnockoutTrivia.Models;

public partial class KnockoutPlayer : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [ObservableProperty]
    private string _name = string.Empty;

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

    public Brush StrikeColorBrush => StrikeCount switch
    {
        0 => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
        1 => new SolidColorBrush(Color.FromRgb(234, 179, 8)),
        2 => new SolidColorBrush(Color.FromRgb(249, 115, 22)),
        _ => new SolidColorBrush(Color.FromRgb(239, 68, 68))
    };
}
