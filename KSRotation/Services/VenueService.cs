// Edited on Sep 17, 2026 @ 12:02:00 -> Wire VenueService to VenueLocationStore for rich GPS and Wi-Fi venue metadata
using System;
using System.Collections.Generic;
using System.Linq;
using Lyracist.Shared;

namespace KSRotation.Services
{
    /// <summary>Persists the DJ's venue name list and location metadata via <see cref="VenueLocationStore"/>.</summary>
    public static class VenueService
    {
        /// <summary>Loads saved venue names from disk.</summary>
        public static List<string> Load() => VenueLocationStore.GetVenueNames();

        /// <summary>Saves the venue names list to disk, preserving location metadata for existing venues.</summary>
        public static void Save(IEnumerable<string> venues)
        {
            var existingLocations = VenueLocationStore.Load().ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);
            var updatedLocations = new List<VenueLocationItem>();

            foreach (var name in venues)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                string trimmed = name.Trim();
                if (existingLocations.TryGetValue(trimmed, out var existing))
                {
                    updatedLocations.Add(existing);
                }
                else
                {
                    updatedLocations.Add(new VenueLocationItem { Name = trimmed, RadiusMeters = 150.0 });
                }
            }

            VenueLocationStore.Save(updatedLocations);
        }

        /// <summary>Loads all venue locations including coordinates and radius.</summary>
        public static List<VenueLocationItem> LoadLocations() => VenueLocationStore.Load();

        /// <summary>Saves all venue locations to disk.</summary>
        public static void SaveLocations(IEnumerable<VenueLocationItem> venues) => VenueLocationStore.Save(venues);

        /// <summary>Upserts a venue with GPS coordinates and Wi-Fi metadata.</summary>
        public static void UpsertVenueLocation(string name, double? latitude, double? longitude, string? wifiSsid = null, double radiusMeters = 150.0)
            => VenueLocationStore.UpsertVenue(name, latitude, longitude, wifiSsid, radiusMeters);

        /// <summary>Finds a matching venue via GPS proximity or house Wi-Fi SSID.</summary>
        public static VenueLocationItem? FindMatchingVenue(double? latitude, double? longitude, string? currentSsid)
            => VenueLocationStore.FindMatchingVenue(latitude, longitude, currentSsid);
    }
}
