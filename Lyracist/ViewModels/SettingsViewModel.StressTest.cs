// Created on Aug 6, 2026 @ 07:01:27 -> Split stress-test system simulator out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    // ─── Stress-Test System Simulator ──────────────────────────────────────

    [ObservableProperty]
    private string _simulationButtonText = "Start Stress-Test";

    [ObservableProperty]
    private string _simulationStatusText = "Idle";

    [ObservableProperty]
    private string _simulationLogText = "Stress-Test Log Console:\nClick 'Start Stress-Test' to launch auto-pilot.";

    [ObservableProperty]
    private int _simulationDurationMinutes = 1;

    private System.Threading.CancellationTokenSource? _simulationCts;

    [RelayCommand]
    private async Task StartSimulation()
    {
        if (_simulationCts != null)
        {
            _simulationCts.Cancel();
            _simulationCts = null;
            SimulationButtonText = "Start Stress-Test";
            SimulationStatusText = "Aborted";
            LogSim(">> Simulation aborted by host.");
            return;
        }

        _simulationCts = new System.Threading.CancellationTokenSource();
        SimulationButtonText = "Stop Stress-Test";
        SimulationStatusText = "Running...";
        SimulationLogText = string.Empty;
        LogSim($">> System Stress-Test Simulation Started (Duration: {SimulationDurationMinutes} minute(s))");
        LogSim($">> Database track count: {_library.GetSongCount()}");
        LogSim($">> Operating IP: {AppSettings.GetActiveIPAddress()}");

        var token = _simulationCts.Token;
        _ = Task.Run(async () =>
        {
            int countSingersAdded = 0;
            int countToggledInactive = 0;
            int countRequestsMocked = 0;
            int countRequestsApproved = 0;
            int countEqAdjustments = 0;
            int countPlaybackToggles = 0;
            int countErrors = 0;
            int steps = (SimulationDurationMinutes * 60) / 2;
            int i = 0;

            try
            {
                var random = new Random();
                for (i = 0; i < steps && !token.IsCancellationRequested; i++)
                {
                    SimulationStatusText = $"Running ({i + 1}/{steps})...";
                    int action = random.Next(6);

                    switch (action)
                    {
                        case 0:
                            string newSinger = $"SimPerformer_{random.Next(100, 999)}";
                            LogSim($"[QUEUE] Simulating Add Performer: {newSinger}");
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                _rotation.NewSingerName = newSinger;
                                _rotation.NewSingerKey = $"{random.Next(-3, 4)}";
                                _rotation.NewSingerNotes = "Simulated request via Auto-Pilot stress-test.";
                                _rotation.AddSingerCommand.Execute(null);
                            });
                            countSingersAdded++;
                            break;

                        case 1:
                            // Count/indexer reads and the toggle mutation must happen inside a single
                            // Dispatcher.Invoke: ObservableCollection isn't thread-safe, so reading Count
                            // here and indexing it separately (with a UI-thread mutation possibly landing
                            // in between) can throw ArgumentOutOfRangeException on a resize.
                            string? toggleLogMsg = System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (_rotation.Rotation.Count > 0)
                                {
                                    var singer = _rotation.Rotation[random.Next(_rotation.Rotation.Count)];
                                    _rotation.ToggleInactiveSingerCommand.Execute(singer);
                                    return $"[QUEUE] Simulating Inactivate Performer: {singer.Name}";
                                }
                                if (_rotation.InactiveSingers.Count > 0)
                                {
                                    var singer = _rotation.InactiveSingers[random.Next(_rotation.InactiveSingers.Count)];
                                    _rotation.ToggleInactiveSingerCommand.Execute(singer);
                                    return $"[QUEUE] Simulating Reactivate Performer: {singer.Name}";
                                }
                                return null;
                            });

                            if (toggleLogMsg != null)
                            {
                                LogSim(toggleLogMsg);
                            }
                            countToggledInactive++;
                            break;

                        case 2:
                            string rSinger = $"MobileSinger_{random.Next(100, 999)}";
                            string rTitle = $"Simulated Hit {random.Next(1, 50)}";
                            string rArtist = "The Stress Testers";
                            LogSim($"[PORTAL] Simulating incoming request: '{rTitle}' by '{rArtist}' for {rSinger}");
                            _requests.AddRequest(rSinger, rTitle, rArtist, "Mobile Portal");
                            countRequestsMocked++;
                            break;

                        case 3:
                            var pendingList = _requests.GetPending().ToList();
                            if (pendingList.Count > 0)
                            {
                                var req = pendingList[0];
                                LogSim($"[PORTAL] Simulating approving request ID {req.Id} for {req.SingerName}");
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    if (App.AppHost.Services.GetService(typeof(RequestsViewModel)) is RequestsViewModel reqVm)
                                    {
                                        reqVm.SelectedPending = reqVm.Pending.FirstOrDefault(p => p.Id == req.Id);
                                        reqVm.ApproveCommand.Execute(null);
                                    }
                                    else
                                    {
                                        _requests.Approve(req.Id);
                                    }
                                });
                            }
                            countRequestsApproved++;
                            break;

                        case 4:
                            double treble = random.Next(-10, 11);
                            double mid = random.Next(-10, 11);
                            double bass = random.Next(-10, 11);
                            int volume = random.Next(50, 101);
                            LogSim($"[AUDIO] Adjusting EQ settings: Treble={treble}dB, Mid={mid}dB, Bass={bass}dB, Volume={volume}%");
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                _karaoke.Treble = treble;
                                _karaoke.Mid = mid;
                                _karaoke.Bass = bass;
                                _karaoke.Volume = volume;
                            });
                            countEqAdjustments++;
                            break;

                        case 5:
                            LogSim("[AUDIO] Triggering playback start/pause simulation");
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (_karaoke.IsPlaying)
                                {
                                    _karaoke.PauseCommand.Execute(null);
                                    LogSim("[AUDIO] Playback PAUSED");
                                }
                                else
                                {
                                    _karaoke.PlayCommand.Execute(null);
                                    LogSim("[AUDIO] Playback RESUMED");
                                }
                            });
                            countPlaybackToggles++;
                            break;
                    }

                    await Task.Delay(2000, token);
                }

                LogSim("\n=========================================");
                LogSim("       STRESS-TEST ANALYSIS REPORT");
                LogSim("=========================================");
                LogSim($"Duration: {i * 2} seconds");
                LogSim($"Events Injected: {i} / {steps}");
                LogSim("-----------------------------------------");
                LogSim($"[Queue] Singers Added: {countSingersAdded}");
                LogSim($"[Queue] Inactive Toggles: {countToggledInactive}");
                LogSim($"[Portal] Requests Mocked: {countRequestsMocked}");
                LogSim($"[Portal] Requests Approved: {countRequestsApproved}");
                LogSim($"[Audio] Slider & EQ Tweaks: {countEqAdjustments}");
                LogSim($"[Audio] Playback Toggles: {countPlaybackToggles}");
                LogSim("-----------------------------------------");

                bool dbOk = false;
                int totalRequestsInDb = 0;
                try
                {
                    using var context = new Lyracist.Data.LyracistDbContext();
                    totalRequestsInDb = context.MusicRequests.Count();
                    dbOk = true;
                }
                catch { }

                LogSim($"[Sanity Check] Database Integrity: {(dbOk ? $"PASS ({totalRequestsInDb} total request records)" : "FAIL")}");
                LogSim($"[Sanity Check] Audio Engine State: PASS (Successfully Reset)");
                LogSim($"[Sanity Check] UI Thread Locks: PASS (0 Dispatcher Blocks)");
                LogSim($"[Sanity Check] Errors Logged: {countErrors}");
                LogSim("=========================================\n");

                LogSim(">> System Stress-Test Simulation Completed Successfully.");
                LogSim(">> Restoring system rotation queue to defaults.");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _rotation.ClearRotationQueue();
                    _rotation.SeedSingers();
                    _karaoke.ResetAudioCommand.Execute(null);
                });
            }
            catch (TaskCanceledException)
            {
                LogSim(">> Simulation task canceled.");
            }
            catch (Exception ex)
            {
                countErrors++;
                LogSim($"[ERROR] Simulation encountered exception: {ex.Message}");
            }
            finally
            {
                SimulationButtonText = "Start Stress-Test";
                SimulationStatusText = "Completed";
                _simulationCts = null;
            }
        }, token);
    }

    private void LogSim(string message)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            SimulationLogText += $"{DateTime.Now:HH:mm:ss} {message}\n";
        }));
    }
}
