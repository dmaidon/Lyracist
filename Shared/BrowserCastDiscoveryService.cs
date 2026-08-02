// Edited on Aug 1, 2026 @ 15:05:00 -> Replace UdpClient with HttpListener for Browser Cast TCP HTTP POST heartbeats
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Lyracist.Shared
{
    public class BrowserCastClient
    {
        public string Ip { get; set; } = string.Empty;
        public string Device { get; set; } = string.Empty;
    }

    public class BrowserCastDiscoveryService
    {
        public ObservableCollection<BrowserCastClient> Clients { get; } =
            new ObservableCollection<BrowserCastClient>();

        public async Task ListenAsync()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add("http://*:9090/");

            try
            {
                listener.Start();
            }
            catch
            {
                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add("http://localhost:9090/");
                    listener.Start();
                }
                catch
                {
                    return;
                }
            }

            while (true)
            {
                var context = await listener.GetContextAsync();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var request = context.Request;
                        string msg = "BrowserCast Client";
                        using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                        {
                            msg = await reader.ReadToEndAsync();
                        }

                        var clientIp = request.RemoteEndPoint.Address.ToString();

                        if (System.Windows.Application.Current != null)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                var existing = Clients.FirstOrDefault(c => c.Ip == clientIp);
                                if (existing != null)
                                {
                                    existing.Device = msg;
                                }
                                else
                                {
                                    Clients.Add(new BrowserCastClient
                                    {
                                        Ip = clientIp,
                                        Device = msg
                                    });
                                }
                            });
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                    finally
                    {
                        try
                        {
                            context.Response.StatusCode = 200;
                            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
                            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                            context.Response.Close();
                        }
                        catch { }
                    }
                });
            }
        }
    }
}
