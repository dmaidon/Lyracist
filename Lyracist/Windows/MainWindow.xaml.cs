// Edited on Jul 19, 2026 @ 09:50:00 -> Add drive connection status indicator
using System;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using Lyracist.Services.Display;
using Lyracist.ViewModels;
using Lyracist.Views.Pages;

namespace Lyracist.Windows;

public partial class MainWindow : FluentWindow, System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    private readonly INavigationService _navigationService;
    private readonly IDisplayService _displayService;

    // Injected ViewModels
    private readonly KaraokeViewModel _karaokeViewModel;
    private readonly RotationViewModel _rotationViewModel;
    
    public KaraokeViewModel Karaoke => _karaokeViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly HelpViewModel _helpViewModel;
    private readonly AboutViewModel _aboutViewModel;

    public IRelayCommand ShowRotationCommand { get; }
    public IRelayCommand ShowLyricsCommand { get; }
    public IRelayCommand<int> MoveRotationToScreenCommand { get; }
    public IRelayCommand<int> MoveLyricsToScreenCommand { get; }

    // Drive Status Monitoring
    private System.Windows.Threading.DispatcherTimer? _driveCheckTimer;
    private string _driveStatusText = "Checking Drives...";
    private System.Windows.Media.Brush _driveStatusBrush = System.Windows.Media.Brushes.Gray;

    public string DriveStatusText
    {
        get => _driveStatusText;
        set
        {
            if (_driveStatusText != value)
            {
                _driveStatusText = value;
                OnPropertyChanged(nameof(DriveStatusText));
            }
        }
    }

    public System.Windows.Media.Brush DriveStatusBrush
    {
        get => _driveStatusBrush;
        set
        {
            if (_driveStatusBrush != value)
            {
                _driveStatusBrush = value;
                OnPropertyChanged(nameof(DriveStatusBrush));
            }
        }
    }

    public MainWindow(
        INavigationService navigationService,
        INavigationViewPageProvider pageProvider,
        IDisplayService displayService,
        KaraokeViewModel karaokeViewModel,
        RotationViewModel rotationViewModel,
        SettingsViewModel settingsViewModel,
        HelpViewModel helpViewModel,
        AboutViewModel aboutViewModel)
    {
        _navigationService = navigationService;
        _displayService = displayService;
        _karaokeViewModel = karaokeViewModel;
        _rotationViewModel = rotationViewModel;
        _settingsViewModel = settingsViewModel;
        _helpViewModel = helpViewModel;
        _aboutViewModel = aboutViewModel;

        // Initialize display commands
        ShowRotationCommand = new RelayCommand(() => _displayService.ShowRotationWindow());
        ShowLyricsCommand = new RelayCommand(() => _displayService.ShowLyricsWindow());
        MoveRotationToScreenCommand = new RelayCommand<int>(screenIndex => _displayService.MoveRotationToScreen(screenIndex));
        MoveLyricsToScreenCommand = new RelayCommand<int>(screenIndex => _displayService.MoveLyricsToScreen(screenIndex));

        InitializeComponent();

        // Expose commands to the view bindings
        DataContext = this;

        // Connect the WPF UI NavigationService to the NavigationView control
        _navigationService.SetNavigationControl(RootNavigation);

        // Set the page provider service for dynamically loading pages via DI
        RootNavigation.SetPageProviderService(pageProvider);

        Loaded += OnMainWindowLoaded;
        PreviewKeyDown += OnMainWindowPreviewKeyDown;

        Closed += (s, ev) => _driveCheckTimer?.Stop();
        StartDriveStatusMonitor();
    }

    private void OnMainWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Bypass hotkeys if the user is typing in a text input control
        var focused = System.Windows.Input.Keyboard.FocusedElement;
        if (focused is System.Windows.Controls.TextBox || focused is System.Windows.Controls.PasswordBox)
        {
            return;
        }

        // Get key string representation (e.g. "Space", "Return", "Escape", "F5", "F6")
        string keyStr = e.Key == System.Windows.Input.Key.System ? e.SystemKey.ToString() : e.Key.ToString();
        var hotkeys = Core.Helpers.AppSettings.Hotkeys;
        if (hotkeys.TryGetValue(keyStr, out string? action) && !string.IsNullOrWhiteSpace(action))
        {
            e.Handled = true;
            ExecuteHotkeyAction(action);
        }
    }

    private void ExecuteHotkeyAction(string action)
    {
        try
        {
            switch (action)
            {
                case "PlayPause":
                    if (_karaokeViewModel.IsPlaying)
                    {
                        if (_karaokeViewModel.PauseCommand.CanExecute(null))
                        {
                            _karaokeViewModel.PauseCommand.Execute(null);
                        }
                    }
                    else
                    {
                        if (_karaokeViewModel.PlayCommand.CanExecute(null))
                        {
                            _karaokeViewModel.PlayCommand.Execute(null);
                        }
                    }
                    break;
                case "Stop":
                    if (_karaokeViewModel.StopCommand.CanExecute(null))
                    {
                        _karaokeViewModel.StopCommand.Execute(null);
                    }
                    break;
                case "DoneSinger":
                    var currentSinger = _rotationViewModel.Rotation.FirstOrDefault(s => s.IsCurrent);
                    if (currentSinger != null && _rotationViewModel.DoneSingerCommand.CanExecute(currentSinger))
                    {
                        _rotationViewModel.DoneSingerCommand.Execute(currentSinger);
                    }
                    break;
                case "ToggleBanner":
                    _karaokeViewModel.ShowRotationBanner = !_karaokeViewModel.ShowRotationBanner;
                    break;
                case "ToggleLyricsWindow":
                    if (ShowLyricsCommand.CanExecute(null))
                    {
                        ShowLyricsCommand.Execute(null);
                    }
                    break;
                case "ToggleRotationWindow":
                    if (ShowRotationCommand.CanExecute(null))
                    {
                        ShowRotationCommand.Execute(null);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to execute hotkey action '{action}': {ex.Message}");
        }
    }

    private void OnMainWindowLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Disable scrollbars initially
        DisableChromeScrollbar(RootNavigation);

        // Find the hosting Frame and subscribe to its Navigated event to ensure chrome scrollbars remain disabled
        var frame = FindVisualChild<System.Windows.Controls.Frame>(RootNavigation);
        frame?.Navigated += (s, ev) => DisableChromeScrollbar(RootNavigation);

        // Navigate to the main Karaoke page once control templates are fully applied
        _navigationService.Navigate(typeof(KaraokePage));
    }

    private void DisableChromeScrollbar(DependencyObject parent)
    {
        if (parent == null) return;

        // Do not traverse into navigated pages to preserve their internal scrollbar controls
        if (parent is System.Windows.Controls.Page) return;

        if (parent is System.Windows.Controls.ScrollViewer scrollViewer)
        {
            scrollViewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
            scrollViewer.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            DisableChromeScrollbar(child);
        }
    }

    private T? FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) return null;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t)
            {
                return t;
            }
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }
        return null;
    }

    private void StartDriveStatusMonitor()
    {
        _driveCheckTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _driveCheckTimer.Tick += (s, e) => CheckDrivesAsync();
        _driveCheckTimer.Start();

        // Run initial check
        CheckDrivesAsync();
    }

    private void CheckDrivesAsync()
    {
        var dirs = Core.Helpers.AppSettings.LibraryDirectories;
        if (dirs.Count == 0)
        {
            DriveStatusText = "No Library Folders";
            DriveStatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(223, 185, 0)); // Yellow/Gold
            return;
        }

        System.Threading.Tasks.Task.Run(() =>
        {
            int existCount = 0;
            int totalCount = dirs.Count;

            foreach (var dir in dirs)
            {
                try
                {
                    if (System.IO.Directory.Exists(dir))
                    {
                        existCount++;
                    }
                }
                catch { }
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (existCount == totalCount)
                {
                    DriveStatusText = "Drives Online";
                    DriveStatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 239, 125)); // Green
                }
                else if (existCount > 0)
                {
                    DriveStatusText = $"{existCount}/{totalCount} Drives Online";
                    DriveStatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(223, 185, 0)); // Yellow/Gold
                }
                else
                {
                    DriveStatusText = "Drives Offline";
                    DriveStatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 77, 77)); // Red
                }
            }));
        });
    }
}
