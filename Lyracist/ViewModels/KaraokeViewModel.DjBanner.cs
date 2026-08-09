// Edited on Aug 9, 2026 @ 09:15:00 -> Add UpdateActiveSpecialEventFromSync helper method to synchronize active special event from KSRotation
using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lyracist.ViewModels;

public partial class KaraokeViewModel
{
    public ObservableCollection<Lyracist.Shared.DjBannerItem> DjBanners { get; } = [];

    [ObservableProperty]
    private Lyracist.Shared.DjBannerItem? _selectedDjBanner;

    private void RefreshDjBanners()
    {
        DjBanners.Clear();
        foreach (var item in Lyracist.Shared.DjBannerFileManager.ScanBanners(Lyracist.Shared.Globals.DjBannersDir))
        {
            DjBanners.Add(item);
        }

        var prefs = _displayService.GetPreferences();
        if (!string.IsNullOrEmpty(prefs.SelectedDjBannerPath))
        {
            SelectedDjBanner = DjBanners.FirstOrDefault(b => b.FullPath == prefs.SelectedDjBannerPath);
        }
        else
        {
            SelectedDjBanner = DjBanners.FirstOrDefault();
        }
    }

    partial void OnSelectedDjBannerChanged(Lyracist.Shared.DjBannerItem? value)
    {
        _displayService.UpdateDjBanner(value?.FullPath ?? string.Empty);
    }

    [RelayCommand]
    private void UploadDjBanner()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Upload DJ Banner",
            Filter = "Supported Banners (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Video Files (*.mp4)|*.mp4|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string destPath = Lyracist.Shared.DjBannerFileManager.CopyInWithDedup(dialog.FileName, Lyracist.Shared.Globals.DjBannersDir);
                RefreshDjBanners();

                SelectedDjBanner = DjBanners.FirstOrDefault(b => b.FullPath == destPath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to upload banner: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void DeleteDjBanner()
    {
        if (SelectedDjBanner == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete the DJ Banner '{SelectedDjBanner.FileName}'?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                string path = SelectedDjBanner.FullPath;
                SelectedDjBanner = null;

                Lyracist.Shared.DjBannerFileManager.DeleteBanner(path);
                RefreshDjBanners();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to delete banner: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void ShowDjBanner()
    {
        _displayService.ShowDjBannerWindow();
    }

    [ObservableProperty]
    private string _activeSpecialEvent = "None";

    public ObservableCollection<SpecialEventOptionViewModel> SpecialEventOptions { get; } = [];

    public void InitializeSpecialEvents()
    {
        var prefs = _displayService.GetPreferences();
        ActiveSpecialEvent = string.IsNullOrEmpty(prefs.SelectedSpecialEvent) ? "None" : prefs.SelectedSpecialEvent;
        RebuildSpecialEventOptions();
    }

    public void RebuildSpecialEventOptions()
    {
        SpecialEventOptions.Clear();
        SpecialEventOptions.Add(new SpecialEventOptionViewModel("None", "None", ActiveSpecialEvent == "None", OnSpecialEventChanged));
        foreach (var ev in Lyracist.Core.Helpers.AppSettings.SpecialEvents)
        {
            SpecialEventOptions.Add(new SpecialEventOptionViewModel(ev.EventName, ev.EventName, ActiveSpecialEvent == ev.EventName, OnSpecialEventChanged));
        }
    }

    private void OnSpecialEventChanged(string value)
    {
        ActiveSpecialEvent = value;
        _displayService.UpdateSpecialEvent(value);
    }

    public void UpdateActiveSpecialEventFromSync(string eventName)
    {
        if (ActiveSpecialEvent == eventName) return;

        ActiveSpecialEvent = eventName;
        _displayService.UpdateSpecialEvent(eventName);

        foreach (var option in SpecialEventOptions)
        {
            if (option.Value == eventName)
            {
                if (!option.IsSelected) option.IsSelected = true;
            }
            else
            {
                if (option.IsSelected) option.IsSelected = false;
            }
        }
    }
}

public class SpecialEventOptionViewModel : ObservableObject
{
    private readonly Action<string> _onSelected;
    public string DisplayName { get; }
    public string Value { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value)
            {
                _onSelected(Value);
            }
        }
    }

    public SpecialEventOptionViewModel(string displayName, string value, bool isSelected, Action<string> onSelected)
    {
        DisplayName = displayName;
        Value = value;
        _isSelected = isSelected;
        _onSelected = onSelected;
    }
}
