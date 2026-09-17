// Created on Sep 17, 2026 @ 11:56:00 -> Unified venue location persistence with backward-compatible JSON loading and hybrid GPS/Wi-Fi matching
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Lyracist.Shared;

/// <summary>
/// Persistence and hybrid proximity matching engine for venue locations and travel router registrations.
/// </summary>
public static class VenueLocationStore
{
    private const string VenuesFileName = "ksrotation_venues.json";
    private const string LegacyVenuesFileName = "venues.json";
    private const string TravelRoutersFileName = "ksrotation_travel_routers.json";
    private const string DefaultVenue = "Karaoke Night";

    private static readonly Lock _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string SettingsDir => Globals.SettingsDir;

    private static string VenuesPath => Path.Combine(SettingsDir, VenuesFileName);
    private static string LegacyVenuesPath => Path.Combine(SettingsDir, LegacyVenuesFileName);
    private static string TravelRoutersPath => Path.Combine(SettingsDir, TravelRoutersFileName);

    /// <summary>
    /// Loads all saved venue locations from disk.
    /// Transparently supports legacy string arrays ("[\"Venue 1\", \"Venue 2\"]") as well as rich objects.
    /// </summary>
    public static List<VenueLocationItem> Load()
    {
        lock (_lock)
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                {
                    Directory.CreateDirectory(SettingsDir);
                }

                string path = VenuesPath;
                if (!File.Exists(path) && File.Exists(LegacyVenuesPath))
                {
                    try { File.Copy(LegacyVenuesPath, path, true); } catch { }
                }

                if (!File.Exists(path))
                {
                    return [new VenueLocationItem { Name = DefaultVenue, RadiusMeters = 150.0 }];
                }

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return [new VenueLocationItem { Name = DefaultVenue, RadiusMeters = 150.0 }];
                }

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return [new VenueLocationItem { Name = DefaultVenue, RadiusMeters = 150.0 }];
                }

                var list = new List<VenueLocationItem>();
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        string? name = element.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            list.Add(new VenueLocationItem { Name = name.Trim(), RadiusMeters = 150.0 });
                        }
                    }
                    else if (element.ValueKind == JsonValueKind.Object)
                    {
                        var item = JsonSerializer.Deserialize<VenueLocationItem>(element.GetRawText(), JsonOptions);
                        if (item != null && !string.IsNullOrWhiteSpace(item.Name))
                        {
                            if (item.RadiusMeters <= 0)
                            {
                                item.RadiusMeters = 150.0;
                            }
                            list.Add(item);
                        }
                    }
                }

                return list.Count > 0 ? list : [new VenueLocationItem { Name = DefaultVenue, RadiusMeters = 150.0 }];
            }
            catch
            {
                return [new VenueLocationItem { Name = DefaultVenue, RadiusMeters = 150.0 }];
            }
        }
    }

    /// <summary>
    /// Saves the venue locations list to disk.
    /// </summary>
    public static void Save(IEnumerable<VenueLocationItem> venues)
    {
        lock (_lock)
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                {
                    Directory.CreateDirectory(SettingsDir);
                }

                var list = venues?.Where(v => !string.IsNullOrWhiteSpace(v.Name)).ToList() ?? [];
                if (list.Count == 0)
                {
                    list.Add(new VenueLocationItem { Name = DefaultVenue, RadiusMeters = 150.0 });
                }

                string json = JsonSerializer.Serialize(list, JsonOptions);
                File.WriteAllText(VenuesPath, json);
            }
            catch
            {
                // Suppress disk write failures safely
            }
        }
    }

    /// <summary>
    /// Loads a simple list of venue names for UI dropdowns and backwards compatibility.
    /// </summary>
    public static List<string> GetVenueNames()
    {
        return Load().Select(v => v.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Inserts or updates a venue's location coordinates, Wi-Fi SSID, and last visited timestamp.
    /// </summary>
    public static void UpsertVenue(string name, double? latitude, double? longitude, string? wifiSsid = null, double radiusMeters = 150.0)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        string trimmedName = name.Trim();
        lock (_lock)
        {
            var venues = Load();
            var existing = venues.FirstOrDefault(v => string.Equals(v.Name, trimmedName, StringComparison.OrdinalIgnoreCase));

            bool isTravelRouter = !string.IsNullOrWhiteSpace(wifiSsid) && IsTravelRouterSsid(wifiSsid);

            if (existing != null)
            {
                if (latitude.HasValue && longitude.HasValue)
                {
                    existing.Latitude = latitude.Value;
                    existing.Longitude = longitude.Value;
                    if (radiusMeters > 0)
                    {
                        existing.RadiusMeters = radiusMeters;
                    }
                }
                if (!string.IsNullOrWhiteSpace(wifiSsid) && !isTravelRouter)
                {
                    existing.WifiSsid = wifiSsid.Trim();
                }
                existing.LastVisited = DateTime.Now;
            }
            else
            {
                venues.Add(new VenueLocationItem
                {
                    Name = trimmedName,
                    Latitude = latitude,
                    Longitude = longitude,
                    RadiusMeters = radiusMeters > 0 ? radiusMeters : 150.0,
                    WifiSsid = isTravelRouter ? null : wifiSsid?.Trim(),
                    LastVisited = DateTime.Now
                });
            }

            Save(venues);
        }
    }

    /// <summary>
    /// Finds a matching venue using the hybrid strategy:
    /// 1. GPS coordinates match within each venue's radius (closest match wins).
    /// 2. If no GPS match, Wi-Fi SSID matches against house Wi-Fi (excluding registered travel router networks).
    /// </summary>
    public static VenueLocationItem? FindMatchingVenue(double? latitude, double? longitude, string? currentSsid, IEnumerable<VenueLocationItem>? venues = null)
    {
        var list = venues?.ToList() ?? Load();

        // 1. Primary Strategy: Physical GPS coordinates
        if (latitude.HasValue && longitude.HasValue)
        {
            double curLat = latitude.Value;
            double curLon = longitude.Value;

            VenueLocationItem? closestVenue = null;
            double closestDistance = double.MaxValue;

            foreach (var v in list)
            {
                if (v.Latitude.HasValue && v.Longitude.HasValue)
                {
                    double dist = GeoMath.CalculateDistanceMeters(curLat, curLon, v.Latitude.Value, v.Longitude.Value);
                    double radius = v.RadiusMeters > 0 ? v.RadiusMeters : 150.0;
                    if (dist <= radius && dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestVenue = v;
                    }
                }
            }

            if (closestVenue != null)
            {
                return closestVenue;
            }
        }

        // 2. Secondary Strategy: Venue House Wi-Fi SSID (excluding travel routers)
        if (!string.IsNullOrWhiteSpace(currentSsid))
        {
            string trimmedSsid = currentSsid.Trim();
            if (!IsTravelRouterSsid(trimmedSsid))
            {
                var wifiMatch = list.FirstOrDefault(v =>
                    !string.IsNullOrWhiteSpace(v.WifiSsid) &&
                    string.Equals(v.WifiSsid, trimmedSsid, StringComparison.OrdinalIgnoreCase));

                if (wifiMatch != null)
                {
                    return wifiMatch;
                }
            }
        }

        return null;
    }

    #region Travel Router Registry

    /// <summary>
    /// Loads the list of SSIDs designated as portable travel routers or personal hotspots.
    /// </summary>
    public static List<string> LoadTravelRouterSsids()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(TravelRoutersPath))
                {
                    return [];
                }
                string json = File.ReadAllText(TravelRoutersPath);
                return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
            }
            catch
            {
                return [];
            }
        }
    }

    /// <summary>
    /// Registers or unregisters an SSID as a travel router.
    /// </summary>
    public static void SetTravelRouterSsid(string ssid, bool isTravelRouter)
    {
        if (string.IsNullOrWhiteSpace(ssid))
        {
            return;
        }

        string trimmed = ssid.Trim();
        lock (_lock)
        {
            var list = LoadTravelRouterSsids();
            bool exists = list.Any(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));

            if (isTravelRouter && !exists)
            {
                list.Add(trimmed);
            }
            else if (!isTravelRouter && exists)
            {
                list.RemoveAll(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
            }

            try
            {
                if (!Directory.Exists(SettingsDir))
                {
                    Directory.CreateDirectory(SettingsDir);
                }
                File.WriteAllText(TravelRoutersPath, JsonSerializer.Serialize(list, JsonOptions));
            }
            catch { }
        }
    }

    /// <summary>
    /// Checks whether an SSID is designated as a travel router.
    /// </summary>
    public static bool IsTravelRouterSsid(string? ssid)
    {
        if (string.IsNullOrWhiteSpace(ssid))
        {
            return false;
        }

        string trimmed = ssid.Trim();
        var list = LoadTravelRouterSsids();
        return list.Any(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
