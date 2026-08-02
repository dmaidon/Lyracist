// Created on Aug 1, 2026 @ 12:05:00 -> Add RotationRenderer implementation
// Moved to Shared on Aug 1, 2026 -> generalized to ICaptureSource so both apps' rotation display
// windows (Lyracist.RotationWindow, KSRotation.SingerDisplayWindow) can share this. The window is
// captured via a lazily-invoked delegate rather than injected directly - CastingService holds an
// IRotationRenderer for its whole lifetime, and if constructing this eagerly touched the actual
// window (a DI singleton with its own constructor dependency chain), that chain running mid-startup
// caused a circular DI resolution hang in Lyracist. Deferring the window lookup until a frame is
// actually requested (i.e. only once a cast is really happening) avoids that entirely.
using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Lyracist.Shared;

public class RotationRenderer : IRotationRenderer
{
    private readonly Func<ICaptureSource?> _windowProvider;

    public RotationRenderer(Func<ICaptureSource?> windowProvider)
    {
        _windowProvider = windowProvider ?? throw new ArgumentNullException(nameof(windowProvider));
    }

    public Task<BitmapSource?> RenderRotationAsync()
    {
        // BrowserCastServer calls this repeatedly from a background thread. The provider may
        // construct a WPF Window the first time it's called (e.g. a DI singleton that hasn't been
        // resolved yet) - WPF requires Window construction to happen on the UI/STA thread, so the
        // provider call itself must be dispatched, not just the capture that follows it.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return Task.FromResult<BitmapSource?>(null);
        }

        try
        {
            BitmapSource? frame = null;
            dispatcher.Invoke(() =>
            {
                var window = _windowProvider();
                if (window != null && window.IsLoaded)
                {
                    var bmp = window.CaptureBitmap();
                    bmp.Freeze();
                    frame = bmp;
                }
            });
            return Task.FromResult(frame);
        }
        catch (Exception ex)
        {
            Globals.LogError("Shared", "RotationRenderer.RenderRotationAsync", ex);
            return Task.FromResult<BitmapSource?>(null);
        }
    }
}
