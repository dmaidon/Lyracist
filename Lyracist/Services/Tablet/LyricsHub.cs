using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Linq;
using Lyracist.ViewModels;

namespace Lyracist.Services.Tablet;

public class LyricsHub : Hub
{
    private readonly RotationViewModel _rotation;
    private readonly KaraokeViewModel _karaoke;

    public LyricsHub(RotationViewModel rotation, KaraokeViewModel karaoke)
    {
        _rotation = rotation;
        _karaoke = karaoke;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        // Push initial state to this connection immediately
        var queueList = _rotation.Rotation.Select(s => new {
            name = s.Name,
            songTitle = s.SongTitle,
            artist = s.Artist,
            key = s.Key,
            source = s.Source
        }).ToList();

        await Clients.Caller.SendAsync("QueueUpdated", queueList);

        await Clients.Caller.SendAsync("ActiveSingerUpdated", new {
            name = _karaoke.NowSingingName,
            song = _karaoke.NowSingingSong,
            isPlaying = _karaoke.IsPlaying
        });

        await Clients.Caller.SendAsync("NextSingerUpdated", new {
            name = _karaoke.NextUpName,
            song = _karaoke.NextUpSong
        });
    }

    public async Task Subscribe()
    {
        // Clients call this to initiate/register subscriptions (optional)
        await Task.CompletedTask;
    }
}
