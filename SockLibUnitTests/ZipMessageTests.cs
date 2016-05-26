using Babbacombe.SockLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
#if DEVICE
using NUnit.Framework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace SockLibUnitTests {
#if DEVICE
    [TestFixture]
#else
    [TestClass]
#endif
    public class ZipMessageTests {

        private bool isDevice
#if DEVICE
            => true;
#else
            => false;
#endif

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        public void TransferZippedData() {
            RecMessage.CustomMessages.Add('Z', typeof(RecZipMessage));

            using (var f = new RandomFile(isDevice ? 1.Megs() : 10.Megs()))
            using (var fs = f.GetStream()) {
                var msg = new SendZipMessage("TestZipStream", fs);
                var reply = (RecZipMessage)MessageTests.TransferMessage(msg);
                Assert.IsTrue(f.IsEqual(reply.GetDataStream()));
                reply.Stream.Dispose();
            }

            using (var f = new RandomFile(isDevice ? 1.Megs() : 10.Megs(), "\r\n"))
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
            using (var zs = new GZipOutputStream(stream)) {
                zs.IsStreamOwner = false;
                _inStream.CopyTo(zs);
                zs.Finish();
            }
        }
    }

    public class RecZipMessage : RecMessage {
        public RecZipMessage(RecMessageHeader header, Stream stream) : base(header, stream) { }

        public Stream GetDataStream() {
            return new GZipInputStream(Stream);
        }
    }
}
