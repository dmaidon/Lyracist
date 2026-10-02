// Edited on Oct 2, 2026 @ 10:55:00 -> Add WelcomeScreenDesign selection and persistence
// Windows-only (not linked into KSRotation.Maui): the welcome overlay is WPF.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSRotation.Models;
using Lyracist.Shared;

namespace KSRotation.ViewModels
{
    public partial class MainViewModel
    {
        // How long the DJ must stop typing a new singer's name before it counts as committed. The Name
        // property updates on every keystroke, so greeting immediately would welcome "J", then "Jo"...
        private static readonly TimeSpan WelcomeNameSettleDelay = TimeSpan.FromSeconds(2.5);

        // A list (not a set) so several names entered in a row are welcomed in the order they were added.
        private readonly List<SingerEntry> _pendingWelcomeSingers = [];
        private DispatcherTimer? _welcomeNameSettleTimer;

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
        /// has finished typing a real name (see <see cref="OnPlaceholderSingerRenamed"/>).
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

        /// <summary>Called when a singer's Name changes; only rows added as placeholders are tracked.</summary>
        private void OnPlaceholderSingerRenamed(SingerEntry singer)
        {
            if (!_pendingWelcomeSingers.Contains(singer)) return;

            if (_welcomeNameSettleTimer == null)
            {
                _welcomeNameSettleTimer = new DispatcherTimer { Interval = WelcomeNameSettleDelay };
                _welcomeNameSettleTimer.Tick += OnWelcomeNameSettled;
            }
            _welcomeNameSettleTimer.Stop();
            _welcomeNameSettleTimer.Start();
        }

        private void OnWelcomeNameSettled(object? sender, EventArgs e)
        {
            _welcomeNameSettleTimer?.Stop();

            foreach (var singer in _pendingWelcomeSingers.ToList())
            {
                if (!Singers.Contains(singer))
                {
                    _pendingWelcomeSingers.Remove(singer); // removed before it was ever named
                    continue;
                }

                if (WelcomeScreenService.IsPlaceholderName(singer.Name)) continue; // still unnamed

                _pendingWelcomeSingers.Remove(singer);
                TryWelcomeSinger(singer);
            }
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
