// Created on Aug 27, 2026 @ 15:07:10 -> SelectableTriviaSource model for multi-database and pack selection
using CommunityToolkit.Mvvm.ComponentModel;

namespace KnockoutTrivia.Models;

public enum TriviaSourceType
{
    Database,
    Pack
}

public partial class SelectableTriviaSource : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private TriviaSourceType _sourceType = TriviaSourceType.Database;

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private int _questionCount;

    public string TypeBadge => SourceType == TriviaSourceType.Database ? "DB" : "PACK";
    public string TypeBadgeColor => SourceType == TriviaSourceType.Database ? "#10B981" : "#3B82F6";
    public string DisplaySubtitle => $"{TypeBadge} • {QuestionCount} Questions";
}
