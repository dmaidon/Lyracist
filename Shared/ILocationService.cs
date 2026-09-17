// Created on Sep 17, 2026 @ 11:56:30 -> Cross-platform location service contract for acquiring GPS coordinates
using System.Threading;
using System.Threading.Tasks;

namespace Lyracist.Shared;

/// <summary>
/// Cross-platform abstraction for querying current device geographic coordinates.
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Gets whether location querying is supported on the current device and platform.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Asynchronously queries current device coordinates (Latitude, Longitude).
    /// Returns null if location services are unavailable, disabled, or denied.
    /// </summary>
    Task<(double Latitude, double Longitude)?> GetCurrentCoordinatesAsync(CancellationToken ct = default);
}
