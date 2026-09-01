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
		// Debug.WriteLine alone is invisible on a DJ's tablet in the field with no debugger
		// attached - this is the last chance to record why the app is about to go down, so it
		// needs to land in the same persistent, on-device log file the rest of the app uses.
		var exceptionToLog = e.Exception ?? new Exception($"Unhandled exception with no Exception object. Message: {e.Message}");
		KSRotation.Services.LoggerService.LogError("KSRotation.Maui.WinUI.App.OnUnhandledException", exceptionToLog);
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

