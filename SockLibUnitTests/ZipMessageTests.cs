using Babbacombe.SockLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SockLibUnitTests {
    [TestClass]
    public class ZipMessageTests {

        [TestMethod]
        public void TransferZippedData() {
            RecMessage.CustomMessages.Add('Z', typeof(RecZipMessage));

            using (var f = new RandomFile(10.Megs()))
            using (var fs = f.GetStream()) {
                var msg = new SendZipMessage("TestZipStream", fs);
                var reply = (RecZipMessage)MessageTests.TransferMessage(msg);
                Assert.IsTrue(f.IsEqual(reply.GetDataStream()));
                reply.Stream.Dispose();
            }

            using (var f = new RandomFile(10.Megs(), "\r\n"))
            using (var fs = f.GetStream()) {
                var msg = new SendZipMessage("TestZipStream", fs);
                var reply = (RecZipMessage)MessageTests.TransferMessage(msg);
                Assert.IsTrue(f.IsEqual(reply.GetDataStream()), "With \\r\\n at end");
                reply.Stream.Dispose();
            }
        }
    }

    public class SendZipMessage : SendMessage {
        private Stream _inStream;

        public SendZipMessage(string command, Stream stream) : base() {
            Command = command;
            _inStream = stream;
        }

        protected override char MessageType => 'Z';

        protected override void SendData(Stream stream) {
            var zs = new GZipStream(stream, CompressionMode.Compress);
            _inStream.CopyTo(zs);
        }
    }

    public class RecZipMessage : RecMessage {
        public RecZipMessage(RecMessageHeader header, Stream stream) : base(header, stream) { }

        public Stream GetDataStream() {
            return new GZipStream(Stream, CompressionMode.Decompress);
        }
    }
}
