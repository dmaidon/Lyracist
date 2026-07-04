namespace Lyracist.Models;

public class KaraokeSong
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string AudioPath { get; set; } = string.Empty;
    public string? CdgPath { get; set; }
    public bool HasCdg => !string.IsNullOrEmpty(CdgPath);
}
