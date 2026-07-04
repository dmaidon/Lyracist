using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lyracist.Services.Tablet;

public class TabletLyricsServer : ITabletLyricsServer
{
    private WebApplication? _webApp;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

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
