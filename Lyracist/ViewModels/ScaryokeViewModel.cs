using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Core.Interfaces;
using Lyracist.Services.Display;

namespace Lyracist.ViewModels;

public partial class ScaryokeViewModel : BaseViewModel
{
    /// <summary>The 12 wheel segments, clockwise from the top.</summary>
    public static readonly string[] WheelCategories =
    {
        "Gender Bender", "Elvis", "Country", "Rock & Roll", "Pop", "80s Music",
        "70s Music", "60s Oldies", "Singer's Choice", "Spin Again", "DJ's Choice", "Motown"
    };

    // Library search terms per genre segment; null = a challenge with no
    // forced song lookup.
    private static readonly Dictionary<string, string?> SearchTerms = new()
    {
        ["Gender Bender"] = null,
        ["Elvis"] = "Elvis",
        ["Country"] = "Country",
        ["Rock & Roll"] = "Rock",
        ["Pop"] = "Pop",
        ["80s Music"] = "80s",
        ["70s Music"] = "70s",
        ["60s Oldies"] = "60s",
        ["Motown"] = "Motown"
    };

    private readonly ILibraryService _library;
    private readonly IDisplayService _display;
    private readonly RotationViewModel _rotation;

    [ObservableProperty]
    private string _resultText = "Spin the wheel to seal a singer's fate!";

    public ScaryokeViewModel(ILibraryService library, IDisplayService display, RotationViewModel rotation)
    {
        _library = library;
        _display = display;
        _rotation = rotation;
    }

    /// <summary>
    /// Handles the category the wheel landed on. Returns true when the wheel
    /// should immediately spin again ("Spin Again" segment).
    /// </summary>
    public bool ApplyResult(string category)
    {
        if (category == "Spin Again")
        {
            ResultText = "🔁 Spin Again!";
            _display.ShowLyricsOverlay("🎃 SCARYOKE: Spin Again!", 4);
            return true;
        }

        var singer = _rotation.SelectedSinger ?? _rotation.Rotation.FirstOrDefault();
        string singerName = singer?.Name ?? "The singer";

        switch (category)
        {
            case "Singer's Choice":
                ResultText = $"{singerName} sings whatever they want!";
                break;

            case "DJ's Choice":
                ResultText = $"The DJ picks {singerName}'s fate!";
                break;

            case "Gender Bender":
                ResultText = $"{singerName} must sing a song made famous by the opposite gender!";
                break;

            default:
                ResultText = AssignRandomSong(category, singer, singerName);
                break;
        }

        _display.ShowLyricsOverlay($"🎃 SCARYOKE: {category}!\n{ResultText}", 10);
        return false;
    }

    private string AssignRandomSong(string category, Models.Singer? singer, string singerName)
    {
        string? term = SearchTerms.GetValueOrDefault(category);
        var matches = term == null ? new List<Models.KaraokeSong>() : _library.Search(term).ToList();

        if (matches.Count == 0)
        {
            return $"{category}: nothing matching in the library — {singerName} picks!";
        }

        var song = matches[Random.Shared.Next(matches.Count)];
        if (singer != null)
        {
            singer.SongTitle = song.Title;
            singer.Artist = song.Artist;
            _display.UpdateRotation(_rotation.Rotation.ToList());
        }

        return $"{singerName} sings: {song.Title} — {song.Artist}";
    }
}
