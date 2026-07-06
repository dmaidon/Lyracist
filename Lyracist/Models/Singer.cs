using CommunityToolkit.Mvvm.ComponentModel;

namespace Lyracist.Models;

public partial class Singer : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _key = "0"; // e.g., +2, -1, or 0

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _songTitle = string.Empty;

    [ObservableProperty]
    private string _artist = string.Empty;

    [ObservableProperty]
    private string _externalLink = string.Empty;

    [ObservableProperty]
    private string _source = "Local"; // Local, PartyTyme, Spotify, YouTube, Amazon

    [ObservableProperty]
    private int _completedCount = 0;

    [ObservableProperty]
    private bool _isPaused = false;

    [ObservableProperty]
    private bool _isInactive = false;

    [ObservableProperty]
    private bool _isCurrent = false;

    [ObservableProperty]
    private bool _isNext = false;
}
