// Edited on Sep 17, 2026 @ 23:31:00 -> Include isSkipped in queue payload
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Lyracist.ViewModels;

namespace Lyracist.Services.Tablet;

public record PerformerClient(string ConnectionId, string SingerName, string DeviceType, string IpAddress);

public class LyricsHub(RotationViewModel rotation, KaraokeViewModel karaoke) : Hub
{
    private readonly RotationViewModel _rotation = rotation;
    private readonly KaraokeViewModel _karaoke = karaoke;

    public static event Action<string>? ReactionReceived;
    public static event Action? ClientsChanged;

    public static ConcurrentDictionary<string, PerformerClient> ActiveClients { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static string GetFriendlyDeviceType(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return "Web Client";
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) return "iPhone";
        if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)) return "iPad";
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            return userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ? "Android Phone" : "Android Tablet";
        }
        if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)) return "Mac";
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return "Windows PC";
        return "Mobile Device";
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        var httpContext = Context.GetHttpContext();
        string singerName = httpContext?.Request.Query["singerName"].ToString() ?? "";
        if (string.IsNullOrWhiteSpace(singerName))
        {
            var referer = httpContext?.Request.Headers["Referer"].ToString();
            if (referer != null && referer.Contains("/monitor", StringComparison.OrdinalIgnoreCase))
            {
                singerName = "Stage Monitor";
            }
            else
            {
                singerName = "Guest";
            }
        }

        string userAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "";
        string deviceType = GetFriendlyDeviceType(userAgent);
        string ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        // Clean IPv6 loopback mapping to friendly IP
        if (ipAddress == "::1")
        {
            ipAddress = "127.0.0.1";
        }

        var client = new PerformerClient(Context.ConnectionId, singerName, deviceType, ipAddress);
        ActiveClients[Context.ConnectionId] = client;
        ClientsChanged?.Invoke();

        // Push initial state to this connection immediately
        var queueList = _rotation.Rotation.Select(s => new
        {
            name = s.Name,
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ActiveClients.TryRemove(Context.ConnectionId, out _);
        ClientsChanged?.Invoke();
        await base.OnDisconnectedAsync(exception);
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
