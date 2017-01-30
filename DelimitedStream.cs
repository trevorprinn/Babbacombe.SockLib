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
using System.Threading;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {
#if TEST
    public class DelimitedStream : Stream {
#else
    internal class DelimitedStream : Stream {
#endif
    private Stream _stream;
        public string Delimiter { get; private set; }
        // Flag that the end of the input stream has been reached. There may still be buffered data to be
        // read from this object.
        private bool _endOfStream;
        // Buffer for handling pushing back bytes that may have been a delimiter
        private Queue<int> _pushBackBuffer = new Queue<int>();
        // Buffer for handling bytes that have been read past but need to be processed
        private Queue<byte> _outerPushbackBuffer = new Queue<byte>();
        private const int BufferSize = 8 * 1024;
        private byte[] _buffer = new byte[BufferSize];
        private int _position;
        private int _bufferCount;

        public DelimitedStream(Stream stream, byte[] overrun = null) {
            _stream = stream;
            if (overrun != null && overrun.Any()) {
                if (overrun.Length > BufferSize) throw new ApplicationException($"Overrun too long, {overrun.Length} bytes");
                overrun.CopyTo(_buffer, 0);
                _bufferCount = overrun.Length;
            }
			try {
                do {
                    Delimiter = readLine(true);
                } while (Delimiter != null && Delimiter == "");
            } catch (IOException) {
                Delimiter = null;
            } catch (ObjectDisposedException) {
                Delimiter = null;
            }
        }

        public override bool CanRead {
            get { return true; }
        }

        public override bool CanWrite {
            get { return false; }
        }

        public override bool CanSeek {
            get { return false; }
        }

        public override void Flush() { }

        public void SkipToEnd() {
            int cnt;
            var buffer = new byte[BufferSize];
            do {
                cnt = Read(buffer, 0, BufferSize);
            } while (cnt > 0);
        }

        public bool EndOfStream { get { return _endOfStream && !_pushBackBuffer.Any() && !_outerPushbackBuffer.Any(); } }

        public override int Read(byte[] buffer, int offset, int count) {
            return readData(buffer, offset, count, false, CancellationToken.None).Value;
        }

        public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            return await readData(buffer, offset, count, true, cancellationToken).Task;
        }

        private struct TaskValue<T> {
            public TaskValue(bool async, T value) {
                Task = async ? new Task<T>(() => { return value; }) : null;
                Value = value;
            }
            public Task<T> Task { get; private set; }
            public T Value { get; private set; }
        }

        private TaskValue<int> readData(byte[] buffer, int offset, int count, bool async, CancellationToken cancelToken) {
            if (EndOfStream) {
                return new TaskValue<int>(async, 0);
            }

            int bytesRead = 0;

            while (_outerPushbackBuffer.Any() && bytesRead < count) {
                buffer[offset + bytesRead++] = _outerPushbackBuffer.Dequeue();
            }

            List<int> delimiterBuffer = new List<int>(100);
            int delimCount = 0; // Count of how many delimiter characters have been read and matched (not inc \r\n)
            while (bytesRead < count && !_endOfStream) {
                if (cancelToken.IsCancellationRequested) return new TaskValue<int>(async, -1);
                int ch = readByte(async, cancelToken);
                if (ch < 0) {
                    _endOfStream = true;
                    return new TaskValue<int>(async, bytesRead);
                }
                if (!_pushBackBuffer.Any() && (ch == '\n' || ch == '\r')) {
                    if (delimiterBuffer.Contains('\n')) {
                        // Found 2 new lines in a row.
                        pushback(delimiterBuffer, ch);
                        delimCount = 0;
                    } else if (delimCount == 0) {
                        // Possibly at or near start of delimiter
                        delimiterBuffer.Add(ch);
                    } else {
                        // End of line reached without matching the delimiter
                        pushback(delimiterBuffer, ch);
                        delimCount = 0;
                    }
                    continue;
                }
                if (delimiterBuffer.Any()) {
                    delimiterBuffer.Add(ch);
                    if (ch == Delimiter[delimCount]) {
                        delimCount++;
                        if (delimCount == Delimiter.Length) {
                            // Found the delimiter
                            // Read to the end of the line
                            do { ch = readByte(async, cancelToken); } while (!cancelToken.IsCancellationRequested && ch >= 0 && ch != '\n');
                            _endOfStream = true;
                            _pushBackBuffer.Clear();
                        }
                    } else {
                        // Doesn't match the delimiter - push what has been saved back on for normal processing
                        pushback(delimiterBuffer);
                        delimCount = 0;
                    }
                } else {
                    buffer[offset + bytesRead++] = (byte)ch;
                }
            }
            return new TaskValue<int>(async, bytesRead);
        }

        private int readByte(bool async, CancellationToken cancelToken) {
            if (EndOfStream) return -1;
            if (_pushBackBuffer.Any()) return _pushBackBuffer.Dequeue();
            if (_position >= _bufferCount) {
                _position = 0;
                if (async) {
                    var t = _stream.ReadAsync(_buffer, 0, BufferSize, cancelToken);
                    t.Wait();
                    _bufferCount = t.Result;
                } else {
                    _bufferCount = _stream.Read(_buffer, 0, BufferSize);
                }
                if (_bufferCount == 0) return -1;
            }
            return _buffer[_position++];
        }

        public byte[] GetOverrun() {
            if (_position >= _bufferCount && !_outerPushbackBuffer.Any()) return new byte[0];
            List<byte> overrun = new List<byte>(_outerPushbackBuffer);
            _outerPushbackBuffer.Clear();
            if (_position < _bufferCount) {
                var bufoverrun = new byte[_bufferCount - _position];
                Array.Copy(_buffer, _position, bufoverrun, 0, bufoverrun.Length);
                overrun.AddRange(bufoverrun);
            }
            return overrun.ToArray();
        }

        public void PushbackOverrun(byte[] overrun) {
            List<byte> b = new List<byte>(overrun);
            b.AddRange(_outerPushbackBuffer);
            _outerPushbackBuffer = new Queue<byte>(b);
        }

        public override int ReadByte() {
            var buf = new byte[1];
            var c = Read(buf, 0, 1);
            return c == 0 ? -1 : buf[0];
        }

        private void pushback(List<int> data, int ch = -1) {
            _pushBackBuffer.Clear();
            foreach (int c in data) _pushBackBuffer.Enqueue(c);
            data.Clear();
            if (ch >= 0) _pushBackBuffer.Enqueue(ch);
        }

        /// <summary>
        /// Reads and returns a line from the stream, not including the line delimiter.
        /// </summary>
        /// <returns>null at the end of the stream.</returns>
        public string ReadLine() {
            return readLine();
        }

        private string readLine(bool readingDelimiter = false) {
            if (EndOfStream) return null;
            var buf = new StringBuilder();
            int ch = readingDelimiter ? readByte(false, CancellationToken.None) : ReadByte();
            while (ch >= 0 && ch != '\n') {
                buf.Append((char)ch);
                ch = readingDelimiter ? readByte(false, CancellationToken.None) : ReadByte();
            }
            if (ch < 0) _endOfStream = true;
            if (_endOfStream && buf.Length == 0) return null;
            while (buf.Length > 0 && buf[buf.Length - 1] == '\r') buf.Length--;
            return buf.ToString();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override long Position {
            get {
                throw new NotSupportedException();
            }
            set {
                throw new NotSupportedException();
            }
        }

        public override long Length {
            get { throw new NotSupportedException(); }
        }

        protected override void Dispose(bool disposing) {
            var outerStream = _stream as DelimitedStream;
            if (outerStream != null) outerStream.PushbackOverrun(GetOverrun());
            base.Dispose(disposing);
        }
    }
}
