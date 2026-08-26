// Edited on Aug 25, 2026 @ 06:43:00 -> Fix projection window wiring and event handlers
using System;
using System.ComponentModel;
using System.Linq;
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
        // Subscribe to TargetMonitorChanged so when the Game Master selects a monitor in settings,
        // the TV display window immediately repositions to that physical display.
        if (DataContext is MainViewModel vm)
        {
            vm.TargetMonitorChanged += (_, _) =>
            {
                Dispatcher.Invoke(RepositionDisplayWindow);
            };

            // Hook up projection window actions
            vm.RequestOpenProjectionWindow += (_, _) =>
            {
                Dispatcher.Invoke(() => EnsureDisplayWindowOpen(vm));
            };

            // Register the DisplayViewModel on startup if the window is open
            if (_displayWindow?.DataContext is DisplayViewModel dvm)
            {
                vm.RegisterDisplayViewModel(dvm);
            }
        }
    }

    private void OpenProjectionWindow_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            EnsureDisplayWindowOpen(vm);
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void EnsureDisplayWindowOpen(MainViewModel mainVm)
    {
        if (_displayWindow == null || !_displayWindow.IsLoaded)
        {
            var displayVm = new DisplayViewModel(
                mainVm.Engine,
                mainVm.VenueName,
                mainVm.ConnectUrl,
                mainVm.QrCodeImage,
                mainVm.WifiSsid,
                mainVm.WifiPassword,
                mainVm.PreGameSecondsRemaining,
                mainVm.HostName);

            _displayWindow = new TriviaDisplayWindow
            {
                DataContext = displayVm
            };
            _displayWindow.Closed += (_, _) =>
            {
                displayVm.Dispose();
                _displayWindow = null;
                mainVm.RegisterDisplayViewModel(null);
            };
            mainVm.RegisterDisplayViewModel(displayVm);
            RepositionDisplayWindow();
            _displayWindow.Show();
            mainVm.OnProjectionOpened();
        }
        else
        {
            RepositionDisplayWindow();
            _displayWindow.Activate();
            mainVm.OnProjectionOpened();
        }
    }

    private void RepositionDisplayWindow()
    {
        if (_displayWindow == null) return;

        if (DataContext is MainViewModel vm)
        {
            string targetDevice = vm.Settings.SelectedMonitorDevice;
            Screen targetScreen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == targetDevice)
                ?? Screen.PrimaryScreen
                ?? Screen.AllScreens[0];

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
        catch (Exception ex)
        {
            _ = ex;
        }

        if (DataContext is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                _ = ex;
            }
        }

        System.Windows.Application.Current?.Shutdown();
    }
}
