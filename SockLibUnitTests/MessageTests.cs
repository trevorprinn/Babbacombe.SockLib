using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Babbacombe.SockLib;

namespace SockLibUnitTests {
    [TestClass]
    public class MessageTests {
        private class TransFile {
            private Stream _stream;
            private string _name;
            public TransFile(string name, Stream stream) {
                _name = name;
                _stream = stream;
            }
            public void Delete() {
                _stream.Dispose();
                File.Delete(_name);
            }
        }
        private static List<TransFile> _tempFiles = new List<TransFile>();

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

        private RecMessage transferMessage(SendMessage msg, bool useFile = false) {
            Stream s;
            if (useFile) {
                var name = Path.GetTempFileName();
                s = new FileStream(name, FileMode.Create, FileAccess.ReadWrite);
                _tempFiles.Add(new TransFile(name, s));
            } else {
                s = new MemoryStream();
            }
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
            using (var f = new RandomFile(10.Megs())) 
            using (var fs = f.GetStream()) {
                var msg = new SendBinaryMessage("TestBinStream", fs);
                var reply = (RecBinaryMessage)transferMessage(msg);
                Assert.IsTrue(f.IsEqual(reply.Stream));
                reply.Stream.Dispose();
            }

            using (var f = new RandomFile(10.Megs(), "\r\n"))
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

        [TestMethod]
        public void MultipartMessage() {
            var r = new Random();
            var files = Enumerable.Range(1, 10).Select(i => new RandomFile(r.Next(50.Megs()), i % 2 == 0 ? "\r\n" : null)).ToArray();
            var bins = Enumerable.Range(1, 5).Select(i => {
                var buf = new byte[r.Next(10.Megs())];
                r.NextBytes(buf);
                return new { Name = Path.GetRandomFileName(), Data = buf };
            }).ToArray();
            List<SendMultipartMessage.BaseItem> items = new List<SendMultipartMessage.BaseItem>(files.Select(f => new SendMultipartMessage.FileItem(f.Name)));
            foreach (var bin in bins) items.Insert(r.Next(items.Count + 1), new SendMultipartMessage.BinaryItem(bin.Name, bin.Data));
            var msg = new SendMultipartMessage("TestMp", items);
            var reply = (RecMultipartMessage)transferMessage(msg, true);
            var man = new MultipartManager(reply.Stream);
            int fcount = 0;
            int bcount = 0;
            man.BinaryUploaded += (s, e) => {
                var bin = bins.Single(b => b.Name == e.Info.Name);
                var pos = 0;
                int buflen = 1.Megs();
                var buf = new byte[buflen];
                int rcount;
                do {
                    rcount = e.Contents.Read(buf, 0, buflen);
                    Assert.IsFalse(bin.Data.Skip(pos).Zip(buf.Take(rcount), (b1, b2) => b1 == b2).Any(res => false));
                    pos += rcount;
                } while (rcount > 0);
                bcount++;
                Assert.IsTrue(pos == bin.Data.Length);
            };
            man.FileUploaded += (s, e) => {
                var file = files.Single(f => f.Name == e.Info.Filename);
                fcount++;
                Assert.IsTrue(file.IsEqual(e.Contents));
            };
            man.Process();
            Assert.AreEqual(10, fcount, "File count");
            Assert.AreEqual(5, bcount, "Binary count");
            foreach (var f in files) f.Dispose();
            reply.Stream.Dispose();
            cleanUp();
        }

        [TestMethod]
        public void MultipartStream() {
            var file = new RandomFile(10.Megs());
            var item = new SendMultipartMessage.FileItem("abc.dat");
            var msg = new SendMultipartMessage("");
            msg.Items.Add(item);
            msg.GetItemStream += (sender, e) => {
                e.Stream = file.GetStream();
            };
            var recMsg = (RecMultipartMessage)transferMessage(msg, true);
            recMsg.Manager.FileUploaded += (sender, e) => {
                Assert.IsTrue(file.IsEqual(e.Contents));
            };
            recMsg.Manager.Process();
            file.Dispose();
            _tempFiles.Clear();
        }

        private static void cleanUp() {
            foreach (var f in _tempFiles) f.Delete();
            _tempFiles.Clear();
        }
    }
}
