// Edited on Oct 6, 2026 @ 12:58:00 -> Trigger welcome screen when singer name entry box loses focus instead of settle timer
// Windows-only (not linked into KSRotation.Maui): the welcome overlay is WPF.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSRotation.Models;
using Lyracist.Shared;

namespace KSRotation.ViewModels
{
    public partial class MainViewModel
    {
        // Tracks newly added placeholder singer rows awaiting name completion when the name entry box loses focus.
        private readonly List<SingerEntry> _pendingWelcomeSingers = [];

        [ObservableProperty]
        public partial bool WelcomeScreenEnabled { get; set; } = true;

        /// <summary>How long the welcome screen stays up, in seconds (3-120).</summary>
        [ObservableProperty]
        public partial int WelcomeScreenSeconds { get; set; } = WelcomeScreenService.DefaultSeconds;

        /// <summary>Device name of the monitor for the welcome; empty means the rotation & DJ banner screens.</summary>
        [ObservableProperty]
        public partial string WelcomeScreenMonitor { get; set; } = string.Empty;

        /// <summary>Chosen welcome design index (0-5), or -1 for All (Random).</summary>
        [ObservableProperty]
        public partial int WelcomeScreenDesign { get; set; } = -1;

        public ObservableCollection<WelcomeScreenChoice> WelcomeScreenChoices { get; } = [];

        public IReadOnlyList<WelcomeDesignChoice> WelcomeDesignChoices { get; } = WelcomeScreenDesigns.Choices;

        partial void OnWelcomeScreenMonitorChanged(string value)
        {
            // ComboBox rebuilds can momentarily push null; treat that as the default and let the saved value stand.
            WelcomeScreenService.Instance.TargetMonitorDevice = value ?? string.Empty;
            QueueSaveSettings();
        }

        partial void OnWelcomeScreenDesignChanged(int value)
        {
            WelcomeScreenService.Instance.SelectedDesign = value;
            QueueSaveSettings();
        }

        partial void OnWelcomeScreenEnabledChanged(bool value)
        {
            WelcomeScreenService.Instance.Enabled = value;
            QueueSaveSettings();
        }

        partial void OnWelcomeScreenSecondsChanged(int value)
        {
            int clamped = WelcomeScreenService.ClampSeconds(value);
            if (clamped != value)
            {
                WelcomeScreenSeconds = clamped; // re-enters this handler with the corrected value
                return;
            }

            WelcomeScreenService.Instance.Seconds = clamped;
            QueueSaveSettings();
        }

        private void LoadWelcomeSettings(AppSettings settings)
        {
            WelcomeScreenEnabled = settings.WelcomeScreenEnabled;
            WelcomeScreenSeconds = settings.WelcomeScreenSeconds;
            RefreshWelcomeScreenChoices();
            WelcomeScreenMonitor = settings.WelcomeScreenMonitor ?? string.Empty;
            WelcomeScreenDesign = settings.WelcomeScreenDesign;
            WelcomeScreenService.Instance.Enabled = WelcomeScreenEnabled;
            WelcomeScreenService.Instance.Seconds = WelcomeScreenSeconds;
            WelcomeScreenService.Instance.SelectedDesign = WelcomeScreenDesign;
        }

        /// <summary>Rebuilds the monitor list (call when monitors change) without losing the saved choice.</summary>
        private void RefreshWelcomeScreenChoices()
        {
            string selected = WelcomeScreenMonitor;
            WelcomeScreenChoices.Clear();
            foreach (var choice in WelcomeScreenService.GetScreenChoices()) WelcomeScreenChoices.Add(choice);
            WelcomeScreenMonitor = selected ?? string.Empty;
        }

        [RelayCommand]
        private void PreviewWelcomeScreen()
        {
            WelcomeScreenService.Instance.SelectedDesign = WelcomeScreenDesign;
            WelcomeScreenService.Instance.Preview();
        }

        /// <summary>
        /// Called for every singer added to the rotation. Named singers (patron requests, user history,
        /// DJ quick-add) are welcomed straight away; a "New Singer" placeholder is welcomed once the DJ
        /// has finished typing a real name and the name entry box loses focus (see <see cref="CommitSingerWelcome"/>).
        /// </summary>
        private void OnSingerAddedForWelcome(SingerEntry singer)
        {
            if (singer.IsSpecial || singer.IsMusic) return;

            if (WelcomeScreenService.IsPlaceholderName(singer.Name))
            {
                // Rows deleted before they were ever named would otherwise stay referenced here.
                _pendingWelcomeSingers.RemoveAll(s => !Singers.Contains(s));
                if (!_pendingWelcomeSingers.Contains(singer)) _pendingWelcomeSingers.Add(singer);
                return;
            }

            TryWelcomeSinger(singer);
        }

        /// <summary>
        /// Welcomes a singer whose name was just finished (e.g. when the singer name entry box loses focus).
        /// </summary>
        public void CommitSingerWelcome(SingerEntry singer)
        {
            if (singer.IsSpecial || singer.IsMusic) return;

            // Only welcome if this singer was a pending placeholder awaiting name entry
            if (!_pendingWelcomeSingers.Remove(singer)) return;

            if (!Singers.Contains(singer)) return;
            if (WelcomeScreenService.IsPlaceholderName(singer.Name)) return;

            TryWelcomeSinger(singer);
        }

        private void TryWelcomeSinger(SingerEntry singer)
        {
            string name = WelcomeScreenService.NormalizeName(singer.Name);

            // Someone else already in tonight's rotation under this name means they aren't new.
            bool alreadyInRotation = Singers.Any(s => !ReferenceEquals(s, singer)
                && string.Equals(WelcomeScreenService.NormalizeName(s.Name), name, StringComparison.OrdinalIgnoreCase));
            if (alreadyInRotation)
            {
                return;
            }

            WelcomeScreenService.Instance.TryWelcome(name);
        }
    }
}
