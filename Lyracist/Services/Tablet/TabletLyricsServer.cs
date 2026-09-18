// Edited on Sep 17, 2026 @ 23:31:00 -> Include isSkipped in queue payload
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
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

    // Name -> (claiming device's token, sliding expiry). A claim frees itself up for a new
    // device to take once it goes stale, instead of permanently owning the name until the
    // server process restarts.
    private sealed record TokenClaim(string Token, DateTime ExpiresAtUtc);
    private static readonly TimeSpan TokenClaimTtl = TimeSpan.FromHours(6);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, TokenClaim> _singerTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _ratedTokens = new();
    private string _lastRatedPerformanceKey = string.Empty;
    private readonly SemaphoreSlim _ratingLock = new(1, 1);

    private const string MobilePortalWritePolicy = "mobile-portal-write";
    private const string MobilePortalSearchPolicy = "mobile-portal-search";

    // Generous caps on mobile-submitted text: enough for any real request, small enough that a
    // buggy or malicious phone can't bloat the request queue/DB with oversized repeated payloads.
    private const int MaxNameLength = 100;
    private const int MaxTitleOrArtistLength = 200;
    private const int MaxNotesLength = 500;
    private const int MaxShortFieldLength = 50;

    private static bool ExceedsLength(string? value, int max) => value != null && value.Length > max;

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

        var now = DateTime.UtcNow;
        var claim = _singerTokens.AddOrUpdate(
            normalizedName,
            _ => new TokenClaim(sessionToken, now + TokenClaimTtl),
            (_, existing) =>
            {
                // The previous claim went stale (device idle/closed past the TTL) - let this
                // device claim the name fresh instead of being locked out forever.
                if (existing.ExpiresAtUtc <= now) return new TokenClaim(sessionToken, now + TokenClaimTtl);
                // Same device continuing to use the name: slide the expiry forward.
                if (existing.Token == sessionToken) return existing with { ExpiresAtUtc = now + TokenClaimTtl };
                // A different, still-active device already holds this name.
                return existing;
            });

        return claim.Token == sessionToken;
    }

    /// <summary>Read-only lookup of a name's current claim token, ignoring stale (expired) claims. Does not renew the claim.</summary>
    private string? GetActiveOwnerToken(string singerName)
    {
        if (_singerTokens.TryGetValue(singerName, out var claim) && claim.ExpiresAtUtc > DateTime.UtcNow)
        {
            return claim.Token;
        }
        return null;
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

    public record KioskRequestDto(string? Name, string? Song, string? Artist, string? RequestType, string? DuetPartner, string? Key, string? Notes);

    public record SingerLoginDto(string? Name, string? Pin);

    public record SingerProfileDto(string? Name, string? Pin, string? Email, string? AvatarType, string? VocalRange, string? CustomTitle);

    public record SingerAvatarUploadDto(string? Name, string? Pin, string? Image);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_webApp != null)
        {
            return Task.CompletedTask;
        }

        try
        {
            var builder = WebApplication.CreateBuilder();
            ConfigureHosting(builder);

            _webApp = builder.Build();
            _webApp.UseRateLimiter();

            _rotation.Rotation.CollectionChanged += OnRotationChanged;
            _karaoke.PropertyChanged += OnKaraokePropertyChanged;
            _scaryokeWindow.SpinStarted += OnScaryokeSpinStarted;
            _scaryokeWindow.SpinCompleted += OnScaryokeSpinCompleted;

            _webApp.MapHub<LyricsHub>("/lyricsHub");

            MapStaticAndPortalRoutes(_webApp);
            MapMobileWriteEndpoints(_webApp);
            MapScaryokeReadEndpoints(_webApp);
            MapDashboardEndpoints(_webApp);

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
                    Lyracist.Shared.Globals.LogError("Lyracist", "Tablet Lyrics Kestrel Server error", ex);
                }
            }, token);
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to initialize Tablet Lyrics Server", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>Kestrel binding, DI registrations, and rate-limiter policy setup for the mobile portal web app.</summary>
    private void ConfigureHosting(WebApplicationBuilder builder)
    {
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

        // Per-IP rate limiting so a single phone (buggy or malicious) can't flood the
        // request queue, DB, or catalog search. Write-style actions get a tight window;
        // catalog search gets a looser one to comfortably fit the client's 300ms debounce.
        builder.Services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            rateLimiterOptions.AddPolicy(MobilePortalWritePolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromSeconds(10),
                        QueueLimit = 0
                    }));

            rateLimiterOptions.AddPolicy(MobilePortalSearchPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromSeconds(10),
                        QueueLimit = 0
                    }));
        });

        // Set minimum logging to warning to avoid flooding standard output/debug window
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
    }

    /// <summary>Static TabletClient assets, the /monitor viewer page, and the root singer-portal pages.</summary>
    private void MapStaticAndPortalRoutes(WebApplication app)
    {
        var clientPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TabletClient");
        if (Directory.Exists(clientPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(clientPath),
                RequestPath = "/monitor"
            });

            // Serve index.html directly under /monitor
            app.MapGet("/monitor", async (HttpContext context) =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.SendFileAsync(Path.Combine(clientPath, "index.html"));
            });
        }

        app.MapGet("/", () => Results.Content(GetMobilePortalHtml(), "text/html"));
        app.MapGet("/join", () => Results.Content(GetMobilePortalHtml(), "text/html"));
        app.MapGet("/request", () => Results.Content(GetMobilePortalHtml(), "text/html"));
        app.MapGet("/kiosk", () => Results.Content(GetKioskHtml(), "text/html"));
        app.MapGet("/kiosk.html", () => Results.Content(GetKioskHtml(), "text/html"));
    }

    /// <summary>The mutating actions a singer's phone can take: join, submit a request, rate a performance, spin the Scaryoke wheel.</summary>
    private void MapMobileWriteEndpoints(WebApplication app)
    {
        // Mobile portal: join endpoint to claim performer stage name
        app.MapPost("/api/join", (MobileJoinDto dto, HttpContext context) =>
        {
            var sessionToken = context.Request.Headers["X-Session-Token"].ToString();
            if (string.IsNullOrWhiteSpace(dto.SingerName))
            {
                return Results.BadRequest(new { error = "Name is required." });
            }
            if (ExceedsLength(dto.SingerName, MaxNameLength))
            {
                return Results.BadRequest(new { error = $"Name must be {MaxNameLength} characters or fewer." });
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
        }).RequireRateLimiting(MobilePortalWritePolicy);

        // Mobile portal: song request submission from singers' phones.
        app.MapPost("/api/requests", (MobileRequestDto dto, HttpContext context) =>
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

            if (ExceedsLength(dto.SingerName, MaxNameLength) ||
                ExceedsLength(dto.Title, MaxTitleOrArtistLength) ||
                ExceedsLength(dto.Artist, MaxTitleOrArtistLength) ||
                ExceedsLength(dto.Notes, MaxNotesLength) ||
                ExceedsLength(dto.Source, MaxShortFieldLength) ||
                ExceedsLength(dto.Key, MaxShortFieldLength) ||
                ExceedsLength(dto.RequestType, MaxShortFieldLength))
            {
                return Results.BadRequest(new { error = "One or more fields exceed the maximum allowed length." });
            }

            if (!Lyracist.Core.Helpers.AppSettings.IsRequestSubmissionAllowed(out string scheduleReason))
            {
                return Results.BadRequest(new { error = scheduleReason });
            }

            if (Lyracist.Core.Helpers.AppSettings.BlockDuplicateSongsInSession)
            {
                if (_rotation.IsSongInCurrentSession(dto.Title ?? string.Empty, dto.Artist ?? string.Empty))
                {
                    return Results.BadRequest(new { error = $"\"{dto.Title}\" has already been performed or queued in this session. Duplicate songs are blocked by the DJ." });
                }
            }

            var request = _requests.AddRequest(
                dto.SingerName ?? "Anonymous",
                dto.Title ?? string.Empty,
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
                    request.Status = RequestStatuses.Approved;
                }
                else
                {
                    // Block (synchronously, on this Kestrel worker thread - not the UI thread)
                    // until the rotation mutation actually completes, instead of firing it via
                    // BeginInvoke and immediately claiming "Queued" in the response. Otherwise a
                    // failure here (caught below) would leave the DB at "Pending" while the
                    // client was already told it succeeded.
                    bool queued = false;
                    Exception? queueError = null;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            _rotation.AddSinger(request.SingerName, request.Title, request.Artist, request.Key, request.Notes, request.Source);
                            _requests.MarkQueued(request.Id);
                            queued = true;
                        }
                        catch (Exception ex)
                        {
                            queueError = ex;
                        }
                    });

                    if (queued)
                    {
                        request.Status = RequestStatuses.Queued;
                    }
                    else
                    {
                        Lyracist.Shared.Globals.LogError("Lyracist", "Failed to auto-accept karaoke request into rotation", queueError!);
                        // Leave request.Status at its true DB value ("Pending") rather than
                        // reporting a success that didn't happen.
                    }
                }
            }

            return Results.Ok(request);
        }).RequireRateLimiting(MobilePortalWritePolicy);

        // Kiosk request submission endpoint
        app.MapPost("/api/request", (KioskRequestDto dto) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Results.BadRequest(new { error = "Name is required." });
            }
            if (string.IsNullOrWhiteSpace(dto.Song))
            {
                return Results.BadRequest(new { error = "Song title is required." });
            }
            if (ExceedsLength(dto.Name, MaxNameLength) ||
                ExceedsLength(dto.DuetPartner, MaxNameLength) ||
                ExceedsLength(dto.Song, MaxTitleOrArtistLength) ||
                ExceedsLength(dto.Artist, MaxTitleOrArtistLength) ||
                ExceedsLength(dto.Notes, MaxNotesLength) ||
                ExceedsLength(dto.Key, MaxShortFieldLength) ||
                ExceedsLength(dto.RequestType, MaxShortFieldLength))
            {
                return Results.BadRequest(new { error = "One or more fields exceed the maximum allowed length." });
            }

            string notes = !string.IsNullOrWhiteSpace(dto.DuetPartner) && dto.DuetPartner != "None"
                ? $"Duet with {dto.DuetPartner}"
                : (dto.Notes ?? string.Empty);

            if (!Lyracist.Core.Helpers.AppSettings.IsRequestSubmissionAllowed(out string scheduleReason))
            {
                return Results.BadRequest(new { error = scheduleReason });
            }

            if (Lyracist.Core.Helpers.AppSettings.BlockDuplicateSongsInSession)
            {
                if (_rotation.IsSongInCurrentSession(dto.Song ?? string.Empty, dto.Artist ?? string.Empty))
                {
                    return Results.BadRequest(new { error = $"\"{dto.Song}\" has already been performed or queued in this session. Duplicate songs are blocked by the DJ." });
                }
            }

            var request = _requests.AddRequest(
                dto.Name,
                dto.Song ?? string.Empty,
                dto.Artist ?? string.Empty,
                "Kiosk",
                dto.RequestType ?? "Karaoke",
                dto.Key ?? "0",
                notes);

            if (Lyracist.Core.Helpers.AppSettings.AutoAcceptRequests)
            {
                if (request.RequestType == "Music")
                {
                    _requests.Approve(request.Id);
                    request.Status = RequestStatuses.Approved;
                }
                else
                {
                    bool queued = false;
                    Exception? queueError = null;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            _rotation.AddSinger(request.SingerName, request.Title, request.Artist, request.Key, request.Notes, request.Source);
                            _requests.MarkQueued(request.Id);
                            queued = true;
                        }
                        catch (Exception ex)
                        {
                            queueError = ex;
                        }
                    });

                    if (queued)
                    {
                        request.Status = RequestStatuses.Queued;
                    }
                    else
                    {
                        Lyracist.Shared.Globals.LogError("Lyracist", "Failed to auto-accept kiosk request into rotation", queueError!);
                    }
                }
            }

            return Results.Ok(new { success = true, request });
        }).RequireRateLimiting(MobilePortalWritePolicy);

        app.MapPost("/api/rate", async (MobileRatingDto dto, HttpContext context) =>
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
            if (ExceedsLength(dto.SingerName, MaxNameLength))
            {
                return Results.BadRequest(new { error = $"Name must be {MaxNameLength} characters or fewer." });
            }

            if (dto.Rating < 1 || dto.Rating > 5)
            {
                return Results.BadRequest(new { error = "Rating must be between 1 and 5." });
            }

            // Prevent self-rating
            var ratingOwnerToken = GetActiveOwnerToken(dto.SingerName);
            if (ratingOwnerToken != null && ratingOwnerToken == sessionToken)
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
                // Serialize the read-modify-write below: two concurrent ratings for the same
                // singer both read the pre-update Score/RatingPoints/RatingCount, and whichever
                // SaveChangesAsync commits second would silently overwrite the first increment
                // (classic lost update) since there's no DB-level concurrency token on Singer.
                int newScore, newRatingCount;
                double newAvgRating;
                bool singerFound;

                await _ratingLock.WaitAsync();
                try
                {
                    using var dbContext = new Lyracist.Data.LyracistDbContext();
                    var dbSinger = await dbContext.Singers.FirstOrDefaultAsync(s => s.Name == dto.SingerName);
                    singerFound = dbSinger != null;
                    if (dbSinger == null)
                    {
                        newScore = 0;
                        newRatingCount = 0;
                        newAvgRating = 0;
                    }
                    else
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

                        newScore = dbSinger.Score;
                        newRatingCount = dbSinger.RatingCount;
                        newAvgRating = dbSinger.AverageRating;
                    }
                }
                finally
                {
                    _ratingLock.Release();
                }

                if (!singerFound)
                {
                    return Results.NotFound(new { error = "Singer not found." });
                }

                // Update active rotation memory model asynchronously
                _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var activeSinger = _rotation.Rotation.FirstOrDefault(s => s.Name.Equals(dto.SingerName, StringComparison.OrdinalIgnoreCase));
                        if (activeSinger != null)
                        {
                            activeSinger.Score = newScore;
                            activeSinger.AverageRating = newAvgRating;
                            activeSinger.RatingCount = newRatingCount;
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

                return Results.Ok(new { success = true, score = newScore, avgRating = newAvgRating });
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Failed to save rating from /api/rate", ex);
                return Results.Problem("Failed to save rating.", statusCode: StatusCodes.Status500InternalServerError);
            }
        }).RequireRateLimiting(MobilePortalWritePolicy);

        app.MapPost("/api/scaryoke/spin", (ScaryokeSpinDto? dto, HttpContext context) =>
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
            var performerOwnerToken = performerName != null ? GetActiveOwnerToken(performerName) : null;
            if (performerOwnerToken != null && performerOwnerToken != sessionToken)
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
        }).RequireRateLimiting(MobilePortalWritePolicy);
    }

    /// <summary>Read-only Scaryoke status/categories, gated behind whether the host has Scaryoke Mode on.</summary>
    private void MapScaryokeReadEndpoints(WebApplication app)
    {
        app.MapGet("/api/scaryoke/enabled", () => Results.Json(new { enabled = _karaoke.IsScaryokeMode }));

        app.MapGet("/api/scaryoke/categories", () =>
        {
            if (!_karaoke.IsScaryokeMode)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            return Results.Json(_scaryokeWindow.ViewModel.WheelSegments);
        });
    }

    /// <summary>Read-only endpoints for the tablet monitor/dashboard: pending requests, logs, the live queue, catalog search.</summary>
    private void MapDashboardEndpoints(WebApplication app)
    {
        app.MapGet("/api/requests", () => Results.Json(_requests.GetPending()));

        app.MapGet("/api/logs", (HttpContext context) =>
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
                Lyracist.Shared.Globals.LogError("Lyracist", "Failed to read logs for /api/logs", ex);
                return Results.Problem("Failed to read logs.", statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        app.MapGet("/api/queue", () => Results.Json(BuildQueuePayload()));
        app.MapGet("/api/rotation", () => Results.Json(BuildQueuePayload()));

        // Singer Profile & Avatar Endpoints
        app.MapPost("/api/singer/login", async (SingerLoginDto dto) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Results.BadRequest(new { error = "Singer name is required." });
            }

            string name = Lyracist.Shared.NameFormatting.ProperCase(dto.Name.Trim());
            string pin = dto.Pin?.Trim() ?? string.Empty;

            await using var context = new Lyracist.Data.LyracistDbContext();
            var dbSinger = await context.Singers.FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower());
            if (dbSinger == null)
            {
                dbSinger = new Lyracist.Data.Models.Singer
                {
                    Name = name,
                    PinCode = pin,
                    AvatarType = "None",
                    AvatarSource = string.Empty
                };
                context.Singers.Add(dbSinger);
                await context.SaveChangesAsync();

                return Results.Ok(new
                {
                    success = true,
                    registered = true,
                    singer = new { name = dbSinger.Name, avatarType = dbSinger.AvatarType, avatarSource = dbSinger.AvatarSource, email = dbSinger.Email, vocalRange = dbSinger.VocalRange, customTitle = dbSinger.CustomTitle }
                });
            }

            if (string.IsNullOrEmpty(dbSinger.PinCode))
            {
                dbSinger.PinCode = pin;
                await context.SaveChangesAsync();

                return Results.Ok(new
                {
                    success = true,
                    claimed = true,
                    singer = new { name = dbSinger.Name, avatarType = dbSinger.AvatarType, avatarSource = dbSinger.AvatarSource, email = dbSinger.Email, vocalRange = dbSinger.VocalRange, customTitle = dbSinger.CustomTitle }
                });
            }

            if (dbSinger.PinCode != pin)
            {
                return Results.Json(new { error = "Invalid PIN for this singer profile." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(new
            {
                success = true,
                singer = new { name = dbSinger.Name, avatarType = dbSinger.AvatarType, avatarSource = dbSinger.AvatarSource, email = dbSinger.Email, vocalRange = dbSinger.VocalRange, customTitle = dbSinger.CustomTitle }
            });
        });

        app.MapPost("/api/singer/profile", async (SingerProfileDto dto) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Results.BadRequest(new { error = "Singer name is required." });
            }

            string name = Lyracist.Shared.NameFormatting.ProperCase(dto.Name.Trim());
            string pin = dto.Pin?.Trim() ?? string.Empty;

            await using var context = new Lyracist.Data.LyracistDbContext();
            var dbSinger = await context.Singers.FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower());
            if (dbSinger == null || dbSinger.PinCode != pin)
            {
                return Results.Json(new { error = "Unauthorized profile update." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            dbSinger.Email = dto.Email?.Trim() ?? string.Empty;
            dbSinger.AvatarType = string.IsNullOrWhiteSpace(dto.AvatarType) ? "None" : dto.AvatarType.Trim();
            dbSinger.VocalRange = dto.VocalRange?.Trim() ?? string.Empty;
            dbSinger.CustomTitle = dto.CustomTitle?.Trim() ?? string.Empty;
            if (dbSinger.AvatarType == "Gravatar")
            {
                byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(dbSinger.Email.ToLowerInvariant());
                byte[] hashBytes = System.Security.Cryptography.MD5.HashData(inputBytes);
                dbSinger.AvatarSource = Convert.ToHexStringLower(hashBytes);
            }

            await context.SaveChangesAsync();
            return Results.Ok(new { success = true });
        });

        app.MapPost("/api/singer/avatar/upload", async (SingerAvatarUploadDto dto) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Results.BadRequest(new { error = "Singer name is required." });
            }

            string name = Lyracist.Shared.NameFormatting.ProperCase(dto.Name.Trim());
            string pin = dto.Pin?.Trim() ?? string.Empty;
            string imageBase64 = dto.Image ?? string.Empty;

            await using var context = new Lyracist.Data.LyracistDbContext();
            var dbSinger = await context.Singers.FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower());
            if (dbSinger == null || dbSinger.PinCode != pin)
            {
                return Results.Json(new { error = "Unauthorized avatar upload." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            if (string.IsNullOrEmpty(imageBase64))
            {
                return Results.BadRequest(new { error = "No image data provided." });
            }

            if (imageBase64.Contains(','))
            {
                imageBase64 = imageBase64[(imageBase64.IndexOf(',') + 1)..];
            }

            byte[] imgBytes;
            try
            {
                imgBytes = Convert.FromBase64String(imageBase64);
            }
            catch
            {
                return Results.BadRequest(new { error = "Invalid base64 image data." });
            }

            if (imgBytes.Length > 2 * 1024 * 1024 || !LooksLikeImage(imgBytes))
            {
                return Results.BadRequest(new { error = "Invalid or oversized image (max 2MB)." });
            }

            string avatarsDir = Lyracist.Shared.Globals.AvatarsDir;
            Directory.CreateDirectory(avatarsDir);
            string fileName = $"avatar_{Guid.NewGuid():N}.jpg";
            string filePath = Path.Combine(avatarsDir, fileName);
            await File.WriteAllBytesAsync(filePath, imgBytes);

            dbSinger.AvatarType = "Uploaded";
            dbSinger.AvatarSource = fileName;
            await context.SaveChangesAsync();

            return Results.Ok(new { success = true, avatarSource = fileName });
        });

        app.MapGet("/api/singer/avatar", async (string? name) =>
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Results.NotFound();
            }

            string singerName = name.Trim();
            await using var context = new Lyracist.Data.LyracistDbContext();
            var dbSinger = await context.Singers.FirstOrDefaultAsync(s => s.Name.ToLower() == singerName.ToLower());
            if (dbSinger == null)
            {
                return Results.NotFound();
            }

            if (dbSinger.AvatarType == "Uploaded" && !string.IsNullOrEmpty(dbSinger.AvatarSource))
            {
                string avatarsDir = Lyracist.Shared.Globals.AvatarsDir;
                string? safePath = ResolveAvatarPath(avatarsDir, dbSinger.AvatarSource);
                if (safePath != null && File.Exists(safePath))
                {
                    byte[] bytes = await File.ReadAllBytesAsync(safePath);
                    return Results.File(bytes, "image/jpeg");
                }
            }
            else if (dbSinger.AvatarType == "Gravatar" && !string.IsNullOrEmpty(dbSinger.AvatarSource))
            {
                return Results.Redirect($"https://www.gravatar.com/avatar/{dbSinger.AvatarSource}?d=mp&s=150");
            }

            return Results.NotFound();
        });

        app.MapGet("/api/catalog", async (string? query, string? scope, ILibraryService library, IOccasionService occasions) =>
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
        }).RequireRateLimiting(MobilePortalSearchPolicy);
    }

    /// <summary>Shared projection used by both the /api/queue poll and the QueueUpdated SignalR broadcast.</summary>
    private object BuildQueuePayload()
    {
        return _rotation.Rotation.Select(s => new
        {
            name = s.Name,
            song = s.SongTitle,
            songTitle = s.SongTitle,
            artist = s.Artist,
            key = s.Key,
            source = s.Source,
            isCurrent = s.IsCurrent,
            isNext = s.IsNext,
            isRotationStart = s.IsRotationStart,
            isPaused = s.IsPaused,
            isSkipped = s.IsSkipped,
            isInactive = s.IsInactive
        }).ToList();
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
                Lyracist.Shared.Globals.LogError("Lyracist", "Error during Tablet Lyrics Server cleanup", ex);
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

    private static string? _kioskHtml;
    private static string GetKioskHtml()
    {
        if (_kioskHtml == null)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TabletClient", "kiosk.html");
            if (File.Exists(path))
            {
                _kioskHtml = File.ReadAllText(path);
            }
            else
            {
                _kioskHtml = "<h1>Kiosk file not found.</h1>";
            }
        }
        return _kioskHtml;
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Error broadcasting scaryoke availability", ex);
        }
    }

    private async Task BroadcastQueueAsync()
    {
        if (_webApp == null) return;
        try
        {
            var hubContext = _webApp.Services.GetRequiredService<IHubContext<LyricsHub>>();
            await hubContext.Clients.All.SendAsync("QueueUpdated", BuildQueuePayload());
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Error broadcasting queue", ex);
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Error broadcasting active singer", ex);
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Error broadcasting next singer", ex);
        }
    }

    public void LoadSong(string songTitle, string artist)
    {
        _ = BroadcastActiveSingerAsync();
    }

    public void LoadSong(Singer singer)
    {
        _ = BroadcastActiveSingerAsync();
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Error broadcasting lyrics", ex);
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Error broadcasting scaryoke spin start", ex);
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Error broadcasting scaryoke spin complete", ex);
        }
    }

    private static bool LooksLikeImage(byte[] bytes)
    {
        if (bytes.Length < 4) return false;
        // JPEG (FF D8)
        if (bytes[0] == 0xFF && bytes[1] == 0xD8) return true;
        // PNG (89 50 4E 47)
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;
        // GIF (GIF8)
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return true;
        // WebP (RIFF....WEBP)
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return true;
        return false;
    }

    private static string? ResolveAvatarPath(string avatarsDir, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.IsPathRooted(fileName)) return null;
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\')) return null;
        string fullPath = Path.GetFullPath(Path.Combine(avatarsDir, fileName));
        string fullAvatarsDir = Path.GetFullPath(avatarsDir);
        if (!fullPath.StartsWith(fullAvatarsDir, StringComparison.OrdinalIgnoreCase)) return null;
        return fullPath;
    }
}
