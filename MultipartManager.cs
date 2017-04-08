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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {

    /// <summary>
    /// Manages the parsing of a RecMultipartMessage, when the Process method is called.
    /// For each uploaded file a FileUploaded event is raised that
    /// contains the header information sent by the client, and a stream containing the data.
    /// For each set of binary sent, a BinaryUploaded event is raised, and for each other data item
    /// a DataReceived event is raised.
    /// </summary>
    /// <remarks>
    /// A class can be derived from MultipartManager to avoid using the events, and respond using
    /// On... methods instead. The RecMultipartMessage.Manager property should be set to an instance
    /// of that class in that case.
    /// </remarks>
    public class MultipartManager {
        private List<DataItem> _dataItems = new List<DataItem>();
        private Stream _stream;

        /// <summary>
        /// Information about a simple data item.
        /// </summary>
        public class DataItem {
            /// <summary>
            /// The name of the data item
            /// </summary>
            public string Name { get; private set; }
            /// <summary>
            /// The value of the data item
            /// </summary>
            public string Value { get; private set; }
            private DataItem() { }
            internal DataItem(string name, string value) {
                Name = name;
                Value = value;
            }
        }

        /// <summary>
        /// Information about the binary info currently being streamed.
        /// </summary>
        public class BinaryInfo {
            /// <summary>
            /// The fields defined with the binary data
            /// </summary>
            public IDictionary<string, string> Fields { get; private set; }
            private BinaryInfo() { }
            internal BinaryInfo(IDictionary<string, string> fields) {
                Fields = fields;
            }
            /// <summary>
            /// The name of the data item
            /// </summary>
            public string Name { get { return Fields.ContainsKey("Name") ? Fields["Name"] : null; } }

            /// <summary>
            /// Reads the entire remainder of the stream into memory.
            /// </summary>
            /// <param name="s"></param>
            /// <returns></returns>
            /// <remarks>If desired, the binary stream can be passed to this method</remarks>
            public byte[] Read(Stream s) {
                using (var mem = new MemoryStream(8192)) {
                    s.CopyTo(mem);
                    mem.Seek(0, SeekOrigin.Begin);
                    return mem.ToArray();
                }
            }
        }

        /// <summary>
        /// Arguments for the BinaryUploaded event.
        /// </summary>
        public class BinaryUploadedEventArgs : EventArgs {
            /// <summary>
            /// Header information included with the binary
            /// </summary>
            public BinaryInfo Info { get; private set; }
            /// <summary>
            /// The binary data
            /// </summary>
            public Stream Contents { get; private set; }
            private BinaryUploadedEventArgs() { }
            internal BinaryUploadedEventArgs(BinaryInfo info, Stream contents) {
                Info = info;
                Contents = contents;
            }
        }

        /// <summary>
        /// Raised when a binary item is being processed.
        /// </summary>
        public event EventHandler<BinaryUploadedEventArgs> BinaryUploaded;

        /// <summary>
        /// Information about the file currently being processed.
        /// </summary>
        public class FileInfo : BinaryInfo {
            internal FileInfo(IDictionary<string, string> fields) : base(fields) { }
            /// <summary>
            /// Gets the name of the file being uploaded.
            /// </summary>
            public string Filename { get { return Fields.ContainsKey("Filename") ? Fields["Filename"] : null; } }
        }

        /// <summary>
        /// Arguments for the FileUploaded event.
        /// </summary>
        public class FileUploadedEventArgs : EventArgs {
            /// <summary>
            /// Gets header information about the file being uploaded.
            /// </summary>
            public FileInfo Info { get; private set; }
            /// <summary>
            /// The binary contents of the file being uploaded.
            /// </summary>
            public Stream Contents { get; private set; }
            private FileUploadedEventArgs() { }
            internal FileUploadedEventArgs(FileInfo info, Stream contents) {
                Info = info;
                Contents = contents;
            }
        }

        /// <summary>
        /// Raised for each file that is being uploaded.
        /// </summary>
        public event EventHandler<FileUploadedEventArgs> FileUploaded;

        /// <summary>
        /// Arguments for the DataReceived event.
        /// </summary>
        public class DataReceivedEventArgs : EventArgs {
            /// <summary>
            /// All of the data values received for this data item
            /// </summary>
            public IDictionary<string, string> Items { get; private set; }
            /// <summary>
            /// The name of the data item
            /// </summary>
            public string Name { get; private set; }
            /// <summary>
            /// The value of the data item
            /// </summary>
            public string Value { get; private set; }
            private DataReceivedEventArgs() { }
            internal DataReceivedEventArgs(IDictionary<string, string> items, string name, string value) {
                Items = items;
                Name = name;
                Value = value;
            }
        }

        /// <summary>
        /// Raised for each data item in the form that is not a filename (or a file upload with no files,
        /// in which case the Value is null). The Name/Value data is also available from the DataItems
        /// property.
        /// </summary>
        public event EventHandler<DataReceivedEventArgs> DataReceived;

        /// <summary>
        /// The data items (excluding uploaded files and binaries) that have been received.
        /// </summary>
        public IEnumerable<DataItem> DataItems { get { return _dataItems; } }

        /// <summary>
        /// Raises the BinaryUploaded event.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="contents"></param>
        protected virtual void OnBinaryUploaded(BinaryInfo info, Stream contents) {
            if (BinaryUploaded != null) BinaryUploaded(this, new BinaryUploadedEventArgs(info, contents));
        }

        /// <summary>
        /// Raises the FileUploaded event.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="contents"></param>
        protected virtual void OnFileUploaded(FileInfo info, Stream contents) {
            if (FileUploaded != null) FileUploaded(this, new FileUploadedEventArgs(info, contents));
        }

        /// <summary>
        /// Raises the DataReceived event.
        /// </summary>
        /// <param name="items"></param>
        /// <param name="name"></param>
        /// <param name="data"></param>
        protected virtual void OnDataReceived(IDictionary<string, string> items, string name, string data) {
            if (DataReceived != null) DataReceived(this, new DataReceivedEventArgs(items, name, data));
        }

        /// <summary>
        /// Creates a MultipartManager object operating on the given stream.
        /// </summary>
        /// <param name="stream">This would normally be the socket stream.</param>
        /// <remarks>
        /// Accessing the Manager property of a RecMultipartMessage will automatically create a base MultipartManager.
        /// It is only necessary to set the Manager property and use this constructor to use a manager of a class derived
        /// from MultipartManager.
        /// </remarks>
        public MultipartManager(Stream stream) {
            _stream = stream;
        }

        /// <summary>
        /// Processes the multipart data posted from the client, raising events as
        /// files and data items are reached.
        /// </summary>
        // This message is suppressed because the code analyser is convinced that disposing of a stream
        // also disposes of the underlying stream. DelimitedStream deliberately doesn't dispose of the
        // underlying stream because you can continue to read on it.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public void Process() {
            using (var mainstream = _stream is DelimitedStream ? (DelimitedStream)_stream : new DelimitedStream(_stream)) {
                do {
                    using (var reader = new DelimitedStream(mainstream)) {
                        var headers = readHeaders(reader);
                        if (headers == null) break;
                        var headerData = parseHeaders(headers);
                        var type = headerData.ContainsKey("_type") ? headerData["_type"] : "String";
                        if (type == "Binary") {
                            OnBinaryUploaded(new BinaryInfo(headerData), reader);
                        }
                        else if (type == "File") {
                            OnFileUploaded(new FileInfo(headerData), reader);
                        } else {
                            string name = headerData.ContainsKey("Name") ? headerData["Name"] : null;
                            string value;
                            using (var m = new MemoryStream()) {
                                reader.CopyTo(m);
                                m.Seek(0, SeekOrigin.Begin);
                                value = m.ToArray().ConvertToString();
                            }
                            _dataItems.Add(new DataItem(name, value));
                            OnDataReceived(headerData, name, value);
                        }
                        reader.SkipToEnd();
                    }
                } while (!mainstream.EndOfStream);
            }
        }

        /// <summary>
        /// Carries out processing asynchronously.
        /// </summary>
        /// <returns></returns>
        public async Task ProcessAsync() {
            await Task.Run(() => Process());
        }

        private string readHeaders(DelimitedStream reader) {
            StringBuilder headers = new StringBuilder();
            string line = reader.ReadLine();
            while (!string.IsNullOrEmpty(line)) {
                headers.AppendLine(line);
                line = reader.ReadLine();
            }
            if (line == null) return null;
            return headers.ToString();
        }

        private IDictionary<string, string> parseHeaders(string headers) {
            Match propertiesMatch = Regex.Match(headers,
                @"((?<Key>[^\:=]+)(?:[\=\:])(?:[\s]*)(?<Value>([^"";\s\r]+)|(""[^""]+""))(?:[;\s]*))+");

            var parsed = propertiesMatch.Groups["Key"].Captures.Cast<Capture>().Select((c, i) => new { c, i })
                .Join(propertiesMatch.Groups["Value"].Captures.Cast<Capture>().Select((c, i) => new { c, i }), key => key.i, value => value.i,
                    (key, value) => new KeyValuePair<string, string>(key.c.Value, value.c.Value))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.StartsWith("\"") ? kvp.Value.Substring(1, kvp.Value.Length - 2) : kvp.Value);

            return parsed;
        }
    }
}
