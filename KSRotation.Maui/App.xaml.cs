// Edited on Sep 19, 2026 @ 19:25:00 -> Raise HandoffUriReceived so a foregrounded app also reacts to the deep link
using Microsoft.Extensions.DependencyInjection;
using System;

namespace KSRotation.Maui;

public partial class App : Application
{
    /// <summary>
    /// Set for a cold-start deep link and consumed once by MainPage.OnAppearing. Kept alongside
    /// <see cref="HandoffUriReceived"/> below because OnAppearing fires before any page subscribes
    /// to that event, so a link that launched the app would otherwise be missed entirely.
    /// </summary>
    public static Uri? PendingHandoffUri { get; set; }

    /// <summary>
    /// Raised by MainActivity.OnNewIntent for a deep link received while the app is already running.
    /// PendingHandoffUri alone isn't enough for that case: OnAppearing only re-fires on navigation,
    /// not when Android redelivers an Intent to an already-foregrounded activity, so a link tapped
    /// while KSRotation.Maui is already open would otherwise be silently dropped.
    /// </summary>
    public static event Action<Uri>? HandoffUriReceived;

    public static void RaiseHandoffUriReceived(Uri uri)
    {
        PendingHandoffUri = uri;
        HandoffUriReceived?.Invoke(uri);
    }

	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}