using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if DEVICE
using NUnit.Framework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

using Babbacombe.SockLib;

namespace SockLibUnitTests {
#if DEVICE
    [TestFixture]
#else
    [TestClass]
#endif
    public class CryptoTests {

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(20000)]
        public void CryptoTextTransaction() {
            var delimGen = SendMessage.DelimGen;
            SendMessage.DelimGen = new RandomDelimGen();
            try {
                using (Server server = new Server(9000))
                using (Client client = new Client("localhost", 9000, Client.Modes.Transaction, true)) {
                    server.Handlers.Add<RecTextMessage>("Test", echoText);

                    Assert.IsTrue(client.UsingCrypto);
                    client.Open();
                    Assert.IsTrue(client.UsingCrypto);

                    var reply = client.Transaction(new SendTextMessage("Test", "abcde"));
#if DEVICE
                Assert.That(reply, Is.InstanceOf<RecTextMessage>());
#else
                    Assert.IsInstanceOfType(reply, typeof(RecTextMessage));
#endif
                    Assert.AreEqual("Test", reply.Command);
                    Assert.AreEqual("abcde", ((RecTextMessage)reply).Text);

                    client.Close();
                    Assert.IsFalse(client.IsOpen, "ClientClosed");
                }
            } finally {
                SendMessage.DelimGen = delimGen;
            }
        }

        private SendMessage echoText(ServerClient c, RecTextMessage r) {
#if DEVICE
            Assert.That(r, Is.InstanceOf<RecTextMessage>());
#else
            Assert.IsInstanceOfType(r, typeof(RecTextMessage));
#endif

            return new SendTextMessage(r.Command, r.Text);
        }

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(60000)]
        public void CryptoTransferFiles() {
            var delimGen = SendMessage.DelimGen;
            SendMessage.DelimGen = new RandomDelimGen();
            try {
                ClientServerTests.transferFiles(true);
            } finally {
                SendMessage.DelimGen = delimGen;
            }
        }

#if DEVICE
        [Test]
#else
        [TestMethod]
#endif
        [Timeout(120000)]
        public async Task CryptoListening() {
            var delimGen = SendMessage.DelimGen;
            SendMessage.DelimGen = new RandomDelimGen();
            try {
                await ClientServerTests.listening(true);
            } finally {
                SendMessage.DelimGen = delimGen;
            }
        }

    }
}
