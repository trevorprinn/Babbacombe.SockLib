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
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Babbacombe.SockLib {
    public abstract class SendMessage {
        protected abstract MessageTypes Type { get; }
        public virtual string Command { get; set; }
        public string Id { get; set; }

        protected SendMessage() { }

        protected abstract void SendData(Stream stream);

#if TEST
        public void Send(Stream stream) {
#else
        internal void Send(Stream stream) {
#endif
            bool inSendData = false;
            try {
                var delim = Encoding.UTF8.GetBytes(new string('-', 29) + Guid.NewGuid().ToString());
                stream.Write(delim, 0, delim.Length);
                stream.WriteByte((byte)'\n');

                var type = (byte)Type.ToString()[0];
                stream.WriteByte(type);

                if (!string.IsNullOrWhiteSpace(Id)) {
                    var buf = Encoding.UTF8.GetBytes(Id);
                    stream.Write(buf, 0, buf.Length);
                }
                stream.WriteByte((byte)'\n');

                if (!string.IsNullOrWhiteSpace(Command)) {
                    var buf = Encoding.UTF8.GetBytes(Command);
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

    public class SendTextMessage : SendMessage {
        public string Text { get; set; }

        protected override MessageTypes Type {
            get { return MessageTypes.Text; }
        }

        public SendTextMessage() { }

        public SendTextMessage(string command, string text = null) {
            Command = command;
            Text = text;
        }

        protected virtual byte[] GetData() {
            if (Text == null) return null;
            return Encoding.UTF8.GetBytes(Text);
        }

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

    public class SendStatusMessage : SendTextMessage {
        public string Status { get; set; }
        public string Description { get; set; }

        protected override MessageTypes Type {
            get { return MessageTypes.Status; }
        }

        public SendStatusMessage(string status, string description = null, string text = null) {
            Status = status;
            Description = description;
            Text = text;
        }

        public SendStatusMessage(Exception ex) : this("Exception", ex.Message, ex.ToString()) { }

        public override string Command {
            get { return string.Format("{0} {1}", Status, Description).Trim(); }
            set { throw new NotImplementedException(); }
        }

        protected override void SendData(Stream stream) { }
    }

    public class SendUnicodeMessage : SendTextMessage {
        protected override MessageTypes Type {
            get { return MessageTypes.Unicode; }
        }

        public SendUnicodeMessage() { }

        public SendUnicodeMessage(string command, string text = null) : base(command, text) { }

        protected override byte[] GetData() {
            if (Text == null) return null;
            return Encoding.Unicode.GetBytes(Text);
        }
    }

    public class SendXmlMessage : SendTextMessage {
        private XDocument _document;

        protected override MessageTypes Type {
            get { return MessageTypes.Xml; }
        }

        public SendXmlMessage() { }

        public SendXmlMessage(string command, XDocument document)
            : base(command, document.ToString()) {
            _document = document;
        }

        public XDocument Document {
            get { return _document; }
            set {
                _document = value;
                Text = _document.ToString();
            }
        }
    }

    public class SendBinaryMessage : SendMessage {
        public byte[] Data { get; set; }
        public Stream Stream { get; set; }

        protected override MessageTypes Type {
            get { return MessageTypes.Binary; }
        }

        public SendBinaryMessage() { }

        public SendBinaryMessage(string command, byte[] data) {
            Command = command;
            Data = data;
        }

        public SendBinaryMessage(string command, Stream stream = null) {
            Command = command;
            Stream = stream;
        }

        protected override void SendData(Stream stream) {
            try {
                if (Data != null) {
                    using (var mem = new MemoryStream(Data)) {
                        mem.CopyTo(stream);
                    }
                } else if (Stream != null) {
                    Stream.CopyTo(stream);
                }
            } catch (IOException) {
                throw new SocketClosedException();
            }
        }
    }

    public class SendFilenamesMessage : SendTextMessage {
        private List<string> _filenames = new List<string>();

        protected override MessageTypes Type {
            get { return MessageTypes.Filenames; }
        }

        public SendFilenamesMessage() { }

        public SendFilenamesMessage(string command, IEnumerable<string> filenames = null) {
            Command = command;
            if (filenames != null) Filenames = filenames;
        }

        public IEnumerable<string> Filenames {
            get { return _filenames; }
            set {
                _filenames.Clear();
                if (value != null) {
                    _filenames.AddRange(value);
                }
            }
        }

        public void AddFile(string filename) {
            _filenames.Add(filename);
        }

        public void AddFiles(IEnumerable<string> filenames) {
            _filenames.AddRange(filenames);
        }

        protected override byte[] GetData() {
            if (!_filenames.Any()) return null;
            var names = string.Join("\n", _filenames);
            return Encoding.UTF8.GetBytes(names);
        }
    }

    public class SendMultipartMessage : SendMessage {
        public List<BaseItem> Items = new List<BaseItem>();

        public class GetItemStreamEventArgs : EventArgs {
            public BaseItem Item { get; private set; }
            public Stream Stream { get; set; }
            public GetItemStreamEventArgs(BaseItem item) {
                Item = item;
            }

            public string ItemName { get { return Item.Name; } }
            public string Filename { get { return Item is FileItem ? ((FileItem)Item).Filename : null; } }
        }
        public event EventHandler<GetItemStreamEventArgs> GetItemStream;

        protected override MessageTypes Type {
            get { return MessageTypes.Multipart; }
        }

        public SendMultipartMessage() { }

        public SendMultipartMessage(string command, IEnumerable<BaseItem> items = null) {
            Command = command;
            Items = items.ToList();
        }

        public abstract class BaseItem : Dictionary<string, string> {
            protected BaseItem(string name) {
                this["Name"] = name;
            }

            public virtual string GetHeader() {
                StringBuilder s = new StringBuilder();
                foreach (var f in this) {
                    if (s.Length > 0) s.Append("; ");
                    s.AppendFormat("{0}: {1}", f.Key, f.Value);
                }
                s.AppendLine();
                return s.ToString();
            }

            public abstract bool DataIsStream { get; }

            protected internal virtual void SendData(Stream s) { }

            public string Name { get { return this["Name"]; } }

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

        public class StringItem : BaseItem {
            public string Data { get; set; }

            public StringItem(string name, string data)
                : base(name) {
                Data = data;
                Type = "String";
            }

            public override bool DataIsStream {
                get { return false; }
            }

            protected internal override void SendData(Stream s) {
                SendMultipartMessage.sendString(s, Data, false);
            }
        }

        public class BinaryItem : BaseItem {
            public byte[] Data { get; set; }

            public BinaryItem(string name, byte[] data = null)
                : base(name) {
                Data = data;
                Type = "Binary";
            }

            public override bool DataIsStream {
                get { return Data == null; }
            }

            protected internal override void SendData(Stream s) {
                try {
                    if (Data != null) s.Write(Data, 0, Data.Length);
                } catch (IOException) {
                    throw new SocketClosedException();
                }
            }
        }

        public class FileItem : BaseItem {
            public FileItem(string filename)
                : this(Path.GetFileName(filename), filename) { }

            public FileItem(string name, string filename)
                : base(name) {
                this["Filename"] = filename;
                Type = "File";
            }

            public string Filename { get { return this["Filename"]; } }

            public override bool DataIsStream {
                get { return true; }
            }
        }

        protected virtual Stream OnGetItemStream(BaseItem item) {
            var ea = new GetItemStreamEventArgs(item);
            if (GetItemStream != null) GetItemStream(this, ea);
            return ea.Stream;
        }

        protected override void SendData(Stream stream) {
            var delim = new string('-', 29) + Guid.NewGuid().ToString();
            foreach (var item in Items) {
                sendString(stream, delim);
                sendString(stream, item.GetHeader());

                if (item.DataIsStream) {
                    var datastream = OnGetItemStream(item);
                    var disposeStream = false;
                    var fileItem = item as FileItem;
                    if (datastream == null && fileItem != null && File.Exists(fileItem.Filename)) {
                        datastream = File.OpenRead(fileItem.Filename);
                        disposeStream = true;
                    }
                    try {
                        if (datastream != null) {
                            datastream.CopyTo(stream);
                            if (disposeStream) datastream.Dispose();
                        }
                    } catch (IOException) {
                        throw new SocketClosedException();
                    }
                } else {
                    item.SendData(stream);
                }
                sendString(stream, null); // Send EOL before the terminating delimiter
                sendString(stream, delim + "--");
            }
            stream.Flush();
        }

        private static void sendString(Stream s, string data, bool addEol = true) {
            if (data == null) data = "";
            if (addEol) data = data + "\r\n";
            if (data == "") return;
            var buf = Encoding.UTF8.GetBytes(data);
            try {
                s.Write(buf, 0, buf.Length);
            } catch (IOException) {
                throw new SocketClosedException();
            }
        }
    }

    public class SocketClosedException : ApplicationException {
        public SocketClosedException() { }
        public SocketClosedException(Exception inner) : base("Socket Closed", inner) { }
    }
}
