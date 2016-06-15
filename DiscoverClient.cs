#region Licence
/*
    Babbacombe SockLib
    https://github.com/trevorprinn/SockLib
    Copyright © 2015 Babbacombe Computers Ltd.

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301
    USA
 */
#endregion
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

        private int _port;

        /// <summary>
        /// Gets or Sets the send and receive timeouts when finding the service, in millisecs.
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Creates a discovery client to search for a discovery server.
        /// </summary>
        /// <param name="port">The UDP port to use for the search.</param>
        public DiscoverClient(int port) {
            _port = port;
            Timeout = 5000;
        }

        /// <summary>
        /// Looks for an advertisement from a discovery server.
        /// </summary>
        /// <param name="serviceName">The name of the service the discovery server supports.</param>
        /// <returns>The end point of the service, or null if no server was found.</returns>
        public IPEndPoint FindService(string serviceName) {
            var start = DateTime.Now;
            try {
                using (var client = new UdpClient(_port)) {
                    client.Client.ReceiveTimeout = Timeout;
                    do {
                        if (DateTime.Now.Subtract(start).TotalMilliseconds > Timeout) return null;
                        IPEndPoint rep = null;
                        var msg = client.Receive(ref rep);
                        if (msg == null) return null; // Timed out

                        var serviceEp = getEp(msg, rep, serviceName);
                        if (serviceEp != null) return serviceEp;
                    } while (true);
                }
            } catch (SocketException) {
                return null;
            }
        }

        /// <summary>
        /// Looks for an advertisment from a discovery server.
        /// </summary>
        /// <param name="serviceName">The name of the service the discovery server supports.</param>
        /// <returns>The end point of the service, or null if no server was found.</returns>
        public async Task<IPEndPoint> FindServiceAsync(string serviceName) {
            var start = DateTime.Now;
            using (var client = new UdpClient(_port)) {
                do {
                    if (DateTime.Now.Subtract(start).TotalMilliseconds > Timeout) return null;
                    var cts = new CancellationTokenSource(Timeout);
                    var msg = await client.ReceiveAsync(cts.Token);
                    if (msg == null) return null; // Timed out

                    var serviceEp = getEp(msg.Value.Buffer, msg.Value.RemoteEndPoint, serviceName);
                    if (serviceEp != null) return serviceEp;
                } while (true);
            }
        }

        private IPEndPoint getEp(byte[] msg, IPEndPoint rep, string serviceName) {
            var advert = Encoding.UTF8.GetString(msg);
            if (!advert.Contains(':')) return null;
            var parts = advert.Split(':');
            if (parts[0] != serviceName) return null;

            int port;
            if (!int.TryParse(parts[1], out port)) return null;
            return new IPEndPoint(rep.Address, port);
        }
    }
}
