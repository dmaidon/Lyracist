// Edited on Jul 30, 2026 @ 07:25:00 -> Restored Theme to Maui.SplashTheme to enable custom splash screen
using Android.App;
using Android.Content.PM;
using Android.OS;

namespace KSRotation.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
