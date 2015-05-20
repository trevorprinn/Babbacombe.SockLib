using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {
    /// <summary>
    /// A UDP client that can be used to find a SockLib server.
    /// </summary>
    public class DiscoverClient {
        private IPEndPoint _broadcast;

        public int Timeout { get; private set; }

        public DiscoverClient(int port) {
            _broadcast = new IPEndPoint(IPAddress.Broadcast, port);
            Timeout = 5000;
        }

        public async Task<IPEndPoint> FindService(string serviceName) {
            using (var client = new UdpClient()) {
                var cts = new CancellationTokenSource(Timeout);
                await client.SendAsync(Encoding.UTF8.GetBytes(serviceName), _broadcast, cts.Token);
                if (cts.IsCancellationRequested) return null;

                cts = new CancellationTokenSource(Timeout);
                var msg = await client.ReceiveAsync(cts.Token);
                if (msg == null) return null; // Timed out


                var advert = Encoding.UTF8.GetString(msg.Value.Buffer);
                if (!advert.Contains(':')) return null;
                var parts = advert.Split(':');
                if (parts[0] != serviceName) return null;

                int port;
                if (!int.TryParse(parts[1], out port)) return null;
                return new IPEndPoint(msg.Value.RemoteEndPoint.Address, port);
            }
        }
    }
}
