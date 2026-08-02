// Created on Aug 1, 2026 @ 12:08:00 -> Add CastingService implementation
// Moved to Shared on Aug 1, 2026 @ 12:09:00 -> byte-for-byte duplicated between Lyracist and KSRotation.
// Takes a plain IRotationRenderer (not lazily wrapped) - RotationRenderer itself now defers
// touching the actual window until a frame is really requested, so constructing CastingService
// eagerly no longer risks pulling the whole window/ViewModel graph into the DI resolution path.
using System;
using System.Threading.Tasks;

namespace Lyracist.Shared;

public class CastingService : ICastingService
{
    private readonly MiracastController _miracast;
    private readonly IChromecastSender _chromecast;
    private readonly BrowserCastServer _browser;
    private readonly IRotationRenderer _renderer;

    public bool IsCasting { get; private set; }
    public DisplayTarget CurrentTarget { get; private set; } = DisplayTarget.Monitor;
    public ChromecastDevice? SelectedDevice { get; set; }

    public CastingService(
        MiracastController miracast,
        IChromecastSender chromecast,
        BrowserCastServer browser,
        IRotationRenderer renderer)
    {
        _miracast = miracast;
        _chromecast = chromecast;
        _browser = browser;
        _renderer = renderer;
    }

    public async Task<bool> CastRotationAsync(DisplayTarget target, ChromecastDevice? device = null)
    {
        await StopCastingAsync(); // always stop previous casting

        CurrentTarget = target;

        if (device != null)
        {
            SelectedDevice = device;
        }

        switch (target)
        {
            case DisplayTarget.Monitor:
            case DisplayTarget.WirelessHDMI:
                // These are treated as normal monitors
                IsCasting = true;
                return true;

            case DisplayTarget.Miracast:
                IsCasting = await _miracast.StartCastingAsync(_renderer);
                return IsCasting;

            case DisplayTarget.Chromecast:
                var targetDevice = device ?? SelectedDevice;
                if (targetDevice == null)
                {
                    Globals.LogInfo("Shared", "CastRotationAsync(Chromecast): no target device.");
                    return false;
                }

                // Start BrowserCast server so the rotation stream is available over HTTP
                var serverStarted = await _browser.StartServerAsync(_renderer);
                Globals.LogInfo("Shared", $"CastRotationAsync(Chromecast): BrowserCastServer started={serverStarted}");

                var launched = await _chromecast.LaunchReceiverAsync(targetDevice);
                Globals.LogInfo("Shared", $"CastRotationAsync(Chromecast): LaunchReceiverAsync to {targetDevice.Name} ({targetDevice.Address}) succeeded={launched}");
                if (!launched) return false;

                var localIp = LocalNetworkHelper.GetLocalIPv4();
                var rotationUrl = $"http://{localIp}:8080/rotation";
                IsCasting = await _chromecast.SendRotationUrlAsync(targetDevice, rotationUrl);
                Globals.LogInfo("Shared", $"CastRotationAsync(Chromecast): SendRotationUrlAsync({rotationUrl}) succeeded={IsCasting}");

                return IsCasting;

            case DisplayTarget.BrowserCast:
                IsCasting = await _browser.StartServerAsync(_renderer);
                return IsCasting;

            case DisplayTarget.AirPlay:
                // Optional: AirServer / Reflector integration
                return false;
        }

        return false;
    }

    public async Task StopCastingAsync()
    {
        await _miracast.StopCastingAsync();

        if (SelectedDevice != null)
        {
            await _chromecast.StopCastingAsync(SelectedDevice);
        }

        await _browser.StopServerAsync();

        IsCasting = false;
        CurrentTarget = DisplayTarget.Monitor;
    }
}
