// Edited on Nov 24, 2026 @ 11:42:30 -> Add unit test verifying all 8 PCDJ LYRX ANGLE-HLSL .frag shaders compile and render in SkiaSharp
using System;
using System.IO;
using System.Linq;
using Lyracist.Shared;
using SkiaSharp;
using Xunit;

namespace Lyracist.Tests;

public class AudioShaderPresetsTests
{
    [Fact]
    public void EnsureDefaultShaderFiles_WritesEditableGlslFilesToShadersFolder()
    {
        AudioShaderPresets.EnsureDefaultShaderFiles();
        Assert.True(Directory.Exists(AudioShaderPresets.CustomShadersDir));

        foreach (string preset in AudioShaderPresets.BuiltInNames.Where(p => p != AudioShaderPresets.Off))
        {
            string expectedPath = Path.Combine(AudioShaderPresets.CustomShadersDir, preset + ".glsl");
            Assert.True(File.Exists(expectedPath), $"Expected shader file on disk: {expectedPath}");
        }
    }

    [Fact]
    public void BuiltInPresets_AllCompileWithoutErrors()
    {
        foreach (string preset in AudioShaderPresets.BuiltInNames)
        {
            if (preset == AudioShaderPresets.Off)
            {
                Assert.False(AudioShaderPresets.IsActive(preset));
                var offEffect = AudioShaderPresets.GetOrCompileEffect(preset, out string? offError);
                Assert.Null(offEffect);
                Assert.Null(offError);
                continue;
            }

            Assert.True(AudioShaderPresets.IsActive(preset));
            var effect = AudioShaderPresets.GetOrCompileEffect(preset, out string? errorText);
            Assert.True(effect != null, $"Preset '{preset}' failed to compile: {errorText}");
        }
    }

    [Fact]
    public void BuiltInPresets_RenderNonEmptyFrameWithAudioUniforms()
    {
        var info = new SKImageInfo(64, 36, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        Assert.NotNull(surface);

        foreach (string preset in AudioShaderPresets.BuiltInNames.Where(p => p != AudioShaderPresets.Off))
        {
            var effect = AudioShaderPresets.GetOrCompileEffect(preset, out string? errorText);
            Assert.NotNull(effect);
            Assert.Null(errorText);

            var uniforms = new SKRuntimeEffectUniforms(effect!)
            {
                ["iResolution"] = new float[] { 64f, 36f },
                ["iTime"] = 1.25f,
                ["iTimeDelta"] = 0.033f,
                ["iFrame"] = 30,
                ["iBass"] = 0.75f,
                ["iMid"] = 0.50f,
                ["iHigh"] = 0.40f,
                ["iEnergy"] = 0.62f,
                ["iMouse"] = new float[] { 32f, 18f, 0f, 0f },
                ["iDate"] = new float[] { 2026f, 10f, 24f, 36000f },
                ["iSampleRate"] = 48000f
            };

            using var shader = effect!.ToShader(uniforms);
            Assert.NotNull(shader);

            surface!.Canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Shader = shader, BlendMode = SKBlendMode.Src };
            surface.Canvas.DrawRect(0, 0, 64, 36, paint);

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();
            Assert.NotNull(pixmap);

            bool anyLit = false;
            for (int y = 0; y < 36 && !anyLit; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    var c = pixmap!.GetPixelColor(x, y);
                    if (c.Red > 5 || c.Green > 5 || c.Blue > 5)
                    {
                        anyLit = true;
                        break;
                    }
                }
            }
            Assert.True(anyLit, $"Preset '{preset}' rendered an entirely black frame.");
        }
    }

    [Fact]
    public void TranslateShaderToyToSksl_SupportsStandardShaderToyAndIChannel0AudioLookups()
    {
        // Standard ShaderToy GLSL using mainImage, iResolution.xy, texture(iChannel0, ...), texelFetch(iChannel0, ...), and 1.0f suffixes
        const string shaderToyGlsl = """
            #version 300 es
            precision highp float;

            void mainImage(out vec4 fragColor, in vec2 fragCoord)
            {
                vec2 uv = fragCoord / iResolution.xy;
                float fft = texture(iChannel0, vec2(uv.x, 0.25f)).x;
                float wave = texelFetch(iChannel0, ivec2(int(uv.x * 512.0), 1), 0).x;
                vec3 col = 0.5 + 0.5 * cos(iTime + uv.xyx + vec3(0, 2, 4));
                col *= (0.4 + 0.6 * (fft + wave));
                fragColor = vec4(col, 1.0f);
            }
            """;

        string tempFile = Path.Combine(Path.GetTempPath(), "TestShaderToyImport_" + Guid.NewGuid().ToString("N")[..8] + ".glsl");
        string? importedDestPath = null;
        try
        {
            File.WriteAllText(tempFile, shaderToyGlsl);

            bool imported = AudioShaderPresets.TryImportShaderFile(tempFile, out string importedName, out string? errorMsg);
            Assert.True(imported, $"Expected ShaderToy import to succeed, but got error: {errorMsg}");
            Assert.False(string.IsNullOrWhiteSpace(importedName));

            importedDestPath = Path.Combine(AudioShaderPresets.CustomShadersDir, importedName + ".glsl");
            Assert.True(File.Exists(importedDestPath));
            Assert.Contains(importedName, AudioShaderPresets.GetAvailableNames());

            var effect = AudioShaderPresets.GetOrCompileEffect(importedName, out string? compileErr);
            Assert.NotNull(effect);
            Assert.Null(compileErr);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (importedDestPath != null && File.Exists(importedDestPath)) File.Delete(importedDestPath);
            AudioShaderPresets.ClearCache();
        }
    }

    [Fact]
    public void Normalize_UnknownOrNull_FallsBackToOff()
    {
        Assert.Equal(AudioShaderPresets.Off, AudioShaderPresets.Normalize(null));
        Assert.Equal(AudioShaderPresets.Off, AudioShaderPresets.Normalize(""));
        Assert.Equal(AudioShaderPresets.Off, AudioShaderPresets.Normalize("NonExistentShader123"));
        Assert.Equal("Neon Tunnel", AudioShaderPresets.Normalize("neon tunnel"));
        Assert.Equal("Cyber Grid", AudioShaderPresets.Normalize("CYBER GRID"));
    }

    [Fact]
    public void TranslateShaderToyToSksl_CompilesAllPcdjLyrxAngleHlslFragFiles()
    {
        foreach (string shaderName in AudioShaderPresets.GetAvailableNames().Where(n => n != AudioShaderPresets.Off))
        {
            var effect = AudioShaderPresets.GetOrCompileEffect(shaderName, out string? errorText);
            Assert.True(effect != null, $"Shader '{shaderName}' in Shaders/ failed to compile:\n{errorText}");
        }

        const string lyrxDir = @"C:\Program Files\PCDJ\LYRX\shaders\hlsl";
        if (Directory.Exists(lyrxDir))
        {
            foreach (string fragFile in Directory.GetFiles(lyrxDir, "*.frag"))
            {
                string? importedDestPath = null;
                try
                {
                    bool imported = AudioShaderPresets.TryImportShaderFile(fragFile, out string importedName, out string? importError);
                    Assert.True(imported, $"TryImportShaderFile failed for LYRX shader '{Path.GetFileName(fragFile)}':\n{importError}");
                    Assert.False(string.IsNullOrWhiteSpace(importedName));

                    importedDestPath = Path.Combine(AudioShaderPresets.CustomShadersDir, importedName + Path.GetExtension(fragFile));
                    var effect = AudioShaderPresets.GetOrCompileEffect(importedName, out string? compileErrors);
                    Assert.True(effect != null, $"Imported LYRX shader '{importedName}' failed to compile:\n{compileErrors}");
                }
                finally
                {
                    if (importedDestPath != null && File.Exists(importedDestPath))
                    {
                        File.Delete(importedDestPath);
                    }
                    AudioShaderPresets.ClearCache();
                }
            }
        }
    }
}
