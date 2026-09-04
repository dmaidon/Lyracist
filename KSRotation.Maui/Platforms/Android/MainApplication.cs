// Edited on Sep 3, 2026 @ 12:22:00 -> Guard with #if ANDROID to prevent cross-framework active document analysis errors
#if ANDROID
using Android.App;
using Android.Runtime;

namespace KSRotation.Maui;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
#endif
