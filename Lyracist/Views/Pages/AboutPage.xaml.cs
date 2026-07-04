using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class AboutPage : Page
{
    public AboutViewModel ViewModel { get; }

    public AboutPage(AboutViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }
}
