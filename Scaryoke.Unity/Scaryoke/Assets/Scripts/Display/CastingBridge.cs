// Created on Sep 23, 2026 @ 11:50:00 -> Wireless TV casting bridge (Miracast & Chromecast web stream)
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Scaryoke.Unity.Wheel;
using UnityEngine;

namespace Scaryoke.Unity.Display
{
    public class CastingBridge : MonoBehaviour
    {
        [Header("Web Streaming Server")]
        public int WebServerPort = 5007;
        public bool AutoStartServer = true;

        [Header("Wheel Reference")]
        public WheelController? Controller;

        private HttpListener? _listener;
        private Thread? _serverThread;
        private volatile bool _isRunning;

        private void Start()
        {
            if (AutoStartServer)
            {
                StartHttpServer();
            }
        }

        private void OnDestroy()
        {
            StopHttpServer();
        }

        public void ConnectToMiracastTv()
        {
            try
            {
                // Open Windows 10/11 Connect / Project flyout for wireless Smart TVs
                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "ms-settings-connectabledevices:devicediscovery",
                    UseShellExecute = true
                };
                Process.Start(psi);
                UnityEngine.Debug.Log("[CastingBridge] Launched Windows Wireless Display / Miracast device discovery.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[CastingBridge] Failed to launch Miracast settings: {ex.Message}");
            }
        }

        public void StartHttpServer()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{WebServerPort}/");
                _listener.Start();
                _isRunning = true;

                _serverThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "ScaryokeWebCastThread"
                };
                _serverThread.Start();
                UnityEngine.Debug.Log($"[CastingBridge] Wheel web cast server started on port {WebServerPort}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[CastingBridge] Could not start HttpListener on port {WebServerPort}: {ex.Message}");
            }
        }

        public void StopHttpServer()
        {
            _isRunning = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
        }

        private void ListenLoop()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[CastingBridge] Listener error: {ex.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                string rawUrl = ctx.Request.RawUrl ?? "/";

                if (rawUrl.StartsWith("/api/status", StringComparison.OrdinalIgnoreCase))
                {
                    bool spinning = Controller != null && Controller.IsSpinning;
                    float rot = Controller != null ? Controller.CurrentRotation : 0f;
                    string json = $"{{\"isSpinning\":{spinning.ToString().ToLowerInvariant()},\"rotation\":{rot:F2}}}";

                    byte[] data = Encoding.UTF8.GetBytes(json);
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.ContentLength64 = data.Length;
                    ctx.Response.OutputStream.Write(data, 0, data.Length);
                }
                else
                {
                    // Basic HTML receiver page for Chromecast DashCast or Smart TV Silk/Chrome browsers
                    string html = @"<!DOCTYPE html>
<html>
<head>
    <title>Scaryoke Wheel Live Cast</title>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body { margin:0; background:#070b14; color:#fff; font-family:system-ui, sans-serif; display:flex; flex-direction:column; align-items:center; justify-content:center; height:100vh; overflow:hidden; }
        h1 { color:#ff6d00; font-size:3rem; margin-bottom:1rem; text-shadow:0 0 20px rgba(255,109,0,0.5); }
        .badge { background:#1e293b; border:1px solid #334155; padding:8px 16px; border-radius:999px; font-weight:bold; color:#a78bfa; font-size:1.2rem; }
    </style>
</head>
<body>
    <h1>🎃 Scaryoke 3D Wheel Live</h1>
    <div class='badge'>Connected to DJ Stage Projection</div>
</body>
</html>";
                    byte[] data = Encoding.UTF8.GetBytes(html);
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    ctx.Response.ContentLength64 = data.Length;
                    ctx.Response.OutputStream.Write(data, 0, data.Length);
                }
            }
            catch { }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
        }
    }
}
