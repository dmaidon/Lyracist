// Edited on Aug 25, 2026 @ 06:40:00 -> Use conditional access (RCS1146)
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Lyracist.Shared
{
    public class ChromecastDevice
    {
        public string Name { get; set; } = string.Empty;
        public IPAddress? Address { get; set; }
        public int Port { get; set; } = 8009; // Chromecast default
    }

    public interface IChromecastDiscoveryService
    {
        Task<List<ChromecastDevice>> DiscoverAsync();
    }

    public class ChromecastDiscoveryService : IChromecastDiscoveryService
    {
        private const string MdnsQuery = "_googlecast._tcp.local";
        private const string GenericDeviceName = "Chromecast Device";

        public async Task<List<ChromecastDevice>> DiscoverAsync()
        {
            using var client = new UdpClient();

            // On a machine with more than one active NIC (e.g. Wi-Fi to the LAN plus a second,
            // publicly-routed adapter), the OS's default route/lowest-metric interface is not
            // necessarily the one the Cast device is actually on - and multicast sends follow
            // that same default unless we pin them to the LAN interface explicitly.
            var localAddress = LocalNetworkHelper.GetLocalIPv4Address();
            Log($"Starting discovery. Local LAN address resolved to {localAddress?.ToString() ?? "(none)"}.");

            // mDNS responders normally answer via multicast back to 224.0.0.251:5353, which a
            // socket on a random ephemeral port that never joined that group won't reliably see
            // (whether it works at all then depends on switch/driver multicast flooding behavior).
            // Binding to 5353 and joining the group lets us receive that path...
            try
            {
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(new IPEndPoint(IPAddress.Any, 5353));
                Log("Bound UDP socket to port 5353.");
            }
            catch (Exception ex)
            {
                // Port 5353 may already be held by another local mDNS responder (e.g. Bonjour).
                client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                Log($"Bind to 5353 failed ({ex.GetType().Name}: {ex.Message}); fell back to an ephemeral port.");
            }

            try
            {
                if (localAddress != null)
                {
                    client.JoinMulticastGroup(IPAddress.Parse("224.0.0.251"), localAddress);
                }
                else
                {
                    client.JoinMulticastGroup(IPAddress.Parse("224.0.0.251"));
                }
                Log("Joined multicast group 224.0.0.251.");
            }
            catch (Exception ex)
            {
                // Best effort - the unicast-response request below covers responders that
                // don't need/support group membership.
                Log($"JoinMulticastGroup failed ({ex.GetType().Name}: {ex.Message}); relying on unicast QU response only.");
            }

            try
            {
                if (localAddress != null)
                {
                    // Pin outbound multicast sends to the LAN interface instead of letting the
                    // OS pick by route metric, which can point at the wrong adapter entirely.
                    client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, localAddress.GetAddressBytes());
                    Log($"Pinned outbound multicast interface to {localAddress}.");
                }
            }
            catch (Exception ex)
            {
                // Best effort - fall back to whatever interface the OS chooses by default
                Log($"Setting MulticastInterface failed ({ex.GetType().Name}: {ex.Message}); using OS default route.");
            }

            // Plain multicast query (any responder falls back to this) and a QU (unicast-
            // response-preferred, RFC 6762 §5.4) variant for responders that support it - we
            // alternate both across retries since we can't assume which this TV's mDNS stack wants.
            var multicastRequest = BuildMdnsQuery(MdnsQuery, unicastResponse: false);
            var unicastRequest = BuildMdnsQuery(MdnsQuery, unicastResponse: true);
            var multicastEp = new IPEndPoint(IPAddress.Parse("224.0.0.251"), 5353);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));

            // UDP queries can simply be dropped, and some TVs are slow to answer (e.g. waking
            // Wi-Fi from a power-save state) - resend a few times across the listen window
            // instead of firing one packet and hoping.
            int sendAttempts = 0;
            int sendFailures = 0;
            _ = Task.Run(async () =>
            {
                for (int attempt = 0; attempt < 4 && !cts.IsCancellationRequested; attempt++)
                {
                    var request = attempt % 2 == 0 ? multicastRequest : unicastRequest;
                    try
                    {
                        await client.SendAsync(request, request.Length, multicastEp);
                        sendAttempts++;
                    }
                    catch (Exception ex)
                    {
                        // Network interface might not support multicast on some configs
                        sendFailures++;
                        Log($"Query send attempt {attempt + 1} failed: {ex.GetType().Name}: {ex.Message}");
                    }

                    try
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(700), cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // listen window closed
                    }
                }
            }, cts.Token);

            var devicesByAddress = new Dictionary<string, ChromecastDevice>();
            int packetsReceived = 0;
            int packetsCastRelated = 0;
            int packetsMentioningGooglecast = 0;

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var receiveTask = client.ReceiveAsync(cts.Token);
                    var result = await receiveTask;
                    packetsReceived++;

                    // Independent of the structured parser below, so a parsing bug can't hide a
                    // packet that genuinely arrived and genuinely mentions the Cast service.
                    if (Encoding.UTF8.GetString(result.Buffer).Contains("googlecast", StringComparison.OrdinalIgnoreCase))
                    {
                        packetsMentioningGooglecast++;
                    }

                    var parsed = ParseMdnsResponse(result.Buffer);
                    if (parsed == null) continue;
                    packetsCastRelated++;

                    parsed.Address ??= result.RemoteEndPoint.Address;
                    if (parsed.Address == null) continue;

                    var key = parsed.Address.ToString();

                    // A device's PTR/SRV/TXT/A records can arrive split across multiple
                    // packets, so merge in a real name if a later packet supplies one for
                    // an address we already saw with only the generic placeholder.
                    if (devicesByAddress.TryGetValue(key, out var existingDevice))
                    {
                        if (existingDevice.Name == GenericDeviceName && parsed.Name != GenericDeviceName)
                        {
                            existingDevice.Name = parsed.Name;
                        }
                    }
                    else
                    {
                        devicesByAddress[key] = parsed;
                        Log($"Discovered candidate device '{parsed.Name}' at {parsed.Address} from {result.RemoteEndPoint}.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout reached
            }
            catch (Exception ex)
            {
                Log($"Discovery loop error: {ex.GetType().Name}: {ex.Message}");
            }

            Log($"Discovery finished. Query sends: {sendAttempts} ok / {sendFailures} failed. " +
                $"Packets received: {packetsReceived} total, {packetsMentioningGooglecast} mention 'googlecast' raw, {packetsCastRelated} parsed as cast-related. " +
                $"Devices found: {devicesByAddress.Count}.");

            return devicesByAddress.Values.ToList();
        }

        private static void Log(string message) => Globals.LogInfo("ChromecastDiscovery", message);

        private byte[] BuildMdnsQuery(string name, bool unicastResponse)
        {
            var parts = name.Split('.');
            using var ms = new MemoryStream();

            // Transaction ID
            ms.Write(new byte[] { 0x00, 0x00 }, 0, 2);

            // Flags: standard query
            ms.Write(new byte[] { 0x00, 0x00 }, 0, 2);

            // Questions: 1
            ms.Write(new byte[] { 0x00, 0x01 }, 0, 2);

            // Answer RRs, Authority RRs, Additional RRs: 0
            ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0, 6);

            foreach (var part in parts)
            {
                var bytes = Encoding.UTF8.GetBytes(part);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }

            ms.WriteByte(0x00); // end of name

            // Type PTR
            ms.Write(new byte[] { 0x00, 0x0C }, 0, 2);

            // Class IN. The top bit optionally requests a unicast response (RFC 6762 §5.4).
            // Some embedded mDNS stacks (seemingly including at least one Sony Bravia's) don't
            // handle the QU bit correctly and just silently drop the query instead of falling
            // back to a normal multicast reply - so we alternate QU on/off across retries rather
            // than assuming either behavior universally.
            ms.WriteByte(unicastResponse ? (byte)0x80 : (byte)0x00);
            ms.WriteByte(0x01);

            return ms.ToArray();
        }

        // mDNS TXT records are length-prefixed binary strings with no delimiter between entries,
        // so scanning the raw packet as UTF-8 text for "fn=...\r\n" is unsound - the byte right
        // after the name is just the next entry's length byte, not a terminator, and can corrupt
        // or break the match. A real (if minimal) DNS message parse is required instead.
        private static ChromecastDevice? ParseMdnsResponse(byte[] buffer)
        {
            try
            {
                if (buffer.Length < 12) return null;

                bool isResponse = (ReadUInt16At(buffer, 2) & 0x8000) != 0; // QR bit

                ushort qdCount = ReadUInt16At(buffer, 4);
                ushort anCount = ReadUInt16At(buffer, 6);
                ushort nsCount = ReadUInt16At(buffer, 8);
                ushort arCount = ReadUInt16At(buffer, 10);

                int pos = 12;
                for (int i = 0; i < qdCount; i++)
                {
                    ReadName(buffer, ref pos);
                    pos += 4; // qtype + qclass
                }

                var records = new List<RawRecord>();
                int totalRecords = anCount + nsCount + arCount;
                for (int i = 0; i < totalRecords && pos < buffer.Length; i++)
                {
                    var recordName = ReadName(buffer, ref pos);
                    if (pos + 10 > buffer.Length) break;

                    ushort type = ReadUInt16At(buffer, pos); pos += 2;
                    pos += 2; // class
                    pos += 4; // ttl
                    ushort rdLength = ReadUInt16At(buffer, pos); pos += 2;

                    if (pos + rdLength > buffer.Length) break;

                    records.Add(new RawRecord(recordName, type, pos, rdLength));
                    pos += rdLength;
                }

                bool isCastRelated = records.Any(r =>
                    r.Name.Contains("googlecast", StringComparison.OrdinalIgnoreCase) ||
                    (r.Type == 12 && r.Name.Equals(MdnsQuery, StringComparison.OrdinalIgnoreCase)));

                if (!isCastRelated)
                {
                    // The structured record walk didn't confidently find a googlecast record -
                    // could be an unusual layout/compression pattern we don't handle. Fall back
                    // to a raw substring check (on genuine responses only, so we don't misidentify
                    // our own outgoing query echoed back via multicast loopback) so detection is
                    // never worse than a plain "does this packet mention googlecast" check.
                    if (!isResponse) return null;

                    var rawText = Encoding.UTF8.GetString(buffer);
                    if (!rawText.Contains("googlecast", StringComparison.OrdinalIgnoreCase)) return null;
                }

                // PTR record's RDATA is the specific device's instance name, which the rest of
                // its records (TXT/SRV) are filed under - e.g. "Living Room TV._googlecast._tcp.local".
                string? instanceName = null;
                var ptr = records.FirstOrDefault(r => r.Type == 12 &&
                    r.Name.Equals(MdnsQuery, StringComparison.OrdinalIgnoreCase));
                if (ptr != null)
                {
                    int ptrNamePos = ptr.RDataStart;
                    instanceName = ReadName(buffer, ref ptrNamePos);
                }

                var txt = instanceName != null
                    ? records.FirstOrDefault(r => r.Type == 16 && r.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase))
                    : records.FirstOrDefault(r => r.Type == 16 && r.Name.Contains("googlecast", StringComparison.OrdinalIgnoreCase));
                string? friendlyName = txt != null ? ParseTxtFriendlyName(buffer, txt.RDataStart, txt.RDataLength) : null;

                string? targetHost = null;
                if (instanceName != null)
                {
                    var srv = records.FirstOrDefault(r => r.Type == 33 && r.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase));
                    if (srv?.RDataLength > 6)
                    {
                        int srvNamePos = srv.RDataStart + 6; // priority(2) + weight(2) + port(2)
                        targetHost = ReadName(buffer, ref srvNamePos);
                    }
                }

                var aRecord = targetHost != null
                    ? records.FirstOrDefault(r => r.Type == 1 && r.Name.Equals(targetHost, StringComparison.OrdinalIgnoreCase))
                    : records.FirstOrDefault(r => r.Type == 1);

                IPAddress? address = null;
                if (aRecord?.RDataLength == 4)
                {
                    address = new IPAddress(new[]
                    {
                        buffer[aRecord.RDataStart], buffer[aRecord.RDataStart + 1],
                        buffer[aRecord.RDataStart + 2], buffer[aRecord.RDataStart + 3]
                    });
                }

                string displayName = GenericDeviceName;
                if (!string.IsNullOrWhiteSpace(friendlyName))
                {
                    displayName = friendlyName!;
                }
                else if (instanceName != null)
                {
                    var suffixIndex = instanceName.IndexOf("._googlecast._tcp.local", StringComparison.OrdinalIgnoreCase);
                    if (suffixIndex > 0)
                    {
                        displayName = instanceName[..suffixIndex];
                    }
                }

                return new ChromecastDevice
                {
                    Name = displayName,
                    Address = address
                };
            }
            catch
            {
                return null;
            }
        }

        private static string? ParseTxtFriendlyName(byte[] buffer, int start, int length)
        {
            int pos = start;
            int end = start + length;

            while (pos < end)
            {
                int entryLen = buffer[pos];
                pos++;
                if (pos + entryLen > end) break;

                var entry = Encoding.UTF8.GetString(buffer, pos, entryLen);
                pos += entryLen;

                if (entry.StartsWith("fn=", StringComparison.OrdinalIgnoreCase))
                {
                    return entry[3..];
                }
            }

            return null;
        }

        /// <summary>Reads a (possibly compressed, RFC 1035 §4.1.4) DNS name starting at pos, advancing pos past it.</summary>
        private static string ReadName(byte[] buffer, ref int pos)
        {
            var labels = new List<string>();
            int cur = pos;
            int jumpedFrom = -1;
            int guard = 0;

            while (cur < buffer.Length && guard++ < 128)
            {
                byte len = buffer[cur];

                if (len == 0)
                {
                    cur++;
                    break;
                }

                if ((len & 0xC0) == 0xC0)
                {
                    if (cur + 1 >= buffer.Length) break;
                    int pointer = ((len & 0x3F) << 8) | buffer[cur + 1];
                    if (jumpedFrom == -1) jumpedFrom = cur + 2;
                    cur = pointer;
                    continue;
                }

                cur++;
                if (cur + len > buffer.Length) break;
                labels.Add(Encoding.UTF8.GetString(buffer, cur, len));
                cur += len;
            }

            pos = jumpedFrom != -1 ? jumpedFrom : cur;
            return string.Join(".", labels);
        }

        private static ushort ReadUInt16At(byte[] buffer, int pos) => (ushort)((buffer[pos] << 8) | buffer[pos + 1]);

        private sealed record RawRecord(string Name, ushort Type, int RDataStart, int RDataLength);
    }
}
