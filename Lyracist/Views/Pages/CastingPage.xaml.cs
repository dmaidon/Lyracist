// Edited on Aug 1, 2026 @ 15:25:00 -> Update to CastingSettingsPageViewModel DataContext
using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages
{
    public partial class CastingPage : Page
    {
        public CastingSettingsPageViewModel ViewModel { get; }

        public CastingPage(CastingSettingsPageViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
