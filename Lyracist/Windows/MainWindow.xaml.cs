using System;
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
        // Navigate to the main Karaoke page once control templates are fully applied
        _navigationService.Navigate(typeof(KaraokePage));
    }
}
