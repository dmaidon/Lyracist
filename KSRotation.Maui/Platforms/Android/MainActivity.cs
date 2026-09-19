// Edited on Sep 19, 2026 @ 18:02:00 -> Add deep linking intent filter and handler for ksrotation://handoff
#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using System;

namespace KSRotation.Maui;

[Activity(Theme = "@style/MainTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter([Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "ksrotation",
    DataHost = "handoff")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ProcessIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        ProcessIntent(intent);
    }

    private void ProcessIntent(Intent? intent)
    {
        if (intent?.DataString != null && Uri.TryCreate(intent.DataString, UriKind.Absolute, out var uri))
        {
            App.RaiseHandoffUriReceived(uri);
        }
    }
}
#endif
