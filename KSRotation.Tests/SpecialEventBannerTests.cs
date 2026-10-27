// Created on Oct 26, 2026 @ 22:58:00 -> Unit tests for special event banner resolution, standard file generation, and display routing
using System;
using System.IO;
using System.Linq;
using Lyracist.Shared;
using Xunit;

namespace KSRotation.Tests;

public class SpecialEventBannerTests
{
    [Theory]
    [InlineData("Birthday", "Birthday.png")]
    [InlineData("Wedding", "Wedding.png")]
    [InlineData("Engagement", "Engagement.png")]
    [InlineData("Anniversary", "Anniversary.png")]
    [InlineData("Last Song", "LastSong.png")]
    [InlineData("Connect Instructions", "ConnectInstructions.png")]
    [InlineData("Announcement", "Announcement.png")]
    public void GetStandardBannerFileName_ReturnsExpectedFileName(string eventName, string expectedFileName)
    {
        string actual = DjBannerFileManager.GetStandardBannerFileName(eventName);
        Assert.Equal(expectedFileName, actual);
    }

    [Fact]
    public void StandardEventNames_ContainsAllCoreCelebrationEvents()
    {
        Assert.Contains("Birthday", DjBannerFileManager.StandardEventNames);
        Assert.Contains("Wedding", DjBannerFileManager.StandardEventNames);
        Assert.Contains("Engagement", DjBannerFileManager.StandardEventNames);
        Assert.Contains("Anniversary", DjBannerFileManager.StandardEventNames);
        Assert.Contains("Last Song", DjBannerFileManager.StandardEventNames);
        Assert.Contains("Announcement", DjBannerFileManager.StandardEventNames);
    }

    [Fact]
    public void SpecialEventConfig_IsStandard_IdentifiesStandardEventsAccurately()
    {
        var birthday = new SpecialEventConfig { EventName = "Birthday", BannerFileName = "Birthday.png" };
        var wedding = new SpecialEventConfig { EventName = "Wedding", BannerFileName = "Wedding.png" };
        var custom = new SpecialEventConfig { EventName = "Trivia Finals", BannerFileName = "trivia_finals.png" };

        Assert.True(birthday.IsStandard);
        Assert.True(wedding.IsStandard);
        Assert.False(custom.IsStandard);
    }

    [Fact]
    public void EnsureStandardEventBanners_GeneratesAllMissingStandardBanners()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"banner_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            DjBannerFileManager.EnsureStandardEventBanners(tempDir);

            string[] expectedBanners = ["Birthday.png", "Wedding.png", "Engagement.png", "Anniversary.png", "LastSong.png", "Announcement.png"];
            foreach (var banner in expectedBanners)
            {
                string path = Path.Combine(tempDir, banner);
                Assert.True(File.Exists(path), $"Expected banner '{banner}' to exist at '{path}'.");
                Assert.True(new FileInfo(path).Length > 0, $"Expected banner '{banner}' to have non-zero size.");
            }
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
    public void ResolveSpecialEventBannerPath_ReturnsNullForNoneOrEmpty()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"banner_test_{Guid.NewGuid():N}");
        Assert.Null(DjBannerFileManager.ResolveSpecialEventBannerPath(null, [], dir));
        Assert.Null(DjBannerFileManager.ResolveSpecialEventBannerPath("", [], dir));
        Assert.Null(DjBannerFileManager.ResolveSpecialEventBannerPath("None", [], dir));
    }

    [Fact]
    public void ResolveSpecialEventBannerPath_GeneratesMissingStandardBannerAndHonorsCustomFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"banner_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);

            string? birthday = DjBannerFileManager.ResolveSpecialEventBannerPath("Birthday", [], dir);
            Assert.Equal(Path.Combine(dir, "Birthday.png"), birthday);
            Assert.True(File.Exists(birthday));

            File.WriteAllBytes(Path.Combine(dir, "finals.png"), [1]);
            var custom = new[] { new SpecialEventConfig { EventName = "Trivia Finals", BannerFileName = "finals.png" } };
            Assert.Equal(Path.Combine(dir, "finals.png"), DjBannerFileManager.ResolveSpecialEventBannerPath("Trivia Finals", custom, dir));

            Assert.Null(DjBannerFileManager.ResolveSpecialEventBannerPath("Unknown Event", [], dir));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
