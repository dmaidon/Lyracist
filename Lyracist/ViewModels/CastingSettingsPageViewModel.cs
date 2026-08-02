// Edited on Aug 2, 2026 @ 09:16:00 -> Expose ConnectedPerformers list from SignalR lyrics hub
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using System.Linq;
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

        public ObservableCollection<Lyracist.Services.Tablet.PerformerClient> ConnectedPerformers { get; } = new();

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

            // Populate initially
            UpdateConnectedPerformers();

            // Subscribe to connected clients change event
            Lyracist.Services.Tablet.LyricsHub.ClientsChanged += UpdateConnectedPerformers;

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

        private void UpdateConnectedPerformers()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(UpdateConnectedPerformers);
                return;
            }

            ConnectedPerformers.Clear();
            foreach (var client in Lyracist.Services.Tablet.LyricsHub.ActiveClients.Values.OrderBy(c => c.SingerName))
            {
                ConnectedPerformers.Add(client);
            }
        }
    }
}
