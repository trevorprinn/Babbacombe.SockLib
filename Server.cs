using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {
    public class Server : IDisposable {
        private TcpListener _listener;
        private Thread _listenThread;
        private bool _stop;

        public class MessageReceivedEventArgs : EventArgs {
            public ServerClient Client { get; private set; }
            public RecMessage Message { get; private set; }
            public SendMessage Reply { get; set; }
            public MessageReceivedEventArgs(ServerClient client, RecMessage message) {
                Client = client;
                Message = message;
                Reply = new SendTextMessage();
            }
        }
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public Server(int port) {
            _listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            _listenThread = new Thread(new ThreadStart(listen));
            _listener.Start();
            _listenThread.Start();
        }

        private void listen() {
            while (!_stop) {
                if (_listener.Pending()) {
                    ThreadPool.QueueUserWorkItem((c) => {
                        handleClient((TcpClient)c);
                    }, _listener.AcceptTcpClient());
                }
                Thread.Sleep(100);
            }
            _listener.Stop();
            _listener = null;
            _listenThread = null;
        }

        private void handleClient(TcpClient c) {
            var client = CreateClient();
            client.Server = this;
            client.Client = c;
            client.OnCreated();

            var header = new RecMessageHeader(client.Stream);
            var msg = RecMessage.Create(header, client.Stream);
            var reply = OnMessageReceived(client, msg);
            reply.Id = string.IsNullOrWhiteSpace(header.Id) ? Guid.NewGuid().ToString() : header.Id;
            client.SendReply(reply);
        }

        protected virtual SendMessage OnMessageReceived(ServerClient client, RecMessage message) {
            var ea = new MessageReceivedEventArgs(client, message);
            if (MessageReceived != null) MessageReceived(this, ea);
            return ea.Reply;
        }

        protected virtual ServerClient CreateClient() {
            return new ServerClient();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (_listenThread != null) {
                _stop = true;
                while (_listenThread != null) Thread.Sleep(10);
            }
        }

    }
}
