// Edited on Sep 17, 2026 @ 12:12:45 -> Fix XML doc comment formatting in MauiLocationService
using System;
using System.Threading;
using System.Threading.Tasks;
using Lyracist.Shared;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace KSRotation.Maui.Services;

/// <summary>
/// Device location service for MAUI (Android and Windows) using hardware GPS and platform sensors.
/// </summary>
public class MauiLocationService : ILocationService
{
    public static MauiLocationService Instance { get; } = new();

    public bool IsSupported => true;

    public async Task<(double Latitude, double Longitude)?> GetCurrentCoordinatesAsync(CancellationToken ct = default)
    {
        try
        {

            // Check / request location permission
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
            {
                return null;
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
            var location = await Geolocation.Default.GetLocationAsync(request, ct);

            if (location != null)
            {
                return (location.Latitude, location.Longitude);
            }

            // Fallback to last known location if quick fix was unavailable
            var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
            if (lastKnown != null)
            {
                return (lastKnown.Latitude, lastKnown.Longitude);
            }
        }
        catch
        {
            // Suppress platform errors (e.g. location disabled in device settings)
        }

        return null;
    }
}
