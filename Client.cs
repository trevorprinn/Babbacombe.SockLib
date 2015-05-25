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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {

    /// <summary>
    /// Manages the client end of a client/server connection.
    /// </summary>
    public class Client : IDisposable {

        /// <summary>
        /// The modes a client can be in.
        /// </summary>
        public enum Modes {
            /// <summary>
            /// In Transaction mode, all communication is started by the client calling the Transaction method,
            /// and the Server should send a reply.
            /// </summary>
            Transaction, 
            /// <summary>
            /// In Listening mode, the client and server communicate with one another by sending messages at any time.
            /// </summary>
            Listening
        }

        private TcpClient _client;
        private Modes _mode = Modes.Transaction;
        private bool _stopListening;
        private NetworkStream _netStream;

        /// <summary>
        /// Whether to raise an exception automatically when a Status message is received as the reply in a Transaction.
        /// Defaults to False.
        /// </summary>
        public bool ExceptionOnStatus { get; set; }

        /// <summary>
        /// Gets the IPEndPoint of the server.
        /// </summary>
        public IPEndPoint HostEp { get; private set; }

        /// <summary>
        /// The host name passed in to the constructor (if there was one).
        /// </summary>
        private string _givenHost;

        /// <summary>
        /// Gets or sets whether handlers are automatically called for Transaction replies. Defaults to False.
        /// </summary>
        public bool AutoHandle { get; set; }

        /// <summary>
        /// Gets whether a handler was called for the last Transaction reply. False if AutoHandle was false,
        /// or if no handler has been set up for the command in the last reply.
        /// </summary>
        public bool AutoHandled { get; private set; }

        /// <summary>
        /// Gets the host name of the server.
        /// </summary>
        /// <remarks>
        /// If a host name was passed into the constructor, and the host was not found, the passed in name is returned,
        /// otherwise the host name obtained from the DNS server is returned.
        /// </remarks>
        public string Host {
            get { return HostEp.Address == IPAddress.None ? _givenHost : Dns.GetHostEntry(HostEp.Address).HostName; }
        }

        /// <summary>
        /// Gets the port number of the server.
        /// </summary>
        public int Port { get { return HostEp.Port; } }

        /// <summary>
        /// True in Listening mode while messages are being processed.
        /// </summary>
        public bool ListenBusy { get; private set; }

        private Func<SendMessage, RecMessage> _trans;

        /// <summary>
        /// Arguments for the MessageReceived event.
        /// </summary>
        public class MessageReceivedEventArgs : EventArgs {
            /// <summary>
            /// The message that has been received from the server.
            /// </summary>
            public RecMessage Message { get; private set; }
            public MessageReceivedEventArgs(RecMessage message) {
                Message = message;
            }
        }
        /// <summary>
        /// Raised in Listening mode when no handler has been declared for the message.
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// Raised in Listening mode if the server shuts down.
        /// </summary>
        public event EventHandler ServerClosed;

        /// <summary>
        /// Gets and sets the handlers for messages received from the server (in either mode).
        /// </summary>
        public Dictionary<string, ClientHandler> Handlers = new Dictionary<string, ClientHandler>();

        /// <summary>
        /// Gets the last exception that occurred in an Open() call, or when the server shuts down in Listening mode.
        /// </summary>
        public Exception LastException { get; private set; }

        private Thread _listeningThread;

        /// <summary>
        /// Sets up (but does not open) a connection to the server.
        /// </summary>
        /// <param name="host">The name or IP address of the server.</param>
        /// <param name="port">The port number of the server.</param>
        /// <param name="mode">The mode to start the connection in. Defaults to Transaction.</param>
        public Client(string host, int port, Modes mode = Modes.Transaction)
            : this(getHostAddress(host), port, mode) {
                _givenHost = host;
        }

        /// <summary>
        /// Sets up (but does not open) a connection to the server.
        /// </summary>
        /// <param name="hostAddress">The address of the server.</param>
        /// <param name="port">The port number of the server.</param>
        /// <param name="mode">The mode to start the connection in. Defaults to Transaction.</param>
        public Client(IPAddress hostAddress, int port, Modes mode = Modes.Transaction)
            : this(new IPEndPoint(hostAddress, port), mode) { }

        /// <summary>
        /// Sets up (but does not open) a connection to the server.
        /// </summary>
        /// <param name="hostEp">The end point of the server.</param>
        /// <param name="mode">The mode to start the connection in. Defaults to Transaction.</param>
        public Client(IPEndPoint hostEp, Modes mode = Modes.Transaction) {
            HostEp = hostEp;
            Mode = mode;
            _trans = Transaction;
        }

        private static IPAddress getHostAddress(string host) {
            try {
                return Dns.GetHostAddresses(host).Single(a => a.AddressFamily == AddressFamily.InterNetwork);
            } catch {
                return IPAddress.None;
            }
        }

        /// <summary>
        /// Opens the socket and connects to the server.
        /// </summary>
        /// <returns>True if successful. If False, the LastException property contains the exception that occurred.</returns>
        /// <remarks>If the connection is already open, it is closed and re-opened.</remarks>
        public bool Open() {
            if (IsOpen) Close();
            try {
                _client = new TcpClient();
                _client.Connect(HostEp);
                _netStream = _client.GetStream();
            } catch (Exception ex) {
                LastException = ex;
                try { _client.Close(); } catch { }
                _client = null;
                return false;
            }
            if (Mode == Modes.Listening) startListening();
            return true;
        }

        /// <summary>
        /// Closes the connection to the server.
        /// </summary>
        public void Close() {
            if (!IsOpen) return;
            if (_listeningThread != null) _stopListening = true;
            while (_listeningThread != null) Thread.Sleep(20);
            _client.Close();
            _netStream = null;
            _client = null;
        }

        /// <summary>
        /// Raises the OnMessageReceived event.
        /// </summary>
        /// <param name="message"></param>
        protected virtual void OnMessageReceived(RecMessage message) {
            if (MessageReceived != null) MessageReceived(this, new MessageReceivedEventArgs(message));
        }

        /// <summary>
        /// Raises the OnServerClosed event.
        /// </summary>
        protected virtual void OnServerClosed() {
            if (ServerClosed != null) ServerClosed(this, EventArgs.Empty);
        }

        
        /// <summary>
        /// Begins an asynchronous Transaction() call. Can be called only in Transaction mode.
        /// </summary>
        /// <param name="message">The message to send to the server.</param>
        /// <param name="callback">The function to process the reply.</param>
        /// <param name="data">User data to pass to the callback function.</param>
        /// <returns></returns>
        public IAsyncResult BeginTransaction(SendMessage message, AsyncCallback callback = null, object data = null) {
            if (_mode != Modes.Transaction) throw new ClientModeException(true);
            return _trans.BeginInvoke(message, callback, data);
        }

        /// <summary>
        /// Ends an asynchronous Transaction() call.
        /// </summary>
        /// <param name="cookie"></param>
        /// <returns></returns>
        public RecMessage EndTransaction(IAsyncResult cookie) {
            return _trans.EndInvoke(cookie);
        }

        /// <summary>
        /// Sends a message to the server and waits for the reply. Can be called only in Transaction mode.
        /// </summary>
        /// <param name="message">The message to send to the server.</param>
        /// <returns>The reply from the server.</returns>
        public RecMessage Transaction(SendMessage message) {
            if (_mode != Modes.Transaction) throw new ClientModeException(true);
            if (!IsOpen) throw new NotOpenException();
            lock (this) {
                try {
                    message.Send(_netStream);
                    var recStream = new DelimitedStream(_netStream);
                    var header = new RecMessageHeader(recStream);
                    if (header.IsEmpty) return null;
                    var reply = RecMessage.Create(header, recStream);
                    AutoHandled = false;
                    if (AutoHandle && CallHandler(reply)) {
                        AutoHandled = true;
                    } else if (ExceptionOnStatus && reply is RecStatusMessage) {
                        throw new StatusException((RecStatusMessage)reply);
                    }
                    return reply;
                } catch (SocketClosedException) {
                    throw new ServerClosedException();
                }
            }
        }

        /// <summary>
        /// Carries out a Transaction() call asynchronously. Can only be called in Transaction mode.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<RecMessage> TransactionAsync(SendMessage message) {
            if (!IsOpen) throw new NotOpenException();
            return await Task<RecMessage>.Run(() => Transaction(message));
        }

        /// <summary>
        /// Sends a message to the server, and does not wait for a reply. Can be called only in Listening mode.
        /// </summary>
        /// <param name="message"></param>
        public void SendMessage(SendMessage message) {
            if (_mode != Modes.Listening) throw new ClientModeException(false);
            if (!IsOpen) throw new NotOpenException();
            lock (this) {
                message.Send(_netStream);
            }
        }

        /// <summary>
        /// Sets or gets the mode, either Transaction or Listening.
        /// </summary>
        public Modes Mode {
            get { return _mode; }
            set {
                if (value == _mode) return;
                if (_mode == Modes.Listening) {
                    _stopListening = true;
                    while (_listeningThread != null) Thread.Sleep(20);
                } else if (IsOpen) {
                    startListening();
                }
                _mode = value;
            }
        }

        private void startListening() {
            if (_listeningThread != null) return;
            _listeningThread = new Thread(new ThreadStart(listen));
            _listeningThread.Name = "SockLib client listener";
            _listeningThread.IsBackground = true;
            _stopListening = false;
            ListenBusy = true;
            _listeningThread.Start();
        }

        private void listen() {
            byte[] overrun = null;
            try {
                do {
                    if (_client == null) break;
                    if ((overrun == null || overrun.Length == 0) && _client.Available <= 0) {
                        ListenBusy = false;
                        Thread.Sleep(20);
                    } else {
                        ListenBusy = true;
                        using (var clientStream = new DelimitedStream(_netStream, overrun)) {
                            var header = new RecMessageHeader(clientStream);
                            if (header == null) {
                                // This shouldn't happen.
                                Close();
                                OnServerClosed();
                                break;
                            }
                            var msg = RecMessage.Create(header, clientStream);
                            if (!CallHandler(msg)) {
                                OnMessageReceived(RecMessage.Create(header, clientStream));
                            }
                            overrun = clientStream.GetOverrun();
                        }
                    }
                } while (!_stopListening);
            } catch (SocketClosedException ex) {
                LastException = ex;
                Close();
                OnServerClosed();
            } finally {
                ListenBusy = false;
                _listeningThread = null;
            }
        }

        /// <summary>
        /// Closes the connection to the server.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            Close();
        }

        /// <summary>
        /// Calls the handler set up for the message's command, if there is one.
        /// </summary>
        /// <param name="recMessage"></param>
        /// <returns>True if a handler has been set up for the command.</returns>
        public bool CallHandler(RecMessage recMessage) {
            if (!Handlers.ContainsKey(recMessage.Command)) return false;
            var handler = Handlers[recMessage.Command];
            handler.Invoke(this, recMessage);
            return true;
        }

        /// <summary>
        /// Gets whether the connection is currently open.
        /// </summary>
        public bool IsOpen {
            get { return _client != null; }
        }
    }

    /// <summary>
    /// Thrown if a Transaction is called in Listening mode, or a bare message is sent in Transaction mode.
    /// </summary>
    public class ClientModeException : ApplicationException {
        private ClientModeException() { }

        public ClientModeException(bool inListeningMode)
            : base(inListeningMode ? "Cannot run a transaction while in Listening mode" : "Cannot send a bare message in Transaction mode") { }
    }

    /// <summary>
    /// Thrown in Transaction mode if ExceptionOnStatus is true, and a status message is sent as the reply from the server.
    /// </summary>
    public class StatusException : ApplicationException {
        private StatusException() { }
        public RecStatusMessage StatusMessage { get; private set; }

        public StatusException(RecStatusMessage statusMessage) {
            StatusMessage = statusMessage;
        }

        public override string Message {
            get {
                return StatusMessage.StatusMessage;
            }
        }

        public override string ToString() {
            return string.Format("{0} {1}", StatusMessage.Status, base.ToString());
        }
    }

    /// <summary>
    /// Thrown on an attempt to use a client that is not open.
    /// </summary>
    public class NotOpenException : ApplicationException {
        public NotOpenException() : base("The SockLib client is not open") { }
    }

    /// <summary>
    /// The type of handlers for messages sent from the server.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="message"></param>
    public delegate void ClientHandler(Client client, RecMessage message);

    
    /// <summary>
    /// Thrown if the server closes while the client is open.
    /// </summary>
    public class ServerClosedException : ApplicationException {
        public ServerClosedException() { }
    }
}
