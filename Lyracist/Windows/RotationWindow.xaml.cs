// Edited on Sep 22, 2026 @ 00:06:00 -> Add Casino Slot Reels, Jukebox, Stadium Jumbotron, and Movie Theater 'Now Showing' projection view animations and handlers
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.Shared;
using Lyracist.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Lyracist.Windows;

public partial class RotationWindow : Window, ICaptureSource
{
    private static readonly Random _rng = new();

    private static readonly SolidColorBrush CrawlGold;
    private static readonly SolidColorBrush CrawlDimGold;
    private static readonly System.Windows.Media.Color MarqueeBulbGlow = System.Windows.Media.Color.FromRgb(0xA8, 0x55, 0xF7);

    static RotationWindow()
    {
        CrawlGold = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00));
        CrawlGold.Freeze();
        CrawlDimGold = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xA0, 0x00));
        CrawlDimGold.Freeze();

        // Bulb core + halo in one brush. The ellipse is drawn MarqueeBulbGlowScale times the bulb's
        // size; the stops out to `core` (the bulb's own radius) are the lit bulb, and the rest fades
        // the glow color out to transparent. Replaces a per-bulb DropShadowEffect: ~100 separate
        // blur shaders re-rendered every time the chase pattern flipped a bulb's opacity (every
        // 110ms), the most GPU-expensive thing on screen.
        const double core = 1.0 / MarqueeBulbGlowScale;
        MarqueeBulbGlowBrush = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(System.Windows.Media.Color.FromRgb(0xF3, 0xE8, 0xFF), 0.0),
                new GradientStop(System.Windows.Media.Color.FromRgb(0xA8, 0x55, 0xF7), core * 0.55),
                new GradientStop(System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED), core),
                new GradientStop(System.Windows.Media.Color.FromArgb(0xB0, MarqueeBulbGlow.R, MarqueeBulbGlow.G, MarqueeBulbGlow.B), core + 0.02),
                new GradientStop(System.Windows.Media.Color.FromArgb(0x40, MarqueeBulbGlow.R, MarqueeBulbGlow.G, MarqueeBulbGlow.B), core + ((1 - core) * 0.4)),
                new GradientStop(System.Windows.Media.Color.FromArgb(0x00, MarqueeBulbGlow.R, MarqueeBulbGlow.G, MarqueeBulbGlow.B), 1.0)
            }
        };
        MarqueeBulbGlowBrush.Freeze();
    }

    private const double MarqueeBulbGlowScale = 1.8;
    private static readonly RadialGradientBrush MarqueeBulbGlowBrush;

    private const double PanelWidth = 800;
    private const double ViewH = 1200;
    private const double ScrollPixelsPerSecond = 90;

    private readonly List<Ellipse> _marqueeBulbs = [];
    private DispatcherTimer? _marqueeTimer;
    private int _marqueeStep;
    private const int MarqueeLitPeriod = 3;
    private const double MarqueeDimOpacity = 0.18;

    private readonly List<RotateTransform> _festivalBeamRotates = [];
    private readonly List<Ellipse> _festivalSparkles = [];
    private static readonly System.Windows.Media.Color[] FestivalBeamPalette =
    [
        System.Windows.Media.Color.FromRgb(0xFF, 0xF0, 0xC8),
        System.Windows.Media.Color.FromRgb(0xFF, 0xC2, 0x4A),
        System.Windows.Media.Color.FromRgb(0xFF, 0x4A, 0x4A),
        System.Windows.Media.Color.FromRgb(0xFF, 0x8A, 0x3D),
        System.Windows.Media.Color.FromRgb(0xFF, 0xD8, 0x4D),
        System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x9E),
    ];
    private static readonly System.Windows.Media.Color[] FestivalSparklePalette =
    [
        System.Windows.Media.Color.FromRgb(0xFF, 0xF3, 0xB0),
        System.Windows.Media.Color.FromRgb(0xFF, 0xE0, 0x6B),
        System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF),
        System.Windows.Media.Color.FromRgb(0xFF, 0xC2, 0x4A),
    ];

    private DispatcherTimer? _synthGridTimer;
    private readonly List<System.Windows.Media.Color> _synthLineColors =
    [
        System.Windows.Media.Color.FromRgb(0x33, 0xD4, 0xFF),
        System.Windows.Media.Color.FromRgb(0xFF, 0x2F, 0xE0),
    ];
    private int _synthLineColorIndex;

    private readonly List<RotateTransform> _discoBeamRotates = [];
    private readonly List<Ellipse> _discoLightSpots = [];
    private static readonly System.Windows.Media.Color[] DiscoBeamPalette =
    [
        System.Windows.Media.Color.FromRgb(0xFF, 0x2F, 0xE0),
        System.Windows.Media.Color.FromRgb(0x33, 0xD4, 0xFF),
        System.Windows.Media.Color.FromRgb(0x9B, 0x5C, 0xFF),
        System.Windows.Media.Color.FromRgb(0xFF, 0xD8, 0x4D),
        System.Windows.Media.Color.FromRgb(0x4C, 0xFF, 0xB0),
        System.Windows.Media.Color.FromRgb(0xFF, 0x8A, 0x3D),
    ];
    private static readonly System.Windows.Media.Color[] DiscoLightPalette =
    [
        System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0xE8),
        System.Windows.Media.Color.FromRgb(0x6B, 0xD4, 0xFF),
        System.Windows.Media.Color.FromRgb(0xC1, 0x8B, 0xFF),
        System.Windows.Media.Color.FromRgb(0xFF, 0xE0, 0x6B),
        System.Windows.Media.Color.FromRgb(0x6B, 0xFF, 0xC4),
    ];

    // ── Casino Slot Reels state ──────────────────────────────────────────
    private readonly List<UIElement> _slotCoins = [];
    private static readonly System.Windows.Media.Color[] SlotParticlePalette =
    [
        System.Windows.Media.Color.FromRgb(0xFF, 0xF2, 0xA3),
        System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00),
        System.Windows.Media.Color.FromRgb(0xFF, 0xC4, 0x00),
        System.Windows.Media.Color.FromRgb(0xFF, 0xA5, 0x00),
        System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF),
    ];

    // ── Jukebox state ───────────────────────────────────────────────────
    private readonly List<UIElement> _jukeboxBubbles = [];
    private static readonly System.Windows.Media.Color[] JukeboxBubblePalette =
    [
        System.Windows.Media.Color.FromRgb(0x00, 0xE5, 0xFF),
        System.Windows.Media.Color.FromRgb(0xFF, 0x35, 0x7E),
        System.Windows.Media.Color.FromRgb(0xFF, 0xD2, 0x69),
        System.Windows.Media.Color.FromRgb(0x8E, 0x44, 0xAD),
        System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0xCC),
    ];

    // ── Stadium Jumbotron state ─────────────────────────────────────────
    private readonly List<RotateTransform> _jumbotronSpotlightRotates = [];

    // ── Movie Theater state ─────────────────────────────────────────────
    private readonly List<UIElement> _theaterBeams = [];

    private RotationWindowViewModel? _vm;
    private int _crawlGen;   // incremented to invalidate in-flight loops
    private VisualBrush? _crawlBrush;
    private DispatcherTimer? _spaceshipTimer;
    private DispatcherTimer? _starRegenTimer;
    private double _lastStarW;
    private double _lastStarH;

    // Off-tree source for the crawl VisualBrush.
    private readonly Canvas _crawlSource = new()
    {
        Width = PanelWidth,
        Height = ViewH,
        Background = System.Windows.Media.Brushes.Transparent
    };

    public RotationWindow(RotationWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += RotationWindow_IsVisibleChanged;
        SizeChanged += OnSizeChanged;
        PreviewKeyDown += OnPreviewKeyDown;

        Lyracist.Services.Tablet.LyricsHub.ReactionReceived += OnReactionReceived;
    }

    public void UpdateLastSongBanner(string? bannerPath)
    {
        if (!string.IsNullOrWhiteSpace(bannerPath) && File.Exists(bannerPath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(bannerPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                LastSongBannerImage.Source = bitmap;
                LastSongBannerOverlay.Visibility = Visibility.Visible;
                return;
            }
            catch
            {
                // Fallback on decode failure
            }
        }

        LastSongBannerOverlay.Visibility = Visibility.Collapsed;
        LastSongBannerImage.Source = null;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        Closed -= OnClosed;
        DataContextChanged -= OnDataContextChanged;
        IsVisibleChanged -= RotationWindow_IsVisibleChanged;
        SizeChanged -= OnSizeChanged;
        PreviewKeyDown -= OnPreviewKeyDown;
        HookViewModel(null);
        StopSpaceshipTimer();

        Lyracist.Services.Tablet.LyricsHub.ReactionReceived -= OnReactionReceived;

        var scaryokeWindow = App.AppHost.Services.GetService(typeof(ScaryokeWindow)) as ScaryokeWindow;
        if (scaryokeWindow != null)
        {
            scaryokeWindow.SpinStarted -= ScaryokeWindow_SpinStarted;
            scaryokeWindow.SpinCompleted -= ScaryokeWindow_SpinCompleted;
            scaryokeWindow.IsVisibleChanged -= ScaryokeWindow_IsVisibleChanged;
        }
    }

    // This window can end up borderless, topmost, and/or parked off-screen (casting targets
    // intentionally hide it off-screen so it can still be captured for streaming). If casting
    // never actually connects, or a "Monitor" target points at a display that's since been
    // unplugged, there is otherwise no title bar and no way to reach it - Escape unconditionally
    // pulls it back into a normal, visible, closeable window so the user is never stuck.
    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape) return;

        Topmost = false;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowState = WindowState.Normal;
        Left = 100;
        Top = 100;
        Width = 1280;
        Height = 720;
        Activate();
        e.Handled = true;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMarqueeFontSizes();
    }

    private void UpdateMarqueeFontSizes()
    {
        if (MarqueeHeader == null || MarqueeSingerName == null || MarqueeSingerSong == null)
            return;

        double ratio = ActualHeight / 1080.0;
        if (ratio <= 0) ratio = 1.0;

        MarqueeHeader.FontSize = Math.Max(24, Math.Round(48 * ratio));
        MarqueeSingerName.FontSize = Math.Max(40, Math.Round(108 * ratio));
        MarqueeSingerSong.FontSize = Math.Max(20, Math.Round(42 * ratio));
    }

    private void RotationWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            ApplyProjectionViewMode();
        }
        else
        {
            StopMarqueeChase();
            StopVinylSpin();
            StopSpaceshipTimer();
            StopDiscoBall();
            StopDiscoBeams();
            StopDiscoLightSpots();
            StopSynthGrid();
            StopFestivalBeams();
            StopFestivalSparkles();
            StopSlotReels();
            StopSlotParticles();
            StopJukebox();
            StopJukeboxBubbles();
            StopJumbotron();
            StopJumbotronSpotlights();
            StopTheater();
            StopTheaterProjectorBeam();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HookViewModel(DataContext as RotationWindowViewModel);
        RebuildBanner();
        ApplyProjectionViewMode();

        var scaryokeWindow = App.AppHost.Services.GetService(typeof(ScaryokeWindow)) as ScaryokeWindow;
        if (scaryokeWindow != null)
        {
            scaryokeWindow.SpinStarted += ScaryokeWindow_SpinStarted;
            scaryokeWindow.SpinCompleted += ScaryokeWindow_SpinCompleted;
            scaryokeWindow.IsVisibleChanged += ScaryokeWindow_IsVisibleChanged;
            UpdateScaryokeOverlayVisibility(scaryokeWindow);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        HookViewModel(DataContext as RotationWindowViewModel);
        RebuildBanner();
        ApplyProjectionViewMode();
    }

    private void HookViewModel(RotationWindowViewModel? vm)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= Vm_PropertyChanged;
            _vm.FullRotation.CollectionChanged -= Rotation_CollectionChanged;
        }
        _vm = vm;
        if (_vm != null)
        {
            _vm.PropertyChanged += Vm_PropertyChanged;
            _vm.FullRotation.CollectionChanged += Rotation_CollectionChanged;
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RotationWindowViewModel.SelectedProjectionView):
                ApplyProjectionViewMode();
                break;

            case nameof(RotationWindowViewModel.CurrentSinger):
                if (_vm?.SelectedProjectionView == "Casino Slot Reels")
                {
                    StartSlotReels();
                }
                break;

            case nameof(RotationWindowViewModel.CrawlBannerText):
            case nameof(RotationWindowViewModel.HasDesignatedCurrentSinger):
            case nameof(RotationWindowViewModel.ShowEstimatedWaitTime):
                RestartCrawlIfActive();
                break;

            case nameof(RotationWindowViewModel.IsAnnouncementVisible):
            case nameof(RotationWindowViewModel.AnnouncementBanner):
                RebuildBanner();
                break;

            case nameof(RotationWindowViewModel.JumbotronBannerPath):
                UpdateJumbotronBanner();
                UpdateSlotBanner();
                break;
        }
    }

    // UpdateRotation rebuilds FullRotation with Clear() + one Add() per singer, so a single rotation
    // update fires N+1 CollectionChanged events. Reacting to each one rebuilt the ticker, the crawl
    // and the film strip N+1 times; instead the first event schedules one refresh that runs after
    // the whole update has finished.
    private bool _rotationRefreshPending;

    private void Rotation_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_rotationRefreshPending) return;
        _rotationRefreshPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() =>
        {
            _rotationRefreshPending = false;
            RebuildBanner();
            RestartCrawlIfActive();

            if (_vm?.SelectedProjectionView == "Movie Theater 'Now Showing'" && TheaterPanel.Visibility == Visibility.Visible)
            {
                BuildTheaterFilmStrip();
            }
        }));
    }

    // Content key of the crawl currently scrolling. Rotation updates that don't change what the
    // crawl would show (e.g. a singer's rating ticking up) leave it running instead of restarting
    // it from the bottom. Null whenever the crawl isn't running, so the next start always builds.
    private string? _crawlSignature;

    private string BuildCrawlSignature()
    {
        if (_vm == null) return string.Empty;
        var sb = new System.Text.StringBuilder();
        sb.Append(ReplaceVariables(_vm.CrawlBannerText)).Append('\u0001')
          .Append(_vm.HasDesignatedCurrentSinger).Append('\u0001')
          .Append(_vm.ShowEstimatedWaitTime);
        foreach (var s in _vm.FullRotation)
        {
            sb.Append('\u0001').Append(s.Name).Append('\u0002').Append(s.SongTitle).Append('\u0002')
              .Append(s.Artist).Append('\u0002').Append(s.IsRotationStart).Append('\u0002').Append(s.EstimatedWaitMinutes);
        }
        return sb.ToString();
    }

    private void RestartCrawlIfActive()
    {
        if (_vm?.SelectedProjectionView != "Star Wars Crawl" || CrawlPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        if (_crawlSignature != null && _crawlSignature == BuildCrawlSignature())
        {
            return;
        }

        _crawlGen++;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)StartCrawl);
    }

    private void ApplyProjectionViewMode()
    {
        if (_vm == null) return;

        // Stop animations and clear crawl canvas
        _crawlGen++;
        _crawlSignature = null;
        CrawlStarCanvas.Children.Clear();
        _crawlSource.Children.Clear();
        CrawlMaterial.Brush = null;
        CrawlBackMaterial.Brush = null;
        _crawlBrush = null;

        StopMarqueeChase();
        StopVinylSpin();
        StopSpaceshipTimer();
        StopDiscoBall();
        StopDiscoBeams();
        StopDiscoLightSpots();
        StopSynthGrid();
        StopFestivalBeams();
        StopFestivalSparkles();
        StopSlotReels();
        StopSlotParticles();
        StopJukebox();
        StopJukeboxBubbles();
        StopJumbotron();
        StopJumbotronSpotlights();
        StopTheater();
        StopTheaterProjectorBeam();

        NormalPanel.Visibility = Visibility.Collapsed;
        CrawlPanel.Visibility = Visibility.Collapsed;
        MarqueePanel.Visibility = Visibility.Collapsed;
        VinylPanel.Visibility = Visibility.Collapsed;
        DiscoPanel.Visibility = Visibility.Collapsed;
        SynthwavePanel.Visibility = Visibility.Collapsed;
        FestivalPanel.Visibility = Visibility.Collapsed;
        SlotReelsPanel.Visibility = Visibility.Collapsed;
        JukeboxPanel.Visibility = Visibility.Collapsed;
        JumbotronPanel.Visibility = Visibility.Collapsed;
        TheaterPanel.Visibility = Visibility.Collapsed;

        switch (_vm.SelectedProjectionView)
        {
            case "Star Wars Crawl":
                CrawlPanel.Visibility = Visibility.Visible;
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)StartCrawl);
                StartSpaceshipTimer();
                break;

            case "Vegas Marquee":
                MarqueePanel.Visibility = Visibility.Visible;
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildMarqueeBulbs);
                break;

            case "Vinyl Turntable":
                VinylPanel.Visibility = Visibility.Visible;
                StartVinylSpin();
                break;

            case "Disco Ball":
                DiscoPanel.Visibility = Visibility.Visible;
                StartDiscoBall();
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildDiscoBeams);
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildDiscoLightSpots);
                break;

            case "Synthwave Grid":
                SynthwavePanel.Visibility = Visibility.Visible;
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildSynthGridStatic);
                StartSynthGrid();
                break;

            case "Concert Festival Lineup":
                FestivalPanel.Visibility = Visibility.Visible;
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildFestivalBeams);
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildFestivalSparkles);
                break;

            case "Casino Slot Reels":
                SlotReelsPanel.Visibility = Visibility.Visible;
                StartSlotReels();
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildSlotParticles);
                UpdateSlotBanner();
                break;

            case "Jukebox":
                JukeboxPanel.Visibility = Visibility.Visible;
                StartJukebox();
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildJukeboxBubbles);
                break;

            case "Stadium Jumbotron":
                JumbotronPanel.Visibility = Visibility.Visible;
                StartJumbotron();
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildJumbotronSpotlights);
                UpdateJumbotronBanner();
                break;

            case "Movie Theater 'Now Showing'":
                TheaterPanel.Visibility = Visibility.Visible;
                StartTheater();
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildTheaterProjectorBeam);
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildTheaterFilmStrip);
                break;

            default:
                NormalPanel.Visibility = Visibility.Visible;
                break;
        }
    }

    private void CrawlStarCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Star Wars Crawl")
        {
            _lastStarW = e.NewSize.Width;
            _lastStarH = e.NewSize.Height;

            if (_starRegenTimer == null)
            {
                _starRegenTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(200)
                };
                _starRegenTimer.Tick += (s, ev) =>
                {
                    _starRegenTimer.Stop();
                    RegenerateStars(_lastStarW, _lastStarH);
                };
            }
            else
            {
                _starRegenTimer.Stop();
            }
            _starRegenTimer.Start();
        }
    }

    private void RegenerateStars(double starW, double starH)
    {
        CrawlStarCanvas.Children.Clear();
        if (starW <= 0 || starH <= 0) return;

        GenerateGalaxies(starW, starH);

        int starCount = (int)Math.Clamp(starW * starH / 7000.0, 100, 300);

        for (int i = 0; i < starCount; i++)
        {
            double sz;
            double op;
            double roll = _rng.NextDouble();

            if (roll < 0.70)
            {
                sz = (_rng.NextDouble() * 1.1) + 0.7;
                op = (_rng.NextDouble() * 0.3) + 0.25;
            }
            else if (roll < 0.93)
            {
                sz = (_rng.NextDouble() * 1.6) + 1.8;
                op = (_rng.NextDouble() * 0.3) + 0.6;
            }
            else
            {
                sz = (_rng.NextDouble() * 3.7) + 3.5;
                op = (_rng.NextDouble() * 0.2) + 0.8;
            }

            bool isBright = roll >= 0.93;
            SolidColorBrush fill = MakeStarBrush();

            var star = new Ellipse
            {
                Width = sz,
                Height = sz,
                Fill = fill,
                Opacity = op
            };

            double x = _rng.NextDouble() * starW;
            double y = _rng.NextDouble() * starH;

            Canvas.SetLeft(star, x);
            Canvas.SetTop(star, y);
            CrawlStarCanvas.Children.Add(star);

            double twinkleChance = isBright ? 0.6 : 0.25;
            bool animateTwinkle = _rng.NextDouble() < twinkleChance;

            if (isBright)
            {
                // Hardware-accelerated outer glow using a separate slightly larger Ellipse with RadialGradientBrush
                var glow = new Ellipse
                {
                    Width = sz * 3.5,
                    Height = sz * 3.5,
                    Fill = new RadialGradientBrush
                    {
                        GradientStops =
                        {
                            new GradientStop(System.Windows.Media.Color.FromArgb(180, fill.Color.R, fill.Color.G, fill.Color.B), 0.0),
                            new GradientStop(System.Windows.Media.Color.FromArgb(0, fill.Color.R, fill.Color.G, fill.Color.B), 1.0)
                        }
                    }
                };
                Canvas.SetLeft(glow, x - (sz * 1.25));
                Canvas.SetTop(glow, y - (sz * 1.25));
                CrawlStarCanvas.Children.Add(glow);

                if (animateTwinkle)
                {
                    glow.BeginAnimation(OpacityProperty, new DoubleAnimation(0.8, 0.15,
                        TimeSpan.FromSeconds((_rng.NextDouble() * 2.5) + 0.8))
                    {
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        BeginTime = TimeSpan.FromSeconds(_rng.NextDouble() * 5)
                    });
                }
            }

            if (animateTwinkle)
            {
                star.BeginAnimation(OpacityProperty, new DoubleAnimation(op, op * 0.2,
                    TimeSpan.FromSeconds((_rng.NextDouble() * 2.5) + 0.8))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(_rng.NextDouble() * 5)
                });
            }
        }
    }

    private static SolidColorBrush MakeStarBrush()
    {
        double tint = _rng.NextDouble();
        byte r, g, b;
        if (tint < 0.25)
        {
            r = 0xCF; g = 0xDD; b = 0xFF;
        }
        else if (tint < 0.45)
        {
            r = 0xFF; g = 0xF2; b = 0xD8;
        }
        else
        {
            r = g = b = 0xFF;
        }

        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void GenerateGalaxies(double w, double h)
    {
        int count = _rng.Next(3, 6);

        for (int i = 0; i < count; i++)
        {
            double gw = (_rng.NextDouble() * 180) + 130;
            double gh = gw * ((_rng.NextDouble() * 0.30) + 0.32);

            (System.Windows.Media.Color core, System.Windows.Media.Color mid) = PickGalaxyPalette();

            var halo = new Ellipse
            {
                Width = gw,
                Height = gh,
                Fill = new RadialGradientBrush
                {
                    GradientStops =
                    {
                        new GradientStop(core, 0.0),
                        new GradientStop(mid, 0.4),
                        new GradientStop(System.Windows.Media.Color.FromArgb(0, mid.R, mid.G, mid.B), 1.0)
                    }
                }
            };

            double coreSize = gh * 0.45;
            var coreGlow = new Ellipse
            {
                Width = coreSize,
                Height = coreSize,
                Fill = new RadialGradientBrush
                {
                    GradientStops =
                    {
                        new GradientStop(System.Windows.Media.Color.FromArgb(220, 255, 255, 255), 0.0),
                        new GradientStop(System.Windows.Media.Color.FromArgb(0, 255, 255, 255), 1.0)
                    }
                }
            };
            Canvas.SetLeft(coreGlow, (gw - coreSize) / 2);
            Canvas.SetTop(coreGlow, (gh - coreSize) / 2);

            var galaxy = new Canvas
            {
                Width = gw,
                Height = gh,
                Opacity = (_rng.NextDouble() * 0.25) + 0.4,
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = new RotateTransform(_rng.NextDouble() * 360)
            };
            galaxy.Children.Add(halo);
            galaxy.Children.Add(coreGlow);

            Canvas.SetLeft(galaxy, _rng.NextDouble() * Math.Max(1, w - gw));
            Canvas.SetTop(galaxy, _rng.NextDouble() * Math.Max(1, h - gh));
            CrawlStarCanvas.Children.Add(galaxy);
        }
    }

    private static (System.Windows.Media.Color core, System.Windows.Media.Color mid) PickGalaxyPalette()
    {
        static System.Windows.Media.Color Argb(byte a, byte r, byte g, byte b) => System.Windows.Media.Color.FromArgb(a, r, g, b);

        return _rng.Next(4) switch
        {
            0 => (Argb(180, 210, 220, 255), Argb(75, 90, 120, 210)),
            1 => (Argb(180, 255, 220, 245), Argb(75, 170, 90, 180)),
            2 => (Argb(180, 215, 255, 245), Argb(75, 70, 170, 150)),
            _ => (Argb(180, 255, 240, 215), Argb(75, 200, 150, 90)),
        };
    }

    private void StartCrawl()
    {
        if (_vm == null || _vm.SelectedProjectionView != "Star Wars Crawl" || _vm.FullRotation.Count == 0) return;

        int gen = ++_crawlGen;
        _crawlSignature = BuildCrawlSignature();

        if (CrawlStarCanvas.Children.Count == 0)
        {
            double starW = CrawlStarCanvas.ActualWidth > 0 ? CrawlStarCanvas.ActualWidth : ActualWidth;
            double starH = CrawlStarCanvas.ActualHeight > 0 ? CrawlStarCanvas.ActualHeight : ActualHeight;
            RegenerateStars(starW, starH);
        }

        StackPanel textPanel = BuildCrawlTextPanel([.. _vm.FullRotation], _vm.CrawlBannerText, _vm.HasDesignatedCurrentSinger, _vm.ShowEstimatedWaitTime);
        textPanel.Measure(new System.Windows.Size(PanelWidth, double.PositiveInfinity));
        textPanel.Arrange(new Rect(0, 0, PanelWidth, textPanel.DesiredSize.Height));
        double panelH = textPanel.DesiredSize.Height;

        var tt = new TranslateTransform(0, ViewH);
        textPanel.RenderTransform = tt;

        _crawlSource.Children.Clear();
        _crawlSource.Children.Add(textPanel);
        Canvas.SetLeft(textPanel, 0);
        Canvas.SetTop(textPanel, 0);

        _crawlSource.Measure(new System.Windows.Size(PanelWidth, ViewH));
        _crawlSource.Arrange(new Rect(0, 0, PanelWidth, ViewH));

        _crawlBrush = new VisualBrush(_crawlSource)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, PanelWidth, ViewH),
            Stretch = Stretch.Fill
        };
        CrawlMaterial.Brush = _crawlBrush;
        CrawlBackMaterial.Brush = _crawlBrush;

        double duration = Math.Max(20.0, (ViewH + panelH) / ScrollPixelsPerSecond);
        var anim = new DoubleAnimation(ViewH, -panelH, TimeSpan.FromSeconds(duration));
        anim.Completed += (_, _) =>
        {
            if (_crawlGen == gen && _vm?.SelectedProjectionView == "Star Wars Crawl")
                StartCrawl();
        };
        tt.BeginAnimation(TranslateTransform.YProperty, anim);
    }

    private static StackPanel BuildCrawlTextPanel(List<Singer> rotation, string bannerText, bool hasDesignatedCurrentSinger, bool showEstimatedWaitTime)
    {
        var gold = CrawlGold;
        var dimGold = CrawlDimGold;
        var white = System.Windows.Media.Brushes.White;

        var panel = new StackPanel
        {
            Width = PanelWidth,
            Background = System.Windows.Media.Brushes.Transparent
        };

        panel.Children.Add(new Border { Height = 120 });

        if (!string.IsNullOrWhiteSpace(bannerText))
        {
            string parsedBannerText = ReplaceVariables(bannerText);
            panel.Children.Add(new TextBlock
            {
                Text = parsedBannerText.ToUpperInvariant(),
                FontSize = 44,
                FontWeight = FontWeights.Bold,
                Foreground = gold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20, 0, 20, 12)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "─────────────────────",
                FontSize = 20,
                Foreground = dimGold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 60)
            });
        }

        if (showEstimatedWaitTime)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "{N} = Estimated wait time",
                FontSize = 18,
                FontStyle = FontStyles.Italic,
                Foreground = dimGold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 40)
            });
        }

        string[] ordinals = ["UP NEXT", "2nd", "3rd", "4th", "5th",
                              "6th", "7th", "8th", "9th", "10th"];

        for (int i = 0; i < rotation.Count; i++)
        {
            Singer entry = rotation[i];
            bool isCurrent = i == 0;

            string posLabel;
            if (isCurrent)
            {
                posLabel = hasDesignatedCurrentSinger ? "NOW SINGING" : "FIRST PERFORMER";
            }
            else
            {
                posLabel = i <= ordinals.Length ? ordinals[i - 1] : $"#{i + 1}";
            }

            panel.Children.Add(new TextBlock
            {
                Text = posLabel,
                FontSize = isCurrent ? 20 : 15,
                FontStyle = FontStyles.Italic,
                Foreground = isCurrent ? white : dimGold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });

            string nameText = entry.IsRotationStart ? $"⚓ {entry.Name}" : entry.Name;
            if (entry.EstimatedWaitMinutes > 0)
            {
                nameText += $" {{{entry.EstimatedWaitMinutes}}}";
            }

            panel.Children.Add(new TextBlock
            {
                Text = nameText,
                FontSize = isCurrent ? 52 : 36,
                FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isCurrent ? white : gold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20, 0, 20, 4)
            });

            string songLine = entry.SongTitle ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(entry.Artist))
                songLine += string.IsNullOrWhiteSpace(songLine)
                    ? entry.Artist
                    : $"  –  {entry.Artist}";

            if (!string.IsNullOrWhiteSpace(songLine))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = songLine,
                    FontSize = isCurrent ? 24 : 18,
                    Foreground = dimGold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20, 0, 20, isCurrent ? 70 : 48)
                });
            }
            else
            {
                panel.Children.Add(new Border { Height = isCurrent ? 70 : 48 });
            }
        }

        panel.Children.Add(new Border { Height = 300 });
        return panel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Registered as a singleton: a closed WPF window can never be shown
        // again, so hide instead and let app shutdown tear it down.
        e.Cancel = true;
        var displayService = App.AppHost.Services.GetService(typeof(IDisplayService)) as IDisplayService;
        if (displayService != null)
        {
            displayService.IsRotationActive = false;
        }
        else
        {
            Hide();
        }
        base.OnClosing(e);
    }

    private void OnToggleFullscreen(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnHide(object sender, RoutedEventArgs e)
    {
        var displayService = App.AppHost.Services.GetService(typeof(IDisplayService)) as IDisplayService;
        if (displayService != null)
        {
            displayService.IsRotationActive = false;
        }
        else
        {
            Hide();
        }
    }

    private void BannerBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        StartAnimation();
    }

    // Text of the ticker currently scrolling. Rebuilding the Inlines restarts the scroll from the
    // right edge, so an update that leaves the ticker text unchanged must not touch it - otherwise
    // every rotation refresh made the ticker visibly jump back to the start.
    private string? _bannerSignature;

    private void RebuildBanner()
    {
        if (_vm == null) return;

        string separator = "    •    ";
        var runs = new List<Run>();

        // Prepend announcement banner if visible
        if (_vm.IsAnnouncementVisible && !string.IsNullOrWhiteSpace(_vm.AnnouncementBanner))
        {
            string parsedBanner = ReplaceVariables(_vm.AnnouncementBanner);
            runs.Add(new Run(parsedBanner) { Foreground = System.Windows.Media.Brushes.Orange, FontWeight = FontWeights.Bold });
            runs.Add(new Run(separator) { Foreground = System.Windows.Media.Brushes.White });
        }

        var activeSingers = _vm.Rotation.Where(s => !s.IsPaused).ToList();
        if (activeSingers.Count == 0)
        {
            string emptyText = ReplaceVariables("Lyracist - No Singers in Queue");
            runs.Add(new Run(emptyText) { Foreground = System.Windows.Media.Brushes.White });
        }
        else
        {
            // Find current singer (either IsCurrent or the first active one)
            var current = activeSingers.FirstOrDefault(s => s.IsCurrent) ?? activeSingers[0];

            // Highlight the current performer
            string currentSingerText = $"Current Performer: {current.Name}";
            if (!string.IsNullOrWhiteSpace(current.SongTitle))
            {
                currentSingerText += $" (\"{current.SongTitle}\")";
            }
            runs.Add(new Run(currentSingerText)
            {
                Foreground = System.Windows.Media.Brushes.Yellow,
                FontWeight = FontWeights.Bold
            });

            // Get the next 5 performers in rotation (wrap around if needed, or just take the subsequent ones)
            int currentIndex = activeSingers.IndexOf(current);
            int count = activeSingers.Count;

            string[] ordinals = ["Next", "2nd", "3rd", "4th", "5th"];

            int addedCount = 0;
            for (int offset = 1; offset < count && addedCount < 5; offset++)
            {
                var singer = activeSingers[(currentIndex + offset) % count];
                string label = offset <= ordinals.Length ? ordinals[offset - 1] : $"#{offset + 1}";
                string singerText = $"{label}: {singer.Name}";
                if (singer.EstimatedWaitMinutes > 0)
                {
                    singerText += $" {{{singer.EstimatedWaitMinutes}}}";
                }
                if (!string.IsNullOrWhiteSpace(singer.SongTitle))
                {
                    singerText += $" (\"{singer.SongTitle}\")";
                }

                runs.Add(new Run(separator) { Foreground = System.Windows.Media.Brushes.White });
                runs.Add(new Run(singerText) { Foreground = System.Windows.Media.Brushes.Cyan });
                addedCount++;
            }
        }

        string signature = string.Join("\u0001", runs.Select(r => r.Text));
        if (signature == _bannerSignature && BannerTextBlock.Inlines.Count > 0)
        {
            return;
        }
        _bannerSignature = signature;

        BannerTextBlock.Inlines.Clear();
        BannerTextBlock.Inlines.AddRange(runs);
        StartAnimation();
    }

    private void StartAnimation()
    {
        double bannerWidth = BannerBorder.ActualWidth;
        if (bannerWidth <= 0) return;

        BannerTextBlock.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        double textWidth = BannerTextBlock.DesiredSize.Width;
        if (textWidth <= 0) return;

        double speed = 60.0;
        DoubleAnimation anim = new()
        {
            From = bannerWidth,
            To = -textWidth,
            Duration = TimeSpan.FromSeconds(Math.Max(4.0, (bannerWidth + textWidth) / speed)),
            RepeatBehavior = RepeatBehavior.Forever
        };

        BannerTextBlock.BeginAnimation(Canvas.LeftProperty, null);
        BannerTextBlock.BeginAnimation(Canvas.LeftProperty, anim);
    }

    private void StartVinylSpin()
    {
        var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(3.5))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        VinylRotate.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    private void StopVinylSpin()
    {
        VinylRotate.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    private void StartDiscoBall()
    {
        var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(9))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        DiscoBallRotate.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    private void StopDiscoBall()
    {
        DiscoBallRotate.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    private void DiscoBeamCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Disco Ball" && DiscoPanel.Visibility == Visibility.Visible)
        {
            BuildDiscoBeams();
        }
    }

    // Sweeping colored spotlight beams fanned out from the ball, each an independently
    // rotating wedge - alternating spin direction/duration per beam is what makes the sweep
    // read as chaotic disco lighting rather than a single synchronized rotation.
    private void BuildDiscoBeams()
    {
        StopDiscoBeams();
        DiscoBeamCanvas.Children.Clear();

        double w = DiscoBeamCanvas.ActualWidth;
        double h = DiscoBeamCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double anchorX = w / 2.0;
        double anchorY = h * 0.18;
        double length = h * 1.15;
        int beamCount = DiscoBeamPalette.Length;

        for (int i = 0; i < beamCount; i++)
        {
            double spreadHalf = 34 + (i % 2 == 0 ? 6 : 0);
            var color = DiscoBeamPalette[i];

            // Points are relative to the apex (0,0) so RenderTransformOrigin="0.5,0" (the
            // horizontal midpoint of the triangle's bounding box, at its top edge) lands exactly
            // on the apex - that's what lets the RotateTransform below sweep the beam around the
            // ball instead of around the triangle's centroid.
            var polygon = new Polygon
            {
                Points = [new System.Windows.Point(0, 0), new System.Windows.Point(-spreadHalf, length), new System.Windows.Point(spreadHalf, length)],
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.0)
            };

            var gradient = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0.5, 0),
                EndPoint = new System.Windows.Point(0.5, 1),
                GradientStops =
                {
                    new GradientStop(System.Windows.Media.Color.FromArgb(0x55, color.R, color.G, color.B), 0.0),
                    new GradientStop(System.Windows.Media.Color.FromArgb(0x00, color.R, color.G, color.B), 1.0)
                }
            };
            gradient.Freeze();
            polygon.Fill = gradient;

            var rotate = new RotateTransform((360.0 / beamCount) * i);
            polygon.RenderTransform = rotate;

            Canvas.SetLeft(polygon, anchorX - spreadHalf);
            Canvas.SetTop(polygon, anchorY);
            DiscoBeamCanvas.Children.Add(polygon);
            _discoBeamRotates.Add(rotate);

            bool clockwise = i % 2 == 0;
            double duration = 9 + (i * 2.3);
            var spin = new DoubleAnimation(
                rotate.Angle,
                rotate.Angle + (clockwise ? 360 : -360),
                TimeSpan.FromSeconds(duration))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            rotate.BeginAnimation(RotateTransform.AngleProperty, spin);
        }
    }

    private void StopDiscoBeams()
    {
        foreach (var rotate in _discoBeamRotates)
        {
            rotate.BeginAnimation(RotateTransform.AngleProperty, null);
        }
        DiscoBeamCanvas.Children.Clear();
        _discoBeamRotates.Clear();
    }

    private void DiscoLightCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Disco Ball" && DiscoPanel.Visibility == Visibility.Visible)
        {
            BuildDiscoLightSpots();
        }
    }

    // Small colored dots drifting and twinkling across the dance floor, standing in for the
    // ball's reflected light spots - reuses the same wobble-and-twinkle technique as the Star
    // Wars crawl's star field (independent looping animations with randomized phase/duration).
    private void BuildDiscoLightSpots()
    {
        StopDiscoLightSpots();
        DiscoLightCanvas.Children.Clear();

        double w = DiscoLightCanvas.ActualWidth;
        double h = DiscoLightCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        int spotCount = (int)Math.Clamp(w * h / 26000.0, 14, 36);

        for (int i = 0; i < spotCount; i++)
        {
            double size = (_rng.NextDouble() * 5) + 3;
            var color = DiscoLightPalette[_rng.Next(DiscoLightPalette.Length)];
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = brush,
                RenderTransform = new TranslateTransform()
            };

            double x = _rng.NextDouble() * w;
            double y = _rng.NextDouble() * h;
            Canvas.SetLeft(dot, x);
            Canvas.SetTop(dot, y);
            DiscoLightCanvas.Children.Add(dot);
            _discoLightSpots.Add(dot);

            var transform = (TranslateTransform)dot.RenderTransform;
            double ampX = (_rng.NextDouble() * 60) + 20;
            double ampY = (_rng.NextDouble() * 60) + 20;
            double durX = (_rng.NextDouble() * 3) + 3;
            double durY = (_rng.NextDouble() * 3) + 3;
            double begin = _rng.NextDouble() * 4;

            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-ampX, ampX, TimeSpan.FromSeconds(durX))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(begin)
            });
            transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-ampY, ampY, TimeSpan.FromSeconds(durY))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(begin)
            });
            dot.BeginAnimation(OpacityProperty, new DoubleAnimation(0.15, 0.95, TimeSpan.FromSeconds((_rng.NextDouble() * 1.5) + 1.2))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(begin)
            });
        }
    }

    private void StopDiscoLightSpots()
    {
        foreach (var dot in _discoLightSpots)
        {
            dot.BeginAnimation(OpacityProperty, null);
            if (dot.RenderTransform is TranslateTransform t)
            {
                t.BeginAnimation(TranslateTransform.XProperty, null);
                t.BeginAnimation(TranslateTransform.YProperty, null);
            }
        }
        DiscoLightCanvas.Children.Clear();
        _discoLightSpots.Clear();
    }

    private void SynthGridCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Synthwave Grid" && SynthwavePanel.Visibility == Visibility.Visible)
        {
            BuildSynthGridStatic();
        }
    }

    /// <summary>Static fan of converging vertical lines from the horizon's vanishing point out to the
    /// bottom edge - the fixed "rails" of the perspective floor. Rebuilt on resize; the moving
    /// horizontal lines are handled separately by <see cref="StartSynthGrid"/>.</summary>
    private void BuildSynthGridStatic()
    {
        SynthGridCanvas.Children.Clear();

        double w = SynthGridCanvas.ActualWidth;
        double h = SynthGridCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double vanishX = w / 2.0;
        double vanishY = h * 0.42;
        const int rayCount = 15;

        for (int i = 0; i <= rayCount; i++)
        {
            double t = (double)i / rayCount;
            double bottomX = (-0.15 * w) + (t * (1.3 * w));
            var line = new Line
            {
                X1 = vanishX,
                Y1 = vanishY,
                X2 = bottomX,
                Y2 = h,
                StrokeThickness = 1.5,
                Stroke = i % 2 == 0
                    ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x55, 0x33, 0xD4, 0xFF))
                    : new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x55, 0xFF, 0x2F, 0xE0))
            };
            SynthGridCanvas.Children.Add(line);
        }
    }

    /// <summary>Spawns a horizontal grid line at the horizon every tick and animates it racing toward
    /// the viewer (growing wider, accelerating via an ease-in curve, fading out near the bottom) -
    /// reuses the same spawn/animate/self-remove shape as <see cref="SpawnSpaceship"/>.</summary>
    private void StartSynthGrid()
    {
        StopSynthGrid();
        _synthGridTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _synthGridTimer.Tick += (_, _) => SpawnSynthGridLine();
        _synthGridTimer.Start();
    }

    private void StopSynthGrid()
    {
        _synthGridTimer?.Stop();
        _synthGridTimer = null;
        SynthGridCanvas.Children.Clear();
    }

    private void SpawnSynthGridLine()
    {
        if (_vm?.SelectedProjectionView != "Synthwave Grid" || SynthwavePanel.Visibility != Visibility.Visible) return;

        double w = SynthGridCanvas.ActualWidth;
        double h = SynthGridCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double vanishX = w / 2.0;
        double horizonY = h * 0.42;
        double maxWidth = w * 1.1;

        var color = _synthLineColors[_synthLineColorIndex++ % _synthLineColors.Count];
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        var rect = new System.Windows.Shapes.Rectangle
        {
            Height = 2,
            Fill = brush,
            Effect = new DropShadowEffect { Color = color, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.8 }
        };
        Canvas.SetTop(rect, horizonY);
        Canvas.SetLeft(rect, vanishX);
        rect.Width = 0;
        SynthGridCanvas.Children.Add(rect);

        double duration = 2.2;
        var ease = new PowerEase { EasingMode = EasingMode.EaseIn, Power = 2.5 };
        var storyboard = new Storyboard();

        var topAnim = new DoubleAnimation(horizonY, h, TimeSpan.FromSeconds(duration)) { EasingFunction = ease };
        Storyboard.SetTarget(topAnim, rect);
        Storyboard.SetTargetProperty(topAnim, new PropertyPath(Canvas.TopProperty));
        storyboard.Children.Add(topAnim);

        var widthAnim = new DoubleAnimation(0, maxWidth, TimeSpan.FromSeconds(duration)) { EasingFunction = ease };
        Storyboard.SetTarget(widthAnim, rect);
        Storyboard.SetTargetProperty(widthAnim, new PropertyPath(System.Windows.Shapes.Rectangle.WidthProperty));
        storyboard.Children.Add(widthAnim);

        var leftAnim = new DoubleAnimation(vanishX, vanishX - (maxWidth / 2.0), TimeSpan.FromSeconds(duration)) { EasingFunction = ease };
        Storyboard.SetTarget(leftAnim, rect);
        Storyboard.SetTargetProperty(leftAnim, new PropertyPath(Canvas.LeftProperty));
        storyboard.Children.Add(leftAnim);

        var opacityAnim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(duration) };
        opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
        opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(0.12)));
        opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(0.8)));
        opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));
        Storyboard.SetTarget(opacityAnim, rect);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(opacityAnim);

        storyboard.Completed += (s, e) => SynthGridCanvas.Children.Remove(rect);
        storyboard.Begin();
    }

    private void FestivalBeamCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Concert Festival Lineup" && FestivalPanel.Visibility == Visibility.Visible)
        {
            BuildFestivalBeams();
        }
    }

    /// <summary>Warm stage-light wedges fanned upward from below the poster, same rotating-wedge
    /// mechanism as <see cref="BuildDiscoBeams"/> but anchored at the bottom edge instead of the top
    /// (each wedge's apex/RenderTransformOrigin is flipped to the bottom of its own bounding box).</summary>
    private void BuildFestivalBeams()
    {
        StopFestivalBeams();
        FestivalBeamCanvas.Children.Clear();

        double w = FestivalBeamCanvas.ActualWidth;
        double h = FestivalBeamCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double anchorX = w / 2.0;
        double anchorY = h;
        double length = h * 1.1;
        int beamCount = FestivalBeamPalette.Length;

        for (int i = 0; i < beamCount; i++)
        {
            double spreadHalf = 30 + (i % 2 == 0 ? 8 : 0);
            var color = FestivalBeamPalette[i];

            var polygon = new Polygon
            {
                Points = [new System.Windows.Point(0, 0), new System.Windows.Point(-spreadHalf, -length), new System.Windows.Point(spreadHalf, -length)],
                RenderTransformOrigin = new System.Windows.Point(0.5, 1.0)
            };

            var gradient = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0.5, 1),
                EndPoint = new System.Windows.Point(0.5, 0),
                GradientStops =
                {
                    new GradientStop(System.Windows.Media.Color.FromArgb(0x50, color.R, color.G, color.B), 0.0),
                    new GradientStop(System.Windows.Media.Color.FromArgb(0x00, color.R, color.G, color.B), 1.0)
                }
            };
            gradient.Freeze();
            polygon.Fill = gradient;

            var rotate = new RotateTransform((360.0 / beamCount) * i);
            polygon.RenderTransform = rotate;

            Canvas.SetLeft(polygon, anchorX - spreadHalf);
            Canvas.SetTop(polygon, anchorY - length);
            FestivalBeamCanvas.Children.Add(polygon);
            _festivalBeamRotates.Add(rotate);

            bool clockwise = i % 2 == 0;
            double duration = 10 + (i * 2.1);
            var spin = new DoubleAnimation(
                rotate.Angle,
                rotate.Angle + (clockwise ? 360 : -360),
                TimeSpan.FromSeconds(duration))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            rotate.BeginAnimation(RotateTransform.AngleProperty, spin);
        }
    }

    private void StopFestivalBeams()
    {
        foreach (var rotate in _festivalBeamRotates)
        {
            rotate.BeginAnimation(RotateTransform.AngleProperty, null);
        }
        FestivalBeamCanvas.Children.Clear();
        _festivalBeamRotates.Clear();
    }

    private void FestivalSparkleCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Concert Festival Lineup" && FestivalPanel.Visibility == Visibility.Visible)
        {
            BuildFestivalSparkles();
        }
    }

    /// <summary>Sparse twinkling gold/white dots standing in for a starlit night sky above the stage -
    /// same drift-and-twinkle technique as <see cref="BuildDiscoLightSpots"/>, just recolored and less
    /// dense so it reads as a night sky rather than a dance floor.</summary>
    private void BuildFestivalSparkles()
    {
        StopFestivalSparkles();
        FestivalSparkleCanvas.Children.Clear();

        double w = FestivalSparkleCanvas.ActualWidth;
        double h = FestivalSparkleCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        int spotCount = (int)Math.Clamp(w * h / 40000.0, 12, 28);

        for (int i = 0; i < spotCount; i++)
        {
            double size = (_rng.NextDouble() * 4) + 2;
            var color = FestivalSparklePalette[_rng.Next(FestivalSparklePalette.Length)];
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = brush,
                RenderTransform = new TranslateTransform()
            };

            double x = _rng.NextDouble() * w;
            double y = _rng.NextDouble() * h * 0.6;
            Canvas.SetLeft(dot, x);
            Canvas.SetTop(dot, y);
            FestivalSparkleCanvas.Children.Add(dot);
            _festivalSparkles.Add(dot);

            var transform = (TranslateTransform)dot.RenderTransform;
            double ampX = (_rng.NextDouble() * 20) + 5;
            double ampY = (_rng.NextDouble() * 20) + 5;
            double durX = (_rng.NextDouble() * 4) + 4;
            double durY = (_rng.NextDouble() * 4) + 4;
            double begin = _rng.NextDouble() * 5;

            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-ampX, ampX, TimeSpan.FromSeconds(durX))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(begin)
            });
            transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-ampY, ampY, TimeSpan.FromSeconds(durY))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(begin)
            });
            dot.BeginAnimation(OpacityProperty, new DoubleAnimation(0.1, 0.9, TimeSpan.FromSeconds((_rng.NextDouble() * 2) + 1.5))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(begin)
            });
        }
    }

    private void StopFestivalSparkles()
    {
        foreach (var dot in _festivalSparkles)
        {
            dot.BeginAnimation(OpacityProperty, null);
            if (dot.RenderTransform is TranslateTransform t)
            {
                t.BeginAnimation(TranslateTransform.XProperty, null);
                t.BeginAnimation(TranslateTransform.YProperty, null);
            }
        }
        FestivalSparkleCanvas.Children.Clear();
        _festivalSparkles.Clear();
    }

    private void MarqueeBulbCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Vegas Marquee" && MarqueePanel.Visibility == Visibility.Visible)
        {
            BuildMarqueeBulbs();
        }
    }

    private void BuildMarqueeBulbs()
    {
        StopMarqueeChase();
        MarqueeBulbCanvas.Children.Clear();
        _marqueeBulbs.Clear();

        double w = MarqueeBulbCanvas.ActualWidth;
        double h = MarqueeBulbCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        const double bulb = 26;
        const double spacing = 54;
        double inset = bulb / 2;

        var positions = new List<System.Windows.Point>();
        for (double x = inset; x <= w - inset; x += spacing) positions.Add(new System.Windows.Point(x, inset));
        for (double y = inset + spacing; y <= h - inset; y += spacing) positions.Add(new System.Windows.Point(w - inset, y));
        for (double x = w - inset - spacing; x >= inset; x -= spacing) positions.Add(new System.Windows.Point(x, h - inset));
        for (double y = h - inset - spacing; y >= inset + spacing; y -= spacing) positions.Add(new System.Windows.Point(inset, y));

        if (positions.Count == 0) return;

        const double glow = bulb * MarqueeBulbGlowScale;
        foreach (var pos in positions)
        {
            var dot = new Ellipse
            {
                Width = glow,
                Height = glow,
                Fill = MarqueeBulbGlowBrush
            };
            Canvas.SetLeft(dot, pos.X - (glow / 2));
            Canvas.SetTop(dot, pos.Y - (glow / 2));
            MarqueeBulbCanvas.Children.Add(dot);
            _marqueeBulbs.Add(dot);
        }

        StartMarqueeChase();
    }

    private void StartMarqueeChase()
    {
        StopMarqueeChase();
        _marqueeStep = 0;
        ApplyMarqueePattern();

        _marqueeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(110) };
        _marqueeTimer.Tick += (_, _) =>
        {
            _marqueeStep++;
            ApplyMarqueePattern();
        };
        _marqueeTimer.Start();
    }

    private void StopMarqueeChase()
    {
        _marqueeTimer?.Stop();
        _marqueeTimer = null;
    }

    private void ApplyMarqueePattern()
    {
        int n = _marqueeBulbs.Count;
        if (n == 0) return;

        for (int i = 0; i < n; i++)
        {
            bool lit = (((i - _marqueeStep) % MarqueeLitPeriod) + MarqueeLitPeriod) % MarqueeLitPeriod == 0;
            _marqueeBulbs[i].Opacity = lit ? 1.0 : MarqueeDimOpacity;
        }
    }

    private static string ReplaceVariables(string template)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        string venue = Lyracist.Core.Helpers.AppSettings.SelectedVenue ?? string.Empty;
        string dj = Lyracist.Core.Helpers.AppSettings.DjName ?? string.Empty;
        return template
            .Replace("{venue}", venue, StringComparison.OrdinalIgnoreCase)
            .Replace("{dj}", dj, StringComparison.OrdinalIgnoreCase);
    }

    private void StartSpaceshipTimer()
    {
        StopSpaceshipTimer();
        _spaceshipTimer = new DispatcherTimer();
        _spaceshipTimer.Tick += SpaceshipTimer_Tick;
        ScheduleNextSpaceship();
    }

    private void StopSpaceshipTimer()
    {
        _spaceshipTimer?.Stop();
        _spaceshipTimer = null;
        CrawlOverlayCanvas?.Children.Clear();
    }

    private void ScheduleNextSpaceship()
    {
        if (_spaceshipTimer == null) return;
        int baseFreq = Lyracist.Core.Helpers.AppSettings.CrawlSpaceshipFrequency;
        int jitter = (int)(baseFreq * 0.15);
        int finalInterval = _rng.Next(Math.Max(5, baseFreq - jitter), baseFreq + jitter);
        _spaceshipTimer.Interval = TimeSpan.FromSeconds(finalInterval);
        _spaceshipTimer.Start();
    }

    private void SpaceshipTimer_Tick(object? sender, EventArgs e)
    {
        _spaceshipTimer?.Stop();
        SpawnSpaceship();
        ScheduleNextSpaceship();
    }

    private void SpawnSpaceship()
    {
        if (_vm == null || _vm.SelectedProjectionView != "Star Wars Crawl" || !IsVisible) return;

        double width = CrawlOverlayCanvas.ActualWidth;
        double height = CrawlOverlayCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var activeSnippets = Lyracist.Core.Helpers.AppSettings.CrawlSpaceshipSnippets
            .Where(s => s.IsEnabled && !string.IsNullOrWhiteSpace(s.Text))
            .ToList();
        if (activeSnippets.Count == 0) return;

        string text = activeSnippets[_rng.Next(activeSnippets.Count)].Text;

        var border = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 0x05, 0x07, 0x0F)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14, 8, 14, 8),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = System.Windows.VerticalAlignment.Top
        };

        var textBlock = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontSize = Lyracist.Core.Helpers.AppSettings.CrawlSpaceshipFontSize,
            FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White,
            Effect = new DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00),
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.8
            }
        };
        border.Child = textBlock;

        var group = new TransformGroup();
        var scale = new ScaleTransform(0.1, 0.1);
        var matrix = new MatrixTransform();
        group.Children.Add(scale);
        group.Children.Add(matrix);
        border.RenderTransform = group;
        border.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

        CrawlOverlayCanvas.Children.Add(border);

        double startX = _rng.NextDouble() < 0.5 ? -150 : width + 150;
        double startY = _rng.Next(100, (int)height - 100);
        double endX = startX < 0 ? width + 150 : -150;
        double endY = _rng.Next(100, (int)height - 100);

        var pathGeometry = new PathGeometry();
        var pathFigure = new PathFigure { StartPoint = new System.Windows.Point(startX, startY) };

        int steps = 120;
        double maxTheta = (_rng.Next(2, 4)) * Math.PI; // 1 to 1.5 full rotations
        double spiralRadius = (_rng.NextDouble() * 150) + 120;

        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            double theta = t * maxTheta;
            double currentRadius = Math.Sin(t * Math.PI) * spiralRadius;
            double cx = startX + (endX - startX) * t;
            double cy = startY + (endY - startY) * t + Math.Sin(t * Math.PI) * 150;

            double x = cx + currentRadius * Math.Cos(theta);
            double y = cy + currentRadius * Math.Sin(theta);

            pathFigure.Segments.Add(new LineSegment(new System.Windows.Point(x, y), isStroked: false));
        }

        pathGeometry.Figures.Add(pathFigure);

        double duration = Lyracist.Core.Helpers.AppSettings.CrawlSpaceshipDuration;
        var storyboard = new Storyboard();

        var pathAnim = new MatrixAnimationUsingPath
        {
            PathGeometry = pathGeometry,
            Duration = TimeSpan.FromSeconds(duration),
            DoesRotateWithTangent = false // Keep the text upright so it is easy to read
        };
        Storyboard.SetTarget(pathAnim, border);
        Storyboard.SetTargetProperty(pathAnim, new PropertyPath("RenderTransform.Children[1].Matrix"));
        storyboard.Children.Add(pathAnim);

        var scaleXAnim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(duration) };
        scaleXAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.1, KeyTime.FromPercent(0.0)));
        scaleXAnim.KeyFrames.Add(new LinearDoubleKeyFrame(1.8, KeyTime.FromPercent(0.5))); // Larger scale peak
        scaleXAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.1, KeyTime.FromPercent(1.0)));
        Storyboard.SetTarget(scaleXAnim, border);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("RenderTransform.Children[0].ScaleX"));
        storyboard.Children.Add(scaleXAnim);

        var scaleYAnim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(duration) };
        scaleYAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.1, KeyTime.FromPercent(0.0)));
        scaleYAnim.KeyFrames.Add(new LinearDoubleKeyFrame(1.8, KeyTime.FromPercent(0.5))); // Larger scale peak
        scaleYAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.1, KeyTime.FromPercent(1.0)));
        Storyboard.SetTarget(scaleYAnim, border);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("RenderTransform.Children[0].ScaleY"));
        storyboard.Children.Add(scaleYAnim);

        var opacityAnim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(duration) };
        opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
        opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0.15)));
        opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0.85)));
        opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));
        Storyboard.SetTarget(opacityAnim, border);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(opacityAnim);

        storyboard.Completed += (s, e) =>
        {
            CrawlOverlayCanvas.Children.Remove(border);
        };

        storyboard.Begin();
    }

    private void ScaryokeWindow_SpinStarted(object? sender, EventArgs e)
    {
        if (sender is ScaryokeWindow scaryokeWindow)
        {
            BuildScaryokeWheel(scaryokeWindow.ViewModel);
            
            double currentAngle = BillboardWheelRotate.Angle;
            double targetAngle = scaryokeWindow.TargetAngle;
            double duration = scaryokeWindow.SpinDuration;
            
            BillboardResultText.Text = "Spinning the wheel to seal a singer's fate...";
            
            var animation = new DoubleAnimation(currentAngle % 360, targetAngle, TimeSpan.FromSeconds(duration))
            {
                DecelerationRatio = 0.9,
                FillBehavior = FillBehavior.HoldEnd
            };
            BillboardWheelRotate.BeginAnimation(RotateTransform.AngleProperty, animation);
        }
    }

    private void ScaryokeWindow_SpinCompleted(object? sender, string category)
    {
        if (sender is ScaryokeWindow scaryokeWindow)
        {
            BillboardWheelRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            BillboardWheelRotate.Angle = scaryokeWindow.TargetAngle % 360;
            
            BillboardResultText.Text = scaryokeWindow.ViewModel.ResultText;
        }
    }

    private void ScaryokeWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ScaryokeWindow scaryokeWindow)
        {
            UpdateScaryokeOverlayVisibility(scaryokeWindow);
        }
    }

    private void UpdateScaryokeOverlayVisibility(ScaryokeWindow scaryokeWindow)
    {
        if (scaryokeWindow.IsVisible)
        {
            BuildScaryokeWheel(scaryokeWindow.ViewModel);
            BillboardResultText.Text = scaryokeWindow.ViewModel.ResultText;
            ScaryokeWheelOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            ScaryokeWheelOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private static System.Windows.Point PolarPoint(double angleDegrees, double radius)
    {
        double rad = angleDegrees * Math.PI / 180.0;
        return new System.Windows.Point(230 + radius * Math.Sin(rad), 230 - radius * Math.Cos(rad));
    }

    private void BuildScaryokeWheel(ScaryokeViewModel vm)
    {
        BillboardWheelCanvas.Children.Clear();
        var segments = vm.WheelSegments;
        if (segments == null || segments.Count == 0) return;
        var strokeBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x13, 0x3A));
        double start = 0;

        for (int i = 0; i < segments.Count; i++)
        {
            double sweep = segments[i].Sweep;
            double end = start + sweep;

            var figure = new PathFigure { StartPoint = new System.Windows.Point(230, 230), IsClosed = true };
            figure.Segments.Add(new LineSegment(PolarPoint(start, 230), true));
            figure.Segments.Add(new ArcSegment(PolarPoint(end, 230), new System.Windows.Size(230, 230), 0,
                false, SweepDirection.Clockwise, true));

            var sliceColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(segments[i].Color);
            var slice = new System.Windows.Shapes.Path
            {
                Data = new PathGeometry(new[] { figure }),
                Fill = new SolidColorBrush(sliceColor),
                Stroke = strokeBrush,
                StrokeThickness = 2
            };
            BillboardWheelCanvas.Children.Add(slice);

            // Label
            double mid = start + sweep / 2;
            var labelPos = PolarPoint(mid, 230 * 0.62);
            double textAngle = mid - 90;
            if (mid > 180) textAngle += 180;

            var label = new Grid
            {
                Width = 150,
                Height = 26,
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = new RotateTransform(textAngle)
            };

            var textColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(segments[i].TextColor);
            bool isDjsChoice = string.Equals(segments[i].Name, "DJ's Choice", StringComparison.OrdinalIgnoreCase);
            label.Children.Add(new TextBlock
            {
                Text = isDjsChoice ? "💀" : segments[i].Name,
                Foreground = new SolidColorBrush(textColor),
                FontSize = isDjsChoice ? 16 : 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            Canvas.SetLeft(label, labelPos.X - label.Width / 2);
            Canvas.SetTop(label, labelPos.Y - label.Height / 2);
            BillboardWheelCanvas.Children.Add(label);

            start = end;
        }
    }

    private void OnReactionReceived(string emoji)
    {
        Dispatcher.Invoke(() =>
        {
            if (ReactionsCanvas == null) return;

            var textBlock = new Emoji.Wpf.TextBlock
            {
                Text = emoji,
                FontSize = 86,
                RenderTransform = new TranslateTransform()
            };

            double width = ActualWidth > 0 ? ActualWidth : 800;
            double height = ActualHeight > 0 ? ActualHeight : 600;

            // Random entry side: 0=bottom, 1=top, 2=left, 3=right
            int side = _rng.Next(4);
            double toX, toY;

            switch (side)
            {
                case 0: // bottom -> travels up
                    Canvas.SetLeft(textBlock, _rng.Next(50, (int)Math.Max(200, width - 100)));
                    Canvas.SetBottom(textBlock, _rng.Next(20, 100));
                    toY = -(height - 150);
                    toX = _rng.NextDouble() * 200 - 100;
                    break;
                case 1: // top -> travels down
                    Canvas.SetLeft(textBlock, _rng.Next(50, (int)Math.Max(200, width - 100)));
                    Canvas.SetTop(textBlock, _rng.Next(20, 100));
                    toY = height - 150;
                    toX = _rng.NextDouble() * 200 - 100;
                    break;
                case 2: // left -> travels right
                    Canvas.SetTop(textBlock, _rng.Next(50, (int)Math.Max(200, height - 100)));
                    Canvas.SetLeft(textBlock, _rng.Next(20, 100));
                    toX = width - 150;
                    toY = _rng.NextDouble() * 200 - 100;
                    break;
                default: // right -> travels left
                    Canvas.SetTop(textBlock, _rng.Next(50, (int)Math.Max(200, height - 100)));
                    Canvas.SetRight(textBlock, _rng.Next(20, 100));
                    toX = -(width - 150);
                    toY = _rng.NextDouble() * 200 - 100;
                    break;
            }

            ReactionsCanvas.Children.Add(textBlock);

            var transform = (TranslateTransform)textBlock.RenderTransform;
            var duration = TimeSpan.FromSeconds(5.5);

            var yAnimation = new DoubleAnimation
            {
                From = 0,
                To = toY,
                Duration = duration
            };

            var xAnimation = new DoubleAnimation
            {
                From = 0,
                To = toX,
                Duration = duration
            };

            var opacityAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = duration
            };

            opacityAnimation.Completed += (s, e) =>
            {
                ReactionsCanvas.Children.Remove(textBlock);
            };

            transform.BeginAnimation(TranslateTransform.YProperty, yAnimation);
            transform.BeginAnimation(TranslateTransform.XProperty, xAnimation);
            textBlock.BeginAnimation(OpacityProperty, opacityAnimation);
        });
    }

    // ── Casino Slot Reels ───────────────────────────────────────────────
    // Each reel strip holds its 7-symbol set duplicated back-to-back (14 rows of 140px =
    // 980px one full set). The scroll position is driven by elapsed wall-clock time modulo
    // the set height - the same technique used to fix the Movie Theater film strip (see
    // TheaterFilmStrip_OnRendering: CompositionTarget.Rendering instead of a WPF Timeline
    // with RepeatBehavior.Forever) - so the fast "blur" spin loops seamlessly with no
    // Timeline wrap instant for a hitch or pop to hide in, and all three reels spin at the
    // same speed (instead of three permanently mismatched speeds, which read as chaotic).
    // After a staggered free-spin window (left reel stops first, like a real cabinet) each
    // reel eases to a stop with its 💎 resting on the payline.
    private const double SlotSymbolHeight = 140;
    private const double SlotReelSetHeight = SlotSymbolHeight * 7; // 980 - one full 7-symbol set
    private const double SlotPaylineY = 95; // center of the 190px-tall reel window (matches the XAML arrow markers)
    private const double SlotSpinPxPerSecond = 500;

    private static double NormalizedMod(double value, double modulus) => ((value % modulus) + modulus) % modulus;

    // The D%980 "forward distance" value at which each reel's 💎 symbol sits exactly on the
    // payline - see the symbol order in the XAML: Reel 1 = 🎤 🍒 🔔 💎 ... (💎 at index 3),
    // Reel 2 = 💎 ⭐ 🎤 ... (index 0), Reel 3 = ⭐ 🍋 💎 ... (index 2). A symbol's own center
    // sits at index*140 + 70 within the strip.
    private static readonly double[] SlotReelLandingDistanceMod =
    [
        NormalizedMod(3 * SlotSymbolHeight + SlotSymbolHeight / 2 - SlotPaylineY, SlotReelSetHeight),
        NormalizedMod(0 * SlotSymbolHeight + SlotSymbolHeight / 2 - SlotPaylineY, SlotReelSetHeight),
        NormalizedMod(2 * SlotSymbolHeight + SlotSymbolHeight / 2 - SlotPaylineY, SlotReelSetHeight),
    ];

    private sealed class SlotReelState
    {
        public TranslateTransform Translate = null!;
        public double SpinSeconds;
        public double LandingDistanceMod;
        public readonly System.Diagnostics.Stopwatch Clock = new();
        public bool IsLanding;
        public bool IsLanded;
        public double LandingFromDistance;
        public double LandingToDistance;
        public double LandingDurationSeconds;
    }

    private SlotReelState[]? _slotReels;
    private bool _slotReelsRenderingHooked;
    private bool _slotJackpotCelebrated;
    private DispatcherTimer? _slotJackpotCleanupTimer;

    private void StartSlotReels()
    {
        _slotReels =
        [
            new SlotReelState { Translate = SlotReel1Translate, SpinSeconds = 3.0, LandingDistanceMod = SlotReelLandingDistanceMod[0] },
            new SlotReelState { Translate = SlotReel2Translate, SpinSeconds = 4.0, LandingDistanceMod = SlotReelLandingDistanceMod[1] },
            new SlotReelState { Translate = SlotReel3Translate, SpinSeconds = 5.0, LandingDistanceMod = SlotReelLandingDistanceMod[2] },
        ];
        foreach (var reel in _slotReels)
        {
            reel.Clock.Restart();
        }

        _slotJackpotCelebrated = false;
        _slotJackpotCleanupTimer?.Stop();
        SlotJackpotBurstCanvas.Children.Clear();

        if (!_slotReelsRenderingHooked)
        {
            CompositionTarget.Rendering += SlotReels_OnRendering;
            _slotReelsRenderingHooked = true;
        }

        var pulse = new DoubleAnimation(18, 40, TimeSpan.FromSeconds(0.75))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        SlotJackpotGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, pulse);
    }

    // Fixed duration for every reel's landing glide, regardless of how far it has to travel to
    // bring its 💎 to the payline. An earlier version derived the duration from the remaining
    // distance (3x the average speed, to match the spin speed exactly at the start of the ease)
    // - but that distance is essentially random (0-980px depending on when the free spin happens
    // to end), so the derived duration ranged from under a second to over seven, meaning some
    // reels were still visibly gliding long after they were expected to have landed. A fixed
    // duration trades a barely-perceptible speed mismatch at the transition for a predictable,
    // consistent stop every time.
    private const double SlotLandingDurationSeconds = 1.0;

    // Runs on every rendered frame while any reel is still moving. Position is always a pure
    // function of elapsed time modulo one set's height (see comment above), and once a reel's
    // free-spin window elapses it eases from its current *distance* to a further distance whose
    // modulo lands the 💎 on the payline - taking the modulo of the eased distance (rather than
    // lerping the on-screen Y directly) so the deceleration still passes through any symbol-set
    // wrap without a visible pop.
    private void SlotReels_OnRendering(object? sender, EventArgs e)
    {
        if (_slotReels == null) return;

        bool allLanded = true;
        foreach (var reel in _slotReels)
        {
            if (reel.IsLanded)
            {
                continue;
            }

            double elapsed = reel.Clock.Elapsed.TotalSeconds;

            if (!reel.IsLanding && elapsed >= reel.SpinSeconds)
            {
                double distanceAtStop = reel.SpinSeconds * SlotSpinPxPerSecond;
                double currentMod = NormalizedMod(distanceAtStop, SlotReelSetHeight);
                double forward = NormalizedMod(reel.LandingDistanceMod - currentMod, SlotReelSetHeight);

                reel.IsLanding = true;
                reel.LandingFromDistance = distanceAtStop;
                reel.LandingToDistance = distanceAtStop + forward;
                reel.LandingDurationSeconds = SlotLandingDurationSeconds;
            }

            double distance;
            if (reel.IsLanding)
            {
                double t = (elapsed - reel.SpinSeconds) / reel.LandingDurationSeconds;
                if (t >= 1.0)
                {
                    distance = reel.LandingToDistance;
                    reel.IsLanded = true;
                }
                else
                {
                    double eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic
                    distance = reel.LandingFromDistance + (reel.LandingToDistance - reel.LandingFromDistance) * eased;
                }
            }
            else
            {
                distance = elapsed * SlotSpinPxPerSecond;
            }

            reel.Translate.Y = -NormalizedMod(distance, SlotReelSetHeight);

            if (!reel.IsLanded)
            {
                allLanded = false;
            }
        }

        if (allLanded)
        {
            if (!_slotJackpotCelebrated)
            {
                _slotJackpotCelebrated = true;
                TriggerSlotJackpotCelebration();
            }
            StopSlotReelsRendering();
        }
    }

    private void StopSlotReelsRendering()
    {
        if (_slotReelsRenderingHooked)
        {
            CompositionTarget.Rendering -= SlotReels_OnRendering;
            _slotReelsRenderingHooked = false;
        }
    }

    // One-shot sparkle burst + expanding shockwave ring, fired from the center of the reel row
    // once all three 💎s land on the payline. Purely decorative - StartSlotReels() clears it
    // (and stops this cleanup timer) before the next spin, so it never lingers or double-fires.
    private void TriggerSlotJackpotCelebration()
    {
        SlotJackpotBurstCanvas.Children.Clear();

        double w = SlotJackpotBurstCanvas.ActualWidth;
        double h = SlotJackpotBurstCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double centerX = w / 2;
        double centerY = h / 2;

        // Width/Height/Canvas.Left/Top are animated directly (not a RenderTransform scale) so the
        // 4px stroke stays a crisp thin ring throughout - scaling the whole element via
        // ScaleTransform would scale the stroke thickness right along with it, turning the ring
        // into a thick glowing blob by the time it's grown a few times its starting size. The
        // final diameter is also kept well inside the reel row's own 190px height so the ring
        // reads as centered on the reels, not ballooning out past the cabinet into the banner.
        var ring = new Ellipse
        {
            Width = 20,
            Height = 20,
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00)),
            StrokeThickness = 4,
        };
        Canvas.SetLeft(ring, centerX - 10);
        Canvas.SetTop(ring, centerY - 10);
        SlotJackpotBurstCanvas.Children.Add(ring);

        const double ringFinalDiameter = 220;
        var ringEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        ring.BeginAnimation(Ellipse.WidthProperty, new DoubleAnimation(20, ringFinalDiameter, TimeSpan.FromSeconds(0.8)) { EasingFunction = ringEase });
        ring.BeginAnimation(Ellipse.HeightProperty, new DoubleAnimation(20, ringFinalDiameter, TimeSpan.FromSeconds(0.8)) { EasingFunction = ringEase });
        ring.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(centerX - 10, centerX - ringFinalDiameter / 2, TimeSpan.FromSeconds(0.8)) { EasingFunction = ringEase });
        ring.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(centerY - 10, centerY - ringFinalDiameter / 2, TimeSpan.FromSeconds(0.8)) { EasingFunction = ringEase });
        ring.BeginAnimation(OpacityProperty, new DoubleAnimation(0.9, 0, TimeSpan.FromSeconds(0.8)));

        const int count = 36;
        for (int i = 0; i < count; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            double distance = (_rng.NextDouble() * 50) + 50;
            double size = (_rng.NextDouble() * 10) + 8;
            var color = SlotParticlePalette[_rng.Next(SlotParticlePalette.Length)];
            var brush = new SolidColorBrush(color);

            // Alternate round sparkles and 8-point glint stars, same shape family as the ambient
            // SlotParticleCanvas coins/sparkles (see BuildSlotParticles).
            Shape particle = i % 2 == 0
                ? new Ellipse { Width = size, Height = size, Fill = brush }
                : new Polygon
                {
                    Points =
                    [
                        new System.Windows.Point(size / 2, 0),
                        new System.Windows.Point(size * 0.6, size * 0.4),
                        new System.Windows.Point(size, size / 2),
                        new System.Windows.Point(size * 0.6, size * 0.6),
                        new System.Windows.Point(size / 2, size),
                        new System.Windows.Point(size * 0.4, size * 0.6),
                        new System.Windows.Point(0, size / 2),
                        new System.Windows.Point(size * 0.4, size * 0.4)
                    ],
                    Fill = brush
                };

            Canvas.SetLeft(particle, centerX - size / 2);
            Canvas.SetTop(particle, centerY - size / 2);

            var translate = new TranslateTransform();
            particle.RenderTransform = translate;
            SlotJackpotBurstCanvas.Children.Add(particle);

            double duration = (_rng.NextDouble() * 0.5) + 0.9;
            var burstEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, Math.Cos(angle) * distance, TimeSpan.FromSeconds(duration)) { EasingFunction = burstEase });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, Math.Sin(angle) * distance, TimeSpan.FromSeconds(duration)) { EasingFunction = burstEase });
            particle.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromSeconds(duration * 0.6)) { BeginTime = TimeSpan.FromSeconds(duration * 0.4) });
        }

        _slotJackpotCleanupTimer?.Stop();
        _slotJackpotCleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        _slotJackpotCleanupTimer.Tick += (_, _) =>
        {
            SlotJackpotBurstCanvas.Children.Clear();
            _slotJackpotCleanupTimer!.Stop();
        };
        _slotJackpotCleanupTimer.Start();
    }

    private void StopSlotReels()
    {
        StopSlotReelsRendering();
        _slotReels = null;
        _slotJackpotCleanupTimer?.Stop();
        SlotJackpotBurstCanvas.Children.Clear();
        SlotJackpotGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
        SlotBannerVideo.Stop();
    }

    // Loads the DJ's chosen sponsor banner (image or video) into the Slot Machine's bottom box.
    // Reuses the same JumbotronBannerPath/IsJumbotronBannerVideo selection as the Stadium
    // Jumbotron - see UpdateJumbotronBanner. No-op unless Casino Slot Reels is the active view -
    // MediaElement only needs to play while visible.
    private void UpdateSlotBanner()
    {
        if (_vm == null || _vm.SelectedProjectionView != "Casino Slot Reels") return;

        if (_vm.IsJumbotronBannerVideo)
        {
            SlotBannerVideo.Source = new Uri(_vm.JumbotronBannerPath);
            SlotBannerVideo.Position = TimeSpan.Zero;
            SlotBannerVideo.Play();
        }
        else
        {
            SlotBannerVideo.Stop();
            SlotBannerVideo.Source = null;
        }
    }

    private void SlotBannerVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        // A MediaEnded event dispatched right as the DJ switches away can arrive after
        // StopSlotReels() already called Stop() - without this guard it would restart
        // playback of a now-hidden, irrelevant video and keep re-triggering itself forever.
        if (_vm?.SelectedProjectionView != "Casino Slot Reels") return;

        SlotBannerVideo.Position = TimeSpan.Zero;
        SlotBannerVideo.Play();
    }

    private void SlotParticleCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Casino Slot Reels" && SlotReelsPanel.Visibility == Visibility.Visible)
        {
            BuildSlotParticles();
        }
    }

    private void BuildSlotParticles()
    {
        StopSlotParticles();
        SlotParticleCanvas.Children.Clear();

        double w = SlotParticleCanvas.ActualWidth;
        double h = SlotParticleCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        int count = 28;
        for (int i = 0; i < count; i++)
        {
            double size = (_rng.NextDouble() * 14) + 10;
            var color = SlotParticlePalette[i % SlotParticlePalette.Length];

            Shape particle;
            if (i % 2 == 0)
            {
                particle = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = new RadialGradientBrush
                    {
                        GradientStops =
                        {
                            new GradientStop(System.Windows.Media.Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF), 0.0),
                            new GradientStop(System.Windows.Media.Color.FromArgb(0xDD, color.R, color.G, color.B), 0.5),
                            new GradientStop(System.Windows.Media.Color.FromArgb(0xAA, 0x8B, 0x65, 0x08), 1.0)
                        }
                    }
                };
            }
            else
            {
                particle = new Polygon
                {
                    Points =
                    [
                        new System.Windows.Point(size / 2, 0),
                        new System.Windows.Point(size * 0.65, size * 0.35),
                        new System.Windows.Point(size, size / 2),
                        new System.Windows.Point(size * 0.65, size * 0.65),
                        new System.Windows.Point(size / 2, size),
                        new System.Windows.Point(size * 0.35, size * 0.65),
                        new System.Windows.Point(0, size / 2),
                        new System.Windows.Point(size * 0.35, size * 0.35)
                    ],
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, color.R, color.G, color.B))
                };
            }

            double left = _rng.NextDouble() * w;
            double top = _rng.NextDouble() * h;
            Canvas.SetLeft(particle, left);
            Canvas.SetTop(particle, top);

            var translate = new TranslateTransform();
            particle.RenderTransform = translate;

            double driftY = -((_rng.NextDouble() * 80) + 40);
            double driftX = (_rng.NextDouble() * 40) - 20;
            double duration = (_rng.NextDouble() * 2.5) + 2.0;
            double begin = _rng.NextDouble() * 2.0;

            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, driftY, TimeSpan.FromSeconds(duration))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(begin)
            });
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-driftX, driftX, TimeSpan.FromSeconds(duration * 1.3))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(begin)
            });
            particle.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 0.95, TimeSpan.FromSeconds(duration))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(begin)
            });

            SlotParticleCanvas.Children.Add(particle);
            _slotCoins.Add(particle);
        }
    }

    private void StopSlotParticles()
    {
        foreach (var elem in _slotCoins)
        {
            elem.BeginAnimation(OpacityProperty, null);
            if (elem.RenderTransform is TranslateTransform t)
            {
                t.BeginAnimation(TranslateTransform.XProperty, null);
                t.BeginAnimation(TranslateTransform.YProperty, null);
            }
        }
        SlotParticleCanvas.Children.Clear();
        _slotCoins.Clear();
    }

    // ── Jukebox ─────────────────────────────────────────────────────────
    private void StartJukebox()
    {
        var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(4))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        JukeboxRecordRotate.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    private void StopJukebox()
    {
        JukeboxRecordRotate.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    private void JukeboxBubbleCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Jukebox" && JukeboxPanel.Visibility == Visibility.Visible)
        {
            BuildJukeboxBubbles();
        }
    }

    private void BuildJukeboxBubbles()
    {
        StopJukeboxBubbles();
        JukeboxBubbleCanvas.Children.Clear();

        double w = JukeboxBubbleCanvas.ActualWidth;
        double h = JukeboxBubbleCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        int bubblesPerTube = 18;
        for (int side = 0; side < 2; side++)
        {
            double tubeLeft = side == 0 ? 12 : w - 48;
            for (int i = 0; i < bubblesPerTube; i++)
            {
                double size = (_rng.NextDouble() * 10) + 6;
                var color = JukeboxBubblePalette[_rng.Next(JukeboxBubblePalette.Length)];

                var bubble = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = new RadialGradientBrush
                    {
                        GradientStops =
                        {
                            new GradientStop(System.Windows.Media.Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF), 0.0),
                            new GradientStop(System.Windows.Media.Color.FromArgb(0x88, color.R, color.G, color.B), 0.6),
                            new GradientStop(System.Windows.Media.Color.FromArgb(0x22, color.R, color.G, color.B), 1.0)
                        }
                    }
                };

                double startX = tubeLeft + (_rng.NextDouble() * 24);
                double startY = h - (_rng.NextDouble() * (h * 0.3));
                Canvas.SetLeft(bubble, startX);
                Canvas.SetTop(bubble, startY);

                var translate = new TranslateTransform();
                bubble.RenderTransform = translate;

                double duration = (_rng.NextDouble() * 2.5) + 3.0;
                double begin = _rng.NextDouble() * 3.0;

                translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -h, TimeSpan.FromSeconds(duration))
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(begin)
                });
                bubble.BeginAnimation(OpacityProperty, new DoubleAnimation(0.3, 0.95, TimeSpan.FromSeconds(duration * 0.5))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(begin)
                });

                JukeboxBubbleCanvas.Children.Add(bubble);
                _jukeboxBubbles.Add(bubble);
            }
        }
    }

    private void StopJukeboxBubbles()
    {
        foreach (var elem in _jukeboxBubbles)
        {
            elem.BeginAnimation(OpacityProperty, null);
            if (elem.RenderTransform is TranslateTransform t)
            {
                t.BeginAnimation(TranslateTransform.YProperty, null);
            }
        }
        JukeboxBubbleCanvas.Children.Clear();
        _jukeboxBubbles.Clear();
    }

    // ── Stadium Jumbotron ───────────────────────────────────────────────
    private void StartJumbotron()
    {
    }

    private void StopJumbotron()
    {
        JumbotronBannerVideo.Stop();
    }

    // Loads the DJ's chosen sponsor banner (image or video) into the Jumbotron's bottom box.
    // No-op unless the Jumbotron is the active view - MediaElement only needs to play while visible.
    private void UpdateJumbotronBanner()
    {
        if (_vm == null || _vm.SelectedProjectionView != "Stadium Jumbotron") return;

        if (_vm.IsJumbotronBannerVideo)
        {
            JumbotronBannerVideo.Source = new Uri(_vm.JumbotronBannerPath);
            JumbotronBannerVideo.Position = TimeSpan.Zero;
            JumbotronBannerVideo.Play();
        }
        else
        {
            JumbotronBannerVideo.Stop();
            JumbotronBannerVideo.Source = null;
        }
    }

    private void JumbotronBannerVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        // A MediaEnded event dispatched right as the DJ switches away can arrive after
        // StopJumbotron() already called Stop() - without this guard it would restart
        // playback of a now-hidden, irrelevant video and keep re-triggering itself forever.
        if (_vm?.SelectedProjectionView != "Stadium Jumbotron") return;

        JumbotronBannerVideo.Position = TimeSpan.Zero;
        JumbotronBannerVideo.Play();
    }

    private void JumbotronSpotlightCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Stadium Jumbotron" && JumbotronPanel.Visibility == Visibility.Visible)
        {
            BuildJumbotronSpotlights();
        }
    }

    private void BuildJumbotronSpotlights()
    {
        StopJumbotronSpotlights();
        JumbotronSpotlightCanvas.Children.Clear();

        double w = JumbotronSpotlightCanvas.ActualWidth;
        double h = JumbotronSpotlightCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double beamLength = Math.Sqrt((w * w) + (h * h)) * 0.9;
        double spread = 45;

        for (int i = 0; i < 2; i++)
        {
            bool isLeft = i == 0;
            double originX = isLeft ? w * 0.08 : w * 0.92;
            double originY = h * 0.95;

            var polygon = new Polygon
            {
                Points = [new System.Windows.Point(0, 0), new System.Windows.Point(-spread, -beamLength), new System.Windows.Point(spread, -beamLength)],
                RenderTransformOrigin = new System.Windows.Point(0.5, 1.0)
            };

            var gradient = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0.5, 1.0),
                EndPoint = new System.Windows.Point(0.5, 0.0),
                GradientStops =
                {
                    new GradientStop(System.Windows.Media.Color.FromArgb(0x44, 0xFF, 0xE0, 0x82), 0.0),
                    new GradientStop(System.Windows.Media.Color.FromArgb(0x22, 0x80, 0xD8, 0xFF), 0.5),
                    new GradientStop(System.Windows.Media.Color.FromArgb(0x00, 0x00, 0x00, 0x00), 1.0)
                }
            };
            polygon.Fill = gradient;

            double baseAngle = isLeft ? 25 : -25;
            var rotate = new RotateTransform(baseAngle);
            polygon.RenderTransform = rotate;

            Canvas.SetLeft(polygon, originX - spread);
            Canvas.SetTop(polygon, originY - beamLength);
            JumbotronSpotlightCanvas.Children.Add(polygon);
            _jumbotronSpotlightRotates.Add(rotate);

            double swing = 30;
            double duration = isLeft ? 6.5 : 7.8;
            var sweep = new DoubleAnimation(baseAngle - swing, baseAngle + swing, TimeSpan.FromSeconds(duration))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            rotate.BeginAnimation(RotateTransform.AngleProperty, sweep);
        }
    }

    private void StopJumbotronSpotlights()
    {
        foreach (var rot in _jumbotronSpotlightRotates)
        {
            rot.BeginAnimation(RotateTransform.AngleProperty, null);
        }
        JumbotronSpotlightCanvas.Children.Clear();
        _jumbotronSpotlightRotates.Clear();
    }

    // ── Movie Theater 'Now Showing' ─────────────────────────────────────
    private void StartTheater()
    {
        var sweep = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(2.0))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        TheaterLeaderSweep.BeginAnimation(RotateTransform.AngleProperty, sweep);
        TheaterLeaderSweepRight.BeginAnimation(RotateTransform.AngleProperty, sweep);
    }

    private void StopTheater()
    {
        TheaterLeaderSweep.BeginAnimation(RotateTransform.AngleProperty, null);
        TheaterLeaderSweepRight.BeginAnimation(RotateTransform.AngleProperty, null);
        StopTheaterFilmStrip();
    }

    // ── Movie Theater film-frame strip ──────────────────────────────────
    // Scrolls a vertical filmstrip: a "COMING ATTRACTIONS" title frame followed directly by
    // one frame per upcoming singer, then repeating with no gap. Built entirely in code (like
    // the other views' particle/beam canvases) because each cycle mixes 2 different frame
    // kinds rather than one repeated data-bound template.
    // Note: types below are fully qualified with System.Windows.* because this project also
    // references System.Windows.Forms/System.Drawing (UseWindowsForms), which otherwise makes
    // Color/Orientation/Brushes/HorizontalAlignment ambiguous.
    private const double TheaterTitleFrameHeight = 110;
    private const double TheaterSingerFrameHeight = 130;

    // Content signature from the last build (singer texts/order + strip width). Routine data
    // refreshes rebuild NextSingers with the exact same entries far more often than the queue
    // actually changes; rebuilding the strip on every one of those would restart the scroll
    // animation from the top and look like it stutters/pauses. Skipping the rebuild when
    // nothing actually changed keeps the loop running uninterrupted.
    private string? _theaterFilmStripSignature;

    // Drives the scroll directly off a running clock every rendered frame (see
    // TheaterFilmStrip_OnRendering) instead of a WPF Timeline with RepeatBehavior.Forever - the
    // position is always computed fresh from elapsed time modulo one set's height, so there is no
    // Timeline "wrap" instant for any engine-level hitch to occur at. _theaterFilmStripTranslate is
    // non-null exactly while the strip is actively scrolling.
    private TranslateTransform? _theaterFilmStripTranslate;
    private double _theaterFilmStripSetHeight;
    private double _theaterFilmStripPxPerSecond;
    private readonly System.Diagnostics.Stopwatch _theaterFilmStripClock = new();
    private bool _theaterFilmStripRenderingHooked;

    private void TheaterFilmStripCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Movie Theater 'Now Showing'" && TheaterPanel.Visibility == Visibility.Visible)
        {
            BuildTheaterFilmStrip();
        }
    }

    // Matches the " {12}" estimated-wait-time badge some display text formats bake directly in
    // (e.g. KSRotation's NextSingerDisplay.Text). That number can tick down on its own as the show
    // runs, with no change to who's actually queued or in what order - the film-strip signature
    // below strips it out so a wait-time tick doesn't count as a "real" queue change and restart
    // the scroll. Lyracist's own NextSingerDisplay.Text doesn't currently embed this badge, but
    // stripping it here keeps the guard correct if that ever changes and keeps the two apps' logic
    // in parity.
    private static readonly Regex TheaterWaitBadgePattern = new(@"\s*\{\d+\}", RegexOptions.Compiled);

    private void BuildTheaterFilmStrip()
    {
        double w = TheaterFilmStripCanvas.ActualWidth;
        double viewportHeight = TheaterFilmStripCanvas.ActualHeight;
        if (w <= 0 || _vm == null) return;

        var singers = _vm.NextSingers.ToList();

        // Joined with U+0001 (a control character that can never appear in singer/song display
        // text) rather than a printable delimiter, so free-text titles can't collide two
        // different queues onto the same signature. Viewport height is included alongside width
        // because it drives the stacked-copy count below - a pure height change (e.g. the DJ
        // resizing the projection window) must still force a rebuild even when the queue itself
        // is unchanged, or the strip would keep scrolling with a copy count sized for the old
        // viewport.
        string signature = w.ToString("F0") + "\u0001" + viewportHeight.ToString("F0") + "\u0001" + string.Join("\u0001",
            singers.Select(s => TheaterWaitBadgePattern.Replace(s.Text, string.Empty) + (s.IsRotationStart ? "#A" : "")));
        if (signature == _theaterFilmStripSignature && TheaterFilmStripCanvas.Children.Count > 0)
        {
            // Same content (ignoring wait-time countdown) already scrolling - leave the
            // running animation alone so the loop never visibly restarts mid-show.
            return;
        }
        // Stop the old animation before recording the new signature - StopTheaterFilmStrip()
        // unconditionally nulls the signature (so a real external stop always forces a rebuild
        // next time), so it must run *before* the assignment below or it would immediately wipe
        // out the very value the no-op-refresh guard above depends on.
        StopTheaterFilmStrip();
        _theaterFilmStripSignature = signature;
        TheaterFilmStripCanvas.Children.Clear();

        double setHeight = TheaterTitleFrameHeight + (singers.Count * TheaterSingerFrameHeight);
        if (setHeight <= 0) return;

        // Stack enough full copies of the set, back-to-back, that the visible viewport is always
        // covered by real content even at the scroll's far extreme (offset -setHeight) - just 2
        // copies only wraps seamlessly if the viewport is no taller than one set. On a display
        // where this panel (an unconstrained Height="*" row) ends up taller than that - a big
        // TV/projector, or a compact hero section leaving more room below - a fixed 2-copy stack
        // runs out of real content near the bottom of the viewport right as the loop wraps,
        // which is visible as a "pop"/redraw once per cycle instead of a seamless loop. Each set
        // is just the title frame followed directly by the singer frames - no divider - so the
        // loop reads as "Coming Attractions, 1, 2, 3, 4, 5, 6, Coming Attractions, 1, 2, ..."
        // with no gap.
        int copies = Math.Max(2, (int)Math.Ceiling((viewportHeight + setHeight) / setHeight));
        for (int copy = 0; copy < copies; copy++)
        {
            double y = copy * setHeight;

            var title = BuildTheaterTitleFrame(w);
            Canvas.SetLeft(title, 0);
            Canvas.SetTop(title, y);
            TheaterFilmStripCanvas.Children.Add(title);
            y += TheaterTitleFrameHeight;

            foreach (var singer in singers)
            {
                var frame = BuildTheaterSingerFrame(singer, w);
                Canvas.SetLeft(frame, 0);
                Canvas.SetTop(frame, y);
                TheaterFilmStripCanvas.Children.Add(frame);
                y += TheaterSingerFrameHeight;
            }
        }

        var translate = new TranslateTransform();
        TheaterFilmStripCanvas.RenderTransform = translate;

        double durationSeconds = Math.Max(10, setHeight / 40.0); // ~40px/sec steady projector crawl, slower floor for tiny queues
        _theaterFilmStripTranslate = translate;
        _theaterFilmStripSetHeight = setHeight;
        _theaterFilmStripPxPerSecond = setHeight / durationSeconds;
        _theaterFilmStripClock.Restart();
        if (!_theaterFilmStripRenderingHooked)
        {
            CompositionTarget.Rendering += TheaterFilmStrip_OnRendering;
            _theaterFilmStripRenderingHooked = true;
        }
    }

    // Runs on every rendered frame while the strip is scrolling. Computes the scroll offset
    // directly from elapsed wall-clock time modulo one set's height, rather than letting a WPF
    // Timeline "restart" itself - the position is always a pure function of elapsed time, so
    // there's no separate wrap/restart instant in the animation engine for a hitch to hide in.
    private void TheaterFilmStrip_OnRendering(object? sender, EventArgs e)
    {
        if (_theaterFilmStripTranslate == null || _theaterFilmStripSetHeight <= 0) return;
        double distance = _theaterFilmStripClock.Elapsed.TotalSeconds * _theaterFilmStripPxPerSecond;
        _theaterFilmStripTranslate.Y = -(distance % _theaterFilmStripSetHeight);
    }

    private void StopTheaterFilmStrip()
    {
        if (_theaterFilmStripRenderingHooked)
        {
            CompositionTarget.Rendering -= TheaterFilmStrip_OnRendering;
            _theaterFilmStripRenderingHooked = false;
        }
        _theaterFilmStripTranslate = null;
        _theaterFilmStripClock.Reset();

        // Invalidate the signature so the next BuildTheaterFilmStrip() call (e.g. when the DJ
        // switches back into this view) always does a real rebuild and restarts the scroll,
        // even if the queue is unchanged - otherwise the guard below would see a matching
        // signature and leave the strip frozen at whatever position it stopped at.
        _theaterFilmStripSignature = null;
    }

    // Shared, frozen brushes/effect for the film-strip frame builders below. Frozen Freezables
    // are immutable and safe to reuse across many elements at once, so hoisting these out of
    // the per-frame builder methods avoids allocating (and change-tracking) 30-60+ throwaway
    // Brush/Effect objects on every rebuild - up to 12 singer frames alone per rebuild (2
    // copies x up to 6 singers).
    private static readonly SolidColorBrush TheaterCelBgBrush = FrozenBrush(0x0A, 0x0A, 0x0A);
    private static readonly SolidColorBrush TheaterSprocketHoleBrush = FrozenBrush(0xE8, 0xD9, 0xB0);
    private static readonly SolidColorBrush TheaterGoldBrush = FrozenBrush(0xFF, 0xE8, 0xB0);
    private static readonly SolidColorBrush TheaterSingerCelBgBrush = FrozenBrush(0x1C, 0x0D, 0x10);
    private static readonly SolidColorBrush TheaterAnchorBadgeBgBrush = FrozenBrush(0xDC, 0x26, 0x26);
    private static readonly SolidColorBrush TheaterSingerTextBrush = FrozenBrush(0xFF, 0xFA, 0xF0);
    private static readonly LinearGradientBrush TheaterTitleGradientBrush = FrozenTitleGradient();
    private static readonly DropShadowEffect TheaterTitleGlowEffect = FrozenGlow();

    private static SolidColorBrush FrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush FrozenTitleGradient()
    {
        var brush = new LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(0x3D, 0x24, 0x06), System.Windows.Media.Color.FromRgb(0x1C, 0x0D, 0x10),
            new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
        brush.Freeze();
        return brush;
    }

    private static DropShadowEffect FrozenGlow()
    {
        var effect = new DropShadowEffect { Color = System.Windows.Media.Color.FromRgb(0xFF, 0xE8, 0xB0), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.8 };
        effect.Freeze();
        return effect;
    }

    private static Grid BuildTheaterFrameShell(double width, double height, out Border cel)
    {
        var root = new Grid
        {
            Width = width,
            Height = height,
            Background = TheaterCelBgBrush
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });

        var leftRail = BuildTheaterSprocketRail(height);
        Grid.SetColumn(leftRail, 0);
        root.Children.Add(leftRail);

        var rightRail = BuildTheaterSprocketRail(height);
        Grid.SetColumn(rightRail, 2);
        root.Children.Add(rightRail);

        cel = new Border
        {
            Margin = new Thickness(2, 8, 2, 8),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(3)
        };
        Grid.SetColumn(cel, 1);
        root.Children.Add(cel);

        return root;
    }

    private static StackPanel BuildTheaterSprocketRail(double frameHeight)
    {
        var panel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        int holeCount = Math.Max(2, (int)(frameHeight / 40));
        for (int i = 0; i < holeCount; i++)
        {
            panel.Children.Add(new Border
            {
                Width = 14,
                Height = 10,
                CornerRadius = new CornerRadius(3),
                Background = TheaterSprocketHoleBrush,
                Margin = new Thickness(0, 6, 0, 6)
            });
        }
        return panel;
    }

    private static Grid BuildTheaterTitleFrame(double width)
    {
        var root = BuildTheaterFrameShell(width, TheaterTitleFrameHeight, out var cel);
        cel.BorderBrush = TheaterGoldBrush;
        cel.Background = TheaterTitleGradientBrush;
        cel.Child = new TextBlock
        {
            Text = "🎬 COMING ATTRACTIONS 🎬",
            FontSize = 26,
            FontWeight = FontWeights.Black,
            Foreground = TheaterGoldBrush,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = TheaterTitleGlowEffect
        };
        return root;
    }

    private static Grid BuildTheaterSingerFrame(RotationWindowViewModel.NextSingerDisplay singer, double width)
    {
        var root = BuildTheaterFrameShell(width, TheaterSingerFrameHeight, out var cel);
        cel.BorderBrush = TheaterGoldBrush;
        cel.Background = TheaterSingerCelBgBrush;

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        if (singer.IsRotationStart)
        {
            stack.Children.Add(new Border
            {
                Background = TheaterAnchorBadgeBgBrush,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                ToolTip = "Rotation Anchor (marks where a round begins)",
                Child = new TextBlock { Text = "⚓ ANCHOR", FontSize = 11, FontWeight = FontWeights.ExtraBold, Foreground = System.Windows.Media.Brushes.White }
            });
        }
        stack.Children.Add(new TextBlock { Text = "🎬", FontSize = 24, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) });
        stack.Children.Add(new TextBlock
        {
            Text = singer.Text,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = TheaterSingerTextBrush,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = Math.Max(60, width - 90)
        });

        cel.Child = stack;
        return root;
    }

    private void TheaterBeamCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Movie Theater 'Now Showing'" && TheaterPanel.Visibility == Visibility.Visible)
        {
            BuildTheaterProjectorBeam();
        }
    }

    private void BuildTheaterProjectorBeam()
    {
        StopTheaterProjectorBeam();
        TheaterBeamCanvas.Children.Clear();

        double w = TheaterBeamCanvas.ActualWidth;
        double h = TheaterBeamCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double topWidth = 60;
        double bottomWidth = w * 0.75;
        double centerX = w / 2;

        var cone = new Polygon
        {
            Points =
            [
                new System.Windows.Point(centerX - (topWidth / 2), 0),
                new System.Windows.Point(centerX + (topWidth / 2), 0),
                new System.Windows.Point(centerX + (bottomWidth / 2), h),
                new System.Windows.Point(centerX - (bottomWidth / 2), h)
            ],
            Fill = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0.5, 0.0),
                EndPoint = new System.Windows.Point(0.5, 1.0),
                GradientStops =
                {
                    new GradientStop(System.Windows.Media.Color.FromArgb(0x30, 0xFF, 0xF0, 0xD0), 0.0),
                    new GradientStop(System.Windows.Media.Color.FromArgb(0x15, 0xFF, 0xE8, 0xB0), 0.4),
                    new GradientStop(System.Windows.Media.Color.FromArgb(0x00, 0x00, 0x00, 0x00), 1.0)
                }
            }
        };

        var flicker = new DoubleAnimation(0.7, 1.0, TimeSpan.FromSeconds(0.12))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        cone.BeginAnimation(OpacityProperty, flicker);

        TheaterBeamCanvas.Children.Add(cone);
        _theaterBeams.Add(cone);
    }

    private void StopTheaterProjectorBeam()
    {
        foreach (var beam in _theaterBeams)
        {
            beam.BeginAnimation(OpacityProperty, null);
        }
        TheaterBeamCanvas.Children.Clear();
        _theaterBeams.Clear();
    }

    public System.Windows.Media.Imaging.BitmapSource CaptureBitmap()
    {
        double width = ActualWidth;
        double height = ActualHeight;

        if (width <= 0) width = 1920;
        if (height <= 0) height = 1080;

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)width,
            (int)height,
            96, 96,
            System.Windows.Media.PixelFormats.Pbgra32);

        rtb.Render(this);
        return rtb;
    }
}