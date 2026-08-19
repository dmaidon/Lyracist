// Edited on Aug 19, 2026 @ 11:08:45 -> Use isolated temp directory for banner generation test
using System;
using System.IO;
using System.Threading;
using Lyracist.Trivia.Core.Services;
using Lyracist.Trivia.Services;
using Xunit;

namespace Lyracist.Trivia.Tests;

public class BannerGeneratorTests
{
    [Fact]
    public void GenerateAllBanners_GeneratesAll15CategoryBannerImages()
    {
        string bannersDir = Path.Combine(Path.GetTempPath(), $"banners_test_{Guid.NewGuid():N}");
        if (!Directory.Exists(bannersDir))
        {
            Directory.CreateDirectory(bannersDir);
        }

        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                TriviaBannerGenerator.GenerateAllBanners(bannersDir);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            throw new InvalidOperationException($"Error in STA thread generating banners: {threadEx.Message}", threadEx);
        }

        Assert.Equal(15, TriviaBannerGenerator.AllBanners.Length);

        foreach (var def in TriviaBannerGenerator.AllBanners)
        {
            string filePath = Path.Combine(bannersDir, def.FileName);
            Assert.True(File.Exists(filePath), $"Banner file does not exist: {filePath}");
            var fileInfo = new FileInfo(filePath);
            Assert.True(fileInfo.Length > 10000, $"Banner file {def.FileName} should be at least 10KB (was {fileInfo.Length} bytes)");
        }

        // Also ensure official TriviaData/Banners directory has all 15 banners
        string prodBannersDir = TriviaStorageHelper.GetBannersDirectory();
        if (Directory.Exists(prodBannersDir))
        {
            foreach (var def in TriviaBannerGenerator.AllBanners)
            {
                try
                {
                    string src = Path.Combine(bannersDir, def.FileName);
                    string dest = Path.Combine(prodBannersDir, def.FileName);
                    if (!File.Exists(dest))
                    {
                        File.Copy(src, dest, true);
                    }
                }
                catch
                {
                }
            }
        }

        try { Directory.Delete(bannersDir, true); } catch { }
    }
}
