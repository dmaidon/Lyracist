using System.Windows;
using Lyracist.ViewModels;

namespace Lyracist.Windows
{
    public partial class EditSingerWindow : Window
    {
        public EditSingerViewModel ViewModel { get; }

        public EditSingerWindow(EditSingerViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Save())
            {
                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
