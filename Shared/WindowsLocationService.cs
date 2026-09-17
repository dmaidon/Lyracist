// Edited on Sep 17, 2026 @ 12:05:00 -> Reflection-based AsTask lookup for WinRT Geolocator compatibility
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Lyracist.Shared;

/// <summary>
/// Location service for Windows desktop environments.
/// Queries Windows Location Services via WinRT Geolocator when available,
/// and supports receiving coordinates pushed from companion tablets (KSRotation.Maui).
/// </summary>
public class WindowsLocationService : ILocationService
{
    private static (double Latitude, double Longitude)? _syncedCoordinates;
    private static DateTime _syncedTimestamp = DateTime.MinValue;

    public static WindowsLocationService Instance { get; } = new();

    public bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>
    /// Updates the location coordinates pushed from a companion device (e.g. tablet running KSRotation.Maui).
    /// </summary>
    public static void SetSyncedCoordinates(double latitude, double longitude)
    {
        _syncedCoordinates = (latitude, longitude);
        _syncedTimestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Clears any cached synced coordinates.
    /// </summary>
    public static void ClearSyncedCoordinates()
    {
        _syncedCoordinates = null;
        _syncedTimestamp = DateTime.MinValue;
    }

    public async Task<(double Latitude, double Longitude)?> GetCurrentCoordinatesAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        // 1. If companion tablet has synced recent GPS coordinates within 4 hours, prioritize that high-accuracy fix
        if (_syncedCoordinates.HasValue && (DateTime.UtcNow - _syncedTimestamp).TotalHours < 4.0)
        {
            return _syncedCoordinates.Value;
        }

        // 2. Query Windows Location Services via WinRT Geolocator using reflection
        try
        {
            var coords = await QueryWindowsGeolocatorAsync(ct);
            if (coords.HasValue)
            {
                return coords.Value;
            }
        }
        catch
        {
            // Windows Location Services might be disabled by user or hardware lacks sensor
        }

        return _syncedCoordinates;
    }

    private static async Task<(double Latitude, double Longitude)?> QueryWindowsGeolocatorAsync(CancellationToken ct)
    {
        Type? geolocatorType = Type.GetType("Windows.Devices.Geolocation.Geolocator, Windows, ContentType=WindowsRuntime") ??
                              Type.GetType("Windows.Devices.Geolocation.Geolocator, Windows.Devices.Geolocation, ContentType=WindowsRuntime");

        if (geolocatorType == null)
        {
            return null;
        }

        object? geolocator = Activator.CreateInstance(geolocatorType);
        if (geolocator == null)
        {
            return null;
        }

        // Invoke GetGeopositionAsync()
        MethodInfo? getPosMethod = geolocatorType.GetMethod("GetGeopositionAsync", Type.EmptyTypes);
        if (getPosMethod == null)
        {
            return null;
        }

        object? asyncOp = getPosMethod.Invoke(geolocator, null);
        if (asyncOp == null)
        {
            return null;
        }

        // Convert Windows.Foundation.IAsyncOperation<Geoposition> to Task
        Type? winRtExtType = Type.GetType("System.WindowsRuntimeSystemExtensions, System.Runtime.WindowsRuntime")
                             ?? Type.GetType("System.WindowsRuntimeSystemExtensions, System.Runtime.InteropServices.WindowsRuntime");
        MethodInfo? asTaskMethod = winRtExtType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "AsTask" && m.GetParameters().Length == 1);
        Task? task = null;
        if (asTaskMethod != null)
        {
            if (asTaskMethod.IsGenericMethodDefinition)
            {
                Type[] genArgs = asyncOp.GetType().GetGenericArguments();
                if (genArgs.Length == 1)
                {
                    asTaskMethod = asTaskMethod.MakeGenericMethod(genArgs[0]);
                }
            }
            task = asTaskMethod.Invoke(null, [asyncOp]) as Task;
        }

        if (task == null)
        {
            // Fallback via dynamic awaiter
            dynamic dynOp = asyncOp;
            task = Task.Run(async () =>
            {
                while (dynOp.Status == 0) // Started
                {
                    await Task.Delay(100, ct).ConfigureAwait(false);
                }
                return dynOp.GetResults();
            }, ct);
        }

        if (task == null)
        {
            return null;
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        await Task.WhenAny(task, Task.Delay(Timeout.Infinite, linkedCts.Token));

        PropertyInfo? resultProp = task.GetType().GetProperty("Result");
        object? geoposition = resultProp?.GetValue(task);
        if (geoposition == null)
        {
            return null;
        }

        PropertyInfo? coordProp = geoposition.GetType().GetProperty("Coordinate");
        object? coordinate = coordProp?.GetValue(geoposition);
        if (coordinate == null)
        {
            return null;
        }

        PropertyInfo? pointProp = coordinate.GetType().GetProperty("Point");
        object? point = pointProp?.GetValue(coordinate);
        if (point != null)
        {
            PropertyInfo? posProp = point.GetType().GetProperty("Position");
            object? pos = posProp?.GetValue(point);
            if (pos != null)
            {
                PropertyInfo? latProp = pos.GetType().GetProperty("Latitude");
                PropertyInfo? lonProp = pos.GetType().GetProperty("Longitude");
                if (latProp != null && lonProp != null)
                {
                    double lat = (double)latProp.GetValue(pos)!;
                    double lon = (double)lonProp.GetValue(pos)!;
                    return (lat, lon);
                }
            }
        }

        return null;
    }
}
