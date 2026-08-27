// Created on Aug 27, 2026 @ 14:35:05 -> KnockoutPlayer model with observable strike states, tokens, and streak counts
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
