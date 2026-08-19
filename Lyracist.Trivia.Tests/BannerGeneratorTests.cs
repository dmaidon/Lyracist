// Created on Aug 19, 2026 @ 09:27:45 -> Unit test for generating all 14 16:9 category announcement banners
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
    public void GenerateAllBanners_GeneratesAll14CategoryBannerImages()
    {
        string bannersDir = TriviaStorageHelper.GetBannersDirectory();
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

        Assert.Equal(14, TriviaBannerGenerator.AllBanners.Length);

        foreach (var def in TriviaBannerGenerator.AllBanners)
        {
            string filePath = Path.Combine(bannersDir, def.FileName);
            Assert.True(File.Exists(filePath), $"Banner file does not exist: {filePath}");
            var fileInfo = new FileInfo(filePath);
            Assert.True(fileInfo.Length > 10000, $"Banner file {def.FileName} should be at least 10KB (was {fileInfo.Length} bytes)");
        }
    }
}
