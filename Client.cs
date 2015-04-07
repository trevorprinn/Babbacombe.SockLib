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

        public enum Modes {  Transaction, Listening }

        private TcpClient _client;
        private Modes _mode = Modes.Transaction;
        private bool _stopListening;
        private NetworkStream _netStream;

        public class MessageReceivedEventArgs : EventArgs {
            public RecMessage Message { get; private set; }
            public MessageReceivedEventArgs(RecMessage message) {
                Message = message;
            }
        }
        public event EventHandler<MessageReceivedEventArgs> TransactionComplete;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler ServerClosed;

        public Client(string host, int port, Modes mode = Modes.Transaction) {
            _client = new TcpClient(host, port);
            _netStream = _client.GetStream();
            Mode = mode;
        }

        protected virtual void OnTransactionComplete(RecMessage message) {
            if (TransactionComplete != null) TransactionComplete(this, new MessageReceivedEventArgs(message));
        }

        protected virtual void OnMessageReceived(RecMessage message) {
            if (MessageReceived != null) MessageReceived(this, new MessageReceivedEventArgs(message));
        }

        protected virtual void OnServerClosed() {
            if (ServerClosed != null) ServerClosed(this, EventArgs.Empty);
        }

        public void BeginTransaction(SendMessage message) {
            if (_mode != Modes.Transaction) throw new ClientModeException(true);
            ThreadPool.QueueUserWorkItem((m) => {
                var reply = Transaction((SendMessage)m);
                OnTransactionComplete(reply);
            }, message);
        }

        public RecMessage Transaction(SendMessage message) {
            if (_mode != Modes.Transaction) throw new ClientModeException(true);
            lock (this) {
                message.Send(_client.GetStream());
                var recStream = new DelimitedStream(_netStream);
                var header = new RecMessageHeader(recStream);
                if (header.IsEmpty) return null;
                return RecMessage.Create(header, recStream);
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
                            OnMessageReceived(RecMessage.Create(header, clientStream));
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
                //_client.GetStream().Dispose();
                _client.Close();
                _client = null;
            }
        }
    }

    public class ClientModeException : ApplicationException {
        private ClientModeException() { }

        public ClientModeException(bool inListeningMode)
            : base(inListeningMode ? "Cannot run a transaction while in Listening mode" : "Cannot send a bare message in Transaction mode") { }
    }

}
