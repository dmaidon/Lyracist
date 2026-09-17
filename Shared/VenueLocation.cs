// Created on Sep 17, 2026 @ 11:55:00 -> Model representing venue location with GPS coordinates, radius, and Wi-Fi metadata
using System;

namespace Lyracist.Shared;

/// <summary>
/// Model representing a venue with optional physical GPS coordinates,
/// proximity matching radius, house Wi-Fi SSID, and Travel Router exclusion flag.
/// </summary>
public class VenueLocationItem
{
    public string Name { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double RadiusMeters { get; set; } = 150.0;
    public string? WifiSsid { get; set; }
    public string? WifiBssid { get; set; }
    public bool IsTravelRouter { get; set; }
    public DateTime? LastVisited { get; set; }
    public string? Notes { get; set; }
}
