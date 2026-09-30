// Edited on Sep 22, 2026 @ 00:06:00 -> Add Casino Slot Reels, Jukebox, Stadium Jumbotron, and Movie Theater 'Now Showing' projection view animations and handlers
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.Shared;
using Lyracist.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
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

    static RotationWindow()
    {
        CrawlGold = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00));
        CrawlGold.Freeze();
        CrawlDimGold = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xA0, 0x00));
        CrawlDimGold.Freeze();
    }

    private const double PanelWidth = 800;
    private const double ViewH = 1200;
    private const double ScrollPixelsPerSecond = 90;

    // Canvas effects shared with KSRotation's SingerDisplayWindow (see Shared/ProjectionEffects.cs).
    // Created after InitializeComponent, since each one drives this window's named canvases.
    private readonly MarqueeBulbChase _marqueeBulbChase;
    private readonly RotatingBeamsEffect _discoBeams;
    private readonly TwinklingDotsEffect _discoLightSpots;
    private readonly SynthwaveGridEffect _synthGrid;
    private readonly RotatingBeamsEffect _festivalBeams;
    private readonly TwinklingDotsEffect _festivalSparkles;
    private readonly SlotMachineEffect _slotMachine;
    private readonly JukeboxBubblesEffect _jukeboxBubbles;
    private readonly JumbotronSpotlightsEffect _jumbotronSpotlights;
    private readonly TheaterEffects _theater;

    // The DJ's "reduced projection effects" setting: fewer particles, no per-element blur.
    private bool ReducedEffects => _vm?.ReducedEffects == true;

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

        _marqueeBulbChase = new MarqueeBulbChase(MarqueeBulbCanvas);
        _discoBeams = new RotatingBeamsEffect(DiscoBeamCanvas);
        _discoLightSpots = new TwinklingDotsEffect(DiscoLightCanvas);
        _synthGrid = new SynthwaveGridEffect(SynthGridCanvas,
            () => _vm?.SelectedProjectionView == "Synthwave Grid" && SynthwavePanel.Visibility == Visibility.Visible);
        _festivalBeams = new RotatingBeamsEffect(FestivalBeamCanvas);
        _festivalSparkles = new TwinklingDotsEffect(FestivalSparkleCanvas);
        _slotMachine = new SlotMachineEffect(SlotReel1Translate, SlotReel2Translate, SlotReel3Translate,
            SlotJackpotBurstCanvas, SlotJackpotGlow, SlotParticleCanvas);
        _jukeboxBubbles = new JukeboxBubblesEffect(JukeboxBubbleCanvas);
        _jumbotronSpotlights = new JumbotronSpotlightsEffect(JumbotronSpotlightCanvas);
        _theater = new TheaterEffects(TheaterFilmStripCanvas, TheaterBeamCanvas);

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

        double ratio = 1.0; // the XAML root Viewbox already scales a fixed 1920x1080 canvas
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
            // Hidden: cancel any mid-fade transition and stop everything; the next show rebuilds the
            // view from scratch without a crossfade.
            _projectionTransitionGen++;
            _displayedProjectionView = null;
            StopAllProjectionAnimations();
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
                // Deferred until UpdateRotation/HighlightSinger finishes: they set CurrentSinger
                // before CurrentSingerIsRotationStart, and the spin reads that flag to decide 💎 vs 7️⃣.
                Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() =>
                {
                    if (_vm?.SelectedProjectionView == "Casino Slot Reels" && SlotReelsPanel.Visibility == Visibility.Visible)
                    {
                        StartSlotReels();
                    }
                }));
                break;

            case nameof(RotationWindowViewModel.ReducedEffects):
                ApplyProjectionViewMode();
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

    // View changes, the reduced-effects toggle, and (re)showing the window all request a rebuild;
    // requests that arrive before it runs collapse into one, so a view is never built and torn down
    // several times in a row (e.g. Loaded plus IsVisibleChanged plus a view change at startup).
    private bool _projectionViewApplyPending;

    private void ApplyProjectionViewMode()
    {
        if (_projectionViewApplyPending) return;
        _projectionViewApplyPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() =>
        {
            _projectionViewApplyPending = false;
            ApplyProjectionViewModeNow();
        }));
    }

    // The view whose panel is currently on screen, and a counter that invalidates an in-flight
    // crossfade when another view change (or a hide) arrives before it finishes.
    private string? _displayedProjectionView;
    private int _projectionTransitionGen;
    private static readonly Duration ProjectionFadeOut = TimeSpan.FromSeconds(0.35);
    private static readonly Duration ProjectionFadeIn = TimeSpan.FromSeconds(0.45);

    private void ApplyProjectionViewModeNow()
    {
        if (_vm == null || !IsVisible) return;

        string target = _vm.SelectedProjectionView;
        int gen = ++_projectionTransitionGen;
        var outgoing = ProjectionPanelFor(_displayedProjectionView);

        // Switching between two different views while on screen (auto-rotation, or the DJ picking a
        // new one) fades the old panel out and the new one in; the old view keeps animating while it
        // fades. Rebuilding the same view (resize, reduced-effects toggle) or the first build after
        // showing the window swaps immediately.
        if (_displayedProjectionView != null && _displayedProjectionView != target && outgoing.Visibility == Visibility.Visible)
        {
            var fadeOut = new DoubleAnimation(outgoing.Opacity, 0, ProjectionFadeOut);
            fadeOut.Completed += (_, _) =>
            {
                if (gen == _projectionTransitionGen) ShowProjectionView(target, fadeIn: true);
            };
            outgoing.BeginAnimation(OpacityProperty, fadeOut);
            return;
        }

        ShowProjectionView(target, fadeIn: false);
    }

    private FrameworkElement[] ProjectionPanels =>
    [
        NormalPanel, CrawlPanel, MarqueePanel, VinylPanel, DiscoPanel, SynthwavePanel,
        FestivalPanel, SlotReelsPanel, JukeboxPanel, JumbotronPanel, TheaterPanel
    ];

    private FrameworkElement ProjectionPanelFor(string? view) => view switch
    {
        "Star Wars Crawl" => CrawlPanel,
        "Vegas Marquee" => MarqueePanel,
        "Vinyl Turntable" => VinylPanel,
        "Disco Ball" => DiscoPanel,
        "Synthwave Grid" => SynthwavePanel,
        "Concert Festival Lineup" => FestivalPanel,
        "Casino Slot Reels" => SlotReelsPanel,
        "Jukebox" => JukeboxPanel,
        "Stadium Jumbotron" => JumbotronPanel,
        "Movie Theater 'Now Showing'" => TheaterPanel,
        _ => NormalPanel
    };

    /// <summary>Stops every projection view's animations and clears their canvases.</summary>
    private void StopAllProjectionAnimations()
    {
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
    }

    private void ShowProjectionView(string view, bool fadeIn)
    {
        StopAllProjectionAnimations();

        foreach (var panel in ProjectionPanels)
        {
            panel.BeginAnimation(OpacityProperty, null);
            panel.Opacity = 1;
            panel.Visibility = Visibility.Collapsed;
        }

        switch (view)
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

        _displayedProjectionView = view;
        if (fadeIn)
        {
            ProjectionPanelFor(view).BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, ProjectionFadeIn));
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

    private void DiscoLightCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Disco Ball" && DiscoPanel.Visibility == Visibility.Visible)
        {
            BuildDiscoLightSpots();
        }
    }

    private void SynthGridCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Synthwave Grid" && SynthwavePanel.Visibility == Visibility.Visible)
        {
            BuildSynthGridStatic();
        }
    }

    private void FestivalBeamCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Concert Festival Lineup" && FestivalPanel.Visibility == Visibility.Visible)
        {
            BuildFestivalBeams();
        }
    }

    private void FestivalSparkleCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Concert Festival Lineup" && FestivalPanel.Visibility == Visibility.Visible)
        {
            BuildFestivalSparkles();
        }
    }

    private void MarqueeBulbCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Vegas Marquee" && MarqueePanel.Visibility == Visibility.Visible)
        {
            BuildMarqueeBulbs();
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

    // Most reactions allowed on screen at once. Each is a large Emoji.Wpf glyph animating for 5.5s;
    // a room full of tablets spamming reactions would otherwise pile up without limit, so extras
    // arriving while the screen is already full are dropped.
    private const int MaxOnScreenReactions = 20;

    private void OnReactionReceived(string emoji)
    {
        // InvokeAsync, not Invoke: this is raised on the SignalR hub's thread, and a synchronous
        // Invoke blocked the hub until the UI thread was free to create and start the animation.
        Dispatcher.InvokeAsync(() =>
        {
            if (ReactionsCanvas == null || ReactionsCanvas.Children.Count >= MaxOnScreenReactions) return;

            var textBlock = new Emoji.Wpf.TextBlock
            {
                Text = emoji,
                FontSize = 86,
                RenderTransform = new TranslateTransform()
            };

            double width = 1920; // design-canvas units (root Viewbox)
            double height = 1080;

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

    // ── Shared canvas effects ───────────────────────────────────────────
    // The effect builders live in Shared/ProjectionEffects.cs (shared with KSRotation's
    // SingerDisplayWindow); these keep this window's existing call sites and pass the DJ's
    // reduced-effects setting through.
    private void RegenerateStars(double starW, double starH) => StarfieldEffect.Regenerate(CrawlStarCanvas, starW, starH, ReducedEffects);

    private void BuildMarqueeBulbs() => _marqueeBulbChase.Build();
    private void StopMarqueeChase() => _marqueeBulbChase.Stop();

    // The disco ball sits 18% of the way down this window's beam canvas.
    private void BuildDiscoBeams() => _discoBeams.BuildDisco(0.18);
    private void StopDiscoBeams() => _discoBeams.Stop();
    private void BuildDiscoLightSpots() => _discoLightSpots.Build(TwinklingDotsEffect.DiscoLightSpots, ReducedEffects);
    private void StopDiscoLightSpots() => _discoLightSpots.Stop();

    private void BuildSynthGridStatic() => _synthGrid.BuildStatic();
    private void StartSynthGrid() => _synthGrid.Start(ReducedEffects);
    private void StopSynthGrid() => _synthGrid.Stop();

    private void BuildFestivalBeams() => _festivalBeams.BuildFestival();
    private void StopFestivalBeams() => _festivalBeams.Stop();
    private void BuildFestivalSparkles() => _festivalSparkles.Build(TwinklingDotsEffect.FestivalSparkles, ReducedEffects);
    private void StopFestivalSparkles() => _festivalSparkles.Stop();

    // ── Casino Slot Reels ───────────────────────────────────────────────
    // Spin/landing, the jackpot celebration, and the floating coins live in SlotMachineEffect. The
    // rotation's anchor singer (the start of a new round) lands 7️⃣s instead of 💎s.
    private void StartSlotReels() => _slotMachine.Spin(_vm?.CurrentSingerIsRotationStart == true, ReducedEffects);

    private void StopSlotReels()
    {
        _slotMachine.Stop();
        SlotBannerVideo.Stop();
    }

    private void BuildSlotParticles() => _slotMachine.BuildCoins(ReducedEffects);
    private void StopSlotParticles() => _slotMachine.StopCoins();

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

    private void BuildJukeboxBubbles() => _jukeboxBubbles.Build(ReducedEffects);
    private void StopJukeboxBubbles() => _jukeboxBubbles.Stop();

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

    private void BuildJumbotronSpotlights() => _jumbotronSpotlights.Build();
    private void StopJumbotronSpotlights() => _jumbotronSpotlights.Stop();

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

    private void BuildTheaterFilmStrip()
    {
        if (_vm == null) return;
        _theater.BuildFilmStrip([.. _vm.NextSingers.Select(s => (s.Text, s.IsRotationStart))]);
    }

    private void StopTheaterFilmStrip() => _theater.StopFilmStrip();
    private void BuildTheaterProjectorBeam() => _theater.BuildProjectorBeam();
    private void StopTheaterProjectorBeam() => _theater.StopProjectorBeam();

    private void TheaterFilmStripCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Movie Theater 'Now Showing'" && TheaterPanel.Visibility == Visibility.Visible)
        {
            BuildTheaterFilmStrip();
        }
    }

    private void TheaterBeamCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedProjectionView == "Movie Theater 'Now Showing'" && TheaterPanel.Visibility == Visibility.Visible)
        {
            BuildTheaterProjectorBeam();
        }
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
