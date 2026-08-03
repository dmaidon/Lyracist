// Edited on Jul 17, 2026 @ 09:00:00 -> Refactor tablet server and serve static files
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Lyracist.Core.Interfaces;
using Lyracist.ViewModels;
using Lyracist.Models;

namespace Lyracist.Services.Tablet;

public class TabletLyricsServer(
    IRequestService requests,
    RotationViewModel rotation,
    ILibraryService library,
    IOccasionService occasions,
    KaraokeViewModel karaoke,
    Lyracist.Windows.ScaryokeWindow scaryokeWindow) : ITabletLyricsServer
{
    private WebApplication? _webApp;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;
    private readonly IRequestService _requests = requests;
    private readonly RotationViewModel _rotation = rotation;
    private readonly ILibraryService _library = library;
    private readonly IOccasionService _occasions = occasions;
    private readonly KaraokeViewModel _karaoke = karaoke;
    private readonly Lyracist.Windows.ScaryokeWindow _scaryokeWindow = scaryokeWindow;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _singerTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _ratedTokens = new();
    private string _lastRatedPerformanceKey = string.Empty;

    private static System.Net.IPAddress? GetLocalLanIp()
    {
        try
        {
            using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is System.Net.IPEndPoint endPoint)
            {
                return endPoint.Address;
            }
        }
        catch
        {
            try
            {
                foreach (var netInterface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netInterface.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        (netInterface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 ||
                         netInterface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet))
                    {
                        foreach (var ip in netInterface.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                var ipStr = ip.Address.ToString();
                                if (!ipStr.StartsWith("127.") && !ipStr.StartsWith("169.254"))
                                {
                                    return ip.Address;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }
        return null;
    }

    private bool ValidateSingerToken(string singerName, string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(singerName)) return false;
        if (string.IsNullOrWhiteSpace(sessionToken)) return false;

        var normalizedName = singerName.Trim();
        if (normalizedName.Equals("Anonymous", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return true; // Anonymous requests are allowed without token claim
        }

        var associatedToken = _singerTokens.GetOrAdd(normalizedName, sessionToken);
        return associatedToken == sessionToken;
    }

    private string GetCurrentPerformanceKey()
    {
        return $"{_karaoke.NowSingingName}:{_karaoke.NowSingingSong}";
    }

    /// <summary>Payload for POST /api/requests from the singer mobile portal.</summary>
    public record MobileRequestDto(string? SingerName, string? Title, string? Artist, string? Source, string? Key, string? Notes, string? RequestType);

    public record MobileRatingDto(string? SingerName, int Rating);

    public record ScaryokeSpinDto(string? SingerName);

    public record MobileJoinDto(string? SingerName);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_webApp != null)
        {
            return Task.CompletedTask;
        }

        try
        {
            var builder = WebApplication.CreateBuilder();

            // Listen on the configured port only on localhost and the active LAN IP
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(System.Net.IPAddress.Loopback, Lyracist.Core.Helpers.AppSettings.TabletPort);
                var lanIp = GetLocalLanIp();
                if (lanIp != null)
                {
                    options.Listen(lanIp, Lyracist.Core.Helpers.AppSettings.TabletPort);
                }
            });

            // Register SignalR services
            builder.Services.AddSignalR();
            builder.Services.AddSingleton(_requests);
            builder.Services.AddSingleton(_rotation);
            builder.Services.AddSingleton(_library);
            builder.Services.AddSingleton(_occasions);
            builder.Services.AddSingleton(_karaoke);

            // Set minimum logging to warning to avoid flooding standard output/debug window
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            _webApp = builder.Build();

            _rotation.Rotation.CollectionChanged += OnRotationChanged;
            _karaoke.PropertyChanged += OnKaraokePropertyChanged;
            _scaryokeWindow.SpinStarted += OnScaryokeSpinStarted;
            _scaryokeWindow.SpinCompleted += OnScaryokeSpinCompleted;

            // Map the lyrics hub endpoint
            _webApp.MapHub<LyricsHub>("/lyricsHub");

            // Serve static files from TabletClient directory
            var clientPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TabletClient");
            if (Directory.Exists(clientPath))
            {
                _webApp.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(clientPath),
                    RequestPath = "/monitor"
                });

                // Serve index.html directly under /monitor
                _webApp.MapGet("/monitor", async (HttpContext context) =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.SendFileAsync(Path.Combine(clientPath, "index.html"));
                });
            }

            // Mobile portal: join endpoint to claim performer stage name
            _webApp.MapPost("/api/join", (MobileJoinDto dto, HttpContext context) =>
            {
                var sessionToken = context.Request.Headers["X-Session-Token"].ToString();
                if (string.IsNullOrWhiteSpace(dto.SingerName))
                {
                    return Results.BadRequest(new { error = "Name is required." });
                }
                if (string.IsNullOrWhiteSpace(sessionToken))
                {
                    return Results.BadRequest(new { error = "Session token is required." });
                }

                if (ValidateSingerToken(dto.SingerName, sessionToken))
                {
                    return Results.Ok(new { success = true });
                }
                else
                {
                    return Results.Conflict(new { error = "This name has already been claimed by another device." });
                }
            });

            // Mobile portal: song request submission from singers' phones.
            _webApp.MapPost("/api/requests", (MobileRequestDto dto, HttpContext context) =>
            {
                var sessionToken = context.Request.Headers["X-Session-Token"].ToString();
                if (string.IsNullOrWhiteSpace(sessionToken))
                {
                    return Results.BadRequest(new { error = "Session token is required." });
                }
                if (!ValidateSingerToken(dto.SingerName ?? "Anonymous", sessionToken))
                {
                    return Results.Conflict(new { error = "This name has already been claimed by another device." });
                }

                if (string.IsNullOrWhiteSpace(dto.Title))
                {
                    return Results.BadRequest(new { error = "Title is required." });
                }

                var request = _requests.AddRequest(
                    dto.SingerName ?? "Anonymous",
                    dto.Title,
                    dto.Artist ?? string.Empty,
                    dto.Source ?? "Portal",
                    dto.RequestType ?? "Karaoke",
                    dto.Key ?? "0",
                    dto.Notes ?? string.Empty);

                if (Lyracist.Core.Helpers.AppSettings.AutoAcceptRequests)
                {
                    // Mirrors RequestsViewModel.Approve(): Music requests just move to the Approved
                    // queue, Karaoke requests send the singer straight into the rotation. Rotation
                    // mutation must happen on the UI dispatcher thread, unlike the DB-only Approve call.
                    if (request.RequestType == "Music")
                    {
                        _requests.Approve(request.Id);
                        request.Status = "Approved";
                    }
                    else
                    {
                        _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                _rotation.AddSinger(request.SingerName, request.Title, request.Artist, request.Key, request.Notes, request.Source);
                                _requests.MarkQueued(request.Id);
                            }
                            catch (Exception ex)
                            {
                                Lyracist.Shared.Globals.LogError("Lyracist", "Failed to auto-accept karaoke request into rotation", ex);
                            }
                        }));
                        request.Status = "Queued";
                    }
                }

                return Results.Ok(request);
            });

            _webApp.MapPost("/api/rate", async (MobileRatingDto dto, HttpContext context) =>
            {
                var sessionToken = context.Request.Headers["X-Session-Token"].ToString();
                if (string.IsNullOrWhiteSpace(sessionToken))
                {
                    return Results.BadRequest(new { error = "Session token is required." });
                }

                if (string.IsNullOrWhiteSpace(dto.SingerName) || dto.SingerName == "None")
                {
                    return Results.BadRequest(new { error = "Singer name is required." });
                }

                if (dto.Rating < 1 || dto.Rating > 5)
                {
                    return Results.BadRequest(new { error = "Rating must be between 1 and 5." });
                }

                // Prevent self-rating
                if (_singerTokens.TryGetValue(dto.SingerName, out var ownerToken) && ownerToken == sessionToken)
                {
                    return Results.BadRequest(new { error = "You cannot rate your own performance." });
                }

                // Enforce duplicate rating check per active performance
                string perfKey = GetCurrentPerformanceKey();
                if (perfKey != _lastRatedPerformanceKey)
                {
                    _ratedTokens.Clear();
                    _lastRatedPerformanceKey = perfKey;
                }

                if (_ratedTokens.ContainsKey(sessionToken))
                {
                    return Results.BadRequest(new { error = "You have already rated this performance." });
                }

                try
                {
                    using var dbContext = new Lyracist.Data.LyracistDbContext();
                    var dbSinger = await dbContext.Singers.FirstOrDefaultAsync(s => s.Name == dto.SingerName);
                    if (dbSinger != null)
                    {
                        // Commit rating registration
                        _ratedTokens[sessionToken] = 0;

                        int pointsEarned = dto.Rating * 10;
                        dbSinger.Score += pointsEarned;
                        
                        int totalPoints = dbSinger.RatingPoints + dto.Rating;
                        int newCount = dbSinger.RatingCount + 1;
                        dbSinger.RatingPoints = totalPoints;
                        dbSinger.RatingCount = newCount;
                        dbSinger.AverageRating = Math.Round((double)totalPoints / newCount, 1);

                        dbContext.Singers.Update(dbSinger);
                        await dbContext.SaveChangesAsync();

                        // Update active rotation memory model asynchronously
                        _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                var activeSinger = _rotation.Rotation.FirstOrDefault(s => s.Name.Equals(dto.SingerName, StringComparison.OrdinalIgnoreCase));
                                if (activeSinger != null)
                                {
                                    activeSinger.Score = dbSinger.Score;
                                    activeSinger.AverageRating = dbSinger.AverageRating;
                                    activeSinger.RatingCount = dbSinger.RatingCount;
                                }
                                _rotation.NotifyRotationReordered();
                            }
                            catch (Exception ex)
                            {
                                Lyracist.Shared.Globals.LogError("Lyracist", "Failed to update rotation memory model on dispatcher thread", ex);
                            }
                        }));

                        // Rebroadcast updated rating live to other performers
                        _ = BroadcastActiveSingerAsync();

                        return Results.Ok(new { success = true, score = dbSinger.Score, avgRating = dbSinger.AverageRating });
                    }
                    return Results.NotFound(new { error = "Singer not found." });
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            _webApp.MapGet("/api/scaryoke/enabled", () => Results.Json(new { enabled = _karaoke.IsScaryokeMode }));

            _webApp.MapGet("/api/scaryoke/categories", () =>
            {
                if (!_karaoke.IsScaryokeMode)
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }
                return Results.Json(_scaryokeWindow.ViewModel.WheelSegments);
            });

            _webApp.MapPost("/api/scaryoke/spin", (ScaryokeSpinDto? dto, HttpContext context) =>
            {
                var sessionToken = context.Request.Headers["X-Session-Token"].ToString();
                if (string.IsNullOrWhiteSpace(sessionToken))
                {
                    return Results.BadRequest(new { error = "Session token is required." });
                }
                if (!_karaoke.IsScaryokeMode)
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                bool isCurrentPerformer =
                    !string.IsNullOrWhiteSpace(dto?.SingerName) &&
                    !_karaoke.NowSingingName.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                    _karaoke.NowSingingName.Equals(dto!.SingerName, StringComparison.OrdinalIgnoreCase);

                if (!isCurrentPerformer)
                {
                    return Results.Json(new { error = "Only the current performer can spin the wheel." }, statusCode: StatusCodes.Status403Forbidden);
                }

                // Check token ownership of the current performer
                var performerName = dto?.SingerName;
                if (performerName != null && _singerTokens.TryGetValue(performerName, out var ownerToken) && ownerToken != sessionToken)
                {
                    return Results.Json(new { error = "Session token does not match the performer." }, statusCode: StatusCodes.Status403Forbidden);
                }

                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var wheel = App.AppHost.Services.GetRequiredService<Lyracist.Windows.ScaryokeWindow>();
                    wheel.RebuildWheel();
                    wheel.Show();
                    wheel.Activate();
                    wheel.Spin();
                }));
                return Results.Ok(new { success = true });
            });

            _webApp.MapGet("/api/requests", () => Results.Json(_requests.GetPending()));

            _webApp.MapGet("/api/logs", (HttpContext context) =>
            {
                var remoteIp = context.Connection.RemoteIpAddress;
                if (remoteIp != null && !System.Net.IPAddress.IsLoopback(remoteIp))
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                try
                {
                    var list = new List<string>();
                    string startupFolder = AppDomain.CurrentDomain.BaseDirectory;
                    string logDir = Path.Combine(startupFolder, "Logs");
                    
                    string appLogPath = Path.Combine(logDir, "app.log");
                    if (File.Exists(appLogPath))
                    {
                        list.Add("=== App Log ===");
                        var lines = File.ReadLines(appLogPath).TakeLast(50).ToList();
                        list.AddRange(lines);
                    }

                    string errFileName = $"err_{DateTime.Now:MMMdd}.log";
                    string errPath = Path.Combine(logDir, errFileName);
                    if (File.Exists(errPath))
                    {
                        list.Add(string.Empty);
                        list.Add("=== Error Log ===");
                        var lines = File.ReadLines(errPath).TakeLast(50).ToList();
                        list.AddRange(lines);
                    }

                    return Results.Ok(list);
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            _webApp.MapGet("/api/queue", (RotationViewModel rotation) =>
            {
                var queueList = rotation.Rotation.Select(s => new
                {
                    name = s.Name,
                    songTitle = s.SongTitle,
                    artist = s.Artist,
                    key = s.Key,
                    source = s.Source
                }).ToList();
                return Results.Json(queueList);
            });

            _webApp.MapGet("/api/catalog", async (string? query, string? scope, ILibraryService library, IOccasionService occasions) =>
            {
                var list = new List<object>();

                if (string.Equals(scope, "music", StringComparison.OrdinalIgnoreCase))
                {
                    var musicMatch = library.GetBackgroundMusicSongs();
                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        musicMatch = musicMatch.Where(s =>
                            s.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            s.Artist.Contains(query, StringComparison.OrdinalIgnoreCase));
                    }
                    foreach (var song in musicMatch.Take(50))
                    {
                        list.Add(new { title = song.Title, artist = song.Artist, source = "Local" });
                    }

                    return Results.Json(list.Take(50));
                }

                var localMatch = await library.SearchAsync(query ?? "");
                foreach (var song in localMatch.Take(30))
                {
                    list.Add(new { title = song.Title, artist = song.Artist, source = "Local" });
                }

                var tree = occasions.GetMenuTree();
                void Traverse(OccasionNode node)
                {
                    if (node.IsItem)
                    {
                        if (string.IsNullOrEmpty(query) || node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            list.Add(new { title = node.Name, artist = "Special Occasion", source = "Occasion" });
                        }
                    }
                    foreach (var child in node.Children)
                    {
                        Traverse(child);
                    }
                }
                foreach (var root in tree)
                {
                    Traverse(root);
                }

                return Results.Json(list.Take(50));
            });

            _webApp.MapGet("/", () => Results.Content(GetMobilePortalHtml(), "text/html"));
            _webApp.MapGet("/join", () => Results.Content(GetMobilePortalHtml(), "text/html"));
            _webApp.MapGet("/request", () => Results.Content(GetMobilePortalHtml(), "text/html"));

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
        _rotation.Rotation.CollectionChanged -= OnRotationChanged;
        _karaoke.PropertyChanged -= OnKaraokePropertyChanged;
        _scaryokeWindow.SpinStarted -= OnScaryokeSpinStarted;
        _scaryokeWindow.SpinCompleted -= OnScaryokeSpinCompleted;

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

    private static string? _mobilePortalHtml;
    private static string GetMobilePortalHtml()
    {
        if (_mobilePortalHtml == null)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TabletClient", "mobile.html");
            if (File.Exists(path))
            {
                _mobilePortalHtml = File.ReadAllText(path);
            }
            else
            {
                _mobilePortalHtml = "<h1>Mobile Portal file not found.</h1>";
            }
        }
        return _mobilePortalHtml;
    }

    private void OnRotationChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _ = BroadcastQueueAsync();
    }

    private void OnKaraokePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KaraokeViewModel.NowSingingName) ||
            e.PropertyName == nameof(KaraokeViewModel.NowSingingSong) ||
            e.PropertyName == nameof(KaraokeViewModel.IsPlaying))
        {
            _ = BroadcastActiveSingerAsync();
        }
        else if (e.PropertyName == nameof(KaraokeViewModel.NextUpName) ||
                 e.PropertyName == nameof(KaraokeViewModel.NextUpSong))
        {
            _ = BroadcastNextSingerAsync();
        }
        else if (e.PropertyName == nameof(KaraokeViewModel.IsScaryokeMode))
        {
            _ = BroadcastScaryokeAvailabilityAsync();
        }
    }

    private async Task BroadcastScaryokeAvailabilityAsync()
    {
        if (_webApp == null) return;
        try
        {
            var hubContext = _webApp.Services.GetRequiredService<IHubContext<LyricsHub>>();
            await hubContext.Clients.All.SendAsync("ScaryokeAvailabilityChanged", new { enabled = _karaoke.IsScaryokeMode });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error broadcasting scaryoke availability: {ex.Message}");
        }
    }

    private async Task BroadcastQueueAsync()
    {
        if (_webApp == null) return;
        try
        {
            var hubContext = _webApp.Services.GetRequiredService<IHubContext<LyricsHub>>();
            var queueList = _rotation.Rotation.Select(s => new
            {
                name = s.Name,
                songTitle = s.SongTitle,
                artist = s.Artist,
                key = s.Key,
                source = s.Source
            }).ToList();
            await hubContext.Clients.All.SendAsync("QueueUpdated", queueList);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error broadcasting queue: {ex.Message}");
        }
    }

    private async Task BroadcastActiveSingerAsync()
    {
        if (_webApp == null) return;
        try
        {
            var hubContext = _webApp.Services.GetRequiredService<IHubContext<LyricsHub>>();
            double avgRating = 0.0;
            var activeSinger = _rotation.Rotation.FirstOrDefault(s => s.Name.Equals(_karaoke.NowSingingName, StringComparison.OrdinalIgnoreCase));
            if (activeSinger != null)
            {
                avgRating = activeSinger.AverageRating;
            }

            await hubContext.Clients.All.SendAsync("ActiveSingerUpdated", new
            {
                name = _karaoke.NowSingingName,
                song = _karaoke.NowSingingSong,
                isPlaying = _karaoke.IsPlaying,
                avgRating = avgRating,
                isRatingSystemEnabled = Lyracist.Core.Helpers.AppSettings.IsRatingSystemEnabled,
                ratingSymbol = Lyracist.Core.Helpers.AppSettings.ActiveRatingIconSymbol
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error broadcasting active singer: {ex.Message}");
        }
    }

    private async Task BroadcastNextSingerAsync()
    {
        if (_webApp == null) return;
        try
        {
            var hubContext = _webApp.Services.GetRequiredService<IHubContext<LyricsHub>>();
            await hubContext.Clients.All.SendAsync("NextSingerUpdated", new
            {
                name = _karaoke.NextUpName,
                song = _karaoke.NextUpSong
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error broadcasting next singer: {ex.Message}");
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

    private void OnScaryokeSpinStarted(object? sender, EventArgs e)
    {
        _ = BroadcastScaryokeSpinStartedAsync();
    }

    private void OnScaryokeSpinCompleted(object? sender, string category)
    {
        _ = BroadcastScaryokeSpinCompletedAsync(category);
    }

    private async Task BroadcastScaryokeSpinStartedAsync()
    {
        if (_webApp == null) return;
        try
        {
            var hubContext = _webApp.Services.GetRequiredService<IHubContext<LyricsHub>>();
            double finalAngle = _scaryokeWindow.TargetAngle % 360;
            var categories = _scaryokeWindow.ViewModel.WheelSegments;
            await hubContext.Clients.All.SendAsync("ScaryokeSpinStarted", finalAngle, categories);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error broadcasting scaryoke spin start: {ex.Message}");
        }
    }

    private async Task BroadcastScaryokeSpinCompletedAsync(string category)
    {
        if (_webApp == null) return;
        try
        {
            var hubContext = _webApp.Services.GetRequiredService<IHubContext<LyricsHub>>();
            await hubContext.Clients.All.SendAsync("ScaryokeSpinCompleted", category);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error broadcasting scaryoke spin complete: {ex.Message}");
        }
    }
}
