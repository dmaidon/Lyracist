using System.Reflection;

namespace Lyracist.ViewModels;

public class AboutViewModel : BaseViewModel
{
    private readonly Assembly _assembly = Assembly.GetExecutingAssembly();

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

    public string Copyright => _assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "© 2026 PAROLE Software";
}
