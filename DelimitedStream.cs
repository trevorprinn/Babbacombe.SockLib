using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {
    internal class DelimitedStream : Stream {
        private Stream _stream;
        public string Delimiter { get; private set; }
        private bool _endOfStream;
        private Queue<int> _pushBackBuffer = new Queue<int>();
        private const int BufferSize = 8192;
        private byte[] _buffer = new byte[BufferSize];
        private int _position;
        private int _bufferCount;

        public DelimitedStream(Stream stream, string delimiter) {
            _stream = stream;
            Delimiter = delimiter;
        }

        public DelimitedStream(Stream stream) {
            _stream = stream;
            Delimiter = ReadLine();
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

        public override int Read(byte[] buffer, int offset, int count) {
            bool delimiterReached = false;
            if (_endOfStream && !_pushBackBuffer.Any()) return 0;

            List<int> delimiterBuffer = new List<int>();
            int delimCount = 0; // Count of how many delimiter characters have been read and matched (not inc \r\n)
            int bytesRead = 0;
            while (bytesRead < count && !delimiterReached) {
                int ch = ReadByte();
                if (ch < 0) {
                    _endOfStream = true;
                    if (delimiterBuffer.Any()) {
                        // Push back any delimiter data that has been held aside.
                        pushback(delimiterBuffer);
                        delimCount = 0;
                        continue;
                    }
                }
                if (!_pushBackBuffer.Any() && (ch == '\n' || ch == '\r')) {
                    if (delimCount == 0) {
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
                            _endOfStream = delimiterReached = true;
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
            return bytesRead;
        }

        private int readByte() {
            if (_pushBackBuffer.Any()) return _pushBackBuffer.Dequeue();
            if (_endOfStream) return -1;
            if (_position >= _bufferCount) {
                _position = 0;
                _bufferCount = _stream.Read(_buffer, 0, 8192);
                if (_bufferCount == 0) return -1;
            }
            return _buffer[_position++];
        }

        public byte[] GetOverrun() {
            if (_position >= _bufferCount) return new byte[0];
            var overrun = new byte[_bufferCount - _position];
            Array.Copy(_buffer, _position, overrun, 0, overrun.Length);
            return overrun;
        }

        public void PushbackOverrun(byte[] overrun) {
            foreach (byte b in overrun) {
                _pushBackBuffer.Enqueue(b);
            }
        }

        public override int ReadByte() {
            return readByte();
        }

        private void pushback(List<int> data, int ch = -1) {
            foreach (int c in data) _pushBackBuffer.Enqueue(c);
            data.Clear();
            if (ch >= 0) _pushBackBuffer.Enqueue(ch);
        }

        /// <summary>
        /// Reads and returns a line from the stream, not including the line delimiter.
        /// </summary>
        /// <returns>null at the end of the stream.</returns>
        public string ReadLine() {
            if (_endOfStream) return null;
            var buf = new StringBuilder();
            int ch = readByte();
            while (ch >= 0 && ch != '\n') {
                buf.Append((char)ch);
                ch = readByte();
            }
            if (ch < 0) _endOfStream = true;
            if (_endOfStream && buf.Length == 0) return null;
            while (buf.Length > 0 && buf[buf.Length - 1] == '\r') buf.Length--;
            return buf.ToString();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotImplementedException();
        }

        public override void SetLength(long value) {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotImplementedException();
        }

        public override long Position {
            get {
                throw new NotImplementedException();
            }
            set {
                throw new NotImplementedException();
            }
        }

        public override long Length {
            get { throw new NotImplementedException(); }
        }
    }
}
