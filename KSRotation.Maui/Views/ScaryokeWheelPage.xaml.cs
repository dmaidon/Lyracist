using KSRotation.Maui.ViewModels;
using System.Collections.Specialized;

namespace KSRotation.Maui.Views;

public partial class ScaryokeWheelPage : ContentPage
{
    private readonly ScaryokeWheelViewModel _vm;
    private readonly ScaryokeWheelDrawable _drawable = new();
    private double _currentAngle;
    private bool _isSpinning;

    public ScaryokeWheelPage(ScaryokeWheelViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;

        _drawable.Segments = _vm.WheelSegments.ToList();
        WheelView.Drawable = _drawable;

        _vm.WheelSegments.CollectionChanged += OnWheelSegmentsChanged;
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

        _isSpinning = true;
        _vm.IsSpinning = true;
        SpinButton.IsEnabled = false;
        _vm.ResultText = "Spinning...";

        // Re-roll where the DJ's Choice slivers land for this spin, matching the desktop wheel.
        _vm.RebuildWheelSegments();
        _drawable.Segments = _vm.WheelSegments.ToList();
        WheelView.Invalidate();

        double target = _currentAngle + 1440 + (Random.Shared.NextDouble() * 720);

        await WheelView.RotateToAsync(target, 4200, Easing.CubicOut);

        _currentAngle = target % 360;
        WheelView.Rotation = _currentAngle;

        _isSpinning = false;
        _vm.IsSpinning = false;
        SpinButton.IsEnabled = true;

        // The pointer is fixed at the top; the segment now under it is whichever one sits at
        // (360 - currentAngle) in the wheel's original (unrotated) layout, since segments are laid
        // out starting at 0 = top, clockwise.
        double targetAngle = (360 - _currentAngle) % 360;
        var landedSegment = _vm.WheelSegments[0];
        double start = 0;
        foreach (var segment in _vm.WheelSegments)
        {
            double end = start + segment.Sweep;
            if (targetAngle >= start && targetAngle < end)
            {
                landedSegment = segment;
                break;
            }
            start = end;
        }

        _vm.ApplyResult(landedSegment.Name);
    }
}
