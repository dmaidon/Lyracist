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
}
