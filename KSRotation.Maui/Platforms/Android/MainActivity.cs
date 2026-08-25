// Edited on Aug 25, 2026 @ 06:15:00 -> Fix RCS1251 empty block in MainActivity.cs
using Android.App;
using Android.Content.PM;
using Android.OS;

namespace KSRotation.Maui;

[Activity(Theme = "@style/MainTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity;
