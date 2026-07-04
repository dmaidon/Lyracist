using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class HelpPage : Page
{
    public HelpViewModel ViewModel { get; }

    public HelpPage(HelpViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }
}
