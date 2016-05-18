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

    /// <summary>
    /// Manages the server end of a connection for one client. Stored in the Server's Clients collection.
    /// Derived classes can be created to store information against the client if required, in which case
    /// the Server's CreateClient method should be overridden to create them.
    /// </summary>
    public class ServerClient : IDisposable {

        /// <summary>
        /// Gets the server that the client is connected to.
        /// </summary>
        public Server Server { get; internal set; }

        /// <summary>
        /// Gets the underlying socket object.
        /// </summary>
        protected internal TcpClient Client { get; internal set; }

        /// <summary>
        /// Raised when the ServerClient object has been created and connected to the server, just before it
        /// is added to the Server's Clients collection.
        /// </summary>
        public event EventHandler Created;

        /// <summary>
        /// Raises the Created event.
        /// </summary>
        protected internal virtual void OnCreated() {
            Created?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Shuts down any remaining connection to the client app, and closes the socket.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Shuts down any remaining connection to the client app, and closes the socket.
        /// </summary>
        protected virtual void Dispose(bool disposing) {
            if (Client != null) {
                if (Client.Connected) Client.GetStream().Dispose();
                Client.Close();
                Client = null;
            }
        }

        /// <summary>
        /// Sends a message to the client app.
        /// </summary>
        /// <param name="message"></param>
        public void SendMessage(SendMessage message) {
            lock (this) {
                message.Send(Client.GetStream());
            }
        }
    }
}
