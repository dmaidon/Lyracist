using System.Windows;
using Lyracist.ViewModels;

namespace Lyracist.Windows;

public partial class RotationWindow : Window
{
    public RotationWindow(RotationWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
