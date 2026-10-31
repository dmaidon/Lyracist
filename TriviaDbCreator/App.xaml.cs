// Edited on Oct 31, 2026 @ 13:21:00 -> Call Shutdown on duplicate instance and log app startup
using System.Windows;
using Lyracist.Shared;

namespace TriviaDbCreator;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!SingleInstanceHelper.EnsureSingleInstance("TriviaDbCreator"))
        {
            Shutdown();
            return;
        }

        Globals.LogAppStart("TriviaDbCreator");
        Globals.PurgeOldLogs(14);

        TextBoxSelectionHelper.EnableGlobalSelectAllOnFocus();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstanceHelper.Cleanup();
        base.OnExit(e);
    }
}

