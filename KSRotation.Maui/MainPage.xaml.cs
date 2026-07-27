// Edited on Jul 16, 2026 @ 11:00:00 -> Integrations for mobile rotation
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

    private async void OnAddPerformerClicked(object? sender, EventArgs e)
    {
        var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
        string name = NameEntry.Text?.Trim() ?? string.Empty;
        string song = SongEntry.Text?.Trim() ?? string.Empty;
        string artist = ArtistEntry.Text?.Trim() ?? string.Empty;

        if (!vm.TryAddPerformer(name, song, artist))
        {
            await DisplayAlertAsync("Required", "Singer name is required.", "OK");
            return;
        }

        // Clear entry fields
        NameEntry.Text = string.Empty;
        SongEntry.Text = string.Empty;
        ArtistEntry.Text = string.Empty;
    }

    private void OnDeleteSingerClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is KSRotation.Models.SingerEntry entry)
        {
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;

            bool wasCurrent = entry.IsCurrent;
            KSRotation.Models.SingerEntry? nextCurrent = null;

            if (wasCurrent)
            {
                // 1. Try the singer already flagged as Next (manual next-singer override)
                nextCurrent = vm.Singers.FirstOrDefault(s => s != entry && s.IsNext && !s.IsInactive);

                if (nextCurrent == null)
                {
                    // 2. Fall back to standard index-based rotation
                    int currentIndex = vm.Singers.IndexOf(entry);
                    int count = vm.Singers.Count;
                    for (int i = 1; i < count; i++)
                    {
                        var candidate = vm.Singers[(currentIndex + i) % count];
                        if (candidate != entry && !candidate.IsInactive)
                        {
                            nextCurrent = candidate;
                            break;
                        }
                    }
                }
            }

            vm.Singers.Remove(entry);

            if (wasCurrent)
            {
                if (nextCurrent != null)
                {
                    nextCurrent.IsCurrent = true;
                    nextCurrent.IsNext = false;
                }
                Lyracist.Shared.RotationHelpers.UpdateNextSingerHighlight(vm.Singers);
            }
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
            ("Marie Carter",     "Livin' on a Prayer",         "Bon Jovi"),
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
            ("Celeste Newsome",     "Somebody That I Used to Know", "Gotye"),
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
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;
            vm.SetCurrentSingerCommand.Execute(entry);
        }
    }

    private void OnToggleSingerInactiveClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is KSRotation.Models.SingerEntry entry)
        {
            var vm = (KSRotation.ViewModels.MainViewModel)BindingContext;

            bool pausing = !entry.IsInactive && entry.IsCurrent;
            KSRotation.Models.SingerEntry? nextCurrent = null;

            if (pausing)
            {
                // 1. Try the singer already flagged as Next (manual next-singer override)
                nextCurrent = vm.Singers.FirstOrDefault(s => s != entry && s.IsNext && !s.IsInactive);

                if (nextCurrent == null)
                {
                    // 2. Fall back to standard index-based rotation
                    int currentIndex = vm.Singers.IndexOf(entry);
                    int count = vm.Singers.Count;
                    for (int i = 1; i < count; i++)
                    {
                        var candidate = vm.Singers[(currentIndex + i) % count];
                        if (candidate != entry && !candidate.IsInactive)
                        {
                            nextCurrent = candidate;
                            break;
                        }
                    }
                }
            }

            // Toggle the inactive flag directly without triggering EnforceActiveInactiveOrder (which reorders the collection)
            entry.IsInactive = !entry.IsInactive;

            if (entry.IsInactive && entry.IsCurrent)
            {
                entry.IsCurrent = false;
            }

            if (pausing && nextCurrent != null)
            {
                nextCurrent.IsCurrent = true;
                nextCurrent.IsNext = false;
            }

            // Recalculate next singer based on new active states
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
}