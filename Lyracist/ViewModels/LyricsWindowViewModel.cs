// Edited on Oct 3, 2026 @ 12:47:00 -> Make SlideshowImagePath nullable to prevent ImageSourceConverter conversion warning
using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Models;
using Lyracist.Services.Display;

namespace Lyracist.ViewModels;

public sealed record MonitorOption(int Index, string Label);

public partial class LyricsWindowViewModel : BaseViewModel
{
    private readonly IDisplayService _display;

    public LyricsWindowViewModel(IDisplayService display)
    {
        _display = display;
        IsMirrored = display.GetPreferences().IsLyricsMirrored;
        RefreshQrCode();
        StartSlideshow();
    }

    [ObservableProperty]
    private System.Windows.Media.Imaging.BitmapImage? _qrCodeImage;

    [ObservableProperty]
    private string _joinUrl = string.Empty;

    public bool ShowQrCodeOnLyricsScreen
    {
        get => Lyracist.Core.Helpers.AppSettings.ShowQrCodeOnLyricsScreen;
        set
        {
            if (Lyracist.Core.Helpers.AppSettings.ShowQrCodeOnLyricsScreen != value)
            {
                Lyracist.Core.Helpers.AppSettings.ShowQrCodeOnLyricsScreen = value;
                OnPropertyChanged();
            }
        }
    }

    public void RefreshQrCode()
    {
        try
        {
            string ip = Lyracist.Core.Helpers.AppSettings.GetActiveIPAddress();
            JoinUrl = $"http://{ip}:{Lyracist.Core.Helpers.AppSettings.TabletPort}";

            using var qrGenerator = new QRCoder.QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(JoinUrl, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);
            QrCodeImage = LoadImage(qrCodeAsPngByteArr);
        }
        catch (System.Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to generate QR Code", ex);
        }
    }

    private static System.Windows.Media.Imaging.BitmapImage? LoadImage(byte[] imageData)
    {
        if (imageData == null || imageData.Length == 0) return null;
        var image = new System.Windows.Media.Imaging.BitmapImage();
        using (var mem = new System.IO.MemoryStream(imageData))
        {
            mem.Position = 0;
            image.BeginInit();
            image.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat;
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.UriSource = null;
            image.StreamSource = mem;
            image.EndInit();
        }
        image.Freeze();
        return image;
    }

    [ObservableProperty]
    private ImageSource? _frame;

    [ObservableProperty]
    private string _fallbackText = "Lyracist — Ready";

    [ObservableProperty]
    private bool _isFallbackVisible = true;

    [ObservableProperty]
    private bool _isMirrored;

    [ObservableProperty]
    private string? _slideshowImagePath;

    [ObservableProperty]
    private bool _isSlideshowVisible;

    private DispatcherTimer? _slideshowTimer;
    private int _slideshowIndex;

    public void StartSlideshow()
    {
        _slideshowTimer?.Stop();
        
        string venue = Lyracist.Core.Helpers.AppSettings.SelectedVenue;
        var graphics = Lyracist.Core.Helpers.AppSettings.GetVenueGraphics(venue);

        if (graphics == null || graphics.Count == 0)
        {
            IsSlideshowVisible = false;
            SlideshowImagePath = null;
            return;
        }

        _slideshowIndex = 0;
        SlideshowImagePath = graphics[_slideshowIndex];
        IsSlideshowVisible = true;

        if (graphics.Count > 1)
        {
            _slideshowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _slideshowTimer.Tick += (s, e) =>
            {
                var currentGraphics = Lyracist.Core.Helpers.AppSettings.GetVenueGraphics(Lyracist.Core.Helpers.AppSettings.SelectedVenue);
                if (currentGraphics == null || currentGraphics.Count == 0)
                {
                    StopSlideshow();
                    return;
                }

                _slideshowIndex = (_slideshowIndex + 1) % currentGraphics.Count;
                SlideshowImagePath = currentGraphics[_slideshowIndex];
            };
            _slideshowTimer.Start();
        }
    }

    public void StopSlideshow()
    {
        _slideshowTimer?.Stop();
        _slideshowTimer = null;
        IsSlideshowVisible = false;
        SlideshowImagePath = null;
    }

    [ObservableProperty]
    private string _overlayText = string.Empty;

    [ObservableProperty]
    private bool _isOverlayVisible;

    private DispatcherTimer? _overlayTimer;

    public ObservableCollection<MonitorOption> Monitors { get; } = [];

    /// <summary>
    /// Persists the mirror flag whenever it changes, whether set via the
    /// context menu checkbox, the M key, or DisplayService.SetLyricsMirror.
    /// </summary>
    partial void OnIsMirroredChanged(bool value)
    {
        _display.SetLyricsMirror(value);
    }

    /// <summary>
    /// Pushes a rendered CDG or MP4 frame to the display. A null frame drops
    /// the window back into fallback text mode.
    /// </summary>
    public void UpdateFrame(ImageSource? newFrame)
    {
        if (newFrame == null)
        {
            ShowFallback("No Lyrics Available");
        }
        else
        {
            Frame = newFrame;
            IsFallbackVisible = false;
            StopSlideshow();
        }
    }

    /// <summary>
    /// Clears the frame display and shows the given text instead.
    /// </summary>
    public void ShowFallback(string text)
    {
        Frame = null;
        FallbackText = text;
        StartSlideshow();
        IsFallbackVisible = !IsSlideshowVisible;
    }

    /// <summary>
    /// Prepares the projection display for a newly loaded song.
    /// </summary>
    public void LoadSong(string songTitle, string artist)
    {
        string label = !string.IsNullOrWhiteSpace(songTitle)
            ? $"{artist} - {songTitle}"
            : "Lyracist — Ready";
        ShowFallback(label);
    }

    public void LoadSong(Singer singer)
    {
        if (singer != null)
        {
            LoadSong(singer.SongTitle, singer.Artist);
        }
    }

    /// <summary>
    /// Flashes an attention banner over whatever is playing (used by Scaryoke
    /// and special-occasion effects), auto-hiding after the given duration.
    /// </summary>
    public void ShowOverlay(string text, int seconds = 8)
    {
        OverlayText = text;
        IsOverlayVisible = true;

        _overlayTimer?.Stop();
        _overlayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(1, seconds)) };
        _overlayTimer.Tick += (_, _) =>
        {
            IsOverlayVisible = false;
            _overlayTimer?.Stop();
        };
        _overlayTimer.Start();
    }

    [RelayCommand]
    public void RefreshMonitors()
    {
        Monitors.Clear();
        foreach (var screen in _display.GetScreens())
        {
            string primary = screen.IsPrimary ? " (Primary)" : "";
            string label = $"Monitor {screen.Index + 1}{primary} — {screen.Bounds.Width:0}×{screen.Bounds.Height:0}";
            Monitors.Add(new MonitorOption(screen.Index, label));
        }
    }

    [RelayCommand]
    private void AssignToMonitor(MonitorOption option)
    {
        _display.MoveLyricsToScreen(option.Index);
    }

    [RelayCommand]
    public void ToggleMirror()
    {
        IsMirrored = !IsMirrored;
    }

    public string CdgBackdropMode => Lyracist.Core.Helpers.AppSettings.CdgBackdropMode;
    public bool IsNebulaVisible => CdgBackdropMode == "Nebula Bokeh";
    public bool IsWaveformVisible => CdgBackdropMode == "Neon Waveform";
    public bool IsSynthwaveVisible => CdgBackdropMode == "Retro Synthwave";
    public bool IsSpaceVisible => CdgBackdropMode == "Space Starfield";

    public bool EnableLyricsVisualizer
    {
        get => Lyracist.Core.Helpers.AppSettings.EnableLyricsVisualizer;
        set
        {
            if (Lyracist.Core.Helpers.AppSettings.EnableLyricsVisualizer != value)
            {
                Lyracist.Core.Helpers.AppSettings.EnableLyricsVisualizer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsVisualizerVisible));
            }
        }
    }

    public bool IsVisualizerVisible => EnableLyricsVisualizer;

    public double VisualizerOpacity
    {
        get => Lyracist.Core.Helpers.AppSettings.LyricsVisualizerOpacity;
        set
        {
            Lyracist.Core.Helpers.AppSettings.LyricsVisualizerOpacity = value;
            OnPropertyChanged();
            NotifyOpacityFlagsChanged();
        }
    }

    public string VisualizerStyle
    {
        get => Lyracist.Core.Helpers.AppSettings.LyricsVisualizerStyle;
        set
        {
            Lyracist.Core.Helpers.AppSettings.LyricsVisualizerStyle = value;
            OnPropertyChanged();
            NotifyStyleFlagsChanged();
        }
    }

    public string VisualizerBarWidth
    {
        get => Lyracist.Core.Helpers.AppSettings.LyricsVisualizerBarWidth;
        set
        {
            Lyracist.Core.Helpers.AppSettings.LyricsVisualizerBarWidth = value;
            OnPropertyChanged();
            NotifyBarWidthFlagsChanged();
        }
    }

    public string VisualizerMode
    {
        get => Lyracist.Core.Helpers.AppSettings.LyricsVisualizerMode;
        set
        {
            Lyracist.Core.Helpers.AppSettings.LyricsVisualizerMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsModeLiveFft));
            OnPropertyChanged(nameof(IsModeSimulated));
        }
    }

    public bool IsModeLiveFft
    {
        get => VisualizerMode == "Audio Spectrum (Live FFT)";
        set
        {
            if (value)
            {
                VisualizerMode = "Audio Spectrum (Live FFT)";
            }
            else
            {
                OnPropertyChanged();
            }
        }
    }

    public bool IsModeSimulated
    {
        get => VisualizerMode == "Simulated / Ambient";
        set
        {
            if (value)
            {
                VisualizerMode = "Simulated / Ambient";
            }
            else
            {
                OnPropertyChanged();
            }
        }
    }

    public bool IsStyleNeonSunset
    {
        get => VisualizerStyle == "Neon Sunset";
        set { if (value) VisualizerStyle = "Neon Sunset"; else OnPropertyChanged(); }
    }
    public bool IsStyleCyberpunk
    {
        get => VisualizerStyle == "Cyberpunk";
        set { if (value) VisualizerStyle = "Cyberpunk"; else OnPropertyChanged(); }
    }
    public bool IsStyleEmeraldPulse
    {
        get => VisualizerStyle == "Emerald Pulse";
        set { if (value) VisualizerStyle = "Emerald Pulse"; else OnPropertyChanged(); }
    }
    public bool IsStyleSolarFlare
    {
        get => VisualizerStyle == "Solar Flare";
        set { if (value) VisualizerStyle = "Solar Flare"; else OnPropertyChanged(); }
    }
    public bool IsStyleElectricBlue
    {
        get => VisualizerStyle == "Electric Blue";
        set { if (value) VisualizerStyle = "Electric Blue"; else OnPropertyChanged(); }
    }
    public bool IsStyleRainbowSpectrum
    {
        get => VisualizerStyle == "Rainbow Spectrum";
        set { if (value) VisualizerStyle = "Rainbow Spectrum"; else OnPropertyChanged(); }
    }
    public bool IsStyleMonochromeGlow
    {
        get => VisualizerStyle == "Monochrome Glow";
        set { if (value) VisualizerStyle = "Monochrome Glow"; else OnPropertyChanged(); }
    }

    public bool IsBarWidthSlim
    {
        get => VisualizerBarWidth == "Slim";
        set { if (value) VisualizerBarWidth = "Slim"; else OnPropertyChanged(); }
    }
    public bool IsBarWidthNormal
    {
        get => VisualizerBarWidth == "Normal";
        set { if (value) VisualizerBarWidth = "Normal"; else OnPropertyChanged(); }
    }
    public bool IsBarWidthWide
    {
        get => VisualizerBarWidth == "Wide";
        set { if (value) VisualizerBarWidth = "Wide"; else OnPropertyChanged(); }
    }
    public bool IsBarWidthExtraWide
    {
        get => VisualizerBarWidth == "Extra Wide";
        set { if (value) VisualizerBarWidth = "Extra Wide"; else OnPropertyChanged(); }
    }

    public bool IsOpacity25
    {
        get => Math.Abs(VisualizerOpacity - 0.25) < 0.05;
        set { if (value) VisualizerOpacity = 0.25; else OnPropertyChanged(); }
    }
    public bool IsOpacity50
    {
        get => Math.Abs(VisualizerOpacity - 0.50) < 0.05;
        set { if (value) VisualizerOpacity = 0.50; else OnPropertyChanged(); }
    }
    public bool IsOpacity80
    {
        get => Math.Abs(VisualizerOpacity - 0.80) < 0.05;
        set { if (value) VisualizerOpacity = 0.80; else OnPropertyChanged(); }
    }
    public bool IsOpacity100
    {
        get => Math.Abs(VisualizerOpacity - 1.00) < 0.05;
        set { if (value) VisualizerOpacity = 1.00; else OnPropertyChanged(); }
    }

    private void NotifyStyleFlagsChanged()
    {
        OnPropertyChanged(nameof(IsStyleNeonSunset));
        OnPropertyChanged(nameof(IsStyleCyberpunk));
        OnPropertyChanged(nameof(IsStyleEmeraldPulse));
        OnPropertyChanged(nameof(IsStyleSolarFlare));
        OnPropertyChanged(nameof(IsStyleElectricBlue));
        OnPropertyChanged(nameof(IsStyleRainbowSpectrum));
        OnPropertyChanged(nameof(IsStyleMonochromeGlow));
    }

    private void NotifyBarWidthFlagsChanged()
    {
        OnPropertyChanged(nameof(IsBarWidthSlim));
        OnPropertyChanged(nameof(IsBarWidthNormal));
        OnPropertyChanged(nameof(IsBarWidthWide));
        OnPropertyChanged(nameof(IsBarWidthExtraWide));
    }

    private void NotifyOpacityFlagsChanged()
    {
        OnPropertyChanged(nameof(IsOpacity25));
        OnPropertyChanged(nameof(IsOpacity50));
        OnPropertyChanged(nameof(IsOpacity80));
        OnPropertyChanged(nameof(IsOpacity100));
    }

    [RelayCommand]
    public void SetVisualizerMode(string mode)
    {
        VisualizerMode = mode;
    }

    [RelayCommand]
    public void SetVisualizerStyle(string style)
    {
        VisualizerStyle = style;
    }

    [RelayCommand]
    public void SetVisualizerBarWidth(string width)
    {
        VisualizerBarWidth = width;
    }

    [RelayCommand]
    public void SetVisualizerOpacity(string opacityStr)
    {
        if (double.TryParse(opacityStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
        {
            VisualizerOpacity = val;
        }
    }

    public void NotifyBackdropChanged()
    {
        OnPropertyChanged(nameof(CdgBackdropMode));
        OnPropertyChanged(nameof(IsNebulaVisible));
        OnPropertyChanged(nameof(IsWaveformVisible));
        OnPropertyChanged(nameof(IsSynthwaveVisible));
        OnPropertyChanged(nameof(IsSpaceVisible));
    }

    public void NotifyVisualizerChanged()
    {
        OnPropertyChanged(nameof(EnableLyricsVisualizer));
        OnPropertyChanged(nameof(IsVisualizerVisible));
        OnPropertyChanged(nameof(VisualizerOpacity));
        OnPropertyChanged(nameof(VisualizerStyle));
        OnPropertyChanged(nameof(VisualizerBarWidth));
        OnPropertyChanged(nameof(VisualizerMode));
        OnPropertyChanged(nameof(IsModeLiveFft));
        OnPropertyChanged(nameof(IsModeSimulated));
        NotifyStyleFlagsChanged();
        NotifyBarWidthFlagsChanged();
        NotifyOpacityFlagsChanged();
    }
}
