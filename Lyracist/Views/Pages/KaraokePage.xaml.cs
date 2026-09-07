// Edited on Sep 6, 2026 @ 18:03:00 -> Add SearchBox_KeyDown to trigger SearchDatabaseCommand on Enter
using System.Windows;
using System.Windows.Controls;
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
            vm.AddSongToRotationCommand.Execute(vm.SelectedSong);
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
