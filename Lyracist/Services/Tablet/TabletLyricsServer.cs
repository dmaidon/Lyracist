using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Lyracist.Core.Interfaces;

namespace Lyracist.Services.Tablet;

public class TabletLyricsServer : ITabletLyricsServer
{
    private WebApplication? _webApp;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;
    private readonly IRequestService _requests;

    public TabletLyricsServer(IRequestService requests)
    {
        _requests = requests;
    }

    /// <summary>Payload for POST /api/requests from the singer mobile portal.</summary>
    public record MobileRequestDto(string? SingerName, string? Title, string? Artist, string? Source);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_webApp != null)
        {
            return Task.CompletedTask;
        }

        try
        {
            var builder = WebApplication.CreateBuilder();
            
            // Listen on port 5005 across all interfaces (allows tablet connection over LAN)
            builder.WebHost.UseUrls("http://*:5005");
            
            // Register SignalR services
            builder.Services.AddSignalR();
            
            // Set minimum logging to warning to avoid flooding standard output/debug window
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            _webApp = builder.Build();
            
            // Map the lyrics hub endpoint
            _webApp.MapHub<LyricsHub>("/lyricsHub");

            // Mobile portal: song request submission from singers' phones.
            // Full portal pages come in Stage 6; these endpoints and the
            // minimal form page make requests work today over the LAN.
            _webApp.MapPost("/api/requests", (MobileRequestDto dto) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Title))
                {
                    return Results.BadRequest(new { error = "Title is required." });
                }

                var request = _requests.AddRequest(
                    dto.SingerName ?? "Anonymous",
                    dto.Title,
                    dto.Artist ?? string.Empty,
                    dto.Source ?? "Portal");
                return Results.Ok(request);
            });

            _webApp.MapGet("/api/requests", () => Results.Json(_requests.GetPending()));

            _webApp.MapGet("/request", () => Results.Content(RequestFormHtml, "text/html"));

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // Start Kestrel in a background task to prevent blocking the WPF UI thread
            _serverTask = Task.Run(async () =>
            {
                try
                {
                    await _webApp.RunAsync();
                }
                catch (OperationCanceledException)
                {
                    // Clean cancellation, safe to ignore
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Tablet Lyrics Kestrel Server error: {ex.Message}");
                }
            }, token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize Tablet Lyrics Server: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        if (_webApp != null)
        {
            try
            {
                await _webApp.StopAsync(cancellationToken);
                await _webApp.DisposeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during Tablet Lyrics Server cleanup: {ex.Message}");
            }
            finally
            {
                _webApp = null;
            }
        }

        if (_serverTask != null)
        {
            try
            {
                await _serverTask;
            }
            catch
            {
                // Ignore background thread exceptions on exit
            }
            finally
            {
                _serverTask = null;
            }
        }
    }

    private const string RequestFormHtml = """
        <!DOCTYPE html>
        <html><head><meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Lyracist — Request a Song</title>
        <style>
          body{font-family:sans-serif;background:#1e1e1e;color:#fff;max-width:420px;margin:0 auto;padding:24px}
          h1{color:#FF4081;font-size:1.5em}
          input{width:100%;box-sizing:border-box;padding:12px;margin:6px 0 14px;border-radius:8px;border:1px solid #3a3a3a;background:#2c2c2c;color:#fff;font-size:1em}
          button{width:100%;padding:14px;border:none;border-radius:8px;background:#FF4081;color:#fff;font-size:1.1em;font-weight:bold}
          #msg{margin-top:14px;font-size:.95em}
        </style></head><body>
        <h1>🎤 Request a Song</h1>
        <label>Your name</label><input id="singer" placeholder="Who's asking?">
        <label>Song title</label><input id="title" placeholder="Song title (required)">
        <label>Artist</label><input id="artist" placeholder="Artist">
        <button onclick="send()">Send Request</button>
        <div id="msg"></div>
        <script>
        async function send(){
          const title=document.getElementById('title').value.trim();
          if(!title){document.getElementById('msg').textContent='Please enter a song title.';return;}
          const res=await fetch('/api/requests',{method:'POST',headers:{'Content-Type':'application/json'},
            body:JSON.stringify({singerName:document.getElementById('singer').value,title:title,artist:document.getElementById('artist').value,source:'Portal'})});
          document.getElementById('msg').textContent=res.ok?'Request sent! 🎶':'Something went wrong — try again.';
          if(res.ok){document.getElementById('title').value='';document.getElementById('artist').value='';}
        }
        </script></body></html>
        """;

    public async Task BroadcastLyricsAsync(LyricsMessage message)
    {
        if (_webApp == null) return;

        try
        {
            var hubContext = _webApp.Services.GetRequiredService<IHubContext<LyricsHub>>();
            await hubContext.Clients.All.SendAsync("LyricsUpdated", message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error broadcasting lyrics: {ex.Message}");
        }
    }
}
