// Edited on Oct 6, 2026 @ 10:23:30 -> Enforce single-instance application execution using SingleInstanceHelper
using System.Configuration;
using System.Data;
using System.Windows;
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

