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
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Babbacombe.SockLib {
    /// <summary>
    /// The abstract base class for constructing messages to send.
    /// </summary>
    public abstract class SendMessage {
        /// <summary>
        /// Defines the identifier of the message type.
        /// </summary>
        protected abstract char MessageType { get; }
        /// <summary>
        /// Gets or sets the Command to send.
        /// </summary>
        public virtual string Command { get; set; }
        /// <summary>
        /// Gets or sets the identifier of the client.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Constructs a message to send.
        /// </summary>
        protected SendMessage() { }

        /// <summary>
        /// Overriden to send the message's data portion to the stream.
        /// </summary>
        /// <param name="stream"></param>
        protected abstract void SendData(Stream stream);

        protected const int CopyToBufSize = 8 * 1024;

        /// <summary>
        /// Object used to generate message delimiters, if not specified by the server or client.
        /// </summary>
        public static IDelimGen DefaultDelimGen { get; set; } = new DefaultDelimGen();

#if TEST
        public void Send(Stream stream, IDelimGen delimGen) {
#else
        internal void Send(Stream stream, IDelimGen delimGen) {
#endif
            bool inSendData = false;
            if (delimGen == null) delimGen = DefaultDelimGen;
            try {
                var delim = delimGen.MakeDelimiter();
                stream.Write(delim, 0, delim.Length);
                stream.WriteByte((byte)'\n');

                var type = (byte)MessageType.ToString()[0];
                stream.WriteByte(type);

                if (!string.IsNullOrWhiteSpace(Id)) {
                    var buf = Id.ConvertToBytes();
                    stream.Write(buf, 0, buf.Length);
                }
                stream.WriteByte((byte)'\n');

                if (!string.IsNullOrWhiteSpace(Command)) {
                    var buf = Command.ConvertToBytes();
                    stream.Write(buf, 0, buf.Length);
                }
                stream.WriteByte((byte)'\n');

                inSendData = true;
                SendData(stream);
                inSendData = false;
                stream.WriteByte((byte)'\n');
                stream.Write(delim, 0, delim.Length);
                stream.WriteByte((byte)'\n');
                stream.Flush();
            } catch (IOException ex) {
                if (!inSendData) throw new SocketClosedException(ex);
                throw;
            }
        }
    }

    /// <summary>
    /// A Ping message to be sent. Either a Ping (a request for a PingReply) or a PingReply,
    /// depending on the command.
    /// </summary>
    internal class SendPingMessage : SendMessage {
        /// <summary>
        /// Defines the identifier of the message type, in this case '@' (internal).
        /// </summary>
        protected override char MessageType => '@';

        public SendPingMessage(bool reply) {
            Command = reply ? "PingReply" : "Ping";
        }

        protected override void SendData(Stream stream) { }
    }

    /// <summary>
    /// A Client mode message to be sent from the client to the server, informing it of a change to
    /// Listening or transaction mode, and whether to send pings.
    /// </summary>
    internal class SendClientModeMessage : SendTextMessage {
        /// <summary>
        /// Defines the identifier of the message type, in this case '@' (internal).
        /// </summary>
        protected override char MessageType => '@';

        public SendClientModeMessage(bool listening, PingManager pm = null) {
            Command = "ClientMode" + (listening && pm != null ? "LY" : listening ? "LN" : "TN");
            if (listening && pm != null) {
                Text = $"{pm.PingInterval}\n{pm.PingTimeout}";
            }
        }
    }

    internal class SendCryptoCheckMessage : SendTextMessage {
        protected override char MessageType => '@';

        public SendCryptoCheckMessage() : base("CryptoCheck") { }

        public SendCryptoCheckMessage(bool supported) : base("CryptoCheck", supported ? "Y" : "N") { }
    }

    internal class SendCryptoKeyMessage : SendBinaryMessage {
        protected override char MessageType => '@';

        public SendCryptoKeyMessage(byte[] publicKey)
            : base("CryptoKey", publicKey) { }
    }

    /// <summary>
    /// A text message to be sent.
    /// </summary>
    public class SendTextMessage : SendMessage {
        /// <summary>
        /// Gets or sets the Text of the message.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Defines the identifer of the message type. In this case, 'T'.
        /// </summary>
        protected override char MessageType => 'T'; 

        /// <summary>
        /// Constructs an empty Text message.
        /// </summary>
        public SendTextMessage() { }

        /// <summary>
        /// Constructs a text message, setting the command and, optionally, the text.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="text"></param>
        public SendTextMessage(string command, string text = null) {
            Command = command;
            Text = text;
        }

        /// <summary>
        /// Gets the UTF-8 Text as a byte array. Override to use a different encoding.
        /// </summary>
        /// <returns></returns>
        protected virtual byte[] GetData() {
            if (Text == null) return null;
            return Encoding.UTF8.GetBytes(Text);
        }

        /// <summary>
        /// Sends the Text to the stream.
        /// </summary>
        /// <param name="stream"></param>
        protected override void SendData(Stream stream) {
            var buf = GetData();
            if (buf == null) return;
            try {
                stream.Write(buf, 0, buf.Length);
            } catch (IOException) {
                throw new SocketClosedException();
            }
        }
    }
    
    /// <summary>
    /// A status message to be sent.
    /// </summary>
    public class SendStatusMessage : SendTextMessage {
        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        public string Status { get; set; }
        /// <summary>
        /// Gets or sets a description of the status.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Defines the identifier of the message type. In this case, 'S'.
        /// </summary>
        protected override char MessageType => 'S';

        /// <summary>
        /// Constructs a status message.
        /// </summary>
        /// <param name="status"></param>
        /// <param name="description"></param>
        /// <param name="text"></param>
        public SendStatusMessage(string status, string description = null, string text = null) {
            Status = status;
            Description = description;
            Text = text;
        }

        /// <summary>
        /// Constructs a status message from an exception.
        /// </summary>
        /// <param name="ex"></param>
        public SendStatusMessage(Exception ex) : this("Exception", ex.Message, ex.ToString()) { }

        /// <summary>
        /// Gets the Command of the message, a combination of Status and Description. This property cannot be
        /// set directly.
        /// </summary>
        public override string Command {
            get { return string.Format("{0} {1}", Status, Description).Trim(); }
            set { throw new NotImplementedException(); }
        }
    }

    /// <summary>
    /// A Unicode message to be sent.
    /// </summary>
    public class SendUnicodeMessage : SendTextMessage {
        /// <summary>
        /// Defines the identifier of the message type, in this case 'U'.
        /// </summary>
        protected override char MessageType => 'U';
        
        /// <summary>
        /// Constructs an empty Unicode message.
        /// </summary>
        public SendUnicodeMessage() { }

        /// <summary>
        /// Constructs a Unicode message, setting the Command and, optionally, the Text.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="text"></param>
        public SendUnicodeMessage(string command, string text = null) : base(command, text) { }

        /// <summary>
        /// Gets the data portion as a Unicode string.
        /// </summary>
        /// <returns></returns>
        protected override byte[] GetData() {
            if (Text == null) return null;
            return Encoding.Unicode.GetBytes(Text);
        }
    }

    /// <summary>
    /// A message containing an XML document to be sent.
    /// </summary>
    public class SendXmlMessage : SendTextMessage {
        private XDocument _document;

        /// <summary>
        /// Defines the identifier of the message type. In this case, 'X'.
        /// </summary>
        protected override char MessageType => 'X';

        /// <summary>
        /// Constructs an empty XML message.
        /// </summary>
        public SendXmlMessage() { }

        /// <summary>
        /// Constructs an XML document, setting the Command and Document.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="document"></param>
        public SendXmlMessage(string command, XDocument document)
            : base(command, document.ToString()) {
            _document = document;
        }

        /// <summary>
        /// Gets or Sets the Document to be sent.
        /// </summary>
        public XDocument Document {
            get { return _document; }
            set {
                _document = value;
                Text = _document.ToString();
            }
        }
    }

    /// <summary>
    /// A message with binary data to be sent.
    /// </summary>
    public class SendBinaryMessage : SendMessage {
        /// <summary>
        /// Gets or sets the Data to be sent. Should not be used if the Stream property is used.
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Gets or sets a stream containing the data to be sent. Should not be used if the Data property is set.
        /// </summary>
        public Stream Stream { get; set; }

        /// <summary>
        /// Defines the identifier for the message type. In this case, 'B'.
        /// </summary>
        protected override char MessageType => 'B';

        /// <summary>
        /// Constructs an empty binary message.
        /// </summary>
        public SendBinaryMessage() { }

        /// <summary>
        /// Constructs a binary message, setting the Command and the data to be sent.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="data"></param>
        public SendBinaryMessage(string command, byte[] data) {
            Command = command;
            Data = data;
        }

        /// <summary>
        /// Constructs a binary message, setting the Command and the Stream containing the data to be sent.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="stream"></param>
        public SendBinaryMessage(string command, Stream stream = null) {
            Command = command;
            Stream = stream;
        }

        /// <summary>
        /// Sends the data.
        /// </summary>
        /// <param name="stream"></param>
        protected override void SendData(Stream stream) {
            try {
                if (Data != null) {
                    using (var mem = new MemoryStream(Data)) {
                        mem.CopyTo(stream, CopyToBufSize);
                    }
                } else if (Stream != null) {
                    Stream.CopyTo(stream, CopyToBufSize);
                }
            } catch (IOException ex) {
                throw new SocketClosedException(ex);
            }
        }
    }

    /// <summary>
    /// A message to be sent containing a list of filenames.
    /// </summary>
    public class SendFilenamesMessage : SendTextMessage {
        private List<string> _filenames = new List<string>();

        /// <summary>
        /// Defines the identifier of the message type. In this case, 'F'.
        /// </summary>
        protected override char MessageType => 'F';

        /// <summary>
        /// Constructs an empty filenames message.
        /// </summary>
        public SendFilenamesMessage() { }

        /// <summary>
        /// Constructs a filenames message, setting the Command and the list of Filenames.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="filenames"></param>
        public SendFilenamesMessage(string command, IEnumerable<string> filenames = null) {
            Command = command;
            if (filenames != null) Filenames = filenames;
        }

        /// <summary>
        /// Gets or sets the list of filenames to be sent.
        /// </summary>
        public IEnumerable<string> Filenames {
            get { return _filenames; }
            set {
                _filenames.Clear();
                if (value != null) {
                    _filenames.AddRange(value);
                }
            }
        }

        /// <summary>
        /// Adds a file to the list of filenames.
        /// </summary>
        /// <param name="filename"></param>
        public void AddFile(string filename) {
            _filenames.Add(filename);
        }

        /// <summary>
        /// Adds multiple files to the list of filenames.
        /// </summary>
        /// <param name="filenames"></param>
        public void AddFiles(IEnumerable<string> filenames) {
            _filenames.AddRange(filenames);
        }

        /// <summary>
        /// Gets the list of filenames for sending.
        /// </summary>
        /// <returns></returns>
        protected override byte[] GetData() {
            if (!_filenames.Any()) return null;
            var names = string.Join("\n", _filenames);
            return Encoding.UTF8.GetBytes(names);
        }
    }

    /// <summary>
    /// A message containing multiple data items and/or files to be sent.
    /// </summary>
    public class SendMultipartMessage : SendMessage {
        /// <summary>
        /// Gets or sets the items to be sent. Each can be a string, binary data or a file.
        /// </summary>
        public List<BaseItem> Items { get; set; } = new List<BaseItem>();

        /// <summary>
        /// The arguments for the GetItemStream event.
        /// </summary>
        public class GetItemStreamEventArgs : EventArgs {
            /// <summary>
            /// Gets the item the data is being requested for.
            /// </summary>
            public BaseItem Item { get; }

            /// <summary>
            /// Gets or sets the Stream containing the data to be sent.
            /// </summary>
            public Stream Stream { get; set; }

            /// <summary>
            /// Constructs a GetItemStreamEventArgs instance.
            /// </summary>
            /// <param name="item"></param>
            public GetItemStreamEventArgs(BaseItem item) {
                Item = item;
            }

            /// <summary>
            /// Gets the name of the Item.
            /// </summary>
            public string ItemName => Item.Name;

            /// <summary>
            /// Gets the Filename of the item. Null if there is no filename.
            /// </summary>
            public string Filename => Item is FileItem ? ((FileItem)Item).Filename : null;
        }
        
        /// <summary>
        /// Raised to request the stream containing the data for the item.
        /// </summary>
        public event EventHandler<GetItemStreamEventArgs> GetItemStream;

        /// <summary>
        /// Defines the identifier of the message type. In this case, 'M'.
        /// </summary>
        protected override char MessageType => 'M';

        /// <summary>
        /// Constructs an empty Multipart message.
        /// </summary>
        public SendMultipartMessage() { }

        /// <summary>
        /// Constructs a Multipart message, setting the Command and, optionally, a set of items.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="items"></param>
        public SendMultipartMessage(string command, IEnumerable<BaseItem> items = null) {
            Command = command;
            if (items != null) Items = items.ToList();
        }

        /// <summary>
        /// The abstract base class for Multipart items.
        /// An item contains a list of key/value pairs. There must always be one keyed on "Name".
        /// There may also be data be appended to the item.
        /// </summary>
        [Serializable]
        public abstract class BaseItem : Dictionary<string, string> {
            /// <summary>
            /// Constructs an item with the given name.
            /// </summary>
            /// <param name="name"></param>
            protected BaseItem(string name) {
                this["Name"] = name;
            }

            /// <summary>
            /// Puts the key/value pairs together into a string.
            /// </summary>
            /// <returns></returns>
            /// <remarks>Probably shouldn't ever be overridden, but made virtual just in case.</remarks>
            public virtual string GetHeader() {
                StringBuilder s = new StringBuilder();
                foreach (var f in this) {
                    if (s.Length > 0) s.Append("; ");
                    s.AppendFormat("{0}: {1}", f.Key, f.Value);
                }
                s.AppendLine();
                return s.ToString();
            }

            /// <summary>
            /// Gets whether the item's data is provided via a stream.
            /// </summary>
            public abstract bool DataIsStream { get; }

            /// <summary>
            /// Overidden to send the data stream for the item.
            /// </summary>
            /// <param name="s"></param>
            protected internal virtual void SendData(Stream s) { }

            /// <summary>
            /// Gets the value of the "Name" key/value pair.
            /// </summary>
            public string Name => this["Name"];

            /// <summary>
            /// Gets or sets a named value within the item.
            /// </summary>
            /// <param name="field"></param>
            /// <returns></returns>
            public new string this[string field] {
                get {
                    string v = ContainsKey(field) ? base[field] : null;
                    if (v == null || !v.StartsWith("\"") || !v.EndsWith("\"")) return v;
                    return v.Substring(1, v.Length - 2);
                }
                set {
                    if (value.Contains(' ')) value = '"' + value + '"';
                    if (ContainsKey(field)) {
                        base[field] = value;
                    } else {
                        Add(field, value);
                    }
                }
            }

            internal string Type {
                get { return this["_type"]; }
                set { this["_type"] = value; }
            }
        }

        /// <summary>
        /// An item that sends a string as its data.
        /// </summary>
        public class StringItem : BaseItem {

            /// <summary>
            /// Gets or sets the Data string to send.
            /// </summary>
            public string Data { get; set; }

            /// <summary>
            /// Creates a string item with a name and the data to send.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="data"></param>
            public StringItem(string name, string data)
                : base(name) {
                Data = data;
                Type = "String";
            }

            /// <summary>
            /// False, the data is not obtained from a stream.
            /// </summary>
            public override bool DataIsStream {
                get { return false; }
            }

            /// <summary>
            /// Sends the data.
            /// </summary>
            /// <param name="s"></param>
            protected internal override void SendData(Stream s) {
                SendMultipartMessage.sendString(s, Data, false);
            }
        }

        /// <summary>
        /// An item that sends binary as its data. The data can be set as a byte array or, if Data is null, 
        /// the SendMultipartMessage will ask for a stream to send in a GetItemStream event.
        /// </summary>
        public class BinaryItem : BaseItem {
            /// <summary>
            /// Gets or sets the data to send. Leave null to send a stream.
            /// </summary>
            public byte[] Data { get; set; }

            /// <summary>
            /// Creates a binary item, setting the Name and, optionally, the data.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="data"></param>
            public BinaryItem(string name, byte[] data = null)
                : base(name) {
                Data = data;
                Type = "Binary";
            }

            /// <summary>
            /// True if Data is null, otherwise False. If False, the SendMultipartMessage will request the data stream
            /// in a GetItemStream event.
            /// </summary>
            public override bool DataIsStream {
                get { return Data == null; }
            }

            /// <summary>
            /// Sends the data, if Data has been set.
            /// </summary>
            /// <param name="s"></param>
            protected internal override void SendData(Stream s) {
                try {
                    if (Data != null) s.Write(Data, 0, Data.Length);
                } catch (IOException) {
                    throw new SocketClosedException();
                }
            }
        }

        /// <summary>
        /// An item that sends a file's contents as its data. The SendMultipartMessage will ask for a stream containing
        /// the contents in a GetItemStream event. If no stream is provided, it will open the file and send the contents itself.
        /// </summary>
        public class FileItem : BaseItem {
            /// <summary>
            /// Creates a file item, setting the item Name and Filename properties to the given filename.
            /// </summary>
            /// <param name="filename"></param>
            public FileItem(string filename)
                : this(Path.GetFileName(filename), filename) { }

            /// <summary>
            /// Creates a file item, setting the item Name and Filename separately.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="filename"></param>
            public FileItem(string name, string filename)
                : base(name) {
                this["Filename"] = filename;
                Type = "File";
            }

            /// <summary>
            /// The "Filename" key/value pair.
            /// </summary>
            public string Filename { get { return this["Filename"]; } }

            /// <summary>
            /// True, file contents data items are always obtained from a stream.
            /// </summary>
            public override bool DataIsStream {
                get { return true; }
            }
        }

        /// <summary>
        /// Raises the GetItemStream event.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        protected virtual Stream OnGetItemStream(BaseItem item) {
            var ea = new GetItemStreamEventArgs(item);
            GetItemStream?.Invoke(this, ea);
            return ea.Stream;
        }

        /// <summary>
        /// Sends the data for all the data items in a multipart message. It will call OnGetItemStream
        /// (raise a GetItemStream event) for each item for which DataIsStream is true.
        /// </summary>
        /// <param name="stream"></param>
        protected override void SendData(Stream stream) {
            var delim = DefaultDelimGen.MakeDelimiter();
            foreach (var item in Items) {
                stream.Write(delim, 0, delim.Length);
                sendString(stream, "");
                sendString(stream, item.GetHeader());

                if (item.DataIsStream) {
                    var datastream = OnGetItemStream(item);
                    var fileItem = item as FileItem;
                    if (datastream == null && fileItem != null && File.Exists(fileItem.Filename)) {
                        datastream = File.OpenRead(fileItem.Filename);
                    }
                    try {
                        if (datastream != null) {
                            datastream.CopyTo(stream, CopyToBufSize);
                            datastream.Dispose();
                        }
                    } catch (IOException) {
                        throw new SocketClosedException();
                    }
                } else {
                    item.SendData(stream);
                }
                sendString(stream, null); // Send EOL before the terminating delimiter
                stream.Write(delim, 0, delim.Length);
                sendString(stream, "--");
            }
            stream.Flush();
        }

        private static void sendString(Stream s, string data, bool addEol = true) {
            if (data == null) data = "";
            if (addEol) data = data + "\r\n";
            if (data == "") return;
            var buf = data.ConvertToBytes();
            try {
                s.Write(buf, 0, buf.Length);
            } catch (IOException) {
                throw new SocketClosedException();
            }
        }
    }

    /// <summary>
    /// Exception raised if the socket is found to be closed while sending a message.
    /// </summary>
    [Serializable]
    public class SocketClosedException : ApplicationException {
        /// <summary>
        /// Creates a Socket Closed exception with the message "Socket Closed"
        /// </summary>
        public SocketClosedException() : base("Socket Closed") { }

        /// <summary>
        /// Creates a Socket Closed exception with the message "Socket Closed" and the exception
        /// that caused it to be raised.
        /// </summary>
        public SocketClosedException(Exception inner) : base("Socket Closed", inner) { }
    }
}
