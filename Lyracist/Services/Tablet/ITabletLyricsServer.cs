// Edited on Aug 20, 2026 @ 09:54:20 -> Add LoadSong methods to ITabletLyricsServer for mobile sync
using System.Threading;
using System.Threading.Tasks;
using Lyracist.Models;

namespace Lyracist.Services.Tablet;

public interface ITabletLyricsServer
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    Task BroadcastLyricsAsync(LyricsMessage message);
    void LoadSong(string songTitle, string artist);
    void LoadSong(Singer singer);
}
