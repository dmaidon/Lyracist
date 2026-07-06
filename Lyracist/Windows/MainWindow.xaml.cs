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

public partial class MainWindow : FluentWindow
{
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
    }

    private void OnMainWindowLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Disable scrollbars initially
        DisableChromeScrollbar(RootNavigation);

        // Find the hosting Frame and subscribe to its Navigated event to ensure chrome scrollbars remain disabled
        var frame = FindVisualChild<System.Windows.Controls.Frame>(RootNavigation);
        if (frame != null)
        {
            frame.Navigated += (s, ev) => DisableChromeScrollbar(RootNavigation);
        }

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
}
