// Edited on Sep 22, 2026 @ 08:49:30 -> Auto-toggle between DJ Banner and Rotation when on the same screen
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Services.Display;
using Microsoft.Extensions.DependencyInjection;

namespace Lyracist.ViewModels;

public partial class KaraokeViewModel
{
    public bool IsLyricsActive
    {
        get => _displayService.IsLyricsActive;
        set
        {
            if (_displayService.IsLyricsActive != value)
            {
                _displayService.IsLyricsActive = value;
                OnPropertyChanged(nameof(IsLyricsActive));
            }
        }
    }

    public bool IsRotationActive
    {
        get => _displayService.IsRotationActive;
        set
        {
            if (_displayService.IsRotationActive != value)
            {
                if (value && _displayService.IsDjBannerActive &&
                    _displayService.GetPreferences().RotationScreenIndex.HasValue &&
                    _displayService.GetPreferences().RotationScreenIndex == _displayService.GetPreferences().DjBannerScreenIndex)
                {
                    _displayService.IsDjBannerActive = false;
                    OnPropertyChanged(nameof(IsDjBannerActive));
                }
                _displayService.IsRotationActive = value;
                OnPropertyChanged(nameof(IsRotationActive));
            }
        }
    }

    public bool IsDjBannerActive
    {
        get => _displayService.IsDjBannerActive;
        set
        {
            if (_displayService.IsDjBannerActive != value)
            {
                if (value && _displayService.IsRotationActive &&
                    _displayService.GetPreferences().DjBannerScreenIndex.HasValue &&
                    _displayService.GetPreferences().DjBannerScreenIndex == _displayService.GetPreferences().RotationScreenIndex)
                {
                    _displayService.IsRotationActive = false;
                    OnPropertyChanged(nameof(IsRotationActive));
                }
                _displayService.IsDjBannerActive = value;
                OnPropertyChanged(nameof(IsDjBannerActive));
            }
        }
    }

    // Added properties for Multi-Monitor display lists
    public ObservableCollection<ScreenInfo> AvailableScreens { get; } = [];

    [ObservableProperty]
    private int _selectedLyricsScreenIndex = 0;

    [ObservableProperty]
    private int _selectedRotationScreenIndex = 0;

    [ObservableProperty]
    private int _selectedDjBannerScreenIndex = 0;

    partial void OnSelectedLyricsScreenIndexChanged(int value)
    {
        if (value >= 0 && value < AvailableScreens.Count)
        {
            var screen = AvailableScreens[value];
            _displayService.MoveLyricsToScreen(screen.Index == -1 ? null : screen.Index);
        }
    }

    partial void OnSelectedRotationScreenIndexChanged(int value)
    {
        if (value >= 0 && value < AvailableScreens.Count)
        {
            var screen = AvailableScreens[value];
            _displayService.MoveRotationToScreen(screen.Index == -1 ? null : screen.Index);
        }
    }

    partial void OnSelectedDjBannerScreenIndexChanged(int value)
    {
        if (value >= 0 && value < AvailableScreens.Count)
        {
            var screen = AvailableScreens[value];
            _displayService.MoveDjBannerToScreen(screen.Index == -1 ? null : screen.Index);
        }
    }

    public List<string> ProjectionViews { get; } = [.. SettingsViewModel.AllProjectionViews];

    public string SelectedProjectionView
    {
        get => _displayService.GetPreferences().RotationViewMode ?? "Normal List";
        set
        {
            if (SelectedProjectionView != value)
            {
                _displayService.SetRotationViewMode(value);
                OnPropertyChanged(nameof(SelectedProjectionView));

                var settingsVm = App.AppHost.Services.GetService<SettingsViewModel>();
                if (settingsVm != null && settingsVm.SelectedProjectionView != value)
                {
                    settingsVm.SelectedProjectionView = value;
                }
            }
        }
    }

    private void OnScreenAssignmentsChanged()
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            var prefs = _displayService.GetPreferences();

            var currentLyricsScreen = prefs.LyricsScreenIndex.HasValue
                ? AvailableScreens.FirstOrDefault(s => s.Index == prefs.LyricsScreenIndex.Value)
                : AvailableScreens.FirstOrDefault(s => s.Index == -1);

            var currentRotationScreen = prefs.RotationScreenIndex.HasValue
                ? AvailableScreens.FirstOrDefault(s => s.Index == prefs.RotationScreenIndex.Value)
                : AvailableScreens.FirstOrDefault(s => s.Index == -1);

            var currentDjBannerScreen = prefs.DjBannerScreenIndex.HasValue
                ? AvailableScreens.FirstOrDefault(s => s.Index == prefs.DjBannerScreenIndex.Value)
                : AvailableScreens.FirstOrDefault(s => s.Index == -1);

            int newLyricsIndex = currentLyricsScreen != null ? AvailableScreens.IndexOf(currentLyricsScreen) : 0;
            int newRotationIndex = currentRotationScreen != null ? AvailableScreens.IndexOf(currentRotationScreen) : 0;
            int newDjBannerIndex = currentDjBannerScreen != null ? AvailableScreens.IndexOf(currentDjBannerScreen) : 0;

#pragma warning disable MVVMTK0034
            if (_selectedLyricsScreenIndex != newLyricsIndex)
            {
                _selectedLyricsScreenIndex = newLyricsIndex;
                OnPropertyChanged(nameof(SelectedLyricsScreenIndex));
            }
            if (_selectedRotationScreenIndex != newRotationIndex)
            {
                _selectedRotationScreenIndex = newRotationIndex;
                OnPropertyChanged(nameof(SelectedRotationScreenIndex));
            }
            if (_selectedDjBannerScreenIndex != newDjBannerIndex)
            {
                _selectedDjBannerScreenIndex = newDjBannerIndex;
                OnPropertyChanged(nameof(SelectedDjBannerScreenIndex));
            }
#pragma warning restore MVVMTK0034

            OnPropertyChanged(nameof(IsLyricsActive));
            OnPropertyChanged(nameof(IsRotationActive));
            OnPropertyChanged(nameof(IsDjBannerActive));

            if (SelectedDjBanner?.FullPath != prefs.SelectedDjBannerPath)
            {
                SelectedDjBanner = DjBanners.FirstOrDefault(b => b.FullPath == prefs.SelectedDjBannerPath);
            }
        });
    }

    public void RaiseSelectedProjectionViewChanged()
    {
        OnPropertyChanged(nameof(SelectedProjectionView));
    }
}
