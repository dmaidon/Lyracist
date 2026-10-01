// Created on Oct 1, 2026 @ 11:20:00 -> Tests for the shared "Welcome to our new performer" service and designs
using System.IO;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lyracist.Shared;

namespace Lyracist.Tests;

public class WelcomeScreenTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("New Singer")]
    [InlineData("  new   singer ")]
    [InlineData("Special Guest")]
    [InlineData("Music Request")]
    public void IsPlaceholderName_TrueForBlankAndAppPlaceholders(string? name)
    {
        Assert.True(WelcomeScreenService.IsPlaceholderName(name));
    }

    [Theory]
    [InlineData("Alice")]
    [InlineData("New Singer Dave")]
    public void IsPlaceholderName_FalseForRealNames(string name)
    {
        Assert.False(WelcomeScreenService.IsPlaceholderName(name));
    }

    [Fact]
    public void NormalizeName_TrimsAndCollapsesWhitespace()
    {
        Assert.Equal("Mary Jane Watson", WelcomeScreenService.NormalizeName("  Mary   Jane\tWatson "));
    }

    [Theory]
    [InlineData(-5, WelcomeScreenService.MinSeconds)]
    [InlineData(0, WelcomeScreenService.MinSeconds)]
    [InlineData(15, 15)]
    [InlineData(9999, WelcomeScreenService.MaxSeconds)]
    public void ClampSeconds_KeepsValueInRange(int input, int expected)
    {
        Assert.Equal(expected, WelcomeScreenService.ClampSeconds(input));
    }

    [Fact]
    public void Seconds_DefaultsToFifteen()
    {
        Assert.Equal(15, new WelcomeScreenService().Seconds);
    }

    [Fact]
    public void TryWelcome_WelcomesEachNameOncePerNight_UntilReset()
    {
        var service = new WelcomeScreenService();

        Assert.True(service.TryWelcome("Alice Test"));
        Assert.False(service.TryWelcome("alice   test"));
        Assert.True(service.HasBeenWelcomed("ALICE TEST"));

        service.ResetTonight();
        Assert.True(service.TryWelcome("Alice Test"));
    }

    [Fact]
    public void TryWelcome_IgnoresPlaceholdersAndDisabledState()
    {
        var service = new WelcomeScreenService();
        Assert.False(service.TryWelcome("New Singer"));

        service.Enabled = false;
        Assert.False(service.TryWelcome("Bob Test"));

        // A disabled attempt must not burn the name: turning the feature back on still welcomes them.
        service.Enabled = true;
        Assert.True(service.TryWelcome("Bob Test"));
    }


    [Fact]
    public void SeveralNames_AreShownOneAfterAnother_ThenClear()
    {
        var seen = new List<string?>();
        var service = new WelcomeScreenService { SecondsUnit = TimeSpan.FromMilliseconds(40) };
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            service.Dispatcher = dispatcher;
            service.Seconds = 5; // 5 x 40 ms per welcome
            service.CurrentChanged += (_, r) => seen.Add(r?.Name);
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait(TestContext.Current.CancellationToken);

        // Entered in quick succession, including a repeat of the first name.
        Assert.True(service.TryWelcome("Order One"));
        Assert.True(service.TryWelcome("Order Two"));
        Assert.True(service.TryWelcome("Order Three"));
        Assert.False(service.TryWelcome("order one"));

        SpinWait.SpinUntil(() => { lock (seen) return seen.Count >= 4; }, TimeSpan.FromSeconds(5));
        dispatcher!.InvokeShutdown();
        thread.Join();

        Assert.Equal(new string?[] { "Order One", "Order Two", "Order Three", null }, seen);
    }

    [Fact]
    public void Designs_AllBuildAndRender()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                string? previewDir = Environment.GetEnvironmentVariable("WELCOME_PREVIEW_DIR");
                for (int design = 0; design < WelcomeScreenDesigns.Count; design++)
                {
                    foreach (string name in new[] { "Alice Johnson", "Maximilian Alexander Featherstonehaugh-Cholmondeley III" })
                    {
                        using var visual = WelcomeScreenDesigns.Build(new WelcomeRequest(name, design, 15));
                        var root = visual.Root;
                        root.Measure(new Size(1920, 1080));
                        root.Arrange(new Rect(0, 0, 1920, 1080));
                        root.UpdateLayout();

                        var bitmap = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(root);

                        if (!string.IsNullOrEmpty(previewDir))
                        {
                            Directory.CreateDirectory(previewDir);
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            using var stream = File.Create(Path.Combine(previewDir, $"welcome_{design}_{name.Length}.png"));
                            encoder.Save(stream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }
}
