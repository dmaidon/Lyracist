// Edited on Aug 21, 2026 @ 09:02:00 -> Add responsive portrait/vertical layout with rotation at top and QR code + requests at bottom
using System;
using System.Linq;
using Microsoft.Maui.Controls;

namespace KSRotation.Maui;

public partial class MainPage : ContentPage
{
    private KSRotation.Models.SingerEntry? _editingSinger;

    public MainPage(KSRotation.ViewModels.MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // The patron request server runs on this device for the whole session — keep the screen
        // awake so Android doesn't dim/lock and throttle it mid-show.
        Microsoft.Maui.Devices.DeviceDisplay.Current.KeepScreenOn = true;

        if (Application.Current != null)
        {
            ThemeBtn.Text = Application.Current.UserAppTheme == AppTheme.Light ? "🌙 Dark Mode" : "☀️ Light Mode";
        }

        // Safety: don't let a real, in-progress queue get wiped by an accidental "Load Test Data" tap.
        UpdateLoadTestDataEnabled(vm);
        vm.Singers.CollectionChanged += (_, _) => UpdateLoadTestDataEnabled(vm);
    }

    private void UpdateLoadTestDataEnabled(KSRotation.ViewModels.MainViewModel vm)
    {
        bool enabled = vm.Singers.Count == 0;
        LoadTestDataBtn.IsEnabled = enabled;
        LoadTestDataBtn.Opacity = enabled ? 1.0 : 0.35;
    }

    private void OnAddPerformerClicked(object? sender, EventArgs e)
    {
        AddNameEntry.Text = string.Empty;
        AddDuetEntry.Text = string.Empty;
        AddSongEntry.Text = string.Empty;
        AddArtistEntry.Text = string.Empty;
        AddSingerOverlay.IsVisible = true;
    }

    private async void OnSaveAddPerformerClicked(object? sender, EventArgs e)
    {
        var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
        string name = AddNameEntry.Text?.Trim() ?? string.Empty;
        string duet = AddDuetEntry.Text?.Trim() ?? string.Empty;
        string song = AddSongEntry.Text?.Trim() ?? string.Empty;
        string artist = AddArtistEntry.Text?.Trim() ?? string.Empty;

        if (!vm.TryAddPerformer(name, song, artist, duet))
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
            KSRotation.Models.SingerEntry? nextCurrent = null;

            if (wasCurrent)
            {
                // 1. Try the singer already flagged as Next (manual next-singer override)
                nextCurrent = vm.Singers.FirstOrDefault(s => s != entry && s.IsNext && !s.IsInactive && !s.IsPaused);

                if (nextCurrent == null)
                {
                    // 2. Fall back to standard index-based rotation
                    int currentIndex = vm.Singers.IndexOf(entry);
                    int count = vm.Singers.Count;
                    for (int i = 1; i < count; i++)
                    {
                        var candidate = vm.Singers[(currentIndex + i) % count];
                        if (candidate != entry && !candidate.IsInactive && !candidate.IsPaused)
                        {
                            nextCurrent = candidate;
                            break;
                        }
                    }
                }
            }

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
            vm.PreferredHostIp = PreferredIpEntry.Text?.Trim() ?? string.Empty;
            vm.EmailRecipient = EmailRecipientEntry.Text?.Trim() ?? string.Empty;
        }
        AboutOverlay.IsVisible = false;
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
        vm.Singers.Clear();

        (string Name, string Song, string Artist)[] testData =
        [
            ("Dennis Maidon",    "I will Be Alright", "Dennis Maidon"),
            ("Marie Hatton",     "Livin' on a Prayer",         "Bon Jovi"),
            ("Brenda Maidon",   "End of the World",     "Ann Murray"),
            ("Carlos Watson",   "Rap God",             "Eminem"),
            ("Amy Banks",     "Don't Stop Believin'",       "Journey"),
            ("Mike Hatton",    "Bohemian Rhapsody",          "Queen"),
            ("Stephen Rayner",     "Remember",        "Dennis Maidon"),
            ("Tim Honeycutt",    "Piano Man",                  "Billy Joel"),
            ("Sharon Jernigan",   "Since U Been Gone",          "Kelly Clarkson"),
            ("Randy Jernigan",      "Mr. Brightside",             "The Killers"),
            ("Todd Stowe",    "Dancing Queen",              "ABBA"),
            ("Wendy Stowe",      "Africa",                     "Toto"),
            ("Wendy Tart",    "Take It to the Limit",    "Eagles"),
            ("Sandra Moore",     "Somebody That I Used to Know", "Gotye"),
            ("Artie Davis",    "Wonderwall",                 "Oasis"),
        ];

        foreach (var (name, song, artist) in testData)
        {
            vm.Singers.Add(new KSRotation.Models.SingerEntry
            {
                Name = name,
                Song = song,
                Artist = artist
            });
        }
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
            EditSingerOverlay.IsVisible = true;
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

        _editingSinger.Name = name;
        _editingSinger.DuetPartnerName = EditDuetEntry.Text?.Trim() ?? string.Empty;
        _editingSinger.Song = EditSongEntry.Text?.Trim() ?? string.Empty;
        _editingSinger.Artist = EditArtistEntry.Text?.Trim() ?? string.Empty;

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
            if (entry.IsPaused || entry.IsInactive) return;
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
            KSRotation.Models.SingerEntry? nextCurrent = null;

            if (pausing)
            {
                // 1. Try the singer already flagged as Next (manual next-singer override)
                nextCurrent = vm.Singers.FirstOrDefault(s => s != entry && s.IsNext && !s.IsInactive && !s.IsPaused);

                if (nextCurrent == null)
                {
                    // 2. Fall back to standard index-based rotation
                    int currentIndex = vm.Singers.IndexOf(entry);
                    int count = vm.Singers.Count;
                    for (int i = 1; i < count; i++)
                    {
                        var candidate = vm.Singers[(currentIndex + i) % count];
                        if (candidate != entry && !candidate.IsInactive && !candidate.IsPaused)
                        {
                            nextCurrent = candidate;
                            break;
                        }
                    }
                }
            }

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

    private void OnFinishSingerSongClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is KSRotation.Models.SingerEntry entry)
        {
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
            vm.FinishSingerSongCommand.Execute(entry);
        }
    }

    private void OnPortalTitleTapped(object? sender, EventArgs e)
    {
        var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
        vm.IsDjQrVisible = !vm.IsDjQrVisible;
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
            }
        }
        catch (Exception ex)
        {
            // Debug.WriteLine alone is invisible on a DJ's tablet in the field with no debugger
            // attached - if this ever throws partway through (several sequential Grid mutations
            // per branch), the layout is left half-updated with zero diagnostic trail. LoggerService
            // writes to the same persistent, on-device log file the rest of the app already uses.
            System.Diagnostics.Debug.WriteLine($"UpdateOrientationLayout error: {ex.Message}");
            KSRotation.Services.LoggerService.LogError("MainPage.UpdateOrientationLayout", ex);
        }
    }
}