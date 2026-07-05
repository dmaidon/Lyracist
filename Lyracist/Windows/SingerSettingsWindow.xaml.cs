using System.Windows;
using Lyracist.ViewModels;

namespace Lyracist.Windows
{
    public partial class SingerSettingsWindow : Window
    {
        public SingerSettingsViewModel ViewModel { get; }

        public SingerSettingsWindow(SingerSettingsViewModel viewModel)
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
