// Created on Nov 13, 2026 @ 16:08:15 -> Code-behind for SavedRotationsPage
using System.Windows;
using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class SavedRotationsPage : Page
{
    public SavedRotationsViewModel ViewModel { get; }

    public SavedRotationsPage(SavedRotationsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.RefreshSavedReportsAsync();
    }
}

