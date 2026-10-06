// Edited on Oct 6, 2026 @ 10:23:00 -> Enforce single-instance application execution using SingleInstanceHelper
using System.Windows;
using Lyracist.Shared;

namespace TriviaDbCreator;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!SingleInstanceHelper.EnsureSingleInstance("TriviaDbCreator"))
        {
            return;
        }

        TextBoxSelectionHelper.EnableGlobalSelectAllOnFocus();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstanceHelper.Cleanup();
        base.OnExit(e);
    }
}

