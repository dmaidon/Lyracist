using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Models;
using Lyracist.Services.Display;

namespace Lyracist.ViewModels;

public partial class RotationViewModel : BaseViewModel
{
    private readonly IDisplayService _display;

    public ObservableCollection<Singer> Rotation { get; } = new();

    [ObservableProperty]
    private Singer? _selectedSinger;

    [ObservableProperty]
    private string _newSingerName = string.Empty;

    [ObservableProperty]
    private string _newSingerNotes = string.Empty;

    [ObservableProperty]
    private string _newSingerKey = "0";

    public RotationViewModel(IDisplayService display)
    {
        _display = display;

        // Seed default singer rotation values with song information
        Rotation.Add(new Singer { Name = "Alice", Key = "+1", Notes = "Sings soprano, prefers classic pop", SongTitle = "Sweet Caroline", Artist = "Neil Diamond" });
        Rotation.Add(new Singer { Name = "Bob", Key = "-2", Notes = "Prefers baritone classic rock", SongTitle = "Hotel California", Artist = "Eagles" });
        Rotation.Add(new Singer { Name = "Charlie", Key = "0", Notes = "First time singing today", SongTitle = "Billie Jean", Artist = "Michael Jackson" });
    }

    public void AddSinger(string name, string title, string artist, string key, string notes)
    {
        Rotation.Add(new Singer
        {
            Name = name,
            SongTitle = title,
            Artist = artist,
            Key = key,
            Notes = notes
        });

        _display.UpdateRotation(Rotation.ToList());
    }

    [RelayCommand]
    private void AddSinger()
    {
        if (string.IsNullOrWhiteSpace(NewSingerName))
            return;

        Rotation.Add(new Singer
        {
            Name = NewSingerName,
            Notes = NewSingerNotes,
            Key = NewSingerKey
        });

        // Reset input properties
        NewSingerName = string.Empty;
        NewSingerNotes = string.Empty;
        NewSingerKey = "0";

        _display.UpdateRotation(Rotation.ToList());
    }

    [RelayCommand]
    private void RemoveSinger()
    {
        if (SelectedSinger != null)
        {
            Rotation.Remove(SelectedSinger);
            SelectedSinger = null;

            _display.UpdateRotation(Rotation.ToList());
        }
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedSinger == null)
            return;

        int index = Rotation.IndexOf(SelectedSinger);
        if (index > 0)
        {
            var singer = SelectedSinger;
            Rotation.RemoveAt(index);
            Rotation.Insert(index - 1, singer);
            SelectedSinger = singer;

            _display.UpdateRotation(Rotation.ToList());
        }
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedSinger == null)
            return;

        int index = Rotation.IndexOf(SelectedSinger);
        if (index < Rotation.Count - 1)
        {
            var singer = SelectedSinger;
            Rotation.RemoveAt(index);
            Rotation.Insert(index + 1, singer);
            SelectedSinger = singer;

            _display.UpdateRotation(Rotation.ToList());
        }
    }

    [RelayCommand]
    private void MarkAsSinging()
    {
        if (SelectedSinger == null)
            return;

        _display.ShowRotationWindow();
        _display.HighlightSinger(SelectedSinger);
    }

    [RelayCommand]
    private void ClearRotation()
    {
        Rotation.Clear();
        SelectedSinger = null;

        _display.UpdateRotation(Rotation.ToList());
    }
}
