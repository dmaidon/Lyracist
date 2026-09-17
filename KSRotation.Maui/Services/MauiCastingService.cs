// Created on Sep 17, 2026 @ 10:37:00 -> Add MauiCastingService for Chromecast web-casting
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Shared;
using Microsoft.Maui.ApplicationModel;

namespace KSRotation.Maui.Services
{
    public partial class MauiCastingService : ObservableObject
    {
        private static readonly Lazy<MauiCastingService> _instance = new(() => new MauiCastingService());
        public static MauiCastingService Instance => _instance.Value;

        private readonly ChromecastDiscoveryService _discovery = new();
        private readonly ChromecastSender _sender = new();

        public ObservableCollection<ChromecastDevice> DiscoveredDevices { get; } = [];

        [ObservableProperty]
        public partial bool IsDiscovering { get; set; }

        [ObservableProperty]
        public partial bool IsCasting { get; set; }

        [ObservableProperty]
        public partial ChromecastDevice? ActiveDevice { get; set; }

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = string.Empty;

        public async Task DiscoverDevicesAsync()
        {
            if (IsDiscovering) return;

            IsDiscovering = true;
            StatusMessage = "Scanning for Google Cast devices on Wi-Fi...";

            try
            {
                var devices = await _discovery.DiscoverAsync();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    DiscoveredDevices.Clear();
                    foreach (var device in devices)
                    {
                        DiscoveredDevices.Add(device);
                    }
                    StatusMessage = DiscoveredDevices.Count > 0
                        ? $"Found {DiscoveredDevices.Count} Cast device(s)."
                        : "No Cast devices found on Wi-Fi.";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Discovery error: {ex.Message}";
                Globals.LogError("MauiCastingService", "DiscoverDevicesAsync", ex);
            }
            finally
            {
                IsDiscovering = false;
            }
        }

        public async Task<bool> CastBillboardAsync(ChromecastDevice device, string billboardUrl)
        {
            if (device == null) return false;

            StatusMessage = $"Connecting to {device.Name}...";

            try
            {
                if (IsCasting && ActiveDevice != null)
                {
                    await StopCastingAsync();
                }

                bool success = await _sender.CastWebUrlAsync(device, billboardUrl);
                if (success)
                {
                    ActiveDevice = device;
                    IsCasting = true;
                    StatusMessage = $"Casting billboard to {device.Name}";
                    return true;
                }
                else
                {
                    StatusMessage = $"Could not cast to {device.Name}.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Cast error: {ex.Message}";
                Globals.LogError("MauiCastingService", $"CastBillboardAsync({device.Name})", ex);
                return false;
            }
        }

        public async Task StopCastingAsync()
        {
            if (ActiveDevice != null)
            {
                try
                {
                    await _sender.StopCastingAsync(ActiveDevice);
                }
                catch (Exception ex)
                {
                    Globals.LogError("MauiCastingService", "StopCastingAsync", ex);
                }
            }

            ActiveDevice = null;
            IsCasting = false;
            StatusMessage = "Casting stopped.";
        }
    }
}
