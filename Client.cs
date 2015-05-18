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

    public class Client : IDisposable {

        public enum Modes { Transaction, Listening }

        private TcpClient _client;
        private Modes _mode = Modes.Transaction;
        private bool _stopListening;
        private NetworkStream _netStream;
        public bool ExceptionOnStatus { get; set; }

        public string Host { get; private set; }
        public int Port { get; private set; }

        private Func<SendMessage, RecMessage> _trans;

        public class MessageReceivedEventArgs : EventArgs {
            public RecMessage Message { get; private set; }
            public MessageReceivedEventArgs(RecMessage message) {
                Message = message;
            }
        }
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler ServerClosed;

        public Dictionary<string, ClientHandler> Handlers = new Dictionary<string, ClientHandler>();

        public Exception LastException { get; private set; }

        private Thread _listeningThread;

        public Client(string host, int port, Modes mode = Modes.Transaction) {
            Host = host;
            Port = port;
            Mode = mode;
            _trans = Transaction;
        }

        public bool Open() {
            if (IsOpen) Close();
            try {
                _client = new TcpClient(Host, Port);
                _netStream = _client.GetStream();
            } catch (Exception ex) {
                LastException = ex;
                return false;
            }
            if (Mode == Modes.Listening) startListening();
            return true;
        }

        public void Close() {
            if (!IsOpen) return;
            if (_listeningThread != null) _stopListening = true;
            while (_listeningThread != null) Thread.Sleep(20);
            _client.Close();
            _netStream = null;
            _client = null;
        }

        protected virtual void OnMessageReceived(RecMessage message) {
            if (MessageReceived != null) MessageReceived(this, new MessageReceivedEventArgs(message));
        }

        protected virtual void OnServerClosed() {
            if (ServerClosed != null) ServerClosed(this, EventArgs.Empty);
        }

        public IAsyncResult BeginTransaction(SendMessage message, AsyncCallback callback = null, object data = null) {
            if (_mode != Modes.Transaction) throw new ClientModeException(true);
            return _trans.BeginInvoke(message, callback, data);
        }

        public RecMessage EndTransaction(IAsyncResult cookie) {
            return _trans.EndInvoke(cookie);
        }

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
                    if (ExceptionOnStatus && reply is RecStatusMessage) throw new StatusException((RecStatusMessage)reply);
                    return reply;
                } catch (SocketClosedException) {
                    throw new ServerClosedException();
                }
            }
        }

        public async Task<RecMessage> TransactionAsync(SendMessage message) {
            if (!IsOpen) throw new NotOpenException();
            return await Task<RecMessage>.Run(() => Transaction(message));
        }

        public void SendMessage(SendMessage message) {
            if (_mode != Modes.Listening) throw new ClientModeException(false);
            if (!IsOpen) throw new NotOpenException();
            lock (this) {
                message.Send(_netStream);
            }
        }

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
            _listeningThread.Start();
        }

        private void listen() {
            byte[] overrun = null;
            try {
                do {
                    if (_client == null) break;
                    if (_client.Available <= 0) {
                        Thread.Sleep(20);
                    } else {
                        var clientStream = new DelimitedStream(_netStream, overrun);
                        var header = new RecMessageHeader(clientStream);
                        if (header == null) {
                            // This shouldn't happen.
                            Close();
                            OnServerClosed();
                            break;
                        }
                        var msg = RecMessage.Create(header, clientStream);
                        if (Handlers.ContainsKey(header.Command)) {
                            CallHandler(msg);
                        } else {
                            OnMessageReceived(RecMessage.Create(header, clientStream));
                        }
                        overrun = clientStream.GetOverrun();
                    }
                } while (!_stopListening);
            } catch (SocketClosedException ex) {
                LastException = ex;
                Close();
                OnServerClosed();
            } finally {
                _listeningThread = null;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (_client != null) Close();
        }

        public void CallHandler(RecMessage recMessage) {
            if (Handlers.ContainsKey(recMessage.Command)) {
                var handler = Handlers[recMessage.Command];
                handler.Invoke(this, recMessage);
            }
        }

        public bool IsOpen {
            get { return _client != null; }
        }
    }

    public class ClientModeException : ApplicationException {
        private ClientModeException() { }

        public ClientModeException(bool inListeningMode)
            : base(inListeningMode ? "Cannot run a transaction while in Listening mode" : "Cannot send a bare message in Transaction mode") { }
    }

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

    public class NotOpenException : ApplicationException {
        public NotOpenException() : base("The SockLib client is not open") { }
    }

    public delegate void ClientHandler(Client client, RecMessage message);

    public class ServerClosedException : ApplicationException {
        public ServerClosedException() { }
    }
}
