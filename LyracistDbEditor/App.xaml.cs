// Edited on Oct 30, 2026 @ 12:05:00 -> Run EnsureSchemaAndRepair on startup to self-heal legacy or locked databases
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
                return;
            }

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
