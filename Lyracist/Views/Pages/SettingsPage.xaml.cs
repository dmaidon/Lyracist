using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var textBox = sender as System.Windows.Controls.TextBox;
        if (textBox != null)
        {
            string keyStr = e.Key == System.Windows.Input.Key.System ? e.SystemKey.ToString() : e.Key.ToString();
            textBox.Text = keyStr;
            // Force update of binding source
            var binding = textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
            binding?.UpdateSource();
        }
    }
}
