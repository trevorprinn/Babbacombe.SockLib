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
    /// A UDP server that can be used to advertise the IP address and port number of a SockLib server on a network.
    /// </summary>
    /// <remarks>
    /// DiscoverClient broadcasts UDP packets which this server reponds to.
    /// </remarks>
    public class DiscoverServer : IDisposable {
        private UdpClient _server;

        /// <summary>
        /// The data that gets sent when a client request is received.
        /// </summary>
        private byte[] _advertisment;

        private string _serviceName;

        private CancellationTokenSource _cancel;

        private Task _runTask;

        /// <summary>
        /// Starts the discovery server on the given port, advertising a service
        /// on the same port.
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="port"></param>
        public DiscoverServer(int port, string serviceName) : this(port, serviceName, port) { }

        /// <summary>
        /// Starts the discovery server on the given port, advertising a service
        /// on a different port.
        /// </summary>
        /// <param name="port"></param>
        /// <param name="serviceName"></param>
        /// <param name="servicePort"></param>
        public DiscoverServer(int port, string serviceName, int servicePort) {
            _serviceName = serviceName;
            _advertisment = Encoding.UTF8.GetBytes(string.Format("{0}:{1}", serviceName, servicePort));

            _server = new UdpClient(port);
            _cancel = new CancellationTokenSource();

            _runTask = Task.Run(() => runServer(_cancel.Token), _cancel.Token);
        }

        private async Task runServer(CancellationToken cancel) {
            while (!cancel.IsCancellationRequested) {
                var msg = await _server.ReceiveAsync(cancel);
                if (!cancel.IsCancellationRequested && msg != null &&
                    Encoding.UTF8.GetString(msg.Value.Buffer) == _serviceName) {
                    await _server.SendAsync(_advertisment, msg.Value.RemoteEndPoint, cancel);
                }
            }
            System.Diagnostics.Debug.WriteLine("Server stopped");
        }

        public async void Dispose() {
            _cancel.Cancel();
            await _runTask;
            _server.Close();
            _cancel.Dispose();
        }
    }
}
