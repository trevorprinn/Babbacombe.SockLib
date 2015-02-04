using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public void Send(Stream stream) {
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

            SendData(stream);
            stream.WriteByte((byte)'\n');
            stream.Write(delim, 0, delim.Length);
            stream.Flush();
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
            stream.Write(buf, 0, buf.Length);
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

        public SendXmlMessage(string command, string text = null) : base(command, text) { }

        public XDocument Document {
            get { return _document; }
            set {
                _document = value;
                Text = _document.ToString();
            }
        }
    }

    public class SendBinaryMessage : SendMessage {
        public Stream DataStream { get; set; }

        protected override MessageTypes Type {
            get { return MessageTypes.Binary; }
        }

        public SendBinaryMessage() { }

        public SendBinaryMessage(string command, Stream stream = null) {
            Command = command;
            SendData(stream);
        }

        protected override void SendData(Stream stream) {
            if (DataStream == null) return;
            DataStream.CopyTo(stream);
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
        private List<Item> _items = new List<Item>();

        public class GetFileDataEventArgs : EventArgs {
            public Item Item { get; private set; }
            public Stream Stream { get; set; }
            public GetFileDataEventArgs(Item item) {
                Item = item;
            }
        }
        public event EventHandler<GetFileDataEventArgs> GetFileData;

        protected override MessageTypes Type {
            get { return MessageTypes.Multipart; }
        }

        public SendMultipartMessage() { }

        public SendMultipartMessage(string command, IEnumerable<Item> items = null) {
            Command = command;
            Items = items;
        }

        public class Item {
            public string Name { get; private set; }
            public string Data { get; private set; }
            public bool IsFilename { get; private set; }
            public string ContentDisposition { get; set; }
            public string ContentType { get; set; }

            public Item(string name, string data, bool isFilename = false) {
                Name = name;
                IsFilename = isFilename;
                Data = data;
                ContentDisposition = "form-data";
                if (IsFilename) ContentType = "text/plain";
            }

            public string GetHeader() {
                StringBuilder s = new StringBuilder();
                s.AppendFormat("Content-Disposition: {0}; name = \"{1}\"", ContentDisposition, Name);
                if (IsFilename) {
                    s.AppendFormat("; filename=\"{0}\"", Path.GetFileName(Data));
                }
                if (ContentType != null) {
                    s.AppendLine();
                    s.AppendFormat("Content-Type: {0}", ContentType);
                }
                s.AppendLine();
                return s.ToString();
            }
        }

        public IEnumerable<Item> Items {
            get { return _items; }
            set {
                _items.Clear();
                if (value != null) _items.AddRange(value);
            }
        }

        public Item AddItem(string name, string data, bool isFilename = false) {
            var item = new Item(name, data, isFilename);
            _items.Add(item);
            return item;
        }

        protected virtual Stream OnGetFileData(Item item) {
            var ea = new GetFileDataEventArgs(item);
            if (GetFileData != null) GetFileData(this, ea);
            return ea.Stream;
        }

        protected override void SendData(Stream stream) {
            var delim = new string('-', 29) + Guid.NewGuid().ToString();
            foreach (var item in Items) {
                sendString(stream, delim);
                sendString(stream, item.GetHeader());
                if (item.IsFilename) {
                    var datastream = OnGetFileData(item);
                    var disposeStream = false;
                    if (datastream == null && File.Exists(item.Data)) {
                        datastream = File.OpenRead(item.Data);
                        disposeStream = true;
                    }
                    if (datastream != null) {
                        datastream.CopyTo(stream);
                        sendString(stream, null);
                        if (disposeStream) datastream.Dispose();
                    }
                } else {
                    sendString(stream, item.Data);
                }
                sendString(stream, delim + "--");
            }
            stream.Flush();
        }

        private void sendString(Stream s, string data, bool addEol = true) {
            if (data == null) data = "";
            if (addEol) data = data + "\r\n";
            if (data == "") return;
            var buf = Encoding.UTF8.GetBytes(data);
            s.Write(buf, 0, buf.Length);
        }
    }
}
