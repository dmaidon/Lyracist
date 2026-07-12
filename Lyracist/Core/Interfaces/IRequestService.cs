using System;
using System.Collections.Generic;
using Lyracist.Models;

namespace Lyracist.Core.Interfaces;

public interface IRequestService
{
    /// <summary>Raised after any request is added or changes status. May fire on a non-UI thread.</summary>
    event EventHandler? RequestsChanged;

    List<RequestInfo> GetPending();
    List<RequestInfo> GetApproved();
    List<RequestInfo> GetHistory();

    RequestInfo AddRequest(string singerName, string title, string artist, string source = "Local",
        string requestType = "Karaoke", string key = "0", string notes = "");
    void Approve(int requestId);
    void Reject(int requestId);
    void MarkPlayed(int requestId);
    void MarkQueued(int requestId);
}
