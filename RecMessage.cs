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
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Babbacombe.SockLib {

    /// <summary>
    /// The abstract base class for handling all received messages.
    /// </summary>
    public abstract class RecMessage {
        /// <summary>
        /// Gets the message header
        /// </summary>
        public RecMessageHeader Header { get; }
#if TEST
        public Stream Stream { get; private set; }
#else
        /// <summary>
        /// Gets the base stream containing the message's data.
        /// </summary>
        protected Stream Stream { get; }
#endif

        private RecMessage() { }

        /// <summary>
        /// Constructs a received message. Must be called by any subclass's constructor.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="stream"></param>
        public RecMessage(RecMessageHeader header, Stream stream) {
            Header = header;
            Stream = stream;
        }

        /// <summary>
        /// Gets the table of custom messages that have been set up.
        /// </summary>
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
                case '@': return getInternalMessage(header, stream);
                default:
                    var custom = CustomMessages.getMessage(header, stream);
                    if (custom == null) throw new ApplicationException($"Unknown message type '{header.MessageType}' received");
                    return custom;                    
            }
        }

        private static RecMessage getInternalMessage(RecMessageHeader header, Stream stream) {
            if (header.Command.StartsWith("Ping")) return new RecPingMessage(header, stream);
            if (header.Command.StartsWith("ClientMode")) return new RecClientModeMessage(header, stream);
            throw new ApplicationException($"Unknown internal message type '{header.Command} received");
        }

        /// <summary>
        /// Gets the command sent in the message
        /// </summary>
        public string Command => Header.Command;

        /// <summary>
        /// Gets the Id sent by the client to identify itself, or the one generated by the server if the client
        /// didn't send one.
        /// </summary>
        public string Id => Header.Id;

        /// <summary>
        /// Reads to the end of the stream (assumes there is not much data)
        /// </summary>
        protected void ReadToEnd() {
            while (Stream.Read(new byte[10], 0, 10) > 0) { }
        }
    }

    /// <summary>
    /// A received Ping message. Can be a ping (a request for a PingReply), or a reply from one, depending on the Command.
    /// </summary>
    internal class RecPingMessage : RecMessage {
        internal RecPingMessage(RecMessageHeader header, Stream stream) : base(header, stream) {
            ReadToEnd();
        }

        public bool IsReply { get { return Command == "PingReply"; } }
    }

    /// <summary>
    /// A Client Mode message. Sent by a client to tell the server that it is changing to Listening or Transaction
    /// mode, and whether to send pings (and if so what the interval and timeout of the pings should be).
    /// </summary>
    internal class RecClientModeMessage : RecTextMessage {
        internal RecClientModeMessage(RecMessageHeader header, Stream stream)
            : base(header, stream) {
        }

        public bool IsListening { get { return Command[10] == 'L'; } }

        public bool SendPings { get { return Command[11] == 'Y'; } }

        public int PingInterval { get { return SendPings ? Convert.ToInt32(Lines.First()) : 0; } }

        public int PingTimeout { get { return SendPings ? Convert.ToInt32(Lines.Skip(1).First()) : 0; } }
    }

    /// <summary>
    /// A received text message.
    /// </summary>
    public class RecTextMessage : RecMessage {
        private byte[] _data;

        protected internal RecTextMessage(RecMessageHeader header, Stream stream)
            : base(header, stream) {
            List<byte> data = new List<byte>();
            var buf = new byte[8192];
            int cnt;
            while ((cnt = stream.Read(buf, 0, 8192)) > 0) {
                data.AddRange(buf.Take(cnt));
            }
            _data = data.ToArray();
        }

        /// <summary>
        /// Gets the UTF-8 string in the message.
        /// </summary>
        public string Text => this.Encoding.GetString(_data.ToArray());

        /// <summary>
        /// Gets the Text split into lines.
        /// </summary>
        public IEnumerable<string> Lines => Text.Split('\n').Select(f => f.TrimEnd('\r'));

        /// <summary>
        /// Defines the encoding used for the text, which in this class is UTF-8.
        /// </summary>
        protected virtual Encoding Encoding => Encoding.UTF8;
    }

    /// <summary>
    /// A received status message.
    /// </summary>
    public class RecStatusMessage : RecTextMessage {
        private string _command;

        protected internal RecStatusMessage(RecMessageHeader header, Stream stream) : base(header, stream) {
            _command = header.Command;
        }

        /// <summary>
        /// Gets the status code.
        /// </summary>
        public string Status => _command.Split(' ')[0];

        /// <summary>
        /// Gets the message associated with the status.
        /// </summary>
        public string StatusMessage {
            get {
                var words = _command.Split(' ');
                if (words.Length < 2) return null;
                return string.Join(" ", words.Skip(1));
            }
        }
    }

    /// <summary>
    /// A Unicode text message.
    /// </summary>
    public class RecUnicodeMessage : RecTextMessage {
        protected internal RecUnicodeMessage(RecMessageHeader header, Stream stream) : base(header, stream) { }

        /// <summary>
        /// Defines the encoding used for the Text, which in this class is Unicode.
        /// </summary>
        protected override Encoding Encoding {
            get { return Encoding.Unicode; }
        }
    }

    /// <summary>
    /// A message containing an XML document.
    /// </summary>
    public class RecXmlMessage : RecTextMessage {
        /// <summary>
        /// Gets the document contained in the message.
        /// </summary>
        public XDocument Document { get; }

        protected internal RecXmlMessage(RecMessageHeader header, Stream stream)
            : base(header, stream) {
                Document = XDocument.Parse(Text);
        }
    }

    /// <summary>
    /// A message containing binary data.
    /// </summary>
    public class RecBinaryMessage : RecMessage {
        private byte[] _data;

        /// <summary>
        /// Gets a stream containing the data in the message.
        /// </summary>
        /// <remarks>
        /// Once the Data property is accessed the stream will be at the end and seek cannot be used on it.
        /// </remarks>
        public new Stream Stream { get { return base.Stream; } }

        protected internal RecBinaryMessage(RecMessageHeader header, Stream stream) : base(header, stream) { }

        /// <summary>
        /// Gets the data in the message.
        /// </summary>
        /// <remarks>
        /// If the Stream property has been used, the Data will start from the point the stream was read to.
        /// After accessing this property, the stream is at the end and seek cannot be used on it.
        /// </remarks>
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

    /// <summary>
    /// A message containing a list of filenames.
    /// </summary>
    public class RecFilenamesMessage : RecTextMessage {

        /// <summary>
        /// Gets the filenames sent in the message. Any directory separator characters are converted
        /// to those of the local machine.
        /// </summary>
        public string[] Filenames { get; }

        protected internal RecFilenamesMessage(RecMessageHeader header, Stream stream)
            : base(header, stream) {
            Filenames = Lines.Select(f => 
                f.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)).ToArray();
        }

        /// <summary>
        /// Creates the message a server will return on receiving a RecFilenamesMessage by default.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// By default, the returned message will send the requested files.
        /// </remarks>
        public SendMultipartMessage CreateDefaultMessage() {
            var msg = new SendMultipartMessage();
            msg.Items = Filenames.Select(f => new SendMultipartMessage.FileItem(f)).Cast<SendMultipartMessage.BaseItem>().ToList();
            return msg;
        }
    }

    /// <summary>
    /// A message containing multiple data items and/or files.
    /// </summary>
    public class RecMultipartMessage : RecMessage {
        private MultipartManager _manager;

        protected internal RecMultipartMessage(RecMessageHeader header, Stream stream)
            : base(header, stream) {
        }

        /// <summary>
        /// Gets the stream containing the message's data. This should be used only to initialise a MultipartManager,
        /// and should not be read directly.
        /// </summary>
        public new Stream Stream => base.Stream;

        /// <summary>
        /// Gets or sets the MultipartManager used to parse the message's Stream. If it is not set, an
        /// instance of the base MultipartManager is automatically created.
        /// </summary>
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

    /// <summary>
    /// Exception raised if a message with an unknown type identifier is received.
    /// </summary>
    [Serializable]
    public class UnknownMessageTypeException : ApplicationException {
        internal UnknownMessageTypeException(char type)
            : base(string.Format("Unknown Message Type '{0}' received", type)) { }
    }
}
