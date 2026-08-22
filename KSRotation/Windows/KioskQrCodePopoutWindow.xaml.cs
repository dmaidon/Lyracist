// Created on Aug 21, 2026 @ 17:23:00 -> Add code-behind for KioskQrCodePopoutWindow
using KSRotation.ViewModels;
using System;
using System.Windows;

namespace KSRotation.Windows
{
    public partial class KioskQrCodePopoutWindow : Window
    {
        public KioskQrCodePopoutWindow()
        {
            InitializeComponent();
        }

        private void OnCopyUrlClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && !string.IsNullOrWhiteSpace(vm.KioskConnectionUrl))
            {
                try
                {
                    System.Windows.Clipboard.SetText(vm.KioskConnectionUrl);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to copy Kiosk URL: {ex.Message}");
                }
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
