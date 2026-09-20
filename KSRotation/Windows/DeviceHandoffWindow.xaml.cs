// Edited on Sep 20, 2026 @ 07:12:00 -> Validate DJ PIN and auto-focus PinTextBox when selecting discovered peer
using KSRotation.Models;
using KSRotation.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using WpfClipboard = System.Windows.Clipboard;
using WpfMessageBox = System.Windows.MessageBox;
using WpfBrushes = System.Windows.Media.Brushes;

namespace KSRotation.Windows
{
    public partial class DeviceHandoffWindow : Window
    {
        public DeviceHandoffWindow()
        {
            InitializeComponent();
        }

        private void OnTabChanged(object sender, RoutedEventArgs e)
        {
            if (SendPanel == null || ReceivePanel == null) return;

            if (TabSendRadio.IsChecked == true)
            {
                SendPanel.Visibility = Visibility.Visible;
                ReceivePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                SendPanel.Visibility = Visibility.Collapsed;
                ReceivePanel.Visibility = Visibility.Visible;
            }
        }

        private void OnCopyUrlClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && !string.IsNullOrWhiteSpace(vm.HandoffConnectionUrl))
            {
                try
                {
                    WpfClipboard.SetText(vm.HandoffConnectionUrl);
                }
                catch (Exception ex)
                {
                    Services.LoggerService.LogError("Failed to copy Handoff URL", ex);
                }
            }
        }

        private void OnCopyPinClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && !string.IsNullOrWhiteSpace(vm.DjPin))
            {
                try
                {
                    WpfClipboard.SetText(vm.DjPin);
                }
                catch (Exception ex)
                {
                    Services.LoggerService.LogError("Failed to copy DJ PIN", ex);
                }
            }
        }

        private async void OnScanNetworkClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                ScanProgressBar.Visibility = Visibility.Visible;
                ReceiveStatusLabel.Text = "Scanning local Wi-Fi for KSRotation devices...";
                ReceiveStatusLabel.Foreground = WpfBrushes.SlateGray;

                try
                {
                    await vm.DiscoverPeersOnLanAsync();
                    if (vm.DiscoveredPeers.Count > 0)
                    {
                        ReceiveStatusLabel.Text = $"Found {vm.DiscoveredPeers.Count} device(s) on Wi-Fi. Select one to import.";
                        ReceiveStatusLabel.Foreground = WpfBrushes.LightGreen;
                    }
                    else
                    {
                        ReceiveStatusLabel.Text = "No devices detected automatically. Enter Host IP manually.";
                        ReceiveStatusLabel.Foreground = WpfBrushes.Orange;
                    }
                }
                catch (Exception ex)
                {
                    ReceiveStatusLabel.Text = $"Scan failed: {ex.Message}";
                    ReceiveStatusLabel.Foreground = WpfBrushes.Crimson;
                }
                finally
                {
                    ScanProgressBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void OnPeerSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PeersListBox.SelectedItem is DiscoveredPeer peer)
            {
                HostIpTextBox.Text = peer.Host;
                PortTextBox.Text = peer.Port.ToString();
                PinTextBox.Focus();
                PinTextBox.SelectAll();
            }
        }

        private async void OnPullSessionClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            string host = HostIpTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(host) || host == "192.168.1.")
            {
                WpfMessageBox.Show("Please enter a valid Host IP address.", "Invalid Host", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(PortTextBox.Text.Trim(), out int port))
            {
                port = 5000;
            }

            string pin = PinTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(pin))
            {
                WpfMessageBox.Show("Please enter the 4-digit DJ PIN displayed on the source device.", "DJ PIN Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                PinTextBox.Focus();
                return;
            }

            var confirm = WpfMessageBox.Show(
                $"Importing session from {host}:{port} will replace the current singer rotation and history on this device.\n\nDo you want to proceed?",
                "Confirm Session Transfer",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            ReceiveStatusLabel.Text = "Pulling session from device...";
            ReceiveStatusLabel.Foreground = WpfBrushes.SlateGray;

            var (success, error) = await vm.PullSessionFromHostAsync(host, port, pin);
            if (success)
            {
                ReceiveStatusLabel.Text = "Session imported successfully! The rotation is now active.";
                ReceiveStatusLabel.Foreground = WpfBrushes.LightGreen;
                WpfMessageBox.Show("Session transferred successfully! You may now continue the show on this machine.", "Handoff Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            else
            {
                ReceiveStatusLabel.Text = $"Failed to transfer session: {error}";
                ReceiveStatusLabel.Foreground = WpfBrushes.Crimson;
                WpfMessageBox.Show($"Could not import session:\n{error}", "Transfer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
