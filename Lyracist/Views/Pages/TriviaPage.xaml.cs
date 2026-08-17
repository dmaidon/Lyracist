// Created on Aug 17, 2026 @ 15:47:30 -> Code-behind for TriviaPage
using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class TriviaPage : Page
{
    public TriviaViewModel ViewModel { get; }

    public TriviaPage(TriviaViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}
