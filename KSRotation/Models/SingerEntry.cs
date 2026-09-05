// Edited on Sep 3, 2026 @ 23:48:45 -> Add HasSungInLastRound property for Last Round mode
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace KSRotation.Models
{
    public record QueuedSong(string Song, string Artist);

    public partial class SingerEntry : ObservableObject, Lyracist.Shared.IRotationSinger
    {
        [ObservableProperty]
        public partial string Email { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string PinCode { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string AvatarType { get; set; } = "None";

        [ObservableProperty]
        public partial string AvatarSource { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string VocalRange { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string CustomTitle { get; set; } = string.Empty;

        private string _duetPartnerName = string.Empty;
        public string DuetPartnerName
        {
            get => _duetPartnerName;
            set
            {
                if (SetProperty(ref _duetPartnerName, ProperCase(value)))
                {
                    OnPropertyChanged(nameof(Partner));
                    OnPropertyChanged(nameof(IsDuet));
                    OnPropertyChanged(nameof(DisplayNameWithDuet));
                }
            }
        }

        public string Partner
        {
            get => DuetPartnerName;
            set => DuetPartnerName = value;
        }

        public bool IsDuet => !string.IsNullOrWhiteSpace(DuetPartnerName) && !DuetPartnerName.Equals("None", StringComparison.OrdinalIgnoreCase);

        public string DisplayNameWithDuet => IsDuet ? $"{Name} (with {DuetPartnerName})" : Name;
        /// <summary>Stable identity assigned once at construction; never changes even when Name is edited.
        /// Declared as <c>init</c> so JSON deserialization can round-trip it, while preventing accidental mutation in code.</summary>
        public Guid Id { get; init; } = Guid.NewGuid();

        public List<QueuedSong> QueuedSongs { get; set; } = [];

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, ProperCase(value)))
                {
                    OnPropertyChanged(nameof(DisplayNameWithDuet));
                }
            }
        }

        private string _song = string.Empty;
        public string Song
        {
            get => _song;
            set => SetProperty(ref _song, ProperCase(value));
        }

        private string _artist = string.Empty;
        public string Artist
        {
            get => _artist;
            set => SetProperty(ref _artist, ProperCase(value));
        }

        private static string ProperCase(string? input) => Lyracist.Shared.NameFormatting.ProperCase(input);

        /// <summary>True when this singer is the one currently at the mic.</summary>
        [ObservableProperty]
        public partial bool IsCurrent { get; set; }

        /// <summary>True when this singer has been marked out for the night (grayed out, not deleted).</summary>
        [ObservableProperty]
        public partial bool IsInactive { get; set; }

        /// <summary>True when this singer is queued up as the next to sing after the current one finishes.</summary>
        [ObservableProperty]
        public partial bool IsNext { get; set; }

        [ObservableProperty]
        public partial bool IsPaused { get; set; }

        [ObservableProperty]
        public partial bool IsMusic { get; set; }

        /// <summary>This entry's own estimated performance length in seconds (queued song's known
        /// duration + 30s), or &lt;= 0 if unknown - KSRotation has no song-duration library, so this
        /// stays 0 and <see cref="Lyracist.Shared.RotationHelpers.RecalculateEstimatedWaits{T}"/>
        /// falls back to its default per-song estimate.</summary>
        [ObservableProperty]
        public partial double EstimatedPerformanceSeconds { get; set; }

        /// <summary>Computed estimated wait, in whole minutes, until this singer is up - set by
        /// <see cref="Lyracist.Shared.RotationHelpers.RecalculateEstimatedWaits{T}"/>.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasEstimatedWait))]
        public partial int EstimatedWaitMinutes { get; set; }

        public bool HasEstimatedWait => EstimatedWaitMinutes > 0;

        /// <summary>True when this singer marks the start / 1st position of the rotation round.</summary>
        [ObservableProperty]
        public partial bool IsRotationStart { get; set; }

        /// <summary>True when this singer has completed their turn in the final round of the night.</summary>
        [ObservableProperty]
        public partial bool HasSungInLastRound { get; set; }

        [ObservableProperty] public partial bool Song1Completed { get; set; }
        [ObservableProperty] public partial bool Song2Completed { get; set; }
        [ObservableProperty] public partial bool Song3Completed { get; set; }
        [ObservableProperty] public partial bool Song4Completed { get; set; }
        [ObservableProperty] public partial bool Song5Completed { get; set; }
        [ObservableProperty] public partial bool Song6Completed { get; set; }
        [ObservableProperty] public partial bool Song7Completed { get; set; }
        [ObservableProperty] public partial bool Song8Completed { get; set; }
        [ObservableProperty] public partial bool Song9Completed { get; set; }
        [ObservableProperty] public partial bool Song10Completed { get; set; }

        /// <summary>Returns the completion flag for a 1-based round number without allocating.</summary>
        private bool GetRoundCompleted(int round) => round switch
        {
            1 => Song1Completed,
            2 => Song2Completed,
            3 => Song3Completed,
            4 => Song4Completed,
            5 => Song5Completed,
            6 => Song6Completed,
            7 => Song7Completed,
            8 => Song8Completed,
            9 => Song9Completed,
            10 => Song10Completed,
            _ => false
        };

        /// <summary>Returns the 1-based round number of the first incomplete round, or 0 if all are complete.</summary>
        public int GetNextIncompleteRound()
        {
            for (int round = 1; round <= 10; round++)
            {
                if (!GetRoundCompleted(round)) return round;
            }
            return 0;
        }

        /// <summary>Marks the specified 1-based round as completed.</summary>
        public void MarkRoundCompleted(int round)
        {
            switch (round)
            {
                case 1: Song1Completed = true; break;
                case 2: Song2Completed = true; break;
                case 3: Song3Completed = true; break;
                case 4: Song4Completed = true; break;
                case 5: Song5Completed = true; break;
                case 6: Song6Completed = true; break;
                case 7: Song7Completed = true; break;
                case 8: Song8Completed = true; break;
                case 9: Song9Completed = true; break;
                case 10: Song10Completed = true; break;
            }
        }

        /// <summary>Sets the completion status of the specified 1-based round.</summary>
        public void SetRoundCompleted(int round, bool completed)
        {
            switch (round)
            {
                case 1: Song1Completed = completed; break;
                case 2: Song2Completed = completed; break;
                case 3: Song3Completed = completed; break;
                case 4: Song4Completed = completed; break;
                case 5: Song5Completed = completed; break;
                case 6: Song6Completed = completed; break;
                case 7: Song7Completed = completed; break;
                case 8: Song8Completed = completed; break;
                case 9: Song9Completed = completed; break;
                case 10: Song10Completed = completed; break;
            }
        }

        /// <summary>Returns the highest completed 1-based round number, or 0 if none are complete.</summary>
        public int GetHighestCompletedRound()
        {
            for (int round = 10; round >= 1; round--)
            {
                if (GetRoundCompleted(round)) return round;
            }
            return 0;
        }

        /// <summary>Returns whether the given 1-based round number has been completed.</summary>
        public bool IsRoundCompleted(int round)
        {
            if (round < 1 || round > 10) return false;
            return GetRoundCompleted(round);
        }
    }
}
