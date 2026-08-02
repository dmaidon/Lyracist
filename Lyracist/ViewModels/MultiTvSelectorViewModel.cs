// Created on Aug 1, 2026 @ 14:45:00 -> Add MultiTvSelectorViewModel class
// Edited on Aug 1, 2026 -> Route CastToDeviceCommand through IDisplayService.MoveRotationTo
// instead of calling ICastingService directly. CastingService alone starts BrowserCastServer and
// sends the LOAD/rotation URL, but never shows or positions RotationWindow - only
// DisplayService.MoveRotationTo does that (off-screen, so it still renders while not visible
// locally). Without it the window is never loaded, so every captured frame comes back null and
// nothing ever reaches the TV.
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Services.Display;
using Lyracist.Shared;

namespace Lyracist.ViewModels
{
    public partial class MultiTvSelectorViewModel : BaseViewModel
    {
        private readonly IChromecastDiscoveryService _discovery;
        private readonly IDisplayService _displayService;

        public ObservableCollection<ChromecastDevice> Devices { get; } =
            new ObservableCollection<ChromecastDevice>();

        [ObservableProperty]
        private ChromecastDevice? _selectedDevice;

        public IAsyncRelayCommand RefreshCommand { get; }
        public IAsyncRelayCommand CastToDeviceCommand { get; }

        public MultiTvSelectorViewModel(
            IChromecastDiscoveryService discovery,
            IDisplayService displayService)
        {
            _discovery = discovery;
            _displayService = displayService;

            RefreshCommand = new AsyncRelayCommand(async () =>
            {
                Devices.Clear();
                var found = await _discovery.DiscoverAsync();
                foreach (var d in found)
                    Devices.Add(d);

                // Auto-select the first device found - otherwise the closed dropdown shows
                // blank even when devices were found, which reads as "nothing found" until
                // the user thinks to open it.
                if (SelectedDevice == null && Devices.Count > 0)
                {
                    SelectedDevice = Devices[0];
                }
            });

            CastToDeviceCommand = new AsyncRelayCommand(async () =>
            {
                if (SelectedDevice == null) return;

                await _displayService.MoveRotationTo(DisplayTarget.Chromecast, SelectedDevice);
            });
        }
    }
}
