// Edited on Nov 24, 2026 @ 10:10:00 -> Fully qualify System.Windows.Controls.Image to avoid CS0104 ambiguity with System.Drawing.Image
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SkiaSharp;
#if !MAUI
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
#endif

namespace Lyracist.Shared;

/// <summary>
/// PCDJ LYRX-style real-time audio-reactive shader presets and custom ShaderToy/SkSL loader,
/// evaluated via SkiaSharp's <see cref="SKRuntimeEffect"/>.
/// </summary>
public static class AudioShaderPresets
{
    public const string Off = "None (Off)";
    public const string Default = Off;

    public static readonly string[] BuiltInNames =
    [
        Off,
        "Neon Tunnel",
        "Plasma Waves",
        "Cyber Grid",
        "Audio Rings",
        "Star Warp",
        "Laser Nebula",
        "Electric Kaleidoscope",
        "Firestorm Pulse"
    ];

    // Custom .sksl / .glsl files dropped into Shaders/ folder in the app startup path
    public static string CustomShadersDir => Path.Combine(Globals.StartupPath, "Shaders");

    private static readonly Lock _cacheLock = new();
    private static readonly Dictionary<string, SKRuntimeEffect?> _compiledEffects = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns all available shader names (built-in presets + any custom .sksl/.glsl files in Shaders/).</summary>
    public static IReadOnlyList<string> GetAvailableNames()
    {
        var list = new List<string>(BuiltInNames);
        try
        {
            if (Directory.Exists(CustomShadersDir))
            {
                foreach (string file in Directory.EnumerateFiles(CustomShadersDir))
                {
                    string ext = Path.GetExtension(file);
                    if (ext.Equals(".sksl", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".glsl", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".frag", StringComparison.OrdinalIgnoreCase))
                    {
                        string name = Path.GetFileNameWithoutExtension(file).Trim();
                        if (!string.IsNullOrEmpty(name) && !list.Contains(name, StringComparer.OrdinalIgnoreCase))
                        {
                            list.Add(name);
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore directory enumeration errors
        }
        return list;
    }

    /// <summary>Normalizes a shader name; unknown or empty values fall back to <see cref="Off"/>.</summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Off;
        var all = GetAvailableNames();
        return all.FirstOrDefault(n => n.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Off;
    }

    public static bool IsActive(string? name)
    {
        string norm = Normalize(name);
        return !string.Equals(norm, Off, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Retrieves or compiles the <see cref="SKRuntimeEffect"/> for the given preset or custom shader name.</summary>
    public static SKRuntimeEffect? GetOrCompileEffect(string? name, out string? errorText)
    {
        errorText = null;
        string normalized = Normalize(name);
        if (string.Equals(normalized, Off, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        lock (_cacheLock)
        {
            if (_compiledEffects.TryGetValue(normalized, out var cached) && cached != null)
            {
                return cached;
            }

            string? source = GetShaderSource(normalized);
            if (string.IsNullOrWhiteSpace(source))
            {
                errorText = $"Shader source not found for '{normalized}'.";
                return null;
            }

            string wrappedSource = EnsureUniformHeader(source);
            var effect = SKRuntimeEffect.CreateShader(wrappedSource, out string? errors);
            if (effect == null)
            {
                errorText = errors;
                Debug.WriteLine($"[AudioShaderPresets] Failed to compile shader '{normalized}': {errors}");
                return null;
            }

            _compiledEffects[normalized] = effect;
            return effect;
        }
    }

    /// <summary>Clears cached compiled effects so edited custom shaders on disk reload cleanly.</summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            foreach (var effect in _compiledEffects.Values)
            {
                effect?.Dispose();
            }
            _compiledEffects.Clear();
        }
    }

    private static string EnsureUniformHeader(string body)
    {
        const string header = """
            uniform float2 iResolution;
            uniform float iTime;
            uniform float iBass;
            uniform float iMid;
            uniform float iHigh;
            uniform float iEnergy;
            """;

        string normalizedBody = body;
        // If user dropped a raw ShaderToy snippet using mainImage(out vec4 fragColor, in vec2 fragCoord),
        // add a thin SkSL entry wrapper if main(float2) isn't already defined.
        if (normalizedBody.Contains("mainImage", StringComparison.Ordinal) &&
            !normalizedBody.Contains("half4 main(", StringComparison.Ordinal) &&
            !normalizedBody.Contains("vec4 main(", StringComparison.Ordinal))
        {
            normalizedBody += "\nvec4 main(float2 fragCoord) { vec4 c = vec4(0.0); mainImage(c, fragCoord); return c; }\n";
        }

        if (normalizedBody.Contains("uniform float2 iResolution", StringComparison.Ordinal) ||
            normalizedBody.Contains("uniform vec2 iResolution", StringComparison.Ordinal))
        {
            return normalizedBody;
        }

        return header + "\n" + normalizedBody;
    }

    public static string? GetShaderSource(string name)
    {
        switch (name)
        {
            case "Neon Tunnel":
                return NeonTunnelSksl;
            case "Plasma Waves":
                return PlasmaWavesSksl;
            case "Cyber Grid":
                return CyberGridSksl;
            case "Audio Rings":
                return AudioRingsSksl;
            case "Star Warp":
                return StarWarpSksl;
            case "Laser Nebula":
                return LaserNebulaSksl;
            case "Electric Kaleidoscope":
                return ElectricKaleidoscopeSksl;
            case "Firestorm Pulse":
                return FirestormPulseSksl;
            default:
                try
                {
                    if (Directory.Exists(CustomShadersDir))
                    {
                        foreach (string ext in new[] { ".sksl", ".glsl", ".frag" })
                        {
                            string path = Path.Combine(CustomShadersDir, name + ext);
                            if (File.Exists(path))
                            {
                                return File.ReadAllText(path);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore I/O failures
                }
                return null;
        }
    }

    // ── 1. Neon Tunnel ──────────────────────────────────────────────────────────────────────────
    private const string NeonTunnelSksl = """
        half4 main(float2 fragCoord) {
            float2 uv = (fragCoord - 0.5 * iResolution) / min(iResolution.x, iResolution.y);
            float r = length(uv) + 0.001;
            float a = atan(uv.y, uv.x);

            float speed = iTime * 1.4 + iBass * 0.85;
            float z = 0.45 / r + speed;

            float ring = sin(z * 8.0) * 0.5 + 0.5;
            float spokes = sin(a * 6.0 + z * 2.5 - iTime * 1.8) * 0.5 + 0.5;
            float grid = smoothstep(0.75, 0.98, ring) + smoothstep(0.82, 0.98, spokes) * 0.7;

            float3 col1 = float3(0.05, 0.85, 1.0);
            float3 col2 = float3(1.0, 0.12, 0.75);
            float3 col3 = float3(0.45, 0.15, 1.0);

            float mixFactor = sin(z * 1.5 - iTime) * 0.5 + 0.5;
            float3 baseCol = mix(col1, col2, mixFactor);
            baseCol = mix(baseCol, col3, iMid * 0.6);

            float pulse = 0.35 + iBass * 0.95;
            float glow = (0.08 / r) * (0.6 + iHigh * 0.8);
            float vignette = smoothstep(0.02, 0.28, r);

            float3 finalCol = baseCol * grid * pulse * vignette + col2 * glow * 0.35;
            finalCol = clamp(finalCol, 0.0, 1.0);
            return half4(half3(finalCol), 1.0);
        }
        """;

    // ── 2. Plasma Waves ─────────────────────────────────────────────────────────────────────────
    private const string PlasmaWavesSksl = """
        half4 main(float2 fragCoord) {
            float2 uv = (fragCoord - 0.5 * iResolution) / min(iResolution.x, iResolution.y);
            float t = iTime * 0.9;

            float v1 = sin(uv.x * 4.5 + t + iBass * 1.8);
            float v2 = sin(4.5 * (uv.x * sin(t * 0.5) + uv.y * cos(t * 0.35)) + t);
            float cx = uv.x + 0.5 * sin(t * 0.3);
            float cy = uv.y + 0.5 * cos(t * 0.45);
            float v3 = sin(sqrt(22.0 * (cx * cx + cy * cy) + 1.0) - t * 1.6 - iMid * 2.0);
            float v = (v1 + v2 + v3) * 0.333;

            float3 col = float3(
                0.5 + 0.5 * sin(3.14159 * v + 0.0 + iBass * 1.2),
                0.5 + 0.5 * sin(3.14159 * v + 2.094 + iMid),
                0.5 + 0.5 * sin(3.14159 * v + 4.188 + iHigh)
            );

            // Add audio-reactive ribbon highlight
            float waveY = sin(uv.x * 6.0 + t * 2.2) * (0.12 + iBass * 0.22);
            float ribbon = 0.018 / (abs(uv.y - waveY) + 0.015);
            col = col * (0.45 + 0.45 * iEnergy) + float3(0.2, 0.85, 1.0) * ribbon * (0.4 + iHigh * 0.8);
            col = clamp(col, 0.0, 1.0);
            return half4(half3(col), 1.0);
        }
        """;

    // ── 3. Cyber Grid ───────────────────────────────────────────────────────────────────────────
    private const string CyberGridSksl = """
        half4 main(float2 fragCoord) {
            float2 uv = (fragCoord - 0.5 * iResolution) / min(iResolution.x, iResolution.y);
            uv.y = -uv.y; // Y+ up

            float3 col = float3(0.03, 0.01, 0.08);
            float horizon = -0.05;

            if (uv.y > horizon) {
                // Sky & Beat-Pulsing Synth Sun
                float2 sunUv = uv - float2(0.0, 0.16);
                float sunRadius = 0.24 + iBass * 0.05;
                float d = length(sunUv);
                if (d < sunRadius) {
                    float stripes = step(0.18, sin(uv.y * 95.0 + iTime * 2.5) + (uv.y - 0.05) * 4.5);
                    float3 sunCol = mix(float3(1.0, 0.08, 0.55), float3(1.0, 0.85, 0.15), (uv.y + 0.05) * 2.6);
                    col = mix(col, sunCol, stripes);
                }
                float sunGlow = exp(-d * 4.2) * (0.55 + iBass * 0.6);
                col += float3(1.0, 0.18, 0.6) * sunGlow;
            } else {
                // Perspective Grid Floor
                float py = abs(uv.y - horizon) + 0.002;
                float z = 0.25 / py;
                float x = uv.x * z * 1.4;

                float gx = abs(fract(x) - 0.5);
                float gz = abs(fract(z - iTime * 2.4 - iBass * 0.6) - 0.5);
                float line = smoothstep(0.06, 0.01, min(gx, gz));

                float fade = exp(-py * 1.5) * smoothstep(0.0, 0.08, py);
                float3 gridCol = mix(float3(0.0, 0.9, 1.0), float3(1.0, 0.1, 0.8), sin(z * 0.4 - iTime) * 0.5 + 0.5);
                col = mix(float3(0.06, 0.01, 0.14), gridCol * (0.8 + iBass * 0.7), line * fade);
            }

            // Horizon glow beam
            float hGlow = 0.012 / (abs(uv.y - horizon) + 0.012);
            col += float3(0.95, 0.2, 0.85) * hGlow * (0.45 + iMid * 0.65);
            col = clamp(col, 0.0, 1.0);
            return half4(half3(col), 1.0);
        }
        """;

    // ── 4. Audio Rings ──────────────────────────────────────────────────────────────────────────
    private const string AudioRingsSksl = """
        half4 main(float2 fragCoord) {
            float2 uv = (fragCoord - 0.5 * iResolution) / min(iResolution.x, iResolution.y);
            float r = length(uv);
            float a = atan(uv.y, uv.x);

            float3 col = float3(0.02, 0.03, 0.07);

            // Concentric audio-reactive rings
            for (int i = 1; i <= 4; i++) {
                float fi = float(i);
                float level = (i == 1) ? iBass : ((i == 2) ? iMid : ((i == 3) ? iHigh : iEnergy));
                float wobble = sin(a * (3.0 + fi * 2.0) + iTime * (1.5 + fi * 0.4) * (mod(fi, 2.0) * 2.0 - 1.0)) * (0.015 + level * 0.045);
                float targetR = fi * 0.11 + level * 0.06 + wobble;
                float dist = abs(r - targetR);
                float glow = 0.007 / (dist + 0.005);

                float3 ringCol = float3(
                    0.5 + 0.5 * cos(fi * 1.3 + iTime * 0.7),
                    0.5 + 0.5 * cos(fi * 1.3 + iTime * 0.7 + 2.094),
                    0.5 + 0.5 * cos(fi * 1.3 + iTime * 0.7 + 4.188)
                );
                col += ringCol * glow * (0.35 + level * 0.75);
            }

            // Center core pulse
            float core = 0.025 / (r + 0.03) * (0.3 + iBass * 0.9);
            col += float3(0.35, 0.75, 1.0) * core;
            col = clamp(col, 0.0, 1.0);
            return half4(half3(col), 1.0);
        }
        """;

    // ── 5. Star Warp ────────────────────────────────────────────────────────────────────────────
    private const string StarWarpSksl = """
        float hash21(float2 p) {
            p = fract(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return fract(p.x * p.y);
        }

        half4 main(float2 fragCoord) {
            float2 uv = (fragCoord - 0.5 * iResolution) / min(iResolution.x, iResolution.y);
            float t = iTime * 0.45 + iBass * 0.15;

            float3 col = float3(0.01, 0.02, 0.06);

            for (int layer = 0; layer < 3; layer++) {
                float fl = float(layer);
                float depth = fract(fl * 0.333 + t);
                float scale = mix(12.0, 1.2, depth);
                float fade = depth * smoothstep(1.0, 0.8, depth);

                float2 p = uv * scale + float2(fl * 17.3, fl * 9.1);
                float2 id = floor(p);
                float2 gv = fract(p) - 0.5;

                float n = hash21(id);
                float2 offset = float2(n - 0.5, fract(n * 34.56) - 0.5) * 0.7;
                float d = length(gv - offset);

                float star = (0.018 * (1.0 + iHigh * 1.2)) / (d + 0.008);
                star *= smoothstep(0.45, 0.05, d);

                float3 starCol = mix(float3(0.4, 0.75, 1.0), float3(1.0, 0.5, 0.9), fract(n * 19.1));
                col += starCol * star * fade * 0.45;
            }

            // Nebula center glow
            float r = length(uv);
            col += float3(0.18, 0.06, 0.35) * exp(-r * 2.2) * (0.6 + iBass * 0.8);
            col = clamp(col, 0.0, 1.0);
            return half4(half3(col), 1.0);
        }
        """;

    // ── 6. Laser Nebula ─────────────────────────────────────────────────────────────────────────
    private const string LaserNebulaSksl = """
        half4 main(float2 fragCoord) {
            float2 uv = (fragCoord - 0.5 * iResolution) / min(iResolution.x, iResolution.y);
            float t = iTime * 0.65;

            float2 p = uv * 2.2;
            for (int i = 1; i < 5; i++) {
                float fi = float(i);
                p.x += 0.35 / fi * sin(fi * 2.5 * p.y + t + iBass * 1.4);
                p.y += 0.35 / fi * cos(fi * 2.5 * p.x + t + iMid * 1.2);
            }

            float r = 0.5 + 0.5 * sin(p.x + p.y + iBass * 2.0);
            float g = 0.5 + 0.5 * sin(p.x - p.y + 2.0 + iMid * 1.5);
            float b = 0.5 + 0.5 * cos(p.x * p.y + 4.0 + iHigh * 1.5);

            float3 col = float3(r * 0.8, g * 0.35 + b * 0.25, b * 0.95);

            // Sweeping laser beams
            for (int k = 0; k < 3; k++) {
                float fk = float(k);
                float angle = sin(t * (0.8 + fk * 0.3) + fk * 2.1) * 0.85;
                float2 dir = float2(cos(angle), sin(angle));
                float beamDist = abs(dot(uv - float2(0.0, -0.45), float2(-dir.y, dir.x)));
                float beam = 0.006 / (beamDist + 0.006) * (0.35 + iHigh * 0.85);
                float3 beamCol = (k == 0) ? float3(0.1, 1.0, 0.6) : ((k == 1) ? float3(1.0, 0.15, 0.7) : float3(0.2, 0.7, 1.0));
                col += beamCol * beam * 0.45;
            }

            col = clamp(col * (0.55 + iEnergy * 0.55), 0.0, 1.0);
            return half4(half3(col), 1.0);
        }
        """;

    // ── 7. Electric Kaleidoscope ────────────────────────────────────────────────────────────────
    private const string ElectricKaleidoscopeSksl = """
        half4 main(float2 fragCoord) {
            float2 uv = (fragCoord - 0.5 * iResolution) / min(iResolution.x, iResolution.y);
            float r = length(uv);
            float a = atan(uv.y, uv.x);

            // 8-fold kaleidoscope mirror symmetry
            float segments = 8.0;
            a = mod(a, 6.2831853 / segments);
            a = abs(a - 3.1415926 / segments);

            float2 p = float2(cos(a), sin(a)) * r;
            float t = iTime * 0.8 + iBass * 0.5;

            float3 col = float3(0.0);
            for (int i = 0; i < 3; i++) {
                float fi = float(i);
                p = abs(p) / clamp(dot(p, p), 0.25, 1.2) - float2(0.55 + 0.15 * sin(t + fi), 0.45 + 0.12 * cos(t * 0.7));
                float d = abs(length(p) - 0.25 - iMid * 0.15);
                float glow = 0.015 / (d + 0.015);
                float3 layerCol = float3(
                    0.5 + 0.5 * sin(fi * 2.1 + t),
                    0.5 + 0.5 * sin(fi * 2.1 + t + 2.09),
                    0.5 + 0.5 * sin(fi * 2.1 + t + 4.18)
                );
                col += layerCol * glow * (0.28 + iEnergy * 0.4);
            }

            col = clamp(col, 0.0, 1.0);
            return half4(half3(col), 1.0);
        }
        """;

    // ── 8. Firestorm Pulse ──────────────────────────────────────────────────────────────────────
    private const string FirestormPulseSksl = """
        half4 main(float2 fragCoord) {
            float2 uv = (fragCoord - 0.5 * iResolution) / min(iResolution.x, iResolution.y);
            uv.y = -uv.y; // Y+ up

            float t = iTime * 1.3;
            float2 p = uv * 3.0;
            p.y -= t + iBass * 0.8;

            float n = 0.0;
            float amp = 0.55;
            for (int i = 0; i < 4; i++) {
                n += amp * (sin(p.x * 1.8 + sin(p.y * 1.4 + t)) * cos(p.y * 1.6 - cos(p.x * 1.3)));
                p = p * 1.9 + float2(1.7, -t * 0.6);
                amp *= 0.5;
            }

            float heat = clamp((0.45 - uv.y) * 1.1 + n * 0.45 + iBass * 0.45, 0.0, 1.2);
            float3 col = float3(
                smoothstep(0.05, 0.55, heat),
                smoothstep(0.35, 0.85, heat) * 0.75,
                smoothstep(0.65, 1.15, heat) * 0.9
            );

            // Ember ring burst
            float r = length(uv);
            float ring = 0.014 / (abs(r - (0.22 + iBass * 0.14)) + 0.018);
            col += float3(1.0, 0.45, 0.1) * ring * (0.4 + iHigh * 0.8);
            col = clamp(col, 0.0, 1.0);
            return half4(half3(col), 1.0);
        }
        """;
}

#if !MAUI
/// <summary>
/// WPF image control that evaluates an <see cref="SKRuntimeEffect"/> fragment shader into a
/// double-buffered <see cref="WriteableBitmap"/> on a 30 FPS render clock, driven by live FFT
/// frequency bands (or smooth ambient motion when audio is idle).
/// </summary>
public sealed class AudioShaderHost : Border
{
    private const int StandardRenderWidth = 480;
    private const int StandardRenderHeight = 270;
    private const int ReducedRenderWidth = 320;
    private const int ReducedRenderHeight = 180;
    private const double MinFrameIntervalSeconds = 1.0 / 30.0;

    public static readonly DependencyProperty ShaderNameProperty = DependencyProperty.Register(
        nameof(ShaderName), typeof(string), typeof(AudioShaderHost),
        new PropertyMetadata(AudioShaderPresets.Off, (d, _) => ((AudioShaderHost)d).RefreshState()));

    public static readonly DependencyProperty ReducedQualityProperty = DependencyProperty.Register(
        nameof(ReducedQuality), typeof(bool), typeof(AudioShaderHost),
        new PropertyMetadata(false));

    /// <summary>Selected shader preset name (or "None (Off)").</summary>
    public string ShaderName
    {
        get => (string)GetValue(ShaderNameProperty);
        set => SetValue(ShaderNameProperty, value);
    }

    /// <summary>When true, renders at a lower internal resolution for older venue PCs.</summary>
    public bool ReducedQuality
    {
        get => (bool)GetValue(ReducedQualityProperty);
        set => SetValue(ReducedQualityProperty, value);
    }

    /// <summary>Optional delegate returning live normalized FFT frequency bands (0..1).</summary>
    public Func<int, float[]>? BandProvider { get; set; }

    private readonly System.Windows.Controls.Image _image;
    private WriteableBitmap? _bitmap;
    private readonly Stopwatch _clock = new();
    private double _lastFrameSeconds = -1;
    private bool _hooked;

    // Smoothed audio uniform state
    private float _bass;
    private float _mid;
    private float _high;
    private float _energy;

    public AudioShaderHost()
    {
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
        Background = System.Windows.Media.Brushes.Transparent;

        _image = new System.Windows.Controls.Image
        {
            Stretch = Stretch.Fill,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.Linear);
        Child = _image;

        Loaded += (_, _) => RefreshState();
        Unloaded += (_, _) => Unhook();
        IsVisibleChanged += (_, _) => RefreshState();
    }

    public void RefreshState()
    {
        bool shouldRun = IsLoaded && AudioShaderPresets.IsActive(ShaderName);
        if (shouldRun)
        {
            if (Visibility != Visibility.Visible)
            {
                Visibility = Visibility.Visible;
            }
            Hook();
        }
        else
        {
            Unhook();
            if (Visibility != Visibility.Collapsed)
            {
                Visibility = Visibility.Collapsed;
            }
        }
    }

    private void Hook()
    {
        if (_hooked) return;
        _clock.Start();
        _lastFrameSeconds = -1;
        CompositionTarget.Rendering += OnRendering;
        _hooked = true;
    }

    private void Unhook()
    {
        if (!_hooked) return;
        CompositionTarget.Rendering -= OnRendering;
        _hooked = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!IsVisible) return;

        double now = _clock.Elapsed.TotalSeconds;
        if (_lastFrameSeconds >= 0 && (now - _lastFrameSeconds) < MinFrameIntervalSeconds)
        {
            return;
        }
        _lastFrameSeconds = now;

        var effect = AudioShaderPresets.GetOrCompileEffect(ShaderName, out _);
        if (effect == null) return;

        int targetW = ReducedQuality ? ReducedRenderWidth : StandardRenderWidth;
        int targetH = ReducedQuality ? ReducedRenderHeight : StandardRenderHeight;

        if (_bitmap == null || _bitmap.PixelWidth != targetW || _bitmap.PixelHeight != targetH)
        {
            _bitmap = new WriteableBitmap(targetW, targetH, 96, 96, PixelFormats.Pbgra32, null);
            _image.Source = _bitmap;
        }

        UpdateAudioUniforms((float)now);

        var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["iResolution"] = new float[] { targetW, targetH },
            ["iTime"] = (float)now,
            ["iBass"] = _bass,
            ["iMid"] = _mid,
            ["iHigh"] = _high,
            ["iEnergy"] = _energy
        };

        using var shader = effect.ToShader(uniforms);
        if (shader == null) return;

        _bitmap.Lock();
        try
        {
            var info = new SKImageInfo(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, _bitmap.BackBuffer, _bitmap.BackBufferStride);
            if (surface != null)
            {
                using var paint = new SKPaint
                {
                    Shader = shader,
                    BlendMode = SKBlendMode.Src
                };
                surface.Canvas.DrawRect(0, 0, targetW, targetH, paint);
            }
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, targetW, targetH));
        }
        finally
        {
            _bitmap.Unlock();
        }
    }

    private void UpdateAudioUniforms(float time)
    {
        float rawBass = 0f;
        float rawMid = 0f;
        float rawHigh = 0f;

        var bands = BandProvider?.Invoke(32);
        if (bands is { Length: >= 16 })
        {
            // Bass: lowest ~20% of logarithmic bands
            int bassEnd = Math.Max(2, bands.Length / 5);
            int midEnd = Math.Max(bassEnd + 2, (bands.Length * 3) / 5);

            for (int i = 0; i < bassEnd; i++) rawBass = Math.Max(rawBass, bands[i]);
            for (int i = bassEnd; i < midEnd; i++) rawMid += bands[i];
            rawMid /= Math.Max(1, midEnd - bassEnd);

            for (int i = midEnd; i < bands.Length; i++) rawHigh += bands[i];
            rawHigh /= Math.Max(1, bands.Length - midEnd);
        }

        // If audio is completely silent, supply a subtle ambient heartbeat so the shader still breathes smoothly
        if (rawBass < 0.01f && rawMid < 0.01f && rawHigh < 0.01f)
        {
            rawBass = 0.18f + 0.12f * (float)Math.Sin(time * 2.1f);
            rawMid = 0.15f + 0.10f * (float)Math.Sin(time * 1.4f + 1.2f);
            rawHigh = 0.12f + 0.08f * (float)Math.Cos(time * 2.7f);
        }

        _bass += (rawBass - _bass) * (rawBass > _bass ? 0.75f : 0.22f);
        _mid += (rawMid - _mid) * (rawMid > _mid ? 0.65f : 0.20f);
        _high += (rawHigh - _high) * (rawHigh > _high ? 0.70f : 0.25f);
        _energy = Math.Clamp((_bass * 0.45f) + (_mid * 0.35f) + (_high * 0.20f), 0f, 1.25f);
    }
}
#endif
