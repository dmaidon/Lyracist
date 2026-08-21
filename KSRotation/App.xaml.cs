// Edited on Aug 21, 2026 @ 08:26:00 -> Enable global select-all on focus for all TextBoxes and numeric boxes
using KSRotation.Services;
using System.Windows;
using System.Windows.Threading;
using Lyracist.Shared;
using WpfApplication = System.Windows.Application;

namespace KSRotation
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : WpfApplication
    {
        /// <inheritdoc/>
        protected override void OnStartup(StartupEventArgs e)
        {
            TextBoxSelectionHelper.EnableGlobalSelectAllOnFocus();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            LoggerService.LogAppStart();
            LoggerService.CleanupLogs();
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LoggerService.LogError("App.DispatcherUnhandledException", e.Exception);
            System.Windows.MessageBox.Show(
                $"An unexpected error occurred:\n{e.Exception.Message}\n\nThe application will continue running. Check the Logs folder for details.",
                "Unexpected Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            e.Handled = true;
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LoggerService.LogError("App.AppDomainUnhandledException", ex);
            }
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LoggerService.LogError("App.UnobservedTaskException", e.Exception);
            e.SetObserved();
        }
    }
}