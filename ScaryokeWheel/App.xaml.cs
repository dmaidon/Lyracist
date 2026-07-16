using System.Configuration;
using System.Data;
using System.Windows;

namespace ScaryokeWheel
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Lyracist.Shared.Globals.LogAppStart("ScaryokeWheel");
            base.OnStartup(e);
        }
    }

}
