using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using Babbacombe.SockLib;
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
    public class SocketTests {

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(300000)]
        public void BigSocketTransfer() {
            const int buflen = 5 * 1024 * 1024;
            var sendbuf = new byte[buflen];
            var rand = new Random();
            rand.NextBytes(sendbuf);

            TcpListener listener = null;
            TcpClient client = null;
            TcpClient serverClient = null;
            try {
                listener = new TcpListener(IPAddress.Any, 9000);
                listener.Start();
                Task.Run(() => {
                    serverClient = listener.AcceptTcpClient();
                    var delim = Encoding.UTF8.GetBytes("---------------------abcdefg\n");
                    serverClient.GetStream().Write(delim, 0, delim.Length);
                    var send = new MemoryStream(sendbuf);
                    //send.CopyTo(serverClient.GetStream());
                    const int sendsize = 8 * 1024;
                    for (int i = 0; i < buflen; i += sendsize) {
                        serverClient.GetStream().Write(sendbuf, i, Math.Min(sendsize, buflen - i));
                    }
                    serverClient.GetStream().WriteByte((byte)'\n');
                    serverClient.GetStream().Write(delim, 0, delim.Length);
                });
                client = new TcpClient();
                client.Connect("localhost", 9000);

                byte[] recbuf;
                using (var ds = new DelimitedStream(client.GetStream()))
                using (var mem = new MemoryStream()) {
                    ds.CopyTo(mem);
                    mem.Seek(0, SeekOrigin.Begin);
                    recbuf = mem.ToArray();
                }

                //int diff = Enumerable.Range(0, buflen).FirstOrDefault(i => sendbuf[i] != recbuf[i]);

                Assert.AreEqual(buflen, recbuf.Length);

                Assert.IsTrue(sendbuf.Zip(recbuf, (s, r) => s == r).All(r => r));
            } finally {
                client.Close();
                serverClient.Close();
                listener.Stop();
            }
        }
    }
}
