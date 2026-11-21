// Edited on Nov 21, 2026 @ 08:15:10 -> Attach right-click previously sung songs menu to Song Title and Notes inputs
using System.Windows;
using Lyracist.Services.Database;
using Lyracist.Shared;
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

            SingerHistoryContextMenuHelper.Attach(
                SongTitleTextBox,
                () => ViewModel.Name,
                ApplySelectedHistoricalSong);

            SingerHistoryContextMenuHelper.Attach(
                NotesTextBox,
                () => ViewModel.Name,
                ApplySelectedHistoricalSong);
        }

        private void ApplySelectedHistoricalSong(SingerHistoryEntry selected)
        {
            ViewModel.SongTitle = selected.SongTitle;
            ViewModel.Artist = selected.Artist;
            if (!string.IsNullOrWhiteSpace(selected.Key) && selected.Key != "0")
            {
                ViewModel.Key = selected.Key;
            }
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
