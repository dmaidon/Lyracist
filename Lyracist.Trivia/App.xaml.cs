// Edited on Oct 6, 2026 @ 10:22:00 -> Enforce single-instance application execution using SingleInstanceHelper
using System.IO;
using System.Windows;
using Lyracist.Shared;
using Lyracist.Trivia.Core.Services;
using Lyracist.Trivia.Services;

namespace Lyracist.Trivia;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!SingleInstanceHelper.EnsureSingleInstance("LyracistTrivia"))
        {
            return;
        }

        TextBoxSelectionHelper.EnableGlobalSelectAllOnFocus();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Lyracist.Shared.Globals.LogAppStart("Lyracist.Trivia");
        Lyracist.Shared.Globals.PurgeOldLogs(14);

        base.OnStartup(e);
        try
        {
            bool forceRegenerateBanners = e.Args.Length > 0 && (e.Args[0] == "--generate-banners" || e.Args[0] == "--headless-banners");
            TriviaBannerGenerator.GenerateAllBanners(force: forceRegenerateBanners);
            if (forceRegenerateBanners)
            {
                Shutdown(0);
            }
        }
        catch (System.Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist.Trivia", "App.OnStartup", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstanceHelper.Cleanup();
        base.OnExit(e);
        Environment.Exit(0);
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
