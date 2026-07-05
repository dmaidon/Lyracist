using System.Windows;
using Lyracist.ViewModels;

namespace Lyracist.Windows;

public partial class AboutWindow : Window
{
    public AboutViewModel ViewModel { get; }

    public AboutWindow(AboutViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnDragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}