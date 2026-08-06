// Created on Aug 6, 2026 @ 07:01:27 -> Split license registration settings out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Core.Helpers;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    // Registration settings
    [ObservableProperty]
    private string _regFirstName = AppSettings.RegFirstName;

    [ObservableProperty]
    private string _regLastName = AppSettings.RegLastName;

    [ObservableProperty]
    private string _regStageName = AppSettings.RegStageName;

    [ObservableProperty]
    private string _regEmail = AppSettings.RegEmail;

    [ObservableProperty]
    private string _regLicenseKey = AppSettings.RegLicenseKey;

    [ObservableProperty]
    private bool _isRegistered = AppSettings.IsRegistered;

    [ObservableProperty]
    private bool? _registrationStatus = null;

    partial void OnRegFirstNameChanged(string value) { AppSettings.RegFirstName = value; ValidateRegistration(); }
    partial void OnRegLastNameChanged(string value) { AppSettings.RegLastName = value; ValidateRegistration(); }
    partial void OnRegStageNameChanged(string value) { AppSettings.RegStageName = value; ValidateRegistration(); }
    partial void OnRegEmailChanged(string value) { AppSettings.RegEmail = value; ValidateRegistration(); }
    partial void OnRegLicenseKeyChanged(string value) { AppSettings.RegLicenseKey = value; ValidateRegistration(); }

    private void ValidateRegistration()
    {
        if (string.IsNullOrWhiteSpace(RegLicenseKey))
        {
            RegistrationStatus = null;
            IsRegistered = false;
        }
        else
        {
            bool isValid = LicenseValidator.ValidateKey(RegFirstName, RegLastName, RegStageName, RegEmail, RegLicenseKey);
            RegistrationStatus = isValid;
            IsRegistered = isValid;
        }
    }
}
