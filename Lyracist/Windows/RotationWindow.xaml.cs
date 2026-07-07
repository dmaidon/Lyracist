using Lyracist.Models;
using Lyracist.ViewModels;
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

namespace Lyracist.Windows;

public partial class RotationWindow : Window
{
    private static readonly Random _rng = new();

    private static readonly SolidColorBrush CrawlGold;
    private static readonly SolidColorBrush CrawlDimGold;
    private static readonly RadialGradientBrush MarqueeBulbBrush;
    private static readonly System.Windows.Media.Color MarqueeBulbGlow = System.Windows.Media.Color.FromRgb(0xA8, 0x55, 0xF7);

    static RotationWindow()
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

    private const double PanelWidth = 800;
    private const double ViewH = 1200;
    private const double ScrollPixelsPerSecond = 90;

    private readonly List<Ellipse> _marqueeBulbs = new();
    private DispatcherTimer? _marqueeTimer;
    private int _marqueeStep;
    private const int MarqueeLitPeriod = 3;
    private const double MarqueeDimOpacity = 0.18;

    private RotationWindowViewModel? _vm;
    private int _crawlGen;   // incremented to invalidate in-flight loops
    private VisualBrush? _crawlBrush;
    private DispatcherTimer? _spaceshipTimer;

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
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        Closed -= OnClosed;
        DataContextChanged -= OnDataContextChanged;
        IsVisibleChanged -= RotationWindow_IsVisibleChanged;
        HookViewModel(null);
        StopSpaceshipTimer();
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
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HookViewModel(DataContext as RotationWindowViewModel);
        RebuildBanner();
        ApplyProjectionViewMode();
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
            _vm.Rotation.CollectionChanged -= Rotation_CollectionChanged;
        }
        _vm = vm;
        if (_vm != null)
        {
            _vm.PropertyChanged += Vm_PropertyChanged;
            _vm.Rotation.CollectionChanged += Rotation_CollectionChanged;
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RotationWindowViewModel.SelectedProjectionView):
                ApplyProjectionViewMode();
                break;

            case nameof(RotationWindowViewModel.CrawlBannerText):
                RestartCrawlIfActive();
                break;

            default:
                RebuildBanner();
                RestartCrawlIfActive();
                break;
        }
    }

    private void Rotation_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildBanner();
        RestartCrawlIfActive();
    }

    private void RestartCrawlIfActive()
    {
        if (_vm?.SelectedProjectionView != "Star Wars Crawl" || CrawlPanel.Visibility != Visibility.Visible)
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
        CrawlStarCanvas.Children.Clear();
        _crawlSource.Children.Clear();
        CrawlMaterial.Brush = null;
        CrawlBackMaterial.Brush = null;
        _crawlBrush = null;

        StopMarqueeChase();
        StopVinylSpin();
        StopSpaceshipTimer();

        NormalPanel.Visibility = Visibility.Collapsed;
        CrawlPanel.Visibility = Visibility.Collapsed;
        MarqueePanel.Visibility = Visibility.Collapsed;
        VinylPanel.Visibility = Visibility.Collapsed;

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

            default:
                NormalPanel.Visibility = Visibility.Visible;
                break;
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
        if (_vm == null || _vm.SelectedProjectionView != "Star Wars Crawl" || _vm.Rotation.Count == 0) return;

        int gen = ++_crawlGen;

        if (CrawlStarCanvas.Children.Count == 0)
        {
            double starW = CrawlStarCanvas.ActualWidth > 0 ? CrawlStarCanvas.ActualWidth : ActualWidth;
            double starH = CrawlStarCanvas.ActualHeight > 0 ? CrawlStarCanvas.ActualHeight : ActualHeight;
            RegenerateStars(starW, starH);
        }

        // Get active ordered list of singers (excluding paused ones, and reordered so current is first)
        var activeSingers = _vm.Rotation.Where(s => !s.IsPaused).ToList();
        if (activeSingers.Count == 0) return;

        var current = activeSingers.FirstOrDefault(s => s.IsCurrent) ?? activeSingers.FirstOrDefault();
        var orderedList = new List<Singer>();
        if (current != null)
        {
            int currentIndex = activeSingers.IndexOf(current);
            int count = activeSingers.Count;
            for (int offset = 0; offset < count; offset++)
            {
                orderedList.Add(activeSingers[(currentIndex + offset) % count]);
            }
        }

        bool hasDesignatedCurrentSinger = activeSingers.Any(s => s.IsCurrent);

        StackPanel textPanel = BuildCrawlTextPanel(orderedList, _vm.CrawlBannerText, hasDesignatedCurrentSinger);
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

    private static StackPanel BuildCrawlTextPanel(List<Singer> rotation, string bannerText, bool hasDesignatedCurrentSinger)
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
        Hide();
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
        Hide();
    }

    private void BannerBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        StartAnimation();
    }

    private void RebuildBanner()
    {
        if (_vm == null) return;

        BannerTextBlock.Inlines.Clear();
        string separator = "    •    ";

        // Prepend announcement banner if visible
        if (_vm.IsAnnouncementVisible && !string.IsNullOrWhiteSpace(_vm.AnnouncementBanner))
        {
            string parsedBanner = ReplaceVariables(_vm.AnnouncementBanner);
            BannerTextBlock.Inlines.Add(new Run(parsedBanner) { Foreground = System.Windows.Media.Brushes.Orange, FontWeight = FontWeights.Bold });
            BannerTextBlock.Inlines.Add(new Run(separator) { Foreground = System.Windows.Media.Brushes.White });
        }

        var activeSingers = _vm.Rotation.Where(s => !s.IsPaused).ToList();
        if (activeSingers.Count == 0)
        {
            string emptyText = ReplaceVariables("Lyracist - No Singers in Queue");
            BannerTextBlock.Inlines.Add(new Run(emptyText) { Foreground = System.Windows.Media.Brushes.White });
            StartAnimation();
            return;
        }

        // Find current singer (either IsCurrent or the first active one)
        var current = activeSingers.FirstOrDefault(s => s.IsCurrent) ?? activeSingers.FirstOrDefault();
        if (current == null) return;

        // Highlight the current performer
        string currentSingerText = $"Current Performer: {current.Name}";
        if (!string.IsNullOrWhiteSpace(current.SongTitle))
        {
            currentSingerText += $" (\"{current.SongTitle}\")";
        }
        BannerTextBlock.Inlines.Add(new Run(currentSingerText)
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
            if (!string.IsNullOrWhiteSpace(singer.SongTitle))
            {
                singerText += $" (\"{singer.SongTitle}\")";
            }

            BannerTextBlock.Inlines.Add(new Run(separator) { Foreground = System.Windows.Media.Brushes.White });
            BannerTextBlock.Inlines.Add(new Run(singerText) { Foreground = System.Windows.Media.Brushes.Cyan });
            addedCount++;
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

        foreach (var pos in positions)
        {
            var dot = new Ellipse
            {
                Width = bulb,
                Height = bulb,
                Fill = MarqueeBulbBrush,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
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
        if (_spaceshipTimer != null)
        {
            _spaceshipTimer.Stop();
            _spaceshipTimer = null;
        }
        if (CrawlOverlayCanvas != null)
        {
            CrawlOverlayCanvas.Children.Clear();
        }
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
            Foreground = System.Windows.Media.Brushes.White
        };

        textBlock.Effect = new DropShadowEffect
        {
            Color = System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00),
            BlurRadius = 8,
            ShadowDepth = 0,
            Opacity = 0.8
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
}