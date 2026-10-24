// Created on Oct 24, 2026 @ 13:00:00 -> Per-category weight/colour and named presets for the Scaryoke wheel
using System.Collections.Generic;

namespace Lyracist.Core.Helpers;

/// <summary>Optional weight and colour override for one Scaryoke wheel category.</summary>
public class ScaryokeCategoryStyle
{
    /// <summary>Relative slice size (1-3).</summary>
    public double Weight { get; set; } = 1;

    /// <summary>Explicit colour; null means "use the palette colour for this position".</summary>
    public string? Color { get; set; }
}

/// <summary>A named snapshot of the wheel's categories and their styles.</summary>
public class ScaryokePreset
{
    public string Name { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = [];
    public Dictionary<string, ScaryokeCategoryStyle> Styles { get; set; } = [];
}
