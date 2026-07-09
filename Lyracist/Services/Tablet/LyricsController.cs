using System;
using System.Threading.Tasks;

namespace Lyracist.Services.Tablet;

public class LyricsController(ITabletLyricsServer server) : ILyricsController
{
    private readonly ITabletLyricsServer _server = server;

    public async Task UpdateLyricsAsync(string title, string currentLine, string nextLine, TimeSpan position)
    {
        var message = new LyricsMessage
        {
            Title = title,
            CurrentLine = currentLine,
            NextLine = nextLine,
            Position = position
        };

        await _server.BroadcastLyricsAsync(message);
    }
}
