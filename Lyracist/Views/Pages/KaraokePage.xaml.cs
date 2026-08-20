// Edited on Aug 20, 2026 @ 09:58:30 -> Add StartSongButton_Click and SkipSingerButton_Click event handlers for DJ Control Panel
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

    private async void StartSongButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.StartSongNow();
    }

    private void SkipSingerButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SkipSinger();
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
}
