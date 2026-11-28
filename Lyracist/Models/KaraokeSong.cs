// Edited on Nov 27, 2026 @ 12:58:00 -> Add SubfolderCrate property for automatic subfolder category filtering
namespace Lyracist.Models;

public class KaraokeSong
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string AudioPath { get; set; } = string.Empty;
    public string? CdgPath { get; set; }
    public bool HasCdg => !string.IsNullOrEmpty(CdgPath);
    public bool IsKaraoke { get; set; }
    public string SubfolderCrate { get; set; } = string.Empty;
}
