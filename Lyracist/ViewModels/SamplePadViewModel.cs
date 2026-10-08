// Created on Oct 7, 2026 @ 19:46:00 -> ViewModel for the 16-pad DJ sample / jingle pad page
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Interfaces;

namespace Lyracist.ViewModels;

public partial class SamplePadSlotItemViewModel : ObservableObject
{
    private readonly ISamplePadService _service;

    public int SlotIndex { get; }

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _color = "#2563EB";

    [ObservableProperty]
    private string _hotKey = string.Empty;

    [ObservableProperty]
    private double _volume = 1.0;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isMissing;

    [ObservableProperty]
    private bool _isAssigned;

    public string FileName => string.IsNullOrEmpty(FilePath) ? "(Empty Pad)" : Path.GetFileName(FilePath);
    public string DisplayToolTip => IsMissing
        ? $"File Missing: {FilePath}"
        : string.IsNullOrEmpty(FilePath)
            ? "Drag and drop an audio file (.mp3, .wav, .m4a) to assign this pad"
            : $"{Label}\nFile: {FilePath}\nHotkey: {(string.IsNullOrEmpty(HotKey) ? "None" : HotKey)}";

    public System.Windows.Media.Brush BackgroundBrush
    {
        get
        {
            try
            {
                var col = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Color);
                if (IsMissing)
                {
                    col = System.Windows.Media.Color.FromArgb(120, 100, 100, 100);
                }
                return new SolidColorBrush(col);
            }
            catch
            {
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
            }
        }
    }

    public SamplePadSlotItemViewModel(SamplePadItem model, ISamplePadService service)
    {
        _service = service;
        SlotIndex = model.SlotIndex;
        ApplyModel(model);
    }

    public void ApplyModel(SamplePadItem model)
    {
        Label = model.Label;
        FilePath = model.FilePath;
        Color = model.Color;
        HotKey = model.HotKey;
        Volume = model.Volume;
        IsAssigned = model.IsAssigned;
        IsMissing = model.IsMissing;
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(DisplayToolTip));
        OnPropertyChanged(nameof(BackgroundBrush));
    }

    [RelayCommand]
    public void Play()
    {
        if (IsAssigned && !IsMissing)
        {
            _service.PlayPad(SlotIndex);
        }
    }

    [RelayCommand]
    public void Clear()
    {
        _service.ClearPad(SlotIndex);
    }
}

public partial class SamplePadViewModel : ObservableObject
{
    private readonly ISamplePadService _service;

    public ObservableCollection<SamplePadSlotItemViewModel> Slots { get; } = [];

    [ObservableProperty]
    private bool _isPlaying;

    public SamplePadViewModel(ISamplePadService service)
    {
        _service = service;

        for (int i = 0; i < 16; i++)
        {
            var model = _service.Pads.FirstOrDefault(p => p.SlotIndex == i) ?? new SamplePadItem { SlotIndex = i };
            Slots.Add(new SamplePadSlotItemViewModel(model, _service));
        }

        _service.StateChanged += OnServiceStateChanged;
        _service.PadPlayingChanged += OnPadPlayingChanged;

        _ = _service.InitializeAsync();
    }

    private void OnServiceStateChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            foreach (var item in _service.Pads)
            {
                if (item.SlotIndex >= 0 && item.SlotIndex < Slots.Count)
                {
                    Slots[item.SlotIndex].ApplyModel(item);
                }
            }
            IsPlaying = _service.IsPlaying;
        });
    }

    private void OnPadPlayingChanged(int slotIndex, bool isPlaying)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            if (slotIndex >= 0 && slotIndex < Slots.Count)
            {
                Slots[slotIndex].IsPlaying = isPlaying;
            }
            IsPlaying = _service.IsPlaying;
        });
    }

    [RelayCommand]
    public void PlaySlot(int slotIndex)
    {
        _service.PlayPad(slotIndex);
    }

    [RelayCommand]
    public void StopAll()
    {
        _service.StopAll();
    }

    public void AssignFile(int slotIndex, string filePath)
    {
        if (slotIndex >= 0 && slotIndex < Slots.Count)
        {
            _service.AssignPad(slotIndex, filePath);
        }
    }

    public void ClearSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Slots.Count)
        {
            _service.ClearPad(slotIndex);
        }
    }

    public void UpdateSlot(SamplePadSlotItemViewModel slot)
    {
        var model = new SamplePadItem
        {
            SlotIndex = slot.SlotIndex,
            Label = slot.Label,
            FilePath = slot.FilePath,
            Color = slot.Color,
            HotKey = slot.HotKey,
            Volume = slot.Volume,
            Order = slot.SlotIndex
        };
        _service.UpdatePad(model);
    }
}
