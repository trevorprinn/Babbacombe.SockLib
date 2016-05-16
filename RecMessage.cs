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
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Babbacombe.SockLib {
    public abstract class RecMessage {
        public RecMessageHeader Header { get; private set; }
#if TEST
        public Stream Stream { get; private set; }
#else
        protected Stream Stream { get; private set; }
#endif

        private RecMessage() { }

        public RecMessage(RecMessageHeader header, Stream stream) {
            Header = header;
            Stream = stream;
        }

        public static CustomMessageTable CustomMessages { get; } = new CustomMessageTable();

#if TEST
        public static RecMessage Create(RecMessageHeader header, Stream stream) {
#else
        internal static RecMessage Create(RecMessageHeader header, Stream stream) {
#endif
            switch (header.MessageType) {
                case 'T': return new RecTextMessage(header, stream);
                case 'S': return new RecStatusMessage(header, stream);
                case 'U': return new RecUnicodeMessage(header, stream);
                case 'X': return new RecXmlMessage(header, stream);
                case 'B': return new RecBinaryMessage(header, stream);
                case 'F': return new RecFilenamesMessage(header, stream);
                case 'M': return new RecMultipartMessage(header, stream);
                default:
                    var custom = CustomMessages.getMessage(header, stream);
                    if (custom == null) throw new ApplicationException($"Unknown message type '{header.MessageType}' received");
                    return custom;                    
            }
        }

        public string Command { get { return Header.Command; } }

        public string Id { get { return Header.Id; } }

        public char MessageType { get { return Header.MessageType;}}
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

        public IEnumerable<string> Lines {
            get { return Text.Split('\n').Select(f => f.TrimEnd('\r')); }
        }

        protected virtual Encoding Encoding { get { return Encoding.UTF8; } }
    }

    public class RecStatusMessage : RecTextMessage {
        private string _command;

        internal RecStatusMessage(RecMessageHeader header, Stream stream) : base(header, stream) {
            _command = header.Command;
        }

        public string Status {
            get { return _command.Split(' ')[0]; }
        }

        public string StatusMessage {
            get {
                var words = _command.Split(' ');
                if (words.Length < 2) return null;
                return string.Join(" ", words.Skip(1));
            }
        }
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
        private byte[] _data;
        public new Stream Stream { get { return base.Stream; } }

        internal RecBinaryMessage(RecMessageHeader header, Stream stream) : base(header, stream) { }

        public byte[] Data {
            get {
                if (_data == null) {
                    using (var mem = new MemoryStream()) {
                        Stream.CopyTo(mem);
                        mem.Seek(0, SeekOrigin.Begin);
                        _data = mem.ToArray();
                    }
                }
                return _data;
            }
        }
    }

    public class RecFilenamesMessage : RecTextMessage {
        public string[] Filenames { get; private set; }

        internal RecFilenamesMessage(RecMessageHeader header, Stream stream)
            : base(header, stream) {
            Filenames = Lines.Select(f => 
                f.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)).ToArray();
        }

        public SendMultipartMessage CreateDefaultMessage() {
            var msg = new SendMultipartMessage();
            msg.Items = Filenames.Select(f => new SendMultipartMessage.FileItem(f)).Cast<SendMultipartMessage.BaseItem>().ToList();
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

    [Serializable]
    public class UnknownMessageTypeException : ApplicationException {
        public UnknownMessageTypeException(char type)
            : base(string.Format("Unknown Message Type '{0}' received", type)) { }
    }
}
