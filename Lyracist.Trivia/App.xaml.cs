// Edited on Aug 19, 2026 @ 09:27:00 -> Ensure category announcement banners are generated on startup
using System.IO;
using System.Windows;
using Lyracist.Trivia.Core.Services;
using Lyracist.Trivia.Services;

namespace Lyracist.Trivia;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Lyracist.Shared.Globals.LogAppStart("Lyracist.Trivia");
        Lyracist.Shared.Globals.PurgeOldLogs(14);

        base.OnStartup(e);
        try
        {
            TriviaBannerGenerator.GenerateAllBanners();
            if (e.Args.Length > 0 && (e.Args[0] == "--generate-banners" || e.Args[0] == "--headless-banners"))
            {
                Shutdown(0);
            }
        }
        catch (System.Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist.Trivia", "App.OnStartup", ex);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        // Log and swallow so a single UI-thread fault (e.g. a bad tick handler) never takes down
        // an unattended show. Do not show a blocking MessageBox here - there may be no one at the
        // keyboard to dismiss it, and a modal dialog would freeze the game/projection indefinitely.
        Lyracist.Shared.Globals.LogError("Lyracist.Trivia", "DispatcherUnhandledException", e.Exception);
        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist.Trivia", "AppDomainUnhandledException", ex);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Lyracist.Shared.Globals.LogError("Lyracist.Trivia", "UnobservedTaskException", e.Exception);
        e.SetObserved();
    }
}
