// Last Edit: Jul 01, 2026 16:55 - Resolved culture-specific string warning in BuildCrawlTextPanel.
using KSRotation.Models;
using KSRotation.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace KSRotation.Windows
{
    public partial class SingerDisplayWindow : Window
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

        // Off-tree source for the crawl VisualBrush. Lives outside the visual tree so it
        // never renders a stray copy in CrawlPanel; we Measure/Arrange it manually.
        private readonly Canvas _crawlSource = new()
        {
            Width = PanelWidth,
            Height = ViewH,
            // Transparent so the 3D crawl quad shows only text over the starfield backdrop,
            // not a tinted surface (the emissive material would otherwise brighten a solid fill).
            Background = System.Windows.Media.Brushes.Transparent
        };

        public SingerDisplayWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closed += OnClosed;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            Loaded -= OnLoaded;
            Closed -= OnClosed;
            DataContextChanged -= OnDataContextChanged;
            StopMarqueeChase();
            StopVinylSpin();
            HookViewModel(null);
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

            BannerTextBlock.Inlines.Add(new Run($"{currentSingerLabel}: {_vm.CurrentSinger}")
            {
                Foreground = System.Windows.Media.Brushes.Yellow,
                FontWeight = FontWeights.Bold
            });

            string[] ordinals = ["Next Singer", "2nd", "3rd", "4th", "5th"];
            int i = 0;
            foreach (string next in _vm.NextSingers.Take(5))
            {
                BannerTextBlock.Inlines.Add(new Run(Separator));
                BannerTextBlock.Inlines.Add(new Run($"{ordinals[i]}: {next}"));
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

            NormalPanel.Visibility = Visibility.Collapsed;
            CrawlPanel.Visibility = Visibility.Collapsed;
            FlipTilePanel.Visibility = Visibility.Collapsed;
            MarqueePanel.Visibility = Visibility.Collapsed;
            VinylPanel.Visibility = Visibility.Collapsed;

            switch (_vm.SelectedProjectionView)
            {
                case "Star Wars Crawl":
                    CrawlPanel.Visibility = Visibility.Visible;
                    Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)StartCrawl);
                    break;

                case "Flip Tile Board":
                    FlipTilePanel.Visibility = Visibility.Visible;
                    break;

                case "Vegas Marquee":
                    MarqueePanel.Visibility = Visibility.Visible;
                    Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)BuildMarqueeBulbs);
                    break;

                case "Vinyl Turntable":
                    VinylPanel.Visibility = Visibility.Visible;
                    StartVinylSpin();
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
            double inset = bulb / 2; // center bulbs on the frame edge

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

            _marqueeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(110) };
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
            StackPanel textPanel = BuildCrawlTextPanel(_vm.FullRotation, _vm.CrawlBannerText, _vm.HasDesignatedCurrentSinger);
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
                RenderOptions.SetCachingHint(_crawlBrush, CachingHint.Cache);
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
        private static StackPanel BuildCrawlTextPanel(List<SingerEntry> rotation, string bannerText, bool hasDesignatedCurrentSinger)
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

            string[] ordinals = ["UP NEXT", "2nd", "3rd", "4th", "5th",
                                  "6th", "7th", "8th", "9th", "10th"];

            for (int i = 0; i < rotation.Count; i++)
            {
                SingerEntry entry = rotation[i];
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

                panel.Children.Add(new TextBlock
                {
                    Text = entry.Name,
                    FontSize = isCurrent ? 52 : 36,
                    FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isCurrent ? white : gold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20, 0, 20, 4)
                });

                string songLine = entry.Song ?? string.Empty;
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

            panel.Children.Add(new Border { Height = 300 }); // trailing space
            return panel;
        }
    }
}