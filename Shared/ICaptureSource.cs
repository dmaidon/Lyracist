// Created on Aug 1, 2026 @ 12:02:00 -> Common capture surface for RotationRenderer, implemented by each
// app's own rotation display window (Lyracist.RotationWindow, KSRotation.SingerDisplayWindow).
// Window already provides IsLoaded/Dispatcher; only CaptureBitmap() needs to be declared here.
using System.Windows.Media.Imaging;

namespace Lyracist.Shared;

public interface ICaptureSource
{
    bool IsLoaded { get; }
    System.Windows.Threading.Dispatcher Dispatcher { get; }
    BitmapSource CaptureBitmap();
}
