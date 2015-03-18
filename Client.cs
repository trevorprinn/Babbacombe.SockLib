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
        private TcpClient _client;

        public class TransactionCompleteEventArgs : EventArgs {
            public RecMessage Message { get; private set; }
            public TransactionCompleteEventArgs(RecMessage message) {
                Message = message;
            }
        }
        public event EventHandler<TransactionCompleteEventArgs> TransactionComplete;

        public Client(string host, int port) {
            _client = new TcpClient(host, port);
        }

        protected virtual void OnTransactionComplete(RecMessage message) {
            if (TransactionComplete != null) TransactionComplete(this, new TransactionCompleteEventArgs(message));
        }

        public void BeginTransaction(SendMessage message) {
            ThreadPool.QueueUserWorkItem((m) => {
                var reply = Transaction((SendMessage)m);
                OnTransactionComplete(reply);
            }, message);
        }

        public RecMessage Transaction(SendMessage message) {
            message.Send(_client.GetStream());
            var recStream = new DelimitedStream(_client.GetStream());
            var header = new RecMessageHeader(recStream);
            return RecMessage.Create(header, recStream);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (_client != null) {
                _client.GetStream().Dispose();
                _client.Close();
                _client = null;
            }
        }
    }
}
