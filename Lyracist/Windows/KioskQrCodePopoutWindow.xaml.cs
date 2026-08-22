// Created on Aug 21, 2026 @ 17:52:00 -> Code-behind for Lyracist Tablet Kiosk QR code popout window
using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Lyracist.ViewModels;

namespace Lyracist.Windows;

public partial class KioskQrCodePopoutWindow : Window
{
    public KioskQrCodePopoutWindow()
    {
        InitializeComponent();
        if (App.AppHost?.Services != null)
        {
            DataContext = App.AppHost.Services.GetService<KaraokeViewModel>();
        }
    }

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnCopyUrlClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is KaraokeViewModel vm && !string.IsNullOrWhiteSpace(vm.KioskUrl))
        {
            try
            {
                System.Windows.Clipboard.SetText(vm.KioskUrl);
            }
            catch { }
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
