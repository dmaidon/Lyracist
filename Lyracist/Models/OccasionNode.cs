using System.Collections.Generic;

namespace Lyracist.Models;

/// <summary>
/// One node of the Special Occasion menu tree: either a category (possibly
/// nested, e.g. Holiday > Christmas) or a playable item with its own audio
/// settings.
/// </summary>
public class OccasionNode
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsItem { get; set; }
    public string FilePath { get; set; } = string.Empty;

    public double Bass { get; set; }
    public double Treble { get; set; }
    public double Gain { get; set; }

    /// <summary>Nesting depth, used to indent the flat category list in editors.</summary>
    public int Depth { get; set; }
    public string Label => new string(' ', Depth * 4) + Name;

    public List<OccasionNode> Children { get; } = new();
}
