// Edited on Oct 6, 2026 @ 10:22:30 -> Enforce single-instance application execution using SingleInstanceHelper
using System.Configuration;
using System.Data;
using System.Windows;
using Lyracist.Shared;

namespace ScaryokeWheel
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (!SingleInstanceHelper.EnsureSingleInstance("ScaryokeWheel"))
            {
                return;
            }

            TextBoxSelectionHelper.EnableGlobalSelectAllOnFocus();
            Lyracist.Shared.Globals.LogAppStart("ScaryokeWheel");
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SingleInstanceHelper.Cleanup();
            base.OnExit(e);
        }
    }
}
