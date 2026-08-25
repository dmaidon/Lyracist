// Edited on Aug 25, 2026 @ 06:40:00 -> Remove unnecessary pragma restore (IDE0079) and clean up LINQ predicates
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Lyracist.Shared
{
    public static class LocalNetworkHelper
    {
        /// <summary>
        /// Resolves the LAN-facing IPv4 address of this machine, so cast targets (Chromecast,
        /// browser cast clients) can be told a URL that is actually reachable from the TV.
        /// </summary>
        public static string GetLocalIPv4() => GetLocalIPv4Address()?.ToString() ?? "127.0.0.1";

        /// <summary>
        /// Same resolution as <see cref="GetLocalIPv4"/> but returns the address itself, so
        /// callers that need to pin a socket to this specific interface (e.g. multicast sends)
        /// don't have to re-parse it.
        /// </summary>
        public static IPAddress? GetLocalIPv4Address()
        {
            try
            {
                var candidates = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                               && (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                                   || nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                               && nic.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
                    .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.Address)
                    .ToList();

                // Cast devices live on a private LAN, essentially without exception - if this
                // machine has multiple routable adapters (e.g. a dedicated WAN/public-IP circuit
                // alongside normal Wi-Fi), prefer the private-range one over a public IP.
                var privateAddress = candidates.FirstOrDefault(IsPrivateIPv4);
                if (privateAddress != null)
                {
                    return privateAddress;
                }

                if (candidates.Count > 0)
                {
                    return candidates[0];
                }

                // Fallback: any up, non-loopback IPv4 address (covers unusual adapter setups).
                var fallback = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                                && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                      && !IPAddress.IsLoopback(a.Address));

                return fallback?.Address;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPrivateIPv4(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length != 4) return false;

            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254); // link-local, still same-segment reachable
        }
    }
}
