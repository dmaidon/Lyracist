// Edited on Aug 20, 2026 @ 12:17:00 -> Added ExitButton_Click and explicit Application.Current.Shutdown on close
using System;
using System.ComponentModel;
using System.Windows;
using Lyracist.Shared;
using Lyracist.Trivia.ViewModels;
using Screen = System.Windows.Forms.Screen;

namespace Lyracist.Trivia.Windows;

public partial class MainWindow : Window
{
    private TriviaDisplayWindow? _displayWindow;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.TargetMonitorChanged += (s, deviceName) =>
            {
                Dispatcher.Invoke(() => PositionDisplayWindow(deviceName));
            };
            vm.RequestOpenProjectionWindow += (s, e) =>
            {
                Dispatcher.Invoke(() => OpenProjectionWindow_Click(this, new RoutedEventArgs()));
            };
        }
    }

    private void OpenProjectionWindow_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Start pre-game countdown when screen is cast to monitor
        vm.OnProjectionOpened();

        if (_displayWindow == null || !_displayWindow.IsLoaded)
        {
            var displayVm = new DisplayViewModel(vm.Engine, vm.VenueName, vm.ConnectUrl, vm.QrCodeImage, vm.WifiSsid, vm.WifiPassword, vm.PreGameSecondsRemaining);
            vm.RegisterDisplayViewModel(displayVm);
            _displayWindow = new TriviaDisplayWindow
            {
                DataContext = displayVm
            };
            _displayWindow.Closed += (s, e) =>
            {
                _displayWindow = null;
            };
            PositionDisplayWindow(vm.SelectedMonitor?.DeviceName);
            _displayWindow.Show();
            PositionDisplayWindow(vm.SelectedMonitor?.DeviceName);
        }
        else
        {
            if (_displayWindow.DataContext is DisplayViewModel existingDvm)
            {
                vm.RegisterDisplayViewModel(existingDvm);
            }
            PositionDisplayWindow(vm.SelectedMonitor?.DeviceName);
            _displayWindow.Activate();
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PositionDisplayWindow(string? deviceName)
    {
        if (_displayWindow == null) return;

        var screens = Screen.AllScreens;
        var targetScreen = WindowPositioner.ResolveByDeviceName(screens, deviceName);
        if (targetScreen != null)
        {
            _displayWindow.WindowState = WindowState.Normal;
            _displayWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            WindowPositioner.FillArea(_displayWindow, targetScreen.Bounds);
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        try
        {
            _displayWindow?.Close();
        }
        catch { }

        if (DataContext is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch { }
        }

        System.Windows.Application.Current?.Shutdown();
    }
}
