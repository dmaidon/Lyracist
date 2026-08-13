// Edited on Aug 12, 2026 @ 10:25:00 -> Set Theme to @style/MainTheme to bypass splash screen per user request
using Android.App;
using Android.Content.PM;
using Android.OS;

namespace KSRotation.Maui;

[Activity(Theme = "@style/MainTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
