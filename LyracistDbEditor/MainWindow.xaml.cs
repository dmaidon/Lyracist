using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LyracistDbEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Stop background scans/renames so they don't touch the dispatcher during shutdown.
            (DataContext as MainViewModel)?.CancelAllOperations();
            base.OnClosing(e);
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                textBox.Dispatcher.BeginInvoke(new Action(() => textBox.SelectAll()));
            }
        }

        private void OnBrowseRenameFolderClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder Containing Karaoke Files to Rename"
            };
            if (dialog.ShowDialog() == true)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.RenameFolderPath = dialog.FolderName;
                }
            }
        }
    }

    // ==========================================
    // VALUE CONVERTERS USED BY XAML BINDINGS
    // ==========================================

    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }
            return true;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}