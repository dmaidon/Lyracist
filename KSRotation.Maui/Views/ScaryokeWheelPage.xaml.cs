// Edited on Oct 29, 2026 @ 14:54:00 -> Broadcast live Scaryoke wheel spin and result to connected audience displays
using KSRotation.Maui.ViewModels;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

namespace KSRotation.Maui.Views;

public partial class ScaryokeWheelPage : ContentPage
{
    private readonly ScaryokeWheelViewModel _vm;
    private readonly ScaryokeWheelDrawable _drawable = new();
    private double _currentAngle;
    private bool _isSpinning;
    private CancellationTokenSource? _dismissBroadcastCts;

    public ScaryokeWheelPage(ScaryokeWheelViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;

        _drawable.Segments = _vm.WheelSegments.ToList();
        WheelView.Drawable = _drawable;

        _vm.WheelSegments.CollectionChanged += OnWheelSegmentsChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _dismissBroadcastCts?.Cancel();
        _vm.DismissBroadcast();
    }

    private void OnWheelSegmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _drawable.Segments = _vm.WheelSegments.ToList();
        WheelView.Invalidate();
    }

    private async void OnSpinClicked(object? sender, EventArgs e)
    {
        if (_isSpinning || _vm.WheelSegments.Count == 0)
        {
            return;
        }

        _dismissBroadcastCts?.Cancel();
        _dismissBroadcastCts = new CancellationTokenSource();
        var token = _dismissBroadcastCts.Token;

        _isSpinning = true;
        _vm.IsSpinning = true;
        SpinButton.IsEnabled = false;
        _vm.ResultText = "Spinning...";
        _vm.BeginSpin();

        // Re-roll where the DJ's Choice slivers land for this spin, matching the desktop wheel.
        _vm.RebuildWheelSegments();
        _drawable.Segments = _vm.WheelSegments.ToList();
        WheelView.Invalidate();

        // Pick the landing point up front (slice size sets the odds), re-rolling while it hits a recent category
        double landing = 0;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            landing = Random.Shared.NextDouble() * 360;
            if (!_vm.IsRecentRepeat(SegmentAtPointerAngle(landing).Name)) break;
        }

        double desired = (360 - landing) % 360;
        double baseAngle = _currentAngle + 1440 + (Random.Shared.Next(0, 2) * 360);
        double target = baseAngle + (((desired - (baseAngle % 360)) + 360) % 360);

        _vm.BroadcastSpinStart(target, 4.2);

        await WheelView.RotateToAsync(target, 4200, Easing.CubicOut);

        _currentAngle = target % 360;
        WheelView.Rotation = _currentAngle;

        _isSpinning = false;
        _vm.IsSpinning = false;
        SpinButton.IsEnabled = true;

        // The pointer is fixed at the top; the segment now under it is whichever one sits at
        // (360 - currentAngle) in the wheel's original (unrotated) layout, since segments are laid
        // out starting at 0 = top, clockwise.
        _vm.ApplyResult(SegmentAtPointerAngle((360 - _currentAngle) % 360).Name);
        _vm.BroadcastSpinResult(_currentAngle);

        try
        {
            await Task.Delay(4500, token);
            if (!token.IsCancellationRequested)
            {
                _vm.DismissBroadcast();
            }
        }
        catch (TaskCanceledException)
        {
            // Next spin started or page closed
        }
    }

    private WheelSegment SegmentAtPointerAngle(double angle)
    {
        double start = 0;
        foreach (var segment in _vm.WheelSegments)
        {
            double end = start + segment.Sweep;
            if (angle >= start && angle < end)
            {
                return segment;
            }
            start = end;
        }

        return _vm.WheelSegments[0];
    }
}
