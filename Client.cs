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
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
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
        private Stream _readStream;
        private Stream _writeStream;
        private bool _busySending;

        private static readonly Lazy<bool> isRunningOnMonoValue = new Lazy<bool>(() =>
        {
            return Type.GetType("Mono.Runtime") != null;
        });

        private static bool isRunningOnMono => isRunningOnMonoValue.Value;

        private static readonly Lazy<bool> isRunningOnLinuxValue = new Lazy<bool>(() => {
            int p = (int)Environment.OSVersion.Platform;
            return (p == 4) || (p == 6) || (p == 128);
        });

        private static bool isRunningOnLinux => isRunningOnLinuxValue.Value;

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

        private ClientPingManager _pingManager;

        public bool UsingCrypto { get; private set; }

        /// <summary>
        /// Arguments for the MessageReceived event.
        /// </summary>
        public class MessageReceivedEventArgs : EventArgs {
            /// <summary>
            /// The message that has been received from the server.
            /// </summary>
            public RecMessage Message { get; private set; }
            internal MessageReceivedEventArgs(RecMessage message) {
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
        /// Raised if pings are not answered.
        /// </summary>
        public event EventHandler ConnectionLost;

        /// <summary>
        /// Gets the handlers for messages received from the server (in either mode).
        /// </summary>
        public ClientHandlers Handlers { get; private set; }

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
        public Client(string host, int port, Modes mode = Modes.Transaction, bool useCrypto = false)
            : this(getHostAddress(host), port, mode, useCrypto) {
                _givenHost = host;
        }

        /// <summary>
        /// Sets up (but does not open) a connection to the server.
        /// </summary>
        /// <param name="hostAddress">The address of the server.</param>
        /// <param name="port">The port number of the server.</param>
        /// <param name="mode">The mode to start the connection in. Defaults to Transaction.</param>
        public Client(IPAddress hostAddress, int port, Modes mode = Modes.Transaction, bool useCrypto = false)
            : this(new IPEndPoint(hostAddress, port), mode, useCrypto) { }

        /// <summary>
        /// Sets up (but does not open) a connection to the server.
        /// </summary>
        /// <param name="hostEp">The end point of the server.</param>
        /// <param name="mode">The mode to start the connection in. Defaults to Transaction.</param>
        public Client(IPEndPoint hostEp, Modes mode = Modes.Transaction, bool useCrypto = false) {
            Handlers = new ClientHandlers();
            HostEp = hostEp;
            Mode = mode;
            _trans = Transaction;
            UsingCrypto = SupportsCrypto && useCrypto;
            _pingManager = new ClientPingManager(this);
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
                _readStream = _client.GetStream();
                _writeStream = _client.GetStream();
            } catch (Exception ex) {
                LastException = ex;
                try { _client.Close(); } catch { }
                _client = null;
                return false;
            }
            if (UsingCrypto) initCrypto();
            if (Mode == Modes.Listening) startListening();
            return true;
        }

        private bool _closing;

        /// <summary>
        /// Closes the connection to the server.
        /// </summary>
        public void Close() {
            if (_client == null ||_closing) return;
            _closing = true;
            _pingManager.Stop();
            try {
                if (_listeningThread != null) {
                    _stopListening = true;
                    if (Thread.CurrentThread.ManagedThreadId == _listeningThread.ManagedThreadId) {
                        // Can't wait for it to end in this thread, because it won't
                        Task.Run(async () => {
                            while (_listeningThread != null) await Task.Delay(20);
                        }).Wait();
                    } else {
                        while (_listeningThread != null) Thread.Sleep(20);
                    }
                }
                _client.Close();
            } finally {
                _readStream = null;
                _writeStream = null;
                _client = null;
                _closing = false;
            }
        }

        private void initCrypto() {
            // Ask the server whether it supports Crypto
            var check = Transaction<RecCryptoCheckMessage>(new SendCryptoCheckMessage());
            if (!check.Supported) {
                UsingCrypto = false;
                return;
            }

            using (var dh = new ECDiffieHellmanCng()) {
                // Generate a public key
                dh.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
                dh.HashAlgorithm = CngAlgorithm.Sha512;
                var pk = dh.PublicKey.ToByteArray();
                // Send it to the server, and get the server's public key back.
                var pkReply = Transaction<RecCryptoKeyMessage>(new SendCryptoKeyMessage(pk));
                // Get the SHA512 key to use for the encryption of the messages
                var hashKey = dh.DeriveKeyMaterial(CngKey.Import(pkReply.PublicKey, CngKeyBlobFormat.EccPublicBlob));

                var cypher = new TribbleCipher.Tribble<SHA512>(hashKey, SHA512.Create());
                _readStream = new CryptoStream(_readStream, cypher.CreateDecryptor());
                _writeStream = new CryptoStream(_writeStream, cypher.CreateEncryptor());
            }
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

        internal void PingTimedOut() {
            Close();
            OnConnectionLost();
        }

        /// <summary>
        /// Raises the ConnectionLost event.
        /// </summary>
        protected virtual void OnConnectionLost() {
            if (ConnectionLost != null) ConnectionLost(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Begins an asynchronous Transaction() call. Can be called only in Transaction mode.
        /// </summary>
        /// <param name="message">The message to send to the server.</param>
        /// <param name="callback">The function to process the reply.</param>
        /// <param name="data">User data to pass to the callback function.</param>
        /// <returns>A cookie to pass into an EndTransaction call.</returns>
        public IAsyncResult BeginTransaction(SendMessage message, AsyncCallback callback = null, object data = null) {
            if (_mode != Modes.Transaction) throw new ClientModeException(true);
            return _trans.BeginInvoke(message, callback, data);
        }

        /// <summary>
        /// Ends an asynchronous Transaction() call.
        /// </summary>
        /// <param name="cookie">The cookie returned from the BeginTransaction call.</param>
        /// <returns>The reply from the server.</returns>
        public RecMessage EndTransaction(IAsyncResult cookie) {
            return _trans.EndInvoke(cookie);
        }

        /// <summary>
        /// Ends an asynchronous Transaction call.
        /// </summary>
        /// <typeparam name="T">The type of the message expected from the server.</typeparam>
        /// <param name="cookie">The cookie returned from the BeginTransaction call.</param>
        /// <returns>The reply from the server.</returns>
        public T EndTransaction<T>(IAsyncResult cookie) where T : RecMessage {
            return (T)_trans.EndInvoke(cookie);
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
                    message.Send(_writeStream);
                    var recStream = new DelimitedStream(_readStream);
                    var header = new RecMessageHeader(recStream);
                    if (header.IsEmpty) {
                        Close();
                        throw new ServerClosedException();
                    }
                    var reply = RecMessage.Create(header, recStream);
                    AutoHandled = false;
                    if (AutoHandle && CallHandler(reply)) {
                        AutoHandled = true;
                    } else if (ExceptionOnStatus && reply is RecStatusMessage) {
                        throw new StatusException((RecStatusMessage)reply);
                    }
                    return reply;
                } catch (SocketClosedException) {
                    Close();
                    throw new ServerClosedException();
                }
            }
        }

        /// <summary>
        /// Sends a message to the server and waits for the reply. Can be called only in Transaction mode.
        /// </summary>
        /// <typeparam name="T">The type of the message expected from the server.</typeparam>
        /// <param name="message">The message to send to the server.</param>
        /// <returns>The reply from the server.</returns>
        public T Transaction<T>(SendMessage message) where T : RecMessage {
            return (T)Transaction(message);
        }

        /// <summary>
        /// Carries out a Transaction() call asynchronously. Can only be called in Transaction mode.
        /// </summary>
        /// <param name="message">The message to send to the server.</param>
        /// <returns>The reply from the server.</returns>
        public async Task<RecMessage> TransactionAsync(SendMessage message) {
            return await TransactionAsync<RecMessage>(message);
        }

        /// <summary>
        /// Carries out a Transaction() call asynchronously. Can only be called in Transaction mode.
        /// </summary>
        /// <typeparam name="T">The type of the message expected from the server.</typeparam>
        /// <param name="message">The message to send to the server.</param>
        /// <returns>The reply from the server.</returns>
        public async Task<T> TransactionAsync<T>(SendMessage message) where T : RecMessage {
            if (!IsOpen) throw new NotOpenException();
            var completion = new TaskCompletionSource<T>();
            lock (this) {
                BeginTransaction(message, (r) => {
                    var reply = EndTransaction<T>(r);
                    completion.SetResult(reply);
                });
            }
            return await completion.Task;
        }

        /// <summary>
        /// Sends a message to the server, and does not wait for a reply. Can be called only in Listening mode.
        /// </summary>
        /// <param name="message">The message to send to the server.</param>
        public void SendMessage(SendMessage message) {
            // Send a ping message regardless of the mode
            if (_mode != Modes.Listening && !(message is SendPingMessage)) throw new ClientModeException(false);
            if (!IsOpen) {
                OnConnectionLost();
                return;
            }
            lock (this) {
                _busySending = true;
                try {
                    message.Send(_writeStream);
                } finally {
                    if (!(message is SendPingMessage)) _pingManager.Reset();
                    _busySending = false;
                }
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

        /// <summary>
        /// Gets or sets whether to send pings when in listening mode. Defaults to true.
        /// This should be set before the connection is made and put into listening mode.
        /// </summary>
        public bool SendPings { get; set; } = true;

        /// <summary>
        /// Gets or sets how often to send pings, in millisecs. Defaults to 500.
        /// This should be set before the connection is made and put into listening mode.
        /// </summary>
        public int PingInterval {
            get { return _pingManager.PingInterval; }
            set { _pingManager.PingInterval = value; }
        }

        /// <summary>
        /// Gets or sets how long to wait for a ping reply before assuming the connection has been lost, in millisecs.
        /// Defaults to 2000.
        /// This should be set before the connection is made and put into listening mode.
        /// </summary>
        public int PingTimeout {
            get { return _pingManager.PingTimeout; }
            set { _pingManager.PingTimeout = value; }
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
                if (SendPings) _pingManager.Start();
                SendMessage(new SendClientModeMessage(true, _pingManager));
                do {
                    if (_client == null) break;
                    if ((overrun == null || overrun.Length == 0) && _client.Available <= 0) {
                        ListenBusy = false;
                        Thread.Sleep(20);
                    } else {
                        ListenBusy = true;
                        using (var clientStream = new DelimitedStream(_readStream, overrun)) {
                            var header = new RecMessageHeader(clientStream);
                            if (header == null) {
                                // This shouldn't happen.
                                Close();
                                OnServerClosed();
                                break;
                            }
                            var msg = RecMessage.Create(header, clientStream);
                            if (msg is RecPingMessage) {
                                if (!((RecPingMessage)msg).IsReply) {
                                    // Send a ping reply
                                    SendMessage(new SendPingMessage(true));
                                }
                            } else if (!CallHandler(msg)) {
                                OnMessageReceived(msg);
                            }
                            overrun = clientStream.GetOverrun();
                        }
                        _pingManager.Reset();
                    }
                } while (!_stopListening);
                if (!_closing) {
                    try { SendMessage(new SendClientModeMessage(false)); } catch { }
                }
            } catch (SocketClosedException ex) {
                LastException = ex;
                Close();
                OnServerClosed();
            } finally {
                _pingManager.Stop();
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

        /// <summary>
        /// Closes the connection to the server.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing) {
            Close();
            _pingManager.Dispose();
        }

        /// <summary>
        /// Calls the handler set up for the message's command, if there is one.
        /// </summary>
        /// <param name="recMessage"></param>
        /// <returns>True if a handler has been set up for the command.</returns>
        public bool CallHandler(RecMessage recMessage) {
            if (!Handlers.HasHandler(recMessage.Command)) return false;
            Handlers.Invoke(recMessage.Command, this, recMessage);
            return true;
        }

        /// <summary>
        /// Gets whether the connection is currently open.
        /// NB On iOS this just returns whether the connection was previously open, as it is unable to
        /// determine whether the connection is still established.
        /// </summary>
        public bool IsOpen {
            get {
                if (_client == null) return false;
#if __IOS__
                // Can't check the connection on iOS, as far as I know, so this is the best way to check for now.
                // In future, perhaps send a small packet to test it.
                if (_client.Client.Connected) return true;
                Close();
                return false;
#else
#if ANDROID
                var connections = new DroidIPGlobalProperties().GetActiveTcpConnections();
#else
                var connections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
#endif
                var state = connections
                    .SingleOrDefault(x => x.LocalEndPoint.Equals(_client.Client.LocalEndPoint));
                if (state != null && correctedTcpState(state.State) == TcpState.Established) return true;
                Close();
                return false;
#endif
            }
        }

        /// <summary>
        /// The states that mono returns on linux are incorrect.
        /// https://bugzilla.xamarin.com/show_bug.cgi?id=15098
        /// http://git.kernel.org/cgit/linux/kernel/git/torvalds/linux.git/tree/include/net/tcp_states.h?id=HEAD
        /// </summary>
        /// <param name="origState"></param>
        /// <returns></returns>
        private TcpState correctedTcpState(TcpState origState) {
            if (!(isRunningOnMono && isRunningOnLinux)) return origState;
            switch ((int)origState) {
                case 1: return TcpState.Established;
                case 2: return TcpState.SynSent;
                case 3: return TcpState.SynReceived;
                case 4: return TcpState.FinWait1;
                case 5: return TcpState.FinWait2;
                case 6: return TcpState.TimeWait;
                case 7: return TcpState.Closed;
                case 8: return TcpState.CloseWait;
                case 9: return TcpState.LastAck;
                case 10: return TcpState.Listen;
                case 11: return TcpState.Closing;
                default: return TcpState.Unknown;
            }
        }

        internal bool Busy { get { return _busySending || ListenBusy; } }

        public bool SupportsCrypto
#if CRYPTO
            => true;
#else
            => false;
#endif

    }

    /// <summary>
    /// Thrown if a Transaction is called in Listening mode, or a bare message is sent in Transaction mode.
    /// </summary>
    [Serializable]
    public class ClientModeException : ApplicationException {
        private ClientModeException() { }

        /// <summary>
        /// Constructs a ClientModeException
        /// </summary>
        /// <param name="inListeningMode"></param>
        public ClientModeException(bool inListeningMode)
            : base(inListeningMode ? "Cannot run a transaction while in Listening mode" : "Cannot send a bare message in Transaction mode") { }
    }

    /// <summary>
    /// Thrown in Transaction mode if ExceptionOnStatus is true, and a status message is sent as the reply from the server.
    /// </summary>
    [Serializable]
    public class StatusException : ApplicationException {
        private StatusException() { }
        
        /// <summary>
        /// The status message sent by the server
        /// </summary>
        public RecStatusMessage StatusMessage { get; }

        internal StatusException(RecStatusMessage statusMessage) {
            StatusMessage = statusMessage;
        }

        /// <summary>
        /// The text of the status message sent by the server.
        /// </summary>
        public override string Message => StatusMessage.StatusMessage;

        /// <summary>
        /// The text of the status message followed by the full exception.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => string.Format("{0} {1}", StatusMessage.Status, base.ToString());
    }

    /// <summary>
    /// Thrown on an attempt to use a client that is not open.
    /// </summary>
    [Serializable]
    public class NotOpenException : ApplicationException {
        internal NotOpenException() : base("The SockLib client is not open") { }
    }

    /// <summary>
    /// A list of the handlers defined for the client.
    /// </summary>
    public class ClientHandlers {
        private class HandlerItem {
            public ClientHandler Handler { get; set; }
            public virtual void Invoke(Client client, RecMessage message) {
                Handler.Invoke(client, message);
            }
            public virtual Task InvokeAsync(Client client, RecMessage message) {
                throw new NotImplementedException();
            }
        }
        private class HandlerItem<T> : HandlerItem where T : RecMessage {
            public new ClientHandler<T> Handler { get; set; }
            public override void Invoke(Client client, RecMessage message) {
                Handler.Invoke(client, (T)message);
            }
        }
        private class AsyncHandlerItem : HandlerItem {
            public new ClientHandlerAsync Handler { get; set; }
            public override void Invoke(Client client, RecMessage message) {
                throw new NotImplementedException();
            }
            public override Task InvokeAsync(Client client, RecMessage message) {
                return Handler.Invoke(client, message);
            }
        }
        private class AsyncHandlerItem<T> : AsyncHandlerItem where T : RecMessage {
            public new ClientHandlerAsync<T> Handler { get; set; }
            public override Task InvokeAsync(Client client, RecMessage message) {
                return Handler.Invoke(client, (T)message);
            }
        }

        private Dictionary<string, HandlerItem> _handlers = new Dictionary<string, HandlerItem>();

        /// <summary>
        /// Adds a handler to the client.
        /// </summary>
        /// <param name="command">The command to handle.</param>
        /// <param name="handler">The handler routine.</param>
        public void Add(string command, ClientHandler handler) {
            _handlers.Add(command, new HandlerItem { Handler = handler });
        }

        /// <summary>
        /// Adds a handler to the client.
        /// </summary>
        /// <typeparam name="T">The type of message expected from the server.</typeparam>
        /// <param name="command">The command to handle.</param>
        /// <param name="handler">The handler routine.</param>
        public void Add<T>(string command, ClientHandler<T> handler) where T : RecMessage {
            _handlers.Add(command, new HandlerItem<T> { Handler = handler });
        }

        /// <summary>
        /// Adds a handler to the client to be run asynchronously.
        /// </summary>
        /// <param name="command">The command to handle.</param>
        /// <param name="handler">The handler routine.</param>
        public void AddAsync(string command, ClientHandlerAsync handler) {
            _handlers.Add(command, new AsyncHandlerItem { Handler = handler });
        }

        /// <summary>
        /// Adds a handler to the client to be run asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of message expected from the server.</typeparam>
        /// <param name="command">The command to handle.</param>
        /// <param name="handler">The handler routine.</param>
        public void AddAsync<T>(string command, ClientHandlerAsync<T> handler) where T : RecMessage {
            _handlers.Add(command, new AsyncHandlerItem<T> { Handler = handler });
        }

        internal bool HasHandler(string command) { return _handlers.ContainsKey(command); }

        internal void Invoke(string command, Client client, RecMessage message) {
            var item = _handlers[command];
            if (item is AsyncHandlerItem) {
                item.InvokeAsync(client, message);
                return;
            }
            item.Invoke(client, message);
        }

    }

    /// <summary>
    /// The type of handlers for messages sent from the server.
    /// </summary>
    /// <param name="client">The client that received the message.</param>
    /// <param name="message">The message received from the server.</param>
    public delegate void ClientHandler(Client client, RecMessage message);

    /// <summary>
    /// The type of handers for messages sent from the server.
    /// </summary>
    /// <typeparam name="T">The type of message expected from the server for the command.</typeparam>
    /// <param name="client">The client that received the message.</param>
    /// <param name="message">The message received from the server.</param>
    public delegate void ClientHandler<T>(Client client, T message) where T : RecMessage;

    /// <summary>
    /// The type of handlers for messages sent from the server that are to be handled asynchronously.
    /// </summary>
    /// <param name="client">The client that received the message.</param>
    /// <param name="message">The message received from the server.</param>
    /// <returns></returns>
    public delegate Task ClientHandlerAsync(Client client, RecMessage message);

    /// <summary>
    /// The type of handlers for messages sent from the server that are to be handled asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of message expected from the server for the command.</typeparam>
    /// <param name="client">The client that received the message.</param>
    /// <param name="message">The message received from the server.</param>
    /// <returns></returns>
    public delegate Task ClientHandlerAsync<T>(Client client, T message) where T : RecMessage;

    /// <summary>
    /// Thrown if the server closes while the client is open.
    /// </summary>
    [Serializable]
    public class ServerClosedException : ApplicationException {
        /// <summary>
        /// Creates a Server Closed Exception.
        /// </summary>
        public ServerClosedException() { }
    }
}
