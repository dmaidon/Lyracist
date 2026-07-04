using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class RotationPage : Page
{
    public RotationViewModel ViewModel { get; }

    public RotationPage(RotationViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }
}
