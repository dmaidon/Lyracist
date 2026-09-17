// Edited on Sep 17, 2026 @ 12:12:00 -> Add GPS location tagging and auto-detection commands in SettingsViewModel.Venues.cs
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Shared;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    // Venues & DJ
    public ObservableCollection<string> Venues { get; } = [];
    public ObservableCollection<string> SelectedVenueGraphics { get; } = [];

    [ObservableProperty]
    private string _djName = AppSettings.DjName;

    [ObservableProperty]
    private string? _selectedVenue = AppSettings.SelectedVenue;

    [ObservableProperty]
    private string _newVenueName = string.Empty;

    public string SelectedVenueLocationStatus
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SelectedVenue)) return "No venue selected";
            var item = VenueLocationStore.Load().FirstOrDefault(v => string.Equals(v.Name, SelectedVenue, System.StringComparison.OrdinalIgnoreCase));
            if (item != null && item.Latitude.HasValue && item.Longitude.HasValue)
            {
                return $"📍 GPS: {item.Latitude.Value:F4}, {item.Longitude.Value:F4} (Radius: {item.RadiusMeters:F0}m)";
            }
            return "📍 Location not tagged";
        }
    }

    private void RefreshVenues()
    {
        Venues.Clear();
        foreach (var v in AppSettings.Venues)
        {
            Venues.Add(v);
        }
        SelectedVenue = AppSettings.SelectedVenue;
        RefreshSelectedVenueGraphics();
        OnPropertyChanged(nameof(SelectedVenueLocationStatus));
    }

    partial void OnDjNameChanged(string value)
    {
        AppSettings.DjName = value;
    }

    partial void OnSelectedVenueChanged(string? value)
    {
        OnPropertyChanged(nameof(SelectedVenueLocationStatus));
        if (!string.IsNullOrEmpty(value))
        {
            AppSettings.SelectedVenue = value;
            RefreshSelectedVenueGraphics();

            var lyricsVm = App.AppHost.Services.GetService(typeof(LyricsWindowViewModel)) as LyricsWindowViewModel;
            lyricsVm?.StartSlideshow();
        }
        else
        {
            SelectedVenueGraphics.Clear();
            var lyricsVm = App.AppHost.Services.GetService(typeof(LyricsWindowViewModel)) as LyricsWindowViewModel;
            lyricsVm?.StopSlideshow();
        }
    }

    private void RefreshSelectedVenueGraphics()
    {
        SelectedVenueGraphics.Clear();
        if (!string.IsNullOrEmpty(SelectedVenue))
        {
            foreach (var img in AppSettings.GetVenueGraphics(SelectedVenue))
            {
                SelectedVenueGraphics.Add(img);
            }
        }
    }

    [RelayCommand]
    private void AddVenue()
    {
        string venue = NewVenueName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(venue)) return;

        AppSettings.AddVenue(venue);
        NewVenueName = string.Empty;
        RefreshVenues();
        SelectedVenue = venue;
    }

    [RelayCommand]
    private async Task TagCurrentVenueLocation()
    {
        if (string.IsNullOrWhiteSpace(SelectedVenue)) return;
        var coords = await WindowsLocationService.Instance.GetCurrentCoordinatesAsync();
        string? ssid = WifiHelper.GetConnectedSsid();
        VenueLocationStore.UpsertVenue(SelectedVenue, coords?.Latitude, coords?.Longitude, ssid);
        OnPropertyChanged(nameof(SelectedVenueLocationStatus));
    }

    [RelayCommand]
    private async Task AutoDetectVenue()
    {
        string? matched = await AppSettings.AutoDetectVenueLocationAsync();
        if (!string.IsNullOrWhiteSpace(matched))
        {
            SelectedVenue = matched;
        }
        OnPropertyChanged(nameof(SelectedVenueLocationStatus));
    }

    [RelayCommand]
    private void RemoveVenue()
    {
        if (SelectedVenue == null) return;

        AppSettings.RemoveVenue(SelectedVenue);
        SelectedVenue = null;
        RefreshVenues();
    }

    [RelayCommand]
    private void AddVenueGraphic()
    {
        if (string.IsNullOrEmpty(SelectedVenue)) return;

        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            Multiselect = false,
            Title = "Select Graphic for Venue"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            AppSettings.AddVenueGraphic(SelectedVenue, openFileDialog.FileName);
            RefreshSelectedVenueGraphics();

            var lyricsVm = App.AppHost.Services.GetService(typeof(LyricsWindowViewModel)) as LyricsWindowViewModel;
            lyricsVm?.StartSlideshow();
        }
    }

    [RelayCommand]
    private void RemoveVenueGraphic(string? path)
    {
        if (string.IsNullOrEmpty(SelectedVenue) || string.IsNullOrEmpty(path)) return;

        AppSettings.RemoveVenueGraphic(SelectedVenue, path);
        RefreshSelectedVenueGraphics();

        var lyricsVm = App.AppHost.Services.GetService(typeof(LyricsWindowViewModel)) as LyricsWindowViewModel;
        lyricsVm?.StartSlideshow();
    }
}
