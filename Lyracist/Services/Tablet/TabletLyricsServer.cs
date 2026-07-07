using System;
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
using Lyracist.Core.Interfaces;
using Lyracist.ViewModels;
using Lyracist.Models;

namespace Lyracist.Services.Tablet;

public class TabletLyricsServer : ITabletLyricsServer
{
    private WebApplication? _webApp;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;
    private readonly IRequestService _requests;
    private readonly RotationViewModel _rotation;
    private readonly ILibraryService _library;
    private readonly IOccasionService _occasions;
    private readonly KaraokeViewModel _karaoke;
    private readonly Lyracist.Windows.ScaryokeWindow _scaryokeWindow;

    public TabletLyricsServer(
        IRequestService requests,
        RotationViewModel rotation,
        ILibraryService library,
        IOccasionService occasions,
        KaraokeViewModel karaoke,
        Lyracist.Windows.ScaryokeWindow scaryokeWindow)
    {
        _requests = requests;
        _rotation = rotation;
        _library = library;
        _occasions = occasions;
        _karaoke = karaoke;
        _scaryokeWindow = scaryokeWindow;
    }

    /// <summary>Payload for POST /api/requests from the singer mobile portal.</summary>
    public record MobileRequestDto(string? SingerName, string? Title, string? Artist, string? Source, string? Key, string? Notes);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_webApp != null)
        {
            return Task.CompletedTask;
        }

        try
        {
            var builder = WebApplication.CreateBuilder();

            // Listen on the configured port across all interfaces (allows tablet connection over LAN)
            builder.WebHost.UseUrls($"http://*:{Lyracist.Core.Helpers.AppSettings.TabletPort}");

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

            // Mobile portal: song request submission from singers' phones.
            _webApp.MapPost("/api/requests", (MobileRequestDto dto) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Title))
                {
                    return Results.BadRequest(new { error = "Title is required." });
                }

                string title = dto.Title;
                if (!string.IsNullOrWhiteSpace(dto.Key) && dto.Key != "0")
                {
                    title += $" [{dto.Key}]";
                }

                string artist = dto.Artist ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(dto.Notes))
                {
                    artist += $" (Notes: {dto.Notes})";
                }

                var request = _requests.AddRequest(
                    dto.SingerName ?? "Anonymous",
                    title,
                    artist,
                    dto.Source ?? "Portal");
                return Results.Ok(request);
            });

            _webApp.MapGet("/api/scaryoke/categories", () => Results.Json(_scaryokeWindow.ViewModel.WheelSegments));
            _webApp.MapPost("/api/scaryoke/spin", () =>
            {
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

            _webApp.MapGet("/api/catalog", (string? query, ILibraryService library, IOccasionService occasions) =>
            {
                var list = new List<object>();

                var localMatch = library.Search(query ?? "");
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

            _webApp.MapGet("/", () => Results.Content(MobilePortalHtml, "text/html"));
            _webApp.MapGet("/join", () => Results.Content(MobilePortalHtml, "text/html"));
            _webApp.MapGet("/request", () => Results.Content(MobilePortalHtml, "text/html"));

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

    private const string MobilePortalHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Lyracist Performer Portal</title>
            <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/7.0.5/signalr.min.js"></script>
            <style>
                :root {
                    --bg-color: #0b0b0e;
                    --card-bg: #16161e;
                    --accent: #ff4e50;
                    --accent-glow: rgba(255, 78, 80, 0.4);
                    --secondary: #0083b0;
                    --text-primary: #ffffff;
                    --text-secondary: #a0a0a8;
                    --success: #1db954;
                    --youtube: #ff0000;
                    --amazon: #00a8e1;
                    --partytyme: #ff8e53;
                }
                * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }
                body { background-color: var(--bg-color); color: var(--text-primary); padding: 16px; display: flex; flex-direction: column; min-height: 100vh; }
                .container { max-width: 480px; width: 100%; margin: 0 auto; display: flex; flex-direction: column; flex-grow: 1; }
                header { display: flex; justify-content: space-between; align-items: center; padding: 12px 0 24px; }
                h1 { font-size: 24px; font-weight: 800; background: linear-gradient(45deg, var(--accent), var(--partytyme)); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
                .status-dot { width: 8px; height: 8px; background-color: var(--success); border-radius: 50%; box-shadow: 0 0 8px var(--success); }
                .card { background-color: var(--card-bg); border-radius: 16px; border: 1px solid #22222f; padding: 20px; margin-bottom: 16px; box-shadow: 0 4px 20px rgba(0,0,0,0.3); }
                .welcome-title { font-size: 20px; font-weight: 700; margin-bottom: 8px; }
                
                /* Banners */
                .banners-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-bottom: 16px; }
                .banner { border-radius: 12px; padding: 12px; display: flex; flex-direction: column; align-items: center; text-align: center; }
                .banner-now { background: #1b101c; border: 1px solid var(--accent); }
                .banner-next { background: #0c1524; border: 1px solid var(--secondary); }
                .banner-label { font-size: 10px; font-weight: 800; letter-spacing: 1px; text-transform: uppercase; margin-bottom: 6px; }
                .banner-label-now { color: var(--accent); }
                .banner-label-next { color: var(--secondary); }
                .banner-name { font-size: 16px; font-weight: 700; max-width: 100%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
                .banner-song { font-size: 11px; color: var(--text-secondary); margin-top: 2px; max-width: 100%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

                /* Tabs */
                .tabs { display: flex; background-color: #121217; border-radius: 10px; padding: 4px; margin-bottom: 16px; }
                .tab-btn { flex: 1; padding: 10px 0; background: none; border: none; color: var(--text-secondary); font-size: 12px; font-weight: 600; cursor: pointer; border-radius: 8px; transition: all 0.2s; }
                .tab-btn.active { background-color: var(--card-bg); color: var(--text-primary); box-shadow: 0 2px 10px rgba(0,0,0,0.5); }
                .tab-content { display: none; }
                .tab-content.active { display: block; }

                /* Join Form */
                .form-group { margin-bottom: 16px; }
                .form-group label { display: block; font-size: 11px; font-weight: 700; text-transform: uppercase; color: var(--text-secondary); margin-bottom: 6px; }
                input, select, textarea { width: 100%; background-color: #0b0b0e; border: 1px solid #22222f; border-radius: 8px; padding: 12px; color: var(--text-primary); font-size: 14px; transition: border 0.2s; }
                input:focus, select:focus, textarea:focus { outline: none; border-color: var(--accent); }
                .btn-primary { width: 100%; padding: 14px; background: linear-gradient(45deg, var(--accent), var(--partytyme)); border: none; border-radius: 8px; color: white; font-weight: 700; font-size: 15px; cursor: pointer; box-shadow: 0 4px 15px var(--accent-glow); transition: transform 0.1s; }
                .btn-primary:active { transform: scale(0.98); }

                /* Queue List */
                .queue-item { display: flex; justify-content: space-between; align-items: center; padding: 12px; border-bottom: 1px solid #22222f; }
                .queue-item:last-child { border-bottom: none; }
                .queue-details { display: flex; flex-direction: column; flex-grow: 1; overflow: hidden; }
                .queue-singer { font-size: 14px; font-weight: 700; }
                .queue-song { font-size: 12px; color: var(--text-secondary); text-overflow: ellipsis; overflow: hidden; white-space: nowrap; margin-top: 2px; }
                .badge { font-size: 8px; font-weight: 900; padding: 2px 6px; border-radius: 4px; display: inline-block; vertical-align: middle; margin-left: 6px; }
                .badge-local { background-color: rgba(29, 185, 84, 0.15); color: var(--success); }
                .badge-partytyme { background-color: rgba(255, 142, 83, 0.15); color: var(--partytyme); }
                .badge-spotify { background-color: rgba(29, 185, 84, 0.15); color: var(--success); }
                .badge-youtube { background-color: rgba(255, 0, 0, 0.15); color: var(--youtube); }
                .badge-amazon { background-color: rgba(0, 168, 225, 0.15); color: var(--amazon); }
                .queue-index { font-size: 18px; font-weight: 800; color: #3a3a47; margin-right: 12px; }

                /* Search Results */
                .search-results { max-height: 350px; overflow-y: auto; margin-top: 12px; }
                .search-item { display: flex; justify-content: space-between; align-items: center; padding: 12px; border-bottom: 1px solid #22222f; cursor: pointer; border-radius: 6px; }
                .search-item:hover { background-color: #1a1a24; }
                .search-item-info { display: flex; flex-direction: column; }
                .search-item-title { font-size: 13px; font-weight: 700; }
                .search-item-artist { font-size: 11px; color: var(--text-secondary); margin-top: 2px; }
                .badge-source { font-size: 8px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }

                /* Modal Popup */
                .modal { display: none; position: fixed; top: 0; left: 0; width: 100%; height: 100%; background-color: rgba(0,0,0,0.8); z-index: 1000; align-items: center; justify-content: center; padding: 20px; }
                .modal-content { background-color: var(--card-bg); border-radius: 16px; border: 1px solid #22222f; width: 100%; max-width: 400px; padding: 24px; box-shadow: 0 10px 30px rgba(0,0,0,0.5); }
                .modal-title { font-size: 18px; font-weight: 700; margin-bottom: 4px; }
                .modal-artist { font-size: 13px; color: var(--text-secondary); margin-bottom: 20px; }
                .modal-actions { display: flex; gap: 10px; margin-top: 20px; }
                .btn-secondary { flex: 1; padding: 12px; background-color: #1e1e27; border: 1px solid #333344; border-radius: 8px; color: var(--text-primary); font-weight: 700; cursor: pointer; }
                
                .no-data { font-size: 13px; color: var(--text-secondary); text-align: center; padding: 30px 0; }
            </style>
        </head>
        <body>
            <div class="container">
                <!-- Header -->
                <header>
                    <h1>Lyracist</h1>
                    <div class="status-dot"></div>
                </header>

                <!-- Join Screen -->
                <div id="join-screen" class="card" style="margin-top: 50px;">
                    <div class="welcome-title" style="text-align: center; margin-bottom: 20px;">Join Karaoke Session</div>
                    <div class="form-group">
                        <label>Your Name</label>
                        <input id="singer-name-input" type="text" placeholder="Enter your stage name..." />
                    </div>
                    <button class="btn-primary" onclick="joinSession()">Enter Room</button>
                </div>

                <!-- Dashboard Screen -->
                <div id="dashboard-screen" style="display: none;">
                    <div class="welcome-title">Hey, <span id="display-name">Singer</span>! 👋</div>
                    
                    <!-- Now / Next Up Grid -->
                    <div class="banners-grid">
                        <div class="banner banner-now">
                            <span class="banner-label banner-label-now">Now Singing</span>
                            <span id="now-name" class="banner-name">None</span>
                            <span id="now-song" class="banner-song">No Song Loaded</span>
                        </div>
                        <div class="banner banner-next">
                            <span class="banner-label banner-label-next">Next Up</span>
                            <span id="next-name" class="banner-name">None</span>
                            <span id="next-song" class="banner-song">No Song Loaded</span>
                        </div>
                    </div>

                    <!-- Tab Selectors -->
                    <div class="tabs">
                        <button id="tab-queue" class="tab-btn active" onclick="switchTab('queue')">Queue Status</button>
                        <button id="tab-catalog" class="tab-btn" onclick="switchTab('catalog')">Search Catalog</button>
                        <button id="tab-custom" class="tab-btn" onclick="switchTab('custom')">Custom Link</button>
                        <button id="tab-scaryoke" class="tab-btn" onclick="switchTab('scaryoke')">Scaryoke</button>
                    </div>

                    <!-- Queue Tab Content -->
                    <div id="content-queue" class="tab-content active card">
                        <div id="queue-list">
                            <div class="no-data">Loading rotation queue...</div>
                        </div>
                    </div>

                    <!-- Catalog Tab Content -->
                    <div id="content-catalog" class="tab-content card">
                        <div class="form-group" style="margin-bottom: 12px;">
                            <input id="catalog-search" type="text" placeholder="Search by title or artist..." oninput="debounceSearch()" />
                        </div>
                        <div id="catalog-list" class="search-results">
                            <div class="no-data">Type to search the karaoke catalog...</div>
                        </div>
                    </div>

                    <!-- Custom Link Tab Content -->
                    <div id="content-custom" class="tab-content card">
                        <div class="form-group">
                            <label>Music/Video Link</label>
                            <input id="custom-url" type="text" placeholder="Paste Spotify, YouTube, or Amazon Link..." />
                        </div>
                        <div class="form-group">
                            <label>Song Title</label>
                            <input id="custom-title" type="text" placeholder="E.g. Yellow..." />
                        </div>
                        <div class="form-group">
                            <label>Artist Name</label>
                            <input id="custom-artist" type="text" placeholder="E.g. Coldplay..." />
                        </div>
                        <div class="form-group">
                            <label>Key Transposition</label>
                            <select id="custom-key">
                                <option value="-6">-6 semitones</option>
                                <option value="-5">-5 semitones</option>
                                <option value="-4">-4 semitones</option>
                                <option value="-3">-3 semitones</option>
                                <option value="-2">-2 semitones</option>
                                <option value="-1">-1 semitones</option>
                                <option value="0" selected>0 (Original Key)</option>
                                <option value="+1">+1 semitone</option>
                                <option value="+2">+2 semitones</option>
                                <option value="+3">+3 semitones</option>
                                <option value="+4">+4 semitones</option>
                                <option value="+5">+5 semitones</option>
                                <option value="+6">+6 semitones</option>
                            </select>
                        </div>
                        <div class="form-group">
                            <label>Performer Notes</label>
                            <textarea id="custom-notes" placeholder="Notes for the KJ..." rows="2"></textarea>
                        </div>
                        <button class="btn-primary" onclick="submitCustomRequest()">Submit Request</button>
                        <div id="custom-msg" style="margin-top: 12px; font-size: 12px; text-align: center; color: var(--accent);"></div>
                    </div>

                    <!-- Scaryoke Tab Content -->
                    <div id="content-scaryoke" class="tab-content card" style="text-align: center;">
                        <h2 style="font-size: 18px; margin-bottom: 12px; color: var(--partytyme); text-shadow: 0 0 10px rgba(255, 110, 0, 0.3);">🎃 SCARYOKE WHEEL 🎃</h2>
                        <p style="font-size: 11px; color: var(--text-secondary); margin-bottom: 16px;">Test your courage! Spin the wheel of terror!</p>
                        
                        <div style="position: relative; display: inline-block; width: 260px; height: 260px; margin: 0 auto 16px;">
                            <canvas id="wheel-canvas" width="260" height="260" style="border-radius: 50%; box-shadow: 0 0 20px rgba(255, 110, 0, 0.25);"></canvas>
                            <div style="position: absolute; top: -8px; left: 120px; width: 20px; height: 25px; background-color: var(--accent); clip-path: polygon(50% 100%, 0 0, 100% 0); z-index: 10; filter: drop-shadow(0 2px 4px rgba(0,0,0,0.5));"></div>
                        </div>
                        
                        <div style="margin-top: 10px;">
                            <button id="btn-spin-wheel" class="btn-primary" style="max-width: 200px; margin: 0 auto; display: block;" onclick="requestSpin()">SPIN WHEEL</button>
                        </div>
                        
                        <div id="scaryoke-result" style="margin-top: 16px; font-size: 15px; font-weight: bold; color: var(--partytyme); height: 24px;"></div>
                    </div>
                </div>
            </div>

            <!-- Request Modal Dialog -->
            <div id="request-modal" class="modal">
                <div class="modal-content">
                    <div id="modal-title" class="modal-title">Song Title</div>
                    <div id="modal-artist" class="modal-artist">Artist</div>
                    
                    <div class="form-group">
                        <label>Key Transposition</label>
                        <select id="request-key">
                            <option value="-6">-6 semitones</option>
                            <option value="-5">-5 semitones</option>
                            <option value="-4">-4 semitones</option>
                            <option value="-3">-3 semitones</option>
                            <option value="-2">-2 semitones</option>
                            <option value="-1">-1 semitones</option>
                            <option value="0" selected>0 (Original Key)</option>
                            <option value="+1">+1 semitone</option>
                            <option value="+2">+2 semitones</option>
                            <option value="+3">+3 semitones</option>
                            <option value="+4">+4 semitones</option>
                            <option value="+5">+5 semitones</option>
                            <option value="+6">+6 semitones</option>
                        </select>
                    </div>
                    
                    <div class="form-group">
                        <label>Performer Notes</label>
                        <textarea id="request-notes" placeholder="Any special requests or instructions..." rows="2"></textarea>
                    </div>
                    
                    <div id="request-msg" style="margin-bottom: 12px; font-size: 12px; text-align: center; color: var(--accent);"></div>
                    
                    <div class="modal-actions">
                        <button class="btn-secondary" onclick="closeRequestModal()">Cancel</button>
                        <button class="btn-primary" style="flex: 1.5;" onclick="submitCatalogRequest()">Send Request</button>
                    </div>
                </div>
            </div>

            <script>
                let singerName = "";
                let selectedCatalogTrack = null;
                let searchTimeout = null;

                window.onload = () => {
                    const savedName = localStorage.getItem("singerName");
                    if (savedName) {
                        singerName = savedName;
                        showDashboard();
                    }
                };

                function joinSession() {
                    const input = document.getElementById("singer-name-input").value.trim();
                    if (!input) {
                        alert("Please enter a performer name!");
                        return;
                    }
                    singerName = input;
                    localStorage.setItem("singerName", singerName);
                    showDashboard();
                }

                let connection = null;

                function showDashboard() {
                    document.getElementById("join-screen").style.display = "none";
                    document.getElementById("dashboard-screen").style.display = "block";
                    document.getElementById("display-name").textContent = singerName;
                    
                    if (typeof signalR !== 'undefined') {
                        connection = new signalR.HubConnectionBuilder()
                            .withUrl("/lyricsHub")
                            .withAutomaticReconnect()
                            .build();

                        connection.on("QueueUpdated", (queue) => {
                            renderQueue(queue);
                        });

                        connection.on("ActiveSingerUpdated", (active) => {
                            document.getElementById("now-name").textContent = active.name;
                            document.getElementById("now-song").textContent = active.song;
                        });

                        connection.on("NextSingerUpdated", (next) => {
                            document.getElementById("next-name").textContent = next.name;
                            document.getElementById("next-song").textContent = next.song;
                        });

                        connection.on("ScaryokeSpinStarted", (targetFinalAngle, activeCategories, durationSec) => {
                            if (activeCategories && activeCategories.length > 0) {
                                categories = activeCategories;
                            }
                            duration = (durationSec || 4.5) * 1000;
                            document.getElementById("btn-spin-wheel").disabled = true;
                            document.getElementById("scaryoke-result").textContent = "Spinning...";
                            
                            // Trigger animation
                            startAngle = currentAngle;
                            target = currentAngle + 1440 + (targetFinalAngle - (currentAngle % 360));
                            if (target < currentAngle + 1440) {
                                target += 360;
                            }
                            startTime = null;
                            requestAnimationFrame(animateSpin);
                        });

                        connection.on("ScaryokeSpinCompleted", (category) => {
                            document.getElementById("scaryoke-result").textContent = "Landed on: " + category;
                            document.getElementById("btn-spin-wheel").disabled = false;
                        });

                        connection.start().then(() => {
                            console.log("SignalR connected!");
                        }).catch(err => {
                            console.error("SignalR failed, falling back to polling", err);
                            startPolling();
                        });
                    } else {
                        console.log("SignalR library not found, falling back to polling");
                        startPolling();
                    }
                }

                function startPolling() {
                    refreshQueue();
                    setInterval(refreshQueue, 5000);
                }

                function switchTab(tabId) {
                    document.querySelectorAll(".tab-btn").forEach(btn => btn.classList.remove("active"));
                    document.querySelectorAll(".tab-content").forEach(c => c.classList.remove("active"));
                    
                    document.getElementById("tab-" + tabId).classList.add("active");
                    document.getElementById("content-" + tabId).classList.add("active");

                    if (tabId === 'catalog') {
                        document.getElementById("catalog-search").focus();
                    } else if (tabId === 'scaryoke') {
                        loadScaryoke();
                    }
                }

                async function refreshQueue() {
                    try {
                        const res = await fetch("/api/queue");
                        if (res.ok) {
                            const queue = await res.json();
                            renderQueue(queue);
                        }
                    } catch (err) {
                        console.error("Failed to load queue", err);
                    }
                }

                function renderQueue(queue) {
                    const nowSinging = queue[0];
                    const nextUp = queue[1];

                    document.getElementById("now-name").textContent = nowSinging ? nowSinging.name : "None";
                    document.getElementById("now-song").textContent = nowSinging ? `${nowSinging.artist} - ${nowSinging.songTitle}` : "No Song Loaded";
                    
                    document.getElementById("next-name").textContent = nextUp ? nextUp.name : "None";
                    document.getElementById("next-song").textContent = nextUp ? `${nextUp.artist} - ${nextUp.songTitle}` : "No Song Loaded";

                    const container = document.getElementById("queue-list");
                    if (queue.length === 0) {
                        container.innerHTML = '<div class="no-data">No performers in queue yet. Be the first!</div>';
                        return;
                    }

                    let html = "";
                    queue.forEach((item, idx) => {
                        const badgeClass = `badge badge-${(item.source || "local").toLowerCase()}`;
                        html += `
                            <div class="queue-item">
                                <div style="display: flex; align-items: center; overflow: hidden; width: 100%;">
                                    <span class="queue-index">${idx + 1}</span>
                                    <div class="queue-details">
                                        <div class="queue-singer">
                                            ${item.name}
                                            <span class="${badgeClass}">${item.source || "Local"}</span>
                                        </div>
                                        <div class="queue-song">${item.artist} - ${item.songTitle}</div>
                                    </div>
                                </div>
                            </div>
                        `;
                    });
                    container.innerHTML = html;
                }

                function debounceSearch() {
                    clearTimeout(searchTimeout);
                    searchTimeout = setTimeout(performSearch, 300);
                }

                async function performSearch() {
                    const query = document.getElementById("catalog-search").value.trim();
                    const container = document.getElementById("catalog-list");
                    
                    if (!query) {
                        container.innerHTML = '<div class="no-data">Type to search the karaoke catalog...</div>';
                        return;
                    }

                    container.innerHTML = '<div class="no-data">Searching catalog...</div>';

                    try {
                        const res = await fetch(`/api/catalog?query=${encodeURIComponent(query)}`);
                        if (res.ok) {
                            const songs = await res.json();
                            renderCatalog(songs);
                        }
                    } catch (err) {
                        container.innerHTML = '<div class="no-data">Search failed. Try again.</div>';
                    }
                }

                function renderCatalog(songs) {
                    const container = document.getElementById("catalog-list");
                    if (songs.length === 0) {
                        container.innerHTML = '<div class="no-data">No matching songs found. Try a different query.</div>';
                        return;
                    }

                    let html = "";
                    songs.forEach((song, idx) => {
                        const badgeClass = `badge-source badge-${song.source.toLowerCase()}`;
                        html += `
                            <div class="search-item" onclick='openRequestModal(${JSON.stringify(song)})'>
                                <div class="search-item-info">
                                    <span class="search-item-title">${song.title}</span>
                                    <span class="search-item-artist">${song.artist}</span>
                                </div>
                                <span class="${badgeClass}">${song.source}</span>
                            </div>
                        `;
                    });
                    container.innerHTML = html;
                }

                function openRequestModal(song) {
                    selectedCatalogTrack = song;
                    document.getElementById("modal-title").textContent = song.title;
                    document.getElementById("modal-artist").textContent = song.artist;
                    document.getElementById("request-key").value = "0";
                    document.getElementById("request-notes").value = "";
                    document.getElementById("request-msg").textContent = "";
                    document.getElementById("request-modal").style.display = "flex";
                }

                function closeRequestModal() {
                    document.getElementById("request-modal").style.display = "none";
                    selectedCatalogTrack = null;
                }

                async function submitCatalogRequest() {
                    if (!selectedCatalogTrack) return;
                    
                    const msgDiv = document.getElementById("request-msg");
                    msgDiv.style.color = "var(--text-secondary)";
                    msgDiv.textContent = "Submitting request...";

                    const key = document.getElementById("request-key").value;
                    const notes = document.getElementById("request-notes").value.trim();

                    try {
                        const res = await fetch("/api/requests", {
                            method: "POST",
                            headers: { "Content-Type": "application/json" },
                            body: JSON.stringify({
                                singerName: singerName,
                                title: selectedCatalogTrack.title,
                                artist: selectedCatalogTrack.artist,
                                source: selectedCatalogTrack.source,
                                key: key,
                                notes: notes
                            })
                        });

                        if (res.ok) {
                            msgDiv.style.color = "var(--success)";
                            msgDiv.textContent = "Request queued successfully! 🎤";
                            setTimeout(() => {
                                closeRequestModal();
                                switchTab("queue");
                                refreshQueue();
                            }, 1200);
                        } else {
                            msgDiv.style.color = "var(--accent)";
                            msgDiv.textContent = "Failed to submit request.";
                        }
                    } catch (err) {
                        msgDiv.style.color = "var(--accent)";
                        msgDiv.textContent = "Network error. Try again.";
                    }
                }

                async function submitCustomRequest() {
                    const url = document.getElementById("custom-url").value.trim();
                    const title = document.getElementById("custom-title").value.trim();
                    const artist = document.getElementById("custom-artist").value.trim();
                    const msgDiv = document.getElementById("custom-msg");

                    if (!url) {
                        msgDiv.textContent = "Please paste a music URL!";
                        return;
                    }
                    if (!title) {
                        msgDiv.textContent = "Please enter a song title!";
                        return;
                    }

                    msgDiv.style.color = "var(--text-secondary)";
                    msgDiv.textContent = "Submitting custom link request...";

                    const key = document.getElementById("custom-key").value;
                    const notes = document.getElementById("custom-notes").value.trim();

                    let source = "YouTube";
                    if (url.includes("spotify.com")) source = "Spotify";
                    else if (url.includes("amazon.com")) source = "Amazon";

                    try {
                        const res = await fetch("/api/requests", {
                            method: "POST",
                            headers: { "Content-Type": "application/json" },
                            body: JSON.stringify({
                                singerName: singerName,
                                title: title,
                                artist: artist || "Unknown Artist",
                                source: source,
                                key: key,
                                notes: `[Link: ${url}] ${notes}`
                            })
                        });

                        if (res.ok) {
                            msgDiv.style.color = "var(--success)";
                            msgDiv.textContent = "Custom request queued successfully! 🎶";
                            
                            document.getElementById("custom-url").value = "";
                            document.getElementById("custom-title").value = "";
                            document.getElementById("custom-artist").value = "";
                            document.getElementById("custom-notes").value = "";
                            document.getElementById("custom-key").value = "0";

                            setTimeout(() => {
                                msgDiv.textContent = "";
                                switchTab("queue");
                                refreshQueue();
                            }, 1500);
                        } else {
                            msgDiv.style.color = "var(--accent)";
                            msgDiv.textContent = "Failed to queue custom request.";
                        }
                    } catch (err) {
                        msgDiv.style.color = "var(--accent)";
                        msgDiv.textContent = "Network error. Try again.";
                    }
                    // --- SCARYOKE WHEEL CLIENT IMPLEMENTATION ---
                    let categories = [];
                    let currentAngle = 0;
                    let startAngle = 0;
                    let target = 0;
                    let startTime = null;
                    let duration = 4500; // Matches WPF animation
                    const canvas = document.getElementById("wheel-canvas");
                    const colors = ["#6A0DAD", "#FF6D00", "#00838F", "#C2185B", "#4527A0", "#EF6C00", "#00695C", "#AD1457", "#5E35B1", "#F57C00", "#00796B", "#D81B60"];

                    async function loadScaryoke() {
                        try {
                            const res = await fetch("/api/scaryoke/categories");
                            if (res.ok) {
                                categories = await res.json();
                                drawWheel(currentAngle * Math.PI / 180);
                            }
                        } catch (err) {
                            console.error("Failed to load scaryoke categories", err);
                        }
                    }

                    function drawWheel(angleOffset) {
                        if (!canvas || categories.length === 0) return;
                        const ctx = canvas.getContext("2d");
                        const r = canvas.width / 2;
                        
                        ctx.clearRect(0, 0, canvas.width, canvas.height);
                        
                        let currentStartAngle = angleOffset - Math.PI / 2; // Offset so 0 is at 12 o'clock
                        
                        for (let i = 0; i < categories.length; i++) {
                            const segment = categories[i];
                            const sweepRad = (segment.sweep || segment.Sweep) * Math.PI / 180;
                            const currentEndAngle = currentStartAngle + sweepRad;
                            
                            ctx.fillStyle = segment.color || segment.Color || "#8A2BE2";
                            ctx.beginPath();
                            ctx.moveTo(r, r);
                            ctx.arc(r, r, r - 2, currentStartAngle, currentEndAngle);
                            ctx.closePath();
                            ctx.fill();
                            ctx.strokeStyle = "#1E133A";
                            ctx.lineWidth = 2;
                            ctx.stroke();
                            
                            // Segment Label text along the radius
                            ctx.save();
                            ctx.translate(r, r);
                            ctx.rotate(currentStartAngle + sweepRad / 2);
                            ctx.fillStyle = segment.textColor || segment.TextColor || "#ffffff";
                            
                            const segName = segment.name || segment.Name || "";
                            const isDjsChoice = segName.toLowerCase() === "dj's choice";
                            
                            ctx.font = isDjsChoice ? "bold 13px -apple-system, sans-serif" : "bold 9px -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif";
                            ctx.textAlign = "right";
                            
                            let text = isDjsChoice ? "💀" : segName;
                            if (text.length > 15) text = text.substring(0, 13) + "...";
                            
                            ctx.fillText(text, r - 15, 3);
                            ctx.restore();
                            
                            currentStartAngle = currentEndAngle;
                        }
                        
                        // Center pin
                        ctx.fillStyle = "#1e133a";
                        ctx.beginPath();
                        ctx.arc(r, r, 12, 0, 2 * Math.PI);
                        ctx.closePath();
                        ctx.fill();
                        ctx.strokeStyle = "#ff6e00";
                        ctx.lineWidth = 2;
                        ctx.stroke();
                    }

                    function animateSpin(timestamp) {
                        if (!startTime) startTime = timestamp;
                        const elapsed = timestamp - startTime;
                        const progress = Math.min(elapsed / duration, 1);
                        
                        const easeProgress = 1 - Math.pow(1 - progress, 2.5);
                        const current = startAngle + (target - startAngle) * easeProgress;
                        
                        drawWheel(current * Math.PI / 180);
                        
                        if (progress < 1) {
                            requestAnimationFrame(animateSpin);
                        } else {
                            currentAngle = target % 360;
                        }
                    }

                    async function requestSpin() {
                        const btn = document.getElementById("btn-spin-wheel");
                        btn.disabled = true;
                        document.getElementById("scaryoke-result").textContent = "Spinning...";
                        try {
                            const res = await fetch("/api/scaryoke/spin", { method: "POST" });
                            if (!res.ok) {
                                btn.disabled = false;
                                document.getElementById("scaryoke-result").textContent = "Failed to trigger spin.";
                            }
                        } catch (err) {
                            console.error("Failed to request spin", err);
                            btn.disabled = false;
                            document.getElementById("scaryoke-result").textContent = "Network error.";
                        }
                    }
                }
            </script>
        </body>
        </html>
        """;

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
            await hubContext.Clients.All.SendAsync("ActiveSingerUpdated", new
            {
                name = _karaoke.NowSingingName,
                song = _karaoke.NowSingingSong,
                isPlaying = _karaoke.IsPlaying
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
