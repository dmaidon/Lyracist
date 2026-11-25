// Edited on Nov 24, 2026 @ 11:42:30 -> Add PCDJ LYRX ANGLE-HLSL .frag to SkSL translator in AudioShaderPresets
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SkiaSharp;
#if !MAUI
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
#endif

namespace Lyracist.Shared;

/// <summary>
/// PCDJ LYRX-style real-time audio-reactive shader library and ShaderToy GLSL importer,
/// stored in the shared <c>Shaders/</c> folder and evaluated via SkiaSharp's <see cref="SKRuntimeEffect"/>.
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

    /// <summary>Supported shader file extensions (ShaderToy GLSL, SkSL, fragment shaders, or PCDJ .txt shader scripts).</summary>
    public static readonly string[] AllowedExtensions = [".glsl", ".sksl", ".frag", ".fs", ".shader", ".txt"];

    /// <summary>Shared Shaders directory (<c>&lt;StartupPath&gt;\Shaders</c>) where all default and imported ShaderToy files live.</summary>
    public static string CustomShadersDir => Globals.ShadersDir;

    private static readonly Lock _cacheLock = new();
    private static readonly Dictionary<string, SKRuntimeEffect?> _compiledEffects = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, DateTime> _compiledTimestamps = new(StringComparer.OrdinalIgnoreCase);
    private static bool _seededDefaults;
    private static FileSystemWatcher? _folderWatcher;
    private static System.Threading.Timer? _debounceTimer;
    private static readonly Dictionary<string, (DateTime Stamp, string? Error)> _failedCompiles = new(StringComparer.OrdinalIgnoreCase);
    private static int _cacheVersion;

    static AudioShaderPresets()
    {
        // The ANGLE translator uses more distinct patterns than the default static Regex cache (15).
        Regex.CacheSize = Math.Max(Regex.CacheSize, 128);
    }

    /// <summary>Raised (on the calling thread) when a selected shader fails to compile, with the compiler error text.</summary>
    public static event Action<string, string?>? CompileFailed;

    internal static void RaiseCompileFailed(string name, string? error) => CompileFailed?.Invoke(name, error);

    /// <summary>Returns the cached compiler error for a shader that failed to compile, or null.</summary>
    public static string? LastCompileError(string? name)
    {
        lock (_cacheLock)
        {
            return name != null && _failedCompiles.TryGetValue(name, out var f) ? f.Error : null;
        }
    }

    /// <summary>Increments whenever the cache is cleared; hosts re-resolve their effect only when this changes.</summary>
    public static int CacheVersion => System.Threading.Volatile.Read(ref _cacheVersion);

    /// <summary>Known PCDJ LYRX / DEX 3 shader IDs mapped to their friendly display names from <c>shaders.xml</c>.</summary>
    public static readonly IReadOnlyDictionary<string, string> KnownLyrxShaderNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["st-ldscRB"] = "3D Kali",
        ["st-XdtBR2"] = "Dancing Sinus",
        ["st-XdByzy"] = "Dancing Stick Figures",
        ["st-MdjyRm"] = "Shaderlamp",
        ["st-ldXBRj"] = "Simple Bar Audio",
        ["st-ls3BDH"] = "SoundEclipse",
        ["st-4lSXzh"] = "Traced Minkowski Tube",
        ["st-ld2yDD"] = "Twitchy Fractal"
    };

    /// <summary>Raised whenever shader files are added, modified, or removed in <see cref="CustomShadersDir"/>.</summary>
    public static event Action? AvailableShadersChanged;

    private static void EnsureFolderWatcher()
    {
        if (_folderWatcher != null || !Directory.Exists(CustomShadersDir)) return;
        try
        {
            var watcher = new FileSystemWatcher(CustomShadersDir)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            // A single save raises several events and may leave the file half-written, so debounce.
            _debounceTimer ??= new System.Threading.Timer(_ =>
            {
                ClearCache();
                AvailableShadersChanged?.Invoke();
            });
            FileSystemEventHandler onChanged = (_, _) => _debounceTimer.Change(300, System.Threading.Timeout.Infinite);
            RenamedEventHandler onRenamed = (_, _) => _debounceTimer.Change(300, System.Threading.Timeout.Infinite);
            watcher.Created += onChanged;
            watcher.Deleted += onChanged;
            watcher.Changed += onChanged;
            watcher.Renamed += onRenamed;
            _folderWatcher = watcher;
        }
        catch
        {
            // Ignore watcher errors on read-only paths
        }
    }

    /// <summary>
    /// Ensures the <c>Shaders/</c> directory exists and writes all 8 built-in shaders as editable
    /// ShaderToy-compatible <c>.glsl</c> files if they are not already present on disk.
    /// </summary>
    public static void EnsureDefaultShaderFiles()
    {
        if (_seededDefaults && Directory.Exists(CustomShadersDir))
        {
            EnsureFolderWatcher();
            return;
        }

        try
        {
            Directory.CreateDirectory(CustomShadersDir);
            foreach (string name in BuiltInNames)
            {
                if (string.Equals(name, Off, StringComparison.OrdinalIgnoreCase)) continue;

                string filePath = Path.Combine(CustomShadersDir, name + ".glsl");
                if (!File.Exists(filePath))
                {
                    string? defaultSource = GetBuiltInShaderSource(name);
                    if (!string.IsNullOrWhiteSpace(defaultSource))
                    {
                        File.WriteAllText(filePath, defaultSource);
                    }
                }
            }
            _seededDefaults = true;
            EnsureFolderWatcher();
        }
        catch
        {
            // Ignore read-only directory errors
        }
    }

    /// <summary>Returns all available shader names ("None (Off)", built-in presets, and all shader files in <c>Shaders/</c>).</summary>
    public static IReadOnlyList<string> GetAvailableNames()
    {
        EnsureDefaultShaderFiles();
        var list = new List<string>(BuiltInNames);
        try
        {
            if (Directory.Exists(CustomShadersDir))
            {
                foreach (string file in Directory.EnumerateFiles(CustomShadersDir).OrderBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase))
                {
                    string ext = Path.GetExtension(file);
                    if (AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    {
                        string name = Path.GetFileNameWithoutExtension(file).Trim();
                        if (!string.IsNullOrEmpty(name) &&
                            !name.Equals("README", StringComparison.OrdinalIgnoreCase) &&
                            !list.Contains(name, StringComparer.OrdinalIgnoreCase))
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

    /// <summary>
    /// Validates and imports an external ShaderToy (<c>.glsl</c>, <c>.frag</c>, <c>.sksl</c>, <c>.fs</c>, <c>.txt</c>)
    /// file into the <c>Shaders/</c> folder so it appears in all shader dropdowns.
    /// </summary>
    public static bool TryImportShaderFile(string sourceFilePath, out string importedShaderName, out string? errorMessage)
    {
        importedShaderName = string.Empty;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            errorMessage = "The selected shader file does not exist.";
            return false;
        }

        try
        {
            string rawSource = File.ReadAllText(sourceFilePath);
            if (string.IsNullOrWhiteSpace(rawSource))
            {
                errorMessage = "The selected shader file is empty.";
                return false;
            }

            string translated = TranslateShaderToyToSksl(rawSource);
            using var testEffect = SKRuntimeEffect.CreateShader(translated, out string? compileErrors);
            if (testEffect == null)
            {
                errorMessage = $"Shader failed to compile (unsupported multi-pass buffer or syntax error):\n{compileErrors}";
                return false;
            }

            EnsureDefaultShaderFiles();
            string baseName = Path.GetFileNameWithoutExtension(sourceFilePath).Trim();
            if (KnownLyrxShaderNames.TryGetValue(baseName, out string? friendlyLyrxName))
            {
                baseName = friendlyLyrxName;
            }
            if (string.IsNullOrWhiteSpace(baseName) || string.Equals(baseName, Off, StringComparison.OrdinalIgnoreCase))
            {
                baseName = "Custom Shader";
            }

            string ext = Path.GetExtension(sourceFilePath);
            if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                ext = ".glsl";
            }

            // If a shader of this name exists under a different extension it would shadow the import, so pick a unique name.
            string? existing = FindShaderFilePath(baseName);
            if (existing != null && !string.Equals(Path.GetExtension(existing), ext, StringComparison.OrdinalIgnoreCase))
            {
                string candidate = baseName;
                for (int n = 2; FindShaderFilePath(candidate) != null; n++) candidate = $"{baseName} ({n})";
                baseName = candidate;
            }

            string destPath = Path.Combine(CustomShadersDir, baseName + ext);
            if (!string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourceFilePath, destPath, overwrite: true);
            }

            ClearCache();
            importedShaderName = baseName;
            AvailableShadersChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
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
            string? diskPath = FindShaderFilePath(normalized);
            DateTime diskWriteUtc = diskPath != null ? File.GetLastWriteTimeUtc(diskPath) : DateTime.MinValue;

            if (_compiledEffects.TryGetValue(normalized, out var cached) &&
                cached != null &&
                _compiledTimestamps.TryGetValue(normalized, out var cachedUtc) &&
                cachedUtc == diskWriteUtc)
            {
                return cached;
            }

            // Don't re-translate/re-compile a shader that already failed for this file version.
            if (_failedCompiles.TryGetValue(normalized, out var failed) && failed.Stamp == diskWriteUtc)
            {
                errorText = failed.Error;
                return null;
            }

            string? source = GetShaderSource(normalized);
            if (string.IsNullOrWhiteSpace(source))
            {
                errorText = $"Shader source not found for '{normalized}'.";
                return null;
            }

            string wrappedSource = TranslateShaderToyToSksl(source);
            var effect = SKRuntimeEffect.CreateShader(wrappedSource, out string? errors);
            if (effect == null)
            {
                // If a modified disk copy of a built-in shader had a typo, fall back to the embedded source
                string? fallbackBuiltIn = GetBuiltInShaderSource(normalized);
                if (fallbackBuiltIn != null && !string.Equals(fallbackBuiltIn, source, StringComparison.Ordinal))
                {
                    effect = SKRuntimeEffect.CreateShader(TranslateShaderToyToSksl(fallbackBuiltIn), out errors);
                }

                if (effect == null)
                {
                    errorText = errors;
                    _failedCompiles[normalized] = (diskWriteUtc, errors);
                    Debug.WriteLine($"[AudioShaderPresets] Failed to compile shader '{normalized}': {errors}");
                    return null;
                }
            }

            // Old effects are intentionally not disposed: a render may still be using one. The GC finalizer frees them.
            _failedCompiles.Remove(normalized);
            _compiledEffects[normalized] = effect;
            _compiledTimestamps[normalized] = diskWriteUtc;
            return effect;
        }
    }

    /// <summary>Clears cached compiled effects so edited or newly imported custom shaders on disk reload immediately.</summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            // Not disposed here (may be mid-render on the UI thread); the GC finalizer releases them.
            _compiledEffects.Clear();
            _compiledTimestamps.Clear();
            _failedCompiles.Clear();
            System.Threading.Interlocked.Increment(ref _cacheVersion);
        }
    }

    private static string? FindShaderFilePath(string name)
    {
        try
        {
            if (Directory.Exists(CustomShadersDir))
            {
                foreach (string ext in AllowedExtensions)
                {
                    string path = Path.Combine(CustomShadersDir, name + ext);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }
        }
        catch
        {
            // Ignore I/O errors
        }
        return null;
    }

    /// <summary>
    /// Translates standard ShaderToy GLSL syntax (such as <c>mainImage(out vec4 fragColor, in vec2 fragCoord)</c>,
    /// <c>iResolution.xy</c>, <c>iTimeDelta</c>, <c>iFrame</c>, <c>iMouse</c>, <c>texture(iChannel0, uv)</c>,
    /// <c>texelFetch(iChannel0, ...)</c>) as well as PCDJ LYRX / DEX 3 ANGLE-transpiled HLSL <c>.frag</c> files
    /// (<c>BEGIN COMPILER 0 OBJ CODE</c>, <c>f_mainImage_float4</c>, <c>gl_main</c>, <c>_iResolution</c>, <c>_iTime</c>)
    /// into SkiaSharp SkSL.
    /// </summary>
    public static string TranslateShaderToyToSksl(string body)
    {
        if (IsAngleHlslShader(body))
        {
            return TranslateAngleHlslToSksl(body);
        }

        const string header = """
            uniform float2 iResolution;
            uniform float iTime;
            uniform float iBass;
            uniform float iMid;
            uniform float iHigh;
            uniform float iEnergy;
            uniform float iTimeDelta;
            uniform int iFrame;
            uniform float4 iMouse;
            uniform float4 iDate;
            uniform float iSampleRate;

            const int iChannel0 = 0;
            const int iChannel1 = 1;
            const int iChannel2 = 2;
            const int iChannel3 = 3;

            // Procedural audio-reactive helpers emulating ShaderToy's iChannel0..3 FFT/noise texture & texelFetch lookups
            vec4 _agySampleAudioChannel(vec2 uv) {
                float x = clamp(uv.x, 0.0, 1.0);
                float band = (x < 0.25) ? mix(iBass, iMid, x * 4.0)
                           : (x < 0.65) ? mix(iMid, iHigh, (x - 0.25) * 2.5)
                           : mix(iHigh, iEnergy * 0.7, (x - 0.65) * 2.85);
                float wave = 0.5 + 0.35 * sin(uv.x * 24.0 + iTime * 6.0) * iEnergy;
                float val = (uv.y < 0.5) ? clamp(band, 0.0, 1.0) : clamp(wave, 0.0, 1.0);
                return vec4(val, band, wave, 1.0);
            }
            vec4 _agyTex(int ch, vec2 uv) { return _agySampleAudioChannel(uv); }
            vec4 _agyTex(int ch, vec2 uv, float bias) { return _agySampleAudioChannel(uv); }
            vec4 _agyTexLod(int ch, vec2 uv, float lod) { return _agySampleAudioChannel(uv); }
            vec4 _agyTexFetch(int ch, ivec2 coord, int lod) {
                return _agySampleAudioChannel(vec2(float(coord.x) / 512.0, float(coord.y)));
            }
            """;

        string code = body;

        // 1. Strip #version and precision qualifiers that SkSL doesn't accept
        code = Regex.Replace(code, @"^\s*#version\b.*$", "", RegexOptions.Multiline);
        code = Regex.Replace(code, @"^\s*precision\s+(lowp|mediump|highp)\s+\w+\s*;", "", RegexOptions.Multiline);

        // 2. Map GLSL texture/texture2D/textureLod/texelFetch calls to SkSL helper functions
        code = Regex.Replace(code, @"\b(?:texture|texture2D)\s*\(", "_agyTex(");
        code = Regex.Replace(code, @"\btextureLod\s*\(", "_agyTexLod(");
        code = Regex.Replace(code, @"\btexelFetch\s*\(", "_agyTexFetch(");

        // 3. Replace iChannelResolution[0..3].xy with iResolution.xy
        code = Regex.Replace(code, @"\biChannelResolution\s*\[\s*[0-3]\s*\]\s*(\.[xyzwrgba]+)?", "vec3(iResolution, 1.0)$1");

        // 4. Replace 1.f / 2.0f float suffixes (valid in GLSL, rejected by SkSL)
        code = Regex.Replace(code, @"\b(\d+\.\d*|\.\d+|\d+)f\b", "$1");

        // 5. If shader defines ShaderToy's mainImage(out vec4 fragColor, in vec2 fragCoord), append SkSL's main(float2) entry point
        if (code.Contains("mainImage", StringComparison.Ordinal) &&
            !Regex.IsMatch(code, @"\b(half4|vec4|float4)\s+main\s*\("))
        {
            code += "\nhalf4 main(float2 fragCoord) { vec4 fragColor = vec4(0.0); mainImage(fragColor, fragCoord); return half4(fragColor); }\n";
        }

        if (code.Contains("uniform float2 iResolution", StringComparison.Ordinal) ||
            code.Contains("uniform vec2 iResolution", StringComparison.Ordinal))
        {
            return code;
        }

        return header + "\n" + code;
    }

    /// <summary>Detects PCDJ LYRX / DEX 3 ANGLE-transpiled Direct3D HLSL <c>.frag</c> shaders.</summary>
    public static bool IsAngleHlslShader(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        return source.Contains("BEGIN COMPILER 0 OBJ CODE", StringComparison.Ordinal) ||
               source.Contains("f_mainImage_float4", StringComparison.Ordinal) ||
               source.Contains("cbuffer DriverConstants", StringComparison.Ordinal) ||
               source.Contains("gl_main2(VOut", StringComparison.Ordinal);
    }

    /// <summary>
    /// Translates a PCDJ LYRX / DEX 3 ANGLE-transpiled HLSL <c>.frag</c> shader into SkiaSharp SkSL.
    /// </summary>
    private static string TranslateAngleHlslToSksl(string raw)
    {
        const string angleHeader = """
            uniform float2 iResolution;
            uniform float iTime;
            uniform float iBass;
            uniform float iMid;
            uniform float iHigh;
            uniform float iEnergy;
            uniform float iTimeDelta;
            uniform int iFrame;
            uniform float4 iMouse;
            uniform float4 iDate;
            uniform float iSampleRate;

            vec4 _agySampleAudioChannel(vec2 uv) {
                float x = clamp(uv.x, 0.0, 1.0);
                float band = (x < 0.25) ? mix(iBass, iMid, x * 4.0)
                           : (x < 0.65) ? mix(iMid, iHigh, (x - 0.25) * 2.5)
                           : mix(iHigh, iEnergy * 0.7, (x - 0.65) * 2.85);
                float wave = 0.5 + 0.35 * sin(uv.x * 24.0 + iTime * 6.0) * iEnergy;
                float val = (uv.y < 0.5) ? clamp(band, 0.0, 1.0) : clamp(wave, 0.0, 1.0);
                return vec4(val, band, wave, 1.0);
            }
            float4 _agy_gl_texture2D(int samplerIndex, float2 t) { return _agySampleAudioChannel(t); }
            float4 _agy_gl_texture2DLod0(int samplerIndex, float2 t) { return _agySampleAudioChannel(t); }
            float2 mul(float2 a, float2x2 b) { return a * b; }
            float2 mul(float2x2 a, float2 b) { return a * b; }
            float2x2 mul(float2x2 a, float2x2 b) { return a * b; }
            float3 mul(float3 a, float3x3 b) { return a * b; }
            float3 mul(float3x3 a, float3 b) { return a * b; }
            float3x3 mul(float3x3 a, float3x3 b) { return a * b; }
            float4 mul(float4 a, float4x4 b) { return a * b; }
            float4 mul(float4x4 a, float4 b) { return a * b; }
            float4x4 mul(float4x4 a, float4x4 b) { return a * b; }
            float2x2 _agyTranspose(float2x2 m) { return float2x2(m[0][0], m[1][0], m[0][1], m[1][1]); }
            float3x3 _agyTranspose(float3x3 m) {
                return float3x3(m[0][0], m[1][0], m[2][0],
                                m[0][1], m[1][1], m[2][1],
                                m[0][2], m[1][2], m[2][2]);
            }
            float4 _agy_gl_Color_0 = float4(0.0, 0.0, 0.0, 1.0);
            float4 _agy_gl_FragCoord = float4(0.0, 0.0, 0.0, 1.0);
            """;

        string code = raw;

        // 1. Extract the ANGLE OBJ CODE section if markers are present
        const string beginMarker = "//// BEGIN COMPILER 0 OBJ CODE ////";
        const string endMarker = "//// END COMPILER 0 OBJ CODE ////";
        int startIdx = code.IndexOf(beginMarker, StringComparison.Ordinal);
        if (startIdx >= 0)
        {
            startIdx += beginMarker.Length;
            int endIdx = code.IndexOf(endMarker, startIdx, StringComparison.Ordinal);
            code = endIdx >= 0 ? code[startIdx..endIdx] : code[startIdx..];
        }
        else
        {
            // Strip any D3D11 VOut / gl_main2 suffix if markers were stripped manually
            int voutIdx = code.IndexOf("struct VOut", StringComparison.Ordinal);
            if (voutIdx >= 0) code = code[..voutIdx];
        }

        // 2. Strip preprocessor directives (#pragma, #ifdef, #else, #endif, #define)
        code = Regex.Replace(code, @"^\s*#\s*(pragma|ifdef|ifndef|else|elif|endif|define)\b.*$", "", RegexOptions.Multiline);

        // 3. Strip cbuffer DriverConstants blocks (which may contain nested struct SamplerMetadata)
        code = Regex.Replace(code, @"\bcbuffer\s+\w+[^{]*\{(?:[^{}]|\{[^{}]*\})*\}\s*;", "", RegexOptions.Singleline);

        // 4. Strip HLSL uniform register declarations and static gl_Color / gl_FragCoord declarations
        code = Regex.Replace(code, @"^\s*uniform\s+[^;]+:\s*register\s*\([^)]+\)\s*;\s*$", "", RegexOptions.Multiline);
        code = Regex.Replace(code, @"\bstatic\s+float4\s+gl_Color\s*\[\s*1\s*\]\s*=\s*\{[^}]*\}\s*;", "", RegexOptions.Singleline);
        code = Regex.Replace(code, @"\bstatic\s+float4\s+gl_FragCoord\s*=[^;]+;", "");

        // 5. Strip ANGLE's D3D11 gl_texture2D / gl_texture2DLod0 definitions (provided by angleHeader)
        code = Regex.Replace(code, @"\bfloat4\s+gl_texture2D(?:Lod0)?\s*\([^{]+\{[^}]*\}", "", RegexOptions.Singleline);

        // 6. Replace gl_Color[0] with _agy_gl_Color_0, rename reserved gl_* identifiers to _agy_gl_*, uint -> int, strip 'static' and LOOP/FLATTEN
        code = code.Replace("gl_Color[0]", "_agy_gl_Color_0", StringComparison.Ordinal);
        code = Regex.Replace(code, @"\bgl_", "_agy_gl_");
        code = Regex.Replace(code, @"\buint\b", "int");
        code = Regex.Replace(code, @"\bstatic\s+", "");
        code = Regex.Replace(code, @"\b(LOOP|FLATTEN)\b", "");

        // 7. Strip standalone empty semicolon lines (';') that ANGLE emits around prototypes
        code = Regex.Replace(code, @"^\s*;\s*$", "", RegexOptions.Multiline);

        // 8. Map HLSL intrinsics to SkSL equivalents
        code = Regex.Replace(code, @"\bfrac\s*\(", "fract(");
        code = Regex.Replace(code, @"\blerp\s*\(", "mix(");
        code = Regex.Replace(code, @"\batan2\s*\(", "atan(");
        code = Regex.Replace(code, @"\brsqrt\s*\(", "inversesqrt(");
        code = Regex.Replace(code, @"\btranspose\s*\(", "_agyTranspose(");

        // 9. Replace HLSL brace initializers: `float3 _p = {0, 0, 0}` or `float _m = {0}` or `int _i = {0}`
        code = Regex.Replace(
            code,
            @"\b(float[234]?|int[234]?|bool[234]?|half[234]?|vec[234]|ivec[234]|bvec[234])\s+([A-Za-z_]\w*)\s*=\s*\{([^{};]*)\}",
            match =>
            {
                string type = match.Groups[1].Value;
                string varName = match.Groups[2].Value;
                string args = match.Groups[3].Value.Trim();
                bool isFloatType = type.StartsWith("float", StringComparison.Ordinal) ||
                                   type.StartsWith("half", StringComparison.Ordinal) ||
                                   type.StartsWith("vec", StringComparison.Ordinal);
                if (isFloatType)
                {
                    var parts = args.Split(',').Select(p =>
                    {
                        string t = p.Trim();
                        if (Regex.IsMatch(t, @"^-?\d+$")) return t + ".0";
                        return t;
                    });
                    args = string.Join(", ", parts);
                }
                return $"{type} {varName} = {type}({args})";
            });

        // 10. Convert comma-chained parenthesized assignments (e.g. in st-4lSXzh.frag) into semicolon statements
        code = Regex.Replace(
            code,
            @"\(\(\(\(\(_v\.x\s*=\s*([^)]+\))\),\s*\(_v\.y\s*=\s*([^)]+\))\)\),\s*\(_v\.z\s*=\s*([^;]+)\)\),\s*\(_v\.w\s*=\s*([^;]+)\)\);",
            "_v.x = $1; _v.y = $2; _v.z = $3; _v.w = $4;");

        // 11. Convert float loop induction variables (e.g. `for(float _f = float(0.0); (_f <= 0.25); (_f += 0.0099999998))` in st-XdByzy.frag)
        // into integer loop induction variables required by SkSL ES2 runtime effects
        code = Regex.Replace(
            code,
            @"for\s*\(\s*float\s+([A-Za-z_]\w*)\s*=\s*(?:float\()?(0(?:\.0+)?)\)?\s*;\s*\(?\1\s*<=\s*0\.25\)?\s*;\s*\(?\1\s*\+=\s*0\.0099999998\)?\s*\)\s*\{",
            "for (int _agyLoopI = 0; _agyLoopI <= 25; _agyLoopI++) { float $1 = float(_agyLoopI) * 0.01;");

        // 12. Fix int-to-float implicit conversion in `if(x == 0 && y == 0) x = 1;` inside ANGLE's atan_emu
        code = Regex.Replace(code, @"if\s*\(\s*x\s*==\s*0\s*&&\s*y\s*==\s*0\s*\)\s*x\s*=\s*1\s*;", "if (x == 0.0 && y == 0.0) x = 1.0;");

        // 13. Replace `_iResolution` and `_iTime` with SkSL uniforms (adding subtle bass reactivity to `_iTime`)
        code = Regex.Replace(code, @"\b_iResolution\b", "vec3(iResolution, 1.0)");
        code = Regex.Replace(code, @"\b_iTime\b", "(iTime + iBass * 0.25)");

        // 14. Append SkSL main(float2 fragCoord) entry point calling _agy_gl_main()
        code += """

            half4 main(float2 fragCoord) {
                _agy_gl_FragCoord = float4(fragCoord.x, iResolution.y - fragCoord.y, 0.0, 1.0);
                _agy_gl_main();
                return half4(_agy_gl_Color_0);
            }
            """;

        return angleHeader + "\n" + code;
    }

    /// <summary>Loads the shader source from <c>Shaders/&lt;name&gt;.glsl</c> on disk first, falling back to the embedded source.</summary>
    public static string? GetShaderSource(string name)
    {
        EnsureDefaultShaderFiles();
        string? diskPath = FindShaderFilePath(name);
        if (diskPath != null)
        {
            try
            {
                return File.ReadAllText(diskPath);
            }
            catch
            {
                // Fall back to built-in source below if file read fails
            }
        }

        return GetBuiltInShaderSource(name);
    }

    public static string? GetBuiltInShaderSource(string name) => name switch
    {
        "Neon Tunnel" => NeonTunnelSksl,
        "Plasma Waves" => PlasmaWavesSksl,
        "Cyber Grid" => CyberGridSksl,
        "Audio Rings" => AudioRingsSksl,
        "Star Warp" => StarWarpSksl,
        "Laser Nebula" => LaserNebulaSksl,
        "Electric Kaleidoscope" => ElectricKaleidoscopeSksl,
        "Firestorm Pulse" => FirestormPulseSksl,
        _ => null
    };

    // ── 1. Neon Tunnel ──────────────────────────────────────────────────────────────────────────
    private const string NeonTunnelSksl = """
        // Neon Tunnel - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
        void mainImage(out vec4 fragColor, in vec2 fragCoord) {
            vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
            float r = length(uv) + 0.001;
            float a = atan(uv.y, uv.x);

            float speed = iTime * 1.4 + iBass * 0.85;
            float z = 0.45 / r + speed;

            float ring = sin(z * 8.0) * 0.5 + 0.5;
            float spokes = sin(a * 6.0 + z * 2.5 - iTime * 1.8) * 0.5 + 0.5;
            float grid = smoothstep(0.75, 0.98, ring) + smoothstep(0.82, 0.98, spokes) * 0.7;

            vec3 col1 = vec3(0.05, 0.85, 1.0);
            vec3 col2 = vec3(1.0, 0.12, 0.75);
            vec3 col3 = vec3(0.45, 0.15, 1.0);

            float mixFactor = sin(z * 1.5 - iTime) * 0.5 + 0.5;
            vec3 baseCol = mix(col1, col2, mixFactor);
            baseCol = mix(baseCol, col3, iMid * 0.6);

            float pulse = 0.35 + iBass * 0.95;
            float glow = (0.08 / r) * (0.6 + iHigh * 0.8);
            float vignette = smoothstep(0.02, 0.28, r);

            vec3 finalCol = baseCol * grid * pulse * vignette + col2 * glow * 0.35;
            fragColor = vec4(clamp(finalCol, 0.0, 1.0), 1.0);
        }
        """;

    // ── 2. Plasma Waves ─────────────────────────────────────────────────────────────────────────
    private const string PlasmaWavesSksl = """
        // Plasma Waves - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
        void mainImage(out vec4 fragColor, in vec2 fragCoord) {
            vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
            float t = iTime * 0.9;

            float v1 = sin(uv.x * 4.5 + t + iBass * 1.8);
            float v2 = sin(4.5 * (uv.x * sin(t * 0.5) + uv.y * cos(t * 0.35)) + t);
            float cx = uv.x + 0.5 * sin(t * 0.3);
            float cy = uv.y + 0.5 * cos(t * 0.45);
            float v3 = sin(sqrt(22.0 * (cx * cx + cy * cy) + 1.0) - t * 1.6 - iMid * 2.0);
            float v = (v1 + v2 + v3) * 0.333;

            vec3 col = vec3(
                0.5 + 0.5 * sin(3.14159 * v + 0.0 + iBass * 1.2),
                0.5 + 0.5 * sin(3.14159 * v + 2.094 + iMid),
                0.5 + 0.5 * sin(3.14159 * v + 4.188 + iHigh)
            );

            float waveY = sin(uv.x * 6.0 + t * 2.2) * (0.12 + iBass * 0.22);
            float ribbon = 0.018 / (abs(uv.y - waveY) + 0.015);
            col = col * (0.45 + 0.45 * iEnergy) + vec3(0.2, 0.85, 1.0) * ribbon * (0.4 + iHigh * 0.8);
            fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
        }
        """;

    // ── 3. Cyber Grid ───────────────────────────────────────────────────────────────────────────
    private const string CyberGridSksl = """
        // Cyber Grid - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
        void mainImage(out vec4 fragColor, in vec2 fragCoord) {
            vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
            uv.y = -uv.y;

            vec3 col = vec3(0.03, 0.01, 0.08);
            float horizon = -0.05;

            if (uv.y > horizon) {
                vec2 sunUv = uv - vec2(0.0, 0.16);
                float sunRadius = 0.24 + iBass * 0.05;
                float d = length(sunUv);
                if (d < sunRadius) {
                    float stripes = step(0.18, sin(uv.y * 95.0 + iTime * 2.5) + (uv.y - 0.05) * 4.5);
                    vec3 sunCol = mix(vec3(1.0, 0.08, 0.55), vec3(1.0, 0.85, 0.15), (uv.y + 0.05) * 2.6);
                    col = mix(col, sunCol, stripes);
                }
                float sunGlow = exp(-d * 4.2) * (0.55 + iBass * 0.6);
                col += vec3(1.0, 0.18, 0.6) * sunGlow;
            } else {
                float py = abs(uv.y - horizon) + 0.002;
                float z = 0.25 / py;
                float x = uv.x * z * 1.4;

                float gx = abs(fract(x) - 0.5);
                float gz = abs(fract(z - iTime * 2.4 - iBass * 0.6) - 0.5);
                float line = smoothstep(0.06, 0.01, min(gx, gz));

                float fade = exp(-py * 1.5) * smoothstep(0.0, 0.08, py);
                vec3 gridCol = mix(vec3(0.0, 0.9, 1.0), vec3(1.0, 0.1, 0.8), sin(z * 0.4 - iTime) * 0.5 + 0.5);
                col = mix(vec3(0.06, 0.01, 0.14), gridCol * (0.8 + iBass * 0.7), line * fade);
            }

            float hGlow = 0.012 / (abs(uv.y - horizon) + 0.012);
            col += vec3(0.95, 0.2, 0.85) * hGlow * (0.45 + iMid * 0.65);
            fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
        }
        """;

    // ── 4. Audio Rings ──────────────────────────────────────────────────────────────────────────
    private const string AudioRingsSksl = """
        // Audio Rings - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
        void mainImage(out vec4 fragColor, in vec2 fragCoord) {
            vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
            float r = length(uv);
            float a = atan(uv.y, uv.x);

            vec3 col = vec3(0.02, 0.03, 0.07);

            for (int i = 1; i <= 4; i++) {
                float fi = float(i);
                float level = (i == 1) ? iBass : ((i == 2) ? iMid : ((i == 3) ? iHigh : iEnergy));
                float wobble = sin(a * (3.0 + fi * 2.0) + iTime * (1.5 + fi * 0.4) * (mod(fi, 2.0) * 2.0 - 1.0)) * (0.015 + level * 0.045);
                float targetR = fi * 0.11 + level * 0.06 + wobble;
                float dist = abs(r - targetR);
                float glow = 0.007 / (dist + 0.005);

                vec3 ringCol = vec3(
                    0.5 + 0.5 * cos(fi * 1.3 + iTime * 0.7),
                    0.5 + 0.5 * cos(fi * 1.3 + iTime * 0.7 + 2.094),
                    0.5 + 0.5 * cos(fi * 1.3 + iTime * 0.7 + 4.188)
                );
                col += ringCol * glow * (0.35 + level * 0.75);
            }

            float core = 0.025 / (r + 0.03) * (0.3 + iBass * 0.9);
            col += vec3(0.35, 0.75, 1.0) * core;
            fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
        }
        """;

    // ── 5. Star Warp ────────────────────────────────────────────────────────────────────────────
    private const string StarWarpSksl = """
        // Star Warp - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
        float hash21(vec2 p) {
            p = fract(p * vec2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return fract(p.x * p.y);
        }

        void mainImage(out vec4 fragColor, in vec2 fragCoord) {
            vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
            float t = iTime * 0.45 + iBass * 0.15;

            vec3 col = vec3(0.01, 0.02, 0.06);

            for (int layer = 0; layer < 3; layer++) {
                float fl = float(layer);
                float depth = fract(fl * 0.333 + t);
                float scale = mix(12.0, 1.2, depth);
                float fade = depth * smoothstep(1.0, 0.8, depth);

                vec2 p = uv * scale + vec2(fl * 17.3, fl * 9.1);
                vec2 id = floor(p);
                vec2 gv = fract(p) - 0.5;

                float n = hash21(id);
                vec2 offset = vec2(n - 0.5, fract(n * 34.56) - 0.5) * 0.7;
                float d = length(gv - offset);

                float star = (0.018 * (1.0 + iHigh * 1.2)) / (d + 0.008);
                star *= smoothstep(0.45, 0.05, d);

                vec3 starCol = mix(vec3(0.4, 0.75, 1.0), vec3(1.0, 0.5, 0.9), fract(n * 19.1));
                col += starCol * star * fade * 0.45;
            }

            float r = length(uv);
            col += vec3(0.18, 0.06, 0.35) * exp(-r * 2.2) * (0.6 + iBass * 0.8);
            fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
        }
        """;

    // ── 6. Laser Nebula ─────────────────────────────────────────────────────────────────────────
    private const string LaserNebulaSksl = """
        // Laser Nebula - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
        void mainImage(out vec4 fragColor, in vec2 fragCoord) {
            vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
            float t = iTime * 0.65;

            vec2 p = uv * 2.2;
            for (int i = 1; i < 5; i++) {
                float fi = float(i);
                p.x += 0.35 / fi * sin(fi * 2.5 * p.y + t + iBass * 1.4);
                p.y += 0.35 / fi * cos(fi * 2.5 * p.x + t + iMid * 1.2);
            }

            float r = 0.5 + 0.5 * sin(p.x + p.y + iBass * 2.0);
            float g = 0.5 + 0.5 * sin(p.x - p.y + 2.0 + iMid * 1.5);
            float b = 0.5 + 0.5 * cos(p.x * p.y + 4.0 + iHigh * 1.5);

            vec3 col = vec3(r * 0.8, g * 0.35 + b * 0.25, b * 0.95);

            for (int k = 0; k < 3; k++) {
                float fk = float(k);
                float angle = sin(t * (0.8 + fk * 0.3) + fk * 2.1) * 0.85;
                vec2 dir = vec2(cos(angle), sin(angle));
                float beamDist = abs(dot(uv - vec2(0.0, -0.45), vec2(-dir.y, dir.x)));
                float beam = 0.006 / (beamDist + 0.006) * (0.35 + iHigh * 0.85);
                vec3 beamCol = (k == 0) ? vec3(0.1, 1.0, 0.6) : ((k == 1) ? vec3(1.0, 0.15, 0.7) : vec3(0.2, 0.7, 1.0));
                col += beamCol * beam * 0.45;
            }

            fragColor = vec4(clamp(col * (0.55 + iEnergy * 0.55), 0.0, 1.0), 1.0);
        }
        """;

    // ── 7. Electric Kaleidoscope ────────────────────────────────────────────────────────────────
    private const string ElectricKaleidoscopeSksl = """
        // Electric Kaleidoscope - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
        void mainImage(out vec4 fragColor, in vec2 fragCoord) {
            vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
            float r = length(uv);
            float a = atan(uv.y, uv.x);

            float segments = 8.0;
            a = mod(a, 6.2831853 / segments);
            a = abs(a - 3.1415926 / segments);

            vec2 p = vec2(cos(a), sin(a)) * r;
            float t = iTime * 0.8 + iBass * 0.5;

            vec3 col = vec3(0.0);
            for (int i = 0; i < 3; i++) {
                float fi = float(i);
                p = abs(p) / clamp(dot(p, p), 0.25, 1.2) - vec2(0.55 + 0.15 * sin(t + fi), 0.45 + 0.12 * cos(t * 0.7));
                float d = abs(length(p) - 0.25 - iMid * 0.15);
                float glow = 0.015 / (d + 0.015);
                vec3 layerCol = vec3(
                    0.5 + 0.5 * sin(fi * 2.1 + t),
                    0.5 + 0.5 * sin(fi * 2.1 + t + 2.09),
                    0.5 + 0.5 * sin(fi * 2.1 + t + 4.18)
                );
                col += layerCol * glow * (0.28 + iEnergy * 0.4);
            }

            fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
        }
        """;

    // ── 8. Firestorm Pulse ──────────────────────────────────────────────────────────────────────
    private const string FirestormPulseSksl = """
        // Firestorm Pulse - Beat-Reactive ShaderToy / SkSL Shader for Lyracist & KSRotation
        void mainImage(out vec4 fragColor, in vec2 fragCoord) {
            vec2 uv = (fragCoord - 0.5 * iResolution.xy) / min(iResolution.x, iResolution.y);
            uv.y = -uv.y;

            float t = iTime * 1.3;
            vec2 p = uv * 3.0;
            p.y -= t + iBass * 0.8;

            float n = 0.0;
            float amp = 0.55;
            for (int i = 0; i < 4; i++) {
                n += amp * (sin(p.x * 1.8 + sin(p.y * 1.4 + t)) * cos(p.y * 1.6 - cos(p.x * 1.3)));
                p = p * 1.9 + vec2(1.7, -t * 0.6);
                amp *= 0.5;
            }

            float heat = clamp((0.45 - uv.y) * 1.1 + n * 0.45 + iBass * 0.45, 0.0, 1.2);
            vec3 col = vec3(
                smoothstep(0.05, 0.55, heat),
                smoothstep(0.35, 0.85, heat) * 0.75,
                smoothstep(0.65, 1.15, heat) * 0.9
            );

            float r = length(uv);
            float ring = 0.014 / (abs(r - (0.22 + iBass * 0.14)) + 0.018);
            col += vec3(1.0, 0.45, 0.1) * ring * (0.4 + iHigh * 0.8);
            fragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
        }
        """;
}

#if !MAUI
/// <summary>
/// One rendered shader surface shared by every <see cref="AudioShaderHost"/> showing the same shader at the
/// same resolution (e.g. the operator window and the venue display), so the effect is evaluated once per frame.
/// </summary>
internal sealed class SharedShaderRenderer
{
    private const double MinFrameIntervalSeconds = 1.0 / 30.0;
    private static readonly Dictionary<string, SharedShaderRenderer> _active = new();
    private static readonly Stopwatch _clock = Stopwatch.StartNew();

    private readonly string _key;
    private int _refCount;
    private readonly SKRuntimeEffect? _effect;
    private readonly SKRuntimeEffectUniforms? _uniforms;
    private readonly HashSet<string>? _uniformNames;
    private readonly float[] _resVec = new float[2];
    private readonly float[] _mouseVec = new float[4];
    private readonly float[] _dateVec = new float[4];
    private readonly SKPaint _paint = new() { BlendMode = SKBlendMode.Src };
    private double _lastFrameSeconds = -1;
    private int _frameCount;
    private float _bass, _mid, _high, _energy;
    private float _mouseX, _mouseY, _clickX, _clickY;
    private bool _mouseDown;
    private bool _mouseTouched;

    public int Width { get; }
    public int Height { get; }

    /// <summary>The bitmap to display, or null when the shader failed to compile.</summary>
    public WriteableBitmap? Bitmap { get; }

    private SharedShaderRenderer(string key, string name, int width, int height)
    {
        _key = key;
        Width = width;
        Height = height;
        _effect = AudioShaderPresets.GetOrCompileEffect(name, out string? error);
        if (_effect == null)
        {
            AudioShaderPresets.RaiseCompileFailed(name, error ?? AudioShaderPresets.LastCompileError(name));
            return;
        }
        _uniforms = new SKRuntimeEffectUniforms(_effect);
        _uniformNames = new HashSet<string>(_effect.Uniforms, StringComparer.Ordinal);
        Bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
    }

    public static SharedShaderRenderer Acquire(string name, int width, int height)
    {
        string key = $"{name}|{width}x{height}|{AudioShaderPresets.CacheVersion}";
        if (!_active.TryGetValue(key, out var renderer))
        {
            renderer = new SharedShaderRenderer(key, name, width, height);
            _active[key] = renderer;
        }
        renderer._refCount++;
        return renderer;
    }

    public void Release()
    {
        if (--_refCount > 0) return;
        _active.Remove(_key);
        _paint.Dispose();
    }

    /// <summary>
    /// Feeds pointer state (render-space pixels, origin bottom-left) using the ShaderToy <c>iMouse</c> convention:
    /// xy follow the pointer while the button is down, zw hold the click position (negated after release).
    /// </summary>
    public void SubmitMouse(float x, float y, bool down)
    {
        _mouseTouched = true;
        if (down)
        {
            if (!_mouseDown) { _clickX = x; _clickY = y; }
            _mouseX = x;
            _mouseY = y;
        }
        _mouseDown = down;
    }

    /// <summary>
    /// Renders a frame unless one was already produced in this interval (by another host sharing this renderer).
    /// Returns the render time in milliseconds, or -1 when nothing was rendered.
    /// </summary>
    public double RenderIfDue(Func<int, float[]>? bandProvider, int sampleRate)
    {
        if (_effect == null || _uniforms == null || _uniformNames == null || Bitmap == null) return -1;

        double now = _clock.Elapsed.TotalSeconds;
        double delta = _lastFrameSeconds >= 0 ? (now - _lastFrameSeconds) : MinFrameIntervalSeconds;
        if (_lastFrameSeconds >= 0 && delta < MinFrameIntervalSeconds) return -1;
        _lastFrameSeconds = now;
        _frameCount++;

        long start = Stopwatch.GetTimestamp();
        UpdateAudioUniforms((float)now, bandProvider);

        var nowDate = DateTime.Now;
        var names = _uniformNames;
        var uniforms = _uniforms;
        _resVec[0] = Width; _resVec[1] = Height;
        if (_mouseTouched)
        {
            _mouseVec[0] = _mouseX; _mouseVec[1] = _mouseY;
            _mouseVec[2] = _mouseDown ? _clickX : -_clickX;
            _mouseVec[3] = _mouseDown ? _clickY : -_clickY;
        }
        else
        {
            _mouseVec[0] = Width * 0.5f; _mouseVec[1] = Height * 0.5f; _mouseVec[2] = 0f; _mouseVec[3] = 0f;
        }
        _dateVec[0] = nowDate.Year; _dateVec[1] = nowDate.Month - 1; _dateVec[2] = nowDate.Day; _dateVec[3] = (float)nowDate.TimeOfDay.TotalSeconds;
        if (names.Contains("iResolution")) uniforms["iResolution"] = _resVec;
        if (names.Contains("iTime")) uniforms["iTime"] = (float)now;
        if (names.Contains("iTimeDelta")) uniforms["iTimeDelta"] = (float)delta;
        if (names.Contains("iFrame")) uniforms["iFrame"] = _frameCount;
        if (names.Contains("iBass")) uniforms["iBass"] = _bass;
        if (names.Contains("iMid")) uniforms["iMid"] = _mid;
        if (names.Contains("iHigh")) uniforms["iHigh"] = _high;
        if (names.Contains("iEnergy")) uniforms["iEnergy"] = _energy;
        if (names.Contains("iMouse")) uniforms["iMouse"] = _mouseVec;
        if (names.Contains("iDate")) uniforms["iDate"] = _dateVec;
        if (names.Contains("iSampleRate")) uniforms["iSampleRate"] = (float)sampleRate;

        using var shader = _effect.ToShader(uniforms);
        if (shader == null) return -1;

        Bitmap.Lock();
        try
        {
            var info = new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, Bitmap.BackBuffer, Bitmap.BackBufferStride);
            if (surface != null)
            {
                _paint.Shader = shader;
                surface.Canvas.DrawRect(0, 0, Width, Height, _paint);
                _paint.Shader = null;
            }
            Bitmap.AddDirtyRect(new Int32Rect(0, 0, Width, Height));
        }
        finally
        {
            Bitmap.Unlock();
        }

        return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    }

    private void UpdateAudioUniforms(float time, Func<int, float[]>? bandProvider)
    {
        float rawBass = 0f;
        float rawMid = 0f;
        float rawHigh = 0f;

        var bands = bandProvider?.Invoke(32);
        if (bands is { Length: >= 16 })
        {
            int bassEnd = Math.Max(2, bands.Length / 5);
            int midEnd = Math.Max(bassEnd + 2, (bands.Length * 3) / 5);

            for (int i = 0; i < bassEnd; i++) rawBass = Math.Max(rawBass, bands[i]);
            for (int i = bassEnd; i < midEnd; i++) rawMid += bands[i];
            rawMid /= Math.Max(1, midEnd - bassEnd);

            for (int i = midEnd; i < bands.Length; i++) rawHigh += bands[i];
            rawHigh /= Math.Max(1, bands.Length - midEnd);
        }

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

/// <summary>
/// WPF image control that displays a <see cref="SharedShaderRenderer"/> frame (an <see cref="SKRuntimeEffect"/>
/// fragment shader on a ~30 FPS clock), driven by live FFT frequency bands or smooth ambient motion when audio is idle.
/// Hosts showing the same shader at the same size share one render.
/// </summary>
public sealed class AudioShaderHost : Border
{
    private const int StandardRenderWidth = 480;
    private const int StandardRenderHeight = 270;
    private const int ReducedRenderWidth = 320;
    private const int ReducedRenderHeight = 180;

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

    /// <summary>Optional delegate returning the capture device sample rate in Hz (defaults to 48000).</summary>
    public Func<int>? SampleRateProvider { get; set; }

    private readonly System.Windows.Controls.Image _image;
    private bool _hooked;
    private bool _autoReduced;
    private int _slowFrames;

    private SharedShaderRenderer? _renderer;
    private string? _resolvedName;
    private int _resolvedVersion = -1;
    private bool _resolvedReduced;

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
        CompositionTarget.Rendering += OnRendering;
        _hooked = true;
    }

    private void Unhook()
    {
        if (!_hooked) return;
        CompositionTarget.Rendering -= OnRendering;
        _hooked = false;
        ReleaseRenderer();
    }

    private void ReleaseRenderer()
    {
        _renderer?.Release();
        _renderer = null;
        _resolvedName = null;
        _image.Source = null;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!IsVisible) return;

        string shaderName = ShaderName;
        if (shaderName != _resolvedName)
        {
            // New shader: forget any adaptive downgrade from the previous one.
            _autoReduced = false;
            _slowFrames = 0;
        }

        bool reduced = ReducedQuality || _autoReduced;
        int version = AudioShaderPresets.CacheVersion;

        // Re-acquire only when the shader, quality or cache changes (no per-frame disk I/O or recompiles).
        if (shaderName != _resolvedName || version != _resolvedVersion || reduced != _resolvedReduced)
        {
            _renderer?.Release();
            _resolvedName = shaderName;
            _resolvedVersion = version;
            _resolvedReduced = reduced;
            _renderer = SharedShaderRenderer.Acquire(
                shaderName,
                reduced ? ReducedRenderWidth : StandardRenderWidth,
                reduced ? ReducedRenderHeight : StandardRenderHeight);
            _image.Source = _renderer.Bitmap;
        }

        if (_renderer != null && ActualWidth > 0 && ActualHeight > 0)
        {
            // Pointer works even though the host is not hit-testable (read from the global mouse state).
            var pos = System.Windows.Input.Mouse.GetPosition(this);
            if (pos.X >= 0 && pos.Y >= 0 && pos.X <= ActualWidth && pos.Y <= ActualHeight)
            {
                bool down = System.Windows.Input.Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed;
                _renderer.SubmitMouse(
                    (float)(pos.X / ActualWidth * _renderer.Width),
                    (float)((1.0 - pos.Y / ActualHeight) * _renderer.Height),
                    down);
            }
        }

        int sampleRate = SampleRateProvider?.Invoke() ?? 48000;
        double ms = _renderer?.RenderIfDue(BandProvider, sampleRate > 0 ? sampleRate : 48000) ?? -1;

        // Adaptive quality: sustained frames over ~28 ms drop this host to the reduced resolution.
        if (ms >= 0 && !reduced)
        {
            _slowFrames = ms > 28.0 ? _slowFrames + 1 : Math.Max(0, _slowFrames - 1);
            if (_slowFrames >= 45) _autoReduced = true;
        }
    }
}

/// <summary>
/// Shared "shader behind the rotation views" logic for the venue display windows: decides whether a shader is
/// showing, makes the projection panels semi-transparent over it, and hides the rotation chrome in Shader Only mode.
/// </summary>
public sealed class ShaderBackdropController
{
    /// <summary>Opacity applied to projection panel backgrounds while a shader is active.</summary>
    public const double ShaderBackdropOpacity = 0.32;

    // Panels whose background brush has already been replaced with a private (cloneable) copy.
    private readonly HashSet<System.Windows.Controls.Panel> _ownedBackgrounds = [];

    /// <summary>True when a shader is selected or Shader Only mode is on.</summary>
    public static bool IsShaderActive(bool shaderOnly, string? shaderPreset) =>
        shaderOnly || AudioShaderPresets.IsActive(shaderPreset);

    /// <summary>
    /// Applies the backdrop state. Returns whether a shader is active so the caller can adjust window-specific
    /// chrome (e.g. the window background).
    /// </summary>
    /// <param name="topChrome">Banner / content area hidden in Shader Only mode.</param>
    public bool Apply(
        bool shaderOnly,
        string? shaderPreset,
        IEnumerable<FrameworkElement> projectionPanels,
        UIElement? topChrome,
        AudioShaderHost? shaderHost,
        UIElement? crawlBackdrop,
        WelcomeOverlayHost? welcomeHost)
    {
        bool shaderActive = IsShaderActive(shaderOnly, shaderPreset);
        double bgOpacity = shaderActive ? ShaderBackdropOpacity : 1.0;

        topChrome?.Visibility = shaderOnly ? Visibility.Collapsed : Visibility.Visible;
        shaderHost?.Opacity = shaderOnly ? 1.0 : 0.75;

        foreach (var element in projectionPanels)
        {
            if (element is System.Windows.Controls.Panel p && p.Background != null)
            {
                // Own a private copy so a brush shared via resources is never mutated for other users.
                if (_ownedBackgrounds.Add(p) || p.Background.IsFrozen)
                {
                    p.Background = p.Background.Clone();
                }
                p.Background.Opacity = bgOpacity;
            }
        }

        crawlBackdrop?.Opacity = bgOpacity;
        welcomeHost?.SetShaderBackdrop(shaderActive);
        return shaderActive;
    }
}
#endif
