using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.Services.Integration;

namespace Lyracist.Windows;

public partial class OccasionSearchWindow : Window
{
    private readonly IOccasionService _occasions;
    private readonly ILibraryService _library;
    private readonly IPartyTymeService _partyTyme;
    private readonly ExternalLinkService _externalLinkService;
    private readonly int _categoryId;
    private readonly Action _callback;

    public ObservableCollection<KaraokeSong> LocalSongs { get; } = [];
    public ObservableCollection<PartyTymeTrack> PartyTymeSongs { get; } = [];
    public ObservableCollection<ExternalTrack> ExternalSongs { get; } = [];

    public OccasionSearchWindow(
        IOccasionService occasions,
        ILibraryService library,
        IPartyTymeService partyTyme,
        int categoryId,
        Action callback)
    {
        _occasions = occasions;
        _library = library;
        _partyTyme = partyTyme;
        _externalLinkService = new ExternalLinkService();
        _categoryId = categoryId;
        _callback = callback;

        InitializeComponent();

        LocalResultsList.ItemsSource = LocalSongs;
        PartyTymeResultsList.ItemsSource = PartyTymeSongs;
        ExternalResultsList.ItemsSource = ExternalSongs;
    }

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformSearch();
        }
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        PerformSearch();
    }

    private async void PerformSearch()
    {
        string query = SearchBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(query)) return;

        // 1. Local Search
        LocalSongs.Clear();
        var localResults = _library.Search(query).ToList();
        foreach (var song in localResults)
        {
            LocalSongs.Add(song);
        }
        NoLocalText.Visibility = LocalSongs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // 2. Party Tyme Search
        PartyTymeSongs.Clear();
        if (_partyTyme.IsAuthenticated)
        {
            PartyTymeLoading.Visibility = Visibility.Visible;
            NoPartyTymeText.Visibility = Visibility.Collapsed;
            try
            {
                var ptResults = await _partyTyme.SearchCatalogAsync(query);
                foreach (var track in ptResults)
                {
                    PartyTymeSongs.Add(track);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Party Tyme Occasion Search failed: {ex.Message}");
            }
            finally
            {
                PartyTymeLoading.Visibility = Visibility.Collapsed;
            }
            NoPartyTymeText.Visibility = PartyTymeSongs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            NoPartyTymeText.Text = "Party Tyme is not authenticated.";
            NoPartyTymeText.Visibility = Visibility.Visible;
        }

        // 3. External Streams Search
        ExternalSongs.Clear();
        ExternalLoading.Visibility = Visibility.Visible;
        NoExternalText.Visibility = Visibility.Collapsed;
        try
        {
            var extResults = await _externalLinkService.SearchAsync(query, "All");
            foreach (var track in extResults)
            {
                ExternalSongs.Add(track);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"External Occasion Search failed: {ex.Message}");
        }
        finally
        {
            ExternalLoading.Visibility = Visibility.Collapsed;
        }
        NoExternalText.Visibility = ExternalSongs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddSelected_Click(object sender, RoutedEventArgs e)
    {
        // Check which list has a selected item
        if (LocalResultsList.SelectedItem is KaraokeSong localSong)
        {
            string name = $"{localSong.Artist} - {localSong.Title}";
            _occasions.AddItem(_categoryId, name, localSong.AudioPath);
            _callback.Invoke();
            Close();
        }
        else if (PartyTymeResultsList.SelectedItem is PartyTymeTrack ptTrack)
        {
            string name = $"{ptTrack.Artist} - {ptTrack.Title} (Party Tyme)";
            string path = $"PartyTyme:{ptTrack.TrackId}";
            _occasions.AddItem(_categoryId, name, path);
            _callback.Invoke();
            Close();
        }
        else if (ExternalResultsList.SelectedItem is ExternalTrack extTrack)
        {
            string name = $"{extTrack.Artist} - {extTrack.Title} ({extTrack.Source})";
            _occasions.AddItem(_categoryId, name, extTrack.Url);
            _callback.Invoke();
            Close();
        }
        else
        {
            System.Windows.MessageBox.Show("Please select a track from one of the tabs first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
