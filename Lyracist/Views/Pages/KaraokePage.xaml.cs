// Edited on Nov 21, 2026 @ 08:18:30 -> Attach right-click previously sung songs menu to KaraokePage song entry text boxes
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lyracist.Models;
using Lyracist.Services.Database;
using Lyracist.Shared;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class KaraokePage : Page
{
    public KaraokeViewModel ViewModel { get; }

    public KaraokePage(KaraokeViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (s, e) => ViewModel.OnNavigatedTo();

        SingerHistoryContextMenuHelper.Attach(
            SongSearchTextBox,
            ResolveActiveSingerName,
            selected =>
            {
                ViewModel.SearchQuery = selected.SongTitle;
                ViewModel.NewSingerSongTitle = selected.SongTitle;
                ViewModel.NewSingerArtist = selected.Artist;
                if (!string.IsNullOrWhiteSpace(selected.Key) && selected.Key != "0")
                {
                    ViewModel.NewSingerKey = selected.Key;
                }
                ViewModel.SelectedHistoryEntry = selected;
                ViewModel.SearchDatabase();
            });

        SingerHistoryContextMenuHelper.Attach(
            CustomExternalTitleTextBox,
            ResolveActiveSingerName,
            selected =>
            {
                ViewModel.CustomExternalTitle = selected.SongTitle;
                ViewModel.CustomExternalArtist = selected.Artist;
                if (!string.IsNullOrWhiteSpace(selected.Key) && selected.Key != "0")
                {
                    ViewModel.NewSingerKey = selected.Key;
                }
            });

        SingerHistoryContextMenuHelper.Attach(
            NewSingerSongTitleTextBox,
            ResolveActiveSingerName,
            ApplySelectedHistoricalSongToAssignment);

        SingerHistoryContextMenuHelper.Attach(
            NewSingerNotesTextBox,
            ResolveActiveSingerName,
            ApplySelectedHistoricalSongToAssignment);
    }

    private string? ResolveActiveSingerName()
    {
        return !string.IsNullOrWhiteSpace(ViewModel.NewSingerName)
            ? ViewModel.NewSingerName
            : ViewModel.Rotation.SelectedSinger?.Name;
    }

    private void ApplySelectedHistoricalSongToAssignment(SingerHistoryEntry selected)
    {
        ViewModel.NewSingerSongTitle = selected.SongTitle;
        ViewModel.NewSingerArtist = selected.Artist;
        if (!string.IsNullOrWhiteSpace(selected.Key) && selected.Key != "0")
        {
            ViewModel.NewSingerKey = selected.Key;
        }
        ViewModel.SelectedHistoryEntry = selected;
    }

    private void PendingQueueSongTextBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        SingerHistoryContextMenuHelper.HandlePreviewMouseRightButtonDown(
            textBox,
            () => ViewModel.Rotation.SelectedSinger?.Name,
            selected => ApplySelectedHistoricalSongToPendingSong(textBox, selected));
    }

    private void PendingQueueSongTextBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        SingerHistoryContextMenuHelper.HandleContextMenuOpening(
            textBox,
            e,
            () => ViewModel.Rotation.SelectedSinger?.Name,
            selected => ApplySelectedHistoricalSongToPendingSong(textBox, selected));
    }

    private static void ApplySelectedHistoricalSongToPendingSong(System.Windows.Controls.TextBox textBox, SingerHistoryEntry selected)
    {
        textBox.Text = selected.SongTitle;
        if (textBox.DataContext is PendingSong pending)
        {
            pending.Title = selected.SongTitle;
            pending.Artist = selected.Artist;
            if (!string.IsNullOrWhiteSpace(selected.Key) && selected.Key != "0")
            {
                pending.Key = selected.Key;
            }
        }
    }

    private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            if (item.IsSelected)
            {
                if (ItemsControl.ItemsControlFromItemContainer(item) is System.Windows.Controls.ListBox listBox)
                {
                    listBox.SelectedItem = null;
                    e.Handled = true;
                }
            }
        }
    }

    private void OnSongDoubleClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is KaraokeViewModel vm && vm.SelectedSong != null)
        {
            if (vm.SelectedSearchTabIndex == 1 || !vm.SelectedSong.IsKaraoke)
            {
                vm.PlaySongCommand.Execute(vm.SelectedSong);
            }
            else
            {
                vm.AddSongToRotationCommand.Execute(vm.SelectedSong);
            }
        }
    }

    private void OnRotationQueueDoubleClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is KaraokeViewModel vm)
        {
            vm.LoadAndPlaySelectedPerformerCommand.Execute(null);
        }
    }

    private void OnHistoryDoubleClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is KaraokeViewModel vm && vm.SelectedHistoryEntry != null)
        {
            vm.AddHistorySongToRotationCommand.Execute(vm.SelectedHistoryEntry);
        }
    }

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && DataContext is KaraokeViewModel vm)
        {
            vm.SearchDatabaseCommand.Execute(null);
        }
    }
}
