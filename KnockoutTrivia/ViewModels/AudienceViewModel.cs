// Created on Aug 27, 2026 @ 15:26:40 -> AudienceViewModel for secondary TV and projector display output
using CommunityToolkit.Mvvm.ComponentModel;
using KnockoutTrivia.Models;

namespace KnockoutTrivia.ViewModels;

public partial class AudienceViewModel : ViewModelBase
{
    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _screenTitle = "Knockout Trivia Live";

    [ObservableProperty]
    private string _audienceMode = "Auto"; // Auto, Question, Scoreboard, Wheel, Banner

    [ObservableProperty]
    private bool _isWindowOpen;

    public void UpdateView(object? view)
    {
        CurrentView = view;
    }
}
