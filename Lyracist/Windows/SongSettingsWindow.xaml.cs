using System.Windows;
using Lyracist.ViewModels;

namespace Lyracist.Windows
{
    public partial class SongSettingsWindow : Window
    {
        public SongSettingsViewModel ViewModel { get; }

        public SongSettingsWindow(SongSettingsViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveCommand.Execute(null);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
