// Edited on Oct 27, 2026 @ 17:05:00 -> Keep Scaryoke wheel overlay visible while Scaryoke mode is active and projected
using KSRotation.Models;
using KSRotation.ViewModels;
using Lyracist.Shared;
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

namespace KSRotation.Windows
{
    public partial class SingerDisplayWindow : Window, ICaptureSource
    {
        private static readonly Random _rng = new();
        private const string Separator = "    •    ";

        private static readonly SolidColorBrush CrawlGold;
        private static readonly SolidColorBrush CrawlDimGold;

        static SingerDisplayWindow()
        {
            CrawlGold = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00));
            CrawlGold.Freeze();
            CrawlDimGold = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xA0, 0x00));
            CrawlDimGold.Freeze();
        }

        // Source-space dimensions for the VisualBrush.
        // PanelWidth × ViewH define the coordinate space sampled by the Viewbox.
        private const double PanelWidth = 800;

        private const double ViewH = 1200;
        private const double ScrollPixelsPerSecond = 90;

        private DisplayViewModel? _vm;
        private int _crawlGen;   // incremented to invalidate in-flight loops
        private VisualBrush? _crawlBrush;

        // Canvas effects shared with Lyracist's RotationWindow (see Shared/ProjectionEffects.cs).
        // Created after InitializeComponent, since each one drives this window's named canvases.
        private readonly MarqueeBulbChase _marqueeBulbChase;
        private readonly CurtainMarqueeBulbChase _curtainBulbChase;
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

        // VisualBrush source canvas declared in XAML (CrawlSourceCanvas) wrapped in a 0x0 clipped Grid
        // to keep layout and render passes active during animations while remaining invisible on screen.
        private Canvas _crawlSource => CrawlSourceCanvas;

        public SingerDisplayWindow()
        {
            InitializeComponent();
            _welcomeHost = Lyracist.Shared.WelcomeOverlayHost.Attach(this);

            _marqueeBulbChase = new MarqueeBulbChase(MarqueeBulbCanvas);
            _curtainBulbChase = new CurtainMarqueeBulbChase(CurtainBulbCanvas);
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

            Loaded += OnLoaded;
            Closed += OnClosed;
            DataContextChanged += OnDataContextChanged;
            SizeChanged += OnSizeChanged;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private readonly Lyracist.Shared.WelcomeOverlayHost _welcomeHost;

        // An empty rotation (or pre-show mode) shows the "sign up for tonight's karaoke" screen instead - but never over the
        // Last Song banner, which has to stay visible even when the rotation is empty.
        private void RefreshSignUpInvite()
        {
            bool show = (_vm?.IsRotationEmpty == true || _vm?.IsPreShowModeActive == true) && LastSongBannerOverlay.Visibility != Visibility.Visible;
            bool showQr = _vm?.ShowQrCode == true;
            _welcomeHost.SetSignUpInvite(
                show,
                showQr ? _vm?.QrCodeImage : null,
                showQr ? _vm?.ConnectionUrl : null,
                showQr ? _vm?.WifiQrCodeImage : null,
                _vm?.WifiSsid,
                _vm?.WifiPasswordDisplay);
        }

        public void UpdateSpecialEventBanner(string? bannerPath)
        {
            if (!string.IsNullOrWhiteSpace(bannerPath) && File.Exists(bannerPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(bannerPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    LastSongBannerImage.Source = bitmap;
                    LastSongBannerOverlay.Visibility = Visibility.Visible;
                    RefreshSignUpInvite();
                    return;
                }
                catch
                {
                    // Fallback on decode failure
                }
            }

            LastSongBannerOverlay.Visibility = Visibility.Collapsed;
            LastSongBannerImage.Source = null;
            RefreshSignUpInvite();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            Loaded -= OnLoaded;
            Closed -= OnClosed;
            DataContextChanged -= OnDataContextChanged;
            SizeChanged -= OnSizeChanged;
            PreviewKeyDown -= OnPreviewKeyDown;
            // Stops every view's animations, not just a few: this window is closed (not hidden) and
            // recreated each time the display is reopened, and the film strip and slot reels hook
            // the static CompositionTarget.Rendering event - left running, that kept the closed
            // window alive and ticking every frame, one more copy per reopen.
            _projectionTransitionGen++; // cancels a crossfade that's still mid-fade
            SetHauntedScreen(false);
            StopAllProjectionAnimations();
            HookViewModel(null);
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

        // ── ViewModel wiring ──────────────────────────────────────────────────
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            HookViewModel(DataContext as DisplayViewModel);
            RebuildBanner();
            ApplyProjectionViewMode();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            HookViewModel(DataContext as DisplayViewModel);
            RebuildBanner();
            ApplyProjectionViewMode();
        }

        private void HookViewModel(DisplayViewModel? vm)
        {
            if (_vm != null)
            {
                _vm.PropertyChanged -= Vm_PropertyChanged;
                _vm.NextSingers.CollectionChanged -= NextSingers_CollectionChanged;
            }
            _vm = vm;
            if (_vm != null)
            {
                _vm.PropertyChanged += Vm_PropertyChanged;
                _vm.NextSingers.CollectionChanged += NextSingers_CollectionChanged;
            }
            RefreshSignUpInvite();
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(DisplayViewModel.IsRotationEmpty):
                case nameof(DisplayViewModel.IsPreShowModeActive):
                case nameof(DisplayViewModel.QrCodeImage):
                case nameof(DisplayViewModel.ConnectionUrl):
                case nameof(DisplayViewModel.ShowQrCode):
                case nameof(DisplayViewModel.WifiQrCodeImage):
                case nameof(DisplayViewModel.WifiSsid):
                case nameof(DisplayViewModel.WifiPasswordDisplay):
                    RefreshSignUpInvite();
                    break;

                case nameof(DisplayViewModel.SelectedProjectionView):
                    ApplyProjectionViewMode();
                    break;

                case nameof(DisplayViewModel.MarqueeSpeed):
                    StartAnimation();   // restart marquee at new speed without rebuilding inlines
                    break;

                case nameof(DisplayViewModel.JumbotronBannerPath):
                    UpdateJumbotronBanner();
                    UpdateSlotBanner();
                    break;

                case nameof(DisplayViewModel.CurrentSingerName):
                    // Deferred until UpdateFromRotation finishes, so the spin reads the final
                    // CurrentSingerIsRotationStart (it decides 💎 vs 7️⃣) whatever order the update
                    // sets the two properties in.
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() =>
                    {
                        if (_vm?.SelectedProjectionView == "Casino Slot Reels" && SlotReelsPanel.Visibility == Visibility.Visible)
                        {
                            StartSlotReels();
                        }
                    }));
                    ScheduleRotationRefresh();
                    break;

                case nameof(DisplayViewModel.ReducedEffects):
                    ApplyProjectionViewMode();
                    break;

                default:
                    ScheduleRotationRefresh();
                    break;
            }
        }

        private void NextSingers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ScheduleRotationRefresh();
        }

        // A single UpdateFromRotation sets ~15 view-model properties and rebuilds NextSingers, each
        // of which used to rebuild the ticker, the crawl and the film strip on the spot - 20+
        // rebuilds for one rotation change, with the film strip rebuilt from partial lists mid-way.
        // The first event now schedules one refresh that runs after the whole update has finished.
        private bool _rotationRefreshPending;

        private void ScheduleRotationRefresh()
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

        // Content key of the crawl currently scrolling. Updates that don't change what the crawl
        // would show leave it running instead of restarting it from the bottom. Null whenever the
        // crawl isn't running, so the next start always builds.
        private string? _crawlSignature;

        private string BuildCrawlSignature()
        {
            if (_vm == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.Append(_vm.CrawlBannerText).Append('\u0001')
              .Append(_vm.HasDesignatedCurrentSinger).Append('\u0001')
              .Append(_vm.ShowEstimatedWaitTime);
            foreach (var s in _vm.FullRotation)
            {
                sb.Append('\u0001').Append(s.Name).Append('\u0002').Append(s.Song).Append('\u0002')
                  .Append(s.Artist).Append('\u0002').Append(s.IsMusic).Append('\u0002')
                  .Append(s.IsRotationStart).Append('\u0002').Append(s.EstimatedWaitMinutes).Append('\u0002').Append(s.ScaryokeCategory);
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

        // ── Marquee banner ────────────────────────────────────────────────────
        private void BannerBorder_SizeChanged(object sender, SizeChangedEventArgs e) => StartAnimation();

        // Text of the ticker currently scrolling. Rebuilding the Inlines restarts the scroll from the
        // right edge, so an update that leaves the ticker text unchanged must not touch it - otherwise
        // every rotation refresh made the ticker visibly jump back to the start.
        private string? _bannerSignature;

        private void RebuildBanner()
        {
            if (_vm == null) return;

            var runs = new List<Run>();

            if (!string.IsNullOrWhiteSpace(_vm.BannerText))
            {
                runs.Add(new Run(_vm.BannerText));
                runs.Add(new Run(Separator));
            }

            string currentSingerLabel = _vm.HasDesignatedCurrentSinger ? "Current Singer" : "First Performer";
            string currentSingerFlag = _vm.CurrentSingerIsRotationStart ? "⚓ " : string.Empty;

            runs.Add(new Run($"{currentSingerLabel}: {currentSingerFlag}{_vm.CurrentSinger}")
            {
                Foreground = System.Windows.Media.Brushes.Yellow,
                FontWeight = FontWeights.Bold
            });

            string[] ordinals = ["Next Singer", "2nd", "3rd", "4th", "5th"];
            int i = 0;
            foreach (var next in _vm.NextSingers.Take(5))
            {
                runs.Add(new Run(Separator));
                string nextFlag = next.IsRotationStart ? "⚓ " : string.Empty;
                runs.Add(new Run($"{ordinals[i]}: {nextFlag}{next.Text}"));
                i++;
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

            double speed = _vm?.MarqueeSpeed is > 0 ? _vm.MarqueeSpeed : 60.0;
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

        // ── Logo visibility ───────────────────────────────────────────────────

        // ── Projection modes toggle ────────────────────────────────────────────
        // View changes, the reduced-effects toggle, and (re)loading all request a rebuild; requests
        // that arrive before it runs collapse into one, so a view is never built and torn down
        // several times in a row (e.g. Loaded plus DataContextChanged plus a view change at startup).
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
        // crossfade when another view change arrives before it finishes.
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

            // Switching between two different views while on screen (auto-rotation, or the DJ picking
            // a new one) fades the old panel out and the new one in; the old view keeps animating
            // while it fades. Rebuilding the same view (resize, reduced-effects toggle) or the first
            // build after opening swaps immediately.
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
            NormalPanel, CrawlPanel, MarqueePanel, CurtainPanel, VinylPanel, DiscoPanel, SynthwavePanel,
            FestivalPanel, SlotReelsPanel, JukeboxPanel, JumbotronPanel, TheaterPanel
        ];

        private FrameworkElement ProjectionPanelFor(string? view) => view switch
        {
            "Star Wars Crawl" => CrawlPanel,
            "Vegas Marquee" => MarqueePanel,
            "Purple Velvet Curtain" => CurtainPanel,
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
            CrawlMaterial?.Brush = null;
            CrawlBackMaterial?.Brush = null;
            _crawlBrush = null;
            StopMarqueeChase();
            StopVinylSpin();
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
            StopCurtainChase();
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

                case "Purple Velvet Curtain":
                    CurtainPanel.Visibility = Visibility.Visible;
                    Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildCurtainFolds);
                    Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildCurtainBulbs);
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

        // ── Vinyl turntable spin ──────────────────────────────────────────────
        private void StartVinylSpin()
        {
            // ~3.5s/rev: slow enough to read the label, fast enough to clearly look like it's spinning.
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

        // ── Disco ball ────────────────────────────────────────────────────────
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

        private void CurtainBulbCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_vm?.SelectedProjectionView == "Purple Velvet Curtain" && CurtainPanel.Visibility == Visibility.Visible)
            {
                BuildCurtainBulbs();
                BuildCurtainFolds();
            }
        }

        private void CrawlStarCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_vm?.SelectedProjectionView == "Star Wars Crawl")
            {
                RegenerateStars(e.NewSize.Width, e.NewSize.Height);
            }
        }

        // ── Crawl animation ───────────────────────────────────────────────────
        private void StartCrawl()
        {
            if (_vm == null || _vm.SelectedProjectionView != "Star Wars Crawl" || _vm.FullRotation.Count == 0) return;

            int gen = ++_crawlGen;
            _crawlSignature = BuildCrawlSignature();

            // Stars - generate once if not already present
            if (CrawlStarCanvas.Children.Count == 0)
            {
                double starW = CrawlStarCanvas.ActualWidth > 0 ? CrawlStarCanvas.ActualWidth : ActualWidth;
                double starH = CrawlStarCanvas.ActualHeight > 0 ? CrawlStarCanvas.ActualHeight : ActualHeight;
                RegenerateStars(starW, starH);
            }

            // Build text panel and host it in an in-tree off-screen canvas.
            StackPanel textPanel = BuildCrawlTextPanel(_vm.FullRotation, _vm.CrawlBannerText, _vm.HasDesignatedCurrentSinger, _vm.ShowEstimatedWaitTime);
            textPanel.Measure(new System.Windows.Size(PanelWidth, double.PositiveInfinity));
            textPanel.Arrange(new Rect(0, 0, PanelWidth, textPanel.DesiredSize.Height));
            double panelH = textPanel.DesiredSize.Height;

            var tt = new TranslateTransform(0, ViewH);
            textPanel.RenderTransform = tt;

            _crawlSource.Children.Clear();
            _crawlSource.Children.Add(textPanel);
            Canvas.SetLeft(textPanel, 0);
            Canvas.SetTop(textPanel, 0);

            // Off-tree visual: Measure/Arrange manually so the VisualBrush has content.
            _crawlSource.Measure(new System.Windows.Size(PanelWidth, ViewH));
            _crawlSource.Arrange(new Rect(0, 0, PanelWidth, ViewH));

            // Viewbox scrolls from above the panel top (empty) down through all content.
            // At Viewbox.Y = -ViewH: panel Y=0 (first singer) is at the viewbox bottom
            //   → maps to UV Y=1 → bottom of 3D quad → bottom of screen (large, near viewer).
            // At Viewbox.Y = panelH: all content has scrolled off the top.
            if (_crawlBrush == null)
            {
                _crawlBrush = new VisualBrush(_crawlSource)
                {
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(0, 0, PanelWidth, ViewH),
                    Stretch = Stretch.Fill
                };
                CrawlMaterial.Brush = _crawlBrush;
                CrawlBackMaterial.Brush = _crawlBrush;
            }

            double duration = Math.Max(20.0, (ViewH + panelH) / ScrollPixelsPerSecond);
            var anim = new DoubleAnimation(ViewH, -panelH, TimeSpan.FromSeconds(duration));
            anim.Completed += (_, _) =>
            {
                if (_crawlGen == gen && _vm?.SelectedProjectionView == "Star Wars Crawl")
                    StartCrawl();
            };
            tt.BeginAnimation(TranslateTransform.YProperty, anim);
        }

        // ── Text panel builder ────────────────────────────────────────────────
        private static StackPanel BuildCrawlTextPanel(List<SingerEntry> rotation, string bannerText, bool hasDesignatedCurrentSinger, bool showEstimatedWaitTime)
        {
            var gold = CrawlGold;
            var dimGold = CrawlDimGold;
            var white = System.Windows.Media.Brushes.White;

            var panel = new StackPanel
            {
                Width = PanelWidth,
                Background = System.Windows.Media.Brushes.Transparent
            };

            // Leading padding — text enters smoothly from below before first singer appears.
            panel.Children.Add(new Border { Height = 120 });

            if (!string.IsNullOrWhiteSpace(bannerText))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = bannerText.ToUpperInvariant(),
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
                SingerEntry entry = rotation[i];
                bool isCurrent = i == 0;

                if (entry.IsMusic)
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = isCurrent ? "NOW PLAYING" : "BACKGROUND MUSIC",
                        FontSize = isCurrent ? 20 : 15,
                        FontStyle = FontStyles.Italic,
                        Foreground = isCurrent ? white : dimGold,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 6)
                    });

                    panel.Children.Add(new TextBlock
                    {
                        Text = entry.Song,
                        FontSize = isCurrent ? 52 : 36,
                        FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = isCurrent ? white : gold,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(20, 0, 20, 4)
                    });

                    string details = string.IsNullOrWhiteSpace(entry.Artist)
                        ? $"Requested by {entry.Name}"
                        : $"by {entry.Artist}   •   Requested by {entry.Name}";

                    panel.Children.Add(new TextBlock
                    {
                        Text = details,
                        FontSize = isCurrent ? 24 : 18,
                        Foreground = dimGold,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(20, 0, 20, isCurrent ? 70 : 48)
                    });
                }
                else
                {
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

                    string crawlNameText = entry.IsRotationStart ? $"⚓ {entry.Name}" : entry.Name;
                    if (!string.IsNullOrWhiteSpace(entry.ScaryokeCategory))
                    {
                        crawlNameText += $" [🎃 {entry.ScaryokeCategory}]";
                    }
                    if (showEstimatedWaitTime && entry.EstimatedWaitMinutes > 0)
                    {
                        crawlNameText += $" {{{entry.EstimatedWaitMinutes}}}";
                    }

                    panel.Children.Add(new TextBlock
                    {
                        Text = crawlNameText,
                        FontSize = isCurrent ? 52 : 36,
                        FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = isCurrent ? white : gold,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(20, 0, 20, 4)
                    });

                    string songLine = entry.Song ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(entry.Artist))
                    {
                        songLine += string.IsNullOrWhiteSpace(songLine)
                            ? entry.Artist
                            : $"  –  {entry.Artist}";
                    }

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
            }

            panel.Children.Add(new Border { Height = 300 }); // trailing space
            return panel;
        }

        // ── Shared canvas effects ─────────────────────────────────────────────
        // The effect builders live in Shared/ProjectionEffects.cs (shared with Lyracist's
        // RotationWindow); these keep this window's existing call sites and pass the DJ's
        // reduced-effects setting through.
        private void RegenerateStars(double starW, double starH) => StarfieldEffect.Regenerate(CrawlStarCanvas, starW, starH, ReducedEffects);

        private void BuildMarqueeBulbs() => _marqueeBulbChase.Build();
        private void StopMarqueeChase() => _marqueeBulbChase.Stop();
        private void BuildCurtainBulbs() => _curtainBulbChase.Build();
        private void StopCurtainChase() => _curtainBulbChase.Stop();
        private void BuildCurtainFolds() => VelvetCurtainEffect.BuildFolds(CurtainFoldsCanvas, CurtainPanel.ActualWidth > 0 ? CurtainPanel.ActualWidth : 1920, CurtainPanel.ActualHeight > 0 ? CurtainPanel.ActualHeight : 1080);

        // The disco ball sits 14% of the way down this window's beam canvas.
        private void BuildDiscoBeams() => _discoBeams.BuildDisco(0.14);
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

        private void BuildJukeboxBubbles() => _jukeboxBubbles.Build(ReducedEffects);
        private void StopJukeboxBubbles() => _jukeboxBubbles.Stop();

        private void BuildJumbotronSpotlights() => _jumbotronSpotlights.Build();
        private void StopJumbotronSpotlights() => _jumbotronSpotlights.Stop();

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

        #region Scaryoke Wheel Projection

        private const double ScaryokeRadius = 220;

        private static System.Windows.Point ScaryokePolarPoint(double angleDegrees, double radius)
        {
            double rad = angleDegrees * Math.PI / 180.0;
            return new System.Windows.Point(ScaryokeRadius + (radius * Math.Sin(rad)), ScaryokeRadius - (radius * Math.Cos(rad)));
        }

        public void UpdateScaryokeOverlay(bool isVisible)
        {
            ScaryokeWheelOverlay.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            if (isVisible)
            {
                Topmost = true;
                Activate();
            }
        }

        public void BuildScaryokeWheel(IReadOnlyList<ScaryokeWheelSegment> segments)
        {
            ScaryokeWheelCanvas.Children.Clear();
            if (segments == null || segments.Count == 0) return;

            var strokeBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x13, 0x3A));
            strokeBrush.Freeze();
            double start = 0;

            for (int i = 0; i < segments.Count; i++)
            {
                double sweep = segments[i].Sweep;
                double end = start + sweep;

                var figure = new PathFigure
                {
                    StartPoint = new System.Windows.Point(ScaryokeRadius, ScaryokeRadius),
                    IsClosed = true
                };
                figure.Segments.Add(new LineSegment(ScaryokePolarPoint(start, ScaryokeRadius), true));
                figure.Segments.Add(new ArcSegment(
                    ScaryokePolarPoint(end, ScaryokeRadius),
                    new System.Windows.Size(ScaryokeRadius, ScaryokeRadius),
                    0,
                    sweep > 180.0,
                    SweepDirection.Clockwise,
                    true));

                var sliceColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(segments[i].Color);
                var slice = new System.Windows.Shapes.Path
                {
                    Data = new PathGeometry(new[] { figure }),
                    Fill = new SolidColorBrush(sliceColor),
                    Stroke = strokeBrush,
                    StrokeThickness = 2
                };
                ScaryokeWheelCanvas.Children.Add(slice);

                // Label
                double mid = start + (sweep / 2);
                var labelPos = ScaryokePolarPoint(mid, ScaryokeRadius * 0.62);
                double textAngle = mid - 90;
                if (mid > 180) textAngle += 180;

                var label = new Grid
                {
                    Width = 140,
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
                    FontWeight = FontWeights.Bold,
                    FontSize = isDjsChoice ? 18 : 12,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                Canvas.SetLeft(label, labelPos.X - 70);
                Canvas.SetTop(label, labelPos.Y - 13);
                ScaryokeWheelCanvas.Children.Add(label);

                start = end;
            }
        }

        private System.Windows.Threading.DispatcherTimer? _scaryokeAutoDismissTimer;

        public void StartScaryokeSpin(double targetAngle, double duration)
        {
            _scaryokeAutoDismissTimer?.Stop();
            // Only (re)show when hidden: UpdateScaryokeOverlay activates the window and would steal focus from the DJ's controls every spin
            if (ScaryokeWheelOverlay.Visibility != Visibility.Visible)
            {
                UpdateScaryokeOverlay(true);
            }
            if (_vm != null)
            {
                _vm.ScaryokeResultText = "SPINNING...";
                _vm.ScaryokeResultDetail = "Spinning the wheel to seal a singer's fate...";
            }
            double current = ScaryokeWheelRotate.Angle % 360;
            var animation = new DoubleAnimation(current, targetAngle, TimeSpan.FromSeconds(duration))
            {
                EasingFunction = new PowerEase { Power = 3.5, EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            ScaryokeWheelRotate.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        public void CompleteScaryokeSpin(string resultText, string resultDetail, bool isDjsChoice, double finalAngle, bool keepVisible = true)
        {
            ScaryokeWheelRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            ScaryokeWheelRotate.Angle = finalAngle % 360;

            if (_vm != null)
            {
                _vm.ScaryokeResultText = resultText;
                _vm.ScaryokeResultDetail = resultDetail;
            }

            // Animate result scale & shake
            var scaleAnim = new DoubleAnimation(0.7, 1.0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }
            };
            ScaryokeResultScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            ScaryokeResultScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

            if (isDjsChoice)
            {
                var flashAnim = new DoubleAnimationUsingKeyFrames();
                flashAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
                flashAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500))));
                ScaryokeFlashOverlay.BeginAnimation(UIElement.OpacityProperty, flashAnim);
                PlayLightningStrike();
            }

            // Auto-dismiss unless Scaryoke mode is active AND the wheel is meant to stay projected
            // (a booth-only mode one-off projected spin must not leave the overlay stuck on screen).
            if (!keepVisible || !(_vm?.IsScaryokeActive ?? false))
            {
                _scaryokeAutoDismissTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
                _scaryokeAutoDismissTimer.Tick -= OnScaryokeAutoDismissTick;
                _scaryokeAutoDismissTimer.Tick += OnScaryokeAutoDismissTick;
                _scaryokeAutoDismissTimer.Stop();
                _scaryokeAutoDismissTimer.Start();
            }
        }

        /// <summary>White double-strobe plus a violent shake of the wheel overlay, for the DJ's Choice reveal.</summary>
        private void PlayLightningStrike()
        {
            var strobe = new DoubleAnimationUsingKeyFrames();
            (double Opacity, int Ms)[] frames = [(0.95, 40), (0.05, 130), (0.85, 190), (0, 420), (0.6, 560), (0, 900)];
            foreach (var (opacity, ms) in frames)
            {
                strobe.KeyFrames.Add(new LinearDoubleKeyFrame(opacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));
            }
            ScaryokeLightningOverlay.BeginAnimation(UIElement.OpacityProperty, strobe);

            var shake = new DoubleAnimationUsingKeyFrames();
            double[] offsets = [-26, 26, -20, 20, -14, 14, -8, 8, -3, 0];
            for (int i = 0; i < offsets.Length; i++)
            {
                shake.KeyFrames.Add(new LinearDoubleKeyFrame(offsets[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds((i + 1) * 55))));
            }
            ScaryokeOverlayShake.BeginAnimation(TranslateTransform.XProperty, shake);
        }

        private void OnScaryokeAutoDismissTick(object? sender, EventArgs e)
        {
            _scaryokeAutoDismissTimer?.Stop();
            UpdateScaryokeOverlay(false);
        }

        #endregion

        #region Haunted Screen

        private System.Windows.Threading.DispatcherTimer? _hauntedFlyerTimer;
        private bool _hauntedOn;

        /// <summary>Turns the seasonal candlelight / fog / flying-bat effects on or off. Animations only run while enabled.</summary>
        public void SetHauntedScreen(bool on)
        {
            if (on == _hauntedOn) return;
            _hauntedOn = on;
            HauntedLayer.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

            if (!on)
            {
                _hauntedFlyerTimer?.Stop();
                HauntedFog1Shift.BeginAnimation(TranslateTransform.XProperty, null);
                HauntedFog2Shift.BeginAnimation(TranslateTransform.XProperty, null);
                HauntedCandle.BeginAnimation(UIElement.OpacityProperty, null);
                HauntedFlyerShift.BeginAnimation(TranslateTransform.XProperty, null);
                HauntedFlyerShift.BeginAnimation(TranslateTransform.YProperty, null);
                HauntedFlyer.BeginAnimation(UIElement.OpacityProperty, null);
                HauntedFlyer.Opacity = 0;
                return;
            }

            var drift1 = new DoubleAnimation(-500, -60, TimeSpan.FromSeconds(42))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            HauntedFog1Shift.BeginAnimation(TranslateTransform.XProperty, drift1);

            var drift2 = new DoubleAnimation(-60, -620, TimeSpan.FromSeconds(58))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            HauntedFog2Shift.BeginAnimation(TranslateTransform.XProperty, drift2);

            // Irregular flicker so it reads as candlelight rather than a metronome
            var flicker = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
            (double Opacity, int Ms)[] steps = [(0.85, 0), (0.7, 160), (0.95, 330), (0.75, 410), (0.9, 700), (0.62, 790), (0.88, 1150), (0.8, 1400), (0.85, 1700)];
            foreach (var (opacity, ms) in steps)
            {
                flicker.KeyFrames.Add(new LinearDoubleKeyFrame(opacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));
            }
            HauntedCandle.BeginAnimation(UIElement.OpacityProperty, flicker);

            _hauntedFlyerTimer ??= new System.Windows.Threading.DispatcherTimer();
            _hauntedFlyerTimer.Tick -= OnHauntedFlyerTick;
            _hauntedFlyerTimer.Tick += OnHauntedFlyerTick;
            _hauntedFlyerTimer.Interval = TimeSpan.FromSeconds(4);
            _hauntedFlyerTimer.Start();
        }

        private void OnHauntedFlyerTick(object? sender, EventArgs e)
        {
            if (!_hauntedOn) return;
            if (_hauntedFlyerTimer != null) _hauntedFlyerTimer.Interval = TimeSpan.FromSeconds(14 + _rng.Next(0, 14));

            string[] flyers = ["🦇", "🦇", "👻", "🦇", "🕷️"];
            HauntedFlyer.Text = flyers[_rng.Next(flyers.Length)];

            bool leftToRight = _rng.Next(2) == 0;
            double y = _rng.Next(60, 640);
            TimeSpan flight = TimeSpan.FromSeconds(6.5);

            HauntedFlyerShift.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(leftToRight ? -200 : 2100, leftToRight ? 2100 : -200, flight));

            var wobble = new DoubleAnimationUsingKeyFrames { Duration = flight };
            double[] ys = [y, y - 90, y + 50, y - 60, y + 30, y];
            for (int i = 0; i < ys.Length; i++)
            {
                wobble.KeyFrames.Add(new EasingDoubleKeyFrame(ys[i], KeyTime.FromPercent((i + 1) / (double)ys.Length),
                    new SineEase { EasingMode = EasingMode.EaseInOut }));
            }
            HauntedFlyerShift.BeginAnimation(TranslateTransform.YProperty, wobble);

            var fade = new DoubleAnimationUsingKeyFrames { Duration = flight };
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(0.1)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(0.9)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1.0)));
            HauntedFlyer.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        #endregion
    }
}
