// Edited on Oct 1, 2026 @ 07:35:00 -> Performance: implement INotifyPropertyChanged on TriviaPlayer for in-place UI updates
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lyracist.Trivia.Core.Models;

public class TriviaPlayer : INotifyPropertyChanged
{
    private string _playerId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    private string _name = string.Empty;
    private string _teamName = string.Empty;
    private int _totalScore;
    private int _currentStreak;
    private int _maxStreak;
    private int _totalCorrect;
    private int _totalAnswered;
    private int _lastAnswerIndex = -1;
    private double _lastResponseTimeMs;
    private bool _hasAnsweredCurrentQuestion;
    private int _lastPointsEarned;
    private int _visibleOptionsAtSubmission = 4;
    private int _remainingSecondsAtSubmission;
    private int _streakBeforeAnswer;
    private int _maxStreakBeforeAnswer;
    private bool _isConnected = true;
    private DateTime _connectedAt = DateTime.UtcNow;
    private DateTime _lastSeenAt = DateTime.UtcNow;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public string PlayerId
    {
        get => _playerId;
        set => SetField(ref _playerId, value);
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetField(ref _name, value))
                OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string TeamName
    {
        get => _teamName;
        set
        {
            if (SetField(ref _teamName, value))
                OnPropertyChanged(nameof(DisplayName));
        }
    }

    public int TotalScore
    {
        get => _totalScore;
        set => SetField(ref _totalScore, value);
    }

    public int CurrentStreak
    {
        get => _currentStreak;
        set => SetField(ref _currentStreak, value);
    }

    public int MaxStreak
    {
        get => _maxStreak;
        set => SetField(ref _maxStreak, value);
    }

    public int TotalCorrect
    {
        get => _totalCorrect;
        set => SetField(ref _totalCorrect, value);
    }

    public int TotalAnswered
    {
        get => _totalAnswered;
        set => SetField(ref _totalAnswered, value);
    }

    public int LastAnswerIndex
    {
        get => _lastAnswerIndex;
        set => SetField(ref _lastAnswerIndex, value);
    }

    public double LastResponseTimeMs
    {
        get => _lastResponseTimeMs;
        set => SetField(ref _lastResponseTimeMs, value);
    }

    public bool HasAnsweredCurrentQuestion
    {
        get => _hasAnsweredCurrentQuestion;
        set => SetField(ref _hasAnsweredCurrentQuestion, value);
    }

    public int LastPointsEarned
    {
        get => _lastPointsEarned;
        set => SetField(ref _lastPointsEarned, value);
    }

    public int VisibleOptionsAtSubmission
    {
        get => _visibleOptionsAtSubmission;
        set => SetField(ref _visibleOptionsAtSubmission, value);
    }

    public int RemainingSecondsAtSubmission
    {
        get => _remainingSecondsAtSubmission;
        set => SetField(ref _remainingSecondsAtSubmission, value);
    }

    /// <summary>
    /// CurrentStreak/MaxStreak as they stood immediately before this question's answer was
    /// scored - lets TriviaGameEngine.VoidCurrentQuestion restore them exactly instead of
    /// leaving streak counters inflated by a voided question. See ScoreAnswer.
    /// </summary>
    public int StreakBeforeAnswer
    {
        get => _streakBeforeAnswer;
        set => SetField(ref _streakBeforeAnswer, value);
    }

    public int MaxStreakBeforeAnswer
    {
        get => _maxStreakBeforeAnswer;
        set => SetField(ref _maxStreakBeforeAnswer, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetField(ref _isConnected, value);
    }

    // UTC, not local time: TriviaGameEngine.GetPlayers() compares LastSeenAt against an elapsed
    // timeout, and local time jumping at a DST transition would make every player look stale (or
    // fresh) for an hour depending on which way the clocks moved.
    public DateTime ConnectedAt
    {
        get => _connectedAt;
        set => SetField(ref _connectedAt, value);
    }

    public DateTime LastSeenAt
    {
        get => _lastSeenAt;
        set => SetField(ref _lastSeenAt, value);
    }

    public string DisplayName => !string.IsNullOrWhiteSpace(TeamName) && TeamName != Name
        ? $"{Name} ({TeamName})"
        : Name;
}
