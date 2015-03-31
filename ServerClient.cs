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
                Client.GetStream().Dispose();
                Client.Close();
                Client = null;
            }
        }

        public virtual void SendMessage(SendMessage message) {
            lock (this) {
                message.Send(Client.GetStream());
            }
        }
    }
}
