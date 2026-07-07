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
                var listBox = ItemsControl.ItemsControlFromItemContainer(item) as System.Windows.Controls.ListBox;
                if (listBox != null)
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
}
