// Edited on Aug 1, 2026 @ 15:15:00 -> Add browser discovery and status integration
// Edited on Aug 1, 2026 @ 14:49:00 -> Add MultiTvSelector property
// Edited on Aug 1, 2026 @ 14:20:00 -> Add Chromecast device discovery properties and commands
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Services.Display;
using Lyracist.Shared;

namespace Lyracist.ViewModels
{
    public partial class CastRotationViewModel : BaseViewModel
    {
        private readonly IDisplayService _displayService;
        private readonly IChromecastDiscoveryService _chromecastDiscovery;
        private readonly ICastingService _casting;
        private readonly BrowserCastDiscoveryService _browserDiscovery;

        public MultiTvSelectorViewModel MultiTvSelector { get; }

        public string StatusText => !_casting.IsCasting ? "Not Casting" : $"Casting to {_casting.CurrentTarget}";

        public ObservableCollection<DisplayTarget> Targets { get; } =
            new ObservableCollection<DisplayTarget>
            {
                DisplayTarget.Monitor,
                DisplayTarget.Miracast,
                DisplayTarget.Chromecast,
                DisplayTarget.BrowserCast,
                DisplayTarget.AirPlay,
                DisplayTarget.WirelessHDMI
            };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsChromecastSelectionVisible))]
        private DisplayTarget _selectedTarget;

        public bool IsChromecastSelectionVisible => SelectedTarget == DisplayTarget.Chromecast;

        [ObservableProperty]
        private ObservableCollection<ChromecastDevice> _availableChromecasts = [];

        [ObservableProperty]
        private ChromecastDevice? _selectedChromecast;

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

        public IAsyncRelayCommand RefreshCommand => MultiTvSelector.RefreshCommand;
        public IAsyncRelayCommand CastToDeviceCommand => MultiTvSelector.CastToDeviceCommand;

        public ObservableCollection<BrowserCastClient> BrowserClients => _browserDiscovery.Clients;

        public IAsyncRelayCommand CastCommand { get; }
        public IAsyncRelayCommand StopCommand { get; }

        public CastRotationViewModel(
            IDisplayService displayService,
            IChromecastDiscoveryService chromecastDiscovery,
            ICastingService casting,
            MultiTvSelectorViewModel multiTvSelector,
            BrowserCastDiscoveryService browserDiscovery)
        {
            _displayService = displayService;
            _chromecastDiscovery = chromecastDiscovery;
            _casting = casting;
            MultiTvSelector = multiTvSelector;
            _browserDiscovery = browserDiscovery;

            // Start BrowserCast heartbeat listening loop
            _ = _browserDiscovery.ListenAsync();

            // Load initial target preference
            var preferences = _displayService.GetPreferences();
            _selectedTarget = preferences.RotationTarget;

            CastCommand = new AsyncRelayCommand(async () =>
            {
                await _displayService.StopRotationCasting();
                await _displayService.MoveRotationTo(SelectedTarget);
                OnPropertyChanged(nameof(StatusText));
            });

            StopCommand = new AsyncRelayCommand(async () =>
            {
                await _displayService.StopRotationCasting();
                OnPropertyChanged(nameof(StatusText));
            });
        }

        partial void OnSelectedTargetChanged(DisplayTarget value)
        {
            if (value == DisplayTarget.Chromecast)
            {
                _ = DiscoverChromecastsAsync();
            }
        }

        partial void OnSelectedChromecastChanged(ChromecastDevice? value)
        {
            _casting.SelectedDevice = value;
        }

        [RelayCommand]
        private async Task DiscoverChromecastsAsync()
        {
            try
            {
                AvailableChromecasts.Clear();
                var devices = await _chromecastDiscovery.DiscoverAsync();
                
                foreach (var device in devices)
                {
                    AvailableChromecasts.Add(device);
                }

                if (devices.Count > 0)
                {
                    SelectedChromecast = devices[0];
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
