// Created on Aug 1, 2026 @ 15:10:00 -> Add CastingSettingsPageViewModel class
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using Lyracist.Shared;

namespace Lyracist.ViewModels
{
    public class CastingSettingsPageViewModel : BaseViewModel
    {
        public CastRotationViewModel CastRotation { get; }
        public CastStatusViewModel CastStatus { get; }
        public MultiTvSelectorViewModel MultiTvSelector { get; }
        public BrowserCastDiscoveryService BrowserDiscovery { get; }

        public string StatusText => CastStatus.StatusText;

        public ObservableCollection<DisplayTarget> Targets => CastRotation.Targets;

        public DisplayTarget SelectedTarget
        {
            get => CastRotation.SelectedTarget;
            set
            {
                CastRotation.SelectedTarget = value;
                OnPropertyChanged();
            }
        }

        public IAsyncRelayCommand CastCommand => CastRotation.CastCommand;
        public IAsyncRelayCommand StopCommand => CastRotation.StopCommand;

        public IAsyncRelayCommand RefreshCommand => MultiTvSelector.RefreshCommand;
        public ObservableCollection<ChromecastDevice> Devices => MultiTvSelector.Devices;

        public ChromecastDevice? SelectedDevice
        {
            get => MultiTvSelector.SelectedDevice;
            set
            {
                MultiTvSelector.SelectedDevice = value;
                OnPropertyChanged();
            }
        }

        public IAsyncRelayCommand CastToDeviceCommand => MultiTvSelector.CastToDeviceCommand;

        public ObservableCollection<BrowserCastClient> BrowserClients => BrowserDiscovery.Clients;

        public CastingSettingsPageViewModel(
            CastRotationViewModel castRotation,
            CastStatusViewModel castStatus,
            MultiTvSelectorViewModel multiTvSelector,
            BrowserCastDiscoveryService browserDiscovery)
        {
            CastRotation = castRotation;
            CastStatus = castStatus;
            MultiTvSelector = multiTvSelector;
            BrowserDiscovery = browserDiscovery;

            // Start BrowserCast discovery background loop
            _ = BrowserDiscovery.ListenAsync();

            // Forward property changes from sub-viewmodels
            CastStatus.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CastStatus.StatusText))
                {
                    OnPropertyChanged(nameof(StatusText));
                }
            };

            CastRotation.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CastRotation.SelectedTarget))
                {
                    OnPropertyChanged(nameof(SelectedTarget));
                }
            };

            MultiTvSelector.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MultiTvSelector.SelectedDevice))
                {
                    OnPropertyChanged(nameof(SelectedDevice));
                }
            };
        }
    }
}
