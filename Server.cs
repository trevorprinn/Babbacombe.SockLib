﻿#region Licence
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

    /// <summary>
    /// Manages the server end of a client/server connection.
    /// </summary>
    public class Server : IDisposable {
        private TcpListener _listener;
        private Thread _listenThread;
        private bool _stop;

        private List<ServerClient> _clients = new List<ServerClient>();

        public class ClientAddedRemovedEventArgs : EventArgs {
            public ServerClient Client { get; private set; }
            public ClientAddedRemovedEventArgs(ServerClient client) {
                Client = client;
            }
        }

        public event EventHandler<ClientAddedRemovedEventArgs> ClientAdded;

        public event EventHandler<ClientAddedRemovedEventArgs> ClientRemoved;

        /// <summary>
        /// Arguments for the MessageReceived event.
        /// </summary>
        public class MessageReceivedEventArgs : EventArgs {
            /// <summary>
            /// The Client that has sent the message.
            /// </summary>
            public ServerClient Client { get; private set; }
            /// <summary>
            /// The message sent by the client.
            /// </summary>
            public RecMessage Message { get; private set; }
            /// <summary>
            /// If set in the event handler, the reply to send to the client.
            /// </summary>
            public SendMessage Reply { get; set; }

            private MessageReceivedEventArgs() { }

            internal MessageReceivedEventArgs(ServerClient client, RecMessage message) {
                Client = client;
                Message = message;
            }
        }
        /// <summary>
        /// Raised when a message is received from a client for which no handler has been
        /// declared. Only raised for a Filenames message if the FilenamesReceived event handler
        /// returns a null reply (the default reply from that is a Multipart message).
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// Arguments for the FilenamesMessageReceived event.
        /// </summary>
        public class FilenamesMessageReceivedEventArgs : MessageReceivedEventArgs {
            /// <summary>
            /// The received message cast as a RecFilenameMessage.
            /// </summary>
            public new RecFilenamesMessage Message { get { return (RecFilenamesMessage)base.Message; } }

            internal FilenamesMessageReceivedEventArgs(ServerClient client, RecFilenamesMessage message)
                : base(client, message) {
                    Reply = message.CreateDefaultMessage();
            }
        }
        /// <summary>
        /// Raised when a Filenames message is received for which no handler has been declared. By default,
        /// a reply will be constructed sending the requested files in a Multipart message. If a null Reply is
        /// returned by the event handler, the MessageReceived event will be raised.
        /// </summary>
        public event EventHandler<FilenamesMessageReceivedEventArgs> FilenamesMessageReceived;

        /// <summary>
        /// Gets the handlers declared for messages received from clients.
        /// </summary>
        public ServerHandlers Handlers { get; private set; }

        /// <summary>
        /// Creates a server listening on localhost.
        /// </summary>
        /// <param name="port">The port number to listen on.</param>
        public Server(int port) : this(IPAddress.Any, port) { }

        /// <summary>
        /// Creates a server listening on a specific address.
        /// </summary>
        /// <param name="address">The address to listen on.</param>
        /// <param name="port">The port number to listen on.</param>
        public Server(string address, int port) : this(IPAddress.Parse(address), port) { }

        /// <summary>
        /// Creates a server listening on a specific address.
        /// </summary>
        /// <param name="address">The address to listen on.</param>
        /// <param name="port">The port number to listen on.</param>
        public Server(IPAddress address, int port) : this(new IPEndPoint(address, port)) { }

        /// <summary>
        /// Creates a server listening on a specific address.
        /// </summary>
        /// <param name="address">The address/port to listen on.</param>
        public Server(IPEndPoint address) {
            Handlers = new ServerHandlers();

            _listener = new TcpListener(address);
            _listenThread = new Thread(new ThreadStart(listen));
            _listener.Start();
            _listenThread.Start();
        }

        private void listen() {
            while (!_stop) {
                if (_listener.Pending()) {
                    ThreadPool.QueueUserWorkItem((c) => {
                        // Run each client connection on its own thread.
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
                OnClientAdded(client);

                byte[] overrun = null;
                try {
                    do {
                        RecMessage msg;
                        RecMessageHeader header;
                        SendMessage reply = null;
                        using (var recStream = new DelimitedStream(client.Client.GetStream(), overrun)) {
							if (recStream.Delimiter == null) break;
                            // Wait until a message is received.
                            header = new RecMessageHeader(recStream);
                            if (header.IsEmpty) break;
                            msg = RecMessage.Create(header, recStream);
                            // If there is more than one message waiting, the DelimitedStream
                            // may have buffered past the end of the first message. This overrun
                            // needs to be put back onto the beginning of the next stream.
                            if (Handlers.HasHandler(msg.Command)) {
                                // There's a handler for this command, so call it.
                                reply = Handlers.Invoke(msg.Command, client, msg);
                            } else {
                                if (msg is RecFilenamesMessage) {
                                    reply = OnFilenamesMessageReceived(client, (RecFilenamesMessage)msg);
                                }
                                if (reply == null) {
                                    reply = OnMessageReceived(client, msg);
                                }
                            }
                            overrun = recStream.GetOverrun();
                        }
                        if (reply != null) {
                            // Send a reply if one has been specified.
                            // Put a client Id in the reply, either the one sent by the client, or a new one.
                            reply.Id = string.IsNullOrWhiteSpace(header.Id) ? Guid.NewGuid().ToString() : header.Id;
                            client.SendMessage(reply);
                        }
                    } while (true);
                } finally {
                    lock (_clients) {
                        _clients.Remove(client);
                        OnClientRemoved(client);
                        client.Dispose();
                    }
                }
            }
        }

        protected virtual void OnClientAdded(ServerClient client) {
            if (ClientAdded != null) ClientAdded(this, new ClientAddedRemovedEventArgs(client));
        }

        protected virtual void OnClientRemoved(ServerClient client) {
            if (ClientRemoved != null) ClientRemoved(this, new ClientAddedRemovedEventArgs(client));
        }

        /// <summary>
        /// Raises the FilenamesMessageReceived event.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual SendMessage OnFilenamesMessageReceived(ServerClient client, RecFilenamesMessage message) {
            var ea = new FilenamesMessageReceivedEventArgs(client, message);
            if (FilenamesMessageReceived != null) FilenamesMessageReceived(this, ea);
            return ea.Reply;
        }

        /// <summary>
        /// Raises the MessageReceived event.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual SendMessage OnMessageReceived(ServerClient client, RecMessage message) {
            var ea = new MessageReceivedEventArgs(client, message);
            if (MessageReceived != null) MessageReceived(this, ea);
            return ea.Reply;
        }

        /// <summary>
        /// Called when a client first makes a connection. By default, creates a base ServerClient
        /// object, but can be overridden to create a ServerClient derivative.
        /// </summary>
        /// <returns></returns>
        protected virtual ServerClient CreateClient() {
            return new ServerClient();
        }

        /// <summary>
        /// Gets a list of the current clients.
        /// </summary>
        public IEnumerable<ServerClient> Clients {
            get { return _clients; }
        }

        /// <summary>
        /// Shuts down the server.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (_listenThread != null) {
                _stop = true;
                while (_listenThread != null) Thread.Sleep(10);
            }
            lock (_clients) foreach (var client in _clients.ToArray()) client.Dispose();
        }

        /// <summary>
        /// Sends the message to a list of clients, or all clients.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="clients">The clients to send to, or null to send to all.</param>
        /// <remarks>
        /// Should only be called for clients that are in Listening mode.
        /// The message sent should be a small one. For example, a multipart message containing large
        /// files will read them for each client.
        /// </remarks>
        public void Broadcast(SendMessage message, IEnumerable<ServerClient> clients = null) {
            lock (_clients) {
                if (clients == null) clients = _clients;
                Parallel.ForEach(clients, c => {
                    if (_clients.Contains(c)) {
                        c.SendMessage(message);
                    }
                });
            }
        }
    }

    public class ServerHandlers {
        private class HandlerItem {
            public ServerHandler Handler { get; set; }
            public virtual SendMessage Invoke(ServerClient client, RecMessage message) {
                return Handler.Invoke(client, message);
            }
        }
        private class HandlerItem<T> : HandlerItem where T : RecMessage {
            public new ServerHandler<T> Handler { get; set; }
            public override SendMessage Invoke(ServerClient client, RecMessage message) {
                return Handler.Invoke(client, (T)message);
            }
        }
        private Dictionary<string, HandlerItem> _handlers = new Dictionary<string, HandlerItem>();

        public void Add(string command, ServerHandler handler) {
            _handlers.Add(command, new HandlerItem { Handler = handler });
        }

        public void Add<T>(string command, ServerHandler<T> handler) where T : RecMessage {
            _handlers.Add(command, new HandlerItem<T> { Handler = handler });
        }

        internal bool HasHandler(string command) { return _handlers.ContainsKey(command); }

        internal SendMessage Invoke(string command, ServerClient client, RecMessage message) {
            var item = _handlers[command];
            return item.Invoke(client, message);
        }
    }

    public delegate SendMessage ServerHandler(ServerClient client, RecMessage message);

    public delegate SendMessage ServerHandler<T>(ServerClient client, T message) where T : RecMessage;
}
