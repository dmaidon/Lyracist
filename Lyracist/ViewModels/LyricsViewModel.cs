// Edited on Aug 6, 2026 @ 07:01:27 -> Document that the FrameReady subscription is intentionally permanent (AddSingleton lifetime)
using System;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Interfaces;
using Lyracist.Services.Display;
using Lyracist.Services.Tablet;

namespace Lyracist.ViewModels;

public partial class LyricsViewModel : ObservableObject
{
    private readonly IMediaEngine _media;
    private readonly IDisplayService _display;
    private readonly ITabletLyricsServer _tablet;

    [ObservableProperty]
    private ImageSource? _framePreview;

    [ObservableProperty]
    private string _currentLine = "🎵 [CDG Graphics Active] 🎵";

    [ObservableProperty]
    private string _nextLine = "[Live Rendering from CDG Subcodes]";

    [ObservableProperty]
    private string _position = "00:00";

    [ObservableProperty]
    private bool _isTabletSyncEnabled = true;

    [ObservableProperty]
    private bool _isLyricsWindowSyncEnabled = true;

    public LyricsViewModel(IMediaEngine media,
                           IDisplayService display,
                           ITabletLyricsServer tablet)
    {
        _media = media;
        _display = display;
        _tablet = tablet;

        // Never unsubscribed: LyricsViewModel is registered AddSingleton in App.xaml.cs, so
        // exactly one instance exists for the app's lifetime and this subscription is meant
        // to live as long as the process does.
        _media.FrameReady += OnFrameReady;
    }

    private void OnFrameReady(ImageSource frame)
    {
        FramePreview = frame;
        Position = "Playing";

        if (IsTabletSyncEnabled)
        {
            _ = SendFrameToTablet();
        }

        if (IsLyricsWindowSyncEnabled)
        {
            _display.UpdateLyricsFrame(frame);
        }
    }

    [RelayCommand]
    private void RefreshPreview()
    {
        // Force refresh via display service/media engine
        if (FramePreview != null)
        {
            _display.UpdateLyricsFrame(FramePreview);
        }
    }

    [RelayCommand]
    private async Task ToggleTabletSync()
    {
        IsTabletSyncEnabled = !IsTabletSyncEnabled;
        if (IsTabletSyncEnabled)
        {
            await SendFrameToTablet();
        }
    }

    [RelayCommand]
    private void ToggleLyricsWindowSync()
    {
        IsLyricsWindowSyncEnabled = !IsLyricsWindowSyncEnabled;
        if (IsLyricsWindowSyncEnabled && FramePreview != null)
        {
            _display.UpdateLyricsFrame(FramePreview);
        }
    }

    [RelayCommand]
    private async Task SendFrameToTablet()
    {
        await _tablet.BroadcastLyricsAsync(new LyricsMessage
        {
            Title = "Active CDG Track",
            CurrentLine = CurrentLine,
            NextLine = NextLine,
            Position = TimeSpan.Zero
        });
    }

    [RelayCommand]
    private void SendFrameToLyricsWindow()
    {
        if (FramePreview != null)
        {
            _display.UpdateLyricsFrame(FramePreview);
        }
    }
}
