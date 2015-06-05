using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// Tests that a ServerClosedException occurs at the client end when the server is closed and
        /// the client attempts a transaction.
        /// </summary>
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
            client.Dispose();
        }

        /// <summary>
        /// Tests a server can send a delayed response to a message from a client.
        /// </summary>
        [TestMethod]
        public void SimpleListen() {
            using (Server server = new Server(9000))
            using (Client client = new Client("localhost", 9000, Client.Modes.Listening)) {
                server.Handlers.Add("Test", echoTextDelayed);
                client.Open();
                int msgCount = 0;

                client.MessageReceived += (s, e) => {
                    msgCount++;
                };

                client.SendMessage(new SendTextMessage("Test"));
                Thread.Sleep(4000);
                Assert.AreEqual(1, msgCount);
            }
        }

        [TestMethod]
        public void CloseClient() {
            using (Server server = new Server(9000)) {
                server.Handlers.Add("Test", echoTextDelayed);

                using (Client client = new Client("localhost", 9000, Client.Modes.Listening)) {
                    client.Open();
                    Thread.Sleep(500);
                    Assert.AreEqual(1, server.Clients.Count());
                    client.SendMessage(new SendTextMessage("Test"));
                }
                // Wait because the server won't attempt to send it back for 3 seconds.
                Thread.Sleep(4000);
                Assert.AreEqual(0, server.Clients.Count());
            }
        }

        private SendMessage echoTextDelayed(ServerClient c, RecMessage r) {
            Thread.Sleep(3000);
            return new SendTextMessage(r.Command, ((RecTextMessage)r).Text);
        }

        /// <summary>
        /// Sends shuffled selections of numbers from the server to a set of clients, and from each
        /// client to the server, and tests that the correct numbers have been received by each in the
        /// correct order.
        /// </summary>
        [TestMethod]
        public async Task Listening() {
            const int clientCount = 5;
            const int serverMsgCount = 50;
            const int clientMsgCount = 50;

            var clients = new List<TestListenClient>();
            using (var server = new TestListenServer(serverMsgCount)) {
                for (int i = 0; i < clientCount; i++) {
                    clients.Add(new TestListenClient(i + 1, clientMsgCount));
                }

                // Wait for all the clients to get connected up, so that
                // they all get broadcast to.
                while (server.Clients.Count() < clientCount) await Task.Delay(1000);
                await Task.Delay(1000);

                List<Task> tasks = new List<Task>();
                
                // Send loads of messages from the clients to the server.
                foreach (var c in clients) tasks.Add(c.Exercise());
                // Broadcast loads of messages from the server to the clients.
                tasks.Add(server.Exercise());

                // Wait until all the messages have been sent.
                await Task.WhenAll(tasks);

                foreach (var c in clients) {
                    // Check the client didn't drop out of listening mode due to an exception.
                    Assert.IsTrue(c.Mode == Client.Modes.Listening);
                    // Check the correct number of messages were received.
                    Assert.AreEqual(clientMsgCount, server.GetRecMessages(c.Ident).Count(), "Server received messages");
                    Assert.AreEqual(serverMsgCount, c.GetRecMessages().Count(), "Client {0} received messages", c.Ident);
                    // Check the correct messages were received at both ends in the correct order.
                    Assert.IsFalse(server.GetRecMessages(c.Ident).Zip(c.GetSentMessages(), (sm, rm) => sm == rm).Any(r => !r), "Server received messages");
                    Assert.IsFalse(c.GetRecMessages().Zip(server.GetSentMessages(), (sm, rm) => sm == rm).Any(r => !r), "Client received messages");
                }

                foreach (var c in clients) c.Dispose();
            }
        }

        [TestMethod]
        public void TransferFiles() {
            var sendFiles = new List<RandomFile>(Enumerable.Range(1, 10).Select(i => new RandomFile(5.Megs())));
            var recFiles = new List<string>();

            using (Server server = new Server(9000)) 
            using (Client client = new Client("localhost", 9000)) {
                client.Open();

                server.Handlers.Add("GetNames", (c, m) => {
                    return new SendFilenamesMessage("Files", sendFiles.Select(f => f.Name));
                });

                var namesReply = (RecFilenamesMessage)client.Transaction(new SendTextMessage("GetNames"));
                var mpReply = (RecMultipartMessage)client.Transaction(new SendFilenamesMessage("GetFiles", namesReply.Filenames));
                mpReply.Manager.FileUploaded += (s, e) => {
                    string fname = Path.GetTempFileName();
                    recFiles.Add(fname);
                    using (var fs = new FileStream(fname, FileMode.Create, FileAccess.Write)) {
                        e.Contents.CopyTo(fs);
                    } 
                };
                mpReply.Manager.Process();
            }

            Assert.AreEqual(sendFiles.Count, recFiles.Count);
            Assert.IsTrue(sendFiles.Zip(recFiles, (sf, rf) => sf.IsEqual(rf)).All(r => r));

            foreach (var f in sendFiles.ToArray()) f.Dispose();
            foreach (var f in recFiles) File.Delete(f);
        }

        [TestMethod]
        public void AutoHandle() {
            using (Server server = new Server(9000))
            using (Client client = new Client("localhost", 9000)) {
                server.Handlers.Add("request", (sc, msg) => {
                    return new SendTextMessage("reply");
                });
                server.Handlers.Add("badrequest", (sc, msg) => {
                    return new SendTextMessage("badreply");
                });

                Assert.IsTrue(client.Open());

                int counter = 0;
                client.Handlers.Add("reply", (c, r) => {
                    counter++;
                });

                var reply = client.Transaction(new SendTextMessage("request"));
                Assert.AreEqual(0, counter);
                Assert.IsFalse(client.AutoHandled);

                client.AutoHandle = true;
                reply = client.Transaction(new SendTextMessage("request"));
                Assert.AreEqual(1, counter);
                Assert.IsTrue(client.AutoHandled);

                reply = client.Transaction(new SendTextMessage("badrequest"));
                Assert.AreEqual(1, counter);
                Assert.IsFalse(client.AutoHandled);

                client.AutoHandle = false;
                reply = client.Transaction(new SendTextMessage("request"));
                Assert.AreEqual(1, counter);
                Assert.IsFalse(client.AutoHandled);
            }
        }
    }
}
