// Edited on Oct 31, 2026 @ 10:39:00 -> Add RefreshNetworkAddress to IKnockoutWebServer
using System;

namespace KnockoutTrivia.Services;

public interface IKnockoutWebServer : IDisposable
{
    bool IsRunning { get; }
    int Port { get; }
    string ConnectUrl { get; }
    void RefreshNetworkAddress();
    bool Start(int? port = null);
    void Stop();
}
