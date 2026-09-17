// Created on Sep 17, 2026 @ 11:55:30 -> Haversine distance formula for calculating proximity in meters
using System;

namespace Lyracist.Shared;

/// <summary>
/// Lightweight spherical distance calculations for GPS coordinates.
/// </summary>
public static class GeoMath
{
    private const double EarthRadiusMeters = 6371000.0;

    /// <summary>
    /// Calculates the distance in meters between two GPS coordinate points using the Haversine formula.
    /// </summary>
    public static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double a = (Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0)) +
                   (Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0));

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return EarthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
