// Created on Nov 28, 2026 @ 10:00:00 -> Code-behind for RequestSlipsWindow.xaml Printable Branded Paper Request Slips popup
using System;
using System.Diagnostics;
using System.Windows;
using Lyracist.Shared;
using MessageBox = System.Windows.MessageBox;

namespace Lyracist.Windows;

public partial class RequestSlipsWindow : Window
{
    public RequestSlipsWindow(string defaultVenue, string defaultDj, string? portalUrl)
    {
        InitializeComponent();

        VenueTextBox.Text = string.IsNullOrWhiteSpace(defaultVenue) ? "Karaoke Night" : defaultVenue;
        DjTextBox.Text = string.IsNullOrWhiteSpace(defaultDj) ? "Guest DJ" : defaultDj;
        PortalUrlTextBox.Text = portalUrl ?? string.Empty;
        IncludeQrCheckBox.IsChecked = !string.IsNullOrWhiteSpace(portalUrl);

        Loaded += (_, _) =>
        {
            Topmost = true;
            Activate();
            VenueTextBox.Focus();
            VenueTextBox.SelectAll();
        };
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void GeneratePdfButton_Click(object sender, RoutedEventArgs e)
    {
        int slips = LayoutComboBox.SelectedIndex == 0 ? 8 : (LayoutComboBox.SelectedIndex == 2 ? 12 : 10);
        try
        {
            string pdfPath = PromoPrintUtility.GeneratePrintableRequestSlipsPdf(
                VenueTextBox.Text,
                DjTextBox.Text,
                PortalUrlTextBox.Text,
                slips,
                IncludeQrCheckBox.IsChecked == true);

            Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to generate request slips PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
