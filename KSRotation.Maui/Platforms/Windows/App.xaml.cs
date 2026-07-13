using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace KSRotation.Maui.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		// Subscribed before InitializeComponent so this runs before the generated
		// Debugger.Break() hook, printing the real exception to the Output window.
		this.UnhandledException += OnUnhandledException;
		this.InitializeComponent();
	}

	private static void OnUnhandledException(object? sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
	{
		System.Diagnostics.Debug.WriteLine("=== UNHANDLED EXCEPTION ===");
		System.Diagnostics.Debug.WriteLine($"Message: {e.Message}");
		System.Diagnostics.Debug.WriteLine(e.Exception?.ToString() ?? "(no Exception object available)");
		System.Diagnostics.Debug.WriteLine("=== END UNHANDLED EXCEPTION ===");
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

