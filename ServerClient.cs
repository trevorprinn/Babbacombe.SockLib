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

        public void SendMessage(SendMessage message) {
            lock (this) {
                message.Send(Client.GetStream());
            }
        }
    }
}
