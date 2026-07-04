using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class RequestsPage : Page
{
    public RequestsViewModel ViewModel { get; }

    public RequestsPage(RequestsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }
}
