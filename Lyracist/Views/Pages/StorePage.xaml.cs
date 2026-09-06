// Edited on Sep 6, 2026 @ 12:41:00 -> Route search box Enter to SearchPreferredProviderCommand
using System.Windows.Controls;
using System.Windows.Input;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class StorePage : Page
{
    public StoreViewModel ViewModel { get; }
    public AnalyticsViewModel AnalyticsViewModel => ViewModel.Analytics;

    public StorePage(StoreViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            await ViewModel.Analytics.RefreshAnalyticsAsync();
        };
    }

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            ViewModel.SearchPreferredProviderCommand.Execute(null);
        }
    }
}
