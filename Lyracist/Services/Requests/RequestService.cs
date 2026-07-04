using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Lyracist.Core.Interfaces;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Models;

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

    public List<RequestInfo> GetPending() => GetByStatus("Pending");
    public List<RequestInfo> GetApproved() => GetByStatus("Approved");

    public List<RequestInfo> GetHistory()
    {
        using var context = new LyracistDbContext();
        return context.MusicRequests
            .Include(r => r.Singer)
            .Where(r => r.Status == "Played" || r.Status == "Rejected")
            .OrderByDescending(r => r.Timestamp)
            .Take(200)
            .ToList()
            .Select(Map)
            .ToList();
    }

    public RequestInfo AddRequest(string singerName, string title, string artist, string source = "Local")
    {
        using var context = new LyracistDbContext();

        string name = string.IsNullOrWhiteSpace(singerName) ? "Anonymous" : singerName.Trim();
        var singer = context.Singers.FirstOrDefault(s => s.Name.ToLower() == name.ToLower())
                     ?? context.Singers.Add(new Data.Models.Singer { Name = name }).Entity;

        var request = new MusicRequest
        {
            Singer = singer,
            Title = title.Trim(),
            Artist = artist.Trim(),
            Source = string.IsNullOrWhiteSpace(source) ? "Local" : source,
            Status = "Pending",
            Timestamp = DateTime.UtcNow
        };
        context.MusicRequests.Add(request);
        context.SaveChanges();

        RequestsChanged?.Invoke(this, EventArgs.Empty);
        return Map(request);
    }

    public void Approve(int requestId) => SetStatus(requestId, "Approved");
    public void Reject(int requestId) => SetStatus(requestId, "Rejected");
    public void MarkPlayed(int requestId) => SetStatus(requestId, "Played");

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
        return context.MusicRequests
            .Include(r => r.Singer)
            .Where(r => r.Status == status)
            .OrderBy(r => r.Timestamp)
            .ToList()
            .Select(Map)
            .ToList();
    }

    private static RequestInfo Map(MusicRequest request) => new()
    {
        Id = request.MusicRequestId,
        SingerName = request.Singer?.Name ?? "Anonymous",
        Title = request.Title,
        Artist = request.Artist,
        Source = request.Source,
        Status = request.Status,
        Timestamp = request.Timestamp.ToLocalTime()
    };
}
