using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class PlaylistsPage : Page
{
    public PlaylistsViewModel ViewModel { get; }

    public PlaylistsPage(PlaylistsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }
}
