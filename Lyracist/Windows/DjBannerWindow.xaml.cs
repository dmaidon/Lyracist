// Edited on Sep 22, 2026 @ 08:46:30 -> Set BannerImage.Source directly and listen to BannerImage property changes
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Lyracist.Services.Display;
using Lyracist.ViewModels;

namespace Lyracist.Windows;

public partial class DjBannerWindow : Window
{
    private readonly DjBannerWindowViewModel _vm;

    public DjBannerWindow(DjBannerWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.PropertyChanged += Vm_PropertyChanged;
        UpdateBannerView();
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DjBannerWindowViewModel.BannerPath) ||
            e.PropertyName == nameof(DjBannerWindowViewModel.BannerImage))
        {
            if (Dispatcher.CheckAccess())
            {
                UpdateBannerView();
            }
            else
            {
                Dispatcher.Invoke(UpdateBannerView);
            }
        }
    }

    private void UpdateBannerView()
    {
        string? path = _vm.BannerPath;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            BannerImage.Source = null;
            BannerVideo.Source = null;
            BannerVideo.Visibility = Visibility.Collapsed;
            BannerImage.Visibility = Visibility.Visible;
            return;
        }

        string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".mp4")
        {
            BannerImage.Source = null;
            BannerImage.Visibility = Visibility.Collapsed;
            BannerVideo.Visibility = Visibility.Visible;
            BannerVideo.Source = new Uri(path);
            BannerVideo.Play();
        }
        else
        {
            BannerVideo.Stop();
            BannerVideo.Source = null;
            BannerVideo.Visibility = Visibility.Collapsed;
            BannerImage.Visibility = Visibility.Visible;
            BannerImage.Source = _vm.BannerImage;
        }
    }

    private void BannerVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        BannerVideo.Position = TimeSpan.Zero;
        BannerVideo.Play();
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                {
                    var displayService = App.AppHost.Services.GetService(typeof(IDisplayService)) as IDisplayService;
                    if (displayService != null)
                    {
                        displayService.IsDjBannerActive = false;
                    }
                    else
                    {
                        Hide();
                    }
                }
                e.Handled = true;
                break;
            case Key.F11:
                ToggleFullscreen();
                e.Handled = true;
                break;
        }

        base.OnKeyDown(e);
    }

    public bool IsShuttingDown { get; set; }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (IsShuttingDown)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        var displayService = App.AppHost.Services.GetService(typeof(IDisplayService)) as IDisplayService;
        if (displayService != null)
        {
            displayService.IsDjBannerActive = false;
        }
        else
        {
            Hide();
        }
        base.OnClosing(e);
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
