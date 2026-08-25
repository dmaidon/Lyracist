// Edited on Aug 25, 2026 @ 06:35:00 -> Clean up whitespace (RCS1037)
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace LyracistKeyGen
{
    public partial class MainWindow : Window
    {
        private SmtpSettings _smtpSettings = new();

        public MainWindow()
        {
            InitializeComponent();

            // Initialize Database
            KeyGenDatabase.Initialize();

            // Load Settings
            LoadSmtpSettings();

            // Refresh history view initially
            RefreshHistoryGrid();
        }

        private void LoadSmtpSettings()
        {
            _smtpSettings = EmailSender.LoadSettings();
            TxtSmtpHost.Text = _smtpSettings.SmtpHost;
            TxtSmtpPort.Text = _smtpSettings.SmtpPort.ToString();
            TxtFromAddress.Text = _smtpSettings.FromAddress;
            TxtSmtpUser.Text = _smtpSettings.Username;
            TxtSmtpPassword.Password = _smtpSettings.Password;
            ChkEnableSsl.IsChecked = _smtpSettings.EnableSsl;
        }

        private SmtpSettings GetSettingsFromUi()
        {
            _ = int.TryParse(TxtSmtpPort.Text, out int port);
            if (port <= 0) port = 587;

            return new SmtpSettings
            {
                SmtpHost = TxtSmtpHost.Text,
                SmtpPort = port,
                FromAddress = TxtFromAddress.Text,
                Username = TxtSmtpUser.Text,
                Password = TxtSmtpPassword.Password,
                EnableSsl = ChkEnableSsl.IsChecked == true
            };
        }

        private void BtnSaveSmtp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _smtpSettings = GetSettingsFromUi();
                EmailSender.SaveSettings(_smtpSettings);
                MessageBox.Show("SMTP settings saved successfully!", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save SMTP settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnTestSmtp_Click(object sender, RoutedEventArgs e)
        {
            string recipient = TxtTestRecipient.Text;
            if (string.IsNullOrWhiteSpace(recipient) || !recipient.Contains('@'))
            {
                MessageBox.Show("Please enter a valid recipient email address.", "Invalid Email", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var settings = GetSettingsFromUi();
                EmailSender.TestConnection(settings, recipient);
                MessageBox.Show("Test email sent successfully! Please check your inbox.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SMTP connection test failed: {ex.Message}", "Connection Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            string firstName = TxtFirstName.Text;
            string lastName = TxtLastName.Text;
            string stageName = TxtStageName.Text;
            string email = TxtEmail.Text;

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("First Name, Last Name, and Email Address are required to generate a valid license key.", "Missing Fields", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!email.Contains('@'))
            {
                MessageBox.Show("Please enter a valid email address.", "Invalid Email", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Generate
                string key = LicenseManager.GenerateKey(firstName, lastName, stageName, email);
                TxtGeneratedKey.Text = key;
                BtnEmailKey.IsEnabled = true;

                // Save to database
                KeyGenDatabase.SaveLicense(firstName, lastName, stageName, email, key);

                // Auto-Email
                if (ChkAutoSendEmail.IsChecked == true)
                {
                    try
                    {
                        var settings = GetSettingsFromUi();
                        EmailSender.SendLicenseKey(settings, email, $"{firstName} {lastName}", key);
                    }
                    catch (Exception mailEx)
                    {
                        MessageBox.Show($"License key generated and saved to database, but email failed: {mailEx.Message}\n\nKey: {key}", "Email Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    // Copy to clipboard
                    Clipboard.SetText(key);
                }

                // Refresh history grid
                RefreshHistoryGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate license: {ex.Message}", "Generation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCopyKey_Click(object sender, RoutedEventArgs e)
        {
            string key = TxtGeneratedKey.Text;
            if (key == "XXXXX-XXXXX-XXXXX") return;

            Clipboard.SetText(key);
            MessageBox.Show("License key copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnEmailKey_Click(object sender, RoutedEventArgs e)
        {
            string key = TxtGeneratedKey.Text;
            string firstName = TxtFirstName.Text;
            string lastName = TxtLastName.Text;
            string email = TxtEmail.Text;

            if (key == "XXXXX-XXXXX-XXXXX" || string.IsNullOrWhiteSpace(email)) return;

            try
            {
                var settings = GetSettingsFromUi();
                EmailSender.SendLicenseKey(settings, email, $"{firstName} {lastName}", key);
                MessageBox.Show($"License key successfully sent to '{email}'!", "Email Sent", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send email: {ex.Message}", "Email Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshHistoryGrid()
        {
            List<LicenseRecord> history = KeyGenDatabase.GetHistory();
            GridHistory.ItemsSource = history;
        }

        private void BtnRefreshHistory_Click(object sender, RoutedEventArgs e)
        {
            RefreshHistoryGrid();
        }

        private void BtnCopySelectedHistory_Click(object sender, RoutedEventArgs e)
        {
            if (GridHistory.SelectedItem is LicenseRecord record)
            {
                Clipboard.SetText(record.LicenseKey);
                MessageBox.Show($"License key for '{record.FirstName} {record.LastName}' copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Please select a license record from the history list first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TabHistory_Selected(object sender, RoutedEventArgs e)
        {
            RefreshHistoryGrid();
        }

        private void MenuResendEmail_Click(object sender, RoutedEventArgs e)
        {
            if (GridHistory.SelectedItem is LicenseRecord record)
            {
                try
                {
                    var settings = GetSettingsFromUi();
                    EmailSender.SendLicenseKey(settings, record.Email, $"{record.FirstName} {record.LastName}", record.LicenseKey);
                    MessageBox.Show($"License key successfully resent to '{record.Email}'!", "Email Sent", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to resend email: {ex.Message}", "Email Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuCopyKey_Click(object sender, RoutedEventArgs e)
        {
            if (GridHistory.SelectedItem is LicenseRecord record)
            {
                try
                {
                    Clipboard.SetText(record.LicenseKey);
                    MessageBox.Show($"License key for '{record.FirstName} {record.LastName}' copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuViewInfo_Click(object sender, RoutedEventArgs e)
        {
            if (GridHistory.SelectedItem is LicenseRecord record)
            {
                var infoWindow = new LicenseInfoWindow(this, record);
                infoWindow.ShowDialog();
            }
        }

        private void InputFields_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Reset generated output text when details are edited
            if (TxtGeneratedKey != null && TxtGeneratedKey.Text != "XXXXX-XXXXX-XXXXX")
            {
                TxtGeneratedKey.Text = "XXXXX-XXXXX-XXXXX";
                BtnEmailKey.IsEnabled = false;
            }
        }
    }
}