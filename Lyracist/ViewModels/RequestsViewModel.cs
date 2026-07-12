using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using Application = System.Windows.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Interfaces;
using Lyracist.Models;

namespace Lyracist.ViewModels;

public partial class RequestsViewModel : BaseViewModel
{
    private readonly IRequestService _requests;
    private readonly ILibraryService _library;
    private readonly IMediaEngine _mediaEngine;
    private readonly RotationViewModel _rotation;

    public ObservableCollection<RequestInfo> Pending { get; } = [];
    public ObservableCollection<RequestInfo> Approved { get; } = [];
    public ObservableCollection<RequestInfo> History { get; } = [];

    [ObservableProperty]
    private RequestInfo? _selectedPending;

    [ObservableProperty]
    private RequestInfo? _selectedApproved;

    [ObservableProperty]
    private string _newRequestSinger = string.Empty;

    [ObservableProperty]
    private string _newRequestTitle = string.Empty;

    [ObservableProperty]
    private string _newRequestArtist = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public RequestsViewModel(IRequestService requests, ILibraryService library, IMediaEngine mediaEngine, RotationViewModel rotation)
    {
        _requests = requests;
        _library = library;
        _mediaEngine = mediaEngine;
        _rotation = rotation;

        // Mobile portal submissions arrive on Kestrel threads; marshal back.
        _requests.RequestsChanged += (_, _) =>
            Application.Current?.Dispatcher.BeginInvoke(Refresh);

        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        Pending.Clear();
        foreach (var request in _requests.GetPending()) Pending.Add(request);

        Approved.Clear();
        foreach (var request in _requests.GetApproved()) Approved.Add(request);

        History.Clear();
        foreach (var request in _requests.GetHistory()) History.Add(request);
    }

    [RelayCommand]
    private void AddRequest()
    {
        if (string.IsNullOrWhiteSpace(NewRequestTitle)) return;

        _requests.AddRequest(NewRequestSinger, NewRequestTitle, NewRequestArtist);
        NewRequestSinger = string.Empty;
        NewRequestTitle = string.Empty;
        NewRequestArtist = string.Empty;
        StatusMessage = "Request added to the queue.";
    }

    [RelayCommand]
    private void Approve()
    {
        if (SelectedPending == null) return;
        var request = SelectedPending;

        if (request.RequestType == "Music")
        {
            // Music requests are just played back directly — hand off to the Approved queue.
            _requests.Approve(request.Id);
            StatusMessage = "Request approved — ready to play.";
        }
        else
        {
            // Karaoke requests mean the singer performs, so send them straight into the rotation.
            _rotation.AddSinger(request.SingerName, request.Title, request.Artist, request.Key, request.Notes, request.Source);
            _requests.MarkQueued(request.Id);
            StatusMessage = $"{request.SingerName} added to the rotation.";
        }
    }

    [RelayCommand]
    private void Reject()
    {
        if (SelectedPending == null) return;
        _requests.Reject(SelectedPending.Id);
        StatusMessage = "Request rejected.";
    }

    [RelayCommand]
    private async Task PlayApproved()
    {
        if (SelectedApproved == null) return;

        // Try the library for a matching track; play the best hit.
        // Music requests point at background-music tracks, which live outside the karaoke catalog.
        var query = $"{SelectedApproved.Title} {SelectedApproved.Artist}".Trim();
        var song = _library.Search(query).FirstOrDefault()
                   ?? _library.Search(SelectedApproved.Title).FirstOrDefault()
                   ?? _library.GetBackgroundMusicSongs().FirstOrDefault(s =>
                       s.Title.Equals(SelectedApproved.Title, StringComparison.OrdinalIgnoreCase) &&
                       (string.IsNullOrEmpty(SelectedApproved.Artist) || s.Artist.Equals(SelectedApproved.Artist, StringComparison.OrdinalIgnoreCase)))
                   ?? _library.GetBackgroundMusicSongs().FirstOrDefault(s =>
                       s.Title.Contains(SelectedApproved.Title, StringComparison.OrdinalIgnoreCase));

        if (song == null)
        {
            StatusMessage = $"'{SelectedApproved.Title}' not found in the library.";
            return;
        }

        await _mediaEngine.LoadSong(song.AudioPath);
        await _mediaEngine.Play();
        _requests.MarkPlayed(SelectedApproved.Id);
        StatusMessage = $"Playing {song.Title} — {song.Artist}.";
    }

    [RelayCommand]
    private void MarkPlayedWithoutPlaying()
    {
        if (SelectedApproved == null) return;
        _requests.MarkPlayed(SelectedApproved.Id);
        StatusMessage = "Marked as played.";
    }
}
