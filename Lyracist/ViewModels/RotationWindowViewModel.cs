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

        foreach (var s in Rotation)
        {
            s.IsCurrent = false;
            s.IsNext = false;
        }

        var activeSingers = singers.Where(s => !s.IsPaused).ToList();
        if (activeSingers.Count > 0)
        {
            var now = activeSingers[0];
            var match = Rotation.FirstOrDefault(s => s.Name == now.Name);
            if (match != null) match.IsCurrent = true;
            CurrentSinger = now.Name;
        }
        else
        {
            CurrentSinger = "No Singer";
        }

        if (activeSingers.Count > 1)
        {
            var next = activeSingers[1];
            var match = Rotation.FirstOrDefault(s => s.Name == next.Name);
            if (match != null) match.IsNext = true;
            NextSinger = next.Name;
        }
        else
        {
            NextSinger = "None";
        }
    }

    public void HighlightSinger(Singer singer)
    {
        foreach (var s in Rotation)
        {
            s.IsCurrent = false;
            s.IsNext = false;
        }

        var currentMatch = Rotation.FirstOrDefault(s => s.Name == singer.Name);
        if (currentMatch != null) currentMatch.IsCurrent = true;
        CurrentSinger = singer.Name;

        // The next singer is the first active/non-paused singer in the queue who is not the current singer
        var activeSingers = Rotation.Where(s => !s.IsPaused && s.Name != singer.Name).ToList();
        var next = activeSingers.FirstOrDefault();
        if (next != null)
        {
            var nextMatch = Rotation.FirstOrDefault(s => s.Name == next.Name);
            if (nextMatch != null) nextMatch.IsNext = true;
            NextSinger = next.Name;
        }
        else
        {
            NextSinger = "None";
        }
    }
}
