// Created on Aug 6, 2026 @ 07:01:27 -> Split MediaEngine/CDG/audio device settings out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    // MediaEngine
    public List<string> CdgScalingModes { get; }

    [ObservableProperty]
    private string _selectedCdgScalingMode = "Nearest";

    public List<string> Mp4Backends { get; }

    [ObservableProperty]
    private string _selectedMp4Backend = "LibVLC";

    [ObservableProperty]
    private int _frameRate = 30;

    public List<int> FpsOptions { get; } = [15, 30, 60];

    [ObservableProperty]
    private bool _enableHardwareAcceleration = AppSettings.EnableHardwareAcceleration;

    [ObservableProperty]
    private bool _enableNoiseGate = AppSettings.EnableNoiseGate;

    [ObservableProperty]
    private bool _enableReverb = AppSettings.EnableReverb;

    public List<string> BackdropModes { get; } = ["Original Color", "Neon Waveform", "Nebula Bokeh", "Retro Synthwave", "Space Starfield"];

    [ObservableProperty]
    private string _cdgBackdropMode = AppSettings.CdgBackdropMode;

    public List<int> BufferSizes { get; } = [64, 128, 256, 512, 1024];

    [ObservableProperty]
    private int _selectedBufferSize = AppSettings.SelectedBufferSize;

    public string SelectedKaraokeAudioDevice
    {
        get => AppSettings.SelectedKaraokeAudioDevice;
        set
        {
            if (AppSettings.SelectedKaraokeAudioDevice != value)
            {
                AppSettings.SelectedKaraokeAudioDevice = value;
                OnPropertyChanged(nameof(SelectedKaraokeAudioDevice));

                var mediaEngine = App.AppHost.Services.GetService(typeof(IMediaEngine)) as IMediaEngine;
                mediaEngine?.UpdateAudioParameters();
            }
        }
    }

    public string SelectedBgmAudioDevice
    {
        get => AppSettings.SelectedBgmAudioDevice;
        set
        {
            if (AppSettings.SelectedBgmAudioDevice != value)
            {
                AppSettings.SelectedBgmAudioDevice = value;
                OnPropertyChanged(nameof(SelectedBgmAudioDevice));
                _showFlow.SetBgmAudioDevice(value);
            }
        }
    }

    public bool IsHardwareMixerMode
    {
        get => AppSettings.IsHardwareMixerMode;
        set
        {
            if (AppSettings.IsHardwareMixerMode != value)
            {
                AppSettings.IsHardwareMixerMode = value;
                OnPropertyChanged(nameof(IsHardwareMixerMode));

                var mediaEngine = App.AppHost.Services.GetService(typeof(IMediaEngine)) as IMediaEngine;
                mediaEngine?.UpdateAudioParameters();

                _showFlow.SetFillInTone(FillInBass, FillInTreble, 0);
                _showFlow.SetOpeningTone(OpeningBass, OpeningTreble, 0);
                _showFlow.SetEndRotationTone(EndRotationBass, EndRotationTreble, 0);
            }
        }
    }

    public bool NormalizeVolumeEnabled
    {
        get => AppSettings.NormalizeVolumeEnabled;
        set
        {
            if (AppSettings.NormalizeVolumeEnabled != value)
            {
                AppSettings.NormalizeVolumeEnabled = value;
                OnPropertyChanged(nameof(NormalizeVolumeEnabled));

                var mediaEngine = App.AppHost.Services.GetService(typeof(IMediaEngine)) as IMediaEngine;
                mediaEngine?.UpdateAudioParameters();
            }
        }
    }

    public double TargetLoudnessLufs
    {
        get => AppSettings.TargetLoudnessLufs;
        set
        {
            if (AppSettings.TargetLoudnessLufs != value)
            {
                AppSettings.TargetLoudnessLufs = value;
                OnPropertyChanged(nameof(TargetLoudnessLufs));

                var mediaEngine = App.AppHost.Services.GetService(typeof(IMediaEngine)) as IMediaEngine;
                mediaEngine?.UpdateAudioParameters();
            }
        }
    }

    partial void OnEnableHardwareAccelerationChanged(bool value) => AppSettings.EnableHardwareAcceleration = value;
    partial void OnEnableNoiseGateChanged(bool value) => AppSettings.EnableNoiseGate = value;
    partial void OnEnableReverbChanged(bool value) => AppSettings.EnableReverb = value;
    partial void OnSelectedBufferSizeChanged(int value) => AppSettings.SelectedBufferSize = value;

    partial void OnCdgBackdropModeChanged(string value)
    {
        AppSettings.CdgBackdropMode = value;
        var lyricsVm = App.AppHost.Services.GetService(typeof(LyricsWindowViewModel)) as LyricsWindowViewModel;
        lyricsVm?.NotifyBackdropChanged();
    }
}
