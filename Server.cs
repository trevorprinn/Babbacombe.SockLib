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

        public class FilenamesMessageReceivedEventArgs : MessageReceivedEventArgs {
            public new RecFilenamesMessage Message { get { return (RecFilenamesMessage)base.Message; } }

            public FilenamesMessageReceivedEventArgs(ServerClient client, RecFilenamesMessage message)
                : base(client, message) {
                    Reply = new SendMultipartMessage("", Message.Filenames.Select(f => new SendMultipartMessage.Item(f)));
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

                var header = new RecMessageHeader(client.Stream);
                var msg = RecMessage.Create(header, client.Stream);
                SendMessage reply = null;
                if (msg is RecFilenamesMessage) {
                    reply = OnFilenamesMessageReceived(client, (RecFilenamesMessage)msg);
                }
                if (reply == null) {
                    reply = OnMessageReceived(client, msg);
                }
                reply.Id = string.IsNullOrWhiteSpace(header.Id) ? Guid.NewGuid().ToString() : header.Id;
                client.SendReply(reply);
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
