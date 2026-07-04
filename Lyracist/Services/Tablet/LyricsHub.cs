using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Lyracist.Services.Tablet;

public class LyricsHub : Hub
{
    public async Task Subscribe()
    {
        // Clients call this to initiate/register subscriptions (optional)
        await Task.CompletedTask;
    }
}
