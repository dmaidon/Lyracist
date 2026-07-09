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

    public RequestsViewModel(IRequestService requests, ILibraryService library, IMediaEngine mediaEngine)
    {
        _requests = requests;
        _library = library;
        _mediaEngine = mediaEngine;

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
        _requests.Approve(SelectedPending.Id);
        StatusMessage = "Request approved.";
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
        var query = $"{SelectedApproved.Title} {SelectedApproved.Artist}".Trim();
        var song = _library.Search(query).FirstOrDefault()
                   ?? _library.Search(SelectedApproved.Title).FirstOrDefault();

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
