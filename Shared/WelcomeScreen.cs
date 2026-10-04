// Edited on Oct 4, 2026 @ 09:52:00 -> Suppress welcome screens while Pre-Show Screen is active and show in sequence on close
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Application = System.Windows.Application;
using Panel = System.Windows.Controls.Panel;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Point = System.Windows.Point;
using Image = System.Windows.Controls.Image;
using ColorConverter = System.Windows.Media.ColorConverter;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace Lyracist.Shared;

/// <summary>One welcome screen being shown: who, which random design, and for how long.</summary>
/// <param name="MonitorDevice">Device name of the monitor that gets a dedicated welcome window, or null
/// to overlay the rotation / DJ banner windows where they already are.</param>
public sealed record WelcomeRequest(string Name, int Design, int Seconds, string? MonitorDevice = null);

/// <summary>One entry of the "which screen shows the welcome" list; an empty DeviceName means the default.</summary>
public sealed record WelcomeScreenChoice(string DeviceName, string Label);

/// <summary>One model choice for the welcome screen (e.g. "All (Random)" or a specific design).</summary>
public sealed record WelcomeDesignChoice(int Id, string Name);

/// <summary>
/// Decides when a "Welcome to our new performer" screen is shown and for how long. Both apps call
/// <see cref="TryWelcome"/> when a singer is added; every projection window that hosts a
/// <see cref="WelcomeOverlayHost"/> listens to <see cref="CurrentChanged"/> and covers whatever it
/// was showing (rotation or DJ banner) until the timer ends.
/// </summary>
public sealed class WelcomeScreenService
{
    public const int DefaultSeconds = 15;
    public const int MinSeconds = 3;
    public const int MaxSeconds = 120;

    // Singer rows the apps create as editable placeholders - never greeted by that name.
    private static readonly string[] PlaceholderNames = ["New Singer", "Special Guest", "Music Request"];

    public static WelcomeScreenService Instance { get; } = new();

    private readonly object _gate = new();
    private readonly HashSet<string> _greeted = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _queue = new();
    private readonly WelcomeMonitorWindowHost _monitorHost = new();
    private DispatcherTimer? _timer;
    private int _lastDesign = -1;
    private int _seconds = DefaultSeconds;
    private bool _isPreShowActive;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true (Pre-Show Screen mode is active), singer welcome screens are not shown on projection
    /// displays. Newly welcomed singers stay queued in sequence and automatically begin displaying one by one
    /// once Pre-Show mode is closed/turned off.
    /// </summary>
    public bool IsPreShowActive
    {
        get
        {
            lock (_gate) return _isPreShowActive;
        }
        set
        {
            RunOnUi(() =>
            {
                lock (_gate)
                {
                    if (_isPreShowActive == value) return;
                    _isPreShowActive = value;
                }

                if (value)
                {
                    // Pre-show screen engaged: hide any active welcome screen immediately and re-queue it at the front
                    if (Current != null)
                    {
                        string currentName = Current.Name;
                        _timer?.Stop();
                        SetCurrent(null);
                        var remaining = _queue.ToList();
                        _queue.Clear();
                        _queue.Enqueue(currentName);
                        foreach (var item in remaining)
                        {
                            _queue.Enqueue(item);
                        }
                    }
                }
                else
                {
                    // Pre-show screen closed: play any queued welcome screens in sequence
                    if (Current == null && _queue.Count > 0)
                    {
                        Start(_queue.Dequeue());
                    }
                }
            });
        }
    }

    /// <summary>
    /// Index of the chosen welcome screen design, or -1 for "All (Random)".
    /// </summary>
    public int SelectedDesign { get; set; } = -1;

    /// <summary>
    /// Device name of the monitor that shows the welcome in its own window. Empty (the default) overlays
    /// the rotation and DJ banner windows on whichever screens they are already on. A device that is no
    /// longer connected also falls back to that default.
    /// </summary>
    public string TargetMonitorDevice { get; set; } = string.Empty;

    /// <summary>How long each welcome stays up; clamped to <see cref="MinSeconds"/>-<see cref="MaxSeconds"/>.</summary>
    public int Seconds
    {
        get => _seconds;
        set => _seconds = ClampSeconds(value);
    }

    /// <summary>Dispatcher to run on; defaults to the application dispatcher. Set by tests.</summary>
    public Dispatcher? Dispatcher { get; set; }

    /// <summary>Length of one "second" of display time; only tests change it, to keep them fast.</summary>
    public TimeSpan SecondsUnit { get; set; } = TimeSpan.FromSeconds(1);

    public WelcomeRequest? Current { get; private set; }

    /// <summary>Raised on the UI thread whenever the active welcome starts (non-null) or ends (null).</summary>
    public event EventHandler<WelcomeRequest?>? CurrentChanged;

    /// <summary>The default choice followed by every connected monitor.</summary>
    public static List<WelcomeScreenChoice> GetScreenChoices()
    {
        var choices = new List<WelcomeScreenChoice> { new(string.Empty, "Rotation & DJ Banner screens (default)") };
        try
        {
            foreach (var m in MonitorEnumerator.GetMonitors())
            {
                choices.Add(new WelcomeScreenChoice(m.DeviceName,
                    $"Monitor {m.Index + 1} ({m.Width}x{m.Height}){(m.IsPrimary ? " [Primary]" : "")}"));
            }
        }
        catch (Exception)
        {
            // Monitor enumeration failing just leaves the default choice.
        }
        return choices;
    }

    public static int ClampSeconds(int seconds) => Math.Clamp(seconds, MinSeconds, MaxSeconds);

    public static string NormalizeName(string? name) =>
        string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static bool IsPlaceholderName(string? name)
    {
        string normalized = NormalizeName(name);
        return normalized.Length == 0 || PlaceholderNames.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasBeenWelcomed(string? name)
    {
        lock (_gate) return _greeted.Contains(NormalizeName(name));
    }

    /// <summary>
    /// Welcomes <paramref name="name"/> unless the feature is off, the name is a placeholder, or this
    /// singer was already welcomed tonight. Returns true when a welcome was accepted (shown now or queued).
    /// </summary>
    public bool TryWelcome(string? name)
    {
        string normalized = NormalizeName(name);
        if (!Enabled || IsPlaceholderName(normalized)) return false;

        lock (_gate)
        {
            if (!_greeted.Add(normalized)) return false;
        }

        RunOnUi(() => Enqueue(normalized));
        return true;
    }

    /// <summary>Shows a welcome regardless of the enabled flag or tonight's history (DJ "Preview" button).</summary>
    public void Preview(string? name = null)
    {
        string normalized = NormalizeName(name);
        if (normalized.Length == 0) normalized = "Your Name Here";
        RunOnUi(() =>
        {
            ClearQueueAndStopTimer();
            Start(normalized);
        });
    }

    /// <summary>Ends the current welcome immediately and drops anything queued behind it.</summary>
    public void Dismiss() => RunOnUi(() =>
    {
        ClearQueueAndStopTimer();
        SetCurrent(null);
    });

    /// <summary>Forgets who has been welcomed - call when the rotation is cleared for a new night.</summary>
    public void ResetTonight()
    {
        lock (_gate) _greeted.Clear();
        RunOnUi(ClearQueueAndStopTimer);
    }

    private void Enqueue(string name)
    {
        bool preShow;
        lock (_gate) preShow = _isPreShowActive;

        if (preShow || Current != null)
        {
            _queue.Enqueue(name);
            return;
        }
        Start(name);
    }

    private void Start(string name)
    {
        int design = PickDesign();
        int seconds = Seconds;
        var d = Dispatcher ?? Application.Current?.Dispatcher;
        if (_timer == null && d != null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Normal, d);
            _timer.Tick += OnTimerTick;
        }
        _timer?.Stop();
        if (_timer != null) _timer.Interval = seconds * SecondsUnit;
        _timer?.Start();

        string? monitor = string.IsNullOrEmpty(TargetMonitorDevice) || WelcomeMonitorWindowHost.FindScreen(TargetMonitorDevice) == null
            ? null
            : TargetMonitorDevice;
        SetCurrent(new WelcomeRequest(name, design, seconds, monitor));
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer?.Stop();
        bool preShow;
        lock (_gate) preShow = _isPreShowActive;

        if (!preShow && _queue.Count > 0)
        {
            Start(_queue.Dequeue());
            return;
        }
        SetCurrent(null);
    }

    private void ClearQueueAndStopTimer()
    {
        _queue.Clear();
        _timer?.Stop();
    }

    private void SetCurrent(WelcomeRequest? request)
    {
        Current = request;
        _monitorHost.Update(request);
        CurrentChanged?.Invoke(this, request);
    }

    // Random (unless a specific design is selected), but never the same design twice in a row when random.
    private int PickDesign()
    {
        if (SelectedDesign >= 0 && SelectedDesign < WelcomeScreenDesigns.Count)
        {
            return SelectedDesign;
        }

        int count = WelcomeScreenDesigns.Count;
        int design = Random.Shared.Next(count);
        if (count > 1 && design == _lastDesign) design = (design + 1 + Random.Shared.Next(count - 1)) % count;
        _lastDesign = design;
        return design;
    }

    private void RunOnUi(Action action)
    {
        var dispatcher = Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher == null) { action(); return; }
        if (dispatcher.CheckAccess()) action();
        else dispatcher.InvokeAsync(action);
    }
}

/// <summary>
/// Layers the welcome screen over a projection window's existing content. Wraps the window's content
/// in a Grid once, so no XAML changes are needed; unsubscribes when the window closes.
/// </summary>
public sealed class WelcomeOverlayHost
{
    private readonly Window _window;
    private readonly Grid _layer = new() { Visibility = Visibility.Collapsed };
    private readonly Grid _inviteLayer = new() { Visibility = Visibility.Collapsed };
    private WelcomeVisual? _visual;
    private WelcomeVisual? _inviteVisual;
    private ImageSource? _inviteQr;
    private string? _inviteUrl;
    private ImageSource? _inviteWifiQr;
    private string? _inviteWifiSsid;
    private string? _inviteWifiPassword;
    private int _generation;
    private int _inviteGeneration;

    private WelcomeOverlayHost(Window window) => _window = window;

    public static WelcomeOverlayHost Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var host = new WelcomeOverlayHost(window);
        var root = new Grid();
        if (window.Content is UIElement original)
        {
            window.Content = null;
            root.Children.Add(original);
        }
        // The sign-up invite sits below the welcome so a welcome can still pop over it.
        Panel.SetZIndex(host._inviteLayer, 900);
        root.Children.Add(host._inviteLayer);
        Panel.SetZIndex(host._layer, 1000);
        root.Children.Add(host._layer);
        window.Content = root;

        WelcomeScreenService.Instance.CurrentChanged += host.OnCurrentChanged;
        window.Closed += (_, _) =>
        {
            WelcomeScreenService.Instance.CurrentChanged -= host.OnCurrentChanged;
            host.Clear();
            host.ClearInvite();
        };

        // A window created (or re-created) mid-welcome joins the one already on screen.
        if (WelcomeScreenService.Instance.Current is { MonitorDevice: null } active) host.Show(active);
        return host;
    }

    /// <summary>
    /// Covers this window's (empty) rotation with the "sign up for tonight's karaoke" screen while
    /// <paramref name="show"/> is true, and fades back to the untouched content when it turns false.
    /// Safe to call repeatedly; it only rebuilds when the QR code or address changes.
    /// </summary>
    public void SetSignUpInvite(
        bool show,
        ImageSource? qr,
        string? url,
        ImageSource? wifiQr = null,
        string? wifiSsid = null,
        string? wifiPassword = null)
    {
        if (!_window.Dispatcher.CheckAccess())
        {
            _window.Dispatcher.InvokeAsync(() => SetSignUpInvite(show, qr, url, wifiQr, wifiSsid, wifiPassword));
            return;
        }

        if (!show)
        {
            if (_inviteLayer.Visibility != Visibility.Visible) return;
            int generation = ++_inviteGeneration;
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
            fade.Completed += (_, _) =>
            {
                if (generation == _inviteGeneration) ClearInvite();
            };
            _inviteLayer.BeginAnimation(UIElement.OpacityProperty, fade);
            return;
        }

        bool alreadyShown = _inviteVisual != null && _inviteLayer.Visibility == Visibility.Visible;
        if (alreadyShown &&
            ReferenceEquals(qr, _inviteQr) &&
            url == _inviteUrl &&
            ReferenceEquals(wifiQr, _inviteWifiQr) &&
            wifiSsid == _inviteWifiSsid &&
            wifiPassword == _inviteWifiPassword)
        {
            // Same content: just make sure a fade-out that was starting is cancelled.
            _inviteGeneration++;
            _inviteLayer.BeginAnimation(UIElement.OpacityProperty, null);
            _inviteLayer.Opacity = 1;
            return;
        }

        _inviteGeneration++;
        ClearInvite();
        _inviteQr = qr;
        _inviteUrl = url;
        _inviteWifiQr = wifiQr;
        _inviteWifiSsid = wifiSsid;
        _inviteWifiPassword = wifiPassword;
        _inviteVisual = WelcomeScreenDesigns.BuildSignUpInvite(qr, url, wifiQr, wifiSsid, wifiPassword);
        _inviteLayer.Children.Add(_inviteVisual.Root);
        _inviteLayer.Visibility = Visibility.Visible;
        _inviteLayer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)));
    }

    private void ClearInvite()
    {
        _inviteLayer.BeginAnimation(UIElement.OpacityProperty, null);
        _inviteLayer.Children.Clear();
        _inviteLayer.Visibility = Visibility.Collapsed;
        _inviteVisual?.Dispose();
        _inviteVisual = null;
        _inviteQr = null;
        _inviteUrl = null;
        _inviteWifiQr = null;
        _inviteWifiSsid = null;
        _inviteWifiPassword = null;
    }

    private void OnCurrentChanged(object? sender, WelcomeRequest? request)
    {
        if (!_window.Dispatcher.CheckAccess())
        {
            _window.Dispatcher.InvokeAsync(() => OnCurrentChanged(sender, request));
            return;
        }

        // A request with a MonitorDevice is shown in its own window instead, so the rotation and DJ
        // banner windows leave it alone; either way this window's own content is never modified.
        if (request == null || request.MonitorDevice != null) FadeOut();
        else Show(request);
    }

    private void Show(WelcomeRequest request)
    {
        _generation++;
        Clear();
        _visual = WelcomeScreenDesigns.Build(request);
        _layer.Children.Add(_visual.Root);
        _layer.Visibility = Visibility.Visible;
        _layer.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)));
    }

    private void FadeOut()
    {
        if (_layer.Visibility != Visibility.Visible) return;
        int generation = _generation;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
        fade.Completed += (_, _) =>
        {
            // A newer welcome may have started while this one was fading.
            if (generation == _generation) Clear();
        };
        _layer.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void Clear()
    {
        _layer.BeginAnimation(UIElement.OpacityProperty, null);
        _layer.Children.Clear();
        _layer.Visibility = Visibility.Collapsed;
        // Looping animations are tied to the clock tree, not the element, so they must be stopped
        // explicitly or they keep ticking (and keep the visuals alive) after the welcome ends.
        _visual?.Dispose();
        _visual = null;
    }
}

/// <summary>
/// Shows the welcome in its own borderless, topmost window on one chosen monitor (instead of over the
/// rotation / DJ banner windows). Back-to-back welcomes swap content in the same window, so there is no
/// flash between them; when the last one ends the window fades out and closes, revealing whatever was
/// underneath unchanged. Never takes keyboard focus, so it can't interrupt the DJ typing.
/// </summary>
internal sealed class WelcomeMonitorWindowHost
{
    private Window? _window;
    private Grid? _layer;
    private WelcomeVisual? _visual;
    private int _generation;

    public static System.Windows.Forms.Screen? FindScreen(string deviceName) =>
        System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s =>
            string.Equals(s.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

    public void Update(WelcomeRequest? request)
    {
        if (request?.MonitorDevice is { } device) Show(request, device);
        else Hide();
    }

    private void Show(WelcomeRequest request, string device)
    {
        var screen = FindScreen(device);
        if (screen == null) return;

        _generation++;
        if (_window == null)
        {
            _layer = new Grid();
            _window = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                Background = Brushes.Black,
                Title = "Welcome Screen",
                Content = _layer,
            };
            // Click to dismiss early (also drops any welcomes still queued).
            _window.MouseLeftButtonDown += (_, _) => WelcomeScreenService.Instance.Dismiss();
            _window.Closed += (_, _) => { _window = null; _layer = null; ReleaseVisual(); };
            WindowPositioner.FillArea(_window, screen.Bounds);
            _window.Show();
        }
        else
        {
            WindowPositioner.FillArea(_window, screen.Bounds);
        }

        ReleaseVisual();
        _layer!.Children.Clear();
        _visual = WelcomeScreenDesigns.Build(request);
        _layer.Children.Add(_visual.Root);
        _layer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)));
    }

    private void Hide()
    {
        if (_window == null) return;

        int generation = _generation;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(500));
        fade.Completed += (_, _) =>
        {
            // A newer welcome may have started while this one was fading.
            if (generation == _generation) _window?.Close();
        };
        _layer?.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private void ReleaseVisual()
    {
        // Looping animations must be stopped explicitly or they outlive the window.
        _visual?.Dispose();
        _visual = null;
    }
}

/// <summary>A built welcome design plus the animations to stop when it is removed.</summary>
public sealed class WelcomeVisual(FrameworkElement root) : IDisposable
{
    private readonly List<(IAnimatable Target, DependencyProperty Property)> _animations = [];

    public FrameworkElement Root { get; } = root;

    public void Animate(IAnimatable target, DependencyProperty property, AnimationTimeline animation)
    {
        _animations.Add((target, property));
        target.BeginAnimation(property, animation);
    }

    public void Dispose()
    {
        foreach (var (target, property) in _animations) target.BeginAnimation(property, null);
        _animations.Clear();
    }
}

/// <summary>The random welcome designs. Each is drawn on a 1920x1080 canvas scaled to the window.</summary>
public static class WelcomeScreenDesigns
{
    public const int Count = 6;

    public static readonly IReadOnlyList<WelcomeDesignChoice> Choices =
    [
        new(-1, "All (Random)"),
        new(0, "Spotlight"),
        new(1, "Neon Night"),
        new(2, "Sunset Stage"),
        new(3, "Confetti Party"),
        new(4, "Disco Rays"),
        new(5, "Red Velvet Curtain"),
    ];

    public static IReadOnlyList<WelcomeDesignChoice> GetDesignChoices() => Choices;

    private const double W = 1920;
    private const double H = 1080;

    private static readonly FontFamily DisplayFont = new("Segoe UI Black, Segoe UI");
    private static readonly FontFamily BodyFont = new("Segoe UI Semibold, Segoe UI");

    public static WelcomeVisual Build(WelcomeRequest request)
    {
        var canvas = new Grid { Width = W, Height = H, ClipToBounds = true };
        var viewbox = new Viewbox { Stretch = Stretch.Uniform, Child = canvas };
        var host = new Grid { Background = Brushes.Black };
        host.Children.Add(viewbox);
        var visual = new WelcomeVisual(host);

        switch (request.Design % Count)
        {
            case 0: Spotlight(canvas, visual, request.Name); break;
            case 1: NeonNight(canvas, visual, request.Name); break;
            case 2: SunsetStage(canvas, visual, request.Name); break;
            case 3: ConfettiParty(canvas, visual, request.Name); break;
            case 4: DiscoRays(canvas, visual, request.Name); break;
            default: RedCurtain(canvas, visual, request.Name); break;
        }
        return visual;
    }

    #region Designs

    private static void Spotlight(Grid canvas, WelcomeVisual visual, string name)
    {
        canvas.Background = Radial("#2B2F7A", "#080A1E", 0.5, 0.45, 0.85);

        var beam = new Ellipse
        {
            Width = 1500, Height = 1500,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Fill = Radial("#FFFFFF", "#00FFFFFF", 0.5, 0.5, 0.5),
            Opacity = 0.2,
        };
        canvas.Children.Add(beam);
        visual.Animate(beam, UIElement.OpacityProperty, Pulse(0.14, 0.3, 2.4));

        AddText(canvas, visual, name, Solid("#FFD76A"), Solid("#FFFFFF"), Solid("#FFD76A"), Rgb("#FFB400"));
    }

    private static void NeonNight(Grid canvas, WelcomeVisual visual, string name)
    {
        canvas.Background = Solid("#07040F");

        var frame = new Border
        {
            Margin = new Thickness(70),
            BorderBrush = Solid("#FF2BD6"),
            BorderThickness = new Thickness(10),
            CornerRadius = new CornerRadius(40),
            Effect = new DropShadowEffect { Color = Rgb("#FF2BD6"), BlurRadius = 45, ShadowDepth = 0, Opacity = 1 },
        };
        canvas.Children.Add(frame);
        visual.Animate(frame, UIElement.OpacityProperty, Pulse(0.55, 1.0, 1.4));

        AddText(canvas, visual, name, Solid("#4DF3FF"), Solid("#FF4FD8"), Solid("#4DF3FF"), Rgb("#00E5FF"));
    }

    private static void SunsetStage(Grid canvas, WelcomeVisual visual, string name)
    {
        canvas.Background = new LinearGradientBrush(
            [new GradientStop(Rgb("#3B1C8C"), 0), new GradientStop(Rgb("#C2307A"), 0.55), new GradientStop(Rgb("#FF9966"), 1)],
            90);

        var sun = new Ellipse
        {
            Width = 900, Height = 900,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, -430),
            Fill = Radial("#FFF2A8", "#00FF9966", 0.5, 0.5, 0.5),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1),
        };
        canvas.Children.Add(sun);
        var scale = (ScaleTransform)sun.RenderTransform;
        visual.Animate(scale, ScaleTransform.ScaleXProperty, Pulse(0.94, 1.08, 3));
        visual.Animate(scale, ScaleTransform.ScaleYProperty, Pulse(0.94, 1.08, 3));

        AddText(canvas, visual, name, Solid("#FFFFFF"), Solid("#FFFFFF"), Solid("#FFFFFF"), Rgb("#7A1850"));
    }

    private static void ConfettiParty(Grid canvas, WelcomeVisual visual, string name)
    {
        canvas.Background = Radial("#5B2A9E", "#1A0833", 0.5, 0.4, 0.9);

        string[] palette = ["#FF4D6D", "#FFD166", "#06D6A0", "#4CC9F0", "#B388FF", "#FF9F1C"];
        var layer = new Canvas { Width = W, Height = H, IsHitTestVisible = false };
        canvas.Children.Add(layer);

        var rng = new Random();
        for (int i = 0; i < 70; i++)
        {
            double size = rng.Next(14, 30);
            var piece = new Rectangle
            {
                Width = size, Height = size * 0.55,
                Fill = Solid(palette[rng.Next(palette.Length)]),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup { Children = [new RotateTransform(rng.Next(360)), new TranslateTransform()] },
            };
            Canvas.SetLeft(piece, rng.Next(0, (int)W));
            Canvas.SetTop(piece, -60);
            layer.Children.Add(piece);

            var group = (TransformGroup)piece.RenderTransform;
            double seconds = 4 + rng.NextDouble() * 5;
            var fall = new DoubleAnimation(0, H + 140, TimeSpan.FromSeconds(seconds))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(-rng.NextDouble() * seconds), // start mid-fall, not all at the top
            };
            visual.Animate((TranslateTransform)group.Children[1], TranslateTransform.YProperty, fall);
            visual.Animate((RotateTransform)group.Children[0], RotateTransform.AngleProperty,
                new DoubleAnimation(0, rng.Next(0, 2) == 0 ? 360 : -360, TimeSpan.FromSeconds(2 + rng.NextDouble() * 3))
                { RepeatBehavior = RepeatBehavior.Forever });
        }

        AddText(canvas, visual, name, Solid("#FFD166"), Solid("#FFFFFF"), Solid("#FFFFFF"), Rgb("#FF4D6D"));
    }

    private static void DiscoRays(Grid canvas, WelcomeVisual visual, string name)
    {
        canvas.Background = Solid("#0B0618");

        var rays = new Canvas { Width = W, Height = H, IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5) };
        var spin = new RotateTransform();
        rays.RenderTransform = spin;
        string[] colors = ["#FF3CAC", "#784BA0", "#2B86C5", "#00F5A0"];
        const int count = 24;
        for (int i = 0; i < count; i++)
        {
            double a0 = i * 2 * Math.PI / count;
            double a1 = a0 + Math.PI / count;
            const double r = 2000;
            var wedge = new Polygon
            {
                Points = [new Point(W / 2, H / 2),
                          new Point(W / 2 + r * Math.Cos(a0), H / 2 + r * Math.Sin(a0)),
                          new Point(W / 2 + r * Math.Cos(a1), H / 2 + r * Math.Sin(a1))],
                Fill = Solid(colors[i % colors.Length]),
                Opacity = 0.32,
            };
            rays.Children.Add(wedge);
        }
        canvas.Children.Add(rays);
        visual.Animate(spin, RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromSeconds(24)) { RepeatBehavior = RepeatBehavior.Forever });

        // Darkened centre so the text stays readable over the rays.
        canvas.Children.Add(new Ellipse
        {
            Width = 1500, Height = 760,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Fill = Radial("#E6060312", "#00060312", 0.5, 0.5, 0.5),
        });

        AddText(canvas, visual, name, Solid("#00F5A0"), Solid("#FFFFFF"), Solid("#FF9DE2"), Rgb("#FF3CAC"));
    }

    private static void RedCurtain(Grid canvas, WelcomeVisual visual, string name)
    {
        canvas.Background = new LinearGradientBrush(
            [new GradientStop(Rgb("#4A0610"), 0), new GradientStop(Rgb("#B3122A"), 0.5), new GradientStop(Rgb("#4A0610"), 1)],
            0);

        // Velvet folds.
        var folds = new Canvas { Width = W, Height = H, IsHitTestVisible = false };
        for (int x = 0; x < W; x += 160)
        {
            folds.Children.Add(new Rectangle
            {
                Width = 70, Height = H,
                Fill = new LinearGradientBrush(Rgb("#00000000"), Rgb("#66000000"), 0),
                Margin = new Thickness(x, 0, 0, 0),
            });
        }
        canvas.Children.Add(folds);

        // Marquee bulbs around the edge, chasing in two alternating groups.
        var bulbs = new Canvas { Width = W, Height = H, IsHitTestVisible = false };
        canvas.Children.Add(bulbs);
        var points = new List<Point>();
        for (double x = 60; x <= W - 60; x += 80) { points.Add(new Point(x, 50)); points.Add(new Point(x, H - 50)); }
        for (double y = 130; y <= H - 130; y += 80) { points.Add(new Point(50, y)); points.Add(new Point(W - 50, y)); }
        for (int i = 0; i < points.Count; i++)
        {
            var bulb = new Ellipse
            {
                Width = 26, Height = 26,
                Fill = Solid("#FFE08A"),
                Effect = new DropShadowEffect { Color = Rgb("#FFC400"), BlurRadius = 18, ShadowDepth = 0 },
            };
            Canvas.SetLeft(bulb, points[i].X - 13);
            Canvas.SetTop(bulb, points[i].Y - 13);
            bulbs.Children.Add(bulb);
            var blink = new DoubleAnimation(i % 2 == 0 ? 1 : 0.25, i % 2 == 0 ? 0.25 : 1, TimeSpan.FromMilliseconds(700))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            visual.Animate(bulb, UIElement.OpacityProperty, blink);
        }

        AddText(canvas, visual, name, Solid("#FFD76A"), Solid("#FFFFFF"), Solid("#FFE9B0"), Rgb("#000000"));
    }

    #endregion

    /// <summary>
    /// Shown instead of an empty rotation: invites everyone to sign up for tonight's karaoke. Built fresh
    /// each time so the date is current; includes the sign-up QR code when one is available.
    /// </summary>
    public static WelcomeVisual BuildSignUpInvite(
        ImageSource? qr,
        string? url,
        ImageSource? wifiQr = null,
        string? wifiSsid = null,
        string? wifiPassword = null)
    {
        var canvas = new Grid { Width = W, Height = H, ClipToBounds = true };
        canvas.Background = new LinearGradientBrush(
            [new GradientStop(Rgb("#1B0B3A"), 0), new GradientStop(Rgb("#5A1470"), 0.55), new GradientStop(Rgb("#B02A6B"), 1)],
            90);
        var host = new Grid { Background = Brushes.Black };
        host.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = canvas });
        var visual = new WelcomeVisual(host);

        // Music notes drifting up the screen.
        var notes = new Canvas { Width = W, Height = H, IsHitTestVisible = false };
        canvas.Children.Add(notes);
        string[] glyphs = ["♪", "♫", "♩", "♬"];
        var rng = new Random();
        for (int i = 0; i < 16; i++)
        {
            var move = new TranslateTransform();
            var note = new TextBlock
            {
                Text = glyphs[rng.Next(glyphs.Length)],
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = rng.Next(60, 150),
                Foreground = Solid(i % 2 == 0 ? "#FFD76A" : "#FF8AD8"),
                Opacity = 0.28,
                RenderTransform = move,
            };
            Canvas.SetLeft(note, rng.Next(0, (int)W - 100));
            Canvas.SetTop(note, H);
            notes.Children.Add(note);
            double seconds = 9 + rng.NextDouble() * 8;
            visual.Animate(move, TranslateTransform.YProperty, new DoubleAnimation(0, -(H + 220), TimeSpan.FromSeconds(seconds))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(-rng.NextDouble() * seconds), // already mid-flight when shown
            });
        }

        var layout = new Grid { Margin = new Thickness(90, 60, 90, 60) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        bool hasQr = qr != null;
        bool hasWifi = wifiQr != null;
        bool hasAnyQr = hasQr || hasWifi;
        bool hasBothQr = hasQr && hasWifi;

        // QR section uses ~20% of the screen width (384px)
        const double qrColumnWidth = 384;
        if (hasAnyQr)
        {
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(qrColumnWidth) });
        }
        canvas.Children.Add(layout);

        // Left-aligned beside the QR cards; centred across the whole screen when there is no QR code.
        var align = hasAnyQr ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        double bigSize = hasBothQr ? 165 : (hasAnyQr ? 185 : 260);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = align };
        text.Children.Add(new TextBlock
        {
            Text = $"TONIGHT  •  {DateTime.Now:dddd, MMMM d}".ToUpperInvariant(),
            FontFamily = BodyFont, FontSize = 50, Foreground = Solid("#FFD76A"), HorizontalAlignment = align,
            Effect = Glow(Rgb("#FFB400"), 18, 0.7),
        });
        text.Children.Add(new TextBlock
        {
            Text = "KARAOKE", FontFamily = DisplayFont, FontSize = bigSize, HorizontalAlignment = align, Foreground = Brushes.White,
            Margin = new Thickness(0, 10, 0, -40), Effect = Glow(Rgb("#FF3CAC"), 40, 0.95),
        });
        text.Children.Add(new TextBlock
        {
            Text = "TONIGHT!", FontFamily = DisplayFont, FontSize = bigSize, HorizontalAlignment = align, Foreground = Solid("#FFD76A"),
            Effect = Glow(Rgb("#FF8A00"), 40, 0.95),
        });
        text.Children.Add(new TextBlock
        {
            Text = "Sign up now and take the stage!",
            FontFamily = BodyFont, FontSize = 64, Foreground = Brushes.White, HorizontalAlignment = align,
            Margin = new Thickness(0, 26, 0, 0), TextWrapping = TextWrapping.Wrap,
            Effect = Glow(Rgb("#FF3CAC"), 22, 0.8),
        });

        string instructionText;
        if (hasBothQr)
        {
            instructionText = "1. Connect to venue Wi-Fi   •   2. Scan to pick your songs";
        }
        else if (hasQr)
        {
            instructionText = "Scan the QR code or ask the DJ to put you on the list";
        }
        else if (hasWifi)
        {
            instructionText = "Connect to venue Wi-Fi and ask the DJ to sign up";
        }
        else
        {
            instructionText = "Ask the DJ to put you on the list";
        }

        text.Children.Add(new TextBlock
        {
            Text = instructionText,
            FontFamily = BodyFont, FontSize = 42, Foreground = Solid("#FFD9F2"), HorizontalAlignment = align,
            Margin = new Thickness(0, 14, 0, 0), TextWrapping = TextWrapping.Wrap,
        });
        layout.Children.Add(text);

        if (hasAnyQr)
        {
            var rightContainer = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = qrColumnWidth,
                Margin = new Thickness(30, 0, 0, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1),
            };

            if (hasBothQr)
            {
                // Top Card: Wi-Fi Join
                rightContainer.Children.Add(new TextBlock
                {
                    Text = "📶 1. CONNECT WI-FI",
                    FontFamily = DisplayFont,
                    FontSize = 26,
                    Foreground = Solid("#38BDF8"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6),
                    Effect = Glow(Rgb("#38BDF8"), 14, 0.75),
                });
                rightContainer.Children.Add(new Border
                {
                    Width = 240,
                    Height = 240,
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(12),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Effect = Glow(Rgb("#38BDF8"), 26, 0.85),
                    Child = new Image { Source = wifiQr, Stretch = Stretch.Uniform },
                });

                string displaySsid = !string.IsNullOrWhiteSpace(wifiSsid) ? wifiSsid : "Venue Wi-Fi";
                rightContainer.Children.Add(new TextBlock
                {
                    Text = $"Network: {displaySsid}",
                    FontFamily = BodyFont,
                    FontSize = 19,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 6, 0, 2),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = qrColumnWidth,
                });

                string displayPwd = !string.IsNullOrWhiteSpace(wifiPassword) ? wifiPassword : "No Password Required";
                rightContainer.Children.Add(new TextBlock
                {
                    Text = $"Password: {displayPwd}",
                    FontFamily = BodyFont,
                    FontSize = 18,
                    Foreground = Solid("#FFD76A"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = qrColumnWidth,
                });

                // Bottom Card: Song Sign-Up
                rightContainer.Children.Add(new TextBlock
                {
                    Text = "📱 2. SCAN TO SIGN UP",
                    FontFamily = DisplayFont,
                    FontSize = 26,
                    Foreground = Solid("#FF8AD8"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6),
                    Effect = Glow(Rgb("#FF3CAC"), 14, 0.75),
                });
                rightContainer.Children.Add(new Border
                {
                    Width = 240,
                    Height = 240,
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(12),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Effect = Glow(Rgb("#FF3CAC"), 26, 0.85),
                    Child = new Image { Source = qr, Stretch = Stretch.Uniform },
                });

                if (!string.IsNullOrWhiteSpace(url))
                {
                    rightContainer.Children.Add(new TextBlock
                    {
                        Text = url,
                        FontFamily = BodyFont,
                        FontSize = 19,
                        Foreground = Solid("#FFD9F2"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 6, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = qrColumnWidth,
                    });
                }
            }
            else
            {
                // Single QR code on the right side (sign-up or Wi-Fi only)
                var singleImage = qr ?? wifiQr;
                string singleTitle = hasQr ? "SCAN TO SIGN UP" : "CONNECT WI-FI";
                var singleColor = hasQr ? Rgb("#FF3CAC") : Rgb("#38BDF8");

                rightContainer.Children.Add(new Border
                {
                    Width = 360,
                    Height = 360,
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(24),
                    Padding = new Thickness(18),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Effect = Glow(singleColor, 40, 0.9),
                    Child = new Image { Source = singleImage, Stretch = Stretch.Uniform },
                });
                rightContainer.Children.Add(new TextBlock
                {
                    Text = singleTitle,
                    FontFamily = DisplayFont,
                    FontSize = 38,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 18, 0, 0),
                });

                if (hasQr && !string.IsNullOrWhiteSpace(url))
                {
                    rightContainer.Children.Add(new TextBlock
                    {
                        Text = url,
                        FontFamily = BodyFont,
                        FontSize = 24,
                        Foreground = Solid("#FFD9F2"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 6, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = qrColumnWidth,
                    });
                }
                else if (hasWifi)
                {
                    string displaySsid = !string.IsNullOrWhiteSpace(wifiSsid) ? wifiSsid : "Venue Wi-Fi";
                    string displayPwd = !string.IsNullOrWhiteSpace(wifiPassword) ? wifiPassword : "No Password Required";
                    rightContainer.Children.Add(new TextBlock
                    {
                        Text = $"Network: {displaySsid}  •  Password: {displayPwd}",
                        FontFamily = BodyFont,
                        FontSize = 20,
                        Foreground = Solid("#FFD76A"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 8, 0, 0),
                    });
                }
            }

            Grid.SetColumn(rightContainer, 1);
            layout.Children.Add(rightContainer);

            var scale = (ScaleTransform)rightContainer.RenderTransform;
            visual.Animate(scale, ScaleTransform.ScaleXProperty, Pulse(1.0, 1.03, 1.6));
            visual.Animate(scale, ScaleTransform.ScaleYProperty, Pulse(1.0, 1.03, 1.6));
        }

        return visual;
    }

    #region Shared pieces

    /// <summary>Adds the headline, the (auto-fitted) name and the tagline, with a pop-in on the name.</summary>
    private static void AddText(Grid canvas, WelcomeVisual visual, string name,
        Brush titleBrush, Brush nameBrush, Brush taglineBrush, Color glow)
    {
        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 1740,
        };

        stack.Children.Add(new TextBlock
        {
            Text = "WELCOME TO OUR NEW PERFORMER",
            FontFamily = BodyFont, FontSize = 66, Foreground = titleBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = Glow(glow, 22, 0.8),
        });

        // The Viewbox shrinks long names to fit on one line instead of clipping or wrapping.
        var nameText = new TextBlock
        {
            Text = name,
            FontFamily = DisplayFont, FontSize = 210, Foreground = nameBrush,
            TextAlignment = TextAlignment.Center,
            Effect = Glow(glow, 40, 0.95),
        };
        var nameBox = new Viewbox
        {
            Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            MaxWidth = 1740, MaxHeight = 330,
            Margin = new Thickness(0, 24, 0, 24),
            Child = nameText,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1),
        };
        stack.Children.Add(nameBox);

        stack.Children.Add(new TextBlock
        {
            Text = "Give them a big round of applause!",
            FontFamily = BodyFont, FontSize = 56, Foreground = taglineBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = Glow(glow, 18, 0.7),
        });

        canvas.Children.Add(stack);

        var scale = (ScaleTransform)nameBox.RenderTransform;
        var pop = new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(900))
        {
            EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 4, EasingMode = EasingMode.EaseOut },
        };
        visual.Animate(scale, ScaleTransform.ScaleXProperty, pop);
        visual.Animate(scale, ScaleTransform.ScaleYProperty, pop);
    }

    private static DoubleAnimation Pulse(double from, double to, double seconds) =>
        new(from, to, TimeSpan.FromSeconds(seconds))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };

    private static DropShadowEffect Glow(Color color, double blur, double opacity) =>
        new() { Color = color, BlurRadius = blur, ShadowDepth = 0, Opacity = opacity };

    private static RadialGradientBrush Radial(string center, string edge, double cx, double cy, double radius) =>
        new(Rgb(center), Rgb(edge))
        {
            Center = new Point(cx, cy), GradientOrigin = new Point(cx, cy), RadiusX = radius, RadiusY = radius,
        };

    private static Color Rgb(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;

    private static SolidColorBrush Solid(string hex)
    {
        var brush = new SolidColorBrush(Rgb(hex));
        brush.Freeze();
        return brush;
    }

    #endregion
}
