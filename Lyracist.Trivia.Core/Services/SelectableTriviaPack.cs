// Created on Aug 19, 2026 -> Checked-listbox wrapper for multi-pack "mix" game selection
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lyracist.Trivia.Core.Services;

public partial class SelectableTriviaPack : ObservableObject
{
    public TriviaQuestionPack Pack { get; }

    public string Title => Pack.Title;
    public string Category => Pack.Category;
    public int QuestionCount => Pack.Questions.Count;

    [ObservableProperty]
    private bool _isChecked;

    public SelectableTriviaPack(TriviaQuestionPack pack, bool isChecked = false)
    {
        Pack = pack;
        _isChecked = isChecked;
    }
}
