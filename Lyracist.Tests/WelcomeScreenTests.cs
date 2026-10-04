// Edited on Oct 4, 2026 @ 09:57:00 -> Add unit tests for Pre-Show welcome screen suppression and sequenced release
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
    public void WelcomeDesignChoices_ContainsAllAndEveryDesign()
    {
        var choices = WelcomeScreenDesigns.Choices;
        Assert.Equal(WelcomeScreenDesigns.Count + 1, choices.Count);
        Assert.Equal(-1, choices[0].Id);
        Assert.Equal("All (Random)", choices[0].Name);
        for (int i = 0; i < WelcomeScreenDesigns.Count; i++)
        {
            Assert.Equal(i, choices[i + 1].Id);
            Assert.False(string.IsNullOrWhiteSpace(choices[i + 1].Name));
        }
    }

    [Fact]
    public void SelectedDesign_WhenFixed_AlwaysUsesSelectedDesign()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var service = new WelcomeScreenService { SelectedDesign = 2, Dispatcher = Dispatcher.CurrentDispatcher };
                service.Preview("Singer One");
                Assert.NotNull(service.Current);
                Assert.Equal(2, service.Current.Design);
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

    [Fact]
    public void SelectedDesign_WhenNegativeOne_PicksInRange()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var service = new WelcomeScreenService { SelectedDesign = -1, Dispatcher = Dispatcher.CurrentDispatcher };
                service.Preview("Singer Two");
                Assert.NotNull(service.Current);
                Assert.InRange(service.Current.Design, 0, WelcomeScreenDesigns.Count - 1);
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
    public void PreShowMode_SuppressesWelcomeScreensUntilPreShowCloses_ThenShowsInSequence()
    {
        var seen = new List<string?>();
        var service = new WelcomeScreenService { SecondsUnit = TimeSpan.FromMilliseconds(40) };
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            service.Dispatcher = dispatcher;
            service.Seconds = 3; // 3 x 40 ms per welcome
            service.CurrentChanged += (_, r) =>
            {
                lock (seen)
                {
                    seen.Add(r?.Name);
                }
            };
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait(TestContext.Current.CancellationToken);

        // Pre-show mode is active before singers arrive
        service.IsPreShowActive = true;

        Assert.True(service.TryWelcome("Alice PreShow"));
        Assert.True(service.TryWelcome("Bob PreShow"));
        Assert.True(service.TryWelcome("Charlie PreShow"));

        // Wait 250 ms to ensure nothing fired while pre-show was active
        Thread.Sleep(250);
        lock (seen)
        {
            Assert.Empty(seen);
        }
        Assert.Null(service.Current);

        // Pre-show screen is closed (turned off)
        service.IsPreShowActive = false;

        // Welcomes must now play in sequence: Alice, Bob, Charlie, then null
        SpinWait.SpinUntil(() => { lock (seen) return seen.Count >= 4; }, TimeSpan.FromSeconds(5));
        dispatcher!.InvokeShutdown();
        thread.Join();

        lock (seen)
        {
            Assert.Equal(new string?[] { "Alice PreShow", "Bob PreShow", "Charlie PreShow", null }, seen);
        }
    }

    [Fact]
    public void PreShowMode_InterruptsActiveWelcome_AndResumesOnClose()
    {
        var seen = new List<string?>();
        var service = new WelcomeScreenService { SecondsUnit = TimeSpan.FromMilliseconds(80) };
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            service.Dispatcher = dispatcher;
            service.Seconds = 20; // 20 x 80 = 1600 ms, plenty of time to interrupt mid-welcome
            service.CurrentChanged += (_, r) =>
            {
                lock (seen)
                {
                    seen.Add(r?.Name);
                }
            };
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait(TestContext.Current.CancellationToken);

        // Start welcome normally
        Assert.True(service.TryWelcome("Active Singer"));
        SpinWait.SpinUntil(() => { lock (seen) return seen.Count >= 1; }, TimeSpan.FromSeconds(2));
        Assert.NotNull(service.Current);

        // Engage pre-show mode mid-welcome -> should immediately interrupt and set Current to null
        service.IsPreShowActive = true;
        SpinWait.SpinUntil(() => service.Current == null, TimeSpan.FromSeconds(2));
        Assert.Null(service.Current);

        // Add another singer during pre-show
        Assert.True(service.TryWelcome("Second Singer"));

        // Close pre-show mode -> should resume Active Singer, then Second Singer, then finish
        service.Seconds = 2; // 2 x 80ms on resume
        service.IsPreShowActive = false;
        SpinWait.SpinUntil(() => { lock (seen) return seen.Count >= 5; }, TimeSpan.FromSeconds(5));
        dispatcher!.InvokeShutdown();
        thread.Join();

        lock (seen)
        {
            Assert.Equal(new string?[] { "Active Singer", null, "Active Singer", "Second Singer", null }, seen);
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void SignUpInvite_BuildsAndRenders_WithAndWithoutQr(bool withQr, bool withWifi)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                BitmapSource? qr = null;
                if (withQr)
                {
                    var pixels = new byte[64 * 64 * 4];
                    for (int i = 0; i < pixels.Length; i += 4) { byte v = (byte)(((i / 4) / 8 + (i / 4) / 64 / 8) % 2 == 0 ? 0 : 255); pixels[i] = pixels[i + 1] = pixels[i + 2] = v; pixels[i + 3] = 255; }
                    qr = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, pixels, 64 * 4);
                    qr.Freeze();
                }

                BitmapSource? wifiQr = null;
                if (withWifi)
                {
                    var pixels = new byte[64 * 64 * 4];
                    for (int i = 0; i < pixels.Length; i += 4) { byte v = (byte)(((i / 4) / 8 + (i / 4) / 64 / 8) % 2 == 0 ? 255 : 0); pixels[i] = pixels[i + 1] = pixels[i + 2] = v; pixels[i + 3] = 255; }
                    wifiQr = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, pixels, 64 * 4);
                    wifiQr.Freeze();
                }

                using var visual = WelcomeScreenDesigns.BuildSignUpInvite(
                    qr,
                    withQr ? "http://192.168.1.50:8080" : null,
                    wifiQr,
                    withWifi ? "TheVenueWiFi" : null,
                    withWifi ? "Secret123" : null);
                var root = visual.Root;
                root.Measure(new Size(1920, 1080));
                root.Arrange(new Rect(0, 0, 1920, 1080));
                root.UpdateLayout();
                var bitmap = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);

                string? previewDir = Environment.GetEnvironmentVariable("WELCOME_PREVIEW_DIR");
                if (!string.IsNullOrEmpty(previewDir))
                {
                    Directory.CreateDirectory(previewDir);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(previewDir, $"invite_qr{withQr}_wifi{withWifi}.png"));
                    encoder.Save(stream);
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
