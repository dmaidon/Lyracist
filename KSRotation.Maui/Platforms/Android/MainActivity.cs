// Edited on Sep 3, 2026 @ 12:22:00 -> Guard with #if ANDROID to prevent cross-framework active document analysis errors
#if ANDROID
using Android.App;
using Android.Content.PM;
using Android.OS;

namespace KSRotation.Maui;

[Activity(Theme = "@style/MainTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity;
#endif
