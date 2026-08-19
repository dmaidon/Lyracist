// Edited on Aug 19, 2026 @ 09:27:00 -> Ensure category announcement banners are generated on startup
using System.IO;
using System.Windows;
using Lyracist.Trivia.Core.Services;
using Lyracist.Trivia.Services;

namespace Lyracist.Trivia;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            TriviaBannerGenerator.GenerateAllBanners();
            if (e.Args.Length > 0 && (e.Args[0] == "--generate-banners" || e.Args[0] == "--headless-banners"))
            {
                Shutdown(0);
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error generating category banners: {ex.Message}");
        }
    }
}
