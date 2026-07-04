using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Models;

namespace Lyracist.ViewModels;

public partial class RotationWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentSinger = string.Empty;

    [ObservableProperty]
    private string _nextSinger = string.Empty;

    [ObservableProperty]
    private string _announcementBanner = string.Empty;

    [ObservableProperty]
    private bool _isAnnouncementVisible;

    public ObservableCollection<Singer> Rotation { get; } = new();

    public void UpdateRotation(List<Singer> singers)
    {
        Rotation.Clear();
        foreach (var s in singers)
        {
            Rotation.Add(s);
        }

        CurrentSinger = singers.FirstOrDefault()?.Name ?? "No Singer";
        NextSinger = singers.Skip(1).FirstOrDefault()?.Name ?? "None";
    }

    public void HighlightSinger(Singer singer)
    {
        CurrentSinger = singer.Name;
        // The next singer is the first singer in the queue who is not the current singer
        NextSinger = Rotation.FirstOrDefault(s => s.Name != singer.Name)?.Name ?? "None";
    }
}
