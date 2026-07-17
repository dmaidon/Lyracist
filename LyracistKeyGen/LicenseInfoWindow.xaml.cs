// Created on Jul 17, 2026 @ 10:04:00 -> Code-behind for LicenseInfoWindow to display record details
using System;
using System.Windows;

namespace LyracistKeyGen
{
    public partial class LicenseInfoWindow : Window
    {
        private readonly LicenseRecord _record;

        public LicenseInfoWindow(Window owner, LicenseRecord record)
        {
            InitializeComponent();
            Owner = owner;
            _record = record;

            TxtId.Text = record.Id.ToString();
            TxtFirstName.Text = record.FirstName;
            TxtLastName.Text = record.LastName;
            TxtStageName.Text = record.StageName;
            TxtEmail.Text = record.Email;
            TxtCreatedAt.Text = record.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            TxtLicenseKey.Text = record.LicenseKey;

            Title = $"Registration Info - {record.FirstName} {record.LastName}";
        }

        private void BtnCopyKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_record.LicenseKey);
                MessageBox.Show("License key copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
