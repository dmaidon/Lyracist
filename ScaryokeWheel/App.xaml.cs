// Edited on Oct 24, 2026 @ 15:05:00 -> Qualify System.Windows.Application to avoid ambiguity with Windows Forms
using System.Configuration;
using System.Data;
using System.Windows;
using Lyracist.Shared;

namespace ScaryokeWheel
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
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
