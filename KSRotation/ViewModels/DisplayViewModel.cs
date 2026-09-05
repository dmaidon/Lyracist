// Edited on Sep 3, 2026 @ 23:50:35 -> Add IsLastRound property and filter out completed singers in Last Round mode
using CommunityToolkit.Mvvm.ComponentModel;
using KSRotation.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KSRotation.ViewModels
{
    public partial class DisplayViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial bool IsLastRound { get; set; }

        [ObservableProperty]
        public partial string CurrentSinger { get; set; } = "No singer selected";

        // Split name/song for the Marquee view's large, two-line layout.
        [ObservableProperty]
        public partial string CurrentSingerName { get; set; } = "No singer";

        // Song + artist combined (e.g. "Song – Artist"), used by the Marquee/Vinyl side panels.
        [ObservableProperty]
        public partial string CurrentSingerSong { get; set; } = string.Empty;

        // Song title only (no artist), used on the vinyl 45 label.
        [ObservableProperty]
        public partial string CurrentSongTitle { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ConnectionUrl { get; set; } = string.Empty;

        [ObservableProperty]
        public partial System.Windows.Media.ImageSource? QrCodeImage { get; set; }

        [ObservableProperty]
        public partial bool ShowQrCode { get; set; } = true;

        [ObservableProperty]
        public partial bool ShowCrawl { get; set; }

        [ObservableProperty]
        public partial double MarqueeSpeed { get; set; } = 60;

        public sealed record NextSingerDisplay(string Text, bool IsRotationStart);

        public ObservableCollection<NextSingerDisplay> NextSingers { get; } = [];

        public ObservableCollection<DisplayRotationEntry> RotationEntries { get; } = [];

        // Full ordered rotation – used by the Star Wars crawl.
        public List<SingerEntry> FullRotation { get; } = [];

        // Used only by the SingerDisplayWindow popup banner.
        [ObservableProperty]
        public partial string BannerText { get; set; } = "Welcome to Karaoke Night!";

        [ObservableProperty]
        public partial string CrawlBannerText { get; set; } = AppSettings.DefaultCrawlBannerText;

        [ObservableProperty]
        public partial string SelectedProjectionView { get; set; } = "Normal List";

        [ObservableProperty]
        public partial double WatermarkOpacity { get; set; } = 0.06;

        [ObservableProperty]
        public partial bool HasDesignatedCurrentSinger { get; set; }

        [ObservableProperty]
        public partial bool CurrentSingerIsRotationStart { get; set; }

        [ObservableProperty]
        public partial bool IsCurrentMusic { get; set; }

        public string PerformerHeaderText
        {
            get
            {
                if (IsCurrentMusic) return "★  BACKGROUND MUSIC  ★";
                return HasDesignatedCurrentSinger ? "★  NOW PERFORMING  ★" : "★  FIRST PERFORMER  ★";
            }
        }

        public string NowSpinningText => IsCurrentMusic ? "♪  NOW PLAYING" : "♪  NOW SPINNING";

        partial void OnHasDesignatedCurrentSingerChanged(bool value)
        {
            OnPropertyChanged(nameof(PerformerHeaderText));
        }

        partial void OnIsCurrentMusicChanged(bool value)
        {
            OnPropertyChanged(nameof(PerformerHeaderText));
            OnPropertyChanged(nameof(NowSpinningText));
        }

        public void UpdateFromRotation(ObservableCollection<SingerEntry> rotation)
        {
            if (rotation == null || rotation.Count == 0)
            {
                CurrentSinger = "No singer";
                CurrentSingerName = "No singer";
                CurrentSingerSong = string.Empty;
                CurrentSongTitle = string.Empty;
                HasDesignatedCurrentSinger = false;
                CurrentSingerIsRotationStart = false;
                NextSingers.Clear();
                RotationEntries.Clear();
                FullRotation.Clear();
                return;
            }

            HasDesignatedCurrentSinger = Lyracist.Shared.RotationHelpers.HasActiveCurrentSinger(rotation);

            // Find the explicitly-marked current singer; fall back to the first active one.
            SingerEntry? current = Lyracist.Shared.RotationHelpers.GetCurrentSinger(rotation)
                                ?? rotation.FirstOrDefault(s => !s.IsInactive && !s.IsPaused && (!IsLastRound || !s.HasSungInLastRound));

            if (current != null && IsLastRound && current.HasSungInLastRound)
            {
                current = null;
            }

            List<SingerEntry> activeRotation = [.. rotation.Where(s => !s.IsInactive && !s.IsPaused && (!IsLastRound || !s.HasSungInLastRound))];

            if (current == null)
            {
                CurrentSinger = "No active singer";
                CurrentSingerName = "No active singer";
                CurrentSingerSong = string.Empty;
                CurrentSongTitle = string.Empty;
                HasDesignatedCurrentSinger = false;
                CurrentSingerIsRotationStart = false;
                IsCurrentMusic = false;
                NextSingers.Clear();
                FullRotation.Clear();
                RotationEntries.Clear();
                return;
            }
            else
            {
                CurrentSingerIsRotationStart = current.IsRotationStart;
                IsCurrentMusic = current.IsMusic;
                if (current.IsMusic)
                {
                    CurrentSinger = $"[MUSIC] {current.Song} (Req. by {current.Name})";
                    CurrentSingerName = $"[MUSIC] {current.Name}";
                    CurrentSongTitle = current.Song ?? string.Empty;
                    CurrentSingerSong = string.IsNullOrWhiteSpace(current.Song)
                        ? string.Empty
                        : (string.IsNullOrWhiteSpace(current.Artist)
                            ? current.Song
                            : $"{current.Song} – {current.Artist}");
                }
                else
                {
                    string currentName = current.IsDuet ? $"{current.Name} & {current.DuetPartnerName}" : current.Name;
                    CurrentSinger = string.IsNullOrWhiteSpace(current.Song)
                        ? currentName
                        : $"{currentName} - {current.Song}";

                    CurrentSingerName = currentName;
                    CurrentSongTitle = current.Song ?? string.Empty;
                    CurrentSingerSong = string.IsNullOrWhiteSpace(current.Song)
                        ? string.Empty
                        : (string.IsNullOrWhiteSpace(current.Artist)
                            ? current.Song
                            : $"{current.Song} – {current.Artist}");
                }
            }

            // Build the next-up list in true rotation order:
            // start immediately after current singer and wrap to the beginning.
            NextSingers.Clear();

            if (current != null)
            {
                int currentIndex = activeRotation.IndexOf(current);
                int count = activeRotation.Count;

                for (int offset = 1; offset < count && NextSingers.Count < 5; offset++)
                {
                    SingerEntry singer = activeRotation[(currentIndex + offset) % count];

                    string waitBadge = singer.EstimatedWaitMinutes > 0 ? $" {{{singer.EstimatedWaitMinutes}}}" : string.Empty;

                    if (singer.IsMusic)
                    {
                        string songText = string.IsNullOrWhiteSpace(singer.Artist)
                            ? singer.Song
                            : $"{singer.Song} – {singer.Artist}";
                        NextSingers.Add(new NextSingerDisplay($"[MUSIC]{waitBadge} {songText}", singer.IsRotationStart));
                    }
                    else
                    {
                        string sName = singer.IsDuet ? $"{singer.Name} & {singer.DuetPartnerName}" : singer.Name;
                        NextSingers.Add(new NextSingerDisplay(string.IsNullOrWhiteSpace(singer.Song)
                            ? $"{sName}{waitBadge}"
                            : $"{sName}{waitBadge} - {singer.Song}", singer.IsRotationStart));
                    }
                }
            }
            else
            {
                foreach (SingerEntry singer in activeRotation.Take(5))
                {
                    if (singer.IsMusic)
                    {
                        string songText = string.IsNullOrWhiteSpace(singer.Artist)
                            ? singer.Song
                            : $"{singer.Song} – {singer.Artist}";
                        NextSingers.Add(new NextSingerDisplay($"[MUSIC] {songText}", singer.IsRotationStart));
                    }
                    else
                    {
                        string sName = singer.IsDuet ? $"{singer.Name} & {singer.DuetPartnerName}" : singer.Name;
                        NextSingers.Add(new NextSingerDisplay(string.IsNullOrWhiteSpace(singer.Song)
                            ? sName
                            : $"{sName} - {singer.Song}", singer.IsRotationStart));
                    }
                }
            }

            // Reorder so current singer is always index 0 (crawl treats i==0 as "NOW SINGING").
            FullRotation.Clear();

            // Build new entries into a local list, then sync RotationEntries in-place to minimise
            // CollectionChanged events — typically only 1-2 items change per singer property update.
            var newRotationEntries = new List<DisplayRotationEntry>();

            if (current != null)
            {
                int currentIndex = activeRotation.IndexOf(current);
                int count = activeRotation.Count;

                for (int offset = 0; offset < count; offset++)
                {
                    SingerEntry singer = activeRotation[(currentIndex + offset) % count];
                    FullRotation.Add(singer);

                    string sName = singer.IsDuet ? $"{singer.Name} & {singer.DuetPartnerName}" : singer.Name;
                    string waitBadge = singer.EstimatedWaitMinutes > 0 ? $" {{{singer.EstimatedWaitMinutes}}}" : string.Empty;
                    string prefixAndSinger = singer.IsMusic
                        ? $"{(offset == 0 ? "★" : $"{offset + 1}")}. [MUSIC]{waitBadge}"
                        : $"{(offset == 0 ? "★" : $"{offset + 1}")}. {sName}{waitBadge}";

                    string songSeparatorAndTitle = string.IsNullOrWhiteSpace(singer.Song)
                        ? string.Empty
                        : (singer.IsMusic ? $" {singer.Song} (Req. by {singer.Name})" : $" - {singer.Song}");

                    string artistInParentheses = string.IsNullOrWhiteSpace(singer.Artist)
                        ? string.Empty
                        : $" ({singer.Artist})";

                    newRotationEntries.Add(new DisplayRotationEntry(prefixAndSinger, songSeparatorAndTitle, artistInParentheses, offset == 0, singer.IsRotationStart));

                }
            }
            else
            {
                int index = 1;
                foreach (SingerEntry singer in activeRotation)
                {
                    FullRotation.Add(singer);

                    string sName = singer.IsDuet ? $"{singer.Name} & {singer.DuetPartnerName}" : singer.Name;
                    string prefixAndSinger = singer.IsMusic
                        ? $"{index}. [MUSIC]"
                        : $"{index}. {sName}";

                    string songSeparatorAndTitle = string.IsNullOrWhiteSpace(singer.Song)
                        ? string.Empty
                        : (singer.IsMusic ? $" {singer.Song} (Req. by {singer.Name})" : $" - {singer.Song}");

                    string artistInParentheses = string.IsNullOrWhiteSpace(singer.Artist)
                        ? string.Empty
                        : $" ({singer.Artist})";

                    newRotationEntries.Add(new DisplayRotationEntry(prefixAndSinger, songSeparatorAndTitle, artistInParentheses, false, singer.IsRotationStart));

                    index++;
                }
            }

            // Sync RotationEntries in-place: update changed slots, add/remove only when count changes.
            for (int i = 0; i < newRotationEntries.Count; i++)
            {
                if (i < RotationEntries.Count)
                {
                    if (!RotationEntries[i].Equals(newRotationEntries[i]))
                        RotationEntries[i] = newRotationEntries[i];
                }
                else
                {
                    RotationEntries.Add(newRotationEntries[i]);
                }
            }
            while (RotationEntries.Count > newRotationEntries.Count)
                RotationEntries.RemoveAt(RotationEntries.Count - 1);
        }

        private static string CleanAndPad(string input, int length)
        {
            input ??= string.Empty;
            string cleaned = input.Replace('–', '-').Replace('—', '-').ToUpperInvariant();
            if (cleaned.Length > length)
            {
                return cleaned[..length];
            }
            return cleaned.PadRight(length);
        }

        public sealed record DisplayRotationEntry(
            string PrefixAndSinger,
            string SongSeparatorAndTitle,
            string ArtistInParentheses,
            bool IsCurrent,
            bool IsRotationStart);
    }

}