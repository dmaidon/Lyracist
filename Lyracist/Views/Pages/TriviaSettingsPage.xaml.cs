// Created on Aug 17, 2026 @ 15:46:30 -> Code-behind for TriviaSettingsPage
using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class TriviaSettingsPage : Page
{
    public TriviaSettingsViewModel ViewModel { get; }

    public TriviaSettingsPage(TriviaSettingsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}
