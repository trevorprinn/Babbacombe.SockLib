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
    public class Server : IDisposable {
        private TcpListener _listener;
        private Thread _listenThread;
        private bool _stop;

        private List<ServerClient> _clients = new List<ServerClient>();

        public class MessageReceivedEventArgs : EventArgs {
            public ServerClient Client { get; private set; }
            public RecMessage Message { get; private set; }
            public SendMessage Reply { get; set; }
            public MessageReceivedEventArgs(ServerClient client, RecMessage message) {
                Client = client;
                Message = message;
            }
        }
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public class FilenamesMessageReceivedEventArgs : MessageReceivedEventArgs {
            public new RecFilenamesMessage Message { get { return (RecFilenamesMessage)base.Message; } }

            public FilenamesMessageReceivedEventArgs(ServerClient client, RecFilenamesMessage message)
                : base(client, message) {
                    Reply = message.CreateDefaultMessage();
            }
        }
        public event EventHandler<FilenamesMessageReceivedEventArgs> FilenamesMessageReceived;

        public Server(int port) : this("127.0.0.1", port) { }

        public Server(string address, int port) : this(IPAddress.Parse(address), port) { }

        public Server(IPAddress address, int port) : this(new IPEndPoint(address, port)) { }

        public Server(IPEndPoint address) {
            _listener = new TcpListener(address);
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
            using (var client = CreateClient()) {
                client.Server = this;
                client.Client = c;
                client.OnCreated();

                lock (_clients) _clients.Add(client);

                try {
                    do {
                        RecMessage msg;
                        RecMessageHeader header;
                        using (var recStream = new DelimitedStream(client.Client.GetStream())) {
                            header = new RecMessageHeader(recStream);
                            if (header.IsEmpty) break;
                            msg = RecMessage.Create(header, recStream);
                        }
                        SendMessage reply = null;
                        if (msg is RecFilenamesMessage) {
                            reply = OnFilenamesMessageReceived(client, (RecFilenamesMessage)msg);
                        }
                        if (reply == null) {
                            reply = OnMessageReceived(client, msg);
                        }
                        if (reply != null) {
                            reply.Id = string.IsNullOrWhiteSpace(header.Id) ? Guid.NewGuid().ToString() : header.Id;
                            client.SendMessage(reply);
                        }
                    } while (true);
                } finally {
                    lock (_clients) _clients.Remove(client);
                }
            }
        }

        protected virtual SendMessage OnFilenamesMessageReceived(ServerClient client, RecFilenamesMessage message) {
            var ea = new FilenamesMessageReceivedEventArgs(client, message);
            if (FilenamesMessageReceived != null) FilenamesMessageReceived(this, ea);
            return ea.Reply;
        }

        protected virtual SendMessage OnMessageReceived(ServerClient client, RecMessage message) {
            var ea = new MessageReceivedEventArgs(client, message);
            if (MessageReceived != null) MessageReceived(this, ea);
            return ea.Reply;
        }

        protected virtual ServerClient CreateClient() {
            return new ServerClient();
        }

        public IEnumerable<ServerClient> Clients {
            get { return _clients; }
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

        public void Broadcast(SendMessage message, IEnumerable<ServerClient> clients = null) {
            lock (_clients) {
                if (clients == null) clients = _clients;
                foreach (var c in clients) {
                    if (_clients.Contains(c)) {
                        c.SendMessage(message);
                    }
                }
            }
        }
    }
}
