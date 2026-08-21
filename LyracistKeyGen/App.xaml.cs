// Edited on Aug 21, 2026 @ 08:26:00 -> Enable global select-all on focus for all TextBoxes and numeric boxes
using System.Windows;
using Lyracist.Shared;

namespace LyracistKeyGen
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            TextBoxSelectionHelper.EnableGlobalSelectAllOnFocus();
            base.OnStartup(e);
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
        }
    }
}
