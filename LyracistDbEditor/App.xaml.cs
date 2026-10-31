// Edited on Oct 31, 2026 @ 13:18:30 -> Call Shutdown on duplicate instance and log app start
using System;
using System.Windows;
using Lyracist.Data;
using Lyracist.Shared;

namespace LyracistDbEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (!SingleInstanceHelper.EnsureSingleInstance("LyracistDbEditor"))
            {
                Shutdown();
                return;
            }

            Globals.LogAppStart("LyracistDbEditor");
            Globals.PurgeOldLogs(14);

            try
            {
                LyracistDbContext.EnsureSchemaAndRepair();
            }
            catch (Exception ex)
            {
                Globals.LogError("LyracistDbEditor", "Startup database migration failed", ex);
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
}
