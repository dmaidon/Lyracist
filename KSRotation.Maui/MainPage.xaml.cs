using System;
using Microsoft.Maui.Controls;

namespace KSRotation.Maui;

public partial class MainPage : ContentPage
{
    public MainPage(KSRotation.ViewModels.MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        if (Application.Current != null)
        {
            ThemeBtn.Text = Application.Current.UserAppTheme == AppTheme.Light ? "🌙 Dark Mode" : "☀️ Light Mode";
        }
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
            vm.Singers.Remove(entry);
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
            
            // Toggle the inactive flag directly without triggering EnforceActiveInactiveOrder (which reorders the collection)
            entry.IsInactive = !entry.IsInactive;
            
            if (entry.IsInactive && entry.IsCurrent)
            {
                entry.IsCurrent = false;
            }
            
            // Recalculate next singer based on new active states
            KSRotation.Services.RotationHelpers.UpdateNextSingerHighlight(vm.Singers);
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