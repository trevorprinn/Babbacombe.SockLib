using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {
    public class ServerClient : IDisposable {
        protected internal Server Server { get; internal set; }
        protected internal TcpClient Client { get; internal set; }
        private DelimitedStream _stream;

        public event EventHandler Created;

        protected internal virtual void OnCreated() {
            if (Created != null) Created(this, EventArgs.Empty);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (Client != null) {
                _stream.Dispose();
                Client.GetStream().Dispose();
                Client.Close();
                Client = null;
            }
        }

        public Stream Stream {
            get {
                if (_stream == null) _stream = new DelimitedStream(Client.GetStream());
                return _stream;
            }
        }

        public virtual void SendReply(SendMessage message) {
            message.Send(Client.GetStream());
        }
    }
}
