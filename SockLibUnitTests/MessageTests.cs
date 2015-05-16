using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Babbacombe.SockLib;

namespace SockLibUnitTests {
    [TestClass]
    public class MessageTests {
        [TestMethod]
        public void TextMessage() {
            testTextMessage("TestText", "abcdefg\r\nxyz");
            testTextMessage("qwerty", "abc\r\n");
            byte[] buf = new byte[2000];
            new Random().NextBytes(buf);
            testTextMessage("RandTest", Encoding.UTF8.GetString(buf));
        }

        private void testTextMessage(string cmd, string text) {
            var msg = new SendTextMessage(cmd, text);
            var reply = (RecTextMessage)transferMessage(msg);
            Assert.AreEqual(cmd, reply.Command);
            Assert.AreEqual(text, reply.Text);
            reply.Stream.Dispose();
        }

        private RecMessage transferMessage(SendMessage msg) {
            var s = new MemoryStream();
            msg.Send(s);
            s.Seek(0, SeekOrigin.Begin);
            var ds = new DelimitedStream(s);
            var header = new RecMessageHeader(ds);
            return RecMessage.Create(header, ds);
        }

        [TestMethod]
        public void BinaryMessage() {
            byte[] bin = new byte[1000000];
            new Random().NextBytes(bin);
            var msg = new SendBinaryMessage("TestBin", bin);
            var reply = (RecBinaryMessage)transferMessage(msg);
            Assert.IsFalse(bin.Zip(reply.Data, (sb, rp) => sb == rp).Any(r => false));
            reply.Stream.Dispose();
        }

        [TestMethod]
        public void BinaryMessageStream() {
            using (var f = new RandomFile(10 * 1024 * 1024)) 
            using (var fs = f.GetStream()) {
                var msg = new SendBinaryMessage("TestBinStream", fs);
                var reply = (RecBinaryMessage)transferMessage(msg);
                Assert.IsTrue(f.IsEqual(reply.Stream));
                reply.Stream.Dispose();
            }

            using (var f = new RandomFile(10 * 1024 * 1024, "\r\n"))
            using (var fs = f.GetStream()) {
                var msg = new SendBinaryMessage("TestBinStream", fs);
                var reply = (RecBinaryMessage)transferMessage(msg);
                Assert.IsTrue(f.IsEqual(reply.Stream), "With \\r\\n at end");
                reply.Stream.Dispose();
            }
        }

        [TestMethod]
        public void FilenamesMessage() {
            var names = Enumerable.Range(1, 150).Select(i => Path.GetRandomFileName()).Concat(Enumerable.Range(1, 150).Select(i => Path.GetRandomFileName()));
            var msg = new SendFilenamesMessage("TestFilenames", names);
            var reply = (RecFilenamesMessage)transferMessage(msg);
            Assert.IsTrue(names.Count() == reply.Filenames.Count());
            Assert.IsFalse(names.Zip(reply.Filenames, (sn, rn) => sn == rn).Any(r => false));
        }

        public class RandomFile : IDisposable {
            private FileInfo _info;

            public RandomFile(int length, string eof = null) {
                _info = new FileInfo(Path.GetTempFileName());
                int bufsize = 16 * 1024;
                byte[] buf = new byte[bufsize];
                var r = new Random();
                int rem = length;
                using (var s = _info.OpenWrite()) {
                    while (rem > 0) {
                        r.NextBytes(buf);
                        s.Write(buf, 0, Math.Min(bufsize, rem));
                        rem -= bufsize;
                    }
                    if (eof != null) {
                        var b = Encoding.UTF8.GetBytes(eof);
                        s.Write(b, 0, b.Length);
                    }
                }
            }

            public Stream GetStream() {
                return _info.OpenRead();
            }

            public void Dispose() {
                _info.Delete();
            }

            public bool IsEqual(Stream other) {
                using (var fs = _info.OpenRead()) {
                    int fb;
                    do {
                        fb = fs.ReadByte();
                        int sb = other.ReadByte();
                        if (fb != sb) return false;
                    } while (fb > 0);
                }
                return true;
            }
        }
    }
}
