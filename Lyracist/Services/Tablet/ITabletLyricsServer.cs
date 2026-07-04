using System.Threading;
using System.Threading.Tasks;

namespace Lyracist.Services.Tablet;

public interface ITabletLyricsServer
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    Task BroadcastLyricsAsync(LyricsMessage message);
}
