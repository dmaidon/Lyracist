// Edited on Aug 25, 2026 @ 06:41:00 -> Fix RCS1146 conditional access
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
                if (window?.IsLoaded == true)
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
