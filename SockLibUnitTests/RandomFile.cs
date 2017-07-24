#if DEVICE
using NUnit.Framework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SockLibUnitTests {
    public class RandomFile : IDisposable {
        private FileInfo _info;

        public RandomFile(int length, string eof = null, string path = null) {
            _info = new FileInfo(path ?? Path.GetTempFileName());
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
                    if (fb != sb) {
                        Assert.AreEqual(fb, sb, $"fb = {fb}, sb = {sb}");
                        return false;
                    }
                } while (fb >= 0);
            }
            return true;
        }

        public bool IsEqual(string otherFilename) {
            using (var fs = new FileStream(otherFilename, FileMode.Open, FileAccess.Read)) {
                return IsEqual(fs);
            }
        }

        public string Name { get { return _info.FullName; } }
    }
}