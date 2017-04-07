#region Licence
/*
    Babbacombe SockLib
    https://github.com/trevorprinn/SockLib
    Copyright © 2017 Babbacombe Computers Ltd.

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
