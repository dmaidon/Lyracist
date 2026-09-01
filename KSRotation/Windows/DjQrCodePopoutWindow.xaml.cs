// Created on Aug 17, 2026 @ 11:10:00 -> Add code-behind for DjQrCodePopoutWindow
using KSRotation.ViewModels;
using System;
using System.Windows;

namespace KSRotation.Windows
{
    public partial class DjQrCodePopoutWindow : Window
    {
        public DjQrCodePopoutWindow()
        {
            InitializeComponent();
        }

        private void OnCopyUrlClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && !string.IsNullOrWhiteSpace(vm.DjConnectionUrl))
            {
                try
                {
                    System.Windows.Clipboard.SetText(vm.DjConnectionUrl);
                }
                catch (Exception ex)
                {
                    KSRotation.Services.LoggerService.LogError("Failed to copy DJ URL", ex);
                }
            }
        }

        private void OnCopyPinClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && !string.IsNullOrWhiteSpace(vm.DjPin))
            {
                try
                {
                    System.Windows.Clipboard.SetText(vm.DjPin);
                }
                catch (Exception ex)
                {
                    KSRotation.Services.LoggerService.LogError("Failed to copy DJ PIN", ex);
                }
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
