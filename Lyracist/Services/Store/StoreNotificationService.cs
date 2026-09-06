// Created on Sep 6, 2026 @ 13:00:00 -> Implement StoreNotificationService for toast notifications, queueing, and auto-dismissal
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Shared;

namespace Lyracist.Services.Store;

/// <summary>
/// Categorization of toast notifications produced within the Store ecosystem.
/// </summary>
public enum StoreNotificationType
{
    TrackImported,
    NormalizationComplete,
    SilenceTrimmed,
    WaveformGenerated,
    SyncCompleted,
    BulkImportCompleted,
    Info,
    Warning,
    Error
}

/// <summary>
/// Model representing an individual toast notification item.
/// </summary>
public partial class StoreNotificationItem : ObservableObject
{
    public string Id { get; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private StoreNotificationType _type;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string? _formatBadge;

    [ObservableProperty]
    private bool _isNormalized;

    [ObservableProperty]
    private bool _isTrimmed;

    [ObservableProperty]
    private bool _hasWaveform;

    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;

    [ObservableProperty]
    private int _autoDismissSeconds = 5;

    public bool HasBadges => !string.IsNullOrEmpty(FormatBadge) || IsNormalized || IsTrimmed || HasWaveform;

    public string TypeIcon => Type switch
    {
        StoreNotificationType.TrackImported => "ShoppingBag24",
        StoreNotificationType.NormalizationComplete => "Speaker224",
        StoreNotificationType.SilenceTrimmed => "Cut24",
        StoreNotificationType.WaveformGenerated => "Pulse24",
        StoreNotificationType.SyncCompleted => "ArrowSync24",
        StoreNotificationType.BulkImportCompleted => "FolderZip24",
        StoreNotificationType.Warning => "Warning24",
        StoreNotificationType.Error => "DismissCircle24",
        _ => "Info24"
    };

    public string TypeAccentBrush => Type switch
    {
        StoreNotificationType.TrackImported => "#8E2DE2",
        StoreNotificationType.NormalizationComplete => "#10B981",
        StoreNotificationType.SilenceTrimmed => "#06B6D4",
        StoreNotificationType.WaveformGenerated => "#38BDF8",
        StoreNotificationType.SyncCompleted => "#6366F1",
        StoreNotificationType.BulkImportCompleted => "#00C9FF",
        StoreNotificationType.Warning => "#F59E0B",
        StoreNotificationType.Error => "#EF4444",
        _ => "#64748B"
    };

    [RelayCommand]
    private void Dismiss()
    {
        StoreNotificationService.Instance.Dismiss(Id);
    }
}

/// <summary>
/// Thread-safe singleton notification manager coordinating toast queues, auto-dismiss timers, and UI event dispatching.
/// </summary>
public sealed class StoreNotificationService
{
    private static readonly Lazy<StoreNotificationService> _lazy = new(() => new StoreNotificationService());
    public static StoreNotificationService Instance => _lazy.Value;

    public ObservableCollection<StoreNotificationItem> ActiveNotifications { get; } = new();

    public event Action<StoreNotificationItem>? NotificationAdded;
    public event Action<string>? NotificationDismissed;

    private readonly object _lock = new();

    /// <summary>
    /// Queues and displays a notification toast with an automatic 5-second dismissal timer.
    /// </summary>
    public void Enqueue(StoreNotificationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        RunOnUIThread(() =>
        {
            lock (_lock)
            {
                // Maximum 5 stacked notifications at any given time to avoid visual overflow
                if (ActiveNotifications.Count >= 5)
                {
                    ActiveNotifications.RemoveAt(0);
                }

                ActiveNotifications.Add(item);
            }

            NotificationAdded?.Invoke(item);

            // Schedule auto-dismissal
            if (item.AutoDismissSeconds > 0)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(item.AutoDismissSeconds));
                    Dismiss(item.Id);
                });
            }
        });
    }

    /// <summary>
    /// Dismisses a notification by its unique ID.
    /// </summary>
    public void Dismiss(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        RunOnUIThread(() =>
        {
            lock (_lock)
            {
                var existing = ActiveNotifications.FirstOrDefault(n => n.Id == id);
                if (existing != null)
                {
                    ActiveNotifications.Remove(existing);
                    NotificationDismissed?.Invoke(id);
                }
            }
        });
    }

    /// <summary>
    /// Clears all currently active notifications.
    /// </summary>
    public void ClearAll()
    {
        RunOnUIThread(() =>
        {
            lock (_lock)
            {
                ActiveNotifications.Clear();
            }
        });
    }

    /// <summary>
    /// Shows a Track Imported toast notification with format and processing badges.
    /// </summary>
    public void ShowTrackImported(
        string title,
        string artist,
        string provider,
        string? format = null,
        bool normalized = false,
        bool trimmed = false,
        bool waveform = false)
    {
        string displayArtist = string.IsNullOrWhiteSpace(artist) ? "Unknown Artist" : artist.Trim();
        string displayTitle = string.IsNullOrWhiteSpace(title) ? "Unknown Title" : title.Trim();
        string displayProvider = string.IsNullOrWhiteSpace(provider) ? "Local" : provider.Trim();

        var notification = new StoreNotificationItem
        {
            Type = StoreNotificationType.TrackImported,
            Title = "Track Imported",
            Message = $"Imported: {displayArtist} - {displayTitle} ({displayProvider})",
            FormatBadge = format,
            IsNormalized = normalized,
            IsTrimmed = trimmed,
            HasWaveform = waveform,
            AutoDismissSeconds = 5
        };

        Enqueue(notification);
    }

    /// <summary>
    /// Shows a Normalization Complete toast notification.
    /// </summary>
    public void ShowNormalizationComplete(string title, string artist)
    {
        var notification = new StoreNotificationItem
        {
            Type = StoreNotificationType.NormalizationComplete,
            Title = "Audio Normalized",
            Message = $"Audio normalized: {artist} - {title}",
            AutoDismissSeconds = 5
        };

        Enqueue(notification);
    }

    /// <summary>
    /// Shows a Silence Trimmed toast notification.
    /// </summary>
    public void ShowSilenceTrimmed(string title, string artist)
    {
        var notification = new StoreNotificationItem
        {
            Type = StoreNotificationType.SilenceTrimmed,
            Title = "Silence Trimmed",
            Message = $"Silence trimmed: {artist} - {title}",
            AutoDismissSeconds = 5
        };

        Enqueue(notification);
    }

    /// <summary>
    /// Shows a Waveform Generated toast notification.
    /// </summary>
    public void ShowWaveformGenerated(string title, string artist)
    {
        var notification = new StoreNotificationItem
        {
            Type = StoreNotificationType.WaveformGenerated,
            Title = "Waveform Ready",
            Message = $"Waveform ready: {artist} - {title}",
            AutoDismissSeconds = 5
        };

        Enqueue(notification);
    }

    /// <summary>
    /// Shows a Store Sync Completed toast notification.
    /// </summary>
    public void ShowSyncCompleted(int importedCount)
    {
        string plural = importedCount == 1 ? "track" : "tracks";
        var notification = new StoreNotificationItem
        {
            Type = StoreNotificationType.SyncCompleted,
            Title = "Store Sync Complete",
            Message = $"Store Sync Complete — {importedCount} {plural} imported",
            AutoDismissSeconds = 5
        };

        Enqueue(notification);
    }

    /// <summary>
    /// Shows a Bulk Import Completed toast notification.
    /// </summary>
    public void ShowBulkImportCompleted(int processedCount)
    {
        string plural = processedCount == 1 ? "track" : "tracks";
        var notification = new StoreNotificationItem
        {
            Type = StoreNotificationType.BulkImportCompleted,
            Title = "Bulk Import Complete",
            Message = $"Bulk Import Complete — {processedCount} {plural} processed",
            AutoDismissSeconds = 5
        };

        Enqueue(notification);
    }

    /// <summary>
    /// Optional dispatcher override used in headless or test environments.
    /// </summary>
    public static Action<Action>? DispatcherOverride { get; set; }

    private static readonly bool _isRunningInTest =
        AppDomain.CurrentDomain.FriendlyName.Contains("test", StringComparison.OrdinalIgnoreCase)
        || AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name?.Contains("test", StringComparison.OrdinalIgnoreCase) == true);

    private static void RunOnUIThread(Action action)
    {
        if (DispatcherOverride != null)
        {
            DispatcherOverride(action);
            return;
        }

        if (_isRunningInTest)
        {
            action();
            return;
        }

        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess() && !app.Dispatcher.HasShutdownStarted)
        {
            try
            {
                app.Dispatcher.BeginInvoke(action);
                return;
            }
            catch
            {
                // Fall back to direct execution if Dispatcher is unavailable/shutting down
            }
        }

        action();
    }
}
