// Created on Sep 24, 2026 @ 11:30:00 -> Shared canvas effects for the Lyracist and KSRotation projection views
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Size = System.Windows.Size;

namespace Lyracist.Shared;

// Decorative, canvas-built effects for the audience projection views. Lyracist's RotationWindow and
// KSRotation's SingerDisplayWindow each host the same themed panels in their own XAML and hand the
// relevant canvases/transforms to these classes, so every effect (and every fix to one) exists once
// instead of being maintained twice. WPF-only: linked into Lyracist and KSRotation, not KSRotation.Maui.
//
// `reduced` parameters implement the DJ's "reduced projection effects" setting for weaker venue PCs:
// fewer particles and no per-element blur effects, with the same overall look.

internal static class ProjectionFx
{
    internal static readonly Random Rng = new();

    internal static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    internal static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    internal static Color Argb(byte a, Color c) => Color.FromArgb(a, c.R, c.G, c.B);

    // Stops the looping Opacity + TranslateTransform animations every drifting/twinkling particle
    // effect below attaches, so cleared particles don't keep ticking in the animation clock.
    internal static void StopDriftAnimations(IEnumerable<UIElement> elements)
    {
        foreach (var elem in elements)
        {
            elem.BeginAnimation(UIElement.OpacityProperty, null);
            if (elem.RenderTransform is TranslateTransform t)
            {
                t.BeginAnimation(TranslateTransform.XProperty, null);
                t.BeginAnimation(TranslateTransform.YProperty, null);
            }
        }
    }

    internal static DoubleAnimation Loop(double from, double to, double seconds, double beginSeconds, bool autoReverse = true) =>
        new(from, to, TimeSpan.FromSeconds(seconds))
        {
            AutoReverse = autoReverse,
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromSeconds(beginSeconds)
        };

    // 8-point glint star polygon used by the slot coins and the jackpot burst.
    internal static Polygon Glint(double size, double pinch, Brush fill) => new()
    {
        Points =
        [
            new Point(size / 2, 0),
            new Point(size * (1 - pinch), size * pinch),
            new Point(size, size / 2),
            new Point(size * (1 - pinch), size * (1 - pinch)),
            new Point(size / 2, size),
            new Point(size * pinch, size * (1 - pinch)),
            new Point(0, size / 2),
            new Point(size * pinch, size * pinch)
        ],
        Fill = fill
    };
}

// ── Star Wars crawl starfield ────────────────────────────────────────────────────────────────────
internal static class StarfieldEffect
{
    // Rebuilds the static starfield behind the crawl: a few soft galaxies, then tiered stars.
    // Bright stars get a radial-gradient halo ellipse rather than a DropShadowEffect - with a few
    // dozen bright stars twinkling, per-star blur shaders were re-rendered every frame.
    public static void Regenerate(Canvas canvas, double width, double height, bool reduced)
    {
        canvas.Children.Clear();
        if (width <= 0 || height <= 0) return;

        GenerateGalaxies(canvas, width, height);

        int starCount = reduced
            ? (int)Math.Clamp(width * height / 14000.0, 60, 150)
            : (int)Math.Clamp(width * height / 7000.0, 100, 300);
        var rng = ProjectionFx.Rng;

        for (int i = 0; i < starCount; i++)
        {
            double sz;
            double op;
            double roll = rng.NextDouble();

            // Three brightness/size tiers mimic a real night sky: lots of faint pinpricks, some
            // medium stars, and a few large bright ones that anchor the field.
            if (roll < 0.70)
            {
                sz = (rng.NextDouble() * 1.1) + 0.7;
                op = (rng.NextDouble() * 0.3) + 0.25;
            }
            else if (roll < 0.93)
            {
                sz = (rng.NextDouble() * 1.6) + 1.8;
                op = (rng.NextDouble() * 0.3) + 0.6;
            }
            else
            {
                sz = (rng.NextDouble() * 3.7) + 3.5;
                op = (rng.NextDouble() * 0.2) + 0.8;
            }

            bool isBright = roll >= 0.93;
            Color tint = PickStarTint();

            var star = new Ellipse { Width = sz, Height = sz, Fill = ProjectionFx.Frozen(tint), Opacity = op };
            double x = rng.NextDouble() * width;
            double y = rng.NextDouble() * height;
            Canvas.SetLeft(star, x);
            Canvas.SetTop(star, y);
            canvas.Children.Add(star);

            // Reduced mode keeps the field static apart from the bright stars.
            double twinkleChance = isBright ? 0.6 : (reduced ? 0 : 0.25);
            bool animateTwinkle = rng.NextDouble() < twinkleChance;

            if (isBright)
            {
                var glow = new Ellipse
                {
                    Width = sz * 3.5,
                    Height = sz * 3.5,
                    Fill = ProjectionFx.Frozen(new RadialGradientBrush
                    {
                        GradientStops =
                        {
                            new GradientStop(ProjectionFx.Argb(180, tint), 0.0),
                            new GradientStop(ProjectionFx.Argb(0, tint), 1.0)
                        }
                    })
                };
                Canvas.SetLeft(glow, x - (sz * 1.25));
                Canvas.SetTop(glow, y - (sz * 1.25));
                canvas.Children.Add(glow);

                if (animateTwinkle)
                {
                    glow.BeginAnimation(UIElement.OpacityProperty,
                        ProjectionFx.Loop(0.8, 0.15, (rng.NextDouble() * 2.5) + 0.8, rng.NextDouble() * 5));
                }
            }

            if (animateTwinkle)
            {
                star.BeginAnimation(UIElement.OpacityProperty,
                    ProjectionFx.Loop(op, op * 0.2, (rng.NextDouble() * 2.5) + 0.8, rng.NextDouble() * 5));
            }
        }
    }

    private static Color PickStarTint()
    {
        double tint = ProjectionFx.Rng.NextDouble();
        if (tint < 0.25) return Color.FromRgb(0xCF, 0xDD, 0xFF); // cool blue-white
        if (tint < 0.45) return Color.FromRgb(0xFF, 0xF2, 0xD8); // warm amber-white
        return Colors.White;
    }

    private static void GenerateGalaxies(Canvas canvas, double w, double h)
    {
        var rng = ProjectionFx.Rng;
        int count = rng.Next(3, 6);

        for (int i = 0; i < count; i++)
        {
            // Elongated elliptical disk; the width/height ratio + rotation suggest an inclined galaxy.
            double gw = (rng.NextDouble() * 180) + 130;
            double gh = gw * ((rng.NextDouble() * 0.30) + 0.32);

            (Color core, Color mid) = PickGalaxyPalette();

            var halo = new Ellipse
            {
                Width = gw,
                Height = gh,
                Fill = ProjectionFx.Frozen(new RadialGradientBrush
                {
                    GradientStops =
                    {
                        new GradientStop(core, 0.0),
                        new GradientStop(mid, 0.4),
                        new GradientStop(ProjectionFx.Argb(0, mid), 1.0)
                    }
                })
            };

            double coreSize = gh * 0.45;
            var coreGlow = new Ellipse
            {
                Width = coreSize,
                Height = coreSize,
                Fill = GalaxyCoreBrush
            };
            Canvas.SetLeft(coreGlow, (gw - coreSize) / 2);
            Canvas.SetTop(coreGlow, (gh - coreSize) / 2);

            var galaxy = new Canvas
            {
                Width = gw,
                Height = gh,
                Opacity = (rng.NextDouble() * 0.25) + 0.4,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(rng.NextDouble() * 360)
            };
            galaxy.Children.Add(halo);
            galaxy.Children.Add(coreGlow);

            Canvas.SetLeft(galaxy, rng.NextDouble() * Math.Max(1, w - gw));
            Canvas.SetTop(galaxy, rng.NextDouble() * Math.Max(1, h - gh));
            canvas.Children.Add(galaxy);
        }
    }

    private static readonly RadialGradientBrush GalaxyCoreBrush = ProjectionFx.Frozen(new RadialGradientBrush
    {
        GradientStops =
        {
            new GradientStop(Color.FromArgb(220, 255, 255, 255), 0.0),
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0)
        }
    });

    private static (Color core, Color mid) PickGalaxyPalette() => ProjectionFx.Rng.Next(4) switch
    {
        0 => (Color.FromArgb(180, 210, 220, 255), Color.FromArgb(75, 90, 120, 210)),
        1 => (Color.FromArgb(180, 255, 220, 245), Color.FromArgb(75, 170, 90, 180)),
        2 => (Color.FromArgb(180, 215, 255, 245), Color.FromArgb(75, 70, 170, 150)),
        _ => (Color.FromArgb(180, 255, 240, 215), Color.FromArgb(75, 200, 150, 90)),
    };
}

// ── Rotating light wedges (Disco Ball beams, Festival stage lights) ─────────────────────────────
internal sealed class RotatingBeamsEffect(Canvas canvas)
{
    private static readonly Color[] DiscoPalette =
    [
        Color.FromRgb(0xFF, 0x2F, 0xE0),
        Color.FromRgb(0x33, 0xD4, 0xFF),
        Color.FromRgb(0x9B, 0x5C, 0xFF),
        Color.FromRgb(0xFF, 0xD8, 0x4D),
        Color.FromRgb(0x4C, 0xFF, 0xB0),
        Color.FromRgb(0xFF, 0x8A, 0x3D),
    ];

    private static readonly Color[] FestivalPalette =
    [
        Color.FromRgb(0xFF, 0xF0, 0xC8),
        Color.FromRgb(0xFF, 0xC2, 0x4A),
        Color.FromRgb(0xFF, 0x4A, 0x4A),
        Color.FromRgb(0xFF, 0x8A, 0x3D),
        Color.FromRgb(0xFF, 0xD8, 0x4D),
        Color.FromRgb(0xFF, 0x6B, 0x9E),
    ];

    /// <summary>Colored spotlight wedges fanned out downward from the disco ball. The ball sits at a
    /// slightly different height in each app's layout, hence the anchor parameter.</summary>
    public void BuildDisco(double ballAnchorYFraction) =>
        Build(DiscoPalette, ballAnchorYFraction, lengthFactor: 1.15, pointUp: false, spreadBase: 34, spreadAlt: 6, startAlpha: 0x55, durationBase: 9, durationStep: 2.3);

    /// <summary>Warm stage-light wedges fanned upward from below the festival poster.</summary>
    public void BuildFestival() =>
        Build(FestivalPalette, anchorYFraction: 1.0, lengthFactor: 1.1, pointUp: true, spreadBase: 30, spreadAlt: 8, startAlpha: 0x50, durationBase: 10, durationStep: 2.1);

    private readonly List<RotateTransform> _rotates = [];

    // Independently rotating translucent wedges fanned out from an anchor point - alternating spin
    // direction/duration per beam is what makes the sweep read as chaotic stage lighting rather than
    // one synchronized rotation. `pointUp` flips the wedge so its apex (and pivot) is at the bottom.
    public void Build(Color[] palette, double anchorYFraction, double lengthFactor, bool pointUp,
        double spreadBase, double spreadAlt, byte startAlpha, double durationBase, double durationStep)
    {
        Stop();

        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double anchorX = w / 2.0;
        double anchorY = h * anchorYFraction;
        double length = h * lengthFactor;
        double tip = pointUp ? -length : length;

        for (int i = 0; i < palette.Length; i++)
        {
            double spreadHalf = spreadBase + (i % 2 == 0 ? spreadAlt : 0);
            var color = palette[i];

            // Points are relative to the apex (0,0); RenderTransformOrigin puts the pivot exactly on
            // the apex so the wedge sweeps around the light source, not around its own centroid.
            var polygon = new Polygon
            {
                Points = [new Point(0, 0), new Point(-spreadHalf, tip), new Point(spreadHalf, tip)],
                RenderTransformOrigin = new Point(0.5, pointUp ? 1.0 : 0.0),
                Fill = ProjectionFx.Frozen(new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, pointUp ? 1 : 0),
                    EndPoint = new Point(0.5, pointUp ? 0 : 1),
                    GradientStops =
                    {
                        new GradientStop(ProjectionFx.Argb(startAlpha, color), 0.0),
                        new GradientStop(ProjectionFx.Argb(0x00, color), 1.0)
                    }
                })
            };

            var rotate = new RotateTransform((360.0 / palette.Length) * i);
            polygon.RenderTransform = rotate;

            Canvas.SetLeft(polygon, anchorX - spreadHalf);
            Canvas.SetTop(polygon, pointUp ? anchorY - length : anchorY);
            canvas.Children.Add(polygon);
            _rotates.Add(rotate);

            bool clockwise = i % 2 == 0;
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(
                rotate.Angle,
                rotate.Angle + (clockwise ? 360 : -360),
                TimeSpan.FromSeconds(durationBase + (i * durationStep)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            });
        }
    }

    public void Stop()
    {
        foreach (var rotate in _rotates)
        {
            rotate.BeginAnimation(RotateTransform.AngleProperty, null);
        }
        canvas.Children.Clear();
        _rotates.Clear();
    }
}

// ── Drifting, twinkling dots (Disco light spots, Festival night sky) ────────────────────────────
internal sealed record TwinklingDotsOptions(
    Color[] Palette,
    double AreaPerDot, int MinDots, int MaxDots,
    double SizeMin, double SizeRange,
    double AmpMin, double AmpRange,
    double DriftMin, double DriftRange,
    double TwinkleMin, double TwinkleRange,
    double OpacityLow, double OpacityHigh,
    double HeightFraction, double BeginRange);

internal sealed class TwinklingDotsEffect(Canvas canvas)
{
    private readonly List<UIElement> _dots = [];

    // Small colored dots wobbling and twinkling on independent randomized loops - the same
    // technique as the crawl's starfield twinkle, just recolored/respaced per view.
    public void Build(TwinklingDotsOptions o, bool reduced)
    {
        Stop();

        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var rng = ProjectionFx.Rng;
        int count = (int)Math.Clamp(w * h / o.AreaPerDot, o.MinDots, o.MaxDots);
        if (reduced) count = Math.Max(6, count / 2);

        for (int i = 0; i < count; i++)
        {
            double size = (rng.NextDouble() * o.SizeRange) + o.SizeMin;
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = ProjectionFx.Frozen(o.Palette[rng.Next(o.Palette.Length)]),
                RenderTransform = new TranslateTransform()
            };
            Canvas.SetLeft(dot, rng.NextDouble() * w);
            Canvas.SetTop(dot, rng.NextDouble() * h * o.HeightFraction);
            canvas.Children.Add(dot);
            _dots.Add(dot);

            var transform = (TranslateTransform)dot.RenderTransform;
            double ampX = (rng.NextDouble() * o.AmpRange) + o.AmpMin;
            double ampY = (rng.NextDouble() * o.AmpRange) + o.AmpMin;
            double begin = rng.NextDouble() * o.BeginRange;

            transform.BeginAnimation(TranslateTransform.XProperty, ProjectionFx.Loop(-ampX, ampX, (rng.NextDouble() * o.DriftRange) + o.DriftMin, begin));
            transform.BeginAnimation(TranslateTransform.YProperty, ProjectionFx.Loop(-ampY, ampY, (rng.NextDouble() * o.DriftRange) + o.DriftMin, begin));
            dot.BeginAnimation(UIElement.OpacityProperty, ProjectionFx.Loop(o.OpacityLow, o.OpacityHigh, (rng.NextDouble() * o.TwinkleRange) + o.TwinkleMin, begin));
        }
    }

    public void Stop()
    {
        ProjectionFx.StopDriftAnimations(_dots);
        canvas.Children.Clear();
        _dots.Clear();
    }

    public static readonly TwinklingDotsOptions DiscoLightSpots = new(
        [
            Color.FromRgb(0xFF, 0x6B, 0xE8),
            Color.FromRgb(0x6B, 0xD4, 0xFF),
            Color.FromRgb(0xC1, 0x8B, 0xFF),
            Color.FromRgb(0xFF, 0xE0, 0x6B),
            Color.FromRgb(0x6B, 0xFF, 0xC4),
        ],
        AreaPerDot: 26000, MinDots: 14, MaxDots: 36,
        SizeMin: 3, SizeRange: 5,
        AmpMin: 20, AmpRange: 60,
        DriftMin: 3, DriftRange: 3,
        TwinkleMin: 1.2, TwinkleRange: 1.5,
        OpacityLow: 0.15, OpacityHigh: 0.95,
        HeightFraction: 1.0, BeginRange: 4);

    public static readonly TwinklingDotsOptions FestivalSparkles = new(
        [
            Color.FromRgb(0xFF, 0xF3, 0xB0),
            Color.FromRgb(0xFF, 0xE0, 0x6B),
            Color.FromRgb(0xFF, 0xFF, 0xFF),
            Color.FromRgb(0xFF, 0xC2, 0x4A),
        ],
        AreaPerDot: 40000, MinDots: 12, MaxDots: 28,
        SizeMin: 2, SizeRange: 4,
        AmpMin: 5, AmpRange: 20,
        DriftMin: 4, DriftRange: 4,
        TwinkleMin: 1.5, TwinkleRange: 2,
        OpacityLow: 0.1, OpacityHigh: 0.9,
        HeightFraction: 0.6, BeginRange: 5);
}

// ── Synthwave grid ───────────────────────────────────────────────────────────────────────────────
internal sealed class SynthwaveGridEffect(Canvas canvas, Func<bool> isActive)
{
    private static readonly Color[] LineColors = [Color.FromRgb(0x33, 0xD4, 0xFF), Color.FromRgb(0xFF, 0x2F, 0xE0)];
    private static readonly SolidColorBrush[] LineBrushes = [.. LineColors.Select(c => ProjectionFx.Frozen(c))];
    private static readonly DropShadowEffect[] LineGlows =
        [.. LineColors.Select(c => ProjectionFx.Frozen(new DropShadowEffect { Color = c, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.8 }))];
    private static readonly SolidColorBrush[] RayBrushes = [.. LineColors.Select(c => ProjectionFx.Frozen(ProjectionFx.Argb(0x55, c)))];

    private DispatcherTimer? _timer;
    private int _colorIndex;
    private bool _reduced;

    /// <summary>Static fan of converging lines from the horizon's vanishing point out to the bottom
    /// edge - the fixed "rails" of the perspective floor. Rebuilt on resize.</summary>
    public void BuildStatic()
    {
        canvas.Children.Clear();

        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double vanishX = w / 2.0;
        double vanishY = h * 0.42;
        const int rayCount = 15;

        for (int i = 0; i <= rayCount; i++)
        {
            double t = (double)i / rayCount;
            canvas.Children.Add(new Line
            {
                X1 = vanishX,
                Y1 = vanishY,
                X2 = (-0.15 * w) + (t * (1.3 * w)),
                Y2 = h,
                StrokeThickness = 1.5,
                Stroke = RayBrushes[i % 2]
            });
        }
    }

    /// <summary>Spawns a horizontal grid line at the horizon every tick and animates it racing toward
    /// the viewer (widening and accelerating via an ease-in curve, fading out near the bottom).</summary>
    public void Start(bool reduced)
    {
        Stop();
        _reduced = reduced;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _timer.Tick += (_, _) => SpawnLine();
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        canvas.Children.Clear();
    }

    private void SpawnLine()
    {
        if (!isActive()) return;

        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double vanishX = w / 2.0;
        double horizonY = h * 0.42;
        double maxWidth = w * 1.1;
        int colorSlot = _colorIndex++ % LineColors.Length;

        // Full-width line whose growth and travel are render transforms (scale X around its center,
        // translate Y) instead of animated Width/Canvas.Left/Canvas.Top - those are layout
        // properties, so animating them re-measured and re-arranged every line on every frame.
        var rect = new Rectangle
        {
            Width = maxWidth,
            Height = 2,
            Fill = LineBrushes[colorSlot],
            Effect = _reduced ? null : LineGlows[colorSlot],
            Opacity = 0
        };
        Canvas.SetLeft(rect, vanishX - (maxWidth / 2.0));
        Canvas.SetTop(rect, 0);
        var grow = new ScaleTransform(0, 1, maxWidth / 2.0, 0);
        var travel = new TranslateTransform(0, horizonY);
        var transforms = new TransformGroup();
        transforms.Children.Add(grow);
        transforms.Children.Add(travel);
        rect.RenderTransform = transforms;
        canvas.Children.Add(rect);

        var duration = TimeSpan.FromSeconds(2.2);
        var ease = new PowerEase { EasingMode = EasingMode.EaseIn, Power = 2.5 };
        grow.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        travel.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(horizonY, h, duration) { EasingFunction = ease });

        var fade = new DoubleAnimationUsingKeyFrames { Duration = duration };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(0.12)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(0.8)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));
        fade.Completed += (_, _) => canvas.Children.Remove(rect);
        rect.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}

// ── Vegas Marquee bulb chase ─────────────────────────────────────────────────────────────────────
internal sealed class MarqueeBulbChase(Canvas canvas)
{
    private const double BulbSize = 26;
    private const double BulbSpacing = 54;
    private const double GlowScale = 1.8;
    private const int LitPeriod = 3;        // every Nth bulb is lit at any moment
    private const double DimOpacity = 0.18;

    // Bulb core + halo in one frozen brush. The ellipse is drawn GlowScale times the bulb's size; the
    // stops out to `core` (the bulb's own radius) are the lit bulb, and the rest fades the glow color
    // out to transparent. Replaces a per-bulb DropShadowEffect: ~100 blur shaders re-rendered every
    // time the chase pattern flipped a bulb's opacity.
    private static readonly RadialGradientBrush BulbBrush = CreateBulbBrush();

    private static RadialGradientBrush CreateBulbBrush()
    {
        var glow = Color.FromRgb(0xA8, 0x55, 0xF7);
        const double core = 1.0 / GlowScale;
        return ProjectionFx.Frozen(new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xF3, 0xE8, 0xFF), 0.0),
                new GradientStop(Color.FromRgb(0xA8, 0x55, 0xF7), core * 0.55),
                new GradientStop(Color.FromRgb(0x7C, 0x3A, 0xED), core),
                new GradientStop(ProjectionFx.Argb(0xB0, glow), core + 0.02),
                new GradientStop(ProjectionFx.Argb(0x40, glow), core + ((1 - core) * 0.4)),
                new GradientStop(ProjectionFx.Argb(0x00, glow), 1.0)
            }
        });
    }

    private readonly List<Ellipse> _bulbs = [];
    private DispatcherTimer? _timer;
    private int _step;

    /// <summary>Lays bulbs around the canvas perimeter and starts a "marching ants" chase.</summary>
    public void Build()
    {
        Stop();

        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        const double inset = BulbSize / 2; // center bulbs on the frame edge
        const double glow = BulbSize * GlowScale;

        // Walk the perimeter clockwise so the lit pattern marches smoothly around the loop.
        var positions = new List<Point>();
        for (double x = inset; x <= w - inset; x += BulbSpacing) positions.Add(new(x, inset));
        for (double y = inset + BulbSpacing; y <= h - inset; y += BulbSpacing) positions.Add(new(w - inset, y));
        for (double x = w - inset - BulbSpacing; x >= inset; x -= BulbSpacing) positions.Add(new(x, h - inset));
        for (double y = h - inset - BulbSpacing; y >= inset + BulbSpacing; y -= BulbSpacing) positions.Add(new(inset, y));
        if (positions.Count == 0) return;

        foreach (var pos in positions)
        {
            var bulb = new Ellipse { Width = glow, Height = glow, Fill = BulbBrush };
            Canvas.SetLeft(bulb, pos.X - (glow / 2));
            Canvas.SetTop(bulb, pos.Y - (glow / 2));
            canvas.Children.Add(bulb);
            _bulbs.Add(bulb);
        }

        _step = 0;
        ApplyPattern();
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(110) };
        _timer.Tick += (_, _) =>
        {
            _step++;
            ApplyPattern();
        };
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        canvas.Children.Clear();
        _bulbs.Clear();
    }

    private void ApplyPattern()
    {
        for (int i = 0; i < _bulbs.Count; i++)
        {
            // Subtract the step so the lit bulbs march clockwise (in position order).
            bool lit = (((i - _step) % LitPeriod) + LitPeriod) % LitPeriod == 0;
            _bulbs[i].Opacity = lit ? 1.0 : DimOpacity;
        }
    }
}

// ── Casino Slot Reels ────────────────────────────────────────────────────────────────────────────
internal sealed class SlotMachineEffect
{
    // Each reel strip holds its 7-symbol set duplicated back-to-back (14 rows of 140px = 980px one
    // full set). Scroll position is driven by elapsed wall-clock time modulo the set height (via
    // CompositionTarget.Rendering rather than a Timeline with RepeatBehavior.Forever), so the spin
    // loops seamlessly with no Timeline wrap instant to hitch at, and all three reels spin at the
    // same speed. After a staggered free-spin window (left reel stops first, like a real cabinet)
    // each reel eases to a stop with the target symbol resting on the payline.
    private const double SymbolHeight = 140;
    private const double SetHeight = SymbolHeight * 7; // 980 - one full 7-symbol set
    private const double PaylineY = 95; // center of the 190px-tall reel window (matches the XAML arrow markers)
    private const double SpinPxPerSecond = 500;

    // Fixed duration for every reel's landing glide. Deriving it from the remaining distance (which
    // is essentially random, 0-980px) made some landings take over seven seconds.
    private const double LandingDurationSeconds = 1.0;

    // Index of each landing symbol within each reel's strip, per the symbol order in the XAML:
    // Reel 1 = 🎤 🍒 🔔 💎 7️⃣ ⭐ 🍋, Reel 2 = 💎 ⭐ 🎤 🍋 🔔 7️⃣ 🍒, Reel 3 = ⭐ 🍋 💎 🍒 🎤 🔔 7️⃣.
    private static readonly int[] DiamondIndices = [3, 0, 2];
    private static readonly int[] SevenIndices = [4, 5, 6];

    private static readonly Color[] ParticlePalette =
    [
        Color.FromRgb(0xFF, 0xF2, 0xA3),
        Color.FromRgb(0xFF, 0xD7, 0x00),
        Color.FromRgb(0xFF, 0xC4, 0x00),
        Color.FromRgb(0xFF, 0xA5, 0x00),
        Color.FromRgb(0xFF, 0xFF, 0xFF),
    ];
    private static readonly SolidColorBrush[] ParticleBrushes = [.. ParticlePalette.Select(c => ProjectionFx.Frozen(c))];
    private static readonly SolidColorBrush RingBrush = ProjectionFx.Frozen(Color.FromRgb(0xFF, 0xD7, 0x00));
    private static readonly SolidColorBrush BannerTextBrush = ProjectionFx.Frozen(Color.FromRgb(0xFF, 0xD7, 0x00));
    private static readonly SolidColorBrush BannerSubTextBrush = ProjectionFx.Frozen(Color.FromRgb(0xFF, 0xF2, 0xA3));
    private static readonly SolidColorBrush BannerBackBrush = ProjectionFx.Frozen(Color.FromArgb(0xD0, 0x1A, 0x04, 0x04));
    private static readonly DropShadowEffect BannerGlow = ProjectionFx.Frozen(new DropShadowEffect { Color = Color.FromRgb(0xFF, 0xD7, 0x00), BlurRadius = 18, ShadowDepth = 0, Opacity = 0.9 });

    private static double NormalizedMod(double value, double modulus) => ((value % modulus) + modulus) % modulus;

    // The distance-mod-980 at which a symbol at `index` sits centered on the payline.
    private static double LandingMod(int index) =>
        NormalizedMod(index * SymbolHeight + SymbolHeight / 2 - PaylineY, SetHeight);

    private sealed class ReelState
    {
        public TranslateTransform Translate = null!;
        public double SpinSeconds;
        public double LandingDistanceMod;
        public readonly Stopwatch Clock = new();
        public bool IsLanding;
        public bool IsLanded;
        public double LandingFromDistance;
        public double LandingToDistance;
    }

    private readonly TranslateTransform[] _reelTransforms;
    private readonly Canvas _burstCanvas;
    private readonly DropShadowEffect _jackpotGlow;
    private readonly Canvas _coinCanvas;
    private readonly List<UIElement> _coins = [];

    private ReelState[]? _reels;
    private bool _renderingHooked;
    private bool _celebrated;
    private bool _landOnSevens;
    private bool _reduced;
    private DispatcherTimer? _cleanupTimer;

    public SlotMachineEffect(TranslateTransform reel1, TranslateTransform reel2, TranslateTransform reel3,
        Canvas burstCanvas, DropShadowEffect jackpotGlow, Canvas coinCanvas)
    {
        _reelTransforms = [reel1, reel2, reel3];
        _burstCanvas = burstCanvas;
        _jackpotGlow = jackpotGlow;
        _coinCanvas = coinCanvas;
    }

    /// <summary>Spins all three reels and lands them on 💎 - or on 7️⃣ when <paramref name="landOnSevens"/>
    /// (the rotation's anchor singer, i.e. a new round, is up) - then fires the celebration.</summary>
    public void Spin(bool landOnSevens, bool reduced)
    {
        _landOnSevens = landOnSevens;
        _reduced = reduced;
        int[] indices = landOnSevens ? SevenIndices : DiamondIndices;
        _reels = [.. _reelTransforms.Select((t, i) => new ReelState
        {
            Translate = t,
            SpinSeconds = 3.0 + i,
            LandingDistanceMod = LandingMod(indices[i])
        })];
        foreach (var reel in _reels)
        {
            reel.Clock.Restart();
        }

        _celebrated = false;
        _cleanupTimer?.Stop();
        _burstCanvas.Children.Clear();

        if (!_renderingHooked)
        {
            CompositionTarget.Rendering += OnRendering;
            _renderingHooked = true;
        }

        // An endlessly animated BlurRadius re-renders the marquee title's shader every frame; reduced
        // mode leaves the glow static.
        if (reduced)
        {
            _jackpotGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
        }
        else
        {
            _jackpotGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty,
                new DoubleAnimation(18, 40, TimeSpan.FromSeconds(0.75)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        }
    }

    public void Stop()
    {
        StopRendering();
        _reels = null;
        _cleanupTimer?.Stop();
        _burstCanvas.Children.Clear();
        _jackpotGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
    }

    // Runs on every rendered frame while any reel is still moving. Once a reel's free-spin window
    // elapses it eases from its current distance to a further distance whose modulo lands the target
    // symbol on the payline - taking the modulo of the eased distance (rather than lerping the
    // on-screen Y) so the deceleration still passes through any symbol-set wrap without a pop.
    private void OnRendering(object? sender, EventArgs e)
    {
        if (_reels == null) return;

        bool allLanded = true;
        foreach (var reel in _reels)
        {
            if (reel.IsLanded) continue;

            double elapsed = reel.Clock.Elapsed.TotalSeconds;
            if (!reel.IsLanding && elapsed >= reel.SpinSeconds)
            {
                double distanceAtStop = reel.SpinSeconds * SpinPxPerSecond;
                double forward = NormalizedMod(reel.LandingDistanceMod - NormalizedMod(distanceAtStop, SetHeight), SetHeight);
                reel.IsLanding = true;
                reel.LandingFromDistance = distanceAtStop;
                reel.LandingToDistance = distanceAtStop + forward;
            }

            double distance;
            if (reel.IsLanding)
            {
                double t = (elapsed - reel.SpinSeconds) / LandingDurationSeconds;
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
                distance = elapsed * SpinPxPerSecond;
            }

            reel.Translate.Y = -NormalizedMod(distance, SetHeight);
            if (!reel.IsLanded) allLanded = false;
        }

        if (allLanded)
        {
            if (!_celebrated)
            {
                _celebrated = true;
                Celebrate();
            }
            StopRendering();
        }
    }

    private void StopRendering()
    {
        if (_renderingHooked)
        {
            CompositionTarget.Rendering -= OnRendering;
            _renderingHooked = false;
        }
    }

    // One-shot sparkle burst, expanding shockwave ring, and "JACKPOT!" / "LUCKY 7s" banner flash,
    // fired from the center of the reel row once all three symbols land. The burst canvas clips to
    // the reel row so nothing spills onto the surrounding banner. Spin() clears it (and stops the
    // cleanup timer) before the next spin, so it never lingers or double-fires.
    private void Celebrate()
    {
        _burstCanvas.Children.Clear();

        double w = _burstCanvas.ActualWidth;
        double h = _burstCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double centerX = w / 2;
        double centerY = h / 2;
        var rng = ProjectionFx.Rng;

        // Width/Height/Canvas.Left/Top are animated directly (not a RenderTransform scale) so the 4px
        // stroke stays a crisp thin ring - a ScaleTransform would scale the stroke thickness too.
        var ring = new Ellipse { Width = 20, Height = 20, Stroke = RingBrush, StrokeThickness = 4 };
        Canvas.SetLeft(ring, centerX - 10);
        Canvas.SetTop(ring, centerY - 10);
        _burstCanvas.Children.Add(ring);

        const double ringFinalDiameter = 220;
        var ringDuration = TimeSpan.FromSeconds(0.8);
        var ringEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        ring.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(20, ringFinalDiameter, ringDuration) { EasingFunction = ringEase });
        ring.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(20, ringFinalDiameter, ringDuration) { EasingFunction = ringEase });
        ring.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(centerX - 10, centerX - ringFinalDiameter / 2, ringDuration) { EasingFunction = ringEase });
        ring.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(centerY - 10, centerY - ringFinalDiameter / 2, ringDuration) { EasingFunction = ringEase });
        ring.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.9, 0, ringDuration));

        int count = _reduced ? 18 : 36;
        for (int i = 0; i < count; i++)
        {
            double angle = rng.NextDouble() * Math.PI * 2;
            double distance = (rng.NextDouble() * 50) + 50;
            double size = (rng.NextDouble() * 10) + 8;
            var brush = ParticleBrushes[rng.Next(ParticleBrushes.Length)];

            Shape particle = i % 2 == 0
                ? new Ellipse { Width = size, Height = size, Fill = brush }
                : ProjectionFx.Glint(size, 0.4, brush);
            Canvas.SetLeft(particle, centerX - size / 2);
            Canvas.SetTop(particle, centerY - size / 2);

            var translate = new TranslateTransform();
            particle.RenderTransform = translate;
            _burstCanvas.Children.Add(particle);

            double duration = (rng.NextDouble() * 0.5) + 0.9;
            var burstEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, Math.Cos(angle) * distance, TimeSpan.FromSeconds(duration)) { EasingFunction = burstEase });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, Math.Sin(angle) * distance, TimeSpan.FromSeconds(duration)) { EasingFunction = burstEase });
            particle.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromSeconds(duration * 0.6)) { BeginTime = TimeSpan.FromSeconds(duration * 0.4) });
        }

        AddBanner(w, h);

        _cleanupTimer?.Stop();
        _cleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        _cleanupTimer.Tick += (_, _) =>
        {
            _burstCanvas.Children.Clear();
            _cleanupTimer?.Stop();
        };
        _cleanupTimer.Start();
    }

    // Pops a "JACKPOT!" (or "LUCKY 7s! / NEW ROUND" for the anchor singer) pill over the payline:
    // scales in with a slight overshoot, holds, then fades out before the cleanup timer clears it.
    private void AddBanner(double w, double h)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = _landOnSevens ? "LUCKY 7s!" : "JACKPOT!",
            FontSize = 44,
            FontWeight = FontWeights.Black,
            Foreground = BannerTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = _reduced ? null : BannerGlow
        });
        if (_landOnSevens)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "⚓ NEW ROUND ⚓",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = BannerSubTextBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }

        var pill = new Border
        {
            Background = BannerBackBrush,
            BorderBrush = RingBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(22, 4, 22, 6),
            Child = stack,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Opacity = 0
        };
        pill.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(pill, (w - pill.DesiredSize.Width) / 2);
        Canvas.SetTop(pill, (h - pill.DesiredSize.Height) / 2);

        var scale = new ScaleTransform(0.2, 0.2);
        pill.RenderTransform = scale;
        _burstCanvas.Children.Add(pill);

        var pop = new DoubleAnimation(0.2, 1.0, TimeSpan.FromSeconds(0.35))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);

        var fade = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(2.0) };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0.08)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0.75)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));
        pill.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>Floating gold coins and glints drifting over the casino backdrop.</summary>
    public void BuildCoins(bool reduced)
    {
        StopCoins();

        double w = _coinCanvas.ActualWidth;
        double h = _coinCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var rng = ProjectionFx.Rng;
        int count = reduced ? 12 : 28;
        for (int i = 0; i < count; i++)
        {
            double size = (rng.NextDouble() * 14) + 10;
            var color = ParticlePalette[i % ParticlePalette.Length];

            Shape particle = i % 2 == 0
                ? new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = CoinBrushes[i % CoinBrushes.Length]
                }
                : ProjectionFx.Glint(size, 0.35, ProjectionFx.Frozen(ProjectionFx.Argb(0xCC, color)));

            Canvas.SetLeft(particle, rng.NextDouble() * w);
            Canvas.SetTop(particle, rng.NextDouble() * h);
            var translate = new TranslateTransform();
            particle.RenderTransform = translate;

            double driftY = -((rng.NextDouble() * 80) + 40);
            double driftX = (rng.NextDouble() * 40) - 20;
            double duration = (rng.NextDouble() * 2.5) + 2.0;
            double begin = rng.NextDouble() * 2.0;

            translate.BeginAnimation(TranslateTransform.YProperty, ProjectionFx.Loop(0, driftY, duration, begin));
            translate.BeginAnimation(TranslateTransform.XProperty, ProjectionFx.Loop(-driftX, driftX, duration * 1.3, begin));
            particle.BeginAnimation(UIElement.OpacityProperty, ProjectionFx.Loop(0.2, 0.95, duration, begin));

            _coinCanvas.Children.Add(particle);
            _coins.Add(particle);
        }
    }

    public void StopCoins()
    {
        ProjectionFx.StopDriftAnimations(_coins);
        _coinCanvas.Children.Clear();
        _coins.Clear();
    }

    private static readonly RadialGradientBrush[] CoinBrushes =
        [.. ParticlePalette.Select(c => ProjectionFx.Frozen(new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF), 0.0),
                new GradientStop(ProjectionFx.Argb(0xDD, c), 0.5),
                new GradientStop(Color.FromArgb(0xAA, 0x8B, 0x65, 0x08), 1.0)
            }
        }))];
}

// ── Jukebox bubble tubes ─────────────────────────────────────────────────────────────────────────
internal sealed class JukeboxBubblesEffect(Canvas canvas)
{
    private static readonly RadialGradientBrush[] BubbleBrushes =
        [.. new[]
        {
            Color.FromRgb(0x00, 0xE5, 0xFF),
            Color.FromRgb(0xFF, 0x35, 0x7E),
            Color.FromRgb(0xFF, 0xD2, 0x69),
            Color.FromRgb(0x8E, 0x44, 0xAD),
            Color.FromRgb(0x00, 0xFF, 0xCC),
        }.Select(c => ProjectionFx.Frozen(new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF), 0.0),
                new GradientStop(ProjectionFx.Argb(0x88, c), 0.6),
                new GradientStop(ProjectionFx.Argb(0x22, c), 1.0)
            }
        }))];

    private readonly List<UIElement> _bubbles = [];

    /// <summary>Bubbles rising up the illuminated tubes along both cabinet sides.</summary>
    public void Build(bool reduced)
    {
        Stop();

        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var rng = ProjectionFx.Rng;
        int bubblesPerTube = reduced ? 8 : 18;
        for (int side = 0; side < 2; side++)
        {
            double tubeLeft = side == 0 ? 12 : w - 48;
            for (int i = 0; i < bubblesPerTube; i++)
            {
                double size = (rng.NextDouble() * 10) + 6;
                var bubble = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = BubbleBrushes[rng.Next(BubbleBrushes.Length)]
                };
                Canvas.SetLeft(bubble, tubeLeft + (rng.NextDouble() * 24));
                Canvas.SetTop(bubble, h - (rng.NextDouble() * (h * 0.3)));

                var translate = new TranslateTransform();
                bubble.RenderTransform = translate;

                double duration = (rng.NextDouble() * 2.5) + 3.0;
                double begin = rng.NextDouble() * 3.0;
                translate.BeginAnimation(TranslateTransform.YProperty, ProjectionFx.Loop(0, -h, duration, begin, autoReverse: false));
                bubble.BeginAnimation(UIElement.OpacityProperty, ProjectionFx.Loop(0.3, 0.95, duration * 0.5, begin));

                canvas.Children.Add(bubble);
                _bubbles.Add(bubble);
            }
        }
    }

    public void Stop()
    {
        ProjectionFx.StopDriftAnimations(_bubbles);
        canvas.Children.Clear();
        _bubbles.Clear();
    }
}

// ── Stadium Jumbotron spotlights ─────────────────────────────────────────────────────────────────
internal sealed class JumbotronSpotlightsEffect(Canvas canvas)
{
    private static readonly LinearGradientBrush BeamBrush = ProjectionFx.Frozen(new LinearGradientBrush
    {
        StartPoint = new Point(0.5, 1.0),
        EndPoint = new Point(0.5, 0.0),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0x44, 0xFF, 0xE0, 0x82), 0.0),
            new GradientStop(Color.FromArgb(0x22, 0x80, 0xD8, 0xFF), 0.5),
            new GradientStop(Color.FromArgb(0x00, 0x00, 0x00, 0x00), 1.0)
        }
    });

    private readonly List<RotateTransform> _rotates = [];

    /// <summary>Two arena spotlights sweeping back and forth from the bottom corners.</summary>
    public void Build()
    {
        Stop();

        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double beamLength = Math.Sqrt((w * w) + (h * h)) * 0.9;
        const double spread = 45;

        for (int i = 0; i < 2; i++)
        {
            bool isLeft = i == 0;
            double originX = isLeft ? w * 0.08 : w * 0.92;
            double originY = h * 0.95;

            var polygon = new Polygon
            {
                Points = [new Point(0, 0), new Point(-spread, -beamLength), new Point(spread, -beamLength)],
                RenderTransformOrigin = new Point(0.5, 1.0),
                Fill = BeamBrush
            };

            double baseAngle = isLeft ? 25 : -25;
            var rotate = new RotateTransform(baseAngle);
            polygon.RenderTransform = rotate;

            Canvas.SetLeft(polygon, originX - spread);
            Canvas.SetTop(polygon, originY - beamLength);
            canvas.Children.Add(polygon);
            _rotates.Add(rotate);

            const double swing = 30;
            rotate.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(baseAngle - swing, baseAngle + swing, TimeSpan.FromSeconds(isLeft ? 6.5 : 7.8))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                });
        }
    }

    public void Stop()
    {
        foreach (var rot in _rotates)
        {
            rot.BeginAnimation(RotateTransform.AngleProperty, null);
        }
        canvas.Children.Clear();
        _rotates.Clear();
    }
}

// ── Movie Theater film strip + projector beam ────────────────────────────────────────────────────
internal sealed class TheaterEffects(Canvas filmStripCanvas, Canvas beamCanvas)
{
    private const double TitleFrameHeight = 110;
    private const double SingerFrameHeight = 130;

    // Matches the " {12}" estimated-wait-time badge some display text formats bake in (e.g.
    // KSRotation's NextSingerDisplay.Text). That number ticks down on its own as the show runs with
    // no change to who's queued, so the signature strips it - a wait-time tick must not count as a
    // real queue change and restart the scroll.
    private static readonly Regex WaitBadgePattern = new(@"\s*\{\d+\}", RegexOptions.Compiled);

    private static readonly SolidColorBrush CelBgBrush = ProjectionFx.Frozen(Color.FromRgb(0x0A, 0x0A, 0x0A));
    private static readonly SolidColorBrush SprocketHoleBrush = ProjectionFx.Frozen(Color.FromRgb(0xE8, 0xD9, 0xB0));
    private static readonly SolidColorBrush GoldBrush = ProjectionFx.Frozen(Color.FromRgb(0xFF, 0xE8, 0xB0));
    private static readonly SolidColorBrush SingerCelBgBrush = ProjectionFx.Frozen(Color.FromRgb(0x1C, 0x0D, 0x10));
    private static readonly SolidColorBrush AnchorBadgeBgBrush = ProjectionFx.Frozen(Color.FromRgb(0xDC, 0x26, 0x26));
    private static readonly SolidColorBrush SingerTextBrush = ProjectionFx.Frozen(Color.FromRgb(0xFF, 0xFA, 0xF0));
    private static readonly LinearGradientBrush TitleGradientBrush = ProjectionFx.Frozen(new LinearGradientBrush(
        Color.FromRgb(0x3D, 0x24, 0x06), Color.FromRgb(0x1C, 0x0D, 0x10), new Point(0, 0), new Point(1, 1)));
    private static readonly DropShadowEffect TitleGlowEffect = ProjectionFx.Frozen(new DropShadowEffect
    {
        Color = Color.FromRgb(0xFF, 0xE8, 0xB0),
        BlurRadius = 18,
        ShadowDepth = 0,
        Opacity = 0.8
    });
    private static readonly LinearGradientBrush BeamBrush = ProjectionFx.Frozen(new LinearGradientBrush
    {
        StartPoint = new Point(0.5, 0.0),
        EndPoint = new Point(0.5, 1.0),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0x30, 0xFF, 0xF0, 0xD0), 0.0),
            new GradientStop(Color.FromArgb(0x15, 0xFF, 0xE8, 0xB0), 0.4),
            new GradientStop(Color.FromArgb(0x00, 0x00, 0x00, 0x00), 1.0)
        }
    });

    // Content signature (singer texts/order + strip size) of the strip currently scrolling. Routine
    // refreshes rebuild the next-singer list with the same entries far more often than the queue
    // actually changes; rebuilding then would restart the scroll and look like a stutter.
    private string? _signature;

    // Scroll is computed from a running clock every rendered frame (elapsed time modulo one set's
    // height) rather than a Timeline with RepeatBehavior.Forever, so there's no wrap instant to hitch
    // at. _translate is non-null exactly while the strip is scrolling.
    private TranslateTransform? _translate;
    private double _setHeight;
    private double _pxPerSecond;
    private readonly Stopwatch _clock = new();
    private bool _renderingHooked;

    private readonly List<UIElement> _beams = [];

    /// <summary>Scrolls a vertical film strip: a "COMING ATTRACTIONS" title frame followed by one frame
    /// per upcoming singer, repeating with no gap. No-op when the content hasn't changed.</summary>
    public void BuildFilmStrip(IReadOnlyList<(string Text, bool IsRotationStart)> singers)
    {
        double w = filmStripCanvas.ActualWidth;
        double viewportHeight = filmStripCanvas.ActualHeight;
        if (w <= 0) return;

        // Joined with U+0001 (never present in display text) so free-text titles can't collide two
        // queues onto one signature. Viewport height drives the copy count below, so a pure resize
        // must still rebuild.
        string signature = w.ToString("F0") + "\u0001" + viewportHeight.ToString("F0") + "\u0001" + string.Join("\u0001",
            singers.Select(s => WaitBadgePattern.Replace(s.Text, string.Empty) + (s.IsRotationStart ? "#A" : "")));
        if (signature == _signature && filmStripCanvas.Children.Count > 0)
        {
            return;
        }
        // StopFilmStrip() nulls the signature (so an external stop always forces a rebuild next
        // time), so it must run before the new signature is recorded.
        StopFilmStrip();
        _signature = signature;
        filmStripCanvas.Children.Clear();

        double setHeight = TitleFrameHeight + (singers.Count * SingerFrameHeight);
        if (setHeight <= 0) return;

        // Stack enough copies that the viewport is always covered by real content even at the scroll's
        // far extreme (offset -setHeight) - a fixed 2 copies runs out on a tall display and "pops"
        // once per cycle.
        int copies = Math.Max(2, (int)Math.Ceiling((viewportHeight + setHeight) / setHeight));
        for (int copy = 0; copy < copies; copy++)
        {
            double y = copy * setHeight;

            var title = BuildTitleFrame(w);
            Canvas.SetLeft(title, 0);
            Canvas.SetTop(title, y);
            filmStripCanvas.Children.Add(title);
            y += TitleFrameHeight;

            foreach (var singer in singers)
            {
                var frame = BuildSingerFrame(singer.Text, singer.IsRotationStart, w);
                Canvas.SetLeft(frame, 0);
                Canvas.SetTop(frame, y);
                filmStripCanvas.Children.Add(frame);
                y += SingerFrameHeight;
            }
        }

        var translate = new TranslateTransform();
        filmStripCanvas.RenderTransform = translate;

        double durationSeconds = Math.Max(10, setHeight / 40.0); // ~40px/sec projector crawl, slower floor for tiny queues
        _translate = translate;
        _setHeight = setHeight;
        _pxPerSecond = setHeight / durationSeconds;
        _clock.Restart();
        if (!_renderingHooked)
        {
            CompositionTarget.Rendering += OnRendering;
            _renderingHooked = true;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_translate == null || _setHeight <= 0) return;
        double distance = _clock.Elapsed.TotalSeconds * _pxPerSecond;
        _translate.Y = -(distance % _setHeight);
    }

    public void StopFilmStrip()
    {
        if (_renderingHooked)
        {
            CompositionTarget.Rendering -= OnRendering;
            _renderingHooked = false;
        }
        _translate = null;
        _clock.Reset();
        // Invalidate so the next BuildFilmStrip (e.g. switching back into this view) always rebuilds
        // and restarts the scroll, instead of leaving it frozen where it stopped.
        _signature = null;
    }

    private static Grid BuildFrameShell(double width, double height, out Border cel)
    {
        var root = new Grid { Width = width, Height = height, Background = CelBgBrush };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });

        var leftRail = BuildSprocketRail(height);
        Grid.SetColumn(leftRail, 0);
        root.Children.Add(leftRail);

        var rightRail = BuildSprocketRail(height);
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

    private static StackPanel BuildSprocketRail(double frameHeight)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        int holeCount = Math.Max(2, (int)(frameHeight / 40));
        for (int i = 0; i < holeCount; i++)
        {
            panel.Children.Add(new Border
            {
                Width = 14,
                Height = 10,
                CornerRadius = new CornerRadius(3),
                Background = SprocketHoleBrush,
                Margin = new Thickness(0, 6, 0, 6)
            });
        }
        return panel;
    }

    private static Grid BuildTitleFrame(double width)
    {
        var root = BuildFrameShell(width, TitleFrameHeight, out var cel);
        cel.BorderBrush = GoldBrush;
        cel.Background = TitleGradientBrush;
        cel.Child = new TextBlock
        {
            Text = "🎬 COMING ATTRACTIONS 🎬",
            FontSize = 26,
            FontWeight = FontWeights.Black,
            Foreground = GoldBrush,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = TitleGlowEffect
        };
        return root;
    }

    private static Grid BuildSingerFrame(string text, bool isRotationStart, double width)
    {
        var root = BuildFrameShell(width, SingerFrameHeight, out var cel);
        cel.BorderBrush = GoldBrush;
        cel.Background = SingerCelBgBrush;

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        if (isRotationStart)
        {
            stack.Children.Add(new Border
            {
                Background = AnchorBadgeBgBrush,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = "Rotation Anchor (marks where a round begins)",
                Child = new TextBlock { Text = "⚓ ANCHOR", FontSize = 11, FontWeight = FontWeights.ExtraBold, Foreground = Brushes.White }
            });
        }
        stack.Children.Add(new TextBlock { Text = "🎬", FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) });
        stack.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = SingerTextBrush,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = Math.Max(60, width - 90)
        });

        cel.Child = stack;
        return root;
    }

    /// <summary>Flickering projector light cone falling from the top of the screen.</summary>
    public void BuildProjectorBeam()
    {
        StopProjectorBeam();

        double w = beamCanvas.ActualWidth;
        double h = beamCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        const double topWidth = 60;
        double bottomWidth = w * 0.75;
        double centerX = w / 2;

        var cone = new Polygon
        {
            Points =
            [
                new Point(centerX - (topWidth / 2), 0),
                new Point(centerX + (topWidth / 2), 0),
                new Point(centerX + (bottomWidth / 2), h),
                new Point(centerX - (bottomWidth / 2), h)
            ],
            Fill = BeamBrush
        };
        cone.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0.7, 1.0, TimeSpan.FromSeconds(0.12)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });

        beamCanvas.Children.Add(cone);
        _beams.Add(cone);
    }

    public void StopProjectorBeam()
    {
        foreach (var beam in _beams)
        {
            beam.BeginAnimation(UIElement.OpacityProperty, null);
        }
        beamCanvas.Children.Clear();
        _beams.Clear();
    }
}
