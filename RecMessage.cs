using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Babbacombe.SockLib {
    public abstract class RecMessage {
        public RecMessageHeader Header { get; private set; }
        protected Stream Stream { get; private set; }

        private RecMessage() { }

        internal RecMessage(RecMessageHeader header, Stream stream) {
            Header = header;
            Stream = stream;
        }

        public static RecMessage Create(RecMessageHeader header, Stream stream) {
            switch (header.Type) {
                case MessageTypes.Text: return new RecTextMessage(header, stream);
                case MessageTypes.Unicode: return new RecUnicodeMessage(header, stream);
                case MessageTypes.Xml: return new RecXmlMessage(header, stream);
                case MessageTypes.Binary: return new RecBinaryMessage(header, stream);
                case MessageTypes.Filenames: return new RecFilenamesMessage(header, stream);
                case MessageTypes.Multipart: return new RecMultipartMessage(header, stream);
                default: throw new ApplicationException("Unknown message type received");
            }
        }
    }

    public class RecTextMessage : RecMessage {
        private byte[] _data;

        internal RecTextMessage(RecMessageHeader header, Stream stream)
            : base(header, stream) {
            List<byte> data = new List<byte>();
            var buf = new byte[8192];
            int cnt;
            while ((cnt = stream.Read(buf, 0, 8192)) > 0) {
                data.AddRange(buf.Take(cnt));
            }
            _data = data.ToArray();
        }

        public string Text { get { return this.Encoding.GetString(_data.ToArray()); } }

        protected virtual Encoding Encoding { get { return Encoding.UTF8; } }
    }

    public class RecUnicodeMessage : RecTextMessage {
        internal RecUnicodeMessage(RecMessageHeader header, Stream stream) : base(header, stream) { }

        protected override Encoding Encoding {
            get { return Encoding.Unicode; }
        }
    }

    public class RecXmlMessage : RecTextMessage {
        public XDocument Document { get; private set; }

        internal RecXmlMessage(RecMessageHeader header, Stream stream)
            : base(header, stream) {
                Document = XDocument.Parse(Text);
        }
    }

    public class RecBinaryMessage : RecMessage {
        public new Stream Stream { get { return base.Stream; } }

        internal RecBinaryMessage(RecMessageHeader header, Stream stream) : base(header, stream) { }
    }

    public class RecFilenamesMessage : RecTextMessage {
        public string[] Filenames { get; private set; }

        internal RecFilenamesMessage(RecMessageHeader header, Stream stream)
            : base(header, stream) {
            Filenames = Text.Split('\n').Select(f => f.TrimEnd('\r')
                .Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)).ToArray();
        }

        public SendMultipartMessage CreateDefaultMessage() {
            var msg = new SendMultipartMessage();
            msg.Items = Filenames.Select(f => new SendMultipartMessage.Item("file", f, true));
            return msg;
        }
    }

    public class RecMultipartMessage : RecMessage {
        private MultipartManager _manager;

        internal RecMultipartMessage(RecMessageHeader header, Stream stream)
            : base(header, stream) {
        }

        public new Stream Stream { get { return base.Stream; } }

        public MultipartManager Manager {
            get {
                if (_manager == null) _manager = new MultipartManager(Stream);
                return _manager;
            }
            set {
                _manager = value;
            }
        }
    }
}
