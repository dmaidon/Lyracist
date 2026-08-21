// Edited on Aug 21, 2026 @ 08:26:00 -> Enable global select-all on focus for all TextBoxes and numeric boxes
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
            TextBoxSelectionHelper.EnableGlobalSelectAllOnFocus();
            base.OnStartup(e);
        }
    }
}

