using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Babbacombe.SockLib;

namespace SockLibUnitTests {
    [TestClass]
    public class ClientServerTests {

        /// <summary>
        /// Test a single transaction works with a single client.
        /// </summary>
        [TestMethod]
        public void OneTextTransaction() {
            using (Server server = new Server(9000)) 
            using (Client client = new Client("localhost", 9000)) {
                server.Handlers.Add("Test", echoText);

                Assert.IsFalse(client.IsOpen, "IsOpen");
                client.Open();
                Assert.IsTrue(client.IsOpen, "IsOpen");

                Thread.Sleep(100);
                Assert.AreEqual(1, server.Clients.Count());

                var reply = client.Transaction(new SendTextMessage("Test", "abcde"));
                Assert.IsInstanceOfType(reply, typeof(RecTextMessage));
                Assert.AreEqual("Test", reply.Command);
                Assert.AreEqual("abcde", ((RecTextMessage)reply).Text);

                client.Close();
                Assert.IsFalse(client.IsOpen, "ClientClosed");

                Thread.Sleep(100);
                Assert.AreEqual(0, server.Clients.Count());
            }
        }

        private SendMessage echoText(ServerClient c, RecMessage r) {
            Assert.IsInstanceOfType(r, typeof(RecTextMessage));

            return new SendTextMessage(r.Command, ((RecTextMessage)r).Text);
        }

        /// <summary>
        /// Tests multiple clients sending multiple transactions get back the correct replies.
        /// </summary>
        [TestMethod]
        public void MultipleClientTextTransactions() {
            const int clientCount = 25;
            const int transCount = 25;
            using (Server server = new Server(9000)) {
                server.Handlers.Add("Test", echoText);

                List<Client> clients = new List<Client>();
                for (int i = 0; i < clientCount; i++) {
                    var client = new Client("localhost", 9000);
                    client.Open();
                    clients.Add(client);
                }
                Parallel.ForEach(clients, client => {
                    int cno = clients.IndexOf(client);
                    Parallel.For(1, transCount, tno => {
                        string text = string.Format("Client: {0}, Transaction {1}", cno, tno);
                        var reply = (RecTextMessage)client.Transaction(new SendTextMessage("Test", text));
                        Assert.AreEqual(text, reply.Text);
                    });
                });
                clients.ForEach(c => c.Dispose());
            }
        }

        [TestMethod]
        public void CloseServer() {
            Client client;
            using (Server server = new Server(9000)) {
                server.Handlers.Add("Test", echoText);
                client = new Client("localhost", 9000);
                client.Open();

                client.Transaction(new SendTextMessage("Test", "abc"));
            }
            try {
                client.Transaction(new SendTextMessage("Test", "xyz"));
            } catch (ServerClosedException) { }
        }
    }
}
