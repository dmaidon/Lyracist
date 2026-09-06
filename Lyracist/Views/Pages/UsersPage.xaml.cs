// Created on Sep 6, 2026 @ 08:53:00 -> Code-behind for UsersPage
using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class UsersPage : Page
{
    public UsersViewModel ViewModel { get; }

    public UsersPage(UsersViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }
}
