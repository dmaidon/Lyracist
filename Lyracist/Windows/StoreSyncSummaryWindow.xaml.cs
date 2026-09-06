// Created on Sep 6, 2026 @ 12:05:00 -> Code-behind for Store Sync summary modal dialog
using System.Windows;
using Lyracist.Services.Store;

namespace Lyracist.Windows;

public partial class StoreSyncSummaryWindow : Window
{
    public StoreSyncSummaryWindow(StoreSyncResult result)
    {
        InitializeComponent();
        DataContext = result;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
