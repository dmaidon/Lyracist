// Created on Aug 28, 2026 @ 09:16:00 -> IKnockoutWebServer interface for embedded phone/tablet companion server
using System;

namespace KnockoutTrivia.Services;

public interface IKnockoutWebServer : IDisposable
{
    bool IsRunning { get; }
    int Port { get; }
    string ConnectUrl { get; }
    void Start(int? port = null);
    void Stop();
}
