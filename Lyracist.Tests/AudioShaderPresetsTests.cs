// Created on Nov 24, 2026 @ 09:57:00 -> Unit tests verifying compilation and rendering of all built-in AudioShaderPresets and ShaderToy mainImage wrappers
using System;
using System.Linq;
using Lyracist.Shared;
using SkiaSharp;
using Xunit;

namespace Lyracist.Tests;

public class AudioShaderPresetsTests
{
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
                ["iBass"] = 0.75f,
                ["iMid"] = 0.50f,
                ["iHigh"] = 0.40f,
                ["iEnergy"] = 0.62f
            };

            using var shader = effect!.ToShader(uniforms);
            Assert.NotNull(shader);

            surface!.Canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Shader = shader, BlendMode = SKBlendMode.Src };
            surface.Canvas.DrawRect(0, 0, 64, 36, paint);

            using var snapshot = surface.Snapshot();
            using var pixmap = snapshot.PeekPixels();
            Assert.NotNull(pixmap);

            // Verify at least one pixel has non-black RGB content
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
    public void Normalize_UnknownOrNull_FallsBackToOff()
    {
        Assert.Equal(AudioShaderPresets.Off, AudioShaderPresets.Normalize(null));
        Assert.Equal(AudioShaderPresets.Off, AudioShaderPresets.Normalize(""));
        Assert.Equal(AudioShaderPresets.Off, AudioShaderPresets.Normalize("NonExistentShader123"));
        Assert.Equal("Neon Tunnel", AudioShaderPresets.Normalize("neon tunnel"));
        Assert.Equal("Cyber Grid", AudioShaderPresets.Normalize("CYBER GRID"));
    }
}
