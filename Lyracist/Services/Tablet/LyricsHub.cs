using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Linq;
using Lyracist.ViewModels;

namespace Lyracist.Services.Tablet;

public class LyricsHub(RotationViewModel rotation, KaraokeViewModel karaoke) : Hub
{
    private readonly RotationViewModel _rotation = rotation;
    private readonly KaraokeViewModel _karaoke = karaoke;

    public static event System.Action<string>? ReactionReceived;

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        // Push initial state to this connection immediately
        var queueList = _rotation.Rotation.Select(s => new
        {
            name = s.Name,
            songTitle = s.SongTitle,
            artist = s.Artist,
            key = s.Key,
            source = s.Source
        }).ToList();

        await Clients.Caller.SendAsync("QueueUpdated", queueList);

        var activeSinger = _rotation.Rotation.FirstOrDefault(s => s.Name.Equals(_karaoke.NowSingingName, System.StringComparison.OrdinalIgnoreCase));
        double avgRating = activeSinger?.AverageRating ?? 0.0;

        await Clients.Caller.SendAsync("ActiveSingerUpdated", new
        {
            name = _karaoke.NowSingingName,
            song = _karaoke.NowSingingSong,
            isPlaying = _karaoke.IsPlaying,
            avgRating = avgRating,
            isRatingSystemEnabled = Lyracist.Core.Helpers.AppSettings.IsRatingSystemEnabled,
            ratingSymbol = Lyracist.Core.Helpers.AppSettings.ActiveRatingIconSymbol
        });

        await Clients.Caller.SendAsync("NextSingerUpdated", new
        {
            name = _karaoke.NextUpName,
            song = _karaoke.NextUpSong
        });
    }

    public async Task SendReaction(string emoji)
    {
        await Clients.All.SendAsync("ReactionReceived", emoji);
        ReactionReceived?.Invoke(emoji);
    }

    public async Task Subscribe()
    {
        // Clients call this to initiate/register subscriptions (optional)
        await Task.CompletedTask;
    }
}
