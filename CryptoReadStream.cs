using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {
    class CryptoStream : Stream {
        private ICryptoTransform _transformer;
        private Stream _innerStream;

        public CryptoStream(Stream stream, ICryptoTransform transformer) {
            _transformer = transformer;
            _innerStream = stream;
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override long Position {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }
        public override long Length {
            get { throw new NotImplementedException(); }
        }
        public override void Write(byte[] buffer, int offset, int count) {
            var encData = new byte[count];
            _transformer.TransformBlock(buffer, offset, count, encData, 0);
            _innerStream.Write(encData, 0, encData.Length);
        }
        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotImplementedException();
        }
        public override bool CanSeek => false;
        public override void SetLength(long value) {
            throw new NotImplementedException();
        }
        public override void Flush() {
            _innerStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            var encData = new byte[count];
            var read = _innerStream.Read(encData, 0, count);
            if (read > 0) _transformer.TransformBlock(encData, 0, read, buffer, 0);
            return read;
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            _transformer.Dispose();
            _innerStream.Dispose();
        }
    }
}
