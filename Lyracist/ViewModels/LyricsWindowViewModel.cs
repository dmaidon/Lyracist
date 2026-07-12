using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Services.Display;

namespace Lyracist.ViewModels;

public sealed record MonitorOption(int Index, string Label);

public partial class LyricsWindowViewModel : ObservableObject
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
            System.Diagnostics.Debug.WriteLine($"Failed to generate QR Code: {ex.Message}");
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
    private string _slideshowImagePath = string.Empty;

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
            SlideshowImagePath = string.Empty;
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
        SlideshowImagePath = string.Empty;
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

    public void NotifyBackdropChanged()
    {
        OnPropertyChanged(nameof(CdgBackdropMode));
        OnPropertyChanged(nameof(IsNebulaVisible));
        OnPropertyChanged(nameof(IsWaveformVisible));
        OnPropertyChanged(nameof(IsSynthwaveVisible));
        OnPropertyChanged(nameof(IsSpaceVisible));
    }
}
