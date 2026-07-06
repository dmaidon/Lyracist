using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Core.Interfaces;
using Lyracist.Services.Display;

namespace Lyracist.ViewModels;

public partial class ScaryokeViewModel : BaseViewModel
{
    /// <summary>The wheel segments, clockwise from the top.</summary>
    public static string[] WheelCategories => Core.Helpers.AppSettings.ScaryokeCategories.ToArray();

    private readonly IDisplayService _display;
    private readonly RotationViewModel _rotation;

    [ObservableProperty]
    private string _resultText = "Spin the wheel to seal a singer's fate!";

    public ScaryokeViewModel(IDisplayService display, RotationViewModel rotation)
    {
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
                ResultText = $"{singerName} picks any song from: {category}!";
                break;
        }

        _display.ShowLyricsOverlay($"🎃 SCARYOKE: {category}!\n{ResultText}", 10);
        return false;
    }
}
