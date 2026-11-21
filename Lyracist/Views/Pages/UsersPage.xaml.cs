// Edited on Nov 21, 2026 @ 08:19:45 -> Attach right-click previously sung songs menu to EditHistorySongTitleTextBox
using System.Windows.Controls;
using Lyracist.Shared;
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

        SingerHistoryContextMenuHelper.Attach(
            EditHistorySongTitleTextBox,
            () => !string.IsNullOrWhiteSpace(ViewModel.EditName) ? ViewModel.EditName : ViewModel.SelectedSinger?.Name,
            selected =>
            {
                ViewModel.EditHistorySongTitle = selected.SongTitle;
                ViewModel.EditHistoryArtist = selected.Artist;
                if (!string.IsNullOrWhiteSpace(selected.Key) && selected.Key != "0")
                {
                    ViewModel.EditHistoryKey = selected.Key;
                }
            });
    }
}
