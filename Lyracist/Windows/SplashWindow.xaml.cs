using System.Windows;
using Lyracist.ViewModels;

namespace Lyracist.Windows;

public partial class SplashWindow : Window
{
    private readonly SplashViewModel _vm;

    public SplashWindow(SplashViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
    }

    public void UpdateStatus(string text)
    {
        _vm.Status = text;
    }
}
