// Created on Sep 17, 2026 @ 12:15:00 -> Tests for GeoMath and VenueLocationStore hybrid auto-location
using System;
using System.Collections.Generic;
using System.IO;
using Lyracist.Shared;
using Xunit;

namespace KSRotation.Tests;

[Collection("Persistence")]
public class VenueLocationTests
{
    [Fact]
    public void GeoMath_CalculateDistanceMeters_IdenticalCoordinates_ReturnsZero()
    {
        double distance = GeoMath.CalculateDistanceMeters(40.7128, -74.0060, 40.7128, -74.0060);
        Assert.Equal(0.0, distance, precision: 2);
    }

    [Fact]
    public void GeoMath_CalculateDistanceMeters_KnownDistance_MatchesExpected()
    {
        // Times Square (40.7580, -73.9855) to Empire State Building (40.7484, -73.9857) is ~1.07 km
        double distance = GeoMath.CalculateDistanceMeters(40.7580, -73.9855, 40.7484, -73.9857);
        Assert.InRange(distance, 1000, 1150);
    }

    [Fact]
    public void VenueLocationStore_FindMatchingVenue_ByCoordinates_ReturnsMatchingVenue()
    {
        var testVenues = new List<VenueLocationItem>
        {
            new()
            {
                Name = "Club Downtown",
                Latitude = 35.7796,
                Longitude = -78.6382,
                RadiusMeters = 150
            },
            new()
            {
                Name = "Uptown Lounge",
                Latitude = 35.8436,
                Longitude = -78.6431,
                RadiusMeters = 150
            }
        };

        // Query within 50m of Club Downtown
        // 35.7798, -78.6382 is approx 22m away
        var matched = VenueLocationStore.FindMatchingVenue(35.7798, -78.6382, null, testVenues);
        Assert.NotNull(matched);
        Assert.Equal("Club Downtown", matched.Name);

        // Query far away (approx 5 miles)
        var outOfRange = VenueLocationStore.FindMatchingVenue(35.7000, -78.6000, null, testVenues);
        Assert.Null(outOfRange);
    }

    [Fact]
    public void VenueLocationStore_FindMatchingVenue_WifiFallback_WorksWhenGpsUnavailable()
    {
        var testVenues = new List<VenueLocationItem>
        {
            new()
            {
                Name = "The Tavern",
                Latitude = 0,
                Longitude = 0,
                WifiSsid = "Tavern_Guest"
            }
        };

        var matched = VenueLocationStore.FindMatchingVenue(null, null, "Tavern_Guest", testVenues);
        Assert.NotNull(matched);
        Assert.Equal("The Tavern", matched.Name);
    }

    [Fact]
    public void VenueLocationStore_FindMatchingVenue_TravelRouter_ExcludesFromWifiMatch()
    {
        var testVenues = new List<VenueLocationItem>
        {
            new()
            {
                Name = "First Gig Venue",
                Latitude = 35.0,
                Longitude = -78.0,
                WifiSsid = "DJ_TravelRouter_5G"
            }
        };

        // When SSID is registered as a travel router, wifi match should be ignored
        VenueLocationStore.SetTravelRouterSsid("DJ_TravelRouter_5G", isTravelRouter: true);
        try
        {
            var matched = VenueLocationStore.FindMatchingVenue(null, null, "DJ_TravelRouter_5G", testVenues);
            Assert.Null(matched);
        }
        finally
        {
            VenueLocationStore.SetTravelRouterSsid("DJ_TravelRouter_5G", isTravelRouter: false);
        }
    }
}
