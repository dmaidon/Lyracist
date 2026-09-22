// Edited on Aug 25, 2026 @ 06:38:00 -> Fix RCS1118 const inset and RCS1001 if braces
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

        // Glowing bulb fill for the Marquee frame: bright lavender core fading to purple.
        private static readonly RadialGradientBrush MarqueeBulbBrush;

        private static readonly System.Windows.Media.Color MarqueeBulbGlow =
            System.Windows.Media.Color.FromRgb(0xA8, 0x55, 0xF7);

        static SingerDisplayWindow()
        {
            CrawlGold = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00));
            CrawlGold.Freeze();
            CrawlDimGold = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xA0, 0x00));
            CrawlDimGold.Freeze();

            MarqueeBulbBrush = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(System.Windows.Media.Color.FromRgb(0xF3, 0xE8, 0xFF), 0.0),
                    new GradientStop(System.Windows.Media.Color.FromRgb(0xA8, 0x55, 0xF7), 0.55),
                    new GradientStop(System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED), 1.0)
                }
            };
            MarqueeBulbBrush.Freeze();
        }

        // Source-space dimensions for the VisualBrush.
        // PanelWidth × ViewH define the coordinate space sampled by the Viewbox.
        private const double PanelWidth = 800;

        private const double ViewH = 1200;
        private const double ScrollPixelsPerSecond = 90;

        private DisplayViewModel? _vm;
        private int _crawlGen;   // incremented to invalidate in-flight loops
        private VisualBrush? _crawlBrush;

        // Marquee "marching ants" bulb chase state.
        private readonly List<Ellipse> _marqueeBulbs = [];

        private DispatcherTimer? _marqueeTimer;
        private int _marqueeStep;
        private const int MarqueeLitPeriod = 3;   // every Nth bulb is lit at any moment
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

        // VisualBrush source canvas declared in XAML (CrawlSourceCanvas) wrapped in a 0x0 clipped Grid
        // to keep layout and render passes active during animations while remaining invisible on screen.
        private Canvas _crawlSource => CrawlSourceCanvas;

        public SingerDisplayWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closed += OnClosed;
            DataContextChanged += OnDataContextChanged;
            SizeChanged += OnSizeChanged;
            PreviewKeyDown += OnPreviewKeyDown;
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
            SizeChanged -= OnSizeChanged;
            PreviewKeyDown -= OnPreviewKeyDown;
            StopMarqueeChase();
            StopVinylSpin();
            StopDiscoBall();
            StopDiscoBeams();
            StopDiscoLightSpots();
            StopSynthGrid();
            StopFestivalBeams();
            StopFestivalSparkles();
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

            double ratio = ActualHeight / 1080.0;
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
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(DisplayViewModel.SelectedProjectionView):
                    ApplyProjectionViewMode();
                    break;

                case nameof(DisplayViewModel.MarqueeSpeed):
                    StartAnimation();   // restart marquee at new speed without rebuilding inlines
                    break;

                default:
                    RebuildBanner();
                    RestartCrawlIfActive();
                    break;
            }
        }

        private void NextSingers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildBanner();
            RestartCrawlIfActive();
        }

        // Crawl picks up updated rotation data on its next natural loop iteration.

        private void RestartCrawlIfActive()
        {
            if (_vm?.SelectedProjectionView != "Star Wars Crawl" || CrawlPanel.Visibility != Visibility.Visible)
            {
                return;
            }

            _crawlGen++;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)StartCrawl);
        }

        // ── Marquee banner ────────────────────────────────────────────────────
        private void BannerBorder_SizeChanged(object sender, SizeChangedEventArgs e) => StartAnimation();

        private void RebuildBanner()
        {
            if (_vm == null) return;

            BannerTextBlock.Inlines.Clear();

            if (!string.IsNullOrWhiteSpace(_vm.BannerText))
            {
                BannerTextBlock.Inlines.Add(new Run(_vm.BannerText));
                BannerTextBlock.Inlines.Add(new Run(Separator));
            }

            string currentSingerLabel = _vm.HasDesignatedCurrentSinger ? "Current Singer" : "First Performer";
            string currentSingerFlag = _vm.CurrentSingerIsRotationStart ? "⚓ " : string.Empty;

            BannerTextBlock.Inlines.Add(new Run($"{currentSingerLabel}: {currentSingerFlag}{_vm.CurrentSinger}")
            {
                Foreground = System.Windows.Media.Brushes.Yellow,
                FontWeight = FontWeights.Bold
            });

            string[] ordinals = ["Next Singer", "2nd", "3rd", "4th", "5th"];
            int i = 0;
            foreach (var next in _vm.NextSingers.Take(5))
            {
                BannerTextBlock.Inlines.Add(new Run(Separator));
                string nextFlag = next.IsRotationStart ? "⚓ " : string.Empty;
                BannerTextBlock.Inlines.Add(new Run($"{ordinals[i]}: {nextFlag}{next.Text}"));
                i++;
            }

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
        private void ApplyProjectionViewMode()
        {
            if (_vm == null) return;

            // Stop animations and clear crawl canvas
            _crawlGen++;
            CrawlStarCanvas.Children.Clear();
            _crawlSource.Children.Clear();
            CrawlMaterial?.Brush = null;
            CrawlBackMaterial?.Brush = null;
            _crawlBrush = null;
            StopMarqueeChase();
            MarqueeBulbCanvas.Children.Clear();
            _marqueeBulbs.Clear();
            StopVinylSpin();
            StopDiscoBall();
            StopDiscoBeams();
            StopDiscoLightSpots();
            StopSynthGrid();
            StopFestivalBeams();
            StopFestivalSparkles();

            NormalPanel.Visibility = Visibility.Collapsed;
            CrawlPanel.Visibility = Visibility.Collapsed;
            MarqueePanel.Visibility = Visibility.Collapsed;
            VinylPanel.Visibility = Visibility.Collapsed;
            DiscoPanel.Visibility = Visibility.Collapsed;
            SynthwavePanel.Visibility = Visibility.Collapsed;
            FestivalPanel.Visibility = Visibility.Collapsed;

            switch (_vm.SelectedProjectionView)
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

                default:
                    NormalPanel.Visibility = Visibility.Visible;
                    break;
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

        /// <summary>Sweeping colored spotlight beams fanned out from the ball, each an independently
        /// rotating wedge - alternating spin direction/duration per beam is what makes the sweep read
        /// as chaotic disco lighting rather than a single synchronized rotation.</summary>
        private void BuildDiscoBeams()
        {
            StopDiscoBeams();
            DiscoBeamCanvas.Children.Clear();

            double w = DiscoBeamCanvas.ActualWidth;
            double h = DiscoBeamCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double anchorX = w / 2.0;
            double anchorY = h * 0.14;
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

        /// <summary>Small colored dots drifting and twinkling across the dance floor, standing in for the
        /// ball's reflected light spots - reuses the same wobble-and-twinkle technique as the Star Wars
        /// crawl's star field (independent looping animations with randomized phase/duration).</summary>
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
        /// reuses the same spawn/animate/self-remove shape as <see cref="SpawnSpaceship"/>-style effects.</summary>
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

        /// <summary>Lays out evenly-spaced bulbs around the marquee frame. A timer then steps a fixed lit/unlit
        /// pattern around the perimeter ("marching ants"), so a crisp run of lit bulbs marches around the loop.</summary>
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
            const double inset = bulb / 2; // center bulbs on the frame edge

            // Walk the perimeter clockwise so the lit pattern marches smoothly around the loop.
            var positions = new List<System.Windows.Point>();
            for (double x = inset; x <= w - inset; x += spacing) positions.Add(new(x, inset));                  // top L→R
            for (double y = inset + spacing; y <= h - inset; y += spacing) positions.Add(new(w - inset, y));    // right T→B
            for (double x = w - inset - spacing; x >= inset; x -= spacing) positions.Add(new(x, h - inset));    // bottom R→L
            for (double y = h - inset - spacing; y >= inset + spacing; y -= spacing) positions.Add(new(inset, y)); // left B→T

            if (positions.Count == 0) return;

            foreach (var pos in positions)
            {
                var dot = new Ellipse
                {
                    Width = bulb,
                    Height = bulb,
                    Fill = MarqueeBulbBrush,
                    Effect = new DropShadowEffect
                    {
                        Color = MarqueeBulbGlow,
                        BlurRadius = 22,
                        ShadowDepth = 0,
                        Opacity = 0.95
                    }
                };
                Canvas.SetLeft(dot, pos.X - (bulb / 2));
                Canvas.SetTop(dot, pos.Y - (bulb / 2));
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

            _marqueeTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(110) };
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

        /// <summary>Lights every <see cref="MarqueeLitPeriod"/>-th bulb, offset by the current step, so the
        /// lit set advances one bulb per tick around the frame.</summary>
        private void ApplyMarqueePattern()
        {
            int n = _marqueeBulbs.Count;
            if (n == 0) return;

            for (int i = 0; i < n; i++)
            {
                // Subtract the step so the lit bulbs march clockwise (in position order).
                bool lit = (((i - _marqueeStep) % MarqueeLitPeriod) + MarqueeLitPeriod) % MarqueeLitPeriod == 0;
                _marqueeBulbs[i].Opacity = lit ? 1.0 : MarqueeDimOpacity;
            }
        }

        private void CrawlStarCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_vm?.SelectedProjectionView == "Star Wars Crawl")
            {
                RegenerateStars(e.NewSize.Width, e.NewSize.Height);
            }
        }

        private void RegenerateStars(double starW, double starH)
        {
            CrawlStarCanvas.Children.Clear();
            if (starW <= 0 || starH <= 0) return;

            // Distant galaxies first so the starfield renders on top of them.
            GenerateGalaxies(starW, starH);

            // Scale the star count to the panel area so larger displays don't look sparse.
            int starCount = (int)Math.Clamp(starW * starH / 4500.0, 220, 600);

            for (int i = 0; i < starCount; i++)
            {
                double sz;
                double op;
                double roll = _rng.NextDouble();

                // Three brightness/size tiers mimic a real night sky: lots of faint pinpricks,
                // some medium stars, and a few large bright ones that anchor the field.
                if (roll < 0.70)
                {
                    // Faint background stars.
                    sz = (_rng.NextDouble() * 1.1) + 0.7;
                    op = (_rng.NextDouble() * 0.3) + 0.25;
                }
                else if (roll < 0.93)
                {
                    // Medium stars.
                    sz = (_rng.NextDouble() * 1.6) + 1.8;
                    op = (_rng.NextDouble() * 0.3) + 0.6;
                }
                else
                {
                    // Bright "anchor" stars.
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

                // Soft glow halo on the brightest stars so they read as luminous, not flat dots.
                if (isBright)
                {
                    star.Effect = new DropShadowEffect
                    {
                        Color = fill.Color,
                        BlurRadius = sz * 3,
                        ShadowDepth = 0,
                        Opacity = 0.9
                    };
                }

                Canvas.SetLeft(star, _rng.NextDouble() * starW);
                Canvas.SetTop(star, _rng.NextDouble() * starH);
                CrawlStarCanvas.Children.Add(star);

                // Brighter stars twinkle more often; the dim ones mostly hold steady.
                double twinkleChance = isBright ? 0.6 : 0.25;
                if (_rng.NextDouble() < twinkleChance)
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

        /// <summary>Returns a frozen near-white brush with a subtle warm or cool tint, like real starlight.</summary>
        private static SolidColorBrush MakeStarBrush()
        {
            double tint = _rng.NextDouble();
            byte r, g, b;
            if (tint < 0.25)
            {
                // Cool blue-white.
                r = 0xCF; g = 0xDD; b = 0xFF;
            }
            else if (tint < 0.45)
            {
                // Warm amber-white.
                r = 0xFF; g = 0xF2; b = 0xD8;
            }
            else
            {
                // Pure white (most common).
                r = g = b = 0xFF;
            }

            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        /// <summary>Scatters a few faint, tinted galaxy glows across the backdrop for depth.</summary>
        private void GenerateGalaxies(double w, double h)
        {
            int count = _rng.Next(3, 6); // 3–5 galaxies

            for (int i = 0; i < count; i++)
            {
                // Elongated elliptical disk; the width/height ratio + rotation suggest an inclined galaxy.
                double gw = (_rng.NextDouble() * 180) + 130;          // 130–310
                double gh = gw * ((_rng.NextDouble() * 0.30) + 0.32); // 0.32–0.62 of the width

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

                // Bright concentrated core.
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
                    Opacity = (_rng.NextDouble() * 0.25) + 0.4, // 0.40–0.65, kept subtle
                    Effect = new BlurEffect { Radius = 8 },
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

        /// <summary>Returns a (core, mid) color pair for a galaxy glow, varying the hue per call.</summary>
        private static (System.Windows.Media.Color core, System.Windows.Media.Color mid) PickGalaxyPalette()
        {
            static System.Windows.Media.Color Argb(byte a, byte r, byte g, byte b) =>
                System.Windows.Media.Color.FromArgb(a, r, g, b);

            return _rng.Next(4) switch
            {
                0 => (Argb(180, 210, 220, 255), Argb(75, 90, 120, 210)),  // cool blue
                1 => (Argb(180, 255, 220, 245), Argb(75, 170, 90, 180)),  // purple / magenta
                2 => (Argb(180, 215, 255, 245), Argb(75, 70, 170, 150)),  // teal
                _ => (Argb(180, 255, 240, 215), Argb(75, 200, 150, 90)),  // amber
            };
        }

        // ── Crawl animation ───────────────────────────────────────────────────
        private void StartCrawl()
        {
            if (_vm == null || _vm.SelectedProjectionView != "Star Wars Crawl" || _vm.FullRotation.Count == 0) return;

            int gen = ++_crawlGen;

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
}