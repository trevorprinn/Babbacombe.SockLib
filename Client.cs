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

        private Func<SendMessage, RecMessage> _trans;

        public class MessageReceivedEventArgs : EventArgs {
            public RecMessage Message { get; private set; }
            public MessageReceivedEventArgs(RecMessage message) {
                Message = message;
            }
        }
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler ServerClosed;

        public Dictionary<string, Action<Client, RecMessage>> Handlers = new Dictionary<string, Action<Client, RecMessage>>();

        public Client(string host, int port, Modes mode = Modes.Transaction) {
            _client = new TcpClient(host, port);
            _netStream = _client.GetStream();
            Mode = mode;
            _trans = Transaction;
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
            lock (this) {
                message.Send(_netStream);
                var recStream = new DelimitedStream(_netStream);
                var header = new RecMessageHeader(recStream);
                if (header.IsEmpty) return null;
                var reply = RecMessage.Create(header, recStream);
                if (ExceptionOnStatus && reply is RecStatusMessage) throw new StatusException((RecStatusMessage)reply);
                return reply;
            }
        }

        public async Task<RecMessage> TransactionAsync(SendMessage message) {
            return await Task<RecMessage>.Run(() => Transaction(message));
        }

        public void SendMessage(SendMessage message) {
            if (_mode != Modes.Listening) throw new ClientModeException(false);
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
                } else {
                    var t = new Thread(new ThreadStart(listen));
                    t.Name = "SockLib client listener";
                    t.IsBackground = true;
                    _stopListening = false;
                    t.Start();
                }
                _mode = value;
            }
        }

        private void listen() {
            try {
                do {
                    if (_client == null) break;
                    if (_client.Available <= 0) {
                        Thread.Sleep(20);
                    } else {
                        var clientStream = new DelimitedStream(_netStream);
                        var header = new RecMessageHeader(clientStream);
                        if (header == null) {
                            // This shouldn't happen.
                            OnServerClosed();
                            break;
                        }
                        var msg = RecMessage.Create(header, clientStream);
                        if (Handlers.ContainsKey(header.Command)) {
                            CallHandler(msg);
                        } else {
                            OnMessageReceived(RecMessage.Create(header, clientStream));
                        }
                    }
                } while (!_stopListening);
            } catch (SocketException) {
                OnServerClosed();
            } finally {
                _mode = Modes.Transaction;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (_client != null) {
                _client.Close();
                _client = null;
            }
        }

        public void CallHandler(RecMessage recMessage) {
            if (Handlers.ContainsKey(recMessage.Command)) {
                var handler = Handlers[recMessage.Command];
                handler.Invoke(this, recMessage);
            }
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

}
