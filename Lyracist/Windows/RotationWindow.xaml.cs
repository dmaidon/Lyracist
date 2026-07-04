using System.ComponentModel;
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

    protected override void OnClosing(CancelEventArgs e)
    {
        // Registered as a singleton: a closed WPF window can never be shown
        // again, so hide instead and let app shutdown tear it down.
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    private void OnToggleFullscreen(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnHide(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
