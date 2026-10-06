// Edited on Oct 6, 2026 @ 10:24:00 -> Enforce single-instance application execution using SingleInstanceHelper
using System.Windows;
using Lyracist.Shared;

namespace LyracistKeyGen
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (!SingleInstanceHelper.EnsureSingleInstance("LyracistKeyGen"))
            {
                return;
            }

            TextBoxSelectionHelper.EnableGlobalSelectAllOnFocus();
            base.OnStartup(e);
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SingleInstanceHelper.Cleanup();
            base.OnExit(e);
        }
    }
}
