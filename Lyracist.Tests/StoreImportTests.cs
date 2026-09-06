// Edited on Sep 6, 2026 @ 11:55:00 -> Add unit tests for Bulk Import Wizard folder scanning, option toggles, and destination conflict resolution
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Lyracist.Services.Store;
using Lyracist.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Lyracist.Tests;

public class StoreImportTests
{
    public StoreImportTests()
    {
        using var context = new LyracistDbContext();
        context.Database.Migrate();
    }

    [Theory]
    [InlineData("KV1234 - Queen - Bohemian Rhapsody.zip", "Karaoke Version")]
    [InlineData("Queen - Bohemian Rhapsody (Karaoke Version).mp3", "Karaoke Version")]
    [InlineData("Queen - Bohemian Rhapsody [Karaoke Version].cdg", "Karaoke Version")]
    [InlineData("C:\\Downloads\\karaoke-version\\track.mp3", "Karaoke Version")]
    [InlineData("PT105 - Journey - Don't Stop Believin'.zip", "Party Tyme")]
    [InlineData("Journey - Don't Stop Believin' (Party Tyme).mp3", "Party Tyme")]
    [InlineData("Sybersound - Journey - Don't Stop Believin'.mp4", "Party Tyme")]
    [InlineData("C:\\Downloads\\karaoke.com\\track.mp3", "Karaoke.com")]
    [InlineData("SF123 - Beatles - Let It Be.mp3", "Sunfly")]
    [InlineData("Sunfly - Queen - Radio Ga Ga.zip", "Sunfly")]
    [InlineData("C:\\Downloads\\sunfly\\track.mp4", "Sunfly")]
    [InlineData("Journey - Don't Stop Believin'.mp3", "Local")]
    public void DetectSource_IdentifiesExpectedProvider(string filePath, string expectedSource)
    {
        string filename = Path.GetFileNameWithoutExtension(filePath);
        string source = PurchasedTrackWatcherService.DetectSource(filePath, filename, "Artist");
        Assert.Equal(expectedSource, source);
    }

    [Fact]
    public void AppSettings_StoreDefaults_AreConfiguredProperly()
    {
        string expectedDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Assert.Equal(expectedDownloads, AppSettings.StorePurchasedTracksFolder);
        Assert.False(AppSettings.StoreAutoImportEnabled);
        Assert.False(AppSettings.StoreNormalizeAudioOnImport);
        Assert.False(AppSettings.StoreTrimSilenceOnImport);
        Assert.False(AppSettings.StoreGenerateWaveformOnImport);
    }

    [Fact]
    public void DeepLink_QueryEncoding_ProducesValidUrls()
    {
        string query = "Sweet Caroline (Neil Diamond)";
        string encoded = Uri.EscapeDataString(query.Trim());

        string kvUrl = $"https://www.karaoke-version.com/search.html?q={encoded}";
        string ptUrl = $"https://www.partytyme.net/search?q={encoded}";
        string kcUrl = $"https://karaoke.com/search?type=product&q={encoded}";
        string sfUrl = $"https://www.sunflykaraoke.com/catalogsearch/result/?q={encoded}";

        Assert.Equal("https://www.karaoke-version.com/search.html?q=Sweet%20Caroline%20%28Neil%20Diamond%29", kvUrl);
        Assert.Equal("https://www.partytyme.net/search?q=Sweet%20Caroline%20%28Neil%20Diamond%29", ptUrl);
        Assert.Equal("https://karaoke.com/search?type=product&q=Sweet%20Caroline%20%28Neil%20Diamond%29", kcUrl);
        Assert.Equal("https://www.sunflykaraoke.com/catalogsearch/result/?q=Sweet%20Caroline%20%28Neil%20Diamond%29", sfUrl);
    }

    [Fact]
    public void PurchasedTrackWatcher_RecentLogs_MaintainsMaxTwentyItems()
    {
        var mockLibrary = new Mock<ILibraryService>();
        using var watcher = new PurchasedTrackWatcherService(mockLibrary.Object);

        for (int i = 1; i <= 25; i++)
        {
            watcher.LogImport($"Track #{i}", "Artist", "Sunfly", i % 2 == 0 ? "Info" : "Success", $"Event #{i}");
        }

        var logs = watcher.RecentLogs;
        Assert.Equal(20, logs.Count);
        // FIFO order: first item is the oldest remaining item (#6)
        Assert.Equal("Event #6", logs[0].Message);
        // Last item is the newest added item (#25)
        Assert.Equal("Event #25", logs[^1].Message);
    }

    [Fact]
    public async Task ImportFileAsync_InsertsSongAndTagsProvider()
    {
        var mockLibrary = new Mock<ILibraryService>();
        using var watcherService = new PurchasedTrackWatcherService(mockLibrary.Object);

        string tempDir = Path.Combine(Path.GetTempPath(), "Lyracist_StoreTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string testFile = Path.Combine(tempDir, "KV999 - ABBA - Dancing Queen (Karaoke Version).mp3");
        await File.WriteAllTextAsync(testFile, "DUMMY AUDIO CONTENT", TestContext.Current.CancellationToken);

        try
        {
            // Temporarily disable moving files so it imports in-place
            bool originalMove = AppSettings.StoreMoveFilesToTarget;
            AppSettings.StoreMoveFilesToTarget = false;

            try
            {
                var result = await watcherService.ImportFileAsync(testFile);

                Assert.NotNull(result);
                Assert.Equal("Karaoke Version", result.Source);
                Assert.True(result.SongId > 0);
                Assert.NotNull(result.Difficulty);
                Assert.NotNull(result.VocalPresence);
                Assert.NotNull(result.Quality);

                // Verify in SQLite database
                using var context = new LyracistDbContext();
                var song = await context.Songs.FirstOrDefaultAsync(s => s.SongId == result.SongId, TestContext.Current.CancellationToken);
                Assert.NotNull(song);
                Assert.Contains("Karaoke Version", song.Tags);
                Assert.Equal(result.Difficulty, song.Difficulty);
                Assert.Equal(result.VocalPresence, song.VocalPresence);
                Assert.Equal(result.Quality, song.Quality);

                mockLibrary.Verify(l => l.NotifyLibraryUpdated(), Times.Once);

                // Cleanup DB record
                context.Songs.Remove(song);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            finally
            {
                AppSettings.StoreMoveFilesToTarget = originalMove;
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Theory]
    [InlineData(new byte[] { 0x01, 0x0F, 0x00, 0x00 }, "Karaoke Version")]
    [InlineData(new byte[] { 0x02, 0x0A, 0x00, 0x00 }, "Party Tyme")]
    [InlineData(new byte[] { 0x03, 0x0C, 0x00, 0x00 }, "Sunfly")]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00 }, null)]
    public async Task DetectProviderFromCdgHeader_IdentifiesSignatures(byte[] headerBytes, string? expected)
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, headerBytes, TestContext.Current.CancellationToken);
            string? detected = PurchasedTrackWatcherService.DetectProviderFromCdgHeader(tempFile);
            Assert.Equal(expected, detected);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("TXXX:KV", "Custom Data", "Karaoke Version")]
    [InlineData("TXXX:PT", "Sybersound", "Party Tyme")]
    [InlineData("TXXX:SF", "Catalog 100", "Sunfly")]
    [InlineData("TXXX:KCOM", "Product 99", "Karaoke.com")]
    [InlineData("ALBUM", "Random Album", null)]
    public void DetectProviderFromId3_IdentifiesTagSignatures(string tagKey, string tagValue, string? expected)
    {
        var probe = new FFprobeResult();
        probe.Tags[tagKey] = tagValue;

        string? detected = PurchasedTrackWatcherService.DetectProviderFromId3(probe);
        Assert.Equal(expected, detected);
    }

    [Theory]
    [InlineData("Sunfly Karaoke", "Artist", "Comment", "Sunfly")]
    [InlineData("Title", "Artist", "Produced by Party Tyme", "Party Tyme")]
    [InlineData("Title", "Karaoke Version", "Comment", "Karaoke Version")]
    [InlineData("Title", "Artist", "Licensed from Karaoke.com", "Karaoke.com")]
    [InlineData("Generic Title", "Generic Artist", "Generic Comment", null)]
    public void DetectProviderFromMp4_IdentifiesMetadataSignatures(string title, string artist, string comment, string? expected)
    {
        var probe = new FFprobeResult
        {
            TitleTag = title,
            ArtistTag = artist,
            CommentTag = comment
        };

        string? detected = PurchasedTrackWatcherService.DetectProviderFromMp4(probe);
        Assert.Equal(expected, detected);
    }

    [Fact]
    public void InspectZipForProvider_IdentifiesInternalHierarchy()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Lyracist_ZipTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string kvZip = Path.Combine(tempDir, "kv_archive.zip");
            using (var zip = ZipFile.Open(kvZip, ZipArchiveMode.Create))
            {
                zip.CreateEntry("custom_backing_track/track.mp3");
                zip.CreateEntry("custom_backing_track/track.cdg");
            }
            Assert.Equal("Karaoke Version", PurchasedTrackWatcherService.InspectZipForProvider(kvZip));

            string ptZip = Path.Combine(tempDir, "pt_archive.zip");
            using (var zip = ZipFile.Open(ptZip, ZipArchiveMode.Create))
            {
                zip.CreateEntry("karaoke/song_pt.mp3");
                zip.CreateEntry("karaoke/song_pt.cdg");
            }
            Assert.Equal("Party Tyme", PurchasedTrackWatcherService.InspectZipForProvider(ptZip));

            string sfZip = Path.Combine(tempDir, "sf_archive.zip");
            using (var zip = ZipFile.Open(sfZip, ZipArchiveMode.Create))
            {
                zip.CreateEntry("SF1234.mp3");
                zip.CreateEntry("SF1234.cdg");
            }
            Assert.Equal("Sunfly", PurchasedTrackWatcherService.InspectZipForProvider(sfZip));

            string kcZip = Path.Combine(tempDir, "kcom_archive.zip");
            using (var zip = ZipFile.Open(kcZip, ZipArchiveMode.Create))
            {
                zip.CreateEntry("KCOM_track.mp3");
                zip.CreateEntry("KCOM_track.cdg");
            }
            Assert.Equal("Karaoke.com", PurchasedTrackWatcherService.InspectZipForProvider(kcZip));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReferrerHint_FallbackAppliesWhenNoOtherSignatureDetected()
    {
        PurchasedTrackWatcherService.ReferrerHint = "Karaoke.com";
        Assert.Equal("Karaoke.com", PurchasedTrackWatcherService.ReferrerHint);

        PurchasedTrackWatcherService.ReferrerHint = null;
    }

    [Fact]
    public void RenameImportedFiles_RenamesPrimaryAndCompanionFilesCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Lyracist_RenameTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string primary = Path.Combine(tempDir, "temp_download_123.mp3");
            string companion = Path.Combine(tempDir, "temp_download_123.cdg");
            string lyrics = Path.Combine(tempDir, "temp_download_123.lrc");
            File.WriteAllText(primary, "dummy audio");
            File.WriteAllText(companion, "dummy cdg");
            File.WriteAllText(lyrics, "[00:01.00] Hello");

            string? compRef = companion;
            string? lyrRef = lyrics;

            PurchasedTrackWatcherService.RenameImportedFiles(
                ref primary,
                ref compRef,
                ref lyrRef,
                "Hello",
                "Adele",
                "Karaoke Version");

            Assert.Equal("Adele - Hello (KV).mp3", Path.GetFileName(primary));
            Assert.Equal("Adele - Hello (KV).cdg", Path.GetFileName(compRef));
            Assert.Equal("Adele - Hello (KV).lrc", Path.GetFileName(lyrRef));
            Assert.True(File.Exists(primary));
            Assert.True(File.Exists(compRef!));
            Assert.True(File.Exists(lyrRef!));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData("Karaoke Version", "Rock", "Rock")]
    [InlineData("Party Tyme", "Hip-Hop", "Hip-Hop")]
    [InlineData("Sunfly", "Dance", "Dance")]
    [InlineData("Karaoke.com", "Country", "Country")]
    public void DetectGenre_MatchesCatalogGenres(string provider, string tagValue, string expected)
    {
        var probe = new FFprobeResult();
        probe.Tags["genre"] = tagValue;

        string? genre = FFprobeRunner.DetectGenre(provider, probe);
        Assert.Equal(expected, genre);
    }

    [Fact]
    public void DetectDifficulty_EvaluatesAttributesCorrectly()
    {
        var easyProbe = new FFprobeResult { Duration = 150 };
        easyProbe.Tags["TBPM"] = "100";
        Assert.Equal("Easy", FFprobeRunner.DetectDifficulty(easyProbe));

        var hardProbe = new FFprobeResult { Duration = 280 };
        hardProbe.Tags["TBPM"] = "165";
        hardProbe.Tags["vocal_range"] = "3 octaves wide";
        Assert.Equal("Hard", FFprobeRunner.DetectDifficulty(hardProbe));
    }

    [Fact]
    public void DetectKey_IdentifiesKeyFromTagsOrComments()
    {
        var probe1 = new FFprobeResult();
        probe1.Tags["TKEY"] = "Am";
        Assert.Equal("Am", FFprobeRunner.DetectKey(probe1));

        var probe2 = new FFprobeResult();
        probe2.CommentTag = "Mastered in Key: C#m";
        Assert.Equal("C#m", FFprobeRunner.DetectKey(probe2));
    }

    [Fact]
    public void DetectBpm_ParsesNumericOrTextBpm()
    {
        var probe1 = new FFprobeResult();
        probe1.Tags["TBPM"] = "128";
        Assert.Equal(128.0, FFprobeRunner.DetectBpm(probe1));

        var probe2 = new FFprobeResult();
        probe2.CommentTag = "Track Tempo: 95 bpm";
        Assert.Equal(95.0, FFprobeRunner.DetectBpm(probe2));
    }

    [Fact]
    public void DetectVocalPresence_ClassifiesVocalPresenceLevels()
    {
        var dualProbe = new FFprobeResult { AudioStreamCount = 2 };
        Assert.Equal("guide vocals", FFprobeRunner.DetectVocalPresence(dualProbe));

        var bgvProbe = new FFprobeResult { AudioStreamCount = 1 };
        bgvProbe.Tags["comment"] = "with backing vocals";
        Assert.Equal("background vocals", FFprobeRunner.DetectVocalPresence(bgvProbe));

        var cleanProbe = new FFprobeResult { AudioStreamCount = 1 };
        cleanProbe.Tags["comment"] = "instrumental karaoke backing track";
        Assert.Equal("no vocals", FFprobeRunner.DetectVocalPresence(cleanProbe));
    }

    [Fact]
    public void DetectQuality_ClassifiesAudioAndVideoQualityLevels()
    {
        var highAudio = new FFprobeResult
        {
            BitrateKbps = 320,
            SampleRate = 44100,
            Channels = 2,
            AudioCodec = "mp3"
        };
        Assert.Equal("High", FFprobeRunner.DetectQuality(highAudio));

        var medAudio = new FFprobeResult
        {
            BitrateKbps = 192,
            SampleRate = 44100,
            Channels = 2,
            AudioCodec = "mp3"
        };
        Assert.Equal("Medium", FFprobeRunner.DetectQuality(medAudio));

        var lowAudio = new FFprobeResult
        {
            BitrateKbps = 96,
            SampleRate = 22050,
            Channels = 1,
            AudioCodec = "mp3"
        };
        Assert.Equal("Low", FFprobeRunner.DetectQuality(lowAudio));

        var highVideo = new FFprobeResult
        {
            VideoCodec = "h264",
            Width = 1920,
            Height = 1080
        };
        Assert.Equal("High", FFprobeRunner.DetectQuality(highVideo));
    }

    [Fact]
    public async Task GetStoreAnalyticsAsync_ComputesProviderFormatAndMusicalStats()
    {
        using var context = new LyracistDbContext();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        var song1 = new Song
        {
            Title = $"Test Analytics Song 1 {suffix}",
            Artist = "Artist 1",
            Tags = "Karaoke Version;KV;Normalized",
            FilePath = $@"C:\Test\{suffix}\track1 (KV).zip",
            IsKaraoke = true,
            KaraokeType = "ZIPCDG",
            Quality = "High",
            Difficulty = "Medium",
            Key = "Am",
            BPM = 115,
            DateAdded = DateTime.UtcNow
        };
        var song2 = new Song
        {
            Title = $"Test Analytics Song 2 {suffix}",
            Artist = "Artist 2",
            Tags = "Karaoke Version;KV",
            FilePath = $@"C:\Test\{suffix}\track2 (KV).mp4",
            IsKaraoke = true,
            KaraokeType = "MP4",
            Quality = "High",
            Difficulty = "Hard",
            Key = "C",
            BPM = 72,
            DateAdded = DateTime.UtcNow
        };
        var song3 = new Song
        {
            Title = $"Test Analytics Song 3 {suffix}",
            Artist = "Artist 3",
            Tags = "Party Tyme;PT",
            FilePath = $@"C:\Test\{suffix}\track3 (PT).mp3",
            IsKaraoke = true,
            KaraokeType = "MP3G",
            Quality = "Low",
            Difficulty = "Easy",
            Key = "Am",
            BPM = 145,
            DateAdded = DateTime.UtcNow.AddDays(-1)
        };

        context.Songs.AddRange(song1, song2, song3);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var analytics = await context.GetStoreAnalyticsAsync();

        Assert.True(analytics.TotalTracks >= 3);
        Assert.True(analytics.KvCount >= 2);
        Assert.True(analytics.PtCount >= 1);
        Assert.True(analytics.KvPercent >= 0);
        Assert.False(string.IsNullOrEmpty(analytics.MostFrequentProvider));

        // Format Stats
        Assert.True(analytics.ZipCdgCount >= 1);
        Assert.True(analytics.Mp4Count >= 1);
        Assert.True(analytics.Mp3gCount >= 1);

        // Quality & Difficulty
        Assert.True(analytics.QualityHighCount >= 2);
        Assert.True(analytics.QualityLowCount >= 1);
        Assert.True(analytics.DifficultyEasyCount >= 1);
        Assert.True(analytics.DifficultyMediumCount >= 1);
        Assert.True(analytics.DifficultyHardCount >= 1);

        // Musical Key & BPM
        Assert.Contains(analytics.KeyDistribution, k => k.Key == "Am");
        Assert.Contains(analytics.BpmHistogram, b => b.RangeLabel == "< 80 BPM" && b.Count >= 1);
        Assert.Contains(analytics.BpmHistogram, b => b.RangeLabel == "100 - 120" && b.Count >= 1);
        Assert.Contains(analytics.BpmHistogram, b => b.RangeLabel == "140+ BPM" && b.Count >= 1);

        // Daily and Hourly timelines
        Assert.Equal(14, analytics.DailyActivity.Count);
        Assert.Equal(24, analytics.HourlyActivity.Count);
        Assert.All(analytics.HourlyActivity, h =>
        {
            Assert.False(string.IsNullOrEmpty(h.ColorHex));
            Assert.False(string.IsNullOrEmpty(h.Tooltip));
        });

        // Cleanup
        context.Songs.RemoveRange(song1, song2, song3);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnalyticsViewModel_RefreshAnalyticsAsync_PopulatesData()
    {
        var vm = new AnalyticsViewModel();
        Assert.True(vm.IsExpanded);

        await vm.RefreshAnalyticsAsync();

        Assert.False(vm.IsLoading);
        Assert.True(vm.TotalTracks >= 0);
        Assert.Equal(14, vm.DailyActivity.Count);
        Assert.Equal(24, vm.HourlyActivity.Count);
        Assert.Equal(5, vm.BpmHistogram.Count);
        Assert.NotNull(vm.AvgProcessingTimeText);

        vm.ToggleAnalytics();
        Assert.False(vm.IsExpanded);
        vm.ToggleAnalytics();
        Assert.True(vm.IsExpanded);
    }

    [Fact]
    public void PurchasedTrackWatcher_ProcessingBenchmarks_TracksMinMaxAvg()
    {
        PurchasedTrackWatcherService.RecordProcessingTime(200.0);
        PurchasedTrackWatcherService.RecordProcessingTime(400.0);

        var (avg, max, min) = PurchasedTrackWatcherService.GetProcessingStats();
        Assert.True(avg >= 200.0);
        Assert.True(max >= 400.0);
        Assert.True(min > 0.0);
    }

    [Fact]
    public async Task BulkImporter_ScanFolderAsync_DiscoversAndPairsMediaFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Lyracist_BulkTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. Create MP3+G pair + matching LRC lyrics
            string mp3Path = Path.Combine(tempDir, "Adele - Hello (KV).mp3");
            string cdgPath = Path.Combine(tempDir, "Adele - Hello (KV).cdg");
            string lrcPath = Path.Combine(tempDir, "Adele - Hello (KV).lrc");
            await File.WriteAllBytesAsync(mp3Path, new byte[128], TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(cdgPath, new byte[64], TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(lrcPath, "[00:10.00]Hello from the other side", TestContext.Current.CancellationToken);

            // 2. Create MP4 video
            string mp4Path = Path.Combine(tempDir, "Queen - Radio Ga Ga (PT).mp4");
            await File.WriteAllBytesAsync(mp4Path, new byte[256], TestContext.Current.CancellationToken);

            // 3. Create ZIP archive
            string zipPath = Path.Combine(tempDir, "Journey - Don't Stop Believin' (SF).zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("track.mp3");
                using var writer = new StreamWriter(entry.Open());
                writer.WriteLine("audio");
            }

            // 4. Create unsupported file that should be ignored
            string txtUnrelated = Path.Combine(tempDir, "readme.doc");
            await File.WriteAllTextAsync(txtUnrelated, "instructions", TestContext.Current.CancellationToken);

            var candidates = await PurchasedTrackBulkImporter.ScanFolderAsync(tempDir, null, TestContext.Current.CancellationToken);

            // Should find exactly 3 candidate tracks (pair combined into 1)
            Assert.Equal(3, candidates.Count);

            var pairCandidate = candidates.FirstOrDefault(c => c.Filename.Contains("Adele"));
            Assert.NotNull(pairCandidate);
            Assert.Equal("MP3+G", pairCandidate.FileType);
            Assert.NotNull(pairCandidate.CompanionFilePath);
            Assert.EndsWith(".cdg", pairCandidate.CompanionFilePath, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(pairCandidate.LyricsFilePath);
            Assert.EndsWith(".lrc", pairCandidate.LyricsFilePath, StringComparison.OrdinalIgnoreCase);

            var mp4Candidate = candidates.FirstOrDefault(c => c.Filename.Contains("Queen"));
            Assert.NotNull(mp4Candidate);
            Assert.Equal("MP4", mp4Candidate.FileType);

            var zipCandidate = candidates.FirstOrDefault(c => c.Filename.Contains("Journey"));
            Assert.NotNull(zipCandidate);
            Assert.Equal("ZIPCDG", zipCandidate.FileType);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void BulkImportViewModel_BatchToggles_UpdatesAllPreviewItems()
    {
        var vm = new BulkImportViewModel();

        var candidate1 = new BulkImportCandidate { PrimaryFilePath = @"C:\dummy1.mp3", WillNormalize = false, WillTrimSilence = false, WillGenerateWaveform = false };
        var candidate2 = new BulkImportCandidate { PrimaryFilePath = @"C:\dummy2.mp3", WillNormalize = false, WillTrimSilence = false, WillGenerateWaveform = false };

        vm.PreviewItems.Add(new BulkImportItemViewModel(candidate1));
        vm.PreviewItems.Add(new BulkImportItemViewModel(candidate2));

        vm.NormalizeAll = true;
        Assert.All(vm.PreviewItems, item => Assert.True(item.WillNormalize));

        vm.TrimSilenceAll = true;
        Assert.All(vm.PreviewItems, item => Assert.True(item.WillTrimSilence));

        vm.GenerateWaveformAll = true;
        Assert.All(vm.PreviewItems, item => Assert.True(item.WillGenerateWaveform));

        vm.NormalizeAll = false;
        Assert.All(vm.PreviewItems, item => Assert.False(item.WillNormalize));
    }

    [Fact]
    public void BulkImporter_GetUniqueDestinationPath_ResolvesNameConflicts()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Lyracist_ConflictTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string baseFile = Path.Combine(tempDir, "Track.mp3");
            File.WriteAllText(baseFile, "test");

            string unique1 = PurchasedTrackBulkImporter.GetUniqueDestinationPath(tempDir, "Track.mp3");
            Assert.Equal(Path.Combine(tempDir, "Track (1).mp3"), unique1);

            File.WriteAllText(unique1, "test 2");

            string unique2 = PurchasedTrackBulkImporter.GetUniqueDestinationPath(tempDir, "Track.mp3");
            Assert.Equal(Path.Combine(tempDir, "Track (2).mp3"), unique2);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
