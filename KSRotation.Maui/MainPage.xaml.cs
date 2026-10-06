// Edited on Oct 6, 2026 @ 11:41:00 -> Use randomized test singers pool in OnLoadTestDataClicked
using System;
using System.Linq;
using System.Threading.Tasks;
using KSRotation.Maui.Services;
using Lyracist.Shared;
using Microsoft.Maui.Controls;

namespace KSRotation.Maui;

public partial class MainPage : ContentPage
{
    private KSRotation.Models.SingerEntry? _editingSinger;

    // Port of the peer last selected from the discovered-peers list, so Import/Remote DJ connect
    // to the port that device's server actually bound to (StartRequestServer can fall back to
    // 5001/5002 if 5000 is taken) instead of always assuming 5000.
    private int _selectedPeerPort = 5000;

    public MainPage(KSRotation.ViewModels.MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // The patron request server runs on this device for the whole session — keep the screen
        // awake so Android doesn't dim/lock and throttle it mid-show.
        Microsoft.Maui.Devices.DeviceDisplay.Current.KeepScreenOn = true;

        // Restore persistent device settings for DJ and Venue until changed
        if (Microsoft.Maui.Storage.Preferences.Default.ContainsKey("DeviceDjName"))
        {
            string savedDj = Microsoft.Maui.Storage.Preferences.Default.Get("DeviceDjName", string.Empty);
            if (!string.IsNullOrWhiteSpace(savedDj))
            {
                vm.DjName = savedDj;
            }
        }
        if (Microsoft.Maui.Storage.Preferences.Default.ContainsKey("DeviceVenueName"))
        {
            string savedVenue = Microsoft.Maui.Storage.Preferences.Default.Get("DeviceVenueName", string.Empty);
            if (!string.IsNullOrWhiteSpace(savedVenue))
            {
                vm.VenueName = savedVenue;
            }
        }
        if (Microsoft.Maui.Storage.Preferences.Default.ContainsKey("DeviceListDjAndVenue"))
        {
            vm.ListDjAndVenueOnBillboard = Microsoft.Maui.Storage.Preferences.Default.Get("DeviceListDjAndVenue", true);
        }

        // Auto-detect venue based on GPS / Wi-Fi on startup
        _ = AutoDetectVenueOnStartupAsync(vm);

        if (Application.Current != null)
        {
            ThemeBtn.Text = Application.Current.UserAppTheme == AppTheme.Light ? "🌙 Dark Mode" : "☀️ Light Mode";
        }

        // Covers the "app already open" case: Android redelivers the Intent via OnNewIntent
        // without re-navigating to this page, so OnAppearing (below) never re-fires for it.
        // MainPage is created once for the life of the app, so this subscription is never removed.
        App.HandoffUriReceived += OnHandoffUriReceived;
        vm.SessionHandedOffToPeer += OnSessionHandedOffToPeer;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = CheckPendingHandoffDeepLinkAsync();
    }

    private void OnHandoffUriReceived(Uri uri)
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
        {
            // This event delivery is about to handle the link itself, so clear the cold-start
            // slot too - otherwise a later OnAppearing (e.g. after backgrounding/foregrounding)
            // would find it still set and re-prompt for a link that was already handled.
            App.PendingHandoffUri = null;
            if (BindingContext is KSRotation.ViewModels.MainViewModel vm)
            {
                await HandleHandoffUriAsync(uri, vm);
            }
        });
    }

    private async Task CheckPendingHandoffDeepLinkAsync()
    {
        if (App.PendingHandoffUri != null && BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            var uri = App.PendingHandoffUri;
            App.PendingHandoffUri = null;
            await HandleHandoffUriAsync(uri, vm);
        }
    }

    public async Task HandleHandoffUriAsync(Uri uri, KSRotation.ViewModels.MainViewModel vm)
    {
        if (uri == null) return;
        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            string host = query["host"] ?? "";
            string portStr = query["port"] ?? "5000";
            int.TryParse(portStr, out int port);
            if (port == 0) port = 5000;
            string pin = query["pin"] ?? "";

            if (string.IsNullOrWhiteSpace(host)) return;

            bool confirm = await DisplayAlertAsync(
                "Transfer Session",
                $"Incoming session handoff from {host}:{port}.\n\nImport this session and take over the rotation on this tablet?",
                "Yes, Import",
                "Cancel");

            if (confirm)
            {
                var (success, error) = await vm.PullSessionFromHostAsync(host, port, pin);
                if (success)
                {
                    await DisplayAlertAsync("Handoff Complete", "Session transferred successfully! The rotation is now active on this tablet.", "OK");
                }
                else
                {
                    await DisplayAlertAsync("Transfer Failed", $"Could not transfer session:\n{error}", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            KSRotation.Services.LoggerService.LogError("MainPage.HandleHandoffUriAsync", ex);
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler != null && BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            KSRotation.Maui.Services.SecondaryDisplayService.Instance.Initialize(vm, Handler.MauiContext!);
        }
    }

    private void OnAddPerformerClicked(object? sender, EventArgs e)
    {
        AddNameEntry.Text = string.Empty;
        AddDuetEntry.Text = string.Empty;
        AddSongEntry.Text = string.Empty;
        AddArtistEntry.Text = string.Empty;
        AddSpecialCheckBox.IsChecked = false;
        AddSingerOverlay.IsVisible = true;
        Dispatcher.Dispatch(() =>
        {
            AddNameEntry.Focus();
        });
    }

    private async void OnSaveAddPerformerClicked(object? sender, EventArgs e)
    {
        var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
        string name = AddNameEntry.Text?.Trim() ?? string.Empty;
        string duet = AddDuetEntry.Text?.Trim() ?? string.Empty;
        string song = AddSongEntry.Text?.Trim() ?? string.Empty;
        string artist = AddArtistEntry.Text?.Trim() ?? string.Empty;
        bool isSpecial = AddSpecialCheckBox.IsChecked;

        if (!vm.TryAddPerformer(name, song, artist, duet, isSpecial))
        {
            await DisplayAlertAsync("Required", "Singer name is required.", "OK");
            return;
        }

        AddSingerOverlay.IsVisible = false;
    }

    private void OnCancelAddPerformerClicked(object? sender, EventArgs e)
    {
        AddSingerOverlay.IsVisible = false;
    }

    private void OnDeleteSingerClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is KSRotation.Models.SingerEntry entry)
        {
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;

            if (entry.IsInactive)
            {
                // Restore singer
                entry.IsInactive = false;
                int oldIndex = vm.Singers.IndexOf(entry);
                if (oldIndex != -1)
                {
                    int activeCount = 0;
                    for (int i = 0; i < vm.Singers.Count; i++)
                    {
                        if (!vm.Singers[i].IsInactive && vm.Singers[i] != entry)
                        {
                            activeCount++;
                        }
                    }
                    vm.Singers.Move(oldIndex, activeCount);
                }
                Lyracist.Shared.RotationHelpers.UpdateNextSingerHighlight(vm.Singers);
                return;
            }

            bool wasCurrent = entry.IsCurrent;
            KSRotation.Models.SingerEntry? nextCurrent = wasCurrent ? RotationHelpers.FindNextEligibleSinger(vm.Singers, entry) : null;

            entry.IsInactive = true;
            entry.IsCurrent = false;
            entry.IsNext = false;

            // Move to the very end of the list
            int oldIdx = vm.Singers.IndexOf(entry);
            if (oldIdx != -1)
            {
                vm.Singers.Move(oldIdx, vm.Singers.Count - 1);
            }

            if (wasCurrent)
            {
                if (nextCurrent != null)
                {
                    nextCurrent.IsCurrent = true;
                    nextCurrent.IsNext = false;
                }
            }
            Lyracist.Shared.RotationHelpers.UpdateNextSingerHighlight(vm.Singers);
        }
    }

    private void OnAcceptRequestClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is KSRotation.Models.PatronRequest request)
        {
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
            vm.AcceptRequestCommand.Execute(request);
        }
    }

    private void OnDeclineRequestClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is KSRotation.Models.PatronRequest request)
        {
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
            vm.DeclineRequestCommand.Execute(request);
        }
    }

    private void OnThemeToggled(object? sender, EventArgs e)
    {
        if (Application.Current != null)
        {
            if (Application.Current.UserAppTheme == AppTheme.Dark)
            {
                Application.Current.UserAppTheme = AppTheme.Light;
                ThemeBtn.Text = "🌙 Dark Mode";
            }
            else
            {
                Application.Current.UserAppTheme = AppTheme.Dark;
                ThemeBtn.Text = "☀️ Light Mode";
            }
        }
    }

    private void OnAboutClicked(object? sender, EventArgs e)
    {
        if (BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            DjNameEntry.Text = vm.DjName;
            VenueNameEntry.Text = vm.VenueName;
            ListDjAndVenueCheckBox.IsChecked = vm.ListDjAndVenueOnBillboard;
            PreferredIpEntry.Text = vm.PreferredHostIp;
            EmailRecipientEntry.Text = vm.EmailRecipient;

            string? currentSsid = Lyracist.Shared.WifiHelper.GetConnectedSsid();
            IsTravelRouterCheckBox.IsChecked = !string.IsNullOrWhiteSpace(currentSsid) && Lyracist.Shared.VenueLocationStore.IsTravelRouterSsid(currentSsid);

            LocationStatusLabel.Text = "📍 Location: Checking...";
            _ = UpdateLocationStatusLabelAsync();
        }
        AboutOverlay.IsVisible = true;
    }

    private async void OnScaryokeWheelClicked(object? sender, EventArgs e)
    {
        if (BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            var page = new Views.ScaryokeWheelPage(new KSRotation.Maui.ViewModels.ScaryokeWheelViewModel(vm));
            await Shell.Current.Navigation.PushAsync(page);
        }
    }

    private void OnCloseAboutClicked(object? sender, EventArgs e)
    {
        if (BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            string dj = DjNameEntry.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(dj))
            {
                vm.DjName = dj;
                Microsoft.Maui.Storage.Preferences.Default.Set("DeviceDjName", vm.DjName);
            }

            string venue = VenueNameEntry.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(venue))
            {
                vm.VenueName = venue;
                vm.SelectedVenue = venue;
                Microsoft.Maui.Storage.Preferences.Default.Set("DeviceVenueName", vm.VenueName);
            }

            vm.ListDjAndVenueOnBillboard = ListDjAndVenueCheckBox.IsChecked;
            Microsoft.Maui.Storage.Preferences.Default.Set("DeviceListDjAndVenue", vm.ListDjAndVenueOnBillboard);

            PersistDjAndVenue(vm);

            vm.PreferredHostIp = PreferredIpEntry.Text?.Trim() ?? string.Empty;
            vm.EmailRecipient = EmailRecipientEntry.Text?.Trim() ?? string.Empty;
        }
        AboutOverlay.IsVisible = false;
    }

    private void OnShowLegendClicked(object? sender, EventArgs e)
    {
        LegendOverlay.IsVisible = true;
    }

    private void OnCloseLegendClicked(object? sender, EventArgs e)
    {
        LegendOverlay.IsVisible = false;
    }

    private void OnShowBillboardClicked(object? sender, EventArgs e)
    {
        BillboardOverlay.IsVisible = true;
    }

    private void OnCloseBillboardClicked(object? sender, EventArgs e)
    {
        BillboardOverlay.IsVisible = false;
    }

    private void OnToggleExternalDisplayClicked(object? sender, EventArgs e)
    {
        KSRotation.Maui.Services.SecondaryDisplayService.Instance.ToggleSecondaryBillboard();
    }

    private async void OnShowCastDialogClicked(object? sender, EventArgs e)
    {
        CastDeviceOverlay.IsVisible = true;
        UpdateCastStatusUI();
        await MauiCastingService.Instance.DiscoverDevicesAsync();
        RefreshCastDeviceListUI();
    }

    private void OnCloseCastDialogClicked(object? sender, EventArgs e)
    {
        CastDeviceOverlay.IsVisible = false;
    }

    private async void OnRescanCastDevicesClicked(object? sender, EventArgs e)
    {
        UpdateCastStatusUI();
        await MauiCastingService.Instance.DiscoverDevicesAsync();
        RefreshCastDeviceListUI();
    }

    private async void OnStopCastingClicked(object? sender, EventArgs e)
    {
        await MauiCastingService.Instance.StopCastingAsync();
        UpdateCastStatusUI();
        RefreshCastDeviceListUI();
    }

    private async Task CastToDeviceAsync(ChromecastDevice device)
    {
        if (BindingContext is not KSRotation.ViewModels.MainViewModel vm) return;

        string billboardUrl = vm.BillboardConnectionUrl;
        if (string.IsNullOrEmpty(billboardUrl))
        {
            await DisplayAlertAsync("Cast Error", "Billboard URL is not available. Please ensure the server is running.", "OK");
            return;
        }

        CastStatusLabel.Text = $"Connecting to {device.Name}...";
        CastScanningSpinner.IsVisible = true;
        CastScanningSpinner.IsRunning = true;

        bool success = await MauiCastingService.Instance.CastBillboardAsync(device, billboardUrl);

        CastScanningSpinner.IsRunning = false;
        CastScanningSpinner.IsVisible = false;
        UpdateCastStatusUI();
        RefreshCastDeviceListUI();

        if (!success)
        {
            await DisplayAlertAsync("Cast Connection", $"Could not launch billboard on {device.Name}. Ensure device is powered on and connected to the same Wi-Fi.", "OK");
        }
    }

    private void UpdateCastStatusUI()
    {
        var castingService = MauiCastingService.Instance;
        bool isCasting = castingService.IsCasting && castingService.ActiveDevice != null;
        CastActiveStatusCard.IsVisible = isCasting;
        if (isCasting && castingService.ActiveDevice != null)
        {
            CastActiveDeviceLabel.Text = $"📡 Currently Casting to {castingService.ActiveDevice.Name}";
        }

        CastScanningSpinner.IsVisible = castingService.IsDiscovering;
        CastScanningSpinner.IsRunning = castingService.IsDiscovering;
        CastStatusLabel.Text = string.IsNullOrEmpty(castingService.StatusMessage)
            ? (castingService.IsDiscovering ? "Scanning for devices..." : "Ready to cast")
            : castingService.StatusMessage;
    }

    private void RefreshCastDeviceListUI()
    {
        UpdateCastStatusUI();
        CastDevicesList.Children.Clear();

        var castingService = MauiCastingService.Instance;
        bool isDark = Application.Current?.UserAppTheme == AppTheme.Dark;

        if (castingService.DiscoveredDevices.Count == 0)
        {
            CastDevicesList.Children.Add(new Label
            {
                Text = castingService.IsDiscovering ? "Searching for Google Cast devices..." : "No Google Cast devices found.\nTap '🔄 Rescan' to search again.",
                TextColor = Color.FromArgb("#9CA3AF"),
                FontSize = 11,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 12, 0, 12)
            });
            return;
        }

        foreach (var device in castingService.DiscoveredDevices)
        {
            bool isThisCasting = castingService.IsCasting && castingService.ActiveDevice?.Address?.Equals(device.Address) == true;

            var card = new Border
            {
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                BackgroundColor = isDark ? Color.FromArgb("#23273A") : Color.FromArgb("#F8FAFC"),
                Stroke = isDark ? Color.FromArgb("#384260") : Color.FromArgb("#E2E8F0"),
                StrokeThickness = 1,
                Padding = new Thickness(10, 8),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var grid = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                VerticalOptions = LayoutOptions.Center
            };

            var infoStack = new VerticalStackLayout { Spacing = 2 };
            infoStack.Children.Add(new Label
            {
                Text = device.Name,
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                TextColor = isDark ? Colors.White : Color.FromArgb("#1E293B")
            });
            infoStack.Children.Add(new Label
            {
                Text = device.Address?.ToString() ?? string.Empty,
                FontSize = 10,
                TextColor = Color.FromArgb("#9CA3AF")
            });

            var castBtn = new Button
            {
                Text = isThisCasting ? "Active" : "Cast",
                BackgroundColor = isThisCasting ? Color.FromArgb("#10B981") : Color.FromArgb("#D97706"),
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                FontSize = 11,
                HeightRequest = 30,
                Padding = new Thickness(12, 0),
                CornerRadius = 6,
                IsEnabled = !isThisCasting
            };

            var targetDevice = device;
            castBtn.Clicked += async (_, _) => await CastToDeviceAsync(targetDevice);

            grid.Children.Add(infoStack);
            Grid.SetColumn(infoStack, 0);

            grid.Children.Add(castBtn);
            Grid.SetColumn(castBtn, 1);

            card.Content = grid;
            CastDevicesList.Children.Add(card);
        }
    }

    private void OnDjNameTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            string val = e.NewTextValue?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(val))
            {
                vm.DjName = val;
                Microsoft.Maui.Storage.Preferences.Default.Set("DeviceDjName", val);
                PersistDjAndVenue(vm);
            }
        }
    }

    private void OnDjNameUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry entry && BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            string val = entry.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(val))
            {
                vm.DjName = val;
                Microsoft.Maui.Storage.Preferences.Default.Set("DeviceDjName", vm.DjName);
                PersistDjAndVenue(vm);
            }
        }
    }

    private void OnDjNameCompleted(object? sender, EventArgs e)
    {
        if (sender is Entry entry && BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            string val = entry.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(val))
            {
                vm.DjName = val;
                Microsoft.Maui.Storage.Preferences.Default.Set("DeviceDjName", vm.DjName);
                PersistDjAndVenue(vm);
            }
        }
    }

    private void OnVenueNameTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            string val = e.NewTextValue?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(val))
            {
                vm.VenueName = val;
                vm.SelectedVenue = val;
                Microsoft.Maui.Storage.Preferences.Default.Set("DeviceVenueName", val);
                PersistDjAndVenue(vm);
            }
        }
    }

    private void OnVenueNameUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry entry && BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            string val = entry.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(val))
            {
                vm.VenueName = val;
                vm.SelectedVenue = val;
                Microsoft.Maui.Storage.Preferences.Default.Set("DeviceVenueName", vm.VenueName);
                PersistDjAndVenue(vm);
            }
        }
    }

    private void OnVenueNameCompleted(object? sender, EventArgs e)
    {
        if (sender is Entry entry && BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            string val = entry.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(val))
            {
                vm.VenueName = val;
                vm.SelectedVenue = val;
                Microsoft.Maui.Storage.Preferences.Default.Set("DeviceVenueName", vm.VenueName);
                PersistDjAndVenue(vm);
            }
        }
    }

    private void OnListDjAndVenueCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            vm.ListDjAndVenueOnBillboard = e.Value;
            Microsoft.Maui.Storage.Preferences.Default.Set("DeviceListDjAndVenue", e.Value);
        }
    }

    private async void OnTagLocationClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not KSRotation.ViewModels.MainViewModel vm) return;
        string venue = VenueNameEntry.Text?.Trim() ?? vm.VenueName;
        if (string.IsNullOrWhiteSpace(venue))
        {
            await DisplayAlertAsync("Tag Location", "Please enter a Venue Name first.", "OK");
            return;
        }

        vm.VenueName = venue;
        vm.SelectedVenue = venue;
        Microsoft.Maui.Storage.Preferences.Default.Set("DeviceVenueName", venue);

        string dj = DjNameEntry.Text?.Trim() ?? vm.DjName;
        if (!string.IsNullOrWhiteSpace(dj))
        {
            vm.DjName = dj;
            Microsoft.Maui.Storage.Preferences.Default.Set("DeviceDjName", dj);
        }

        PersistDjAndVenue(vm);

        LocationStatusLabel.Text = "📍 Acquiring GPS fix...";
        var coords = await Services.MauiLocationService.Instance.GetCurrentCoordinatesAsync();
        if (!coords.HasValue)
        {
            await DisplayAlertAsync("GPS Location", "Could not acquire GPS coordinates. Please ensure Location permissions and device GPS are turned on.", "OK");
            LocationStatusLabel.Text = "⚠️ No GPS fix";
            return;
        }

        string? currentSsid = Lyracist.Shared.WifiHelper.GetConnectedSsid();
        Lyracist.Shared.VenueLocationStore.UpsertVenue(venue, coords.Value.Latitude, coords.Value.Longitude, currentSsid);
        LocationStatusLabel.Text = $"📍 Tagged: {coords.Value.Latitude:F4}, {coords.Value.Longitude:F4}";
        await DisplayAlertAsync("Location Tagged", $"'{venue}' has been associated with current GPS coordinates ({coords.Value.Latitude:F4}, {coords.Value.Longitude:F4}). It will auto-retrieve on return visits!", "OK");

        // Also push to desktop host if connected
        _ = PushVenueLocationToHostAsync(vm, venue, coords.Value.Latitude, coords.Value.Longitude, currentSsid);
    }

    private void OnIsTravelRouterCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        string? currentSsid = Lyracist.Shared.WifiHelper.GetConnectedSsid();
        if (!string.IsNullOrWhiteSpace(currentSsid))
        {
            Lyracist.Shared.VenueLocationStore.SetTravelRouterSsid(currentSsid, e.Value);
        }
    }

    private async Task UpdateLocationStatusLabelAsync()
    {
        try
        {
            var coords = await Services.MauiLocationService.Instance.GetCurrentCoordinatesAsync();
            if (coords.HasValue)
            {
                string? ssid = Lyracist.Shared.WifiHelper.GetConnectedSsid();
                var matched = Lyracist.Shared.VenueLocationStore.FindMatchingVenue(coords.Value.Latitude, coords.Value.Longitude, ssid);
                if (matched != null)
                {
                    double dist = Lyracist.Shared.GeoMath.CalculateDistanceMeters(coords.Value.Latitude, coords.Value.Longitude, matched.Latitude ?? coords.Value.Latitude, matched.Longitude ?? coords.Value.Longitude);
                    LocationStatusLabel.Text = $"📍 Matched: {matched.Name} ({dist:F0}m)";
                }
                else
                {
                    LocationStatusLabel.Text = $"📍 GPS: {coords.Value.Latitude:F4}, {coords.Value.Longitude:F4} (New)";
                }
            }
            else
            {
                string? ssid = Lyracist.Shared.WifiHelper.GetConnectedSsid();
                if (!string.IsNullOrWhiteSpace(ssid))
                {
                    LocationStatusLabel.Text = $"📍 Wi-Fi: {ssid} (No GPS fix)";
                }
                else
                {
                    LocationStatusLabel.Text = "⚠️ Location unavailable";
                }
            }
        }
        catch
        {
            LocationStatusLabel.Text = "⚠️ Location unavailable";
        }
    }

    private static async Task AutoDetectVenueOnStartupAsync(KSRotation.ViewModels.MainViewModel vm)
    {
        try
        {
            var coords = await Services.MauiLocationService.Instance.GetCurrentCoordinatesAsync();
            string? ssid = Lyracist.Shared.WifiHelper.GetConnectedSsid();
            var matched = Lyracist.Shared.VenueLocationStore.FindMatchingVenue(coords?.Latitude, coords?.Longitude, ssid);
            if (matched != null && !string.IsNullOrWhiteSpace(matched.Name))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    vm.VenueName = matched.Name;
                    vm.SelectedVenue = matched.Name;
                    Microsoft.Maui.Storage.Preferences.Default.Set("DeviceVenueName", matched.Name);
                });

                if (coords.HasValue)
                {
                    _ = PushVenueLocationToHostAsync(vm, matched.Name, coords.Value.Latitude, coords.Value.Longitude, ssid);
                }
            }
        }
        catch { }
    }

    private static void PersistDjAndVenue(KSRotation.ViewModels.MainViewModel vm)
    {
        if (!string.IsNullOrWhiteSpace(vm.DjName) && !vm.Djs.Any(d => string.Equals(d, vm.DjName, StringComparison.OrdinalIgnoreCase)))
        {
            vm.Djs.Add(vm.DjName);
            KSRotation.Services.DjService.Save(vm.Djs);
        }
        if (!string.IsNullOrWhiteSpace(vm.VenueName))
        {
            if (!vm.Venues.Any(v => string.Equals(v, vm.VenueName, StringComparison.OrdinalIgnoreCase)))
            {
                vm.Venues.Add(vm.VenueName);
            }
            // Auto-save location if new venue or update existing
            _ = AutoSaveVenueLocationAsync(vm.VenueName);
            KSRotation.Services.VenueService.Save(vm.Venues);
        }
    }

    private static async Task AutoSaveVenueLocationAsync(string venueName)
    {
        try
        {
            var coords = await Services.MauiLocationService.Instance.GetCurrentCoordinatesAsync();
            string? currentSsid = Lyracist.Shared.WifiHelper.GetConnectedSsid();
            Lyracist.Shared.VenueLocationStore.UpsertVenue(venueName, coords?.Latitude, coords?.Longitude, currentSsid);
        }
        catch { }
    }

    private static async Task PushVenueLocationToHostAsync(KSRotation.ViewModels.MainViewModel vm, string venueName, double lat, double lon, string? wifiSsid)
    {
        try
        {
            string hostIp = !string.IsNullOrWhiteSpace(vm.PreferredHostIp) ? vm.PreferredHostIp : "127.0.0.1";
            string url = $"http://{hostIp}:5005/api/venue/location";
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var payload = new { venueName, latitude = lat, longitude = lon, wifiSsid };
            string json = System.Text.Json.JsonSerializer.Serialize(payload);
            using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
            await client.PostAsync(url, content);
        }
        catch { }
    }

    private void OnPreferredHostIpUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry entry && BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            vm.PreferredHostIp = entry.Text?.Trim() ?? string.Empty;
        }
    }

    private void OnPreferredHostIpCompleted(object? sender, EventArgs e)
    {
        if (sender is Entry entry && BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            vm.PreferredHostIp = entry.Text?.Trim() ?? string.Empty;
        }
    }

    private void OnEmailRecipientUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry entry && BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            vm.EmailRecipient = entry.Text?.Trim() ?? string.Empty;
        }
    }

    private void OnEmailRecipientCompleted(object? sender, EventArgs e)
    {
        if (sender is Entry entry && BindingContext is KSRotation.ViewModels.MainViewModel vm)
        {
            vm.EmailRecipient = entry.Text?.Trim() ?? string.Empty;
        }
    }

    private async void OnSaveRotationClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not KSRotation.ViewModels.MainViewModel vm)
        {
            return;
        }

        vm.EmailRecipient = EmailRecipientEntry.Text?.Trim() ?? string.Empty;

        bool confirm = await DisplayAlertAsync(
            "Save & Email Night's Report",
            "This saves a PDF/CSV report of tonight's rotation" +
            (vm.SendEmailOnSave && !string.IsNullOrWhiteSpace(vm.EmailRecipient) ? " and opens an email with it attached, " : ", ") +
            "then clears the active queue and performance history so you're ready for the next night. Continue?",
            "Yes", "No");

        if (!confirm)
        {
            return;
        }

        await vm.SaveRotationCommand.ExecuteAsync(null);
        AboutOverlay.IsVisible = false;
    }

    private async void OnResetSessionClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync("Confirm Reset", "Are you sure you want to reset everything? This will clear the active queue, performance history, incoming requests, and generate a new DJ login PIN.", "Yes", "No");
        if (confirm)
        {
            if (BindingContext is KSRotation.ViewModels.MainViewModel vm)
            {
                vm.ResetEverything();
            }
            AboutOverlay.IsVisible = false;
        }
    }

    private async void OnClearQueueClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync("Confirm Clear", "Are you sure you want to clear the entire rotation queue?", "Yes", "No");
        if (confirm)
        {
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
            vm.Singers.Clear();
        }
    }

    private void OnLoadTestDataClicked(object? sender, EventArgs e)
    {
        var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
        vm.LoadTestData(vm.TestSingerCount);
    }

    private void OnEditSingerClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is KSRotation.Models.SingerEntry entry)
        {
            _editingSinger = entry;
            EditNameEntry.Text = entry.Name;
            EditDuetEntry.Text = entry.DuetPartnerName;
            EditSongEntry.Text = entry.Song;
            EditArtistEntry.Text = entry.Artist;
            EditSpecialCheckBox.IsChecked = entry.IsSpecial;
            EditLastRoundDoneCheckBox.IsChecked = entry.HasSungInLastRound;
            EditSingerOverlay.IsVisible = true;
            Dispatcher.Dispatch(() =>
            {
                EditNameEntry.Focus();
                EditNameEntry.CursorPosition = 0;
                EditNameEntry.SelectionLength = EditNameEntry.Text?.Length ?? 0;
            });
        }
    }

    private async void OnSaveEditSingerClicked(object? sender, EventArgs e)
    {
        if (_editingSinger == null)
        {
            return;
        }

        string name = EditNameEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlertAsync("Required", "Singer name is required.", "OK");
            return;
        }

        bool becameSpecial = !_editingSinger.IsSpecial && EditSpecialCheckBox.IsChecked;
        bool lastRoundDoneChanged = _editingSinger.HasSungInLastRound != EditLastRoundDoneCheckBox.IsChecked;
        bool targetLastRoundDone = EditLastRoundDoneCheckBox.IsChecked;

        _editingSinger.Name = name;
        _editingSinger.DuetPartnerName = EditDuetEntry.Text?.Trim() ?? string.Empty;
        _editingSinger.Song = EditSongEntry.Text?.Trim() ?? string.Empty;
        _editingSinger.Artist = EditArtistEntry.Text?.Trim() ?? string.Empty;

        var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
        if (becameSpecial)
        {
            // Reuse the same command OnSetSpecialSingerClicked uses so promotion is persisted and
            // synced (RebuildRotationJsonCacheNow, QueueSaveDatabase, display window update) instead
            // of only mutating the in-memory list.
            vm.ToggleSpecialSingerCommand.Execute(_editingSinger);
        }
        else
        {
            _editingSinger.IsSpecial = EditSpecialCheckBox.IsChecked;
        }

        if (lastRoundDoneChanged)
        {
            if (!targetLastRoundDone)
            {
                vm.ClearLastRoundDoneCommand.Execute(_editingSinger);
            }
            else
            {
                vm.ToggleLastRoundDoneCommand.Execute(_editingSinger);
            }
        }

        _editingSinger = null;
        EditSingerOverlay.IsVisible = false;
    }

    private void OnCancelEditSingerClicked(object? sender, EventArgs e)
    {
        _editingSinger = null;
        EditSingerOverlay.IsVisible = false;
    }

    private void OnSetCurrentSingerClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is KSRotation.Models.SingerEntry entry)
        {
            if (entry.IsPaused || entry.IsInactive || entry.IsSkipped) return;
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
            vm.SetCurrentSingerCommand.Execute(entry);
        }
    }

    private void OnToggleSingerPausedClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is KSRotation.Models.SingerEntry entry)
        {
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;

            if (entry.IsInactive) return;

            bool pausing = !entry.IsPaused && entry.IsCurrent;
            KSRotation.Models.SingerEntry? nextCurrent = pausing ? RotationHelpers.FindNextEligibleSinger(vm.Singers, entry) : null;

            // Toggle the paused flag (retains spot in rotation)
            entry.IsPaused = !entry.IsPaused;

            if (entry.IsPaused && entry.IsCurrent)
            {
                entry.IsCurrent = false;
            }

            if (pausing && nextCurrent != null)
            {
                nextCurrent.IsCurrent = true;
                nextCurrent.IsNext = false;
            }

            // Recalculate next singer based on new active/paused states
            Lyracist.Shared.RotationHelpers.UpdateNextSingerHighlight(vm.Singers);
        }
    }

    private void OnToggleSingerSkippedClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is KSRotation.Models.SingerEntry entry)
        {
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
            vm.ToggleSkipSingerCommand.Execute(entry);
        }
    }

    private void OnToggleSingerSpecialClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is KSRotation.Models.SingerEntry entry)
        {
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
            vm.ToggleSpecialSingerCommand.Execute(entry);
        }
    }

    private void OnFinishSingerSongClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is KSRotation.Models.SingerEntry entry)
        {
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
            vm.FinishSingerSongCommand.Execute(entry);
        }
    }

    private void OnSelectPatronQrClicked(object? sender, EventArgs e)
    {
        var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
        vm.IsDjQrVisible = false;
        vm.IsKioskQrVisible = false;
        vm.IsBillboardQrVisible = false;
    }

    private void OnSelectKioskQrClicked(object? sender, EventArgs e)
    {
        var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
        vm.IsDjQrVisible = false;
        vm.IsKioskQrVisible = true;
        vm.IsBillboardQrVisible = false;
    }

    private void OnSelectBillboardQrClicked(object? sender, EventArgs e)
    {
        var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
        vm.IsDjQrVisible = false;
        vm.IsKioskQrVisible = false;
        vm.IsBillboardQrVisible = true;
    }

    private void OnSelectDjQrClicked(object? sender, EventArgs e)
    {
        var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
        vm.IsDjQrVisible = true;
        vm.IsKioskQrVisible = false;
        vm.IsBillboardQrVisible = false;
    }

    private void OnShowConnectQrClicked(object? sender, EventArgs e)
    {
        ConnectQrOverlay.IsVisible = true;
    }

    private void OnCloseConnectQrClicked(object? sender, EventArgs e)
    {
        ConnectQrOverlay.IsVisible = false;
    }

    private bool _isPortrait = false;
    private bool _hasAllocatedSize = false;

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0 || height <= 0)
            return;

        bool isPortrait = height > width;
        if (!_hasAllocatedSize || _isPortrait != isPortrait)
        {
            _hasAllocatedSize = true;
            _isPortrait = isPortrait;
            Dispatcher.Dispatch(() => UpdateOrientationLayout(isPortrait));
        }
    }

    private void UpdateOrientationLayout(bool isPortrait)
    {
        try
        {
            if (isPortrait)
            {
                // Vertical / Portrait Mode: Rotation Queue at top, QR Code and Requests at bottom
                RootLayoutGrid.ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star }
                };

                RootLayoutGrid.RowDefinitions = new RowDefinitionCollection
                {
                    new RowDefinition { Height = GridLength.Star },
                    new RowDefinition { Height = GridLength.Auto }
                };

                Grid.SetRow(RotationSectionGrid, 0);
                Grid.SetColumn(RotationSectionGrid, 0);

                Grid.SetRow(PortalAndRequestsGrid, 1);
                Grid.SetColumn(PortalAndRequestsGrid, 0);

                // Inside PortalAndRequestsGrid: Side-by-side bottom layout (QR code card on left, Requests on right)
                PortalAndRequestsGrid.RowDefinitions = new RowDefinitionCollection
                {
                    new RowDefinition { Height = new GridLength(220) }
                };

                PortalAndRequestsGrid.ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = new GridLength(170) },
                    new ColumnDefinition { Width = GridLength.Star }
                };

                Grid.SetRow(PortalConnectionCard, 0);
                Grid.SetColumn(PortalConnectionCard, 0);

                Grid.SetRow(IncomingRequestsGrid, 0);
                Grid.SetColumn(IncomingRequestsGrid, 1);
                IncomingRequestsGrid.HeightRequest = 220;

                Grid.SetRowSpan(AboutOverlay, 2);
                Grid.SetColumnSpan(AboutOverlay, 1);
                Grid.SetRowSpan(EditSingerOverlay, 2);
                Grid.SetColumnSpan(EditSingerOverlay, 1);
                Grid.SetRowSpan(AddSingerOverlay, 2);
                Grid.SetColumnSpan(AddSingerOverlay, 1);
                Grid.SetRowSpan(ConnectQrOverlay, 2);
                Grid.SetColumnSpan(ConnectQrOverlay, 1);
                Grid.SetRowSpan(LegendOverlay, 2);
                Grid.SetColumnSpan(LegendOverlay, 1);
                Grid.SetRowSpan(BillboardOverlay, 2);
                Grid.SetColumnSpan(BillboardOverlay, 1);
                Grid.SetRowSpan(SwitchDeviceOverlay, 2);
                Grid.SetColumnSpan(SwitchDeviceOverlay, 1);
            }
            else
            {
                // Horizontal / Landscape Mode: Rotation on left (*), Portal & Requests on right (280px)
                RootLayoutGrid.RowDefinitions = new RowDefinitionCollection
                {
                    new RowDefinition { Height = GridLength.Star }
                };

                RootLayoutGrid.ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = new GridLength(280) }
                };

                Grid.SetRow(RotationSectionGrid, 0);
                Grid.SetColumn(RotationSectionGrid, 0);

                Grid.SetRow(PortalAndRequestsGrid, 0);
                Grid.SetColumn(PortalAndRequestsGrid, 1);

                // Inside PortalAndRequestsGrid: Stacked vertical layout (QR code card on top, Requests list below)
                PortalAndRequestsGrid.ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star }
                };

                PortalAndRequestsGrid.RowDefinitions = new RowDefinitionCollection
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star }
                };

                Grid.SetRow(PortalConnectionCard, 0);
                Grid.SetColumn(PortalConnectionCard, 0);

                Grid.SetRow(IncomingRequestsGrid, 1);
                Grid.SetColumn(IncomingRequestsGrid, 0);
                IncomingRequestsGrid.ClearValue(VisualElement.HeightRequestProperty);

                Grid.SetRowSpan(AboutOverlay, 1);
                Grid.SetColumnSpan(AboutOverlay, 2);
                Grid.SetRowSpan(EditSingerOverlay, 1);
                Grid.SetColumnSpan(EditSingerOverlay, 2);
                Grid.SetRowSpan(AddSingerOverlay, 1);
                Grid.SetColumnSpan(AddSingerOverlay, 2);
                Grid.SetRowSpan(ConnectQrOverlay, 1);
                Grid.SetColumnSpan(ConnectQrOverlay, 2);
                Grid.SetRowSpan(LegendOverlay, 1);
                Grid.SetColumnSpan(LegendOverlay, 2);
                Grid.SetRowSpan(BillboardOverlay, 1);
                Grid.SetColumnSpan(BillboardOverlay, 2);
                Grid.SetRowSpan(SwitchDeviceOverlay, 1);
                Grid.SetColumnSpan(SwitchDeviceOverlay, 2);
            }
        }
        catch (Exception ex)
        {
            // Debug.WriteLine alone is invisible on a DJ's tablet in the field with no debugger
            // attached - if this ever throws partway through (several sequential Grid mutations
            // per branch), the layout is left half-updated with zero diagnostic trail. LoggerService
            // writes to the same persistent, on-device log file the rest of the app already uses.
            KSRotation.Services.LoggerService.LogError("MainPage.UpdateOrientationLayout", ex);
        }
    }

    private void OnSwitchDeviceClicked(object? sender, EventArgs e)
    {
        SwitchDeviceOverlay.IsVisible = true;
    }

    private void OnCloseSwitchDeviceClicked(object? sender, EventArgs e)
    {
        SwitchDeviceOverlay.IsVisible = false;
    }

    private void OnHandoffTabReceiveClicked(object? sender, EventArgs e)
    {
        HandoffReceivePanel.IsVisible = true;
        HandoffSendPanel.IsVisible = false;
        HandoffTabReceiveBtn.BackgroundColor = Color.FromArgb("#8B5CF6");
        HandoffTabReceiveBtn.TextColor = Colors.White;
        HandoffTabSendBtn.BackgroundColor = Colors.Transparent;
        HandoffTabSendBtn.TextColor = Color.FromArgb("#9CA3AF");
    }

    private void OnHandoffTabSendClicked(object? sender, EventArgs e)
    {
        HandoffReceivePanel.IsVisible = false;
        HandoffSendPanel.IsVisible = true;
        HandoffTabSendBtn.BackgroundColor = Color.FromArgb("#8B5CF6");
        HandoffTabSendBtn.TextColor = Colors.White;
        HandoffTabReceiveBtn.BackgroundColor = Colors.Transparent;
        HandoffTabReceiveBtn.TextColor = Color.FromArgb("#9CA3AF");
    }

    private async void OnScanHandoffPeersClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not KSRotation.ViewModels.MainViewModel vm) return;

        HandoffNoPeersLabel.Text = "Scanning Wi-Fi for devices...";
        HandoffPeersStack.Children.Clear();
        HandoffPeersStack.Children.Add(HandoffNoPeersLabel);

        try
        {
            await vm.DiscoverPeersOnLanAsync();
            HandoffPeersStack.Children.Clear();

            if (vm.DiscoveredPeers.Count == 0)
            {
                HandoffNoPeersLabel.Text = "No devices detected. Enter IP manually below.";
                HandoffPeersStack.Children.Add(HandoffNoPeersLabel);
            }
            else
            {
                foreach (var peer in vm.DiscoveredPeers)
                {
                    var btn = new Button
                    {
                        Text = $"{peer.VenueName} ({peer.Host}) - {peer.SingerCount} singers",
                        FontSize = 11,
                        HeightRequest = 32,
                        BackgroundColor = Color.FromArgb("#1E293B"),
                        TextColor = Color.FromArgb("#F8FAFC"),
                        CornerRadius = 6,
                        Margin = new Thickness(0, 2)
                    };
                    btn.Clicked += (_, _) =>
                    {
                        HandoffHostEntry.Text = peer.Host;
                        _selectedPeerPort = peer.Port > 0 ? peer.Port : 5000;
                        HandoffPinEntry.Focus();
                    };
                    HandoffPeersStack.Children.Add(btn);
                }
            }
        }
        catch (Exception ex)
        {
            HandoffNoPeersLabel.Text = $"Scan error: {ex.Message}";
            HandoffPeersStack.Children.Clear();
            HandoffPeersStack.Children.Add(HandoffNoPeersLabel);
        }
    }

    private async void OnExecutePullSessionClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not KSRotation.ViewModels.MainViewModel vm) return;

        string host = HandoffHostEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host) || host == "192.168.1.")
        {
            await DisplayAlertAsync("Invalid Host", "Please enter a valid Host IP address.", "OK");
            return;
        }

        string pin = HandoffPinEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pin))
        {
            await DisplayAlertAsync("DJ PIN Required", "Please enter the 4-digit DJ PIN displayed on the source device.", "OK");
            HandoffPinEntry.Focus();
            return;
        }

        bool confirm = await DisplayAlertAsync(
            "Transfer Session",
            $"Importing session from {host} will replace the current rotation and performance history on this tablet.\n\nContinue?",
            "Yes, Import",
            "Cancel");

        if (!confirm) return;

        var (success, error) = await vm.PullSessionFromHostAsync(host, _selectedPeerPort, pin);
        if (success)
        {
            SwitchDeviceOverlay.IsVisible = false;
            await DisplayAlertAsync("Handoff Complete", "Session transferred successfully! You may now continue the show on this tablet.", "OK");
        }
        else
        {
            await DisplayAlertAsync("Transfer Failed", $"Could not transfer session:\n{error}", "OK");
        }
    }

    private void OnSessionHandedOffToPeer(string peerHost, int peerPort, string peerPin)
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (BindingContext is not KSRotation.ViewModels.MainViewModel vm) return;

            if (vm.AutoSwitchToRemoteDjOnHandoff)
            {
                SwitchToRemoteDjView(peerHost, peerPort, peerPin);
            }
            else
            {
                bool answer = await DisplayAlertAsync(
                    "Session Transferred",
                    $"Your session was transferred to laptop ({peerHost}:{peerPort}).\n\nWould you like to switch this tablet to Remote DJ mode?",
                    "Switch to Remote DJ",
                    "Stay in Host Mode");

                if (answer)
                {
                    SwitchToRemoteDjView(peerHost, peerPort, peerPin);
                }
            }
        });
    }

    private void SwitchToRemoteDjView(string host, int port, string pin)
    {
        SwitchDeviceOverlay.IsVisible = false;

        string targetUrl = $"http://{host}:{port}/dj.html?pin={Uri.EscapeDataString(pin)}";
        RemoteDjHostLabel.Text = $"Connected to {host}:{port}";
        RemoteDjWebView.Source = new UrlWebViewSource { Url = targetUrl };
        RemoteDjOverlay.IsVisible = true;
    }

    private void OnConnectAsRemoteDjClicked(object? sender, EventArgs e)
    {
        string host = HandoffHostEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host) || host == "192.168.1.")
        {
            _ = DisplayAlertAsync("Invalid Host", "Please enter a valid Host IP address.", "OK");
            return;
        }

        string pin = HandoffPinEntry.Text?.Trim() ?? string.Empty;
        SwitchToRemoteDjView(host, _selectedPeerPort, pin);
    }

    private void OnHandoffHostEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        // The tracked port only applies to the peer it was captured from - once the host field
        // is hand-edited it no longer necessarily refers to that peer, so fall back to the default.
        _selectedPeerPort = 5000;
    }

    private void OnRefreshRemoteDjClicked(object? sender, EventArgs e)
    {
        RemoteDjWebView.Reload();
    }

    private async void OnExitRemoteDjClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlertAsync(
            "Exit Remote DJ Mode",
            "Do you want to exit Remote DJ mode and return to Standalone Host mode?",
            "Yes, Exit",
            "Cancel");

        if (confirm)
        {
            RemoteDjOverlay.IsVisible = false;
            RemoteDjWebView.Source = null;
        }
    }
}