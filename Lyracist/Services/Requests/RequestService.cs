// Edited on Aug 27, 2026 @ 07:07:00 -> Use Lyracist.Shared.NameFormatting
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Lyracist.Core.Interfaces;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Models;
using Lyracist.Shared;


namespace Lyracist.Services.Requests;

/// <summary>
/// DB-backed music request queue. Statuses flow Pending -> Approved/Rejected,
/// and Approved -> Played. Requests can arrive from the control panel or from
/// the mobile portal endpoints on the tablet server (non-UI threads), so
/// consumers of RequestsChanged must marshal to the dispatcher themselves.
/// </summary>
public class RequestService : IRequestService
{
    public event EventHandler? RequestsChanged;

    public List<RequestInfo> GetPending() => GetByStatus(RequestStatuses.Pending);
    public List<RequestInfo> GetApproved() => GetByStatus(RequestStatuses.Approved);

    public List<RequestInfo> GetHistory()
    {
        using var context = new LyracistDbContext();
        return [.. context.MusicRequests
            .Include(r => r.Singer)
            .Where(r => r.Status == RequestStatuses.Played || r.Status == RequestStatuses.Rejected || r.Status == RequestStatuses.Queued)
            .OrderByDescending(r => r.Timestamp)
            .Take(200)
            .ToList()
            .Select(Map)];
    }

    public RequestInfo AddRequest(string singerName, string title, string artist, string source = "Local",
        string requestType = "Karaoke", string key = "0", string notes = "")
    {
        using var context = new LyracistDbContext();

        string name = NameFormatting.ProperCase(string.IsNullOrWhiteSpace(singerName) ? "Anonymous" : singerName.Trim());
        var singer = context.Singers.FirstOrDefault(s => s.Name.ToLower() == name.ToLower())
                     ?? context.Singers.Add(new Data.Models.Singer { Name = name }).Entity;

        var request = new MusicRequest
        {
            Singer = singer,
            Title = NameFormatting.ProperCase(title),
            Artist = NameFormatting.ProperCase(artist),
            Source = string.IsNullOrWhiteSpace(source) ? "Local" : source,
            RequestType = string.IsNullOrWhiteSpace(requestType) ? "Karaoke" : requestType,
            Key = string.IsNullOrWhiteSpace(key) ? "0" : key,
            Notes = notes?.Trim() ?? string.Empty,
            Status = RequestStatuses.Pending,
            Timestamp = DateTime.UtcNow
        };
        context.MusicRequests.Add(request);
        context.SaveChanges();

        RequestsChanged?.Invoke(this, EventArgs.Empty);
        return Map(request);
    }

    public void Approve(int requestId) => SetStatus(requestId, RequestStatuses.Approved);
    public void Reject(int requestId) => SetStatus(requestId, RequestStatuses.Rejected);
    public void MarkPlayed(int requestId) => SetStatus(requestId, RequestStatuses.Played);
    public void MarkQueued(int requestId) => SetStatus(requestId, RequestStatuses.Queued);

    private void SetStatus(int requestId, string status)
    {
        using var context = new LyracistDbContext();
        var request = context.MusicRequests.Find(requestId);
        if (request == null) return;

        request.Status = status;
        context.SaveChanges();
        RequestsChanged?.Invoke(this, EventArgs.Empty);
    }

    private List<RequestInfo> GetByStatus(string status)
    {
        using var context = new LyracistDbContext();
        return [.. context.MusicRequests
            .Include(r => r.Singer)
            .Where(r => r.Status == status)
            .OrderBy(r => r.Timestamp)
            .ToList()
            .Select(Map)];
    }

    private static RequestInfo Map(MusicRequest request) => new()
    {
        Id = request.MusicRequestId,
        SingerName = request.Singer?.Name ?? "Anonymous",
        Title = request.Title,
        Artist = request.Artist,
        Source = request.Source,
        RequestType = request.RequestType,
        Key = request.Key,
        Notes = request.Notes,
        Status = request.Status,
        Timestamp = request.Timestamp.ToLocalTime()
    };
}
