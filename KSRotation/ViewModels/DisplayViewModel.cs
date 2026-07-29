// Edited on Jul 28, 2026 @ 18:41:00 -> Add support for formatting music requests differently on master screens
// Last Edit: Jul 02, 2026 16:54 - Added designated-current state tracking to support First Performer labels before a current singer is set.
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
        public partial bool ShowCrawl { get; set; }

        [ObservableProperty]
        public partial double MarqueeSpeed { get; set; } = 60;

        public ObservableCollection<string> NextSingers { get; } = [];

        public ObservableCollection<DisplayRotationEntry> RotationEntries { get; } = [];

        // Full ordered rotation – used by the Star Wars crawl.
        public List<SingerEntry> FullRotation { get; } = [];

        // Full ordered rotation entries for the Flip Tile board view.
        public ObservableCollection<DisplayFlipTileEntry> FlipTileEntries { get; } = [];

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
                NextSingers.Clear();
                RotationEntries.Clear();
                FullRotation.Clear();
                FlipTileEntries.Clear();
                return;
            }

            HasDesignatedCurrentSinger = rotation.Any(s => s.IsCurrent && !s.IsInactive);

            // Find the explicitly-marked current singer; fall back to the first active one.
            SingerEntry? current = rotation.FirstOrDefault(s => s.IsCurrent && !s.IsInactive)
                                ?? rotation.FirstOrDefault(s => !s.IsInactive);

            List<SingerEntry> activeRotation = [.. rotation.Where(s => !s.IsInactive)];

            if (current == null)
            {
                CurrentSinger = "No active singer";
                CurrentSingerName = "No active singer";
                CurrentSingerSong = string.Empty;
                CurrentSongTitle = string.Empty;
                HasDesignatedCurrentSinger = false;
                IsCurrentMusic = false;
                NextSingers.Clear();
                FullRotation.Clear();
                RotationEntries.Clear();
                FlipTileEntries.Clear();
                return;
            }
            else
            {
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
                    CurrentSinger = string.IsNullOrWhiteSpace(current.Song)
                        ? current.Name
                        : $"{current.Name} - {current.Song}";

                    CurrentSingerName = current.Name;
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

                    if (singer.IsMusic)
                    {
                        string songText = string.IsNullOrWhiteSpace(singer.Artist)
                            ? singer.Song
                            : $"{singer.Song} – {singer.Artist}";
                        NextSingers.Add($"[MUSIC] {songText}");
                    }
                    else
                    {
                        NextSingers.Add(string.IsNullOrWhiteSpace(singer.Song)
                            ? singer.Name
                            : $"{singer.Name} - {singer.Song}");
                    }
                }
            }
            else
            {
                foreach (SingerEntry singer in rotation.Where(s => !s.IsInactive).Take(5))
                {
                    if (singer.IsMusic)
                    {
                        string songText = string.IsNullOrWhiteSpace(singer.Artist)
                            ? singer.Song
                            : $"{singer.Song} – {singer.Artist}";
                        NextSingers.Add($"[MUSIC] {songText}");
                    }
                    else
                    {
                        NextSingers.Add(string.IsNullOrWhiteSpace(singer.Song)
                            ? singer.Name
                            : $"{singer.Name} - {singer.Song}");
                    }
                }
            }

            // Reorder so current singer is always index 0 (crawl treats i==0 as "NOW SINGING").
            FullRotation.Clear();
            FlipTileEntries.Clear();

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

                    string prefixAndSinger = singer.IsMusic 
                        ? $"{(offset == 0 ? "★" : $"{offset + 1}")}. [MUSIC]"
                        : $"{(offset == 0 ? "★" : $"{offset + 1}")}. {singer.Name}";

                    string songSeparatorAndTitle = string.IsNullOrWhiteSpace(singer.Song)
                        ? string.Empty
                        : (singer.IsMusic ? $" {singer.Song} (Req. by {singer.Name})" : $" - {singer.Song}");

                    string artistInParentheses = string.IsNullOrWhiteSpace(singer.Artist)
                        ? string.Empty
                        : $" ({singer.Artist})";

                    newRotationEntries.Add(new DisplayRotationEntry(prefixAndSinger, songSeparatorAndTitle, artistInParentheses, offset == 0));

                    // Populate Flip Tile Entry (limit to at most 10 displayed singers)
                    if (offset < 10)
                    {
                        string statusText = string.IsNullOrWhiteSpace(singer.Song)
                            ? "No Song Selected"
                            : singer.Song;

                        if (!string.IsNullOrWhiteSpace(singer.Artist))
                        {
                            statusText += $" – {singer.Artist}";
                        }

                        string posLabel = offset == 0 ? "★"
                                        : offset == 1 ? "Next"
                                        : $"{offset + 1}";

                        string displayName = singer.IsMusic ? $"[M] {singer.Name}" : singer.Name;

                        FlipTileEntries.Add(new DisplayFlipTileEntry
                        {
                            Position = CleanAndPad(posLabel, 4),
                            Name = CleanAndPad(displayName, 20),
                            Status = CleanAndPad(statusText, 36),
                            IsCurrent = offset == 0,
                            IsNext = offset == 1
                        });
                    }
                }
            }
            else
            {
                int index = 1;
                foreach (SingerEntry singer in activeRotation)
                {
                    FullRotation.Add(singer);

                    string prefixAndSinger = singer.IsMusic
                        ? $"{index}. [MUSIC]"
                        : $"{index}. {singer.Name}";

                    string songSeparatorAndTitle = string.IsNullOrWhiteSpace(singer.Song)
                        ? string.Empty
                        : (singer.IsMusic ? $" {singer.Song} (Req. by {singer.Name})" : $" - {singer.Song}");

                    string artistInParentheses = string.IsNullOrWhiteSpace(singer.Artist)
                        ? string.Empty
                        : $" ({singer.Artist})";

                    newRotationEntries.Add(new DisplayRotationEntry(prefixAndSinger, songSeparatorAndTitle, artistInParentheses, false));

                    // Populate Flip Tile Entry (limit to at most 10 displayed singers)
                    if (index <= 10)
                    {
                        string statusText = string.IsNullOrWhiteSpace(singer.Song)
                            ? "No Song Selected"
                            : singer.Song;

                        if (!string.IsNullOrWhiteSpace(singer.Artist))
                        {
                            statusText += $" – {singer.Artist}";
                        }

                        string displayName = singer.IsMusic ? $"[M] {singer.Name}" : singer.Name;

                        FlipTileEntries.Add(new DisplayFlipTileEntry
                        {
                            Position = CleanAndPad($"{index}", 4),
                            Name = CleanAndPad(displayName, 20),
                            Status = CleanAndPad(statusText, 36),
                            IsCurrent = false,
                            IsNext = false
                        });
                    }

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
            bool IsCurrent);
    }

    public class DisplayFlipTileEntry
    {
        public string Position { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
        public bool IsNext { get; set; }
    }
}