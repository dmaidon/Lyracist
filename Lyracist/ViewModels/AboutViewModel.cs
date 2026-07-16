using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Lyracist.ViewModels;

public partial class AboutViewModel : BaseViewModel
{
    private readonly Assembly _assembly = Assembly.GetExecutingAssembly();

    [RelayCommand]
    private void OpenAboutWindow()
    {
        var window = App.AppHost.Services.GetRequiredService<Windows.AboutWindow>();
        window.Owner = System.Windows.Application.Current.MainWindow;
        window.ShowDialog();
    }

    public string AppName => _assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Lyracist";

    public string Version
    {
        get
        {
            var fileVersion = _assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            return !string.IsNullOrEmpty(fileVersion) ? $"Version {fileVersion}" : "Version 1.0.0";
        }
    }

    public string Author => "Dennis Maidon";

    public string Company => _assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "PAROLE Software";

    public string FrameworkVersion => ".NET 10.0-windows";

    public string Description => "A high-performance, modern karaoke player and graphic lyrics projection system built with Fluent UI aesthetics.";

    public string Copyright => Lyracist.Shared.Globals.Copyright;
}
