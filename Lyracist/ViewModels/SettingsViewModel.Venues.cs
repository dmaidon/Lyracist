// Created on Aug 6, 2026 @ 07:01:27 -> Split venue & DJ name settings out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;

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

    private void RefreshVenues()
    {
        Venues.Clear();
        foreach (var v in AppSettings.Venues)
        {
            Venues.Add(v);
        }
        SelectedVenue = AppSettings.SelectedVenue;
        RefreshSelectedVenueGraphics();
    }

    partial void OnDjNameChanged(string value)
    {
        AppSettings.DjName = value;
    }

    partial void OnSelectedVenueChanged(string? value)
    {
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
