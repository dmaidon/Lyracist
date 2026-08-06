// Created on Aug 6, 2026 @ 07:01:27 -> Split tablet server + KSRotation sync settings out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    // Tablet Server
    [ObservableProperty]
    private int _tabletPort = AppSettings.TabletPort;

    partial void OnTabletPortChanged(int value)
    {
        AppSettings.TabletPort = value;
        _rotationWindowVm.RefreshQrCode();
    }

    [ObservableProperty]
    private string _tabletStatus = "Running";

    // KSRotation Sync
    [ObservableProperty]
    private bool _kSRotationSyncEnabled = AppSettings.KSRotationSyncEnabled;

    [ObservableProperty]
    private string _kSRotationIpAddress = AppSettings.KSRotationIpAddress;

    [ObservableProperty]
    private int _kSRotationPort = AppSettings.KSRotationPort;

    partial void OnKSRotationSyncEnabledChanged(bool value)
    {
        AppSettings.KSRotationSyncEnabled = value;
        _ksRotationSync.TriggerSettingsReloadAsync();
    }

    partial void OnKSRotationIpAddressChanged(string value)
    {
        AppSettings.KSRotationIpAddress = value;
        _ksRotationSync.TriggerSettingsReloadAsync();
    }

    partial void OnKSRotationPortChanged(int value)
    {
        AppSettings.KSRotationPort = value;
        _ksRotationSync.TriggerSettingsReloadAsync();
    }

    [RelayCommand]
    private async Task StartTabletServer()
    {
        await _tablet.StartAsync();
        TabletStatus = "Running";
    }

    [RelayCommand]
    private async Task StopTabletServer()
    {
        await _tablet.StopAsync();
        TabletStatus = "Stopped";
    }
}
