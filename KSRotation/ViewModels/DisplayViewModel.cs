// Edited on Oct 2, 2026 @ 12:20:00 -> Load upright avatar images using AvatarImageHelper with EXIF orientation handling
using CommunityToolkit.Mvvm.ComponentModel;
using KSRotation.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace KSRotation.ViewModels
{
    public partial class DisplayViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial bool IsLastRound { get; set; }

        [ObservableProperty]
        public partial int ActiveSingerCount { get; set; }

        [ObservableProperty]
        public partial string ActiveSingerCountText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasActiveSingers { get; set; }

        [ObservableProperty]
        public partial string CurrentSinger { get; set; } = "No singer selected";

        [ObservableProperty]
        public partial System.Windows.Media.ImageSource? CurrentSingerAvatar { get; set; }

        [ObservableProperty]
        public partial bool HasCurrentSingerAvatar { get; set; }

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

        /// <summary>True while nobody is in the rotation, so the window shows the sign-up invite instead of an empty list.</summary>
        [ObservableProperty]
        public partial bool IsRotationEmpty { get; set; } = true;

        [ObservableProperty]
        public partial bool ShowCrawl { get; set; }

        [ObservableProperty]
        public partial double MarqueeSpeed { get; set; } = 60;

        /// <summary>Whether the "UP NEXT: # in {} = Estimated wait time" / "ON DECK: ..." legend
        /// captions (Marquee and Vinyl views) are shown - mirrors the DJ's "Show Estimated Wait
        /// Time" setting, since the legend is meaningless once the badges themselves are hidden.</summary>
        [ObservableProperty]
        public partial bool ShowEstimatedWaitTime { get; set; } = true;

        /// <summary>DJ's "reduced projection effects" setting for weaker venue PCs: the projection
        /// views use fewer particles and skip per-element blur effects.</summary>
        [ObservableProperty]
        public partial bool ReducedEffects { get; set; }

        /// <summary>Whether the live synth bars (line-in spectrum) show on the rotation screen.</summary>
        [ObservableProperty]
        public partial bool ShowSpectrum { get; set; }

        public sealed record NextSingerDisplay
        {
            public string Text { get; init; }
            public bool IsRotationStart { get; init; }
            public string SingerName { get; init; }
            public string Song { get; init; }
            public string SongTitle => Song;
            public string WaitTime { get; init; }
            public bool HasSong => !string.IsNullOrWhiteSpace(Song);
            public bool HasWaitTime => !string.IsNullOrWhiteSpace(WaitTime);

            public NextSingerDisplay(string text, bool isRotationStart, string singerName = "", string song = "", string waitTime = "")
            {
                Text = text;
                IsRotationStart = isRotationStart;
                SingerName = string.IsNullOrWhiteSpace(singerName) ? text : singerName;
                Song = song;
                WaitTime = waitTime;
            }
        }

        public ObservableCollection<NextSingerDisplay> NextSingers { get; } = [];

        // Avatar type/source looked up by name for singers whose rotation entry has none.
        // UpdateFromRotation runs on the UI thread on every rotation change, and querying SQLite each
        // time stalled it. Entries expire after a minute so an avatar the DJ assigns mid-show appears.
        private readonly Dictionary<string, (string? Type, string? Source, DateTime FetchedUtc)> _dbAvatarLookups = new(StringComparer.Ordinal);
        private static readonly TimeSpan DbAvatarLookupTtl = TimeSpan.FromMinutes(1);

        private (string? Type, string? Source) LookupDbAvatar(string name)
        {
            if (_dbAvatarLookups.TryGetValue(name, out var hit) && DateTime.UtcNow - hit.FetchedUtc < DbAvatarLookupTtl)
            {
                return (hit.Type, hit.Source);
            }

            string? type = null;
            string? source = null;
            try
            {
                using var db = new Lyracist.Data.LyracistDbContext();
                var dbSinger = db.Singers.FirstOrDefault(s => s.Name == name);
                if (dbSinger != null)
                {
                    type = dbSinger.AvatarType;
                    source = dbSinger.AvatarSource;
                }
            }
            catch
            {
                // Ignored - cached as "no avatar" until the entry expires, rather than retried every update.
            }

            if (_dbAvatarLookups.Count >= 256) _dbAvatarLookups.Clear();
            _dbAvatarLookups[name] = (type, source, DateTime.UtcNow);
            return (type, source);
        }

        // Decoded avatars keyed by source (plus the file's timestamp for uploads, so replacing an
        // avatar file under the same name is picked up). Every rotation update used to re-read and
        // re-decode the file, or start a brand-new Gravatar download, and handed the view a new image
        // object each time. UI thread only: Gravatar bitmaps load asynchronously and can't be frozen.
        private readonly Dictionary<string, System.Windows.Media.Imaging.BitmapSource> _avatarImages = new(StringComparer.Ordinal);

        private System.Windows.Media.Imaging.BitmapSource? ResolveAvatarImage(string? avType, string? avSource)
        {
            if (string.IsNullOrEmpty(avSource)) return null;

            try
            {
                string key;
                if (avType == "Gravatar")
                {
                    key = "G\u0001" + avSource;
                    if (_avatarImages.TryGetValue(key, out var cached)) return cached;

                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri($"https://www.gravatar.com/avatar/{avSource}?d=identicon&s=150", UriKind.Absolute);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    // A failed download must not stay cached, or the singer would show a blank avatar until restart.
                    bitmap.DownloadFailed += (_, _) => _avatarImages.Remove(key);

                    if (_avatarImages.Count >= 256) _avatarImages.Clear();
                    _avatarImages[key] = bitmap;
                    return bitmap;
                }
                else if (avType == "Uploaded")
                {
                    string fullPath = Path.Combine(Lyracist.Shared.Globals.AvatarsDir, avSource);
                    if (!File.Exists(fullPath)) return null;

                    key = "U\u0001" + avSource + "\u0001" + File.GetLastWriteTimeUtc(fullPath).Ticks;
                    if (_avatarImages.TryGetValue(key, out var cached)) return cached;

                    var bitmap = Lyracist.Shared.AvatarImageHelper.LoadOrientedBitmapFromFile(fullPath);
                    if (bitmap == null) return null;

                    if (_avatarImages.Count >= 256) _avatarImages.Clear();
                    _avatarImages[key] = bitmap;
                    return bitmap;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        // Eight themed panels each bind an ItemsControl to NextSingers (collapsed ones still regenerate
        // their item templates), so Clear()+Add() on every rotation update rebuilt ~50 templates even
        // when the queue hadn't changed. Diffing by record value only touches entries that did change.
        private static void SyncNextSingers(ObservableCollection<NextSingerDisplay> target, List<NextSingerDisplay> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (i < target.Count)
                {
                    if (!target[i].Equals(source[i]))
                    {
                        target[i] = source[i];
                    }
                }
                else
                {
                    target.Add(source[i]);
                }
            }
            while (target.Count > source.Count)
            {
                target.RemoveAt(target.Count - 1);
            }
        }

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

        /// <summary>File path of the DJ banner (image or .mp4) the DJ picked to display in the
        /// Stadium Jumbotron's bottom sponsor box. Empty when none is selected.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasJumbotronBanner))]
        [NotifyPropertyChangedFor(nameof(IsJumbotronBannerVideo))]
        public partial string JumbotronBannerPath { get; set; } = string.Empty;

        public bool HasJumbotronBanner => !string.IsNullOrEmpty(JumbotronBannerPath) && File.Exists(JumbotronBannerPath);

        public bool IsJumbotronBannerVideo => HasJumbotronBanner
            && Path.GetExtension(JumbotronBannerPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase);

        [ObservableProperty]
        public partial bool HasDesignatedCurrentSinger { get; set; }

        [ObservableProperty]
        public partial bool CurrentSingerIsRotationStart { get; set; }

        [ObservableProperty]
        public partial bool CurrentSingerIsSpecial { get; set; }

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
            IsRotationEmpty = rotation == null || rotation.Count == 0;

            if (rotation == null || rotation.Count == 0)
            {
                CurrentSinger = "No singer";
                CurrentSingerName = "No singer";
                CurrentSingerSong = string.Empty;
                CurrentSongTitle = string.Empty;
                HasDesignatedCurrentSinger = false;
                CurrentSingerIsRotationStart = false;
                CurrentSingerIsSpecial = false;
                CurrentSingerAvatar = null;
                HasCurrentSingerAvatar = false;
                ActiveSingerCount = 0;
                ActiveSingerCountText = "0 Singers in Rotation";
                HasActiveSingers = false;
                NextSingers.Clear();
                RotationEntries.Clear();
                FullRotation.Clear();
                return;
            }

            HasDesignatedCurrentSinger = Lyracist.Shared.RotationHelpers.HasActiveCurrentSinger(rotation);

            // Find the explicitly-marked current singer; fall back to the first active one.
            SingerEntry? current = Lyracist.Shared.RotationHelpers.GetCurrentSinger(rotation)
                                ?? rotation.FirstOrDefault(s => !s.IsInactive && !s.IsPaused && !s.IsSkipped && (!IsLastRound || !s.HasSungInLastRound));

            if (current != null && IsLastRound && current.HasSungInLastRound)
            {
                current = null;
            }

            List<SingerEntry> activeRotation = [.. rotation.Where(s => !s.IsInactive && !s.IsPaused && !s.IsSkipped && (!IsLastRound || !s.HasSungInLastRound))];
            int activeCount = activeRotation.Count(s => !s.IsMusic);
            ActiveSingerCount = activeCount;
            ActiveSingerCountText = activeCount == 1 ? "1 Singer in Rotation" : $"{activeCount} Singers in Rotation";
            HasActiveSingers = activeCount > 0;

            if (current == null)
            {
                CurrentSinger = "No active singer";
                CurrentSingerName = "No active singer";
                CurrentSingerSong = string.Empty;
                CurrentSongTitle = string.Empty;
                HasDesignatedCurrentSinger = false;
                CurrentSingerIsRotationStart = false;
                CurrentSingerIsSpecial = false;
                IsCurrentMusic = false;
                CurrentSingerAvatar = null;
                HasCurrentSingerAvatar = false;
                NextSingers.Clear();
                FullRotation.Clear();
                RotationEntries.Clear();
                return;
            }
            else
            {
                CurrentSingerIsRotationStart = current.IsRotationStart;
                CurrentSingerIsSpecial = current.IsSpecial;
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
                    CurrentSingerAvatar = null;
                    HasCurrentSingerAvatar = false;
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

                    string? avType = current.AvatarType;
                    string? avSource = current.AvatarSource;

                    if ((string.IsNullOrEmpty(avSource) || avType == "None") && !string.IsNullOrEmpty(current.Name))
                    {
                        (avType, avSource) = LookupDbAvatar(current.Name);
                    }

                    var avatar = ResolveAvatarImage(avType, avSource);

                    CurrentSingerAvatar = avatar;
                    HasCurrentSingerAvatar = avatar != null;
                }
            }

            // Build the next-up list in true rotation order:
            // start immediately after current singer and wrap to the beginning.
            var nextSingers = new List<NextSingerDisplay>();

            if (current != null)
            {
                int currentIndex = activeRotation.IndexOf(current);
                int count = activeRotation.Count;

                for (int offset = 1; offset < count && nextSingers.Count < 6; offset++)
                {
                    SingerEntry singer = activeRotation[(currentIndex + offset) % count];

                    string waitBadge = (ShowEstimatedWaitTime && singer.EstimatedWaitMinutes > 0) ? $" {{{singer.EstimatedWaitMinutes}}}" : string.Empty;
                    string waitTime = (ShowEstimatedWaitTime && singer.EstimatedWaitMinutes > 0) ? $"{{{singer.EstimatedWaitMinutes}}}" : string.Empty;

                    if (singer.IsMusic)
                    {
                        string songText = string.IsNullOrWhiteSpace(singer.Artist)
                            ? (singer.Song ?? string.Empty)
                            : $"{singer.Song} – {singer.Artist}";
                        nextSingers.Add(new NextSingerDisplay($"[MUSIC]{waitBadge} {songText}", singer.IsRotationStart, "[MUSIC]", songText, waitTime));
                    }
                    else
                    {
                        string sName = singer.IsDuet ? $"{singer.Name} & {singer.DuetPartnerName}" : singer.Name;
                        nextSingers.Add(new NextSingerDisplay(string.IsNullOrWhiteSpace(singer.Song)
                            ? $"{sName}{waitBadge}"
                            : $"{sName}{waitBadge} - {singer.Song}", singer.IsRotationStart, sName, singer.Song ?? string.Empty, waitTime));
                    }
                }
            }
            else
            {
                foreach (SingerEntry singer in activeRotation.Take(6))
                {
                    string waitBadge = (ShowEstimatedWaitTime && singer.EstimatedWaitMinutes > 0) ? $" {{{singer.EstimatedWaitMinutes}}}" : string.Empty;
                    string waitTime = (ShowEstimatedWaitTime && singer.EstimatedWaitMinutes > 0) ? $"{{{singer.EstimatedWaitMinutes}}}" : string.Empty;

                    if (singer.IsMusic)
                    {
                        string songText = string.IsNullOrWhiteSpace(singer.Artist)
                            ? (singer.Song ?? string.Empty)
                            : $"{singer.Song} – {singer.Artist}";
                        nextSingers.Add(new NextSingerDisplay($"[MUSIC]{waitBadge} {songText}", singer.IsRotationStart, "[MUSIC]", songText, waitTime));
                    }
                    else
                    {
                        string sName = singer.IsDuet ? $"{singer.Name} & {singer.DuetPartnerName}" : singer.Name;
                        nextSingers.Add(new NextSingerDisplay(string.IsNullOrWhiteSpace(singer.Song)
                            ? $"{sName}{waitBadge}"
                            : $"{sName}{waitBadge} - {singer.Song}", singer.IsRotationStart, sName, singer.Song ?? string.Empty, waitTime));
                    }
                }
            }
            SyncNextSingers(NextSingers, nextSingers);

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

                    newRotationEntries.Add(new DisplayRotationEntry(prefixAndSinger, songSeparatorAndTitle, artistInParentheses, offset == 0, singer.IsRotationStart, singer.IsSpecial));
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

                    newRotationEntries.Add(new DisplayRotationEntry(prefixAndSinger, songSeparatorAndTitle, artistInParentheses, false, singer.IsRotationStart, singer.IsSpecial));

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
            bool IsRotationStart,
            bool IsSpecial = false);
    }
}